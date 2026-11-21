using System.Collections.Generic;
using UnityEngine;
using SP.Actors;
using SP.Core;
using SP.Operacion;
using SP.Vehicles;

namespace SP.Presentation
{
    // Ronda 17, pedidos 3 y 10 (plan 2.7): vida en la sala de servidores (interior) y en su fachada (exterior). Mismo patron que AjustesDeNivelOpus:
    // un vigilante en runtime (cada 2 s, idempotente por nombre de grupo, latches Listo*) que arma el arte sin reconstruir la escena.
    //   INTERIOR  (R17_Servidores_Vida, hijo de 9_Servidores): LEDs que parpadean (4 materiales de fase), mazos de cable y cintas en el piso,
    //             carros / cajas / gabinete abierto (con collider: son cobertura, con NavMeshObstacle carving), senaletica con OperacionTextoMundo y
    //             2 balizas rojas giratorias que se encienden mientras alguien hackea la computadora (panel del techo al 60 %).
    //   EXTERIOR  (R17_Servidores_Fachada, hijo de 3_CentroDeDatos): 6 condensadores de aire contra los muros este y oeste, conductos y bandejas,
    //             cartel "CENTRO DE DATOS · NODO 3" con su reflector, franjas de advertencia frente a las 3 puertas y un generador con tanque.
    // Todo el arte va SIN collider (mallas del cubo / cilindro compartidas, sin primitivos) salvo lo que es cobertura. Materiales estaticos compartidos.
    // Sin sonidos nuevos. Las piezas de arte sin colision se combinan por grupo con StaticBatchingUtility.
    public sealed class AjustesDeNivelR17 : MonoBehaviour
    {
        public const string GrupoInterior = "R17_Servidores_Vida", GrupoFachada = "R17_Servidores_Fachada";

        public static bool ListoInterior { get; private set; }
        public static bool ListoFachada { get; private set; }
        public static int ObjetosCreados { get; private set; }
        public static int CoberturasCreadas { get; private set; }
        public static int CoberturasOmitidas { get; private set; }
        public static int LedsTotales { get; private set; }
        public static int LedsConFase { get; private set; }
        public static float FraccionLedsConFase => LedsTotales > 0 ? (float)LedsConFase / LedsTotales : 0f;
        public static int TextosCreados { get; private set; }
        static OperacionTextoMundo otmCache;   // una sola busqueda para todos los carteles

        // Interior: el piso elevado tiene su cara de arriba en ~0,12 (juntas de baldosa a 0,125).
        const float PisoDecor = 0.12f, PisoCobertura = 0.10f;
        static readonly Dictionary<string, Material> materiales = new Dictionary<string, Material>();
        static Mesh cilindro;
        static readonly Collider[] bufferChoques = new Collider[24];

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reiniciar()
        {
            ListoInterior = ListoFachada = false;
            ObjetosCreados = CoberturasCreadas = CoberturasOmitidas = LedsTotales = LedsConFase = TextosCreados = 0;
            materiales.Clear(); cilindro = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Crear()
        {
            var go = new GameObject("AjustesDeNivelR17") { hideFlags = HideFlags.HideAndDontSave };
            go.AddComponent<AjustesDeNivelR17>();
        }

        OperacionDirector ultimo; float proximo;

        void Update()
        {
            if (Time.unscaledTime < proximo) return;
            proximo = Time.unscaledTime + 2f;
            var d = OperacionDirector.Instancia;
            if (d == null || !UnityEngine.SceneManagement.SceneManager.GetActiveScene().name.StartsWith("SC_Operacion", System.StringComparison.Ordinal)) return;
            if (d != ultimo) { ultimo = d; ListoInterior = ListoFachada = false; }
            Aplicar(d);
        }

        public static void Aplicar(OperacionDirector d)
        {
            if (!ListoInterior) ArmarInterior();
            if (!ListoFachada) ArmarFachada();
        }

        // ---------------------------------------------------------------- materiales y mallas compartidos
        static Material Mat(string clave, Color c, float emision = 0f)
        {
            if (materiales.TryGetValue(clave, out var m) && m != null) return m;
            m = SafeMaterial.Create(c);
            m.name = "R17_" + clave;
            if (emision > 0f && m.HasProperty("_EmissionColor"))
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", c * emision);
            }
            materiales[clave] = m;
            return m;
        }

