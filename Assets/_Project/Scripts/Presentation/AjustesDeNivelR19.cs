using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using SP.Actors;
using SP.Core;
using SP.Vehicles;

namespace SP.Presentation
{
    // Ronda 19, punto 4 "Vestir exterior servidores" (Docs/PLAN_R19_v2.md, 3.4, tareas F1-F2). Parche de RUNTIME (las escenas son binarias y no se
    // tocan): el kit R19 cuelga de la misma raiz que la fachada de la R17 (3_CentroDeDatos), en el grupo R19_Servidores_Exterior. Lo arma
    // AjustesDeNivelR17.Aplicar (mismo vigilante, cada 2 s): idempotente por el grupo cacheado (si ya cuelga de la raiz, no hace nada), sin Find.
    //
    // Caras exteriores del edificio: sur z -80,75 · norte z -39,25 · oeste x -26,75 · este x 26,75 (muros de 8 m, techo a 8,0-8,3).
    // Zonas libres (ni arte ni collider): puerta sur x [-4,6; 4,6] hasta 4,75 m; salida norte x [-7; 7] hasta 7,95 m (ahi explota el muro);
    // puertas laterales z [-62,6; -55,4] hasta 4,75 m. Lo que tiene collider (tableros, torres, totem, bolardos) lleva carving.
    //   K1 zocalo · K2 cornisa · K3 pilastras (solo donde el kit modular no puso pilares) · K4 franja cian · K5 rejas · K6 luminarias (+2 Light en
    //   la cara norte) · K7 norte: tuberia triple, bajadas y 2 tableros · K8 sur: marquesina, totem lector y 6 bolardos · K9 este/oeste: 2 torres de
    //   enfriamiento por lado, escalera de gato y camara · K10 luces rojas de obstruccion · K11 vereda perimetral y 2 carteles.
    // Reglas (plan 3.4 / H7): ningun nombre empieza con los prefijos que SalaDespejadaEnRts trata aparte (todo empieza con "R19_"); todo adorno
    // apoya en muro o piso (lo que depende de una cobertura destructible es HIJO de ella: se va con ella); antes de poner una pieza puntual se
    // compara contra las zonas libres, los pilares del kit modular y lo que ya asoma de la fachada (arte R17 incluido), recogidos UNA vez.
    public static class AjustesDeNivelR19
    {
        public const string GrupoExterior = "R19_Servidores_Exterior";
        public const float CaraSur = -80.75f, CaraNorte = -39.25f, CaraOeste = -26.75f, CaraEste = 26.75f;
        public const float AlturaDeLosMuros = 8f;
        public const int Sur = 0, Norte = 1, Oeste = 2, Este = 3;

        // Prefijos que SalaDespejadaEnRts oculta con otra regla (ademas de "Techo"): ninguna pieza R19 puede empezar asi (el check lo vigila).
        public static readonly string[] PrefijosProhibidos = { "Bandeja", "Cable", "Ducto", "Baliza", "Viga", "LineaLuz", "Panel_Luz", "Rejilla_Aire", "Costilla", "Dintel_Luz", "Travesano", "Techo" };

        public struct Cara
        {
            public string Nombre; public bool EjeX; public float Plano, Salida, A0, A1;
            public float Largo => A1 - A0;
        }

        // EjeX: la cara corre a lo largo de X y su plano es una Z. Salida: hacia donde es "afuera" (+1 / -1) sobre el eje del plano.
        public static readonly Cara[] Caras =
        {
            new Cara { Nombre = "sur",   EjeX = true,  Plano = CaraSur,   Salida = -1f, A0 = CaraOeste, A1 = CaraEste },
            new Cara { Nombre = "norte", EjeX = true,  Plano = CaraNorte, Salida =  1f, A0 = CaraOeste, A1 = CaraEste },
            new Cara { Nombre = "oeste", EjeX = false, Plano = CaraOeste, Salida = -1f, A0 = CaraSur,   A1 = CaraNorte },
            new Cara { Nombre = "este",  EjeX = false, Plano = CaraEste,  Salida =  1f, A0 = CaraSur,   A1 = CaraNorte },
        };

        public struct Zona { public int Cara; public float S0, S1, YMax; }
        public static readonly Zona[] ZonasLibres =
        {
            new Zona { Cara = Sur,   S0 = -4.6f,  S1 = 4.6f,   YMax = 4.75f },
            new Zona { Cara = Norte, S0 = -7f,    S1 = 7f,     YMax = 7.95f },
            new Zona { Cara = Oeste, S0 = -62.6f, S1 = -55.4f, YMax = 4.75f },
            new Zona { Cara = Este,  S0 = -62.6f, S1 = -55.4f, YMax = 4.75f },
        };

        // ---------------------------------------------------------------- estado (para checks y capturas)
        public static bool Listo { get; private set; }
        public static Transform GrupoActual { get; private set; }
        public static int Construcciones { get; private set; }
        public static int ObjetosCreados { get; private set; }
        public static int CoberturasCreadas { get; private set; }
        public static int SolidosCreados { get; private set; }
        public static int PiezasOmitidas { get; private set; }
        public static int Pilastras { get; private set; }
        public static int PilaresDelKit { get; private set; }
        public static int Rejas { get; private set; }
        public static int Luminarias { get; private set; }
        public static int LucesReales { get; private set; }
        public static int ObstaculosRecogidos { get; private set; }

