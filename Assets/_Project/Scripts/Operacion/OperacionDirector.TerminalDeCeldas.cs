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

        void CrearTerminalDeCeldas()
        {
            if (TerminalDeCeldas != null) return;
            var raiz = new GameObject("Terminal_Celdas").transform;
            var pos = PosTerminalDeCeldas; pos.y = G.Nivel(2);
            raiz.position = pos;
            raiz.rotation = Quaternion.LookRotation(Plano(G.PuertaDeCeldas, pos).normalized, Vector3.up);
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
            Pieza("Mueble", new Vector3(0f, 0.45f, 0f), new Vector3(1.5f, 0.9f, 0.8f), oscuro, true);
            Pieza("Pantalla", new Vector3(0f, 1.3f, 0.05f), new Vector3(1.2f, 0.7f, 0.06f), MaterialDePantallaDeTerminal(), false).localRotation = Quaternion.Euler(-12f, 0f, 0f);
            Pieza("Teclado", new Vector3(0f, 0.93f, -0.22f), new Vector3(0.9f, 0.04f, 0.28f), oscuro, false);
            var luz = new GameObject("Luz_Terminal").AddComponent<Light>();
            luz.transform.SetParent(raiz, false); luz.transform.localPosition = new Vector3(0f, 1.6f, -0.9f);
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