        static Mesh MallaCilindro()
        {
            if (cilindro != null) return cilindro;
            var t = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            cilindro = t.GetComponent<MeshFilter>().sharedMesh;
            Object.Destroy(t);
            return cilindro;
        }

        static float Suelo(float x, float z, float respaldo)
        {
            if (Physics.Raycast(new Vector3(x, 4f, z), Vector3.down, out var h, 8f, ~0, QueryTriggerInteraction.Ignore) && h.normal.y > 0.7f && h.point.y < 1.5f) return h.point.y;
            return respaldo;
        }

        static Transform Grupo(Transform padre, string nombre)
        {
            var g = new GameObject(nombre).transform;
            g.SetParent(padre, false);
            g.localPosition = Vector3.zero; g.localRotation = Quaternion.identity; g.localScale = Vector3.one;
            return g;
        }

        // Pieza de arte SIN collider: GameObject + MeshFilter + MeshRenderer con la malla compartida (no se usan primitivos).
        static GameObject Pieza(Transform padre, string nombre, Mesh malla, Vector3 centro, Vector3 escala, Material mat, float yaw = 0f)
            => Pieza(padre, nombre, malla, centro, escala, mat, Quaternion.Euler(0f, yaw, 0f));

        static GameObject Pieza(Transform padre, string nombre, Mesh malla, Vector3 centro, Vector3 escala, Material mat, Quaternion rot)
        {
            var go = new GameObject(nombre);
            go.transform.SetParent(padre, false);
            go.transform.position = centro; go.transform.rotation = rot; go.transform.localScale = escala;
            go.AddComponent<MeshFilter>().sharedMesh = malla;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ObjetosCreados++;
            return go;
        }

        static GameObject Caja(Transform p, string n, Vector3 c, Vector3 e, Material m, float yaw = 0f) => Pieza(p, n, ParticleMaterialFactory.MallaCubo(), c, e, m, yaw);

        // Cobertura: cubo (o cilindro tumbado a lo largo de X) con collider, ObstacleMarker destructible y NavMeshObstacle con carving.
        // Si el lugar ya esta ocupado por otro collider solido (no soldados ni vehiculos), no se crea: la nueva decoracion jamas tapa nada.
        static GameObject Cobertura(Transform padre, string nombre, Vector3 centro, Vector3 tam, Material mat, float yaw, int vida, bool cilindroAlLargoDeX = false)
        {
            var rot = Quaternion.Euler(0f, yaw, 0f);
            var mitad = new Vector3(Mathf.Max(0.05f, tam.x * 0.5f - 0.04f), Mathf.Max(0.05f, tam.y * 0.5f - 0.15f), Mathf.Max(0.05f, tam.z * 0.5f - 0.04f));
            int n = Physics.OverlapBoxNonAlloc(centro + Vector3.up * 0.1f, mitad, bufferChoques, rot, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var c = bufferChoques[i];
                if (c == null || c.attachedRigidbody != null || c.GetComponentInParent<Soldier>() != null || c.GetComponentInParent<Vehicle>() != null) continue;
                CoberturasOmitidas++;
                return null;
            }
            var b = GameObject.CreatePrimitive(cilindroAlLargoDeX ? PrimitiveType.Cylinder : PrimitiveType.Cube);
            b.name = nombre;
            b.transform.SetParent(padre, false);
            b.transform.position = centro;
            if (cilindroAlLargoDeX) { b.transform.rotation = Quaternion.Euler(0f, yaw, 90f); b.transform.localScale = new Vector3(tam.y, tam.x * 0.5f, tam.z); }
            else { b.transform.rotation = rot; b.transform.localScale = tam; }
            b.GetComponent<Renderer>().sharedMaterial = mat;
            var m = b.AddComponent<ObstacleMarker>(); m.ConfigurarVida(vida);
            var ob = b.AddComponent<UnityEngine.AI.NavMeshObstacle>();
            ob.shape = UnityEngine.AI.NavMeshObstacleShape.Box; ob.size = Vector3.one; ob.center = Vector3.zero; ob.carving = true;
            CoberturasCreadas++; ObjetosCreados++;
            return b;
        }

