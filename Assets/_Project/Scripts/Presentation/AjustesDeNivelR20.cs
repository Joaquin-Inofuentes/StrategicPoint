using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using SP.Actors;
using SP.Core;
using SP.Vehicles;
using G = SP.Operacion.MansionGeometria;

namespace SP.Presentation
{
    // Ronda 20, punto 4 de Docs/COLA_PENDIENTES.md: "emprolijar totalmente la zona final" (objetivo 6: la azotea de la mansion, donde se posa el
    // helicoptero de extraccion; se llega por las torres de escalera C, D y E desde el 2.o piso). Parche de RUNTIME (las escenas son binarias): lo
    // llama AjustesDeNivelR17.Aplicar (mismo vigilante, cada 2 s). Idempotente: todo cuelga del grupo raiz propio R20_ZonaFinal (cacheado; si
    // sigue vivo no se hace nada). Sin Find: la mansion se ubica con un rayo contra la losa de la azotea y se recorre su jerarquia UNA vez.
    //
    // Lo ROTO que se limpia (se recuerda para poder revertirlo en las tomas ANTES de Bug998b):
    //   L1 Mastil_Gala_Remate: la esfera dorada del mastil de gala quedo flotando a 11 m sobre el helipuerto (AjustesDeNivelNov08 quito el mastil).
    //   L2 Cadena de las aranas: 1,2 m desde la lampara, atraviesan la losa de arriba y asoman 0,7 m en el piso siguiente (en la azotea, 3 palitos
    //      dorados delante del heli; en el 2.o piso, en la ruta a las escaleras). Se reemplazan por una cadena que termina en el cielorraso.
    //   L3 Apliques y cuadros de las torres C/D/E que quedaron por encima de la azotea, flotando en el aire sobre el hueco de la escalera.
    //   L4 Bases de los reflectores del techo (Reflector_Techo_O/E) a 0,9 m del piso sin nada abajo: se les pone pedestal.
    // Lo que se VISTE (todo empieza con "R20_"; nada con collider dentro de la zona reservada: heli r < 7,5 m, pasillos de las salidas de las
    // torres al heli, huellas de las torres y el punto donde aparece la escuadra):
    //   V1 helipuerto: plataforma oscura r 6,5, borde blanco (TLOF) sobre el disco amarillo y 12 luces verdes de borde.
    //   V2 sendas pintadas desde las salidas de las torres D y C hasta el helipuerto, con flechas (la ruta final).
    //   V3 2 torres de iluminacion con reflector (2 Light reales, sin sombra) y una manga de viento.
    //   V4 equipos de techo contra el parapeto (3 condensadores y 2 tableros: coberturas destructibles; el arte que depende de ellos es HIJO),
    //      escotilla y 4 ventilaciones (solidos con carving), conductos y 6 desagues.
    //   V5 zocalo continuo en la cara interior del parapeto (solo donde hay piso) y balizas calidas cada ~8 m.
    public static class AjustesDeNivelR20
    {
        public const string GrupoRaiz = "R20_ZonaFinal";
        public const float RadioReservadoHeli = 7.5f, AnchoPasillo = 1.6f;
        public static float Y => G.Nivel(3);

        // ---------------------------------------------------------------- estado (checks y tomas)
        public static bool Listo { get; private set; }
        public static Transform GrupoActual { get; private set; }
        public static Transform Mansion { get; private set; }
        public static int Construcciones { get; private set; }
        public static int ObjetosCreados { get; private set; }
        public static int CoberturasCreadas { get; private set; }
        public static int SolidosCreados { get; private set; }
        public static int PiezasOmitidas { get; private set; }
        public static int LucesReales { get; private set; }
        public static int CadenasRecortadas { get; private set; }
        public static int FlotantesApagados { get; private set; }
        public static int Pedestales { get; private set; }
        public static int RemateApagado { get; private set; }
        public static bool Aplicado { get; private set; } = true;

        static readonly List<GameObject> apagados = new List<GameObject>(32);
        static readonly List<Light> lucesApagadas = new List<Light>(16);
        static readonly Dictionary<string, Material> materiales = new Dictionary<string, Material>();
        static readonly Collider[] buffer = new Collider[16];
        static readonly List<Renderer> tmpR = new List<Renderer>(4096);
        static readonly List<Light> tmpL = new List<Light>(512);
        static int propios;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reiniciar()
        {
            Listo = false; GrupoActual = null; Mansion = null; Aplicado = true;
            Construcciones = ObjetosCreados = CoberturasCreadas = SolidosCreados = PiezasOmitidas = LucesReales = CadenasRecortadas = FlotantesApagados = Pedestales = RemateApagado = 0;
            apagados.Clear(); lucesApagadas.Clear(); materiales.Clear(); tmpR.Clear(); tmpL.Clear(); propios = 0;
        }

        // ---------------------------------------------------------------- zonas reservadas (publicas: el check usa las mismas)
        // Salidas de las torres que llegan a la azotea (C, D y E) y el punto donde el salto de prueba pone a la escuadra.
        public static readonly int[] TorresDeLaAzotea = { 2, 3, 4 };
        public static Vector3 Llegada => G.CentroDeLaAzotea + new Vector3(-9f, 0f, 2f);

