using UnityEngine;
using SP.Core;
using G = SP.Operacion.MansionGeometria;

namespace SP.Operacion
{
    // Pedido 2026-11-07: "en la parte de celdas falta una computadora para interactuar, con su rombo bien notorio y claro, con emissive para que resalte
    // entre todo, y las otras luces mas apagadas". La terminal se arma en runtime al entrar al 2.o piso (junto a la puerta de las celdas); las celdas se
    // abren con [E] parado frente a ella (antes se abrian desde 9 m de la puerta, sin ningun objeto que lo explicara).
    public partial class OperacionDirector
    {
        public const float AlcanceDeLaTerminalDeCeldas = 3.6f, FactorDeLucesDelPiso = 0.55f;
        public Transform TerminalDeCeldas { get; private set; }
        public int LucesApagadasDelPiso { get; private set; }
        public Vector3 PosTerminalDeCeldas => G.PuertaDeCeldas + new Vector3(-2.6f, 0f, -2.2f);

        // Bug #420 ("la zona de interaccion y el rombo deberian estar en la pantalla"): desde el #387 la terminal se monta contra la pared mas cercana,
        // pero la zona de uso y el rombo seguian en el punto de diseno (PosTerminalDeCeldas), unos metros de la pantalla. Ahora siguen a la terminal real:
        // la zona es el piso frente a la pantalla y el rombo flota sobre ella.
        public Vector3 PosDeUsoDeLaTerminal
        {
            get
            {
                if (TerminalDeCeldas == null) return PosTerminalDeCeldas;
                var p = TerminalDeCeldas.position + TerminalDeCeldas.forward * 1.0f;   // un metro delante de la pantalla
                p.y = G.Nivel(2);
                return p;
            }
        }
        public Vector3 PosDelRomboDeLaTerminal
        {
            get
            {
                if (TerminalDeCeldas == null) { var q = PosTerminalDeCeldas; q.y = G.Nivel(2); return q + Vector3.up * 0.8f; }
                var pantalla = TerminalDeCeldas.Find("Pantalla");
                return pantalla != null ? pantalla.position + TerminalDeCeldas.forward * 0.3f : TerminalDeCeldas.position + Vector3.up * 1.45f;
            }
        }

        static Material MaterialDePantallaDeTerminal()
        {
            Material modelo = null;
            foreach (var r in FindObjectsByType<Renderer>(FindObjectsInactive.Include))
                if (r != null && r.sharedMaterial != null && r.sharedMaterial.name == "M_Op_PantallaAzul") { modelo = r.sharedMaterial; break; }
            var m = modelo != null ? new Material(modelo) : new Material(Shader.Find("Universal Render Pipeline/Lit"));
            m.name = "M_Op_TerminalCeldas";
            m.EnableKeyword("_EMISSION");
            // la pantalla muestra las camaras de las celdas: la imagen es la base y tambien la emision (luz propia, que se lea de noche)
            var imagen = PantallaDeCamaras();
            m.SetTexture("_BaseMap", imagen); m.SetTexture("_MainTex", imagen); m.SetTexture("_EmissionMap", imagen);
            m.SetColor("_BaseColor", Color.white);
            m.SetColor("_EmissionColor", new Color(1.1f, 1.25f, 1.3f));
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            return m;
        }

        static Texture2D pantallaDeCamaras;