        // Cartel con OperacionTextoMundo: placa oscura + TextMesh. "yaw" = hacia donde MIRA quien lee (0 = mira al norte, 180 = mira al sur).
        static void Texto(Transform padre, string texto, Vector3 pos, float yaw, float tam, Color color, Material placa, float ancho, float alto)
        {
            var raiz = new GameObject("R17_Texto_" + texto.Replace("\n", " ")).transform;
            raiz.SetParent(padre, false);
            raiz.position = pos; raiz.rotation = Quaternion.Euler(0f, yaw, 0f);
            if (placa != null)
            {
                var pl = Caja(raiz, "Placa", pos, new Vector3(ancho, alto, 0.12f), placa, yaw);
                pl.transform.localPosition = new Vector3(0f, 0f, 0.07f);
            }
            var t = new GameObject("Texto");
            t.transform.SetParent(raiz, false);
            t.transform.localPosition = Vector3.zero; t.transform.localRotation = Quaternion.identity;
            var tm = t.AddComponent<TextMesh>();
            var fuente = FuentesBelicas.Para(0);
            if (fuente == null) fuente = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            tm.font = fuente; tm.text = texto; tm.fontSize = 64; tm.characterSize = 0.1f * tam;
            tm.anchor = TextAnchor.MiddleCenter; tm.alignment = TextAlignment.Center; tm.fontStyle = FontStyle.Bold; tm.color = color;
            var mr = t.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                if (otmCache == null) otmCache = Object.FindFirstObjectByType<OperacionTextoMundo>();
                var otm = otmCache;
                if (otm != null && otm.material != null) mr.sharedMaterial = otm.material;   // con prueba de profundidad; el barrido de OperacionTextoMundo pone la textura de la fuente
            }
            TextosCreados++;
        }

        static void Combinar(Transform grupo)
        {
            if (grupo == null || grupo.childCount < 2) return;
            foreach (var mf in grupo.GetComponentsInChildren<MeshFilter>(true))
                if (mf.sharedMesh == null || !mf.sharedMesh.isReadable) return;   // sin malla legible no se combina (queda tal cual, sigue andando)
            StaticBatchingUtility.Combine(grupo.gameObject);
        }