        static readonly List<Bounds> obstaculos = new List<Bounds>(256);
        static readonly List<Vector2>[] ocupadoPorCara = { new List<Vector2>(), new List<Vector2>(), new List<Vector2>(), new List<Vector2>() };
        static readonly int[] pilaresPorCara = new int[4];
        static readonly List<GameObject> raices = new List<GameObject>(64);
        static readonly List<MeshRenderer> tmpRenderers = new List<MeshRenderer>(1024);
        static readonly Collider[] bufferChoques = new Collider[16];
        static Mesh esfera;
        static int propios;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reiniciar()
        {
            Listo = false; GrupoActual = null;
            Construcciones = ObjetosCreados = CoberturasCreadas = SolidosCreados = PiezasOmitidas = Pilastras = PilaresDelKit = Rejas = Luminarias = LucesReales = ObstaculosRecogidos = 0;
            obstaculos.Clear(); foreach (var l in ocupadoPorCara) l.Clear();
            for (int i = 0; i < 4; i++) pilaresPorCara[i] = 0;
            raices.Clear(); tmpRenderers.Clear();
            esfera = null; propios = 0;
        }

        // ---------------------------------------------------------------- geometria de las caras (publica: el check usa la misma)
        public static Vector3 Punto(Cara c, float s, float y, float d)
            => c.EjeX ? new Vector3(s, y, c.Plano + c.Salida * d) : new Vector3(c.Plano + c.Salida * d, y, s);

        // Caja alineada a los ejes: s a lo largo de la cara, y en altura, d hacia afuera (negativo = metido en el muro).
        public static Bounds CajaEnCara(Cara c, float s0, float s1, float y0, float y1, float d0, float d1)
        {
            var b = new Bounds(Punto(c, s0, y0, d0), Vector3.zero);
            b.Encapsulate(Punto(c, s1, y1, d1));
            return b;
        }

        public static Bounds CajaDeZona(Zona z, float haciaAfuera = 1.5f) => CajaEnCara(Caras[z.Cara], z.S0, z.S1, 0f, z.YMax, -0.8f, haciaAfuera);

        // Cuanto asoma b por fuera del plano de la cara, y si cae sobre su largo.
        public static float Asoma(Cara c, Bounds b)
            => c.EjeX ? (c.Salida > 0f ? b.max.z - c.Plano : c.Plano - b.min.z) : (c.Salida > 0f ? b.max.x - c.Plano : c.Plano - b.min.x);
        static float Lmin(Cara c, Bounds b) => c.EjeX ? b.min.x : b.min.z;
        static float Lmax(Cara c, Bounds b) => c.EjeX ? b.max.x : b.max.z;
        static bool SobreLaCara(Cara c, Bounds b) => Lmax(c, b) > c.A0 - 0.6f && Lmin(c, b) < c.A1 + 0.6f;

        static bool Solapan(Bounds a, Bounds b, float margen = 0.01f)
            => a.min.x < b.max.x - margen && a.max.x > b.min.x + margen && a.min.y < b.max.y - margen && a.max.y > b.min.y + margen && a.min.z < b.max.z - margen && a.max.z > b.min.z + margen;

        public static bool EnZonaLibre(Bounds b)
        {
            foreach (var z in ZonasLibres) if (Solapan(b, CajaDeZona(z))) return true;
            return false;
        }

        static bool Libre(Bounds b)
        {
            if (EnZonaLibre(b)) return false;
            for (int i = 0; i < obstaculos.Count; i++) if (Solapan(b, obstaculos[i], 0.02f)) return false;
            return true;
        }

        static float Piso(float x, float z)
        {
            // desde 0,6 m: el rayo nace por debajo de condensadores y tableros, asi que da el piso del patio y no la tapa de un equipo
            if (Physics.Raycast(new Vector3(x, 0.6f, z), Vector3.down, out var h, 2f, ~0, QueryTriggerInteraction.Ignore) && h.normal.y > 0.7f) return h.point.y;
            return 0.06f;
        }

        static Mesh MallaEsfera()
        {
            if (esfera != null) return esfera;
            var t = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            esfera = t.GetComponent<MeshFilter>().sharedMesh;
            Object.Destroy(t);
            return esfera;
        }

        // ---------------------------------------------------------------- entrada
        public static void Aplicar()
        {
            var raiz = AjustesDeNivelR17.RaizDeLaFachada;
            if (raiz == null) return;                                                   // la fachada R17 todavia no encontro su raiz: se reintenta
            if (GrupoActual != null && GrupoActual.parent == raiz) { Listo = true; return; }   // idempotente: ya esta armado en esta escena
            Armar(raiz);
        }