        // Huella 2D de una torre (el hueco de la escalera y sus barandas).
        public static Rect HuellaDeTorre(int i)
        {
            var t = G.Torres[i];
            var a = t.Mundo(0f, 0f); var b = t.Mundo(G.TorreAncho, G.TorreFondo);
            return Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
        }

        static float DistSegmento(Vector2 p, Vector2 a, Vector2 b)
        {
            var ab = b - a; float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(ab.sqrMagnitude, 1e-4f));
            return (a + ab * t - p).magnitude;
        }

        static float DistCaja(Vector2 p, Bounds b) => new Vector2(Mathf.Max(b.min.x - p.x, 0f, p.x - b.max.x), Mathf.Max(b.min.z - p.y, 0f, p.y - b.max.z)).magnitude;

        // true si la caja (en planta) pisa el circulo del heli, un pasillo salida->heli, la huella de una torre o el punto de llegada.
        public static bool EnZonaReservada(Bounds b)
        {
            var c = G.CentroDeLaAzotea; var c2 = new Vector2(c.x, c.z);
            if (DistCaja(c2, b) < RadioReservadoHeli) return true;
            var l = Llegada; if (DistCaja(new Vector2(l.x, l.z), b) < 3f) return true;
            foreach (int i in TorresDeLaAzotea)
            {
                var h = HuellaDeTorre(i); h.xMin -= 0.3f; h.yMin -= 0.3f; h.xMax += 0.3f; h.yMax += 0.3f;
                if (b.max.x > h.xMin && b.min.x < h.xMax && b.max.z > h.yMin && b.min.z < h.yMax) return true;
                var s = G.SalidaDeTorre(i); var s2 = new Vector2(s.x, s.z);
                // el pasillo se mide desde las cuatro esquinas y el centro de la caja
                foreach (var p in new[] { new Vector2(b.min.x, b.min.z), new Vector2(b.max.x, b.min.z), new Vector2(b.min.x, b.max.z), new Vector2(b.max.x, b.max.z), new Vector2(b.center.x, b.center.z) })
                    if (DistSegmento(p, s2, c2) < AnchoPasillo) return true;
                if (DistSegmento(new Vector2(b.center.x, b.center.z), s2, c2) < AnchoPasillo + Mathf.Max(b.extents.x, b.extents.z)) return true;
            }
            return false;
        }

        // ---------------------------------------------------------------- entrada
        public static void Aplicar()
        {
            if (GrupoActual != null) { Listo = true; return; }   // idempotente: el grupo sigue vivo en esta escena
            var m = BuscarMansion();
            if (m == null) return;                               // la ciudad todavia no esta: se reintenta en el proximo tic del vigilante
            Mansion = m;
            Armar();
        }

        // Para las tomas ANTES/DESPUES y la linea base de NavMesh del check: apaga/prende el kit y devuelve lo roto a su estado original.
        public static void Mostrar(bool aplicado)
        {
            if (GrupoActual == null) return;
            Aplicado = aplicado;
            GrupoActual.gameObject.SetActive(aplicado);
            foreach (var go in apagados) if (go != null) go.SetActive(!aplicado);
            foreach (var l in lucesApagadas) if (l != null) l.enabled = !aplicado;
        }

        // Un rayo contra la losa de la azotea (sin Find): de la losa se sube hasta la raiz P_Op_8_Mansion.
        static Transform BuscarMansion()
        {
            var hits = Physics.RaycastAll(new Vector3(330f, Y + 12f, 258f), Vector3.down, 16f, ~0, QueryTriggerInteraction.Ignore);
            foreach (var h in hits)
            {
                if (Mathf.Abs(h.point.y - Y) > 0.2f) continue;
                for (var t = h.collider.transform; t != null; t = t.parent)
                    if (t.name.StartsWith("P_Op_8_Mansion", System.StringComparison.Ordinal)) return t;
            }
            return null;
        }