        // Monitor de vigilancia: cuatro camaras de celda (2x2). Cada una con barrotes, la silueta de un preso, un punto REC rojo y lineas de barrido.
        static Texture2D PantallaDeCamaras()
        {
            if (pantallaDeCamaras != null) return pantallaDeCamaras;
            const int W = 256, H = 144;
            var px = new Color32[W * H];
            var rng = new System.Random(77);
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    int qx = x < W / 2 ? 0 : 1, qy = y < H / 2 ? 0 : 1;
                    int lx = x % (W / 2), ly = y % (H / 2);
                    bool borde = lx < 2 || ly < 2 || lx >= W / 2 - 2 || ly >= H / 2 - 2;
                    float baseV = 0.10f + 0.05f * ly / (H / 2f);   // piso un poco mas claro
                    float ruido = ((float)rng.NextDouble() - 0.5f) * 0.05f;
                    float barrido = (y % 3 == 0) ? -0.025f : 0f;
                    var c = new Color(0.04f + baseV * 0.3f, 0.2f + baseV * 1.6f, 0.2f + baseV * 1.4f) + new Color(ruido, ruido, ruido) + new Color(barrido, barrido, barrido);
                    // barrotes verticales delante de la camara
                    if (!borde && lx % 20 >= 17) c = new Color(0.02f, 0.07f, 0.08f);
                    // silueta del preso: cabeza y cuerpo (distinta posicion en cada camara; la 4.a esta vacia)
                    int idx = qy * 2 + qx; if (idx != 3)
                    {
                        int cx = 30 + (idx * 23 % 40), cy = 22;
                        float dx = lx - cx, dy = ly - (cy + 20);
                        bool cabeza = dx * dx + dy * dy < 49f;
                        bool cuerpo = Mathf.Abs(lx - cx) < 10 && ly > cy - 12 && ly < cy + 12;
                        if (cabeza || cuerpo) c = new Color(0.55f, 0.95f, 0.85f) * (0.75f + 0.25f * ruido * 10f);
                    }
                    // punto REC
                    float rx = lx - 8, ry = ly - (H / 2 - 9);
                    if (rx * rx + ry * ry < 9f) c = new Color(1f, 0.1f, 0.1f);
                    if (borde) c = new Color(0.1f, 0.45f, 0.5f);
                    px[y * W + x] = c;
                }
            var t = new Texture2D(W, H, TextureFormat.RGBA32, false) { name = "T_Op_CamarasDeCeldas", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            t.SetPixels32(px); t.Apply(false, false);
            return pantallaDeCamaras = t;
        }

        public bool TerminalEnPared { get; private set; }
        public string DiagnosticoDeParedes { get; private set; } = "";

        // Pared lisa mas cercana (hasta 7 m) en 16 direcciones; ignora rejas, puertas y todo lo que no sea un muro de pie.
        bool BuscarParedParaLaTerminal(Vector3 desde, out Vector3 punto, out Vector3 normal)
        {
            punto = desde; normal = Vector3.forward;
            var origen = new Vector3(desde.x, G.Nivel(2) + 1.4f, desde.z);
            float mejor = float.MaxValue; bool hay = false; int sinGolpe = 0, inclinada = 0, filtrada = 0, ocupada = 0; string ejemplo = "";
            for (int i = 0; i < 16; i++)
            {
                var dir = Quaternion.Euler(0f, i * 22.5f, 0f) * Vector3.forward;
                if (!Physics.Raycast(origen, dir, out var h, 16f, ~0, QueryTriggerInteraction.Ignore)) { sinGolpe++; continue; }
                if (Mathf.Abs(h.normal.y) > 0.25f) { inclinada++; continue; }
                string n = h.collider.name.ToLowerInvariant();
                if (ejemplo == "") ejemplo = h.collider.name;
                if (n.Contains("barrote") || n.Contains("reja") || n.Contains("celda") || n.Contains("puerta") || n.Contains("hoja") || n.Contains("jamba") || n.Contains("cristal") || n.Contains("ventana")) { filtrada++; continue; }
                if (h.collider.GetComponentInParent<SP.Actors.Soldier>() != null) { filtrada++; continue; }
                // Despejada: nada a 1,2 m delante del punto elegido (que no quede tapada por un mueble).
                var libre = h.point + h.normal * 0.9f;
                if (Physics.CheckSphere(libre, 0.3f, ~0, QueryTriggerInteraction.Ignore)) { ocupada++; continue; }
                if (h.distance < mejor) { mejor = h.distance; punto = h.point; normal = new Vector3(h.normal.x, 0f, h.normal.z).normalized; hay = true; }
            }
            TerminalEnPared = hay;
            DiagnosticoDeParedes = $"origen={origen} sinGolpe={sinGolpe} inclinada={inclinada} filtrada={filtrada} ocupada={ocupada} ejemplo='{ejemplo}'";
            return hay;
        }