        static void Armar(Transform raiz)
        {
            int obj0 = AjustesDeNivelR17.ObjetosCreados, cob0 = AjustesDeNivelR17.CoberturasCreadas, omit0 = AjustesDeNivelR17.CoberturasOmitidas;
            propios = 0; PiezasOmitidas = 0; Pilastras = 0; Rejas = 0; Luminarias = 0; LucesReales = 0; SolidosCreados = 0;
            foreach (var l in ocupadoPorCara) l.Clear();

            var g = AjustesDeNivelR17.Grupo(raiz, GrupoExterior);
            GrupoActual = g; Listo = true; Construcciones++;
            RecogerPerimetro(g);

            var deco = AjustesDeNivelR17.Grupo(g, "R19_Decoracion");
            var cob = AjustesDeNivelR17.Grupo(g, "R19_Cobertura");
            var tex = AjustesDeNivelR17.Grupo(g, "R19_Textos");
            var luces = AjustesDeNivelR17.Grupo(g, "R19_Luces");

            // Materiales: los de la R17 se reusan (mismo color, misma clave = mismo material compartido); 5 nuevos.
            var zocalo = AjustesDeNivelR17.Mat("Gabinete", new Color(0.12f, 0.14f, 0.18f));
            var claro = AjustesDeNivelR17.Mat("Condensador", new Color(0.62f, 0.66f, 0.70f));
            var negro = AjustesDeNivelR17.Mat("Negro", new Color(0.05f, 0.05f, 0.06f));
            var metal = AjustesDeNivelR17.Mat("Metal", new Color(0.34f, 0.38f, 0.45f));
            var ducto = AjustesDeNivelR17.Mat("DuctoFachada", new Color(0.50f, 0.54f, 0.58f));
            var tablero = AjustesDeNivelR17.Mat("CarroServidor", new Color(0.18f, 0.20f, 0.26f));
            var cinta = AjustesDeNivelR17.Mat("CintaAmarilla", new Color(0.95f, 0.78f, 0.06f));
            var placa = AjustesDeNivelR17.Mat("PlacaOscura", new Color(0.04f, 0.05f, 0.07f));
            var ventilador = AjustesDeNivelR17.Mat("Ventilador", new Color(0.07f, 0.08f, 0.09f));
            var pilastra = AjustesDeNivelR17.Mat("R19_Pilastra", new Color(0.20f, 0.22f, 0.26f));
            var cian = AjustesDeNivelR17.Mat("R19_FranjaCian", new Color(0.2f, 0.8f, 1.0f), 0.9f);
            var luzFria = AjustesDeNivelR17.Mat("R19_LuzFria", new Color(0.85f, 0.93f, 1.0f), 1.4f);
            var luzRoja = AjustesDeNivelR17.Mat("R19_LuzRoja", new Color(1f, 0.08f, 0.06f), 2.2f);
            var vereda = AjustesDeNivelR17.Mat("R19_Vereda", new Color(0.42f, 0.42f, 0.42f));

            // ---- K1 zocalo, K2 cornisa, K4 franja: tiras continuas cortadas por las zonas libres que alcanzan su altura
            for (int ic = 0; ic < 4; ic++)
            {
                Tira(deco, "R19_Zocalo", ic, 0.10f, 0f, 0.60f, -0.02f, 0.10f, zocalo);
                Tira(deco, "R19_Cornisa", ic, 0.30f, AlturaDeLosMuros, AlturaDeLosMuros + 0.35f, -0.02f, 0.30f, claro);
                Tira(deco, "R19_Franja_Corporativa", ic, 0.04f, 7.14f, 7.26f, -0.02f, 0.04f, cian);
            }

            // ---- K3 pilastras: solo en las caras donde el kit modular no puso pilares que asomen, y en las esquinas que quedaron peladas
            ArmarPilastras(deco, pilastra);

            // ---- K7 norte, K8 sur, K9 este/oeste, K10 esquinas, K11 vereda y carteles
            ArmarNorte(deco, cob, luces, ducto, negro, tablero, cinta);
            ArmarSur(deco, pilastra, cian, zocalo, negro, cinta);
            ArmarLaterales(deco, cob, claro, ventilador, negro, metal);
            ArmarEsquinas(deco, negro, luzRoja);
            ArmarVereda(deco, tex, vereda, placa);

            // ---- K5 rejas y K6 luminarias: rellenan los panos entre pilares (despues de todo lo puntual, para no encimarse)
            ArmarPanos(deco, luces, negro, metal, luzFria);

            AjustesDeNivelR17.Combinar(deco);
            Physics.SyncTransforms();
            CoberturasCreadas = AjustesDeNivelR17.CoberturasCreadas - cob0;
            PiezasOmitidas += AjustesDeNivelR17.CoberturasOmitidas - omit0;
            ObjetosCreados = (AjustesDeNivelR17.ObjetosCreados - obj0) + propios;
            if (CoberturasCreadas > 0 && Application.isPlaying) Coberturas.Registrar();
            GameLog.Line($"R19 fachada: {ObjetosCreados} objetos, coberturas {CoberturasCreadas}, solidos {SolidosCreados}, pilastras {Pilastras} (pilares del kit {PilaresDelKit}), rejas {Rejas}, luminarias {Luminarias}, luces {LucesReales}, omitidas {PiezasOmitidas}, obstaculos {ObstaculosRecogidos}");
        }

