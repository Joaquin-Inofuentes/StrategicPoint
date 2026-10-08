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
    //   z -218..-100  2. MURALLA DE CONTENCION (z -170): dos bunkers con punto de carga (solo el ASALTO) + porton blindado; aproximacion al sur, corredor al norte
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
        static GameObject[] torretasDelCentro;   // las arma CentroDeDatos() y Construir() las cablea al director (bug #043)
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
        // Obstaculo que se rompe (bug #090): el tanque lo aplasta, el canon lo revienta. vida > 0.
        static GameObject Destruible(Transform p, string n, float x, float z, float w, float h, float d, Material m, int vida, float yaw = 0f, float y0 = 0f)
            => Cubo(p, n, x, z, w, h, d, m, yaw, y0, true, vida);
        // Lo que va sobre la calzada: no entra al horneado del NavMesh (sino el tanque, que sigue una ruta por la malla, esquivaria la barrera en vez de aplastarla).
        static GameObject SobreLaCalzada(GameObject go)
        {
            var mod = go.GetComponent<NavMeshModifier>();
            if (mod == null) mod = go.AddComponent<NavMeshModifier>();
            mod.ignoreFromBuild = true;
            var marca = go.GetComponent<ObstacleMarker>();
            if (marca != null) marca.ConfigurarSinNavMesh();
            return go;
        }
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

        static void Farola(Transform p, float x, float z, bool conLuz, float alto = 6f, int vida = 0)
        {
            var poste = vida > 0 ? Destruible(p, "Farola_Poste", x, z, 0.3f, alto, 0.3f, CementoOsc, vida) : Solido(p, "Farola_Poste", x, z, 0.3f, alto, 0.3f, CementoOsc);
            var cabeza = Decor(p, "Farola_Cabeza", x, z, 1.1f, 0.3f, 0.7f, Lampara, 0f, alto);
            // Con vida, la cabeza cuelga del poste: si el poste cae se va con el (sino quedaba la lampara flotando).
            if (vida > 0) cabeza.transform.SetParent(poste.transform, true);
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
            var puestos = Puestos(raiz, out var portonBlindado, out var soldadosDeTrinchera, out var soldadosDelCorredor);
            var computadora = CentroDeDatos(raiz, out var oleadasCentro);
            var tanque = PatioDelTanque(raiz, vehiculos, out var oleadaHuida);
            var ruta = Autopista(raiz);
            var autoPlantilla = CrearPlantillaDeAuto();
            var ciudad = Ciudad(raiz, out var oleadasCiudad, out var plaza, out var helipad);
            var heli = CrearHelicoptero(raiz, out var heliInicio);

            var entradas = new Transform[6];
            string[] nombresEntradas = { "Infiltrar", "Puestos", "Datos", "Huir", "Resistir", "Extraer" };
            Vector3[] posEntradas = { new Vector3(0, 0, -373), new Vector3(0, 0, -212), new Vector3(0, 0, -98), new Vector3(0, 0, -36), new Vector3(246, 0, 228), new Vector3(318, 0, 214) };
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
            dir.torres = torresDelCuartel;
            dir.reflectores = reflectoresDelCuartel;
            dir.portonCuartel = porton;
            dir.entradas = entradas;
            dir.puestos = puestos;
            dir.portonBlindado = portonBlindado;
            dir.soldadosDeTrinchera = soldadosDeTrinchera;
            dir.soldadosDelCorredor = soldadosDelCorredor;
            dir.anclasDeZona = CrearAnclasDeZona(raiz, tanque, plaza, helipad);
            dir.computadora = computadora;
            dir.oleadasDelCentro = oleadasCentro;
            dir.oleadaDeHuida = oleadaHuida;
            dir.tanque = tanque;
            dir.rutaDelTanque = ruta;
            dir.autoPlantilla = autoPlantilla;
            dir.segundosDeTrayecto = 70f;   // WP9b (#073): ruta larga de ~70 s
            dir.tripulacionDelJefe = tripulacionJefe;
            dir.oleadasDeReparacion = oleadasReparacion;
            dir.patrullaDelJefe = patrullaJefe;
            dir.cajasDeCohetes = cajasJefe;
            dir.halcon = halconPlantilla;
            dir.emboscadas = emboscadasEscena;
            dir.autosSimultaneos = 4;
            dir.oleadasDeLaCiudad = oleadasCiudad;
            dir.segundosDeResistencia = 90f;   // WP10 (#101): reloj efectivo del mando (antes 40 s)
            dir.radioDeCampana = radioDeCampana;
            dir.sectoresDeDefensa = sectoresMando;
            dir.prefabsDeMiliciano = prefabsMiliciano;
            dir.salidasDeMiliciano = salidasMiliciano;
            dir.campanario = campanarioMando;
            dir.plaza = plaza;
            dir.heli = heli;
            dir.heliInicio = heliInicio;
            dir.heliAterrizaje = helipad;
            dir.torretasDelCentro = torretasDelCentro;
            EditorUtility.SetDirty(dir);

            ColocarEscuadra();
            ConectarVehiculoDelJugador(tanque);
            ConfigurarCamaras();
            LevelBlockoutBuilder.AjustarAlcancesDeCombate();
            PostprocesoNocturno();
            CiudadDeCubos(raiz);     // decorado del resto del mapa (antes de hornear: sus edificios llevan el volumen no caminable)
            LevelBlockoutBuilder.BakeNavMesh();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log(ResumenCiudadDeCubos);
            Debug.Log($"[Operacion] Nivel listo: {contadorEnemigos} soldados enemigos, {raiz.GetComponentsInChildren<Transform>().Length} objetos de blockout, NavMesh horneado y escena guardada ({EscenaDestino}).");
        }

        // WP9a (#097): una ancla por zona/objetivo (1 cuartel, 2 muralla, 3 centro de datos, 4 patio del tanque, 5 plaza, 6 helipuerto). La cinematica
        // inicial calcula sus tomas desde aca (dolly de 35 m a 18 m de altura mirando el ancla). WP9b y WP10 tienen que mantenerlas.
        static Transform[] CrearAnclasDeZona(Transform padre, Vehicle tanque, Transform plaza, Transform helipad)
        {
            var g = Grupo(padre, "Anclas_Zona");
            Vector3[] pos =
            {
                new Vector3(0f, 0f, -280f),                                              // 1 cuartel
                new Vector3(0f, 0f, ZMuralla - 4f),                                      // 2 muralla de contencion
                new Vector3(0f, 0f, -60f),                                               // 3 centro de datos
                tanque != null ? new Vector3(tanque.transform.position.x, 0f, tanque.transform.position.z) : new Vector3(0f, 0f, 10f),   // 4 patio del tanque
                radioDeCampana != null ? new Vector3(radioDeCampana.position.x, 0f, radioDeCampana.position.z) : (plaza != null ? new Vector3(plaza.position.x, 0f, plaza.position.z) : new Vector3(322f, 0f, 228f)),   // 5 la radio (WP10)
                helipad != null ? new Vector3(helipad.position.x, 0f, helipad.position.z) : new Vector3(322f, 0f, 228f),                 // 6 helipuerto
            };
            string[] nombres = { "Cuartel", "Muralla", "CentroDeDatos", "PatioDelTanque", "Plaza", "Helipuerto" };
            var anclas = new Transform[6];
            for (int i = 0; i < 6; i++)
            {
                var t = new GameObject($"Ancla_Zona_{i + 1}_{nombres[i]}").transform;
                t.SetParent(g, false);
                t.position = pos[i];
                anclas[i] = t;
            }
            return anclas;
        }

        // ---------------------------------------------------------------
        // Escena base
        // ---------------------------------------------------------------
        static void LimpiarEscena()
        {
            string[] raices = { "Enemies", "Waypoints", "Vehicles", "Weapons", "Nivel_Blockout", "Blocking", "Arte", "ArteMundo", "Ambientacion",
                                "PostProceso_Noche", "Estrategia", "Mision", "CinematicaDeIntro", "CinematicaOperacion", "Operacion", "TorretasDelCentro" };
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

        // La escena de la Operacion se juega DE NOCHE (luna fria, cielo estrellado, niebla y postproceso nocturno): es el estado que
        // se fue armando a mano sobre el nivel (ver NightLightingBuilder) y que un rebuild no debe perder. false = el dia claro
        // original del blockout (ConfigurarAmbienteDia), por si hace falta ver el nivel sin oscuridad.
        const bool Noche = true;
        const string RutaCieloNoche = "Assets/_Project/Materials/M_Skybox_Noche.mat";
        const string RutaPerfilNoche = "Assets/_Project/Lighting/VP_Noche.asset";

        static void ConfigurarAmbiente()
        {
            if (Noche) ConfigurarAmbienteNoche(); else ConfigurarAmbienteDia();
        }

        static void ConfigurarAmbienteNoche()
        {
            var sunGo = GameObject.Find("SunLight");
            if (sunGo != null)
            {
                var sun = sunGo.GetComponent<Light>();
                sun.color = new Color(0.58f, 0.7f, 0.98f);
                sun.intensity = 0.42f;
                sun.shadows = LightShadows.Hard;
                sunGo.transform.rotation = Quaternion.LookRotation(-NightLightingBuilder.DireccionDeLaLuna);
                RenderSettings.sun = sun;
            }
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.13f, 0.155f, 0.24f);
            var cielo = AssetDatabase.LoadAssetAtPath<Material>(RutaCieloNoche);
            if (cielo != null) RenderSettings.skybox = cielo;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.085f, 0.115f, 0.2f);
            RenderSettings.fogStartDistance = 60f;
            RenderSettings.fogEndDistance = 260f;
            DynamicGI.UpdateEnvironment();
        }

        // Postproceso nocturno global (perfil ya existente). La raiz "PostProceso_Noche" se destruye en LimpiarEscena.
        static void PostprocesoNocturno()
        {
            if (!Noche) return;
            var perfil = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.VolumeProfile>(RutaPerfilNoche);
            if (perfil == null) { Debug.LogWarning("[Operacion] Falta " + RutaPerfilNoche + ": la escena queda sin postproceso nocturno."); return; }
            var g = new GameObject("PostProceso_Noche");
            var vol = g.AddComponent<UnityEngine.Rendering.Volume>();
            vol.isGlobal = true; vol.priority = 5f; vol.sharedProfile = perfil;
        }

        static void ConfigurarAmbienteDia()
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
    }
}
