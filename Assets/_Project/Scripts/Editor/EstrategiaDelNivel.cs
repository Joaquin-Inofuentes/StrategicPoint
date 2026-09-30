using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using SP.Mision;
using SP.Presentation;

namespace SP.EditorTools
{
    // LA ESTRATEGIA DEL NIVEL: que cada camino pida un ROL distinto y que cambiar de soldado sea la decision clave.
    //
    //   ASALTO (Doc)         COMPUERTAS BLINDADAS que solo una carga (o nada) abre: sellan el sendero del bosque en el
    //                        canon y el porton norte del fortin. Tambien los cohetes contra los tanques y la ANTENA.
    //   FRANCOTIRADOR (Kes)  VIGIAS enemigos (francotiradores con vision larga) en las torres, y la LUZ: de noche te ven de
    //                        lejos solo si estas iluminado, asi que apagar farolas y reflectores a tiros abre camino.
    //   MEDICO (Vega)        el camino largo desgasta: puestos sanitarios en cada tramo y polvorines que hieren a muchos.
    //
    // Y dos cosas que cambian la partida: la ANTENA de radio (demolerla evita la segunda oleada) y el CIERRE del camino de
    // ida (al rescatar al civil el enemigo sella el carril que usaste: hay que volver por otro, con otro reparto de roles).
    //
    // Idempotente: borra y rearma la raiz "Estrategia". Los francotiradores viven en LevelBlockoutBuilder.Infantes.
    public static class EstrategiaDelNivel
    {
        const string Raiz = "Estrategia";
        const string CarpetaMateriales = "Assets/_Project/Materials/Estrategia";
        static Transform raiz;

        [MenuItem("Strategic Point/Nivel/Estrategia (compuertas, polvorines, antena, cierres)")]
        public static void Construir()
        {
            var escena = SceneManager.GetActiveScene();
            if (escena.name != "SC_Gameplay") { Debug.LogWarning("[Estrategia] Abri SC_Gameplay antes."); return; }
            var previa = GameObject.Find(Raiz);
            if (previa != null) Object.DestroyImmediate(previa);
            raiz = new GameObject(Raiz).transform;
            Physics.SyncTransforms();

            var compuertas = Sub("Compuertas");
            Compuerta(compuertas, "Compuerta_Oeste_Canon", -43f, 72f, 10f, 3f);      // sella el sendero del bosque
            Compuerta(compuertas, "Compuerta_Fortin_Norte", 4f, 270f, 8f, 1.5f);     // porton norte del fortin (camino central)

            var cierres = Sub("Cierres");
            Cierre(cierres, "Cierre_Oeste", -43f, 72f, 10f);
            Cierre(cierres, "Cierre_Centro", 3f, 72f, 18f);
            Cierre(cierres, "Cierre_Este", 51f, 72f, 10f);

            var polvorines = Sub("Polvorines");
            Polvorin(polvorines, "Polvorin_Puesto", new Vector2(11f, 176f));
            Polvorin(polvorines, "Polvorin_Oeste", new Vector2(-37.5f, 143f));
            Polvorin(polvorines, "Polvorin_Este", new Vector2(46f, 151f));
            Polvorin(polvorines, "Polvorin_Fortin", new Vector2(8f, 236f));
            Polvorin(polvorines, "Polvorin_Refugio", new Vector2(-30f, 283f));

            Antena(Sub("Antena"), new Vector3(40f, 0f, 250f));

            DespejarRutas();

            EditorSceneManager.MarkSceneDirty(escena);
            Debug.Log("[Estrategia] Compuertas, cierres de ruta, polvorines y antena listos.");
        }