        // ---------------------------------------------------------------- recoleccion (una vez por armado)
        // Lo que ya asoma por fuera de las caras (pilares SM_Mod_Muro_Pilar del kit, arte R17, carteles): de las raices de las escenas cargadas
        // (el kit modular vive en SC_Operacion_Cuarteles, no bajo la raiz de la R17). Muros, techo y suelo (> 12 m) no cuentan.
        static void RecogerPerimetro(Transform propio)
        {
            obstaculos.Clear();
            for (int i = 0; i < 4; i++) pilaresPorCara[i] = 0;
            var cinturon = new Bounds(new Vector3(0f, 4.6f, (CaraNorte + CaraSur) * 0.5f), new Vector3(CaraEste * 2f + 4f, 9.6f, (CaraNorte - CaraSur) + 4f));
            int pilares = 0;
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var s = SceneManager.GetSceneAt(i);
                if (!s.isLoaded) continue;
                s.GetRootGameObjects(raices);
                foreach (var go in raices)
                {
                    if (go == null || !go.activeInHierarchy) continue;
                    go.GetComponentsInChildren(false, tmpRenderers);
                    foreach (var r in tmpRenderers)
                    {
                        if (r == null || !r.enabled) continue;
                        var b = r.bounds;
                        if (!b.Intersects(cinturon) || Mathf.Max(b.size.x, b.size.z) > 12f) continue;
                        if (propio != null && r.transform.IsChildOf(propio)) continue;
                        bool asoma = false;
                        for (int k = 0; k < 4; k++)
                        {
                            var c = Caras[k];
                            if (!SobreLaCara(c, b) || Asoma(c, b) <= 0.03f) continue;
                            asoma = true;
                            // pilar del kit: alto (>= 6 m), angosto a lo largo de la cara y asomando >= 0,1 m
                            if (b.size.y >= 6f && Lmax(c, b) - Lmin(c, b) <= 2.6f && Asoma(c, b) >= 0.1f && Asoma(c, b) < 2.5f)
                            {
                                ocupadoPorCara[k].Add(new Vector2(Lmin(c, b), Lmax(c, b)));
                                pilaresPorCara[k]++; pilares++;
                            }
                        }
                        if (!asoma) continue;
                        if (r.GetComponentInParent<Soldier>() != null || r.GetComponentInParent<Vehicle>() != null) continue;
                        obstaculos.Add(b);
                    }
                }
            }
            raices.Clear(); tmpRenderers.Clear();
            ObstaculosRecogidos = obstaculos.Count; PilaresDelKit = pilares;
        }

        static void Registrar(GameObject go)
        {
            if (go == null) return;
            foreach (var r in go.GetComponentsInChildren<Renderer>(true)) obstaculos.Add(r.bounds);
        }

        // ---------------------------------------------------------------- constructores
        static GameObject CajaB(Transform p, string n, Bounds b, Material m) => AjustesDeNivelR17.Caja(p, n, b.center, b.size, m);

        // Pieza hija de una cobertura (escalada): se compensa la escala del padre para que mida lo pedido en el mundo.
        static GameObject Hija(Transform padre, string n, Mesh malla, Vector3 centro, Vector3 tam, Material m)
        {
            var go = AjustesDeNivelR17.Pieza(padre, n, malla, centro, tam, m, 0f);
            var ls = padre.lossyScale;
            go.transform.localScale = new Vector3(tam.x / Mathf.Max(ls.x, 0.001f), tam.y / Mathf.Max(ls.y, 0.001f), tam.z / Mathf.Max(ls.z, 0.001f));
            return go;
        }

        // Solido NO destructible (totem, bolardos): collider del primitivo + NavMeshObstacle con carving. Sin ObstacleMarker: no es cobertura de la IA.
        static GameObject Solido(Transform p, string n, PrimitiveType tipo, Vector3 centro, Vector3 tam, Material m)
        {
            var mitad = new Vector3(Mathf.Max(0.05f, tam.x * 0.5f - 0.04f), Mathf.Max(0.05f, tam.y * 0.5f - 0.15f), Mathf.Max(0.05f, tam.z * 0.5f - 0.04f));
            int k = Physics.OverlapBoxNonAlloc(centro + Vector3.up * 0.1f, mitad, bufferChoques, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < k; i++)
            {
                var c = bufferChoques[i];
                if (c == null || c.attachedRigidbody != null || c.GetComponentInParent<Soldier>() != null || c.GetComponentInParent<Vehicle>() != null) continue;
                PiezasOmitidas++;
                return null;
            }
            var go = GameObject.CreatePrimitive(tipo);
            go.name = n;
            go.transform.SetParent(p, false);
            go.transform.position = centro;
            go.transform.localScale = tipo == PrimitiveType.Cylinder ? new Vector3(tam.x, tam.y * 0.5f, tam.z) : tam;
            go.GetComponent<MeshRenderer>().sharedMaterial = m;
            var ob = go.AddComponent<UnityEngine.AI.NavMeshObstacle>();
            ob.shape = UnityEngine.AI.NavMeshObstacleShape.Box; ob.center = Vector3.zero;
            ob.size = tipo == PrimitiveType.Cylinder ? new Vector3(1f, 2f, 1f) : Vector3.one; ob.carving = true;
            SolidosCreados++; propios++;
            return go;
        }

        // Tira continua a lo largo de toda la cara, alargada "extra" en las puntas (cierra la esquina con la cara vecina), cortada por las zonas
        // libres de esa cara que llegan a su altura.
        static void Tira(Transform p, string n, int ic, float extra, float y0, float y1, float d0, float d1, Material m)
        {
            var c = Caras[ic];
            float a0 = c.A0 - (c.EjeX ? extra : 0f), a1 = c.A1 + (c.EjeX ? extra : 0f);
            float a = a0;
            foreach (var z in ZonasLibres)
            {
                if (z.Cara != ic || z.YMax <= y0) continue;
                if (z.S0 > a + 0.05f) CajaB(p, n, CajaEnCara(c, a, Mathf.Min(z.S0, a1), y0, y1, d0, d1), m);
                a = Mathf.Max(a, z.S1);
            }
            if (a1 > a + 0.05f) CajaB(p, n, CajaEnCara(c, a, a1, y0, y1, d0, d1), m);
        }

        static void Tramo(Transform p, string n, int ic, float s0, float s1, float y0, float y1, float d0, float d1, Material m)
        {
            // igual que Tira, pero entre s0 y s1 (la vereda no da toda la vuelta)
            var c = Caras[ic]; float a = s0;
            foreach (var z in ZonasLibres)
            {
                if (z.Cara != ic || z.YMax <= y0 || z.S1 <= s0 || z.S0 >= s1) continue;
                if (z.S0 > a + 0.05f) CajaB(p, n, CajaEnCara(c, a, Mathf.Min(z.S0, s1), y0, y1, d0, d1), m);
                a = Mathf.Max(a, z.S1);
            }
            if (s1 > a + 0.05f) CajaB(p, n, CajaEnCara(c, a, s1, y0, y1, d0, d1), m);
        }