        static Material Mat(string clave, Color c, float emision = 0f)
        {
            if (materiales.TryGetValue(clave, out var m) && m != null) return m;
            m = SafeMaterial.Create(c);
            m.name = clave;
            if (emision > 0f && m.HasProperty("_EmissionColor")) { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", c * emision); }
            materiales[clave] = m;
            return m;
        }

        // ---------------------------------------------------------------- armado
        static void Armar()
        {
            int obj0 = AjustesDeNivelR17.ObjetosCreados, cob0 = AjustesDeNivelR17.CoberturasCreadas, omit0 = AjustesDeNivelR17.CoberturasOmitidas;
            propios = 0; PiezasOmitidas = 0; SolidosCreados = 0; LucesReales = 0;
            var raiz = new GameObject(GrupoRaiz).transform;
            GrupoActual = raiz; Listo = true; Aplicado = true; Construcciones++;
            var deco = AjustesDeNivelR17.Grupo(raiz, "R20_Decoracion");
            var pintura = AjustesDeNivelR17.Grupo(raiz, "R20_Pintura");
            var cob = AjustesDeNivelR17.Grupo(raiz, "R20_Cobertura");
            var sol = AjustesDeNivelR17.Grupo(raiz, "R20_Solidos");
            var luces = AjustesDeNivelR17.Grupo(raiz, "R20_Luces");

            var blanco = Mat("R20_PinturaBlanca", new Color(0.92f, 0.92f, 0.90f));
            var amarillo = Mat("R20_PinturaAmarilla", new Color(0.95f, 0.78f, 0.10f));
            var asfalto = Mat("R20_Plataforma", new Color(0.15f, 0.16f, 0.17f));
            var senda = Mat("R20_Senda", new Color(0.60f, 0.61f, 0.62f));
            var verde = Mat("R20_LuzVerde", new Color(0.25f, 1f, 0.45f), 2.4f);
            var calida = Mat("R20_LuzCalida", new Color(1f, 0.86f, 0.62f), 2.0f);
            var equipo = Mat("R20_Equipo", new Color(0.66f, 0.68f, 0.70f));
            var naranja = Mat("R20_Naranja", new Color(1f, 0.42f, 0.08f));
            var negro = AjustesDeNivelR17.Mat("Negro", new Color(0.05f, 0.05f, 0.06f));
            var metal = AjustesDeNivelR17.Mat("Metal", new Color(0.34f, 0.38f, 0.45f));
            var ventilador = AjustesDeNivelR17.Mat("Ventilador", new Color(0.07f, 0.08f, 0.09f));
            var zocalo = AjustesDeNivelR17.Mat("Gabinete", new Color(0.12f, 0.14f, 0.18f));

            LimpiarLoRoto(deco, metal);
            ArmarHelipuerto(pintura, asfalto, blanco, verde);
            ArmarSendas(pintura, senda, amarillo);
            ArmarIluminacion(sol, deco, luces, metal, calida, naranja, blanco);
            ArmarEquipos(cob, sol, deco, equipo, ventilador, negro, metal, amarillo);
            ArmarParapeto(deco, zocalo, calida);

            AjustesDeNivelR17.Combinar(deco);
            AjustesDeNivelR17.Combinar(pintura);
            Physics.SyncTransforms();
            CoberturasCreadas = AjustesDeNivelR17.CoberturasCreadas - cob0;
            PiezasOmitidas += AjustesDeNivelR17.CoberturasOmitidas - omit0;
            ObjetosCreados = (AjustesDeNivelR17.ObjetosCreados - obj0) + propios;
            if (CoberturasCreadas > 0 && Application.isPlaying) Coberturas.Registrar();
            GameLog.Line($"R20 zona final: {ObjetosCreados} objetos, coberturas {CoberturasCreadas}, solidos {SolidosCreados}, luces {LucesReales}, cadenas {CadenasRecortadas}, flotantes apagados {FlotantesApagados}, pedestales {Pedestales}, remate {RemateApagado}, omitidas {PiezasOmitidas}");
        }

        // ---------------------------------------------------------------- piso
        // Piso de la azotea bajo (x, z): el golpe mas alto de la mansion con normal hacia arriba. false si no hay losa (hueco de una torre).
        public static bool PisoEn(float x, float z, out float y)
        {
            y = Y;
            var hits = Physics.RaycastAll(new Vector3(x, Y + 2.5f, z), Vector3.down, 3.2f, ~0, QueryTriggerInteraction.Ignore);
            float mejor = float.NegativeInfinity;
            foreach (var h in hits)
            {
                if (h.normal.y < 0.7f || h.collider.attachedRigidbody != null) continue;
                if (Mansion != null && !h.collider.transform.IsChildOf(Mansion)) continue;
                if (h.point.y > mejor) mejor = h.point.y;
            }
            if (float.IsNegativeInfinity(mejor) || Mathf.Abs(mejor - Y) > 0.06f) return false;
            y = mejor; return true;
        }

        static bool PisoBajo(Bounds b)
        {
            foreach (var p in new[] { new Vector2(b.min.x + 0.05f, b.min.z + 0.05f), new Vector2(b.max.x - 0.05f, b.min.z + 0.05f), new Vector2(b.min.x + 0.05f, b.max.z - 0.05f), new Vector2(b.max.x - 0.05f, b.max.z - 0.05f), new Vector2(b.center.x, b.center.z) })
                if (!PisoEn(p.x, p.y, out _)) return false;
            return true;
        }

        // Hay algun collider (que no sea el piso, ni soldado/vehiculo/rigidbody) dentro de la caja.
        static bool Ocupado(Bounds b)
        {
            var mitad = new Vector3(Mathf.Max(0.04f, b.extents.x - 0.03f), Mathf.Max(0.04f, b.extents.y - 0.06f), Mathf.Max(0.04f, b.extents.z - 0.03f));
            int k = Physics.OverlapBoxNonAlloc(b.center + Vector3.up * 0.04f, mitad, buffer, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < k; i++)
            {
                var c = buffer[i];
                if (c == null || c.attachedRigidbody != null || c.GetComponentInParent<Soldier>() != null || c.GetComponentInParent<Vehicle>() != null) continue;
                return true;
            }
            return false;
        }

        // ---------------------------------------------------------------- L1-L4: lo roto
        static void Apagar(GameObject go) { if (go == null || !go.activeSelf) return; go.SetActive(false); apagados.Add(go); }

        static bool SobreHuecoDeTorre(Vector3 p)
        {
            foreach (int i in TorresDeLaAzotea) { var h = HuellaDeTorre(i); if (h.Contains(new Vector2(p.x, p.z))) return true; }
            return false;
        }

        static void LimpiarLoRoto(Transform deco, Material metal)
        {
            var huella = new Bounds(new Vector3((G.X0 + G.X1) * 0.5f, 12f, (G.Z0 + G.Z1) * 0.5f), new Vector3(G.X1 - G.X0 + 2f, 40f, G.Z1 - G.Z0 + 2f));
            var raizCiudad = Mansion.root;
            raizCiudad.GetComponentsInChildren(true, tmpR);
            foreach (var r in tmpR)
            {
                if (r == null || !r.gameObject.activeInHierarchy || !r.enabled) continue;
                var b = r.bounds;
                if (!huella.Intersects(b)) continue;
                string n = r.name;
                // L1
                if (n == "Mastil_Gala_Remate") { Apagar(r.gameObject); RemateApagado++; continue; }
                // L2: la cadena de cada arana termina en la cara de abajo de la losa que tiene encima
                if (n == "Cadena" && r.transform.IsChildOf(Mansion))
                {
                    float y0 = b.min.y;
                    if (Physics.Raycast(new Vector3(b.center.x, y0 + 0.02f, b.center.z), Vector3.up, out var techo, 2f, ~0, QueryTriggerInteraction.Ignore) && techo.point.y < b.max.y - 0.02f)
                    {
                        Apagar(r.gameObject);
                        float alto = techo.point.y - y0;
                        if (alto > 0.03f) AjustesDeNivelR17.Caja(deco, "R20_Cadena", new Vector3(b.center.x, y0 + alto * 0.5f, b.center.z), new Vector3(b.size.x, alto, b.size.z), r.sharedMaterial);
                        CadenasRecortadas++;
                    }
                    continue;
                }
                // L3: apliques y cuadros de las torres que asoman por encima de la azotea, en el aire sobre el hueco de la escalera
                if ((n.StartsWith("Aplique_", System.StringComparison.Ordinal) || n.Contains("_Lujo_")) && b.max.y > Y + 0.15f && SobreHuecoDeTorre(b.center))
                { Apagar(r.gameObject); FlotantesApagados++; continue; }
                // L4: base del reflector del techo sin apoyo: pedestal desde el piso
                if (n.StartsWith("Reflector_Techo", System.StringComparison.Ordinal) && n.EndsWith("_Base", System.StringComparison.Ordinal) && b.min.y > Y + 0.1f && b.min.y < Y + 2f)
                {
                    if (!PisoEn(b.center.x, b.center.z, out float py)) continue;
                    float alto = b.min.y - py + 0.02f;
                    var p = AjustesDeNivelR17.Caja(deco, "R20_Pedestal_Reflector", new Vector3(b.center.x, py + alto * 0.5f, b.center.z), new Vector3(b.size.x * 0.6f, alto, b.size.z * 0.6f), metal);
                    AjustesDeNivelR17.Caja(deco, "R20_Pedestal_Reflector_Base", new Vector3(b.center.x, py + 0.06f, b.center.z), new Vector3(b.size.x * 0.85f, 0.12f, b.size.z * 0.85f), metal);
                    Pedestales++;
                }
            }
            tmpR.Clear();
            // las luces de los apliques apagados (las que quedaron por encima de la azotea)
            raizCiudad.GetComponentsInChildren(true, tmpL);
            foreach (var l in tmpL)
            {
                if (l == null || !l.enabled || l.name != "Luz_Aplique") continue;
                var p = l.transform.position;
                if (p.y > Y + 0.2f && SobreHuecoDeTorre(p)) { l.enabled = false; lucesApagadas.Add(l); }
            }
            tmpL.Clear();
        }

        // ---------------------------------------------------------------- V1 helipuerto
        static void ArmarHelipuerto(Transform p, Material asfalto, Material blanco, Material verde)
        {
            var c = G.CentroDeLaAzotea; var cil = AjustesDeNivelR17.MallaCilindro(); var cubo = ParticleMaterialFactory.MallaCubo();
            // plataforma oscura bajo el disco amarillo (r 5,5): asoma como un anillo de 1 m. r 6,5 < 6,8 (borde del hueco de la torre E)
            AjustesDeNivelR17.Pieza(p, "R20_Helipuerto_Plataforma", cil, new Vector3(c.x, Y + 0.006f, c.z), new Vector3(13f, 0.006f, 13f), asfalto, 0f);
            // borde blanco (TLOF) sobre el disco amarillo, en 24 tramos
            const int Tramos = 24; const float R = 5.26f;
            for (int i = 0; i < Tramos; i++)
            {
                float a = (i + 0.5f) * 360f / Tramos;
                var pos = c + Quaternion.Euler(0f, a, 0f) * Vector3.forward * R; pos.y = Y + 0.038f;
                AjustesDeNivelR17.Caja(p, "R20_Helipuerto_Borde", pos, new Vector3(1.40f, 0.012f, 0.24f), blanco, a);
            }
            // 12 luces verdes de borde sobre la plataforma (r 6,1), solo donde hay losa
            for (int i = 0; i < 12; i++)
            {
                float a = (i + 0.5f) * 30f;
                var pos = c + Quaternion.Euler(0f, a, 0f) * Vector3.forward * 6.1f;
                if (!PisoEn(pos.x, pos.z, out float py)) { PiezasOmitidas++; continue; }
                AjustesDeNivelR17.Pieza(p, "R20_Helipuerto_Luz_Borde", cil, new Vector3(pos.x, py + 0.05f, pos.z), new Vector3(0.22f, 0.05f, 0.22f), verde, 0f);
            }
        }

        // ---------------------------------------------------------------- V2 sendas (ruta final): de la salida de D y de C al helipuerto
        static void ArmarSendas(Transform p, Material senda, Material amarillo)
        {
            var c = G.CentroDeLaAzotea;
            foreach (int ti in new[] { 3, 2 })
            {
                var s = G.SalidaDeTorre(ti);
                var dir = new Vector3(c.x - s.x, 0f, c.z - s.z);
                float largoTotal = dir.magnitude - 6.6f;   // termina en el borde de la plataforma
                if (largoTotal < 2f) continue;
                dir /= dir.magnitude;
                var lado = new Vector3(dir.z, 0f, -dir.x);
                // se recorta el comienzo hasta que haya losa a los dos lados (no pintar sobre el hueco)
                float d0 = 0f;
                while (d0 < largoTotal - 2f)
                {
                    var q = s + dir * d0;
                    if (PisoEn(q.x + lado.x * 0.8f, q.z + lado.z * 0.8f, out _) && PisoEn(q.x - lado.x * 0.8f, q.z - lado.z * 0.8f, out _)) break;
                    d0 += 0.5f;
                }
                bool sana = true;
                for (float d = d0; d <= largoTotal; d += 0.8f)
                {
                    var q = s + dir * d;
                    if (!PisoEn(q.x + lado.x * 0.8f, q.z + lado.z * 0.8f, out _) || !PisoEn(q.x - lado.x * 0.8f, q.z - lado.z * 0.8f, out _)) { sana = false; break; }
                }
                if (!sana) { PiezasOmitidas++; continue; }
                float largo = largoTotal - d0;
                float yaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
                var medio = s + dir * (d0 + largo * 0.5f); medio.y = Y + 0.006f;
                AjustesDeNivelR17.Caja(p, "R20_Senda", medio, new Vector3(1.5f, 0.012f, largo), senda, yaw);
                foreach (float k in new[] { -0.7f, 0.7f })
                {
                    var linea = medio + lado * k; linea.y = Y + 0.015f;
                    AjustesDeNivelR17.Caja(p, "R20_Senda_Linea", linea, new Vector3(0.1f, 0.008f, largo), amarillo, yaw);
                }
                // flechas (chevrones) apuntando al helipuerto
                int n = Mathf.Clamp(Mathf.FloorToInt(largo / 3.5f), 1, 4);
                for (int i = 0; i < n; i++)
                {
                    var f = s + dir * (d0 + largo * (i + 0.5f) / n); f.y = Y + 0.015f;
                    foreach (float sg in new[] { -1f, 1f })
                    {
                        var brazo = f + lado * (sg * 0.22f) - dir * 0.18f;
                        AjustesDeNivelR17.Caja(p, "R20_Senda_Flecha", brazo, new Vector3(0.12f, 0.008f, 0.62f), amarillo, yaw + sg * 40f);
                    }
                }
            }
        }

        // ---------------------------------------------------------------- V3 torres de iluminacion y manga de viento
        // Solido NO destructible: primitivo con collider + NavMeshObstacle con carving. Se omite si pisa la zona reservada, si no hay losa debajo o
        // si el lugar ya esta ocupado.
        static GameObject Solido(Transform p, string n, PrimitiveType tipo, Vector3 centro, Vector3 tam, Material m, bool carving = true)
        {
            var b = new Bounds(centro, tam);
            if (EnZonaReservada(b) || !PisoBajo(b) || Ocupado(b)) { PiezasOmitidas++; return null; }
            var go = GameObject.CreatePrimitive(tipo);
            go.name = n;
            go.transform.SetParent(p, false);
            go.transform.position = centro;
            go.transform.localScale = tipo == PrimitiveType.Cylinder ? new Vector3(tam.x, tam.y * 0.5f, tam.z) : tam;
            var mr = go.GetComponent<MeshRenderer>(); mr.sharedMaterial = m; mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            if (carving)
            {
                var ob = go.AddComponent<UnityEngine.AI.NavMeshObstacle>();
                ob.shape = UnityEngine.AI.NavMeshObstacleShape.Box; ob.center = Vector3.zero;
                ob.size = tipo == PrimitiveType.Cylinder ? new Vector3(1f, 2f, 1f) : Vector3.one; ob.carving = true;
            }
            SolidosCreados++; propios++;
            return go;
        }

        static void ArmarIluminacion(Transform sol, Transform deco, Transform luces, Material metal, Material calida, Material naranja, Material blanco)
        {
            var c = G.CentroDeLaAzotea; var cubo = ParticleMaterialFactory.MallaCubo();
            const float Alto = 4.2f;
            foreach (var xz in new[] { new Vector2(334f, 259.5f), new Vector2(310.5f, 273.5f) })
            {
                var poste = Solido(sol, "R20_Torre_Luz_Poste", PrimitiveType.Cylinder, new Vector3(xz.x, Y + Alto * 0.5f, xz.y), new Vector3(0.22f, Alto, 0.22f), metal);
                if (poste == null) continue;
                var haciaHeli = new Vector3(c.x - xz.x, 0f, c.z - xz.y).normalized;
                float yaw = Mathf.Atan2(haciaHeli.x, haciaHeli.z) * Mathf.Rad2Deg;
                // cabezal apoyado sobre el poste (no hijo: el poste lo sostiene) y la cara encendida mirando al helipuerto
                var cab = new Vector3(xz.x, Y + Alto + 0.2f, xz.y);
                AjustesDeNivelR17.Caja(deco, "R20_Torre_Luz_Cabezal", cab, new Vector3(0.9f, 0.4f, 0.35f), metal, yaw);
                AjustesDeNivelR17.Caja(deco, "R20_Torre_Luz_Lente", cab + haciaHeli * 0.18f, new Vector3(0.8f, 0.3f, 0.02f), calida, yaw);
                AjustesDeNivelR17.Caja(deco, "R20_Torre_Luz_Base", new Vector3(xz.x, Y + 0.08f, xz.y), new Vector3(0.6f, 0.16f, 0.6f), metal);
                if (LucesReales < 2)
                {
                    var lg = new GameObject("R20_Luz_Helipuerto");
                    lg.transform.SetParent(luces, false);
                    lg.transform.position = cab + haciaHeli * 0.3f;
                    lg.transform.rotation = Quaternion.LookRotation((new Vector3(c.x, Y, c.z) - lg.transform.position).normalized);
                    var l = lg.AddComponent<Light>();
                    l.type = LightType.Spot; l.spotAngle = 75f; l.range = 24f; l.intensity = 3.2f; l.color = new Color(1f, 0.92f, 0.80f); l.shadows = LightShadows.None;
                    LucesReales++; propios++;
                }
            }
            // manga de viento en la esquina sureste (sopla hacia el este, por encima del parapeto)
            var mx = new Vector3(346.4f, 0f, 250.6f);
            var mastil = Solido(sol, "R20_Manga_Mastil", PrimitiveType.Cylinder, new Vector3(mx.x, Y + 2.3f, mx.z), new Vector3(0.12f, 4.6f, 0.12f), metal, false);
            if (mastil != null)
            {
                float yM = Y + 4.35f;
                AjustesDeNivelR17.Caja(deco, "R20_Manga_Aro", new Vector3(mx.x + 0.1f, yM, mx.z), new Vector3(0.08f, 0.5f, 0.5f), metal);
                AjustesDeNivelR17.Caja(deco, "R20_Manga_A", new Vector3(mx.x + 0.45f, yM - 0.03f, mx.z), new Vector3(0.6f, 0.44f, 0.44f), naranja);
                AjustesDeNivelR17.Caja(deco, "R20_Manga_B", new Vector3(mx.x + 1.0f, yM - 0.09f, mx.z), new Vector3(0.5f, 0.34f, 0.34f), blanco);
                AjustesDeNivelR17.Caja(deco, "R20_Manga_C", new Vector3(mx.x + 1.45f, yM - 0.16f, mx.z), new Vector3(0.4f, 0.24f, 0.24f), naranja);
            }
        }

        // ---------------------------------------------------------------- V4 equipos de techo
        static GameObject Hija(Transform padre, string n, Mesh malla, Vector3 centro, Vector3 tam, Material m)
        {
            var go = AjustesDeNivelR17.Pieza(padre, n, malla, centro, tam, m, 0f);
            var ls = padre.lossyScale;
            go.transform.localScale = new Vector3(tam.x / Mathf.Max(ls.x, 0.001f), tam.y / Mathf.Max(ls.y, 0.001f), tam.z / Mathf.Max(ls.z, 0.001f));
            return go;
        }

        static GameObject CoberturaR20(Transform p, string n, Vector3 centro, Vector3 tam, Material m, int vida)
        {
            var b = new Bounds(centro, tam);
            if (EnZonaReservada(b) || !PisoBajo(b)) { PiezasOmitidas++; return null; }
            return AjustesDeNivelR17.Cobertura(p, n, centro, tam, m, 0f, vida);
        }

        static void ArmarEquipos(Transform cob, Transform sol, Transform deco, Material equipo, Material ventilador, Material negro, Material metal, Material amarillo)
        {
            var cubo = ParticleMaterialFactory.MallaCubo(); var cil = AjustesDeNivelR17.MallaCilindro();
            // 3 condensadores contra el parapeto (2 al norte-oeste, 1 al este): cuerpo claro, ventilador arriba y rejilla al frente (hijos)
            var condensadores = new[] { (new Vector3(302.8f, 0f, 284.3f), new Vector3(2.4f, 1.3f, 1.5f), new Vector3(0f, 0f, -1f)),
                                        (new Vector3(308.4f, 0f, 284.3f), new Vector3(2.4f, 1.3f, 1.5f), new Vector3(0f, 0f, -1f)),
                                        (new Vector3(346.45f, 0f, 255.0f), new Vector3(1.5f, 1.3f, 2.4f), new Vector3(-1f, 0f, 0f)) };
            foreach (var (pos, tam, frente) in condensadores)
            {
                var centro = new Vector3(pos.x, Y + tam.y * 0.5f, pos.z);
                var t = CoberturaR20(cob, "R20_Condensador", centro, tam, equipo, 300);
                if (t == null) continue;
                Hija(t.transform, "R20_Condensador_Ventilador", cil, new Vector3(pos.x, Y + tam.y + 0.015f, pos.z), new Vector3(1.0f, 0.015f, 1.0f), ventilador);
                Hija(t.transform, "R20_Condensador_Rejilla", cubo, centro + frente * (Mathf.Abs(frente.x) > 0f ? tam.x : tam.z) * 0.5f + frente * 0.01f,
                    Mathf.Abs(frente.x) > 0f ? new Vector3(0.02f, tam.y * 0.6f, tam.z * 0.8f) : new Vector3(tam.x * 0.8f, tam.y * 0.6f, 0.02f), negro);
                Hija(t.transform, "R20_Condensador_Aviso", cubo, centro + frente * ((Mathf.Abs(frente.x) > 0f ? tam.x : tam.z) * 0.5f + 0.015f) + Vector3.up * (tam.y * 0.38f),
                    Mathf.Abs(frente.x) > 0f ? new Vector3(0.02f, 0.14f, 0.3f) : new Vector3(0.3f, 0.14f, 0.02f), amarillo);
            }
            // 2 tableros electricos contra el parapeto (este y oeste)
            foreach (var (pos, tam, frente) in new[] { (new Vector3(346.95f, 0f, 268.5f), new Vector3(0.6f, 1.6f, 1.2f), new Vector3(-1f, 0f, 0f)),
                                                         (new Vector3(297.05f, 0f, 276.5f), new Vector3(0.6f, 1.6f, 1.2f), new Vector3(1f, 0f, 0f)) })
            {
                var centro = new Vector3(pos.x, Y + tam.y * 0.5f, pos.z);
                var t = CoberturaR20(cob, "R20_Tablero", centro, tam, metal, 250);
                if (t == null) continue;
                Hija(t.transform, "R20_Tablero_Junta", cubo, centro + frente * (tam.x * 0.5f + 0.01f), new Vector3(0.02f, tam.y * 0.85f, 0.03f), negro);
                Hija(t.transform, "R20_Tablero_Aviso", cubo, centro + frente * (tam.x * 0.5f + 0.01f) + new Vector3(0f, 0.45f, 0.3f), new Vector3(0.02f, 0.26f, 0.3f), amarillo);
            }
            // 3 jardineras (coberturas bajas, como las del borde sur) en el cuadrante noroeste y al sur del centro; el seto es HIJO
            var hoja = AjustesDeNivelR17.Mat("Seto", new Color(0.16f, 0.30f, 0.14f));
            foreach (var j in new[] { new Vector2(303.6f, 279.6f), new Vector2(306.6f, 271.6f), new Vector2(316.4f, 255.4f) })
            {
                var centro = new Vector3(j.x, Y + 0.35f, j.y);
                var t = CoberturaR20(cob, "R20_Jardinera", centro, new Vector3(2.4f, 0.7f, 1.2f), equipo, 300);
                if (t == null) continue;
                Hija(t.transform, "R20_Jardinera_Seto", cubo, new Vector3(j.x, Y + 0.95f, j.y), new Vector3(2.2f, 0.5f, 1.0f), hoja);
                Hija(t.transform, "R20_Jardinera_Borde", cubo, new Vector3(j.x, Y + 0.71f, j.y), new Vector3(2.46f, 0.04f, 1.26f), metal);
            }
            // escotilla de techo (entre las torres E y C) y 5 ventilaciones tipo hongo
            var esc = Solido(sol, "R20_Escotilla", PrimitiveType.Cube, new Vector3(332f, Y + 0.3f, 280f), new Vector3(1.3f, 0.6f, 1.3f), equipo);
            if (esc != null) AjustesDeNivelR17.Caja(deco, "R20_Escotilla_Manija", new Vector3(332f, Y + 0.64f, 280f), new Vector3(0.5f, 0.08f, 0.06f), negro);
            foreach (var v in new[] { new Vector2(330.5f, 254.2f), new Vector2(328.6f, 253.2f), new Vector2(314.5f, 252.6f), new Vector2(312.2f, 278.2f), new Vector2(311.4f, 256.8f) })
            {
                var cuerpo = Solido(sol, "R20_Ventilacion", PrimitiveType.Cylinder, new Vector3(v.x, Y + 0.35f, v.y), new Vector3(0.36f, 0.7f, 0.36f), metal);
                if (cuerpo == null) continue;
                AjustesDeNivelR17.Pieza(deco, "R20_Ventilacion_Sombrero", cil, new Vector3(v.x, Y + 0.76f, v.y), new Vector3(0.62f, 0.06f, 0.62f), metal, 0f);
            }
            // conductos a ras del piso contra el parapeto (del tablero este al condensador este, del tablero oeste hacia el norte)
            Conducto(deco, metal, new Vector3(347.2f, 0f, 256.4f), new Vector3(347.2f, 0f, 267.8f));
            Conducto(deco, metal, new Vector3(296.85f, 0f, 277.2f), new Vector3(296.85f, 0f, 284.6f));
            Conducto(deco, metal, new Vector3(301.4f, 0f, 285.24f), new Vector3(309.8f, 0f, 285.24f));
            // 6 desagues
            foreach (var d in new[] { new Vector2(300.5f, 264f), new Vector2(312f, 250.6f), new Vector2(332f, 250.6f), new Vector2(343f, 266f), new Vector2(312f, 281f), new Vector2(332f, 272.5f) })
            {
                if (!PisoEn(d.x, d.y, out float py)) { PiezasOmitidas++; continue; }
                AjustesDeNivelR17.Caja(deco, "R20_Desague", new Vector3(d.x, py + 0.005f, d.y), new Vector3(0.5f, 0.01f, 0.5f), negro);
            }
        }

        static void Conducto(Transform deco, Material m, Vector3 a, Vector3 b)
        {
            var medio = (a + b) * 0.5f; var d = b - a; float largo = d.magnitude;
            if (!PisoEn(a.x, a.z, out _) || !PisoEn(b.x, b.z, out _) || !PisoEn(medio.x, medio.z, out _)) { PiezasOmitidas++; return; }
            float yaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
            AjustesDeNivelR17.Caja(deco, "R20_Conducto", new Vector3(medio.x, Y + 0.07f, medio.z), new Vector3(0.16f, 0.14f, largo), m, yaw);
        }

        // ---------------------------------------------------------------- V5 zocalo y balizas en la cara interior del parapeto
        static void ArmarParapeto(Transform deco, Material zocalo, Material calida)
        {
            // las cuatro caras interiores: (punto de inicio, direccion, largo, normal hacia adentro)
            float xi0 = G.X0 + G.Muro, xi1 = G.X1 - G.Muro, zi0 = G.Z0 + G.Muro, zi1 = G.Z1 - G.Muro;
            var caras = new[] { (new Vector3(xi0, 0f, zi0), Vector3.right, xi1 - xi0, Vector3.forward),
                                (new Vector3(xi0, 0f, zi1), Vector3.right, xi1 - xi0, Vector3.back),
                                (new Vector3(xi0, 0f, zi0), Vector3.forward, zi1 - zi0, Vector3.right),
                                (new Vector3(xi1, 0f, zi0), Vector3.forward, zi1 - zi0, Vector3.left) };
            const float Paso = 0.5f, Espesor = 0.06f, AltoZ = 0.25f;
            foreach (var (ini, dir, largo, nrm) in caras)
            {
                // tramos con losa a 0,3 m del muro
                float tramo0 = -1f; float ultimaBaliza = -99f;
                for (float s = 0f; s <= largo + 0.001f; s += Paso)
                {
                    var q = ini + dir * s + nrm * 0.3f;
                    bool hay = s < largo && PisoEn(q.x, q.z, out _);
                    if (hay && tramo0 < 0f) tramo0 = s;
                    if ((!hay || s + Paso > largo) && tramo0 >= 0f)
                    {
                        float s1 = hay ? largo : s;
                        if (s1 - tramo0 >= 1f)
                        {
                            var medio = ini + dir * ((tramo0 + s1) * 0.5f) + nrm * (Espesor * 0.5f);
                            medio.y = Y + AltoZ * 0.5f;
                            var tam = Mathf.Abs(dir.x) > 0f ? new Vector3(s1 - tramo0, AltoZ, Espesor) : new Vector3(Espesor, AltoZ, s1 - tramo0);
                            AjustesDeNivelR17.Caja(deco, "R20_Zocalo_Parapeto", medio, tam, zocalo);
                            // balizas calidas cada ~8 m sobre el tramo, a 0,8 m (empotradas en el parapeto), si no hay nada delante
                            for (float sb = tramo0 + 2f; sb < s1 - 1f; sb += 8f)
                            {
                                if (sb - ultimaBaliza < 6f) continue;
                                var pb = ini + dir * sb + nrm * 0.03f; pb.y = Y + 0.8f;
                                var caja = new Bounds(pb + nrm * 0.3f, new Vector3(0.6f, 0.6f, 0.6f));
                                if (Ocupado(caja)) { PiezasOmitidas++; continue; }
                                var tamB = Mathf.Abs(dir.x) > 0f ? new Vector3(0.36f, 0.14f, 0.06f) : new Vector3(0.06f, 0.14f, 0.36f);
                                AjustesDeNivelR17.Caja(deco, "R20_Baliza_Parapeto", pb, tamB, calida);
                                ultimaBaliza = sb;
                            }
                        }
                        tramo0 = -1f;
                    }
                }
            }
        }
    }
}