        // ---------------------------------------------------------------
        // Rutas libres para la IA
        // ---------------------------------------------------------------
        // La IA rodea obstaculos con una grilla (NavService) que infla cada collider ~0,75 m: una cobertura de 3 m en medio
        // de un sendero de 5 m lo cierra por completo, y un prop suelto (crater, escombros, barril) en la carretera tambien.
        // El NavMesh horneado no lo notaba, la grilla si: la escuadra aliada no encontraba camino y caminaba en linea recta.
        // Esta pasada mueve (o quita, si no hay donde) todo lo pequeño y movible que invade el eje de un camino.
        static float SemiAnchoLibre(RutaDelNivel r)
        {
            switch (r.Tipo)
            {
                case TipoDeRuta.Carretera: return 2.7f;
                case TipoDeRuta.Bosque: return 3.0f;
                case TipoDeRuta.Servicio: return 3.3f;
                default: return 2.4f;
            }
        }

        static readonly string[] PrefijosMovibles = { "Cob_", "Sacos_", "Erizo_", "MuroCaido_", "Barricada_", "Caja_", "Carro_", "Pozo" };

        static bool EsMovible(string nombre)
        {
            foreach (var pf in PrefijosMovibles) if (nombre.StartsWith(pf)) return true;
            return false;
        }

        // Rectangulo XZ que encierra los colliders (no trigger) de un objeto.
        static bool RectangulodeColliders(Transform t, out Vector2 centro, out Vector2 medio)
        {
            centro = medio = Vector2.zero;
            bool hay = false; Bounds bb = default;
            foreach (var c in t.GetComponentsInChildren<Collider>(false))
            {
                if (c.isTrigger || c is TerrainCollider) continue;
                if (!hay) { bb = c.bounds; hay = true; } else bb.Encapsulate(c.bounds);
            }
            if (!hay) return false;
            centro = new Vector2(bb.center.x, bb.center.z);
            medio = new Vector2(bb.extents.x, bb.extents.z);
            return true;
        }

        // Peor invasion de este rectangulo sobre cualquier camino: cuanto falta para salir, desde que punto del eje y con que
        // tangente.
        static bool Invasion(Vector2 c, Vector2 m, out float falta, out Vector2 desde, out Vector2 tangente)
        {
            falta = 0f; desde = Vector2.zero; tangente = Vector2.up;
            foreach (var r in RutasDelNivel.Todas)
            {
                float hw = SemiAnchoLibre(r);
                float largo = r.Largo;
                for (float d = 0f; d <= largo; d += 1f)
                {
                    r.Muestrear(d, out var p, out var tg);
                    float dx = Mathf.Max(Mathf.Abs(c.x - p.x) - m.x, 0f), dz = Mathf.Max(Mathf.Abs(c.y - p.y) - m.y, 0f);
                    float dist = Mathf.Sqrt(dx * dx + dz * dz);
                    if (dist < hw && hw - dist > falta) { falta = hw - dist; desde = p; tangente = tg; }
                }
            }
            return falta > 0.01f;
        }

        static bool LimiteJugable(Vector2 c, Vector2 m) => c.x - m.x > -48.6f && c.x + m.x < 57.3f && c.y - m.y > -16f && c.y + m.y < 294f;

        static bool ChocaConEstatico(Transform yo, Vector2 c, Vector2 m, float alto)
        {
            var cajas = Physics.OverlapBox(new Vector3(c.x, alto * 0.5f + 0.2f, c.y), new Vector3(Mathf.Max(0.1f, m.x - 0.45f), Mathf.Max(0.3f, alto * 0.5f), Mathf.Max(0.1f, m.y - 0.45f)));
            foreach (var col in cajas)
            {
                if (col.isTrigger || col is TerrainCollider || col.name == "Ground") continue;
                if (col.transform == yo || col.transform.IsChildOf(yo)) continue;
                if (col.name.StartsWith("ArbolCollider")) continue;   // el arbol que quede debajo se quita aparte
                if (col.GetComponentInParent<SP.Actors.Soldier>() != null || col.GetComponentInParent<SP.Vehicles.Vehicle>() != null) continue;
                return true;
            }
            return false;
        }

        static int movidos, quitados;