        // ---------------------------------------------------------------- K3
        static void ArmarPilastras(Transform deco, Material mat)
        {
            const float Paso = 8.67f, Ancho = 0.70f, Saliente = 0.25f;
            for (int ic = 0; ic < 4; ic++)
            {
                var c = Caras[ic];
                if (pilaresPorCara[ic] >= 3) continue;   // el kit modular ya le da ritmo a esta cara: no se enciman pilastras
                int n = Mathf.Max(2, Mathf.RoundToInt(c.Largo / Paso));
                for (int i = 1; i < n; i++)                // las puntas son esquinas: las resuelve ArmarEsquinas
                {
                    float s = c.A0 + c.Largo * i / n;
                    foreach (var z in ZonasLibres)
                        if (z.Cara == ic && s > z.S0 - Ancho && s < z.S1 + Ancho)
                            s = (s - z.S0 < z.S1 - s) ? z.S0 - Ancho * 0.5f - 0.05f : z.S1 + Ancho * 0.5f + 0.05f;   // se corre al borde de la puerta
                    bool cerca = false;
                    foreach (var o in ocupadoPorCara[ic]) if (s > o.x - 1.5f && s < o.y + 1.5f) { cerca = true; break; }
                    var b = CajaEnCara(c, s - Ancho * 0.5f, s + Ancho * 0.5f, 0f, AlturaDeLosMuros, -0.02f, Saliente);
                    if (cerca || !Libre(b)) { PiezasOmitidas++; continue; }
                    Registrar(CajaB(deco, "R19_Pilastra", b, mat));
                    ocupadoPorCara[ic].Add(new Vector2(s - Ancho * 0.5f, s + Ancho * 0.5f));
                    Pilastras++;
                }
            }
            // esquinas: un poste de 0,7 x 0,7 que abraza la arista, solo si ningun pilar del kit la cubre ya
            foreach (float sx in new[] { CaraOeste, CaraEste })
                foreach (float sz in new[] { CaraSur, CaraNorte })
                {
                    float gx = Mathf.Sign(sx), gz = sz > -60f ? 1f : -1f;
                    var punta = new Vector3(sx + gx * 0.15f, 4f, sz + gz * 0.15f);
                    bool cubierta = false;
                    foreach (var o in obstaculos) if (o.Contains(punta)) { cubierta = true; break; }
                    if (cubierta) continue;
                    var b = new Bounds(new Vector3(sx - gx * 0.05f, AlturaDeLosMuros * 0.5f, sz - gz * 0.05f), new Vector3(0.70f, AlturaDeLosMuros, 0.70f));
                    Registrar(CajaB(deco, "R19_Pilastra_Esquina", b, mat));
                    int caraX = gz > 0f ? Norte : Sur, caraZ = gx > 0f ? Este : Oeste;
                    ocupadoPorCara[caraX].Add(new Vector2(b.min.x, b.max.x));
                    ocupadoPorCara[caraZ].Add(new Vector2(b.min.z, b.max.z));
                    Pilastras++;
                }
        }

        // Panos: los tramos libres de una cara entre pilares (del kit o R19), zonas libres y puntas. Los muy largos se parten.
        static List<Vector2> Panos(int ic)
        {
            var c = Caras[ic];
            var ocup = new List<Vector2>(ocupadoPorCara[ic]);
            foreach (var z in ZonasLibres) if (z.Cara == ic) ocup.Add(new Vector2(z.S0, z.S1));
            ocup.Sort((p, q) => p.x.CompareTo(q.x));
            var res = new List<Vector2>();
            float a = c.A0;
            void Agregar(float x0, float x1)
            {
                float w = x1 - x0;
                if (w < 2.7f) return;
                int partes = w > 9.5f ? Mathf.CeilToInt(w / 8f) : 1;
                for (int i = 0; i < partes; i++) res.Add(new Vector2(x0 + w * i / partes, x0 + w * (i + 1) / partes));
            }
            foreach (var o in ocup)
            {
                if (o.x > a) Agregar(a, Mathf.Min(o.x, c.A1));
                a = Mathf.Max(a, o.y);
            }
            if (c.A1 > a) Agregar(a, c.A1);
            return res;
        }