        // ================================================================ INTERIOR
        static void ArmarInterior()
        {
            var raiz = GameObject.Find("9_Servidores");
            if (raiz == null) return;   // la sala no esta: se reintenta
            ListoInterior = true;
            if (raiz.transform.Find(GrupoInterior) != null) return;   // idempotente
            var g = Grupo(raiz.transform, GrupoInterior);

            // ---- 3a. LEDs vivos
            var leds = new List<MeshRenderer>();
            foreach (var r in raiz.GetComponentsInChildren<MeshRenderer>(true))
                if (r != null && (r.name == "Led" || r.name == "Torre_Led")) leds.Add(r);
            var parpadeo = g.gameObject.AddComponent<ParpadeoDeLeds>();
            parpadeo.Preparar(leds);
            LedsTotales = leds.Count; LedsConFase = parpadeo.LedsAsignados;

            var cableR = Mat("CableRojo", new Color(0.55f, 0.08f, 0.08f));
            var cableA = Mat("CableAzul", new Color(0.10f, 0.25f, 0.55f));
            var cinta = Mat("CintaAmarilla", new Color(0.95f, 0.78f, 0.06f));
            var negro = Mat("Negro", new Color(0.05f, 0.05f, 0.06f));
            var metal = Mat("Metal", new Color(0.34f, 0.38f, 0.45f));
            var gab = Mat("Gabinete", new Color(0.12f, 0.14f, 0.18f));
            var caja = Mat("CajaEquipo", new Color(0.22f, 0.26f, 0.20f));
            var carroMat = Mat("CarroServidor", new Color(0.18f, 0.20f, 0.26f));
            var cubo = ParticleMaterialFactory.MallaCubo();

            // ---- 3b. piso (sin collider): 6 mazos de cable con cinta amarilla encima
            var deco = Grupo(g, "Decoracion");
            var mazos = new[]
            {
                new Vector4(-14f, -75.5f, 3.2f, 8f), new Vector4(14.5f, -75.2f, 2.6f, -10f), new Vector4(-12f, -67f, 2.2f, 90f),
                new Vector4(12f, -66f, 2.4f, 90f), new Vector4(-20f, -53f, 3.0f, 25f), new Vector4(20f, -52f, 2.8f, -30f),
            };
            for (int i = 0; i < mazos.Length; i++)
            {
                var m = mazos[i]; float yaw = m.w; var rot = Quaternion.Euler(0f, yaw, 0f);
                var c = new Vector3(m.x, PisoDecor + 0.03f, m.y);
                Caja(deco, "Cable_Mazo_A", c + rot * new Vector3(-0.1f, 0f, 0f), new Vector3(0.14f, 0.06f, m.z), (i % 2 == 0) ? cableR : cableA, yaw);
                Caja(deco, "Cable_Mazo_B", c + rot * new Vector3(0.1f, 0f, 0f), new Vector3(0.14f, 0.06f, m.z * 0.92f), (i % 2 == 0) ? cableA : cableR, yaw);
                for (int k = -1; k <= 1; k += 2)
                    Caja(deco, "Cable_Cinta", c + rot * new Vector3(0f, 0.002f, k * m.z * 0.28f), new Vector3(0.42f, 0.07f, 0.09f), cinta, yaw);
            }
            // gabinete abierto: la puerta entreabierta y dos unidades sacadas, en el suelo (arte; el cuerpo del gabinete es cobertura mas abajo)
            Caja(deco, "Gabinete_Puerta_Abierta", new Vector3(-22.55f, 1.12f, -72.45f), new Vector3(0.82f, 2.0f, 0.04f), metal, 62f);
            Caja(deco, "Unidad_Suelo_A", new Vector3(-22.0f, PisoDecor + 0.15f, -71.7f), new Vector3(0.66f, 0.30f, 0.9f), metal, 18f);
            Caja(deco, "Unidad_Suelo_B", new Vector3(-21.7f, PisoDecor + 0.15f, -70.6f), new Vector3(0.66f, 0.30f, 0.9f), negro, -12f);
            Combinar(deco);

            // ---- 3b. cobertura (con collider + carving), fuera de la ruta de entrada sur (|x| < 5,5) y de las puertas laterales (z -63..-55)
            var cob = Grupo(g, "Cobertura");
            Cobertura(cob, "Carro_Servidores_1", new Vector3(-13f, PisoCobertura + 0.7f, -65f), new Vector3(0.9f, 1.4f, 1.5f), carroMat, 0f, 250);
            Cobertura(cob, "Carro_Servidores_2", new Vector3(13f, PisoCobertura + 0.7f, -65f), new Vector3(0.9f, 1.4f, 1.5f), carroMat, 0f, 250);
            Cobertura(cob, "Caja_Equipos_1", new Vector3(-23.6f, PisoCobertura + 0.5f, -69f), new Vector3(1.2f, 1.0f, 1.6f), caja, 0f, 250);
            Cobertura(cob, "Caja_Equipos_2", new Vector3(23.6f, PisoCobertura + 0.5f, -69f), new Vector3(1.2f, 1.0f, 1.6f), caja, 0f, 250);
            Cobertura(cob, "Caja_Equipos_3", new Vector3(23.6f, PisoCobertura + 0.5f, -73.5f), new Vector3(1.2f, 1.0f, 1.6f), caja, 0f, 250);
            Cobertura(cob, "Gabinete_Abierto", new Vector3(-23.4f, PisoCobertura + 1.1f, -73.5f), new Vector3(0.92f, 2.2f, 1.0f), gab, 0f, 350);

            // ---- 3c. senaletica
            var tex = Grupo(g, "Textos");
            var placa = Mat("PlacaOscura", new Color(0.04f, 0.05f, 0.07f));
            Texto(tex, "SALA DE SERVIDORES · ACCESO RESTRINGIDO", new Vector3(0f, 5.9f, -79.15f), 180f, 0.55f, new Color(1f, 0.82f, 0.15f), placa, 9.6f, 0.9f);
            string[] letras = { "A", "B" };
            for (int lado = 0; lado < 2; lado++)
                for (int k = 0; k < 4; k++)
                {
                    float sx = (lado == 0 ? -1f : 1f) * (5.6f + (k * 5 + 0.5f) * 0.98f);
                    Texto(tex, letras[lado] + (k + 1), new Vector3(sx, 3.1f, -79.15f), 180f, 0.4f, new Color(0.7f, 0.92f, 1f), placa, 1.1f, 0.6f);
                }

            // ---- 3d. balizas rojas (apagadas hasta que alguien hackea) + panel del techo
            var vivos = g.gameObject.AddComponent<ServidoresVivosR17>();
            vivos.Armar(g, raiz.transform);

            Physics.SyncTransforms();
            if (CoberturasCreadas > 0 && Application.isPlaying) Coberturas.Registrar();
            GameLog.Line($"R17 servidores: LEDs con fase {LedsConFase}/{LedsTotales}, coberturas {CoberturasCreadas} (omitidas {CoberturasOmitidas}), textos {TextosCreados}");
        }