        static void DespejarRutas()
        {
            movidos = quitados = 0;
            Physics.SyncTransforms();
            var candidatos = new List<Transform>();
            var blk = GameObject.Find("Nivel_Blockout");
            if (blk != null)
                foreach (var mk in blk.GetComponentsInChildren<ObstacleMarker>(false))
                    if (EsMovible(mk.name)) candidatos.Add(mk.transform);
            var amb0 = GameObject.Find("Ambientacion");
            if (amb0 != null)
            {
                var f = amb0.transform.Find("Fuegos");
                if (f != null) for (int i = 0; i < f.childCount; i++) candidatos.Add(f.GetChild(i));
            }
            foreach (Transform g in raiz.Find("Polvorines")) candidatos.Add(g);
            var props = new List<Transform>();
            var amb = GameObject.Find("ArteMundo");
            if (amb != null)
            {
                var ambiente = amb.transform.Find("Ambiente");
                if (ambiente != null) for (int i = 0; i < ambiente.childCount; i++) props.Add(ambiente.GetChild(i));
            }

            foreach (var t in candidatos) if (t != null) Despejar(t, false);
            foreach (var t in props) if (t != null) Despejar(t, true);

            Physics.SyncTransforms();
            Debug.Log($"[Estrategia] Rutas despejadas para la IA: {movidos} objetos movidos, {quitados} quitados.");
        }

        // Intenta sacar el objeto del camino empujandolo por la NORMAL del tramo (no hacia el punto mas cercano: un obstaculo
        // justo sobre el eje se empujaba a lo largo del camino y nunca salia). lado = +1/-1 elige a que costado.
        static bool Intentar(Transform t, ref Vector2 c, Vector2 m, int ladoForzado)
        {
            float alto = 1.5f;
            int lado = ladoForzado;
            for (int intento = 0; intento < 10; intento++)
            {
                if (!Invasion(c, m, out var falta, out var desde, out var tg)) return LimiteJugable(c, m) && !ChocaConEstatico(t, c, m, alto);
                var n = new Vector2(-tg.y, tg.x);
                float s = Vector2.Dot(c - desde, n);
                if (lado == 0) lado = s >= 0f ? 1 : -1;
                float e = Mathf.Abs(n.x) * m.x + Mathf.Abs(n.y) * m.y;
                // distancia a la que el borde del objeto queda fuera del semiancho libre, medida sobre la normal
                float actual = lado * s;                       // distancia con signo hacia el costado elegido
                float objetivo = SemiAnchoDeTramo(desde) + e + 0.3f;
                float paso = Mathf.Max(0.3f, objetivo - actual);
                var mov = n * lado * paso;
                c += mov;
                t.position += new Vector3(mov.x, 0f, mov.y);
            }
            return !Invasion(c, m, out _, out _, out _) && LimiteJugable(c, m) && !ChocaConEstatico(t, c, m, alto);
        }

        // Semiancho libre del camino al que pertenece un punto del eje (el mayor si hay varios cerca).
        static float SemiAnchoDeTramo(Vector2 p)
        {
            float mejor = 2.4f;
            foreach (var r in RutasDelNivel.Todas)
            {
                float largo = r.Largo;
                for (float d = 0f; d <= largo; d += 1f)
                {
                    r.Muestrear(d, out var q, out _);
                    if ((q - p).sqrMagnitude < 0.01f) return SemiAnchoLibre(r);
                }
            }
            return mejor;
        }