        // ---------------------------------------------------------------- K7: cara norte (la que se ve al salir hacia el tanque)
        static void ArmarNorte(Transform deco, Transform cob, Transform luces, Material ducto, Material negro, Material mTablero, Material cinta)
        {
            var c = Caras[Norte];
            var panos = Panos(Norte);
            const float DTubo = 0.35f, Diam = 0.18f;
            var cil = AjustesDeNivelR17.MallaCilindro(); var cubo = ParticleMaterialFactory.MallaCubo();
            foreach (int lado in new[] { -1, 1 })
            {
                // tuberia triple de x +-25 a +-8 (fuera de la salida norte), separada de los pilares del kit (asoman 0,2 m)
                float s0 = lado < 0 ? -25f : 8f, s1 = lado < 0 ? -8f : 25f;
                foreach (float y in new[] { 5.2f, 5.5f, 5.8f })
                {
                    var centro = Punto(c, (s0 + s1) * 0.5f, y, DTubo);
                    AjustesDeNivelR17.Pieza(deco, "R19_Tuberia", cil, centro, new Vector3(Diam, (s1 - s0) * 0.5f, Diam), ducto, Quaternion.Euler(0f, 0f, 90f));
                }
                for (float s = s0 + 0.4f; s <= s1 - 0.3f; s += 4.2f)   // soportes empotrados en el muro: la tuberia no flota
                    CajaB(deco, "R19_Tuberia_Soporte", CajaEnCara(c, s - 0.05f, s + 0.05f, 5.0f, 5.95f, -0.02f, DTubo + 0.08f), negro);
                obstaculos.Add(CajaEnCara(c, s0, s1, 5.05f, 5.92f, -0.02f, DTubo + 0.1f));

                // tablero electrico (cobertura) en el pano mas cercano a x = +-21, con su bajada hasta el piso al costado
                float meta = lado * 21f, mejor = float.MaxValue, cx = meta;
                foreach (var p in panos) { float m = (p.x + p.y) * 0.5f; if (Mathf.Sign(m) == lado && Mathf.Abs(m) >= 18f && Mathf.Abs(m - meta) < mejor) { mejor = Mathf.Abs(m - meta); cx = m; } }
                if (Mathf.Abs(cx) < 18f) cx = meta;
                float piso = Piso(cx, c.Plano + 0.65f);
                var t = AjustesDeNivelR17.Cobertura(cob, "R19_Tablero_Electrico", Punto(c, cx, piso + 0.9f, 0.65f), new Vector3(1.2f, 1.8f, 0.5f), mTablero, 0f, 300);
                if (t != null)
                {
                    // arte que depende del tablero: HIJO de el (se va con el al demolerlo)
                    Hija(t.transform, "R19_Tablero_Junta", cubo, Punto(c, cx, piso + 0.9f, 0.905f), new Vector3(0.03f, 1.5f, 0.02f), negro);
                    Hija(t.transform, "R19_Tablero_Aviso", cubo, Punto(c, cx - 0.3f, piso + 1.4f, 0.905f), new Vector3(0.32f, 0.28f, 0.02f), cinta);
                    Registrar(t);
                }
                float bx = cx - lado * 0.85f;   // hacia el centro de la cara
                float alto = 5.2f - piso;
                AjustesDeNivelR17.Pieza(deco, "R19_Tuberia_Bajada", cil, Punto(c, bx, piso + alto * 0.5f, DTubo), new Vector3(Diam, alto * 0.5f, Diam), ducto, 0f);
                obstaculos.Add(CajaEnCara(c, bx - 0.12f, bx + 0.12f, 0f, 5.3f, -0.02f, DTubo + 0.1f));
            }
        }

        // ---------------------------------------------------------------- K8: cara sur (entrada)
        static void ArmarSur(Transform deco, Material mPilastra, Material cian, Material oscuro, Material negro, Material cinta)
        {
            var c = Caras[Sur];
            var cubo = ParticleMaterialFactory.MallaCubo(); var cil = AjustesDeNivelR17.MallaCilindro();
            // marquesina sobre la puerta: base a 4,78 (SalaDespejadaEnRts la oculta sola en RTS), apoyada en los pilares del kit a los costados
            var losa = CajaEnCara(c, -5f, 5f, 4.78f, 5.03f, -0.02f, 2.4f);
            if (!EnZonaLibre(losa))
            {
                CajaB(deco, "R19_Marquesina", losa, mPilastra);
                CajaB(deco, "R19_Marquesina_Borde", CajaEnCara(c, -4.95f, 4.95f, 4.80f, 4.86f, 2.36f, 2.43f), cian);
                obstaculos.Add(losa);
            }
            else PiezasOmitidas++;
            // totem lector al costado de la puerta (solido, con carving)
            float sxT = 5.65f, pisoT = Piso(sxT, c.Plano - 0.9f);
            var bT = CajaEnCara(c, sxT - 0.225f, sxT + 0.225f, pisoT, pisoT + 1.5f, 0.725f, 1.075f);
            if (Libre(bT))
            {
                var totem = Solido(deco.parent, "R19_Totem_Lector", PrimitiveType.Cube, bT.center, bT.size, oscuro);
                if (totem != null)
                {
                    Hija(totem.transform, "R19_Totem_Pantalla", cubo, Punto(c, sxT, pisoT + 1.15f, 1.085f), new Vector3(0.30f, 0.35f, 0.02f), cian);
                    Registrar(totem);
                }
            }
            else PiezasOmitidas++;
            // 6 bolardos en z -84,5 (fila que protege la entrada; dejan pasar a pie entre ellos)
            foreach (float sx in new[] { -11f, -8.5f, -6f, 6f, 8.5f, 11f })
            {
                float py = Piso(sx, -84.5f);
                var b = Solido(deco.parent, "R19_Bolardo", PrimitiveType.Cylinder, new Vector3(sx, py + 0.45f, -84.5f), new Vector3(0.35f, 0.9f, 0.35f), cinta);
                if (b == null) continue;
                AjustesDeNivelR17.Pieza(deco, "R19_Bolardo_Tapa", cil, new Vector3(sx, py + 0.9f, -84.5f), new Vector3(0.37f, 0.03f, 0.37f), negro, 0f);
            }
        }