        // ================================================================ EXTERIOR
        static void ArmarFachada()
        {
            var raiz = GameObject.Find("3_CentroDeDatos");
            if (raiz == null) return;
            ListoFachada = true;
            if (raiz.transform.Find(GrupoFachada) != null) return;
            var g = Grupo(raiz.transform, GrupoFachada);

            var cond = Mat("Condensador", new Color(0.62f, 0.66f, 0.70f));
            var ventilador = Mat("Ventilador", new Color(0.07f, 0.08f, 0.09f));
            var conducto = Mat("DuctoFachada", new Color(0.50f, 0.54f, 0.58f));
            var cinta = Mat("CintaAmarilla", new Color(0.95f, 0.78f, 0.06f));
            var negro = Mat("Negro", new Color(0.05f, 0.05f, 0.06f));
            var placa = Mat("PlacaOscura", new Color(0.04f, 0.05f, 0.07f));
            var gen = Mat("Generador", new Color(0.24f, 0.32f, 0.22f));
            var tanque = Mat("TanqueGasoil", new Color(0.78f, 0.45f, 0.08f));
            var cubo = ParticleMaterialFactory.MallaCubo(); var cil = MallaCilindro();

            var cob = Grupo(g, "Cobertura");
            var deco = Grupo(g, "Decoracion");
            var tex = Grupo(g, "Textos");

            // ---- 10a. condensadores: pegados al muro (x +-27,6 = 0,85 m afuera de la cara exterior), 3 por lado en z -76 / -72 / -68, lejos de las
            // puertas laterales (z -62..-56) y de los patios de aparicion (x +-52..60). 10b. cada uno con su conducto vertical hasta el techo.
            float[] zs = { -76f, -72f, -68f };
            foreach (int lado in new[] { -1, 1 })
            {
                float x = lado * 27.6f;
                foreach (float z in zs)
                {
                    float sue = Suelo(x, z, 0.06f);
                    var c = Cobertura(cob, "Condensador_Aire", new Vector3(x, sue + 0.7f, z), new Vector3(1.0f, 1.4f, 1.8f), cond, 0f, 400);
                    if (c == null) continue;
                    // ventilador (disco) sobre la cara que mira afuera, y conducto hasta la cornisa
                    Pieza(deco, "Condensador_Ventilador", cil, new Vector3(x + lado * 0.51f, sue + 0.7f, z), new Vector3(1.1f, 0.012f, 1.1f), ventilador, Quaternion.Euler(0f, 0f, 90f));
                    Caja(deco, "Condensador_Rejilla", new Vector3(x + lado * 0.52f, sue + 0.7f, z), new Vector3(0.02f, 1.2f, 0.08f), negro);
                    float alto = 4.6f - (sue + 1.4f);
                    Caja(deco, "Conducto_Vertical", new Vector3(lado * 27.1f, sue + 1.4f + alto * 0.5f, z), new Vector3(0.35f, alto, 0.45f), conducto);
                }
                foreach (float y in new[] { 3.4f, 4.3f })
                    Caja(deco, "Bandeja_Fachada", new Vector3(lado * 26.95f, y, -72f), new Vector3(0.22f, 0.12f, 10.4f), negro);
            }

            // ---- 10b. cartel de fachada y reflector (1 Light spot sin sombras)
            var posCartel = new Vector3(-12.5f, 3.4f, -80.85f);
            Texto(tex, "CENTRO DE DATOS · NODO 3", posCartel, 0f, 0.8f, Color.white, placa, 12f, 1.5f);
            var brazo = Caja(deco, "Reflector_Brazo", new Vector3(-12.5f, 4.55f, -81.3f), new Vector3(0.12f, 0.12f, 1.0f), negro);
            var luzGo = new GameObject("Reflector_Cartel");
            luzGo.transform.SetParent(g, false);
            luzGo.transform.position = new Vector3(-12.5f, 4.45f, -82.0f);
            luzGo.transform.rotation = Quaternion.LookRotation((posCartel - luzGo.transform.position).normalized);
            var luz = luzGo.AddComponent<Light>();
            luz.type = LightType.Spot; luz.spotAngle = 75f; luz.range = 8f; luz.intensity = 3.2f; luz.color = new Color(1f, 0.95f, 0.82f); luz.shadows = LightShadows.None;
            ObjetosCreados++;

            // ---- 10c. franjas de advertencia frente a las 3 puertas (sin collider)
            for (int i = 0; i < 8; i++)
            {
                float sx = -3.5f + i; float sz = -82.3f;
                Caja(deco, "Franja_Aviso_Sur", new Vector3(sx, Suelo(sx, sz, 0.06f) + 0.02f, sz), new Vector3(1.0f, 0.03f, 0.7f), (i % 2 == 0) ? cinta : negro);
            }
            foreach (int lado in new[] { -1, 1 })
                for (int i = 0; i < 6; i++)
                {
                    float sz = -61.5f + i; float sx = lado * 27.7f;
                    Caja(deco, "Franja_Aviso_Lateral", new Vector3(sx, Suelo(sx, sz, 0.06f) + 0.02f, sz), new Vector3(1.4f, 0.03f, 1.0f), (i % 2 == 0) ? cinta : negro);
                }

            // ---- 10c. generador con tanque de gasoil en la esquina SE (x 20..24, z -86..-83)
            float sg = Suelo(22f, -85f, 0.06f);
            var gc = Cobertura(cob, "Generador", new Vector3(22f, sg + 0.75f, -85f), new Vector3(3.2f, 1.5f, 1.6f), gen, 0f, 500);
            if (gc != null)
            {
                Pieza(deco, "Generador_Escape", cil, new Vector3(23.1f, sg + 1.5f + 0.3f, -85.4f), new Vector3(0.14f, 0.3f, 0.14f), negro, 0f);
                Caja(deco, "Generador_Franja", new Vector3(22f, sg + 0.9f, -85.82f), new Vector3(3.0f, 0.18f, 0.03f), cinta);
                for (int i = 0; i < 4; i++)
                    Caja(deco, "Generador_Aviso", new Vector3(20.7f + i * 0.9f, sg + 0.02f, -86.7f), new Vector3(0.9f, 0.03f, 0.6f), (i % 2 == 0) ? cinta : negro);
            }
            float st = Suelo(22f, -83.1f, 0.06f);
            Cobertura(cob, "Tanque_Gasoil", new Vector3(22f, st + 0.5f, -83.1f), new Vector3(2.4f, 1.0f, 1.0f), tanque, 0f, 400, cilindroAlLargoDeX: true);

            Combinar(deco);
            Physics.SyncTransforms();
            if (CoberturasCreadas > 0 && Application.isPlaying) Coberturas.Registrar();
            GameLog.Line($"R17 fachada: coberturas {CoberturasCreadas} (omitidas {CoberturasOmitidas})");
        }
    }

    // 3a. Un unico componente modula la emision de 4 materiales de fase (clones de Led Verde/Cian/Ambar): 4 SetColor como mucho por cuadro, sean
    // cuantos sean los LEDs. Cada LED de 9_Servidores ("Led", "Torre_Led") pasa a usar uno de ellos segun su color; los rojos de falla quedan fijos.
    public sealed class ParpadeoDeLeds : MonoBehaviour
    {
        public static int MaterialesDeFase { get; private set; }
        static readonly float[] Hz = { 2f, 3.5f, 5f, 7f };

        readonly Material[] mats = new Material[4];
        readonly Color[] encendido = new Color[4];
        readonly int[] estado = { -1, -1, -1, -1 };
        public int LedsAsignados { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reiniciar() { MaterialesDeFase = 0; }

        // Indices de fase: 0 = verde (A), 1 = verde (B, otro ritmo), 2 = cian, 3 = ambar.
        public void Preparar(List<MeshRenderer> leds)
        {
            var origen = new Material[4];
            foreach (var r in leds)
            {
                if (r == null || r.sharedMaterial == null) continue;
                var s = r.sharedMaterial.name;
                if (origen[0] == null && s.Contains("Verde") && !s.Contains("Acceso")) origen[0] = origen[1] = r.sharedMaterial;
                else if (origen[2] == null && s.Contains("Cian")) origen[2] = r.sharedMaterial;
                else if (origen[3] == null && s.Contains("Ambar")) origen[3] = r.sharedMaterial;
            }
            for (int i = 0; i < 4; i++)
            {
                if (origen[i] == null) continue;
                var m = new Material(origen[i]) { name = "R17_LedFase" + i, hideFlags = HideFlags.HideAndDontSave };
                encendido[i] = m.HasProperty("_EmissionColor") ? m.GetColor("_EmissionColor") : Color.white;
                mats[i] = m; MaterialesDeFase++;
            }
            int n = 0, parVerde = 0;
            foreach (var r in leds)
            {
                if (r == null || r.sharedMaterial == null) continue;
                var s = r.sharedMaterial.name; int f = -1;
                if (s.Contains("Acceso")) continue;
                if (s.Contains("Verde")) f = (parVerde++ % 2);
                else if (s.Contains("Cian")) f = 2;
                else if (s.Contains("Ambar")) f = 3;
                if (f < 0 || mats[f] == null) continue;
                r.sharedMaterial = mats[f]; n++;
            }
            LedsAsignados = n;
        }

        void Update()
        {
            float t = Time.unscaledTime;
            for (int i = 0; i < 4; i++)
            {
                if (mats[i] == null) continue;
                float fase = Mathf.Repeat(t * Hz[i], 1f);
                bool on;
                switch (i)
                {
                    case 0: on = fase < 0.5f; break;                                   // 2 Hz, parejo
                    case 1: on = fase < 0.18f || (fase > 0.36f && fase < 0.54f); break;   // 3,5 Hz, doble destello
                    case 2: on = fase < 0.75f; break;                                  // 5 Hz, casi siempre prendido
                    default: on = fase < 0.25f; break;                                 // 7 Hz, destello corto
                }
                int e = on ? 1 : 0;
                if (estado[i] == e) continue;
                estado[i] = e;
                mats[i].SetColor("_EmissionColor", on ? encendido[i] : encendido[i] * 0.08f);
            }
        }

        void OnDestroy()
        {
            for (int i = 0; i < 4; i++) if (mats[i] != null) { Destroy(mats[i]); MaterialesDeFase = Mathf.Max(0, MaterialesDeFase - 1); }
        }
    }

    // 3d. Balizas rojas giratorias de la sala: encendidas mientras la computadora tiene operador y el hackeo no esta completo.
    // Una sola Light spot (sin sombras) en la primera baliza; la segunda es solo la malla emisiva. Los paneles del techo bajan a 0,6.
    public sealed class ServidoresVivosR17 : MonoBehaviour
    {
        public static int BalizasEncendidas { get; private set; }
        public static bool HackeoActivo { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reiniciar() { BalizasEncendidas = 0; HackeoActivo = false; }

        readonly Transform[] pivotes = new Transform[2];
        readonly GameObject[] balizas = new GameObject[2];
        Light luz;
        Material matPanel; Color emisionPanel = Color.white; float factorPanel = 1f;

        public void Armar(Transform grupo, Transform sala)
        {
            var rojo = new Color(1f, 0.1f, 0.08f);
            var mRojo = SafeMaterial.Create(rojo);
            if (mRojo.HasProperty("_EmissionColor")) { mRojo.EnableKeyword("_EMISSION"); mRojo.SetColor("_EmissionColor", rojo * 3f); }
            var mBase = SafeMaterial.Create(new Color(0.08f, 0.08f, 0.09f));
            var cubo = ParticleMaterialFactory.MallaCubo();
            var posiciones = new[] { new Vector3(-24.6f, 6.9f, -50f), new Vector3(24.6f, 6.9f, -70f) };
            var bal = new GameObject("Balizas").transform; bal.SetParent(grupo, false);
            for (int i = 0; i < 2; i++)
            {
                var raiz = new GameObject("Baliza_Roja_" + (i + 1));
                raiz.transform.SetParent(bal, false); raiz.transform.position = posiciones[i];
                Pieza(raiz.transform, "Base", cubo, posiciones[i] + new Vector3(i == 0 ? 0.1f : -0.1f, -0.2f, 0f), new Vector3(0.35f, 0.12f, 0.35f), mBase);
                var piv = new GameObject("Pivote").transform; piv.SetParent(raiz.transform, false); piv.position = posiciones[i];
                Pieza(piv, "Cuerpo", cubo, posiciones[i], new Vector3(0.22f, 0.22f, 0.22f), mRojo);
                Pieza(piv, "Haz", cubo, posiciones[i] + new Vector3(0f, 0f, 0.28f), new Vector3(0.09f, 0.09f, 0.36f), mRojo);
                if (i == 0)
                {
                    var lg = new GameObject("Luz_Baliza"); lg.transform.SetParent(piv, false); lg.transform.position = posiciones[i];
                    luz = lg.AddComponent<Light>();
                    luz.type = LightType.Spot; luz.spotAngle = 60f; luz.range = 22f; luz.intensity = 4f; luz.color = rojo; luz.shadows = LightShadows.None;
                }
                pivotes[i] = piv; balizas[i] = raiz; raiz.SetActive(false);
            }
            // paneles del techo: se les da un clon propio (el material compartido es un asset del proyecto: no se toca)
            foreach (var r in sala.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (r == null || r.name != "Panel_Luz" || r.sharedMaterial == null) continue;
                if (matPanel == null)
                {
                    matPanel = new Material(r.sharedMaterial) { name = "R17_PanelLuzTecho", hideFlags = HideFlags.HideAndDontSave };
                    if (matPanel.HasProperty("_EmissionColor")) emisionPanel = matPanel.GetColor("_EmissionColor");
                }
                r.sharedMaterial = matPanel;
            }
        }

        static void Pieza(Transform padre, string nombre, Mesh malla, Vector3 centro, Vector3 escala, Material mat)
        {
            var go = new GameObject(nombre);
            go.transform.SetParent(padre, false);
            go.transform.position = centro; go.transform.localScale = escala;
            go.AddComponent<MeshFilter>().sharedMesh = malla;
            var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        void Update()
        {
            var d = OperacionDirector.Instancia;
            var pc = d != null ? d.computadora : null;
            bool activo = pc != null && pc.Operador != null && !pc.Completo;
            HackeoActivo = activo;
            int n = 0;
            for (int i = 0; i < 2; i++)
            {
                if (balizas[i] == null) continue;
                if (balizas[i].activeSelf != activo) balizas[i].SetActive(activo);
                if (activo)
                {
                    n++;
                    pivotes[i].rotation = Quaternion.Euler(18f, Time.unscaledTime * (i == 0 ? 220f : -220f) + i * 90f, 0f);
                }
            }
            BalizasEncendidas = n;
            if (matPanel != null)
            {
                float meta = activo ? 0.6f : 1f;
                if (!Mathf.Approximately(factorPanel, meta))
                {
                    factorPanel = Mathf.MoveTowards(factorPanel, meta, Time.unscaledDeltaTime * 0.8f);
                    matPanel.SetColor("_EmissionColor", emisionPanel * factorPanel);
                }
            }
        }

        void OnDestroy()
        {
            BalizasEncendidas = 0;
            if (matPanel != null) Destroy(matPanel);
        }
    }
}
