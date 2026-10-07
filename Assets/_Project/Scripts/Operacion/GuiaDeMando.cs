using UnityEngine;
using UnityEngine.AI;
using SP.Core;
using SP.Presentation;

namespace SP.Operacion
{
    // P7 (#114): guia del tutorial del mando tactico (objetivo 5, "Aca me perdi: falta mas instrucciones o punteado del piso"). Sobre el paso
    // ACTUAL del panel (OperacionDirector.GuiaDelPaso) dibuja en el mundo:
    //   - una flecha 3D grande y pulsante sobre el objetivo del paso (el soldado a elegir o el circulo de sector adonde mandarlo);
    //   - cuando el paso es "mandalo al sector X", el punteado del piso por el NavMesh desde ese soldado hasta el centro del sector.
    // Funciona en RTS y en FPS; el ancho del punteado y el tamano de la flecha escalan con la distancia a la camara (a 66 m un trazo de 30 cm
    // no se veria). Lo crea el director al tomar la radio (nada en la escena).
    public class GuiaDeMando : MonoBehaviour
    {
        static GuiaDeMando instancia;
        public static GuiaDeMando Instancia => instancia;

        Transform flecha;
        MeshRenderer flechaRend;
        LineRenderer ruta;
        NavMeshPath camino;
        float proximaRuta, t;
        Vector3 ultimoOrigen, ultimoDestino;

        public bool Activa { get; private set; }
        public int PasoGuiado { get; private set; } = -1;
        public bool FlechaVisible => flecha != null && flecha.gameObject.activeSelf;
        public Vector3 PosicionDeLaFlecha => flecha != null ? flecha.position : Vector3.zero;
        public bool RutaActiva => ruta != null && ruta.enabled && ruta.positionCount >= 2;
        public int PuntosDeRuta => ruta != null && ruta.enabled ? ruta.positionCount : 0;
        public Vector3 FinDeLaRuta => RutaActiva ? ruta.GetPosition(ruta.positionCount - 1) : Vector3.zero;
        public Vector3 InicioDeLaRuta => RutaActiva ? ruta.GetPosition(0) : Vector3.zero;
        public float AnchoDeLaRuta => ruta != null ? ruta.widthMultiplier : 0f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reiniciar() { instancia = null; }

        public static GuiaDeMando Asegurar()
        {
            if (!Application.isPlaying) return null;
            if (instancia != null) return instancia;
            var go = new GameObject("GuiaDeMando") { hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild };
            instancia = go.AddComponent<GuiaDeMando>();
            return instancia;
        }

        void Awake()
        {
            camino = new NavMeshPath();
            var f = new GameObject("FlechaDelPaso");
            f.transform.SetParent(transform, false);
            f.AddComponent<MeshFilter>().sharedMesh = GuiaDeObjetivo.MallaFlecha();
            flechaRend = f.AddComponent<MeshRenderer>();
            flechaRend.sharedMaterial = SafeMaterial.CreateLinea(Color.white);
            flechaRend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            flechaRend.receiveShadows = false;
            flecha = f.transform;
            f.SetActive(false);
            var r = new GameObject("RutaDelPaso");
            r.transform.SetParent(transform, false);
            ruta = r.AddComponent<LineRenderer>();
            var mat = SafeMaterial.CreateLinea(new Color(1f, 0.88f, 0.25f, 0.95f));
            mat.mainTexture = GuiaDeObjetivo.TexturaPunteado();
            ruta.sharedMaterial = mat;
            ruta.numCornerVertices = 2;
            ruta.textureMode = LineTextureMode.Tile;
            ruta.alignment = LineAlignment.TransformZ;
            ruta.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            ruta.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ruta.receiveShadows = false;
            ruta.useWorldSpace = true;
            ruta.startColor = ruta.endColor = new Color(1f, 0.88f, 0.25f, 0.95f);
            ruta.enabled = false;
        }

        void OnDestroy() { if (instancia == this) instancia = null; }

        void Update()
        {
            t += Time.deltaTime;
            var d = OperacionDirector.Instancia;
            if (d != null && d.GuiaDelPaso(out var g) && !SesionLog.DialogoAbierto) Mostrar(g);
            else Apagar();
        }

        void Apagar()
        {
            Activa = false; PasoGuiado = -1;
            if (flecha != null && flecha.gameObject.activeSelf) flecha.gameObject.SetActive(false);
            if (ruta != null && ruta.enabled) ruta.enabled = false;
        }

        void Mostrar(OperacionDirector.GuiaDePaso g)
        {
            Activa = true; PasoGuiado = g.paso;
            var cam = CamaraPrincipal.Actual;
            float dCam = cam != null ? Vector3.Distance(cam.transform.position, g.flechaEn) : 20f;

            // Flecha 3D sobre el objetivo del paso (mas grande cuanto mas lejos esta la camara; tope mayor que el de la guia de FPS por la vista tactica).
            if (!flecha.gameObject.activeSelf) flecha.gameObject.SetActive(true);
            float escala = Mathf.Clamp(dCam * 0.035f, 1f, 6f) * (1f + 0.15f * Mathf.Sin(t * 7.5f));
            float bob = 0.5f * Mathf.Sin(t * 3.2f) * escala;
            // La punta queda justo sobre el objetivo: sobre un soldado/vehiculo (ya viene con su altura) un poco de aire; sobre el circulo del sector, mas.
            bool sobreUnidad = g.paso == 0 || g.paso == 2 || (g.paso == 4 && Vector3.Distance(g.flechaEn, g.destino) > 0.5f) || g.paso == 5;
            float alto = sobreUnidad ? 0.3f + 0.25f * escala : 1.5f + 0.55f * escala;
            flecha.position = g.flechaEn + Vector3.up * (alto + bob);
            flecha.rotation = Quaternion.Euler(0f, t * 70f, 0f);
            flecha.localScale = Vector3.one * escala;
            flechaRend.sharedMaterial.color = Color.Lerp(new Color(1f, 0.82f, 0.25f), new Color(1f, 0.55f, 0.15f), 0.5f + 0.5f * Mathf.Sin(t * 7.5f));

            // Punteado del piso del soldado al sector.
            if (!g.hayRuta) { if (ruta.enabled) ruta.enabled = false; return; }
            float ancho = Mathf.Clamp(dCam * 0.014f, 0.32f, 2.2f);
            ruta.widthMultiplier = ancho;
            ruta.textureScale = new Vector2(1f / (ancho * 2.8f), 1f);
            proximaRuta -= Time.deltaTime;
            bool cambio = (g.origen - ultimoOrigen).sqrMagnitude > 1f || (g.destino - ultimoDestino).sqrMagnitude > 0.25f;
            if (proximaRuta > 0f && ruta.enabled && !cambio) return;
            proximaRuta = 0.3f; ultimoOrigen = g.origen; ultimoDestino = g.destino;
            if (!NavMesh.SamplePosition(g.origen, out var h0, 4f, NavMesh.AllAreas) || !NavMesh.SamplePosition(g.destino, out var h1, 10f, NavMesh.AllAreas)
                || !NavMesh.CalculatePath(h0.position, h1.position, NavMesh.AllAreas, camino) || camino.corners.Length < 2)
            {
                ruta.enabled = false;
                return;
            }
            var esq = camino.corners;
            ruta.enabled = true;
            ruta.positionCount = esq.Length;
            for (int i = 0; i < esq.Length; i++) ruta.SetPosition(i, esq[i] + Vector3.up * 0.2f);
        }
    }
}
