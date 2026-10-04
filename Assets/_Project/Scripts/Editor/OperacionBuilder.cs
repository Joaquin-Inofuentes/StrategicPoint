using System.Collections.Generic;
using System.IO;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using SP.Actors;
using SP.Ai;
using SP.Combat;
using SP.Mision;
using SP.Operacion;
using SP.Presentation;
using SP.Vehicles;

namespace SP.EditorTools
{
    // "Operacion Cuartel": nivel NUEVO de blockout (SC_Operacion). Todo el escenario es cubos (con colliders y NavMesh real);
    // los unicos modelos reales son los soldados (escuadra y enemigos). Se arma partiendo de una copia de SC_Gameplay (de ahi salen el
    // HUD, la camara, la escuadra y los sistemas) y se borra todo el nivel viejo. Es IDEMPOTENTE: cada corrida deja la escena igual.
    //
    // MAPA (x lateral, z hacia el norte; suelo de 780 x 780 m centrado en el origen; NavService trabaja en +-400):
    //
    //   z -380..-340  Aproximacion (adentro del "valle": muralla perimetral x=+-61). Aca arranca la escuadra.
    //   z -340..-220  1. CUARTEL: brecha sur, 6 barracas, comedor, parque motor, comando, torres. ~20 enemigos.
    //   z -220        Porton norte (se abre al matar a todos)
    //   z -185/-150/-115  2. TRES PUESTOS DE CONTROL: muro transversal a todo el valle + barrera + panel (solo el FLANQUEADOR)
    //   z  -80..-40   3. CENTRO DE DATOS: edificio sin techo, 3 puertas, computadora (30 s) y oleadas desde los patios
    //   z  -40..60    Salida y patio del TANQUE (el jugador va de artillero)
    //   carretera     4. AUTOPISTA ~420 m: norte y despues al este hasta la ciudad (60 s a 7 m/s)
    //   x 262..382    5. CIUDAD CHICA: plaza (helipad), dos calles, 16 casas, 40 s de resistencia
    //   helicoptero   6. Llega volando desde el NE, se posa en la plaza (no esta estacionado) y se sube con [E]
    public static partial class OperacionBuilder
    {
        const string EscenaOrigen = "Assets/_Project/Scenes/SC_Gameplay.unity";
        const string EscenaDestino = "Assets/_Project/Scenes/SC_Operacion.unity";
        const string CarpetaMat = "Assets/_Project/Materials/Operacion";
        const string PrefabEnemigo = "Assets/_Project/Prefabs/P_Soldier_Enemy.prefab";
        const string PrefabTanque = "Assets/_Project/Prefabs/P_Vehicle_Blindado.prefab";
        const int Indestructible = 999999;

        static Transform raiz, rEnemigos, rWaypoints, rReservas;
        static readonly Dictionary<string, Material> mats = new Dictionary<string, Material>();
        static Font fuente;
        static int contadorEnemigos;

        // ---------------------------------------------------------------
        // Materiales
        // ---------------------------------------------------------------
        static Material Mat(string clave, Color c, float emision = 0f)
        {
            if (mats.TryGetValue(clave, out var m) && m != null) return m;
            if (!AssetDatabase.IsValidFolder(CarpetaMat)) AssetDatabase.CreateFolder("Assets/_Project/Materials", "Operacion");
            string path = $"{CarpetaMat}/M_Op_{clave}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "M_Op_" + clave };
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.SetColor("_BaseColor", c);
            mat.SetFloat("_Smoothness", 0.08f);
            if (emision > 0f)
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", c * emision);
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            EditorUtility.SetDirty(mat);
            mats[clave] = mat;
            return mat;
        }

        // Material de los carteles (ver OperacionTextoMundo): Sprites/Default, la textura de la fuente la pone ese componente.
        static Material MatTexto()
        {
            const string path = CarpetaMat + "/M_Op_Texto.mat";
            if (!AssetDatabase.IsValidFolder(CarpetaMat)) AssetDatabase.CreateFolder("Assets/_Project/Materials", "Operacion");
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(Shader.Find("SP/OperacionTexto")) { name = "M_Op_Texto" };
                AssetDatabase.CreateAsset(m, path);
            }
            m.shader = Shader.Find("SP/OperacionTexto");
            return m;
        }

        static Material Suelo => Mat("Suelo", new Color(0.50f, 0.58f, 0.42f));
        static Material Asfalto => Mat("Asfalto", new Color(0.20f, 0.21f, 0.23f));
        static Material Cemento => Mat("Cemento", new Color(0.68f, 0.68f, 0.66f));
        static Material CementoOsc => Mat("CementoOsc", new Color(0.46f, 0.47f, 0.50f));
        static Material Amarillo => Mat("Amarillo", new Color(1f, 0.82f, 0.1f), 0.35f);
        static Material Blanco => Mat("Blanco", new Color(0.95f, 0.95f, 0.95f));
        static Material Rojo => Mat("Rojo", new Color(0.85f, 0.12f, 0.1f));
        static Material Naranja => Mat("Naranja", new Color(1f, 0.5f, 0.08f), 0.3f);
        static Material AzulSenal => Mat("AzulSenal", new Color(0.08f, 0.32f, 0.78f));
        static Material VerdeSenal => Mat("VerdeSenal", new Color(0.07f, 0.55f, 0.25f));
        static Material Barraca => Mat("Barraca", new Color(0.55f, 0.58f, 0.40f));
        static Material Techo => Mat("Techo", new Color(0.36f, 0.38f, 0.30f));
        static Material Casa => Mat("Casa", new Color(0.80f, 0.70f, 0.54f));
        static Material CasaB => Mat("CasaB", new Color(0.66f, 0.74f, 0.82f));
        static Material CasaC => Mat("CasaC", new Color(0.82f, 0.60f, 0.52f));
        static Material Datos => Mat("Datos", new Color(0.55f, 0.66f, 0.78f));
        static Material Rack => Mat("Rack", new Color(0.12f, 0.14f, 0.18f), 0f);
        static Material RackLuz => Mat("RackLuz", new Color(0.2f, 0.9f, 1f), 1.6f);
        static Material Lampara => Mat("Lampara", new Color(1f, 0.92f, 0.65f), 2.2f);
        static Material Pantalla => Mat("Pantalla", new Color(1f, 0.15f, 0.1f), 2.2f);
        static Material Oliva => Mat("Oliva", new Color(0.30f, 0.36f, 0.22f));
        static Material Negro => Mat("Negro", new Color(0.07f, 0.07f, 0.08f));
        static Material Cristal => Mat("Cristal", new Color(0.35f, 0.6f, 0.75f));
        static Material Cobertura => Mat("Cobertura", new Color(0.62f, 0.52f, 0.34f));
        static Material AutoRojo => Mat("AutoRojo", new Color(0.72f, 0.12f, 0.1f));