        static void Despejar(Transform t, bool esProp)
        {
            if (!RectangulodeColliders(t, out var c, out var m)) return;
            if (m.x > 7f || m.y > 7f) return;   // perimetro, muros largos, agrupaciones grandes: no se tocan
            if (!Invasion(c, m, out _, out _, out _)) return;
            var origen = t.position;
            var c0 = c;
            bool bien = Intentar(t, ref c, m, 0);
            if (!bien)
            {
                // Probar del otro costado.
                Invasion(c0, m, out _, out var desde0, out var tg0);
                int primero = Vector2.Dot(c0 - desde0, new Vector2(-tg0.y, tg0.x)) >= 0f ? 1 : -1;
                t.position = origen; c = c0;
                bien = Intentar(t, ref c, m, -primero);
            }
            if (bien) { movidos++; return; }
            // No hay donde ponerlo: los props de ambiente y las coberturas chicas se quitan; el resto vuelve a su lugar.
            if (esProp || t.name.StartsWith("Cob_") || t.name.StartsWith("Caja_") || t.name.StartsWith("Erizo_") || t.name.StartsWith("Sacos_") || t.name.StartsWith("MuroCaido_") || t.name.StartsWith("Barricada_") || t.name.StartsWith("Carro_"))
            {
                Debug.Log($"[Estrategia] Se quita {t.name} (invadia un camino y no habia donde moverlo).");
                Object.DestroyImmediate(t.gameObject);
                quitados++;
            }
            else t.position = origen;
        }

        // ---------------------------------------------------------------
        // Compuerta blindada: solo el ASALTO la abre (carga de 4 s)
        // ---------------------------------------------------------------
        static GameObject Compuerta(Transform padre, string nombre, float x, float z, float ancho, float fondo)
        {
            const float alto = 4.2f;
            var acero = Mat("Compuerta_Acero", new Color(0.16f, 0.17f, 0.19f), default, 0f, 0.45f, 0.7f);
            var naranja = Mat("Compuerta_Franja", new Color(0.95f, 0.46f, 0.06f), new Color(1f, 0.45f, 0.05f), 2.2f, 0.3f);
            var negro = Mat("Compuerta_Negro", new Color(0.05f, 0.05f, 0.06f), default, 0f, 0.3f);
            var carga = Mat("Compuerta_Carga", new Color(0.55f, 0.08f, 0.06f), new Color(1f, 0.1f, 0.05f), 1.2f, 0.3f);

            var go = new GameObject(nombre);
            go.transform.SetParent(padre, false);
            go.transform.position = new Vector3(x, 0f, z);
            var col = go.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, alto * 0.5f, 0f);
            col.size = new Vector3(ancho, alto, fondo);
            int capa = LayerMask.NameToLayer("Obstacle");
            if (capa >= 0) go.layer = capa;
            var marca = go.AddComponent<ObstacleMarker>();
            marca.ConfigurarVida(999999);