        // ---------------------------------------------------------------- K9: caras este y oeste (mitad norte)
        static void ArmarLaterales(Transform deco, Transform cob, Material claro, Material ventilador, Material negro, Material metal)
        {
            var cubo = ParticleMaterialFactory.MallaCubo(); var cil = AjustesDeNivelR17.MallaCilindro();
            foreach (int ic in new[] { Oeste, Este })
            {
                var c = Caras[ic];
                // 2 torres de enfriamiento (coberturas cilindricas), 1,25 m afuera del muro: no tocan los pilares del kit que asoman 1,15 m en el oeste
                foreach (float sz in new[] { -47f, -50.5f })
                {
                    float px = c.Plano + c.Salida * 2.25f, py = Piso(px, sz);
                    var t = AjustesDeNivelR17.Cobertura(cob, "R19_Torre_Enfriamiento", new Vector3(px, py + 1.2f, sz), new Vector3(2.0f, 2.4f, 2.0f), claro, 0f, 400, cilindroDePie: true);
                    if (t == null) continue;
                    Hija(t.transform, "R19_Torre_Ventilador", cil, new Vector3(px, py + 2.42f, sz), new Vector3(1.6f, 0.03f, 1.6f), ventilador);
                    Hija(t.transform, "R19_Torre_Banda", cil, new Vector3(px, py + 1.9f, sz), new Vector3(2.04f, 0.08f, 2.04f), negro);
                    Registrar(t);
                }
                // escalera de gato hasta la cornisa (largueros desde el piso)
                float sE = -44f;
                for (int intento = 0; intento < 3; intento++)
                {
                    var caja = CajaEnCara(c, sE - 0.3f, sE + 0.3f, 0f, AlturaDeLosMuros + 0.3f, -0.02f, 0.2f);
                    if (Libre(caja)) break;
                    sE += intento == 0 ? 1.2f : -2.4f;
                }
                var esc = CajaEnCara(c, sE - 0.3f, sE + 0.3f, 0f, AlturaDeLosMuros + 0.3f, -0.02f, 0.2f);
                if (Libre(esc))
                {
                    foreach (float k in new[] { -0.27f, 0.27f })
                        CajaB(deco, "R19_Escalera_Larguero", CajaEnCara(c, sE + k - 0.03f, sE + k + 0.03f, 0f, AlturaDeLosMuros + 0.3f, 0.12f, 0.18f), metal);
                    for (int i = 0; i < 10; i++)
                    {
                        float y = 0.6f + i * 0.75f;
                        CajaB(deco, "R19_Escalera_Peldano", CajaEnCara(c, sE - 0.27f, sE + 0.27f, y - 0.02f, y + 0.02f, 0.10f, 0.16f), metal);
                    }
                    obstaculos.Add(esc);
                }
                else PiezasOmitidas++;
                // camara de seguridad mirando al patio
                float sC = -52.5f;
                var bc = CajaEnCara(c, sC - 0.15f, sC + 0.15f, 5.35f, 5.7f, -0.02f, 0.8f);
                if (Libre(bc))
                {
                    CajaB(deco, "R19_Camara_Brazo", CajaEnCara(c, sC - 0.04f, sC + 0.04f, 5.56f, 5.64f, -0.02f, 0.45f), negro);
                    CajaB(deco, "R19_Camara_Cuerpo", CajaEnCara(c, sC - 0.11f, sC + 0.11f, 5.40f, 5.62f, 0.35f, 0.75f), claro);
                    CajaB(deco, "R19_Camara_Lente", CajaEnCara(c, sC - 0.06f, sC + 0.06f, 5.45f, 5.57f, 0.75f, 0.79f), negro);
                    obstaculos.Add(bc);
                }
                else PiezasOmitidas++;
            }
        }

        // ---------------------------------------------------------------- K10: luces rojas de obstruccion en las 4 esquinas (sobre la cornisa)
        static void ArmarEsquinas(Transform deco, Material negro, Material roja)
        {
            var esf = MallaEsfera();
            foreach (float sx in new[] { CaraOeste, CaraEste })
                foreach (float sz in new[] { CaraSur, CaraNorte })
                {
                    float gx = Mathf.Sign(sx), gz = sz > -60f ? 1f : -1f;
                    var p = new Vector3(sx + gx * 0.12f, 0f, sz + gz * 0.12f);
                    AjustesDeNivelR17.Caja(deco, "R19_Luz_Obstruccion_Mastil", new Vector3(p.x, 8.40f, p.z), new Vector3(0.06f, 0.12f, 0.06f), negro);
                    AjustesDeNivelR17.Pieza(deco, "R19_Luz_Obstruccion", esf, new Vector3(p.x, 8.60f, p.z), new Vector3(0.3f, 0.3f, 0.3f), roja, 0f);
                }
        }

        // ---------------------------------------------------------------- K11: vereda perimetral (N, E, O) y 2 carteles
        static void ArmarVereda(Transform deco, Transform tex, Material vereda, Material placa)
        {
            const float Ancho = 1.2f;
            float pN = Piso(-15f, CaraNorte + 0.6f), pE = Piso(CaraEste + 0.6f, -70f), pO = Piso(CaraOeste - 0.6f, -70f);
            Tramo(deco, "R19_Vereda", Norte, CaraOeste - Ancho, CaraEste + Ancho, pN - 0.03f, pN + 0.02f, 0f, Ancho, vereda);
            // este/oeste: hasta z -41 (de ahi al norte esta el muro de cierre del valle)
            Tramo(deco, "R19_Vereda", Este, CaraSur, -41f, pE - 0.03f, pE + 0.02f, 0f, Ancho, vereda);
            Tramo(deco, "R19_Vereda", Oeste, CaraSur, -41f, pO - 0.03f, pO + 0.02f, 0f, Ancho, vereda);

            var color = new Color(0.7f, 0.92f, 1f);
            Cartel(tex, Norte, -16f, "NODO 3 · SECTOR N", 180f, color, placa);
            Cartel(tex, Este, -51f, "NODO 3 · SECTOR E", 270f, color, placa);
        }

