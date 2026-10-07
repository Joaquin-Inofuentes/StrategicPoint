using UnityEngine;
using UnityEngine.AI;
using SP.CameraSystem;
using SP.Player;
using SP.Presentation;

namespace SP.Operacion
{
    // Bug #105: guia de "que hago ahora" en las fases 1 a 4 (cuartel, muralla, centro de datos, patio/tanque). Une tres ayudas sobre el
    // mismo objetivo, que es el de la baliza activa del director:
    //   - una flecha 3D grande y pulsante flotando sobre el objetivo (se ve de lejos, escala con la distancia);
    //   - una ruta punteada AUTOMATICA en el piso por el NavMesh, desde el jugador hasta el objetivo (sin tener que mantener ninguna tecla);
    //   - la flecha plana a los pies del jugador (ObjectiveArrowIndicator, la misma de la Mision) apuntando al objetivo.
    // Solo en FPS, a pie y fuera de cinematicas; se apaga al estar encima del objetivo. Lo crea OperacionDirector.Start (nada en la escena).
    public class GuiaDeObjetivo : MonoBehaviour
    {
        public const float DistanciaParaOcultarRuta = 7f, DistanciaParaOcultarFlecha = 3f, AlturaFlecha = 5.5f, IntervaloDeRuta = 0.3f;
        static GuiaDeObjetivo instancia;
        public static GuiaDeObjetivo Instancia => instancia;

        Transform flecha;
        MeshRenderer flechaRend;
        LineRenderer ruta;
        ObjectiveArrowIndicator flechaPies;
        NavMeshPath camino;
        float proximaRuta, t;
        static Texture2D texPunteado;