        void CrearTerminalDeCeldas()
        {
            if (TerminalDeCeldas != null) return;
            var raiz = new GameObject("Terminal_Celdas").transform;
            var pos = PosTerminalDeCeldas; pos.y = G.Nivel(2);
            raiz.position = pos;
            raiz.rotation = Quaternion.LookRotation(Plano(G.PuertaDeCeldas, pos).normalized, Vector3.up);
            // #387 (2026-11-08: "esto deberia estar en una pared y facil de ver"): se monta contra la pared mas cercana, de frente a la sala.
            bool enPared = BuscarParedParaLaTerminal(pos, out var puntoPared, out var normalPared);
            if (enPared)
            {
                pos = new Vector3(puntoPared.x, G.Nivel(2), puntoPared.z) + normalPared * 0.02f;
                raiz.position = pos; raiz.rotation = Quaternion.LookRotation(normalPared, Vector3.up);
            }
            var oscuro = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "M_Op_TerminalCeldas_Cuerpo" };
            oscuro.SetColor("_BaseColor", new Color(0.07f, 0.08f, 0.1f));
            Transform Pieza(string nombre, Vector3 local, Vector3 escala, Material mat, bool collider)
            {
                var c = GameObject.CreatePrimitive(PrimitiveType.Cube); c.name = nombre;
                c.transform.SetParent(raiz, false); c.transform.localPosition = local; c.transform.localScale = escala;
                c.GetComponent<Renderer>().sharedMaterial = mat;
                if (!collider) Destroy(c.GetComponent<Collider>());
                return c.transform;
            }
            // 2026-11-13 ("la pantalla para ver el monitor de la gente en celdas no la veo, falta el monitor, falta su mesa y teclado"): un puesto de trabajo de verdad
            // en vez de un panel pegado a la pared: mesa con patas, monitor con marco y pie, teclado, mouse y una silla. Mira hacia el cuarto (+Z local); la
            // espalda del monitor queda contra la pared. La pantalla muestra las camaras de las celdas (2x2).
            var madera = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "M_Op_TerminalCeldas_Mesa" };
            madera.SetColor("_BaseColor", new Color(0.42f, 0.31f, 0.2f)); madera.SetFloat("_Smoothness", 0.3f);
            var metal = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "M_Op_TerminalCeldas_Metal" };
            metal.SetColor("_BaseColor", new Color(0.52f, 0.55f, 0.6f)); metal.SetFloat("_Metallic", 0.55f); metal.SetFloat("_Smoothness", 0.5f);
            var teclas = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "M_Op_TerminalCeldas_Teclas" };
            teclas.SetColor("_BaseColor", new Color(0.2f, 0.22f, 0.26f)); teclas.SetFloat("_Smoothness", 0.4f);
            var cuero = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "M_Op_TerminalCeldas_Silla" };
            cuero.SetColor("_BaseColor", new Color(0.12f, 0.13f, 0.17f)); cuero.SetFloat("_Smoothness", 0.35f);
            // Mesa (1,7 x 0,9 m, tablero a 0,74 m), con una caja de colision invisible de piso a tablero para que nadie la atraviese
            Pieza("Mesa", new Vector3(0f, 0.74f, 0.5f), new Vector3(1.7f, 0.06f, 0.9f), madera, false);
            var col = Pieza("Mesa_Colision", new Vector3(0f, 0.4f, 0.5f), new Vector3(1.7f, 0.8f, 0.9f), oscuro, true); col.GetComponent<Renderer>().enabled = false;
            Pieza("Mesa_PataIzq", new Vector3(-0.8f, 0.36f, 0.5f), new Vector3(0.06f, 0.72f, 0.8f), metal, false);
            Pieza("Mesa_PataDer", new Vector3(0.8f, 0.36f, 0.5f), new Vector3(0.06f, 0.72f, 0.8f), metal, false);
            Pieza("Mesa_Panel", new Vector3(0f, 0.4f, 0.12f), new Vector3(1.6f, 0.62f, 0.03f), oscuro, false);
            // Monitor: pie, cuello, marco y pantalla con las camaras de las celdas
            Pieza("Monitor_Pie", new Vector3(0f, 0.775f, 0.3f), new Vector3(0.34f, 0.03f, 0.22f), metal, false);
            Pieza("Monitor_Cuello", new Vector3(0f, 0.92f, 0.3f), new Vector3(0.07f, 0.26f, 0.05f), metal, false);
            Pieza("Monitor_Marco", new Vector3(0f, 1.3f, 0.28f), new Vector3(1.14f, 0.7f, 0.07f), oscuro, false);
            Pieza("Pantalla", new Vector3(0f, 1.3f, 0.32f), new Vector3(1.02f, 0.58f, 0.02f), MaterialDePantallaDeTerminal(), false);
            Pieza("Monitor_Led", new Vector3(0.5f, 0.97f, 0.325f), new Vector3(0.025f, 0.015f, 0.01f), MaterialDePantallaDeTerminal(), false);
            // Teclado y mouse sobre la mesa
            Pieza("Teclado", new Vector3(0f, 0.79f, 0.7f), new Vector3(0.52f, 0.025f, 0.18f), teclas, false);
            Pieza("Teclado_Teclas", new Vector3(0f, 0.805f, 0.7f), new Vector3(0.47f, 0.008f, 0.135f), metal, false);
            Pieza("Mouse", new Vector3(0.45f, 0.79f, 0.7f), new Vector3(0.06f, 0.028f, 0.1f), teclas, false);
            // Silla de oficina delante (sin colision: es la zona de uso)
            Pieza("Silla_Asiento", new Vector3(0f, 0.48f, 1.5f), new Vector3(0.5f, 0.07f, 0.5f), cuero, false);
            Pieza("Silla_Respaldo", new Vector3(0f, 0.82f, 1.76f), new Vector3(0.48f, 0.55f, 0.07f), cuero, false);
            Pieza("Silla_Pie", new Vector3(0f, 0.25f, 1.5f), new Vector3(0.07f, 0.46f, 0.07f), metal, false);
            Pieza("Silla_Base1", new Vector3(0f, 0.04f, 1.5f), new Vector3(0.56f, 0.05f, 0.07f), metal, false);
            Pieza("Silla_Base2", new Vector3(0f, 0.04f, 1.5f), new Vector3(0.07f, 0.05f, 0.56f), metal, false);
            var luz = new GameObject("Luz_Terminal").AddComponent<Light>();
            luz.transform.SetParent(raiz, false); luz.transform.localPosition = new Vector3(0f, 1.55f, 1.0f);
            luz.type = LightType.Point; luz.color = new Color(0.3f, 0.95f, 1f); luz.intensity = 2.2f; luz.range = 7f; luz.shadows = LightShadows.None;
            TerminalDeCeldas = raiz;
            // Las demas luces del piso se apagan un poco para que la terminal resalte.
            float y0 = G.Nivel(2) - 1f, y1 = G.Nivel(2) + 6f; var centro = G.Centro(2); LucesApagadasDelPiso = 0;
            foreach (var l in FindObjectsByType<Light>())
            {
                if (l == null || l == luz || l.type == LightType.Directional || l.transform.IsChildOf(raiz)) continue;
                var p = l.transform.position; if (p.y < y0 || p.y > y1 || Plano(p, centro).magnitude > 45f) continue;
                l.intensity *= FactorDeLucesDelPiso; LucesApagadasDelPiso++;
            }
            GameLog.Line($"Terminal de las celdas creada en {pos}; {LucesApagadasDelPiso} luces del piso atenuadas");
        }
    }
}