        static void Cartel(Transform tex, int ic, float sDeseado, string texto, float yaw, Color color, Material placa)
        {
            var c = Caras[ic];
            const float Ancho = 4.2f, Alto = 0.7f, Y = 3.0f;
            float mejor = float.MaxValue, s = sDeseado;
            foreach (var p in Panos(ic))
            {
                float m = (p.x + p.y) * 0.5f;
                if (p.y - p.x < Ancho + 0.2f) continue;
                if (Mathf.Abs(m - sDeseado) < mejor) { mejor = Mathf.Abs(m - sDeseado); s = m; }
            }
            var b = CajaEnCara(c, s - Ancho * 0.5f, s + Ancho * 0.5f, Y - Alto * 0.5f, Y + Alto * 0.5f, -0.02f, 0.14f);
            if (!Libre(b)) { PiezasOmitidas++; return; }
            var raiz = AjustesDeNivelR17.Texto(tex, texto, Punto(c, s, Y, 0.10f), yaw, 0.45f, color, placa, Ancho, Alto);
            if (raiz != null) raiz.name = "R19_Texto_" + texto;
            obstaculos.Add(b);
        }

        // ---------------------------------------------------------------- K5 rejas y K6 luminarias (+2 Light reales en la cara norte)
        static void ArmarPanos(Transform deco, Transform luces, Material marco, Material lama, Material luzFria)
        {
            const int RejasPorCara = 4, LuminariasPorCara = 2;
            for (int ic = 0; ic < 4; ic++)
            {
                var c = Caras[ic];
                var panos = Panos(ic);
                float yReja = ic == Norte ? 6.55f : 6.0f;   // en el norte, por encima de la tuberia triple (5,2-5,8) y por debajo de la franja (7,14)

                // rejas: los panos mas anchos primero
                var orden = new List<Vector2>(panos);
                orden.Sort((p, q) => (q.y - q.x).CompareTo(p.y - p.x));
                int nR = 0;
                foreach (var p in orden)
                {
                    if (nR >= RejasPorCara) break;
                    if (p.y - p.x < 2.8f) continue;
                    if (!Ubicar(c, p, 2.4f, yReja - 0.6f, yReja + 0.6f, -0.02f, 0.12f, out float s)) { PiezasOmitidas++; continue; }
                    CajaB(deco, "R19_Reja", CajaEnCara(c, s - 1.2f, s + 1.2f, yReja - 0.6f, yReja + 0.6f, -0.02f, 0.06f), marco);
                    foreach (float dy in new[] { -0.36f, 0f, 0.36f })
                        CajaB(deco, "R19_Reja_Lama", CajaEnCara(c, s - 1.12f, s + 1.12f, yReja + dy - 0.05f, yReja + dy + 0.05f, 0.04f, 0.12f), lama);
                    obstaculos.Add(CajaEnCara(c, s - 1.2f, s + 1.2f, yReja - 0.6f, yReja + 0.6f, -0.02f, 0.12f));
                    nR++; Rejas++;
                }

                // luminarias: panos alternos; de los candidatos, el primero y el ultimo (simetria aproximada)
                var cand = new List<Vector2>();
                for (int i = 0; i < panos.Count; i += 2) cand.Add(panos[i]);
                var elegidos = new List<Vector2>();
                if (cand.Count > 0) elegidos.Add(cand[0]);
                if (cand.Count > 1) elegidos.Add(cand[cand.Count - 1]);
                int nL = 0;
                foreach (var p in elegidos)
                {
                    if (nL >= LuminariasPorCara) break;
                    if (!Ubicar(c, p, 0.5f, 4.31f, 4.49f, -0.02f, 0.20f, out float s)) { PiezasOmitidas++; continue; }
                    var b = CajaEnCara(c, s - 0.25f, s + 0.25f, 4.31f, 4.49f, -0.02f, 0.20f);
                    CajaB(deco, "R19_Luminaria", b, luzFria);
                    obstaculos.Add(b);
                    nL++; Luminarias++;
                    if (ic == Norte && LucesReales < 2)
                    {
                        var lg = new GameObject("R19_Luz_Pared");
                        lg.transform.SetParent(luces, false);
                        lg.transform.position = Punto(c, s, 4.25f, 0.35f);
                        lg.transform.rotation = Quaternion.LookRotation((Punto(c, s, 0f, 3.2f) - lg.transform.position).normalized);
                        var l = lg.AddComponent<Light>();
                        l.type = LightType.Spot; l.spotAngle = 100f; l.range = 9f; l.intensity = 2.6f; l.color = new Color(0.85f, 0.93f, 1f); l.shadows = LightShadows.None;
                        LucesReales++; propios++;
                    }
                }
            }
        }

        // Busca dentro del pano un lugar libre para una pieza de ancho w (centro, y despues corriendose hacia los costados).
        static bool Ubicar(Cara c, Vector2 pano, float w, float y0, float y1, float d0, float d1, out float s)
        {
            float centro = (pano.x + pano.y) * 0.5f, holgura = (pano.y - pano.x - w) * 0.5f - 0.15f;
            foreach (float f in new[] { 0f, -0.5f, 0.5f, -1f, 1f })
            {
                s = centro + f * Mathf.Max(0f, holgura);
                if (Libre(CajaEnCara(c, s - w * 0.5f, s + w * 0.5f, y0, y1, d0, d1))) return true;
            }
            s = centro;
            return false;
        }
    }
}
