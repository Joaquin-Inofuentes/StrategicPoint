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
            m.SetColor("_BaseColor", new Color(0.1f, 0.9f, 1f));
            m.SetColor("_EmissionColor", new Color(0.2f, 1.6f, 2.0f) * 1.3f);
            return m;
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
            if (enPared)
            {
                // Panel plano contra la pared: marco oscuro, pantalla grande emisiva y una repisa; nada apoyado en el piso del cuarto.
                Pieza("Mueble", new Vector3(0f, 1.45f, 0.07f), new Vector3(1.9f, 1.25f, 0.14f), oscuro, true);
                Pieza("Pantalla", new Vector3(0f, 1.45f, 0.16f), new Vector3(1.7f, 1.0f, 0.04f), MaterialDePantallaDeTerminal(), false);
                Pieza("Teclado", new Vector3(0f, 0.95f, 0.28f), new Vector3(1.1f, 0.05f, 0.4f), oscuro, false);
            }
            else
            {
                Pieza("Mueble", new Vector3(0f, 0.45f, 0f), new Vector3(1.5f, 0.9f, 0.8f), oscuro, true);
                Pieza("Pantalla", new Vector3(0f, 1.3f, 0.05f), new Vector3(1.2f, 0.7f, 0.06f), MaterialDePantallaDeTerminal(), false).localRotation = Quaternion.Euler(-12f, 0f, 0f);
                Pieza("Teclado", new Vector3(0f, 0.93f, -0.22f), new Vector3(0.9f, 0.04f, 0.28f), oscuro, false);
            }
            var luz = new GameObject("Luz_Terminal").AddComponent<Light>();
            luz.transform.SetParent(raiz, false); luz.transform.localPosition = enPared ? new Vector3(0f, 1.5f, 1.1f) : new Vector3(0f, 1.6f, -0.9f);
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