            // Hoja de acero + marco + franjas de peligro en ambas caras.
            Prim(PrimitiveType.Cube, "Hoja", go.transform, new Vector3(0f, alto * 0.5f, 0f), new Vector3(ancho, alto, fondo * 0.7f), acero);
            Prim(PrimitiveType.Cube, "Dintel", go.transform, new Vector3(0f, alto + 0.12f, 0f), new Vector3(ancho + 0.6f, 0.34f, fondo * 1.05f), negro);
            Prim(PrimitiveType.Cube, "Poste_O", go.transform, new Vector3(-ancho * 0.5f - 0.1f, alto * 0.5f + 0.1f, 0f), new Vector3(0.5f, alto + 0.2f, fondo * 1.05f), negro);
            Prim(PrimitiveType.Cube, "Poste_E", go.transform, new Vector3(ancho * 0.5f + 0.1f, alto * 0.5f + 0.1f, 0f), new Vector3(0.5f, alto + 0.2f, fondo * 1.05f), negro);
            int franjas = Mathf.Max(3, Mathf.RoundToInt(ancho / 1.6f));
            for (int cara = -1; cara <= 1; cara += 2)
            {
                float zf = cara * (fondo * 0.35f + 0.03f);
                for (int i = 0; i < franjas; i++)
                {
                    float fx = -ancho * 0.5f + (i + 0.5f) * ancho / franjas;
                    var f = Prim(PrimitiveType.Cube, "Franja", go.transform, new Vector3(fx, alto * 0.5f, zf), new Vector3(ancho / franjas * 0.42f, alto * 0.94f, 0.06f), i % 2 == 0 ? naranja : negro);
                    f.transform.localRotation = Quaternion.Euler(0f, 0f, i % 2 == 0 ? 16f : -16f);
                }
                Cartel(go.transform, "T_Cartel_SoloAsalto", new Vector3(0f, alto * 0.5f, cara * (fondo * 0.35f + 0.09f)), cara, Mathf.Min(ancho * 0.9f, 7f));
            }
            // Cargas de demolicion pegadas al pie: es lo que va a volar.
            for (int i = 0; i < 3; i++)
                Prim(PrimitiveType.Cube, "Carga_" + i, go.transform, new Vector3(-ancho * 0.25f + i * ancho * 0.25f, 0.28f, fondo * 0.5f + 0.2f), new Vector3(0.5f, 0.42f, 0.28f), carga);
            // Baliza ambar que late sobre el dintel.
            var bal = new GameObject("Baliza");
            bal.transform.SetParent(go.transform, false);
            bal.transform.localPosition = new Vector3(0f, alto + 0.85f, 0f);
            var luz = NuevaLuz(bal, new Color(1f, 0.55f, 0.12f), 4.5f, 10f);
            var lampara = Prim(PrimitiveType.Sphere, "BalizaMalla", bal.transform, Vector3.zero, Vector3.one * 0.32f, naranja);
            var pulso = bal.AddComponent<BalizaPulsante>();
            pulso.Configurar(luz, lampara.GetComponent<Renderer>(), new Color(1f, 0.5f, 0.1f) * 3f, 0.9f, 0.2f, 0f);
            return go;
        }

        // ---------------------------------------------------------------
        // Cierre del carril de ida: nace APAGADO; lo prende MisionDirector al rescatar al civil
        // ---------------------------------------------------------------
        static void Cierre(Transform padre, string nombre, float x, float z, float ancho)
        {
            const float alto = 3.4f, fondo = 2.6f;
            var acero = Mat("Cierre_Acero", new Color(0.24f, 0.2f, 0.17f), default, 0f, 0.35f, 0.4f);
            var rojo = Mat("Cierre_Franja", new Color(0.85f, 0.1f, 0.08f), new Color(1f, 0.1f, 0.06f), 1f, 0.3f);
            var blanco = Mat("Cierre_Blanco", new Color(0.85f, 0.85f, 0.82f), default, 0f, 0.25f);
            var go = new GameObject(nombre);
            go.transform.SetParent(padre, false);
            go.transform.position = new Vector3(x, 0f, z);
            var col = go.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, alto * 0.5f, 0f);
            col.size = new Vector3(ancho, alto, fondo);
            int capa = LayerMask.NameToLayer("Obstacle");
            if (capa >= 0) go.layer = capa;
            go.AddComponent<ObstacleMarker>().ConfigurarVida(999999);