        public bool Activa { get; private set; }
        public bool RutaActiva => ruta != null && ruta.enabled && ruta.positionCount >= 2;
        public int PuntosDeRuta => ruta != null && ruta.enabled ? ruta.positionCount : 0;
        public bool FlechaVisible => flecha != null && flecha.gameObject.activeSelf;
        public Vector3 PosicionDeLaFlecha => flecha != null ? flecha.position : Vector3.zero;
        public bool FlechaDelPiesVisible => flechaPies != null && flechaPies.transform.childCount > 0 && flechaPies.transform.GetChild(0).gameObject.activeSelf;
        public Vector3 ObjetivoActual { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reiniciar() { instancia = null; texPunteado = null; }

        public static GuiaDeObjetivo Asegurar()
        {
            if (!Application.isPlaying) return null;
            if (instancia != null) return instancia;
            var go = new GameObject("GuiaDeObjetivo") { hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild };
            instancia = go.AddComponent<GuiaDeObjetivo>();
            return instancia;
        }

        void Awake()
        {
            camino = new NavMeshPath();
            // Flecha 3D (apunta hacia abajo, al objetivo).
            var f = new GameObject("FlechaGrande");
            f.transform.SetParent(transform, false);
            f.AddComponent<MeshFilter>().sharedMesh = MallaDeFlecha();
            flechaRend = f.AddComponent<MeshRenderer>();
            flechaRend.sharedMaterial = SafeMaterial.CreateLinea(Color.white);   // unlit, respeta el color de vertices
            flechaRend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            flechaRend.receiveShadows = false;
            flecha = f.transform;
            f.SetActive(false);
            // Ruta punteada.
            var r = new GameObject("RutaPunteada");
            r.transform.SetParent(transform, false);
            ruta = r.AddComponent<LineRenderer>();
            var mat = SafeMaterial.CreateLinea(new Color(1f, 0.88f, 0.25f, 0.95f));
            mat.mainTexture = Punteado();
            ruta.sharedMaterial = mat;
            ruta.widthMultiplier = 0.32f;
            ruta.numCornerVertices = 2;
            ruta.textureMode = LineTextureMode.Tile;
            ruta.alignment = LineAlignment.TransformZ;
            ruta.transform.rotation = Quaternion.Euler(90f, 0f, 0f);   // el ancho queda en el plano del piso (se ve desde arriba y en rasante)
            ruta.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ruta.receiveShadows = false;
            ruta.useWorldSpace = true;
            ruta.startColor = ruta.endColor = new Color(1f, 0.88f, 0.25f, 0.95f);
            ruta.enabled = false;
            flechaPies = ObjectiveArrowIndicator.Crear();
            flechaPies.transform.SetParent(transform, false);
        }

        void OnDestroy() { if (instancia == this) instancia = null; }

        // P7 (#114): la guia del mando tactico reusa la malla de la flecha y la textura del punteado.
        public static Mesh MallaFlecha() => MallaDeFlecha();
        public static Texture2D TexturaPunteado() => Punteado();

        static Texture2D Punteado()
        {
            if (texPunteado != null) return texPunteado;
            var tex = new Texture2D(8, 1, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Point, hideFlags = HideFlags.HideAndDontSave };
            for (int i = 0; i < 8; i++) tex.SetPixel(i, 0, i < 4 ? Color.white : new Color(1f, 1f, 1f, 0f));
            tex.Apply(false, true);
            return texPunteado = tex;
        }

        // Flecha 3D apuntando hacia abajo: cabeza de piramide de 4 caras y mastil cuadrado. Colores de vertice (arriba claro, abajo oscuro).
        static Mesh MallaDeFlecha()
        {
            var m = new Mesh { name = "FlechaGrande" };
            var v = new System.Collections.Generic.List<Vector3>();
            var c = new System.Collections.Generic.List<Color>();
            var tr = new System.Collections.Generic.List<int>();
            Color claro = new Color(1f, 1f, 1f, 1f), medio = new Color(0.82f, 0.82f, 0.82f, 1f), oscuro = new Color(0.62f, 0.62f, 0.62f, 1f);
            void Tri(Vector3 a, Vector3 b, Vector3 d, Color ca, Color cb, Color cd)
            {
                int i = v.Count;
                v.Add(a); v.Add(b); v.Add(d); c.Add(ca); c.Add(cb); c.Add(cd);
                tr.Add(i); tr.Add(i + 1); tr.Add(i + 2);
            }
            float h = 0.75f;
            Vector3 punta = Vector3.zero;
            var b0 = new Vector3(-h, 1f, -h); var b1 = new Vector3(-h, 1f, h); var b2 = new Vector3(h, 1f, h); var b3 = new Vector3(h, 1f, -h);
            Tri(punta, b0, b1, oscuro, medio, medio); Tri(punta, b1, b2, oscuro, medio, medio);
            Tri(punta, b2, b3, oscuro, medio, medio); Tri(punta, b3, b0, oscuro, medio, medio);
            Tri(b0, b2, b1, claro, claro, claro); Tri(b0, b3, b2, claro, claro, claro);
            float w = 0.3f;
            var s0 = new Vector3(-w, 1f, -w); var s1 = new Vector3(-w, 1f, w); var s2 = new Vector3(w, 1f, w); var s3 = new Vector3(w, 1f, -w);
            var t0 = new Vector3(-w, 2.4f, -w); var t1 = new Vector3(-w, 2.4f, w); var t2 = new Vector3(w, 2.4f, w); var t3 = new Vector3(w, 2.4f, -w);
            void Cara(Vector3 a, Vector3 b, Vector3 d, Vector3 e, Color col) { Tri(a, b, d, col, col, col); Tri(a, d, e, col, col, col); }
            Cara(s0, t0, t1, s1, medio); Cara(s1, t1, t2, s2, oscuro); Cara(s2, t2, t3, s3, medio); Cara(s3, t3, t0, s0, oscuro);
            Cara(t0, t3, t2, t1, claro);
            m.SetVertices(v); m.SetColors(c); m.SetTriangles(tr, 0);
            m.RecalculateBounds();
            return m;
        }

        void Update()
        {
            t += Time.deltaTime;
            var d = OperacionDirector.Instancia;
            bool ok = d != null && d.GuiaPermitida(out var objetivo0);
            var objetivo = Vector3.zero;
            if (ok) d.GuiaPermitida(out objetivo);
            var drv = PlayerInputDriver.Activo;
            var yo = drv != null && drv.Brain != null ? drv.Brain.Current : null;
            if (ok && yo != null && yo.Health != null && yo.Health.IsAlive)
            {
                var rig = CameraRig.Instance;
                if (drv.CurrentSeat.HasValue) ok = false;
                if (rig != null && rig.Mode != ControlMode.Fps) ok = false;
                if (CinematicaDeOperacion.IntroActiva) ok = false;
                if (SP.Core.SesionLog.DialogoAbierto) ok = false;
                if (ok) { Mostrar(yo.transform.position, objetivo); return; }
            }
            Apagar();
        }

        void Apagar()
        {
            Activa = false;
            if (flecha != null && flecha.gameObject.activeSelf) flecha.gameObject.SetActive(false);
            if (ruta != null && ruta.enabled) ruta.enabled = false;
            if (flechaPies != null) flechaPies.Ocultar();
        }

        void Mostrar(Vector3 yo, Vector3 objetivo)
        {
            Activa = true;
            ObjetivoActual = objetivo;
            var plano = objetivo - yo; plano.y = 0f;
            float dist = plano.magnitude;

            // Flecha 3D sobre el objetivo.
            bool verFlecha = dist > DistanciaParaOcultarFlecha;
            if (flecha.gameObject.activeSelf != verFlecha) flecha.gameObject.SetActive(verFlecha);
            if (verFlecha)
            {
                var cam = SP.Core.CamaraPrincipal.Actual;
                float dCam = cam != null ? Vector3.Distance(cam.transform.position, objetivo) : dist;
                float escala = Mathf.Clamp(dCam * 0.035f, 1f, 4.5f) * (1f + 0.15f * Mathf.Sin(t * 7.5f));
                float bob = 0.5f * Mathf.Sin(t * 3.2f) * escala;
                flecha.position = new Vector3(objetivo.x, Mathf.Max(objetivo.y, 0f) + AlturaFlecha + bob, objetivo.z);
                flecha.rotation = Quaternion.Euler(0f, t * 70f, 0f);
                flecha.localScale = Vector3.one * escala;
                var col = Color.Lerp(new Color(1f, 0.82f, 0.25f), new Color(1f, 0.55f, 0.15f), 0.5f + 0.5f * Mathf.Sin(t * 7.5f));
                flechaRend.sharedMaterial.color = col;
            }

            // Flecha plana a los pies.
            if (dist > DistanciaParaOcultarFlecha) flechaPies.Actualizar(yo, objetivo); else flechaPies.Ocultar();

            // Ruta punteada por el NavMesh.
            if (dist <= DistanciaParaOcultarRuta) { ruta.enabled = false; return; }
            proximaRuta -= Time.deltaTime;
            if (proximaRuta > 0f && ruta.enabled) return;
            proximaRuta = IntervaloDeRuta;
            if (!NavMesh.SamplePosition(yo, out var h0, 4f, NavMesh.AllAreas) || !NavMesh.SamplePosition(objetivo, out var h1, 10f, NavMesh.AllAreas)
                || !NavMesh.CalculatePath(h0.position, h1.position, NavMesh.AllAreas, camino) || camino.corners.Length < 2)
            {
                ruta.enabled = false;
                return;
            }
            var esq = camino.corners;
            ruta.enabled = true;
            ruta.positionCount = esq.Length;
            for (int i = 0; i < esq.Length; i++) ruta.SetPosition(i, esq[i] + Vector3.up * 0.18f);
            ruta.textureScale = new Vector2(1f / 0.9f, 1f);
        }
    }
}