        // ---------------------------------------------------------------
        // Primitivas de blockout
        // ---------------------------------------------------------------
        static GameObject Cubo(Transform padre, string nombre, float x, float z, float w, float h, float d, Material m, float yaw = 0f, float y0 = 0f, bool colision = true, int vida = 0)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = nombre;
            go.transform.SetParent(padre, false);
            go.transform.position = new Vector3(x, y0 + h * 0.5f, z);
            go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            go.transform.localScale = new Vector3(w, h, d);
            go.GetComponent<MeshRenderer>().sharedMaterial = m;
            if (!colision)
            {
                Object.DestroyImmediate(go.GetComponent<BoxCollider>());
                var mod = go.AddComponent<NavMeshModifier>(); mod.ignoreFromBuild = true;
            }
            else if (vida > 0)
            {
                var marca = go.AddComponent<ObstacleMarker>();
                marca.ConfigurarVida(vida);
            }
            return go;
        }

        // Pared/edificio: solido, aparece en el minimapa y como cobertura de la IA (indestructible).
        static GameObject Solido(Transform p, string n, float x, float z, float w, float h, float d, Material m, float yaw = 0f, float y0 = 0f)
            => Cubo(p, n, x, z, w, h, d, m, yaw, y0, true, 0);
        static GameObject Edificio(Transform p, string n, float x, float z, float w, float h, float d, Material m)
            => Cubo(p, n, x, z, w, h, d, m, 0f, 0f, true, Indestructible);
        // Cobertura baja destructible.
        static GameObject Saco(Transform p, string n, float x, float z, bool horizontal = true, int vida = 300)
            => Cubo(p, n, x, z, horizontal ? 3f : 1f, 1.2f, horizontal ? 1f : 3f, Cobertura, 0f, 0f, true, vida);
        static GameObject Pintura(Transform p, string n, float x, float z, float w, float d, Material m, float yaw = 0f, float y0 = 0.075f, float h = 0.045f)
            => Cubo(p, n, x, z, w, h, d, m, yaw, y0, false);
        static GameObject Decor(Transform p, string n, float x, float z, float w, float h, float d, Material m, float yaw = 0f, float y0 = 0f)
            => Cubo(p, n, x, z, w, h, d, m, yaw, y0, false);

        static Transform Grupo(Transform padre, string nombre)
        {
            var g = new GameObject(nombre).transform;
            g.SetParent(padre, false);
            return g;
        }

        // Tramo de muro entre dos X en una Z (o entre dos Z en una X).
        static GameObject MuroX(Transform p, string n, float x0, float x1, float z, float h = 4f, float grosor = 2f, Material m = null)
            => Solido(p, n, (x0 + x1) * 0.5f, z, x1 - x0, h, grosor, m ?? Cemento);
        static GameObject MuroZ(Transform p, string n, float x, float z0, float z1, float h = 4f, float grosor = 2f, Material m = null)
            => Solido(p, n, x, (z0 + z1) * 0.5f, grosor, h, z1 - z0, m ?? Cemento);

        // Cartel de texto en el mundo: placa + TextMesh. "yaw" = hacia donde MIRA quien lo lee (0 = mira al norte).
        static GameObject Rotulo(Transform p, string texto, float x, float y, float z, float yaw, float tam = 1f, Color? color = null, Material placa = null, float ancho = -1f, float alto = -1f)
        {
            if (fuente == null) fuente = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var raizR = new GameObject("Rotulo_" + texto.Replace("\n", " ")).transform;
            raizR.SetParent(p, false);
            raizR.position = new Vector3(x, y, z);
            raizR.rotation = Quaternion.Euler(0f, yaw, 0f);
            int lineas = texto.Split('\n').Length;
            int maxLen = 0; foreach (var l in texto.Split('\n')) maxLen = Mathf.Max(maxLen, l.Length);
            float w = ancho > 0f ? ancho : 0.62f * tam * maxLen * 0.42f + 0.6f;
            float h = alto > 0f ? alto : 0.8f * tam * lineas + 0.4f;
            if (placa != null)
            {
                var pl = GameObject.CreatePrimitive(PrimitiveType.Cube);
                pl.name = "Placa"; pl.transform.SetParent(raizR, false);
                pl.transform.localPosition = new Vector3(0f, 0f, 0.07f);
                pl.transform.localScale = new Vector3(w, h, 0.12f);
                pl.GetComponent<MeshRenderer>().sharedMaterial = placa;
                Object.DestroyImmediate(pl.GetComponent<BoxCollider>());
                pl.AddComponent<NavMeshModifier>().ignoreFromBuild = true;
            }
            var t = new GameObject("Texto");
            t.transform.SetParent(raizR, false);
            t.transform.localPosition = Vector3.zero;
            var tm = t.AddComponent<TextMesh>();
            tm.font = fuente; tm.text = texto; tm.fontSize = 64; tm.characterSize = 0.1f * tam;
            tm.anchor = TextAnchor.MiddleCenter; tm.alignment = TextAlignment.Center; tm.fontStyle = FontStyle.Bold;
            tm.color = color ?? Color.white;
            var mr = t.GetComponent<MeshRenderer>();
            mr.sharedMaterial = MatTexto();
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            t.AddComponent<NavMeshModifier>().ignoreFromBuild = true;
            raizR.gameObject.AddComponent<NavMeshModifier>().ignoreFromBuild = true;
            return raizR.gameObject;
        }

        static void Farola(Transform p, float x, float z, bool conLuz, float alto = 6f)
        {
            Solido(p, "Farola_Poste", x, z, 0.3f, alto, 0.3f, CementoOsc);
            Decor(p, "Farola_Cabeza", x, z, 1.1f, 0.3f, 0.7f, Lampara, 0f, alto);
            if (conLuz)
            {
                var l = new GameObject("Farola_Luz");
                l.transform.SetParent(p, false);
                l.transform.position = new Vector3(x, alto - 0.4f, z);
                var li = l.AddComponent<Light>();
                li.type = LightType.Point; li.color = new Color(1f, 0.93f, 0.75f); li.intensity = 3.2f; li.range = 16f;
            }
        }

        static void LuzPuntual(Transform p, string n, float x, float y, float z, Color c, float intensidad, float rango)
        {
            var l = new GameObject(n);
            l.transform.SetParent(p, false);
            l.transform.position = new Vector3(x, y, z);
            var li = l.AddComponent<Light>();
            li.type = LightType.Point; li.color = c; li.intensity = intensidad; li.range = rango;
        }

        // Flecha amarilla pintada en el piso (la senalizacion del camino): apunta hacia "yaw".
        static void FlechaPiso(Transform p, float x, float z, float yaw)
        {
            var g = new GameObject("FlechaPiso").transform;
            g.SetParent(p, false);
            g.position = new Vector3(x, 0f, z);
            g.rotation = Quaternion.Euler(0f, yaw, 0f);
            Pintura(g, "Cuerpo", 0, 0, 0.7f, 3.2f, Amarillo).transform.localPosition = new Vector3(0f, 0.05f, -1f);
            var a = Pintura(g, "AlaI", 0, 0, 0.7f, 2.4f, Amarillo); a.transform.localPosition = new Vector3(-0.8f, 0.05f, 0.5f); a.transform.localRotation = Quaternion.Euler(0f, 40f, 0f);
            var b = Pintura(g, "AlaD", 0, 0, 0.7f, 2.4f, Amarillo); b.transform.localPosition = new Vector3(0.8f, 0.05f, 0.5f); b.transform.localRotation = Quaternion.Euler(0f, -40f, 0f);
        }

        // ---------------------------------------------------------------
        // Entrada principal
        // ---------------------------------------------------------------
        [MenuItem("Strategic Point/Operacion/Construir nivel SC_Operacion")]
        public static void Construir()
        {
            var activa = EditorSceneManager.GetActiveScene();
            if (activa.isDirty) { Debug.LogError("[Operacion] La escena abierta tiene cambios sin guardar: guardala antes de construir (no se toco nada)."); return; }
            if (!File.Exists(EscenaDestino)) AssetDatabase.CopyAsset(EscenaOrigen, EscenaDestino);
            var scene = EditorSceneManager.OpenScene(EscenaDestino, OpenSceneMode.Single);
            mats.Clear();
            contadorEnemigos = 0;

            LimpiarEscena();
            ConfigurarAmbiente();

            raiz = new GameObject("Operacion").transform;
            rEnemigos = new GameObject("Enemies").transform;
            rWaypoints = new GameObject("Waypoints").transform;
            rReservas = Grupo(raiz, "Reservas");
            var vehiculos = new GameObject("Vehicles").transform;

            AjustarSuelo();
            var dir = raiz.gameObject.AddComponent<OperacionDirector>();
            var texto = raiz.gameObject.AddComponent<OperacionTextoMundo>();
            texto.material = MatTexto();

            Perimetro(raiz);
            var cuartel = Cuartel(raiz, out var enemigosCuartel, out var porton);
            var puestos = Puestos(raiz);
            var computadora = CentroDeDatos(raiz, out var oleadasCentro);
            var tanque = PatioDelTanque(raiz, vehiculos, out var oleadaHuida);
            var ruta = Autopista(raiz);
            var autoPlantilla = CrearPlantillaDeAuto();
            var ciudad = Ciudad(raiz, out var oleadasCiudad, out var plaza, out var helipad);
            var heli = CrearHelicoptero(raiz, out var heliInicio);

            var entradas = new Transform[6];
            string[] nombresEntradas = { "Infiltrar", "Puestos", "Datos", "Huir", "Resistir", "Extraer" };
            Vector3[] posEntradas = { new Vector3(0, 0, -373), new Vector3(0, 0, -212), new Vector3(0, 0, -98), new Vector3(0, 0, 8), new Vector3(246, 0, 228), new Vector3(318, 0, 214) };
            float[] rotEntradas = { 0f, 0f, 0f, 0f, 90f, 0f };
            var rEntradas = Grupo(raiz, "Entradas_Fase");
            for (int i = 0; i < entradas.Length; i++)
            {
                var e = new GameObject("Entrada_" + nombresEntradas[i]).transform;
                e.SetParent(rEntradas, false);
                e.SetPositionAndRotation(posEntradas[i], Quaternion.Euler(0f, rotEntradas[i], 0f));
                entradas[i] = e;
            }

            // Director
            dir.enemigosCuartel = enemigosCuartel;
            dir.portonCuartel = porton;
            dir.entradas = entradas;
            dir.puestos = puestos;
            dir.computadora = computadora;
            dir.oleadasDelCentro = oleadasCentro;
            dir.oleadaDeHuida = oleadaHuida;
            dir.tanque = tanque;
            dir.rutaDelTanque = ruta;
            dir.autoPlantilla = autoPlantilla;
            dir.segundosDeTrayecto = 52f;
            dir.autosSimultaneos = 3;
            dir.oleadasDeLaCiudad = oleadasCiudad;
            dir.segundosDeResistencia = 40f;
            dir.plaza = plaza;
            dir.heli = heli;
            dir.heliInicio = heliInicio;
            dir.heliAterrizaje = helipad;
            EditorUtility.SetDirty(dir);

            ColocarEscuadra();
            ConectarVehiculoDelJugador(tanque);
            ConfigurarCamaras();
            LevelBlockoutBuilder.AjustarAlcancesDeCombate();
            LevelBlockoutBuilder.BakeNavMesh();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[Operacion] Nivel listo: {contadorEnemigos} soldados enemigos, {raiz.GetComponentsInChildren<Transform>().Length} objetos de blockout, NavMesh horneado y escena guardada ({EscenaDestino}).");
        }

        // ---------------------------------------------------------------
        // Escena base
        // ---------------------------------------------------------------
        static void LimpiarEscena()
        {
            string[] raices = { "Enemies", "Waypoints", "Vehicles", "Weapons", "Nivel_Blockout", "Blocking", "Arte", "ArteMundo", "Ambientacion",
                                "PostProceso_Noche", "Estrategia", "Mision", "CinematicaDeIntro", "Operacion" };
            foreach (var g in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (System.Array.IndexOf(raices, g.name) >= 0) { Object.DestroyImmediate(g); continue; }
                if (g.name == "Environment")
                {
                    foreach (var n in new[] { "Terrain_Main", "Terrain_Fondo" })
                    {
                        var t = g.transform.Find(n);
                        if (t != null) Object.DestroyImmediate(t.gameObject);
                    }
                }
                else if (g.name == "UI_World")
                {
                    for (int i = g.transform.childCount - 1; i >= 0; i--)
                    {
                        var c = g.transform.GetChild(i);
                        if (c.name.Contains("MinimapIcon")) Object.DestroyImmediate(c.gameObject);
                    }
                }
            }
        }

        static void ConfigurarAmbiente()
        {
            // Dia claro y despejado: el blockout tiene que leerse bien (camino iluminado y senalizado).
            var sunGo = GameObject.Find("SunLight");
            if (sunGo != null)
            {
                var sun = sunGo.GetComponent<Light>();
                sun.color = new Color(1f, 0.96f, 0.88f);
                sun.intensity = 1.05f;
                sun.shadows = LightShadows.Soft;
                sunGo.transform.rotation = Quaternion.Euler(52f, -28f, 0f);
                RenderSettings.sun = sun;
            }
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.56f, 0.60f, 0.66f);
            RenderSettings.fog = false;
            if (!AssetDatabase.IsValidFolder(CarpetaMat)) AssetDatabase.CreateFolder("Assets/_Project/Materials", "Operacion");
            var skyPath = CarpetaMat + "/M_Skybox_Dia_Operacion.mat";
            var sky = AssetDatabase.LoadAssetAtPath<Material>(skyPath);
            if (sky == null)
            {
                sky = new Material(Shader.Find("Skybox/Procedural"));
                AssetDatabase.CreateAsset(sky, skyPath);
            }
            sky.SetFloat("_AtmosphereThickness", 0.9f);
            sky.SetFloat("_Exposure", 1.15f);
            EditorUtility.SetDirty(sky);
            RenderSettings.skybox = sky;
            DynamicGI.UpdateEnvironment();
        }

        static void AjustarSuelo()
        {
            var ground = GameObject.Find("Ground");
            if (ground == null)
            {
                ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
                ground.name = "Ground";
                var env = GameObject.Find("Environment");
                if (env != null) ground.transform.SetParent(env.transform, false);
            }
            ground.transform.position = new Vector3(0f, -0.5f, 0f);
            ground.transform.rotation = Quaternion.identity;
            ground.transform.localScale = new Vector3(780f, 1f, 780f);
            var gr = ground.GetComponent<MeshRenderer>();
            gr.enabled = true;   // en SC_Gameplay el cubo era solo collider (el piso era el Terrain)
            gr.sharedMaterial = Suelo;
        }

        static void ColocarEscuadra()
        {
            var squad = GameObject.Find("PlayerSquad");
            if (squad == null) return;
            int i = 0;
            foreach (Transform s in squad.transform)
            {
                // Roles de la escuadra: el FLANQUEADOR (Kes) es el unico que puede desactivar los puestos de control.
                var soldado = s.GetComponent<Soldier>();
                if (soldado != null)
                {
                    RoleType rol = s.name.Contains("Kes") ? RoleType.Flanker : s.name.Contains("Doc") ? RoleType.Medic : RoleType.Assault;
                    var so = new SerializedObject(soldado);
                    so.FindProperty("role").enumValueIndex = (int)rol;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(soldado);
                }
                s.position = new Vector3(-2.5f + i * 2.5f, 0.8f, -372f - i * 0.8f);
                s.rotation = Quaternion.identity;
                i++;
            }
        }

        // El PlayerInputDriver guarda "su" vehiculo (campo serializado): sin esto no puede subir a ningun tanque.
        static void ConectarVehiculoDelJugador(Vehicle v)
        {
            var driver = Object.FindFirstObjectByType<SP.Player.PlayerInputDriver>(FindObjectsInactive.Include);
            if (driver == null) { Debug.LogWarning("[Operacion] No hay PlayerInputDriver."); return; }
            var so = new SerializedObject(driver);
            var p = so.FindProperty("Vehicle");
            if (p != null) { p.objectReferenceValue = v; so.ApplyModifiedPropertiesWithoutUndo(); }
            EditorUtility.SetDirty(driver);
        }

        static void ConfigurarCamaras()
        {
            var cam = GameObject.Find("MainCamera");
            if (cam != null) cam.transform.position = new Vector3(0f, 2.3f, -377f);
            var rts = GameObject.Find("RtsCamaraInicial");
            if (rts != null) rts.transform.position = new Vector3(0f, 30f, -373f);
            var mini = GameObject.Find("MinimapCamera");
            if (mini != null) mini.transform.position = new Vector3(0f, 60f, -373f);
        }

        static void AgregarAlBuild()
        {
            var lista = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            foreach (var s in lista) if (s.path == EscenaDestino) return;
            lista.Add(new EditorBuildSettingsScene(EscenaDestino, true));
            EditorBuildSettings.scenes = lista.ToArray();
        }

        // ---------------------------------------------------------------
        // Enemigos
        // ---------------------------------------------------------------
        static Soldier Enemigo(string nombre, float x, float z, float yaw, Transform padre = null, bool conRonda = true, float ronda = 5f, bool reserva = false)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabEnemigo);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, padre != null ? padre : rEnemigos);
            go.name = nombre;
            go.transform.position = new Vector3(x, 0.8f, z);
            go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            var brain = go.GetComponent<AiBrain>();
            if (brain != null)
            {
                if (conRonda && !reserva)
                {
                    var puntos = new[]
                    {
                        new Vector3(x - ronda, 0f, z - ronda), new Vector3(x + ronda, 0f, z - ronda),
                        new Vector3(x + ronda, 0f, z + ronda), new Vector3(x - ronda, 0f, z + ronda),
                    };
                    // Puntos de ronda como Transforms vacios (sin esferas visibles: es un blockout limpio).
                    var rondaRaiz = new GameObject("Ronda_" + nombre).transform;
                    rondaRaiz.SetParent(rWaypoints, false);
                    var marcas = new Transform[puntos.Length];
                    for (int i = 0; i < puntos.Length; i++)
                    {
                        var m = new GameObject("P" + (i + 1)).transform;
                        m.SetParent(rondaRaiz, false);
                        m.position = puntos[i];
                        marcas[i] = m;
                    }
                    brain.SetPatrolWaypoints(marcas);
                }
                else brain.SetPatrolWaypoints(null);
                EditorUtility.SetDirty(brain);
            }
            if (reserva) go.SetActive(false);
            contadorEnemigos++;
            return go.GetComponent<Soldier>();
        }

        // ---------------------------------------------------------------
        // Perimetro del "valle" militar + borde del mapa
        // ---------------------------------------------------------------
        static void Perimetro(Transform padre)
        {
            var g = Grupo(padre, "0_Perimetro");
            // Valle del cuartel: x=+-61, z -382..-38. Cierra todo el camino: no se puede rodear los puestos de control.
            MuroZ(g, "Valle_Oeste", -61f, -382f, -38f, 6f, 2f, CementoOsc);
            MuroZ(g, "Valle_Este", 61f, -382f, -38f, 6f, 2f, CementoOsc);
            MuroX(g, "Valle_Sur", -62f, 62f, -381f, 6f, 2f, CementoOsc);
            // Borde del mapa (invisible para el juego, pero solido): evita caerse del suelo de 780 m.
            MuroX(g, "Borde_Norte", -391f, 391f, 389f, 8f, 2f, CementoOsc);
            MuroX(g, "Borde_Sur", -391f, 391f, -389f, 8f, 2f, CementoOsc);
            MuroZ(g, "Borde_Oeste", -389f, -391f, 391f, 8f, 2f, CementoOsc);
            MuroZ(g, "Borde_Este", 389f, -391f, 391f, 8f, 2f, CementoOsc);

            // Aproximacion sur: camino de tierra hasta la brecha, con carteles.
            var ap = Grupo(padre, "1a_Aproximacion");
            Pintura(ap, "Camino_Aproximacion", 0f, -359f, 10f, 40f, Asfalto, 0f, 0.0f, 0.08f);
            for (int k = 0; k < 5; k++) Pintura(ap, "Linea_Aprox_" + k, 0f, -376f + k * 8f, 0.35f, 3.5f, Amarillo);
            Rotulo(ap, "ZONA MILITAR\nCUARTEL ENEMIGO", 0f, 7.4f, -362f, 0f, 1.5f, Color.white, Rojo, 15f, 4.2f);
            Solido(ap, "Poste_Zona_L", -7.6f, -362f, 0.4f, 9.5f, 0.4f, CementoOsc);
            Solido(ap, "Poste_Zona_R", 7.6f, -362f, 0.4f, 9.5f, 0.4f, CementoOsc);
            Rotulo(ap, "INFILTRATE POR LA BRECHA  ^\n(elimina a TODOS los enemigos)", 0f, 7.6f, -350f, 0f, 1.1f, Color.white, AzulSenal, 16f, 3.4f);
            Solido(ap, "Poste_Brecha_L", -7.6f, -350f, 0.4f, 9.5f, 0.4f, CementoOsc);
            Solido(ap, "Poste_Brecha_R", 7.6f, -350f, 0.4f, 9.5f, 0.4f, CementoOsc);
            FlechaPiso(ap, 0f, -358f, 0f); FlechaPiso(ap, 0f, -348f, 0f);
            Farola(ap, -7f, -370f, true); Farola(ap, 7f, -370f, true); Farola(ap, -7f, -346f, true); Farola(ap, 7f, -346f, true);
        }

        // ---------------------------------------------------------------
        // 1. Cuartel
        // ---------------------------------------------------------------
        static Transform Cuartel(Transform padre, out Soldier[] enemigos, out GameObject porton)
        {
            var g = Grupo(padre, "1_Cuartel");
            var estructura = Grupo(g, "Estructuras");
            // Muro sur con la brecha (x -8..8) y muro norte con el porton (x -7..7).
            MuroX(estructura, "Cuartel_Sur_O", -61f, -8f, -340f, 4f, 2f);
            MuroX(estructura, "Cuartel_Sur_E", 8f, 61f, -340f, 4f, 2f);
            MuroX(estructura, "Cuartel_Norte_O", -61f, -7f, -220f, 4f, 2f);
            MuroX(estructura, "Cuartel_Norte_E", 7f, 61f, -220f, 4f, 2f);
            Solido(estructura, "Brecha_Pilar_O", -8.6f, -340f, 1.6f, 6f, 1.6f, CementoOsc);
            Solido(estructura, "Brecha_Pilar_E", 8.6f, -340f, 1.6f, 6f, 1.6f, CementoOsc);
            Solido(estructura, "Porton_Pilar_O", -7.6f, -220f, 1.8f, 7f, 1.8f, CementoOsc);
            Solido(estructura, "Porton_Pilar_E", 7.6f, -220f, 1.8f, 7f, 1.8f, CementoOsc);
            Decor(estructura, "Porton_Dintel", 0f, -220f, 16f, 0.8f, 1.6f, CementoOsc, 0f, 6.2f);
            Rotulo(estructura, "SALIDA NORTE", 0f, 6.6f, -220.95f, 0f, 1.2f, Color.white, Rojo, 9f, 1.6f);
            Rotulo(estructura, "SALIDA NORTE", 0f, 6.6f, -219.05f, 180f, 1.2f, Color.white, Rojo, 9f, 1.6f);
            Rotulo(estructura, "BRECHA", 0f, 6.6f, -341.15f, 0f, 1.2f, Color.white, Naranja, 7f, 1.6f);

            // Porton: barrera roja y blanca que cierra el paso hasta que cae el ultimo enemigo.
            porton = Solido(estructura, "Porton_Cuartel", 0f, -220f, 14f, 3.4f, 1.2f, Rojo);
            for (int i = -3; i <= 3; i++) Decor(porton.transform, "Franja", 0f, 0f, 1f, 1f, 1f, Blanco).transform.SetPositionAndRotation(new Vector3(i * 1.9f, 1.7f, -220f), Quaternion.identity);
            foreach (Transform t in porton.transform) t.localScale = new Vector3(0.9f / 14f, 1.02f, 1.1f);

            // Barracas: 3 al oeste y 3 al este.
            string[] letras = { "A", "B", "C" };
            for (int i = 0; i < 3; i++)
            {
                float z = -322f + i * 24f;
                var b1 = Edificio(estructura, "Barraca_O_" + letras[i], -48f, z, 20f, 5f, 12f, Barraca);
                var b2 = Edificio(estructura, "Barraca_E_" + letras[i], 48f, z, 20f, 5f, 12f, Barraca);
                Decor(estructura, "Techo_O_" + letras[i], -48f, z, 20.6f, 0.5f, 12.6f, Techo, 0f, 5f);
                Decor(estructura, "Techo_E_" + letras[i], 48f, z, 20.6f, 0.5f, 12.6f, Techo, 0f, 5f);
                Rotulo(estructura, "BARRACA " + letras[i], -37.8f, 3.2f, z, 270f, 1.1f, Color.white, AzulSenal, 8f, 1.5f);
                Rotulo(estructura, "BARRACA " + letras[i], 37.8f, 3.2f, z, 90f, 1.1f, Color.white, AzulSenal, 8f, 1.5f);
            }
            Edificio(estructura, "Comedor", -30f, -246f, 20f, 4.5f, 12f, Casa);
            Rotulo(estructura, "COMEDOR", -30f, 3f, -252.2f, 0f, 1.2f, Color.white, VerdeSenal, 8f, 1.6f);
            Edificio(estructura, "Parque_Motor", 30f, -246f, 20f, 4.5f, 12f, Oliva);
            Rotulo(estructura, "PARQUE MOTOR", 30f, 3f, -252.2f, 0f, 1.2f, Color.white, Naranja, 10f, 1.6f);
            Edificio(estructura, "Comando", 0f, -268f, 18f, 6f, 10f, CementoOsc);
            Decor(estructura, "Techo_Comando", 0f, -268f, 18.6f, 0.5f, 10.6f, Techo, 0f, 6f);
            Rotulo(estructura, "COMANDO", 0f, 4.2f, -273.2f, 0f, 1.4f, Color.white, Rojo, 9f, 1.8f);
            // Torres de vigilancia en las esquinas.
            foreach (var sx in new[] { -1f, 1f })
                foreach (var tz in new[] { -334f, -226f })
                {
                    Solido(estructura, "Torre_Pata", sx * 55f, tz, 3f, 9f, 3f, CementoOsc);
                    Decor(estructura, "Torre_Plataforma", sx * 55f, tz, 6f, 0.6f, 6f, Techo, 0f, 9f);
                    Decor(estructura, "Torre_Baranda", sx * 55f, tz + 3f, 6f, 1f, 0.2f, CementoOsc, 0f, 9.6f);
                }
            // Camiones y cajas como cobertura del patio.
            Cubo(estructura, "Camion_1", -14f, -292f, 3f, 2.8f, 7f, Oliva, 0f, 0f, true, 700);
            Cubo(estructura, "Camion_2", 16f, -304f, 3f, 2.8f, 7f, Oliva, 0f, 0f, true, 700);
            Cubo(estructura, "Camion_3", 36f, -276f, 7f, 2.8f, 3f, Oliva, 0f, 0f, true, 700);
            Cubo(estructura, "Caja_1", -22f, -318f, 3f, 2f, 3f, Cobertura, 0f, 0f, true, 400);
            Cubo(estructura, "Caja_2", 24f, -326f, 3f, 2f, 3f, Cobertura, 0f, 0f, true, 400);
            Saco(estructura, "Saco_1", -6f, -312f); Saco(estructura, "Saco_2", 8f, -314f); Saco(estructura, "Saco_3", -18f, -276f); Saco(estructura, "Saco_4", 18f, -280f);
            Saco(estructura, "Saco_5", -4f, -236f); Saco(estructura, "Saco_6", 4f, -236f); Saco(estructura, "Saco_7", -28f, -300f, false); Saco(estructura, "Saco_8", 28f, -296f, false);
            // Patio de formacion (pintura) y camino iluminado hasta el porton.
            Pintura(estructura, "Patio_Formacion", 0f, -304f, 50f, 34f, CementoOsc, 0f, 0.0f, 0.06f);
            Pintura(estructura, "Camino_Cuartel", 0f, -280f, 8f, 120f, Asfalto, 0f, 0.0f, 0.08f);
            for (int k = 0; k < 12; k++) Pintura(estructura, "Linea_Cuartel_" + k, 0f, -336f + k * 9.5f, 0.3f, 3.5f, Amarillo);
            for (float z = -330f; z <= -230f; z += 20f) { Farola(estructura, -6.5f, z, false); Farola(estructura, 6.5f, z, false); }
            LuzPuntual(estructura, "Luz_Patio_1", -20f, 7f, -300f, new Color(1f, 0.93f, 0.8f), 3f, 24f);
            LuzPuntual(estructura, "Luz_Patio_2", 20f, 7f, -300f, new Color(1f, 0.93f, 0.8f), 3f, 24f);
            LuzPuntual(estructura, "Luz_Porton", 0f, 7f, -228f, new Color(1f, 0.93f, 0.8f), 3f, 22f);
            FlechaPiso(estructura, 0f, -250f, 0f); FlechaPiso(estructura, 0f, -232f, 0f);
            Rotulo(estructura, "^ PORTON NORTE", 0f, 5f, -236f, 0f, 1.0f, Color.white, AzulSenal, 10f, 1.4f);

            // Enemigos del cuartel.
            var en = Grupo(rEnemigos, "Cuartel");
            var lista = new List<Soldier>();
            float[][] pos =
            {
                new[]{-20f,-312f}, new[]{20f,-310f}, new[]{-8f,-298f}, new[]{10f,-300f}, new[]{0f,-286f}, new[]{-26f,-282f}, new[]{26f,-284f},
                new[]{-34f,-324f}, new[]{34f,-322f}, new[]{-34f,-298f}, new[]{34f,-296f}, new[]{-34f,-274f}, new[]{-14f,-262f}, new[]{14f,-262f},
                new[]{-12f,-236f}, new[]{12f,-236f}, new[]{-3f,-228f}, new[]{3f,-228f},
            };
            for (int i = 0; i < pos.Length; i++)
                lista.Add(Enemigo("Enemigo_Cuartel_" + (i + 1), pos[i][0], pos[i][1], 180f, en, true, 4.5f));
            lista.Add(Enemigo("Enemigo_Vigia_Oeste", -52f, -300f, 90f, en, false));
            lista.Add(Enemigo("Enemigo_Vigia_Este", 52f, -300f, 270f, en, false));
            enemigos = lista.ToArray();
            return g;
        }

        // ---------------------------------------------------------------
        // 2. Tres puestos de control
        // ---------------------------------------------------------------
        static PuestoDeControl[] Puestos(Transform padre)
        {
            var g = Grupo(padre, "2_PuestosDeControl");
            // Camino que conecta el porton con el centro de datos.
            var camino = Grupo(g, "Camino");
            Pintura(camino, "Asfalto_Corredor", 0f, -150f, 14f, 150f, Asfalto, 0f, 0f, 0.08f);
            for (int k = 0; k < 17; k++) Pintura(camino, "Linea_Corredor_" + k, 0f, -216f + k * 8f, 0.3f, 3.5f, Amarillo);
            for (float z = -210f; z <= -80f; z += 26f) { Farola(camino, -8.4f, z, false); Farola(camino, 8.4f, z, false); }
            for (float z = -205f; z <= -120f; z += 22f) FlechaPiso(camino, 0f, z, 0f);

            float[] zs = { -185f, -150f, -115f };
            int[] cantidad = { 3, 4, 5 };
            var puestos = new PuestoDeControl[3];
            for (int i = 0; i < 3; i++)
            {
                float z0 = zs[i];
                var p = Grupo(g, "Puesto_" + (i + 1));
                var est = Grupo(p, "Estructuras");
                // Muro transversal a todo el valle, con el hueco de la ruta.
                MuroX(est, "Muro_O", -60f, -8f, z0, 3.5f, 3f, CementoOsc);
                MuroX(est, "Muro_E", 8f, 60f, z0, 3.5f, 3f, CementoOsc);
                Solido(est, "Pilar_O", -8.8f, z0, 1.6f, 5f, 3.4f, Cemento);
                Solido(est, "Pilar_E", 8.8f, z0, 1.6f, 5f, 3.4f, Cemento);
                // Barrera que se hunde cuando el FLANQUEADOR desactiva el panel.
                var barrera = Solido(est, "Barrera_Puesto_" + (i + 1), 0f, z0, 16f, 2.4f, 0.9f, Rojo);
                for (int k = -3; k <= 3; k++)
                {
                    var f = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    f.name = "Franja"; f.transform.SetParent(barrera.transform, false);
                    f.transform.localPosition = new Vector3(k / 8f * 1f, 0f, 0f);
                    f.transform.localScale = new Vector3(0.07f, 1.02f, 1.1f);
                    f.GetComponent<MeshRenderer>().sharedMaterial = Blanco;
                    Object.DestroyImmediate(f.GetComponent<BoxCollider>());
                }
                // Garita de guardia y casilla del panel.
                Edificio(est, "Garita_O", -13f, z0 - 5f, 4f, 3f, 4f, Casa);
                Edificio(est, "Garita_E", 13f, z0 - 5f, 4f, 3f, 4f, Casa);
                // Pasillo en S antes de la barrera (jersey barriers): obliga a bajar la velocidad y da cobertura.
                Solido(est, "Jersey_1", -3f, z0 - 16f, 9f, 1.1f, 1.2f, Cemento);
                Solido(est, "Jersey_2", 3f, z0 - 24f, 9f, 1.1f, 1.2f, Cemento);
                Saco(est, "Saco_A", -4f, z0 + 9f); Saco(est, "Saco_B", 5f, z0 + 11f);
                // Cartel en portico + senal de alto.
                Solido(est, "Portico_Pata_O", -8.2f, z0 - 9f, 0.5f, 7f, 0.5f, CementoOsc);
                Solido(est, "Portico_Pata_E", 8.2f, z0 - 9f, 0.5f, 7f, 0.5f, CementoOsc);
                Decor(est, "Portico_Viga", 0f, z0 - 9f, 17f, 0.6f, 0.6f, CementoOsc, 0f, 6.8f);
                Rotulo(est, "PUESTO DE CONTROL " + (i + 1) + "/3", 0f, 6.6f, z0 - 9.4f, 0f, 1.5f, Color.white, AzulSenal, 14f, 1.9f);
                Rotulo(est, "ALTO", 0f, 3.2f, z0 - 0.6f, 0f, 2.2f, Color.white, Rojo, 5f, 1.8f);
                Rotulo(est, "SOLO EL FLANQUEADOR\nPUEDE DESACTIVARLO", 11.6f, 2.6f, z0 - 12f, 0f, 0.7f, Color.white, Naranja, 7.6f, 1.8f);
                Solido(est, "Poste_Aviso", 11.6f, z0 - 12.2f, 0.3f, 1.8f, 0.3f, CementoOsc);
                LuzPuntual(est, "Luz_Puesto_A", -6f, 6f, z0 - 3f, new Color(1f, 0.93f, 0.8f), 3f, 18f);
                LuzPuntual(est, "Luz_Puesto_B", 6f, 6f, z0 + 4f, new Color(1f, 0.93f, 0.8f), 3f, 18f);

                // Panel de control (a la derecha de la ruta, de este lado de la barrera).
                var panelRaiz = Grupo(p, "Panel");
                var pedestal = Solido(panelRaiz, "Pedestal", 5.6f, z0 - 3.2f, 1.6f, 1.2f, 1f, CementoOsc);
                var pantalla = Decor(panelRaiz, "Pantalla", 5.6f, z0 - 3.72f, 1.2f, 0.7f, 0.12f, Pantalla, 0f, 1.3f);
                var barraProg = Decor(panelRaiz, "BarraProgreso", 5.6f, z0 - 3.85f, 1.2f, 0.12f, 0.12f, Mat("Progreso", new Color(0.35f, 1f, 0.5f), 1.4f), 0f, 1.15f);
                Rotulo(panelRaiz, "PANEL\nPUESTO " + (i + 1), 5.6f, 2.55f, z0 - 3.8f, 0f, 0.8f, Color.white, Negro, 3.4f, 1.6f);
                var term = pedestal.AddComponent<OperacionTerminal>();
                term.titulo = "PUESTO " + (i + 1);
                term.duracion = 4f;
                term.soloUnRol = true;
                term.rol = RoleType.Flanker;
                term.nombreDelRol = "FLANQUEADOR";
                term.radio = 3.8f;
                term.modoSostener = true;
                term.decae = true;
                term.indicador = barraProg.transform;
                term.luz = pantalla.GetComponent<MeshRenderer>();

                // Guardias.
                var en = Grupo(rEnemigos, "Puesto_" + (i + 1));
                var guardias = new List<Soldier>();
                float[][] gp = { new[]{-4f, z0 + 8f}, new[]{4f, z0 + 8f}, new[]{0f, z0 + 13f}, new[]{-12f, z0 + 7f}, new[]{12f, z0 + 7f} };
                for (int k = 0; k < cantidad[i]; k++) guardias.Add(Enemigo($"Enemigo_Puesto{i + 1}_{k + 1}", gp[k][0], gp[k][1], 180f, en, true, 3.5f));

                puestos[i] = new PuestoDeControl { nombre = "PUESTO " + (i + 1), barrera = barrera, panel = term, guardias = guardias.ToArray() };
            }
            return puestos;
        }

        // ---------------------------------------------------------------
        // 3. Centro de datos
        // ---------------------------------------------------------------
        static OperacionTerminal CentroDeDatos(Transform padre, out OleadaDeReserva[] oleadas)
        {
            var g = Grupo(padre, "3_CentroDeDatos");
            var est = Grupo(g, "Estructuras");
            // Patio sur y patios laterales (adentro del valle).
            Pintura(est, "Patio_Sur", 0f, -97f, 120f, 36f, CementoOsc, 0f, 0f, 0.06f);
            Pintura(est, "Patio_Oeste", -44f, -60f, 34f, 42f, CementoOsc, 0f, 0f, 0.06f);
            Pintura(est, "Patio_Este", 44f, -60f, 34f, 42f, CementoOsc, 0f, 0f, 0.06f);
            // Cierre del valle a los costados del edificio (z=-40) y paredes del edificio (sin techo: vista libre).
            MuroX(est, "Cierre_Norte_O", -60f, -26f, -40f, 5f, 2f, CementoOsc);
            MuroX(est, "Cierre_Norte_E", 26f, 60f, -40f, 5f, 2f, CementoOsc);
            MuroX(est, "CD_Sur_O", -26f, -4f, -80f, 5f, 1.5f, Datos);
            MuroX(est, "CD_Sur_E", 4f, 26f, -80f, 5f, 1.5f, Datos);
            MuroX(est, "CD_Norte_O", -26f, -4f, -40f, 5f, 1.5f, Datos);
            MuroX(est, "CD_Norte_E", 4f, 26f, -40f, 5f, 1.5f, Datos);
            MuroZ(est, "CD_Oeste_S", -26f, -80f, -62f, 5f, 1.5f, Datos);
            MuroZ(est, "CD_Oeste_N", -26f, -56f, -40f, 5f, 1.5f, Datos);
            MuroZ(est, "CD_Este_S", 26f, -80f, -62f, 5f, 1.5f, Datos);
            MuroZ(est, "CD_Este_N", 26f, -56f, -40f, 5f, 1.5f, Datos);
            Pintura(est, "CD_Piso", 0f, -60f, 52f, 40f, Mat("PisoDatos", new Color(0.52f, 0.58f, 0.66f)), 0f, 0.0f, 0.1f);
            Rotulo(est, "CENTRO DE DATOS", 0f, 8.2f, -80.9f, 0f, 2f, Color.white, AzulSenal, 22f, 2.6f);
            Rotulo(est, "ENTRADA", 0f, 5.9f, -80.9f, 0f, 1f, Color.white, VerdeSenal, 6f, 1.2f);
            Rotulo(est, "SALIDA", 0f, 5.9f, -39.1f, 180f, 1f, Color.white, VerdeSenal, 6f, 1.2f);
            Rotulo(est, "SALIDA ^", 0f, 5.9f, -40.9f, 0f, 1f, Color.white, VerdeSenal, 6f, 1.2f);
            // Racks de servidores (cobertura) con luces de estado.
            float[][] racks = { new[]{-9f,-71f,1.4f,7f}, new[]{9f,-71f,1.4f,7f}, new[]{-15f,-58f,7f,1.4f}, new[]{15f,-58f,7f,1.4f}, new[]{-19f,-46f,1.4f,5f}, new[]{19f,-46f,1.4f,5f} };
            for (int i = 0; i < racks.Length; i++)
            {
                var r = Cubo(est, "Rack_" + (i + 1), racks[i][0], racks[i][1], racks[i][2], 2.8f, racks[i][3], Rack, 0f, 0f, true, 600);
                Decor(est, "Rack_Luz_" + (i + 1), racks[i][0], racks[i][1], racks[i][2] + 0.04f, 0.18f, racks[i][3] + 0.04f, RackLuz, 0f, 2.1f);
            }
            LuzPuntual(est, "Luz_CD_1", -10f, 5.5f, -56f, new Color(0.7f, 0.9f, 1f), 3.5f, 22f);
            LuzPuntual(est, "Luz_CD_2", 10f, 5.5f, -56f, new Color(0.7f, 0.9f, 1f), 3.5f, 22f);
            LuzPuntual(est, "Luz_CD_Sur", 0f, 6f, -90f, new Color(1f, 0.93f, 0.8f), 3f, 24f);

            // La computadora: mesa + monitor emisivo + barra de progreso.
            var pc = Grupo(g, "Computadora");
            var mesa = Solido(pc, "Mesa", 0f, -45.2f, 4.4f, 1.1f, 1.6f, CementoOsc);
            Solido(pc, "Torre_Servidor", -3.4f, -45.2f, 0.9f, 1.6f, 0.9f, Rack);
            var monitor = Decor(pc, "Monitor", 0f, -44.6f, 3.2f, 1.5f, 0.15f, Pantalla, 0f, 1.1f);
            var barra = Decor(pc, "BarraProgreso", 0f, -45.9f, 3.2f, 0.12f, 0.2f, Mat("Progreso", new Color(0.35f, 1f, 0.5f), 1.4f), 0f, 1.1f);
            Rotulo(pc, "COMPUTADORA\n[E] INTERACTUAR 30 s", 0f, 7.2f, -45f, 0f, 0.8f, Color.white, Negro, 6.6f, 1.6f);
            LuzPuntual(pc, "Luz_PC", 0f, 3.2f, -50f, new Color(0.4f, 0.8f, 1f), 3.5f, 14f);
            var term = mesa.AddComponent<OperacionTerminal>();
            term.titulo = "COMPUTADORA";
            term.duracion = 30f;
            term.soloUnRol = false;
            term.radio = 3.6f;
            term.modoSostener = false;
            term.decae = false;
            term.indicador = barra.transform;
            term.luz = monitor.GetComponent<MeshRenderer>();

            // Oleadas de reserva (apagadas hasta que empieza el hackeo).
            var res = Grupo(rReservas, "CentroDeDatos");
            var w1 = new List<Soldier>(); var w2 = new List<Soldier>(); var w3 = new List<Soldier>();
            for (int i = 0; i < 5; i++) w1.Add(Enemigo("Reserva_CD1_" + (i + 1), -16f + i * 8f, -104f, 0f, res, false, 3f, true));
            for (int i = 0; i < 5; i++) w2.Add(Enemigo("Reserva_CD2_" + (i + 1), -52f + (i % 2) * 6f, -50f - i * 5f, 90f, res, false, 3f, true));
            for (int i = 0; i < 6; i++) w3.Add(Enemigo("Reserva_CD3_" + (i + 1), 52f - (i % 2) * 6f, -50f - i * 4.5f, 270f, res, false, 3f, true));
            oleadas = new[]
            {
                new OleadaDeReserva { segundo = 0f, aviso = "OLEADA 1/3 · POR LA ENTRADA SUR", soldados = w1.ToArray(), destino = DestinoPc(pc) },
                new OleadaDeReserva { segundo = 10f, aviso = "OLEADA 2/3 · POR EL PATIO OESTE", soldados = w2.ToArray(), destino = DestinoPc(pc) },
                new OleadaDeReserva { segundo = 20f, aviso = "OLEADA 3/3 · POR EL PATIO ESTE", soldados = w3.ToArray(), destino = DestinoPc(pc) },
            };
            return term;
        }

        static Transform DestinoPc(Transform pc)
        {
            var t = new GameObject("Destino_Oleadas").transform;
            t.SetParent(pc, false);
            t.position = new Vector3(0f, 0f, -54f);
            return t;
        }

        // ---------------------------------------------------------------
        // 4a. Patio del tanque (salida del centro de datos)
        // ---------------------------------------------------------------
        static Vehicle PatioDelTanque(Transform padre, Transform vehiculos, out OleadaDeReserva oleada)
        {
            var g = Grupo(padre, "4_PatioDelTanque");
            Pintura(g, "Calle_Salida", 0f, -15f, 12f, 50f, Asfalto, 0f, 0f, 0.08f);
            Pintura(g, "Patio_Tanque", 0f, 22f, 52f, 46f, Asfalto, 0f, 0f, 0.08f);
            Pintura(g, "Plaza_Cemento", 0f, 22f, 30f, 30f, CementoOsc, 0f, 0.08f, 0.04f);
            for (int k = 0; k < 6; k++) Pintura(g, "Linea_Salida_" + k, 0f, -36f + k * 8f, 0.3f, 3.5f, Amarillo);
            for (int k = 0; k < 4; k++) FlechaPiso(g, 0f, -30f + k * 12f, 0f);
            // Cocheras laterales y barricadas (cobertura para el combate de salida).
            Edificio(g, "Cochera_O", -30f, 24f, 14f, 5f, 28f, Oliva);
            Edificio(g, "Cochera_E", 30f, 24f, 14f, 5f, 28f, Oliva);
            Decor(g, "Techo_Cochera_O", -30f, 24f, 14.6f, 0.5f, 28.6f, Techo, 0f, 5f);
            Decor(g, "Techo_Cochera_E", 30f, 24f, 14.6f, 0.5f, 28.6f, Techo, 0f, 5f);
            Saco(g, "Saco_Salida_1", -6f, -10f); Saco(g, "Saco_Salida_2", 6f, -4f); Saco(g, "Saco_Patio_1", -12f, 8f, false); Saco(g, "Saco_Patio_2", 12f, 10f, false);
            Cubo(g, "Camion_Patio", -16f, 40f, 3f, 2.8f, 7f, Oliva, 0f, 0f, true, 700);
            Cubo(g, "Caja_Patio", 15f, 38f, 3f, 2f, 3f, Cobertura, 0f, 0f, true, 400);
            Rotulo(g, "TANQUE\nPUNTO DE FUGA ^", 0f, 5.2f, 3f, 0f, 1.5f, Color.white, VerdeSenal, 12f, 3f);
            Solido(g, "Poste_Tanque_O", -6.2f, 3f, 0.4f, 5f, 0.4f, CementoOsc);
            Solido(g, "Poste_Tanque_E", 6.2f, 3f, 0.4f, 5f, 0.4f, CementoOsc);
            Farola(g, -8f, -20f, true); Farola(g, 8f, -20f, true); Farola(g, -14f, 14f, true); Farola(g, 14f, 14f, true);
            LuzPuntual(g, "Luz_Tanque", 0f, 7f, 22f, new Color(1f, 0.95f, 0.85f), 3.5f, 28f);

            // Tanque amigo: maneja un aliado (autopiloto por la carretera) y el jugador va de artillero.
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabTanque);
            var t = (GameObject)PrefabUtility.InstantiatePrefab(prefab, vehiculos);
            t.name = "Tanque_Fuga";
            t.transform.SetPositionAndRotation(new Vector3(0f, 0.6f, 22f), Quaternion.identity);
            var v = t.GetComponent<Vehicle>();
            var so = new SerializedObject(v);
            so.FindProperty("maxHealth").intValue = 900;
            so.ApplyModifiedPropertiesWithoutUndo();
            // Pedido: "quiero que uses el tanque que tenes, el que tiene torreta y orugas" (el mismo Vehiculo_Blindado de
            // SC_Gameplay). Antes aca se apagaban los VisualMundo y se prendian los cubos (blockout); ahora queda el arte del
            // prefab tal cual: cuerpo con orugas, torreta, cañon y metralleta reales (SM_Veh_Tanque_*, trimsheet).
            PrefabUtility.RecordPrefabInstancePropertyModifications(v);
            EditorUtility.SetDirty(v);

            // Los que "abren el paso" hasta el tanque: un grupo que sale del patio de la fabrica en cuanto termina la descarga.
            var res = Grupo(rReservas, "Huida");
            var lista = new List<Soldier>();
            for (int i = 0; i < 6; i++) lista.Add(Enemigo("Reserva_Huida_" + (i + 1), -22f + i * 9f, 52f + (i % 2) * 6f, 180f, res, false, 3f, true));
            var destino = new GameObject("Destino_Huida").transform;
            destino.SetParent(g, false);
            destino.position = new Vector3(0f, 0f, 24f);
            oleada = new OleadaDeReserva { segundo = 0f, aviso = "REFUERZOS ENEMIGOS EN EL PATIO", soldados = lista.ToArray(), destino = destino };
            return v;
        }

        // ---------------------------------------------------------------
        // 4b. Autopista (ruta del tanque)
        // ---------------------------------------------------------------
        static Transform[] Autopista(Transform padre)
        {
            var g = Grupo(padre, "5_Autopista");
            // Ruta del doble de largo (~870 m): recta inicial, gran curva a la izquierda, bajada en "S" por el norte y entrada a la
            // ciudad por el oeste. A ~17 m/s son ~52 s de trayecto frenetico.
            Vector2[] pts =
            {
                new Vector2(0f, 40f), new Vector2(0f, 110f), new Vector2(0f, 180f), new Vector2(-40f, 215f), new Vector2(-110f, 235f),
                new Vector2(-170f, 255f), new Vector2(-170f, 320f), new Vector2(-90f, 352f), new Vector2(0f, 346f), new Vector2(100f, 332f),
                new Vector2(180f, 304f), new Vector2(222f, 268f), new Vector2(238f, 228f), new Vector2(262f, 228f),
            };
            var rutaRaiz = Grupo(g, "RutaTanque");
            var ruta = new Transform[pts.Length];
            for (int i = 0; i < pts.Length; i++)
            {
                var t = new GameObject("Ruta_" + (i + 1)).transform;
                t.SetParent(rutaRaiz, false);
                t.position = new Vector3(pts[i].x, 0.6f, pts[i].y);
                ruta[i] = t;
            }
            float acumulado = 0f;
            for (int i = 0; i < pts.Length - 1; i++)
            {
                var a = pts[i]; var b = pts[i + 1];
                var d = b - a; float largo = d.magnitude;
                float yaw = Mathf.Atan2(d.x, d.y) * Mathf.Rad2Deg;
                var mid = (a + b) * 0.5f;
                Pintura(g, "Asfalto_" + i, mid.x, mid.y, 14f, largo + 1.5f, Asfalto, yaw, 0f, 0.08f);
                // Banquinas blancas y linea central discontinua.
                var lado = new Vector2(d.y, -d.x).normalized;
                Pintura(g, "Borde_I_" + i, mid.x + lado.x * 6.6f, mid.y + lado.y * 6.6f, 0.3f, largo, Blanco, yaw, 0.02f, 0.1f);
                Pintura(g, "Borde_D_" + i, mid.x - lado.x * 6.6f, mid.y - lado.y * 6.6f, 0.3f, largo, Blanco, yaw, 0.02f, 0.1f);
                var dn = d.normalized;
                for (float s = 2f; s < largo - 2f; s += 9f)
                {
                    var p = a + dn * s;
                    Pintura(g, "Linea", p.x, p.y, 0.3f, 4f, Amarillo, yaw, 0.02f, 0.1f);
                }
                // Farolas alternadas, defensa de cemento (con huecos) y senales de km.
                for (float s = 10f; s < largo - 4f; s += 30f)
                {
                    var p = a + dn * s;
                    float sign = ((int)((acumulado + s) / 30f) % 2 == 0) ? 1f : -1f;
                    Farola(g, p.x + lado.x * 9.5f * sign, p.y + lado.y * 9.5f * sign, false, 7f);
                    var p2 = a + dn * (s + 15f);
                    if (s + 15f < largo - 3f) FlechaPiso(g, p2.x, p2.y, yaw);
                }
                for (float s = 6f; s < largo - 6f; s += 22f)
                {
                    var p = a + dn * s;
                    Solido(g, "Jersey_I", p.x + lado.x * 8.4f, p.y + lado.y * 8.4f, 0.7f, 1.0f, 8f, Cemento, yaw);
                    Solido(g, "Jersey_D", p.x - lado.x * 8.4f, p.y - lado.y * 8.4f, 0.7f, 1.0f, 8f, Cemento, yaw);
                }
                acumulado += largo;
            }
            // Cartelera y senales de la autopista.
            Rotulo(g, "AUTOPISTA ^\nCIUDAD ESTE 870 m", -17.5f, 4.6f, 62f, 0f, 1.3f, Color.white, VerdeSenal, 12f, 3f);
            Solido(g, "Poste_Autopista_O", -22.5f, 62f, 0.4f, 6f, 0.4f, CementoOsc);
            Solido(g, "Poste_Autopista_E", -12.5f, 62f, 0.4f, 6f, 0.4f, CementoOsc);
            Rotulo(g, "CURVA A LA IZQUIERDA <<", -17.5f, 3.8f, 165f, 0f, 1.2f, Color.black, Amarillo, 12f, 1.8f);
            Solido(g, "Poste_Curva_O", -22.5f, 165f, 0.3f, 4f, 0.3f, CementoOsc);
            Solido(g, "Poste_Curva_E", -12.5f, 165f, 0.3f, 4f, 0.3f, CementoOsc);
            // Chevrons en cada curva cerrada y carteles de km cada ~200 m, a la derecha del sentido de la marcha.
            float recorrido = 0f, proximoKm = 200f; int km = 1;
            for (int i = 0; i < pts.Length - 1; i++)
            {
                var d0 = pts[i + 1] - pts[i]; float l0 = d0.magnitude; var n0 = d0.normalized;
                var der = new Vector2(n0.y, -n0.x);
                if (i >= 1 && i < pts.Length - 2)
                {
                    var d1 = (pts[i + 2] - pts[i + 1]).normalized;
                    if (Vector2.Angle(n0, d1) > 25f)
                    {
                        var esq = pts[i + 1] + (n0 - d1).normalized * 10.5f;   // por fuera de la curva
                        Solido(g, "Chevron_" + i, esq.x, esq.y, 0.4f, 2.4f, 2.4f, Naranja);
                    }
                }
                while (proximoKm >= recorrido && proximoKm < recorrido + l0 - 6f)
                {
                    var pk = pts[i] + n0 * (proximoKm - recorrido) + der * 11f;
                    float yawK = Mathf.Atan2(-n0.x, n0.y) * Mathf.Rad2Deg + 180f;
                    Rotulo(g, "KM " + km, pk.x, 2.6f, pk.y, yawK, 1.2f, Color.white, AzulSenal, 4f, 1.5f);
                    Solido(g, "Poste_Km" + km, pk.x, pk.y, 0.3f, 2.6f, 0.3f, CementoOsc);
                    km++; proximoKm += 200f;
                }
                recorrido += l0;
            }
            Rotulo(g, "CIUDAD >>\nEXTRACCION EN LA PLAZA", 232f, 4.8f, 206f, 90f, 1.3f, Color.white, VerdeSenal, 10f, 3f);
            Solido(g, "Poste_Ciudad_O", 232f, 201.4f, 0.4f, 6f, 0.4f, CementoOsc);
            Solido(g, "Poste_Ciudad_E", 232f, 210.6f, 0.4f, 6f, 0.4f, CementoOsc);

            // Escenario a los costados: depositos, graneros y rocas, siempre a mas de 24 m del eje de la ruta (para no cerrar el paso).
            var esc = Grupo(g, "Escenario");
            System.Func<float, float, float> distRuta = (px, pz) =>
            {
                float mejor = 1e9f;
                for (int i = 0; i < pts.Length - 1; i++)
                {
                    var a2 = pts[i]; var b2 = pts[i + 1]; var ab = b2 - a2;
                    float t = Mathf.Clamp01(Vector2.Dot(new Vector2(px, pz) - a2, ab) / ab.sqrMagnitude);
                    mejor = Mathf.Min(mejor, (new Vector2(px, pz) - (a2 + ab * t)).magnitude);
                }
                return mejor;
            };
            float[][] edificios =
            {
                new[]{-40f,90f,24f,6f,16f,0}, new[]{44f,140f,20f,6f,14f,0}, new[]{-50f,165f,18f,7f,14f,1}, new[]{70f,200f,20f,6f,12f,2},
                new[]{-110f,190f,22f,6f,14f,0}, new[]{-60f,275f,20f,6f,12f,1}, new[]{-215f,290f,24f,7f,14f,0}, new[]{-125f,300f,18f,6f,12f,2},
                new[]{-100f,395f - 30f,22f,6f,12f,1}, new[]{40f,300f,20f,6f,12f,0}, new[]{110f,372f - 30f,20f,6f,10f,2}, new[]{150f,270f,18f,5f,12f,1},
                new[]{205f,330f,16f,5f,10f,0}, new[]{250f,300f,14f,5f,10f,2}, new[]{160f,215f,16f,5f,10f,1},
            };
            var matsE = new[] { Oliva, Casa, CasaC };
            int ne = 0;
            foreach (var e in edificios)
            {
                if (distRuta(e[0], e[1]) < 24f + Mathf.Max(e[2], 8f) * 0.5f) continue;
                Edificio(esc, "Edificio_" + (ne++), e[0], e[1], e[2], e[3], e[4], matsE[(int)e[5]]);
            }
            Edificio(esc, "Silo_1", 190f, 170f, 10f, 12f, 10f, CementoOsc);
            Edificio(esc, "Silo_2", 205f, 170f, 10f, 12f, 10f, CementoOsc);
            int rocas = 0;
            for (int i = 0; rocas < 18 && i < 200; i++)
            {
                float rx = -200f + ((i * 97) % 420), rz = 40f + ((i * 53) % 330);
                if (distRuta(rx, rz) < 14f || distRuta(rx, rz) > 60f) continue;
                Cubo(esc, "Roca_" + rocas, rx, rz, 4f, 2.5f, 4f, CementoOsc, i * 20f, 0f, true, Indestructible);
                rocas++;
            }
            return ruta;
        }

        // ---------------------------------------------------------------
        // Auto perseguidor (plantilla inactiva; el director la clona)
        // ---------------------------------------------------------------
        static GameObject CrearPlantillaDeAuto()
        {
            var raizA = new GameObject("AutoPlantilla");
            raizA.transform.SetParent(rReservas, false);
            raizA.transform.position = new Vector3(0f, 0.6f, 0f);
            var col = raizA.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 1.1f, -0.2f); col.size = new Vector3(2.3f, 2.2f, 5.6f);
            var rb = raizA.AddComponent<Rigidbody>(); rb.isKinematic = true; rb.useGravity = false;
            var vis = Grupo(raizA.transform, "Visual");
            // CAMIONETA ("technical"): los enemigos manejan camionetas, vos manejas el tanque. Capot + cabina adelante (el frente es +Z),
            // caja abierta atras con barral y la metralleta montada sobre la caja.
            var vidrio = Mat("AutoVidrio", new Color(0.10f, 0.13f, 0.17f));
            var metal = Mat("AutoCaja", new Color(0.20f, 0.18f, 0.16f));
            Decor(vis, "Chasis", 0, 0, 2.1f, 0.45f, 5.2f, Negro, 0f, 0f).transform.localPosition = new Vector3(0f, 0.62f, 0f);
            Decor(vis, "Capot", 0, 0, 2.0f, 0.55f, 1.5f, AutoRojo, 0f, 0f).transform.localPosition = new Vector3(0f, 1.1f, 1.75f);
            Decor(vis, "Parrilla", 0, 0, 1.7f, 0.4f, 0.12f, Negro, 0f, 0f).transform.localPosition = new Vector3(0f, 1.05f, 2.55f);
            Decor(vis, "Cabina", 0, 0, 2.0f, 0.85f, 1.7f, AutoRojo, 0f, 0f).transform.localPosition = new Vector3(0f, 1.62f, 0.3f);
            Decor(vis, "Parabrisas", 0, 0, 1.8f, 0.5f, 0.1f, vidrio, 0f, 0f).transform.localPosition = new Vector3(0f, 1.75f, 1.16f);
            Decor(vis, "Techo", 0, 0, 2.05f, 0.1f, 1.8f, AutoRojo, 0f, 0f).transform.localPosition = new Vector3(0f, 2.1f, 0.3f);
            Decor(vis, "Caja_Piso", 0, 0, 2.0f, 0.12f, 2.5f, metal, 0f, 0f).transform.localPosition = new Vector3(0f, 0.95f, -1.7f);
            Decor(vis, "Caja_I", 0, 0, 0.1f, 0.55f, 2.5f, metal, 0f, 0f).transform.localPosition = new Vector3(-0.95f, 1.25f, -1.7f);
            Decor(vis, "Caja_D", 0, 0, 0.1f, 0.55f, 2.5f, metal, 0f, 0f).transform.localPosition = new Vector3(0.95f, 1.25f, -1.7f);
            Decor(vis, "Caja_Fondo", 0, 0, 2.0f, 0.55f, 0.1f, metal, 0f, 0f).transform.localPosition = new Vector3(0f, 1.25f, -2.9f);
            Decor(vis, "Barral_I", 0, 0, 0.1f, 1.1f, 0.1f, Negro, 0f, 0f).transform.localPosition = new Vector3(-0.8f, 1.75f, -0.75f);
            Decor(vis, "Barral_D", 0, 0, 0.1f, 1.1f, 0.1f, Negro, 0f, 0f).transform.localPosition = new Vector3(0.8f, 1.75f, -0.75f);
            Decor(vis, "Barral_Tope", 0, 0, 1.7f, 0.1f, 0.1f, Negro, 0f, 0f).transform.localPosition = new Vector3(0f, 2.3f, -0.75f);
            foreach (var sx in new[] { -1.1f, 1.1f })
                foreach (var sz in new[] { -1.8f, 1.6f })
                    Decor(vis, "Rueda", 0, 0, 0.4f, 0.98f, 0.98f, Negro, 0f, 0f).transform.localPosition = new Vector3(sx, 0.49f, sz);
            Decor(vis, "Sirena", 0, 0, 1.0f, 0.16f, 0.28f, Mat("AutoSirena", new Color(1f, 0.1f, 0.05f), 2f), 0f, 0f).transform.localPosition = new Vector3(0f, 2.22f, 0.5f);
            // Metralleta del techo: pivote que gira hacia el tanque.
            var pivote = new GameObject("MetralletaPivote").transform;
            pivote.SetParent(raizA.transform, false);
            pivote.localPosition = new Vector3(0f, 2.2f, -1.4f);
            Decor(pivote, "Soporte", 0, 0, 0.5f, 0.4f, 0.5f, Negro, 0f, 0f).transform.localPosition = new Vector3(0f, -0.1f, 0f);
            Decor(pivote, "Canon", 0, 0, 0.14f, 0.14f, 1.4f, Negro, 0f, 0f).transform.localPosition = new Vector3(0f, 0.1f, 0.8f);
            var boca = new GameObject("Boca").transform;
            boca.SetParent(pivote, false);
            boca.localPosition = new Vector3(0f, 0.1f, 1.55f);

            var v = raizA.AddComponent<Vehicle>();
            var so = new SerializedObject(v);
            so.FindProperty("maxHealth").intValue = 110;
            so.ApplyModifiedPropertiesWithoutUndo();
            var a = raizA.AddComponent<OperacionAuto>();
            a.metralleta = pivote;
            a.boca = boca;
            raizA.SetActive(false);
            return raizA;
        }

        // ---------------------------------------------------------------
        // 5. Ciudad chica
        // ---------------------------------------------------------------
        static Transform Ciudad(Transform padre, out OleadaDeReserva[] oleadas, out Transform plaza, out Transform helipad)
        {
            var g = Grupo(padre, "6_Ciudad");
            const float cx = 322f, cz = 228f;
            // Calles: principal (E-O) y transversal (N-S).
            Pintura(g, "Calle_Principal", 321f, cz, 118f, 12f, Asfalto, 0f, 0f, 0.08f);
            Pintura(g, "Calle_Transversal", cx, 230f, 12f, 122f, Asfalto, 0f, 0f, 0.08f);
            Pintura(g, "Plaza", cx, cz, 34f, 34f, CementoOsc, 0f, 0.06f, 0.05f);
            for (float x = 270f; x < 380f; x += 9f) if (Mathf.Abs(x - cx) > 20f) Pintura(g, "Linea_P", x, cz, 4f, 0.3f, Amarillo, 0f, 0.02f, 0.1f);
            for (float z = 172f; z < 290f; z += 9f) if (Mathf.Abs(z - cz) > 20f) Pintura(g, "Linea_T", cx, z, 0.3f, 4f, Amarillo, 0f, 0.02f, 0.1f);
            // Helipad pintado (anillo + H).
            Pintura(g, "Helipad_Anillo_N", cx, cz + 8f, 16f, 0.8f, Amarillo, 0f, 0.11f, 0.05f);
            Pintura(g, "Helipad_Anillo_S", cx, cz - 8f, 16f, 0.8f, Amarillo, 0f, 0.11f, 0.05f);
            Pintura(g, "Helipad_Anillo_E", cx + 8f, cz, 0.8f, 16f, Amarillo, 0f, 0.11f, 0.05f);
            Pintura(g, "Helipad_Anillo_O", cx - 8f, cz, 0.8f, 16f, Amarillo, 0f, 0.11f, 0.05f);
            Pintura(g, "H_I", cx - 2.2f, cz, 0.9f, 6f, Amarillo, 0f, 0.11f, 0.05f);
            Pintura(g, "H_D", cx + 2.2f, cz, 0.9f, 6f, Amarillo, 0f, 0.11f, 0.05f);
            Pintura(g, "H_C", cx, cz, 4.4f, 0.9f, Amarillo, 0f, 0.11f, 0.05f);
            // Entrada de la ciudad.
            Rotulo(g, "BIENVENIDOS A\nPUEBLO CHICO", 262f, 6f, 228f, 90f, 1.3f, Color.white, AzulSenal, 18f, 3f);
            Solido(g, "Poste_Entrada", 262f, 219f, 0.5f, 7.5f, 0.5f, CementoOsc);
            Solido(g, "Poste_Entrada_2", 262f, 237f, 0.5f, 7.5f, 0.5f, CementoOsc);
            // Casas (4 por cuadrante) y ayuntamiento.
            float[][] casas =
            {
                new[]{280f,255f,12f,5f,10f}, new[]{298f,256f,10f,6f,12f}, new[]{284f,274f,14f,5f,9f}, new[]{302f,277f,10f,5f,10f},
                new[]{344f,254f,12f,5f,10f}, new[]{362f,256f,12f,6f,12f}, new[]{350f,276f,10f,5f,10f}, new[]{368f,277f,12f,5f,9f},
                new[]{282f,190f,12f,5f,10f}, new[]{300f,192f,12f,6f,10f}, new[]{284f,208f,10f,5f,9f}, new[]{302f,208f,10f,5f,10f},
                new[]{344f,190f,12f,5f,10f}, new[]{362f,192f,12f,5f,10f}, new[]{348f,208f,12f,5f,9f}, new[]{368f,208f,10f,5f,10f},
            };
            var mats3 = new[] { Casa, CasaB, CasaC };
            for (int i = 0; i < casas.Length; i++)
            {
                var c = casas[i];
                Edificio(g, "Casa_" + (i + 1), c[0], c[1], c[2], c[3], c[4], mats3[i % 3]);
                Decor(g, "Techo_" + (i + 1), c[0], c[1], c[2] + 0.6f, 0.5f, c[4] + 0.6f, Techo, 0f, c[3]);
            }
            Edificio(g, "Ayuntamiento", cx, 280f, 24f, 8f, 10f, CementoOsc);
            Rotulo(g, "AYUNTAMIENTO", cx, 6f, 274.8f, 0f, 1.4f, Color.white, Rojo, 12f, 2f);
            // Cobertura: autos abandonados, sacos y barriles en las calles.
            Cubo(g, "Auto_Viejo_1", 296f, 224f, 2.2f, 1.5f, 4.6f, Mat("AutoViejo", new Color(0.4f, 0.45f, 0.5f)), 90f, 0f, true, 500);
            Cubo(g, "Auto_Viejo_2", 346f, 232f, 2.2f, 1.5f, 4.6f, Mat("AutoViejo", new Color(0.4f, 0.45f, 0.5f)), 90f, 0f, true, 500);
            Cubo(g, "Auto_Viejo_3", 319f, 252f, 2.2f, 1.5f, 4.6f, Mat("AutoViejo", new Color(0.4f, 0.45f, 0.5f)), 0f, 0f, true, 500);
            Cubo(g, "Auto_Viejo_4", 325f, 200f, 2.2f, 1.5f, 4.6f, Mat("AutoViejo", new Color(0.4f, 0.45f, 0.5f)), 0f, 0f, true, 500);
            Saco(g, "Saco_Plaza_1", 310f, 214f); Saco(g, "Saco_Plaza_2", 334f, 242f); Saco(g, "Saco_Plaza_3", 310f, 242f, false); Saco(g, "Saco_Plaza_4", 334f, 214f, false);
            Saco(g, "Saco_Calle_1", 276f, 222f); Saco(g, "Saco_Calle_2", 372f, 234f);
            Saco(g, "Saco_Calle_3", 316f, 176f, false); Saco(g, "Saco_Calle_4", 328f, 262f, false);
            for (float z = 176f; z <= 280f; z += 26f) { Farola(g, 313f, z, false); Farola(g, 331f, z, false); }
            for (float x = 270f; x <= 378f; x += 27f) { Farola(g, x, 221f, false); Farola(g, x, 235f, false); }
            LuzPuntual(g, "Luz_Plaza_1", 312f, 8f, 218f, new Color(1f, 0.93f, 0.8f), 3.5f, 28f);
            LuzPuntual(g, "Luz_Plaza_2", 332f, 8f, 238f, new Color(1f, 0.93f, 0.8f), 3.5f, 28f);
            Rotulo(g, "PLAZA >>\nEXTRACCION", 306f, 5.2f, 228f, 90f, 1.2f, Color.black, Amarillo, 11f, 2.6f);
            Solido(g, "Poste_Plaza_S", 306f, 222.2f, 0.4f, 6.8f, 0.4f, CementoOsc);
            Solido(g, "Poste_Plaza_N", 306f, 233.8f, 0.4f, 6.8f, 0.4f, CementoOsc);

            // Puntos de referencia.
            var t1 = new GameObject("Plaza").transform; t1.SetParent(g, false); t1.position = new Vector3(cx, 0f, cz);
            var t2 = new GameObject("Helipad").transform; t2.SetParent(g, false); t2.position = new Vector3(cx, 0.1f, cz);
            plaza = t1; helipad = t2;

            // Oleadas de la ciudad: en los extremos de las calles.
            var res = Grupo(rReservas, "Ciudad");
            var w1 = new List<Soldier>(); var w2 = new List<Soldier>(); var w3 = new List<Soldier>();
            for (int i = 0; i < 5; i++) w1.Add(Enemigo("Reserva_Ciudad1_" + (i + 1), 372f + (i % 2) * 4f, 222f + i * 3f, 270f, res, false, 3f, true));
            for (int i = 0; i < 5; i++) w2.Add(Enemigo("Reserva_Ciudad2_" + (i + 1), 316f + i * 3f, 284f + (i % 2) * 3f, 180f, res, false, 3f, true));
            for (int i = 0; i < 6; i++) w3.Add(Enemigo("Reserva_Ciudad3_" + (i + 1), 316f + (i % 3) * 5f, 170f + (i / 3) * 4f, 0f, res, false, 3f, true));
            var meta = new GameObject("Destino_Ciudad").transform; meta.SetParent(g, false); meta.position = new Vector3(cx, 0f, cz);
            oleadas = new[]
            {
                new OleadaDeReserva { segundo = 2f, aviso = "OLEADA 1/3 · POR LA CALLE ESTE", soldados = w1.ToArray(), destino = meta },
                new OleadaDeReserva { segundo = 14f, aviso = "OLEADA 2/3 · DESDE EL AYUNTAMIENTO", soldados = w2.ToArray(), destino = meta },
                new OleadaDeReserva { segundo = 26f, aviso = "OLEADA 3/3 · POR LA CALLE SUR", soldados = w3.ToArray(), destino = meta },
            };
            AmpliarCiudad(g);
            return g;
        }

        // ---------------------------------------------------------------
        // Helicoptero (cubos): llega volando, no esta estacionado. Rotores con los nombres que espera Helicoptero.cs.
        // ---------------------------------------------------------------
        static Transform CrearHelicoptero(Transform padre, out Transform inicio)
        {
            var h = new GameObject("Helicoptero_Extraccion").transform;
            h.SetParent(padre, false);
            h.position = new Vector3(330f, 70f, 380f);
            var verde = Mat("HeliCuerpo", new Color(0.26f, 0.32f, 0.22f));
            var verdeOsc = Mat("HeliOscuro", new Color(0.16f, 0.2f, 0.14f));
            void P(string n, float x, float y, float z, float w, float hh, float d, Material m) { var c = Decor(h, n, 0, 0, w, hh, d, m); c.transform.localPosition = new Vector3(x, y, z); }
            P("Fuselaje", 0f, 2.1f, 0f, 2.4f, 2.2f, 6.2f, verde);
            P("Nariz", 0f, 1.8f, 3.5f, 2.0f, 1.6f, 1.8f, verdeOsc);
            P("Cabina_Cristal", 0f, 2.9f, 2.0f, 2.2f, 1.1f, 2.2f, Cristal);
            P("Cola", 0f, 2.6f, -5.3f, 0.8f, 0.9f, 5.6f, verde);
            P("Aleta", 0f, 3.4f, -7.9f, 0.18f, 2.0f, 1.2f, verdeOsc);
            P("Patin_I", -1.4f, 0.35f, 0.2f, 0.2f, 0.2f, 5.2f, Negro);
            P("Patin_D", 1.4f, 0.35f, 0.2f, 0.2f, 0.2f, 5.2f, Negro);
            P("Puntal_I", -1.3f, 0.85f, 0.2f, 0.15f, 1.0f, 0.15f, Negro);
            P("Puntal_D", 1.3f, 0.85f, 0.2f, 0.15f, 1.0f, 0.15f, Negro);
            P("Mastil", 0f, 3.5f, 0f, 0.35f, 0.7f, 0.35f, Negro);
            // Metralleta de puerta (lado derecho: Helicoptero.cs dispara desde +right).
            P("Metra_Soporte", 1.35f, 2.2f, 0.7f, 0.5f, 0.4f, 0.5f, Negro);
            P("Metra_Canon", 1.5f, 2.3f, 1.4f, 0.14f, 0.14f, 1.5f, Negro);
            var rp = new GameObject("RotorPrincipal").transform; rp.SetParent(h, false); rp.localPosition = new Vector3(0f, 3.95f, 0f);
            var b1 = Decor(rp, "Pala_1", 0, 0, 8.4f, 0.08f, 0.45f, Negro); b1.transform.localPosition = Vector3.zero;
            var b2 = Decor(rp, "Pala_2", 0, 0, 0.45f, 0.08f, 8.4f, Negro); b2.transform.localPosition = Vector3.zero;
            var rc = new GameObject("RotorCola").transform; rc.SetParent(h, false); rc.localPosition = new Vector3(0.3f, 3.6f, -7.9f);
            var c1 = Decor(rc, "Pala_C1", 0, 0, 0.08f, 2.2f, 0.3f, Negro); c1.transform.localPosition = Vector3.zero;
            h.gameObject.AddComponent<Helicoptero>();
            h.gameObject.SetActive(false);
            var ini = new GameObject("Heli_Inicio").transform; ini.SetParent(padre, false); ini.position = new Vector3(330f, 75f, 385f);
            inicio = ini;
            return h;
        }
    }
}