            Prim(PrimitiveType.Cube, "Muro", go.transform, new Vector3(0f, alto * 0.5f, 0f), new Vector3(ancho, alto, fondo * 0.8f), acero);
            // Sacos de arena por delante y por detras, y una faja roja con el cartel.
            for (int cara = -1; cara <= 1; cara += 2)
            {
                float zf = cara * (fondo * 0.4f + 0.25f);
                int n = Mathf.Max(3, Mathf.RoundToInt(ancho / 1.3f));
                for (int i = 0; i < n; i++)
                {
                    float fx = -ancho * 0.5f + (i + 0.5f) * ancho / n;
                    Prim(PrimitiveType.Cube, "Saco", go.transform, new Vector3(fx, 0.3f + (i % 2) * 0.05f, zf), new Vector3(ancho / n * 0.94f, 0.6f, 0.5f), blanco);
                }
                Prim(PrimitiveType.Cube, "Faja", go.transform, new Vector3(0f, alto * 0.72f, cara * (fondo * 0.4f + 0.02f)), new Vector3(ancho * 0.96f, 0.5f, 0.05f), rojo);
                Cartel(go.transform, "T_Cartel_CaminoCerrado", new Vector3(0f, alto * 0.72f, cara * (fondo * 0.4f + 0.09f)), cara, Mathf.Min(ancho * 0.85f, 6.5f));
            }
            var bal = new GameObject("Baliza");
            bal.transform.SetParent(go.transform, false);
            bal.transform.localPosition = new Vector3(0f, alto + 0.9f, 0f);
            var luz = NuevaLuz(bal, new Color(1f, 0.15f, 0.1f), 5f, 13f);
            var lampara = Prim(PrimitiveType.Sphere, "BalizaMalla", bal.transform, Vector3.zero, Vector3.one * 0.4f, rojo);
            bal.AddComponent<BalizaPulsante>().Configurar(luz, lampara.GetComponent<Renderer>(), new Color(1f, 0.1f, 0.05f) * 3.5f, 1.4f, 0.15f, 0f);
            go.SetActive(false);
        }

        // ---------------------------------------------------------------
        // Polvorin: barriles explosivos que arrastran a los vecinos
        // ---------------------------------------------------------------
        static void Polvorin(Transform padre, string nombre, Vector2 centro)
        {
            var go = new GameObject(nombre).transform;
            go.SetParent(padre, false);
            go.position = new Vector3(centro.x, 0f, centro.y);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/ArteMundo/P_Env_Barril.prefab");
            if (prefab == null)
            {
                foreach (var guid in AssetDatabase.FindAssets("P_Env_Barril t:Prefab"))
                {
                    prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
                    if (prefab != null) break;
                }
            }
            var offsets = new[] { new Vector2(0f, 0f), new Vector2(1.05f, 0.3f), new Vector2(-0.3f, 1.0f), new Vector2(0.75f, 1.15f) };
            foreach (var o in offsets)
            {
                var b = prefab != null ? (GameObject)PrefabUtility.InstantiatePrefab(prefab, go) : GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                b.name = "Barril_Explosivo";
                b.transform.SetParent(go, false);
                b.transform.localPosition = new Vector3(o.x, 0f, o.y);
                b.transform.localRotation = Quaternion.Euler(0f, o.x * 97f, 0f);
                b.transform.localScale = Vector3.one * 0.9f;
                foreach (var c in b.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
                BlockoutArtDresser.AgregarColliderYDestruccion(b, true, 400, true);   // vida alta: el primer tiro lo enciende y estalla solo (un barril debil moria antes de estallar)
                var m = b.GetComponent<ObstacleMarker>();
                if (m != null)
                {
                    var so = new SerializedObject(m);
                    so.FindProperty("radioExplosion").floatValue = 8f;
                    so.FindProperty("danoExplosion").intValue = 90;
                    so.FindProperty("demoraExplosion").floatValue = 1.4f;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
                int capa = LayerMask.NameToLayer("Obstacle");
                if (capa >= 0) b.layer = capa;
            }
            // Cartel amarillo de aviso sobre el grupo (mastil + placa por ambas caras). Sin collider: no bloquea nada.
            Prim(PrimitiveType.Cube, "Mastil", go, new Vector3(0.4f, 1.4f, 2.0f), new Vector3(0.08f, 2.8f, 0.08f), Mat("Polvorin_Mastil", new Color(0.2f, 0.2f, 0.22f)));
            Cartel(go, "T_Cartel_Peligro", new Vector3(0.4f, 2.75f, 1.95f), -1, 0.86f);
            Cartel(go, "T_Cartel_Peligro", new Vector3(0.4f, 2.75f, 2.05f), 1, 0.86f);
        }

        // ---------------------------------------------------------------
        // Antena de radio del fortin
        // ---------------------------------------------------------------
        static void Antena(Transform padre, Vector3 pos)
        {
            const float alto = 8.4f;
            var go = new GameObject("Antena_Radio");
            go.transform.SetParent(padre, false);
            go.transform.position = pos;
            var acero = Mat("Antena_Acero", new Color(0.32f, 0.33f, 0.36f), default, 0f, 0.4f, 0.6f);
            var concreto = Mat("Antena_Base", new Color(0.5f, 0.5f, 0.5f), default, 0f, 0.15f);
            var rojo = Mat("Antena_Baliza", new Color(0.6f, 0.05f, 0.04f), new Color(1f, 0.08f, 0.04f), 2.4f, 0.3f);

            var col = go.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, alto * 0.5f, 0f);
            col.size = new Vector3(2.2f, alto, 2.2f);
            int capa = LayerMask.NameToLayer("Obstacle");
            if (capa >= 0) go.layer = capa;
            go.AddComponent<ObstacleMarker>().ConfigurarVida(320);
            go.AddComponent<AntenaDeRadio>();

            Prim(PrimitiveType.Cube, "Base", go.transform, new Vector3(0f, 0.4f, 0f), new Vector3(2.6f, 0.8f, 2.6f), concreto);
            // Celosia: cuatro montantes y travesaños en cruz.
            for (int i = 0; i < 4; i++)
            {
                float sx = (i % 2 == 0 ? -1f : 1f) * 0.62f, sz = (i < 2 ? -1f : 1f) * 0.62f;
                Prim(PrimitiveType.Cube, "Montante_" + i, go.transform, new Vector3(sx, alto * 0.5f, sz), new Vector3(0.16f, alto, 0.16f), acero);
            }
            for (int k = 0; k < 6; k++)
            {
                float y = 1.2f + k * 1.2f, s = Mathf.Lerp(1f, 0.6f, k / 5f);
                Prim(PrimitiveType.Cube, "Cruz_A_" + k, go.transform, new Vector3(0f, y, 0f), new Vector3(1.75f * s, 0.09f, 0.09f), acero).transform.localRotation = Quaternion.Euler(0f, 45f, 0f);
                Prim(PrimitiveType.Cube, "Cruz_B_" + k, go.transform, new Vector3(0f, y, 0f), new Vector3(1.75f * s, 0.09f, 0.09f), acero).transform.localRotation = Quaternion.Euler(0f, -45f, 0f);
            }
            var plato = Prim(PrimitiveType.Cylinder, "Plato", go.transform, new Vector3(0.9f, alto - 1.6f, 0f), new Vector3(1.9f, 0.06f, 1.9f), acero);
            plato.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            Prim(PrimitiveType.Cylinder, "Mastil", go.transform, new Vector3(0f, alto + 0.6f, 0f), new Vector3(0.06f, 0.9f, 0.06f), acero);
            // Dos balizas rojas: se ve desde toda la mitad norte del mapa.
            foreach (var y in new[] { alto + 1.5f, alto * 0.55f })
            {
                var b = new GameObject("Baliza_" + y.ToString("0"));
                b.transform.SetParent(go.transform, false);
                b.transform.localPosition = new Vector3(0f, y, 0f);
                var luz = NuevaLuz(b, new Color(1f, 0.1f, 0.06f), 6f, 16f);
                var mb = Prim(PrimitiveType.Sphere, "BalizaMalla", b.transform, Vector3.zero, Vector3.one * 0.3f, rojo);
                b.AddComponent<BalizaPulsante>().Configurar(luz, mb.GetComponent<Renderer>(), new Color(1f, 0.08f, 0.04f) * 3f, 0.8f, 0.1f, y * 0.13f);
            }
            Cartel(go.transform, "T_Cartel_Radio", new Vector3(0f, 2.0f, -1.42f), -1, 2.6f);
            Cartel(go.transform, "T_Cartel_Radio", new Vector3(0f, 2.0f, 1.42f), 1, 2.6f);
        }

        static Transform Sub(string nombre)
        {
            var g = new GameObject(nombre).transform;
            g.SetParent(raiz, false);
            return g;
        }

        // ---------------------------------------------------------------
        // Materiales (assets persistentes)
        // ---------------------------------------------------------------
        static Material Mat(string nombre, Color color, Color emision = default, float k = 0f, float suavidad = 0.2f, float metal = 0f)
        {
            if (!AssetDatabase.IsValidFolder(CarpetaMateriales)) AssetDatabase.CreateFolder("Assets/_Project/Materials", "Estrategia");
            string path = $"{CarpetaMateriales}/M_{nombre}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            bool nuevo = m == null;
            if (nuevo) m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "M_" + nombre };
            m.SetColor("_BaseColor", color);
            m.SetFloat("_Smoothness", suavidad);
            m.SetFloat("_Metallic", metal);
            if (k > 0f)
            {
                m.EnableKeyword("_EMISSION");
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
                m.SetColor("_EmissionColor", emision * k);
            }
            if (nuevo) AssetDatabase.CreateAsset(m, path); else EditorUtility.SetDirty(m);
            return m;
        }

        static GameObject Prim(PrimitiveType tipo, string nombre, Transform padre, Vector3 pos, Vector3 escala, Material mat, bool conCollider = false)
        {
            var g = GameObject.CreatePrimitive(tipo);
            g.name = nombre;
            g.transform.SetParent(padre, false);
            g.transform.localPosition = pos;
            g.transform.localScale = escala;
            g.GetComponent<MeshRenderer>().sharedMaterial = mat;
            if (!conCollider) Object.DestroyImmediate(g.GetComponent<Collider>());
            return g;
        }

        static Light NuevaLuz(GameObject go, Color color, float intensidad, float rango)
        {
            var l = go.AddComponent<Light>();
            l.type = LightType.Point; l.color = color; l.intensity = intensidad; l.range = rango;
            l.shadows = LightShadows.None;
            return l;
        }

        // Cartel: un quad con una textura de texto (PNG generado aparte) y material sin luz, por AMBAS caras. Un TextMesh
        // se dibujaba a traves de las paredes (su shader ignora la profundidad), asi que se usan quads.
        const string CarpetaTexturas = "Assets/_Project/Textures/Estrategia";

        static Material MatCartel(string textura)
        {
            string path = $"{CarpetaMateriales}/M_{textura}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m != null) return m;
            if (!AssetDatabase.IsValidFolder(CarpetaMateriales)) AssetDatabase.CreateFolder("Assets/_Project/Materials", "Estrategia");
            string tp = $"{CarpetaTexturas}/{textura}.png";
            AssetDatabase.ImportAsset(tp, ImportAssetOptions.ForceSynchronousImport);
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(tp);
            m = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { name = "M_" + textura };
            m.SetTexture("_BaseMap", tex);
            m.SetColor("_BaseColor", new Color(0.92f, 0.92f, 0.92f));
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        // Pone el cartel a 'pos' (local) con el frente hacia +Z local del padre (cara > 0) o -Z (cara < 0).
        static void Cartel(Transform padre, string textura, Vector3 pos, int cara, float ancho, float alto = -1f)
        {
            if (alto < 0f) alto = ancho * 0.25f;
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            q.name = "Cartel";
            Object.DestroyImmediate(q.GetComponent<Collider>());
            q.transform.SetParent(padre, false);
            q.transform.localPosition = pos;
            // El quad de Unity mira hacia -Z: para que se lea desde +Z hay que girarlo 180.
            q.transform.localRotation = Quaternion.Euler(0f, cara > 0 ? 180f : 0f, 0f);
            q.transform.localScale = new Vector3(ancho, alto, 1f);
            var mr = q.GetComponent<MeshRenderer>();
            mr.sharedMaterial = MatCartel(textura);
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
    }
}
