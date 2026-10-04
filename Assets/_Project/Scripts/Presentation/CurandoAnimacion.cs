using System.Collections.Generic;
using UnityEngine;
using SP.Actors;

namespace SP.Presentation
{
    // Animacion de CURAR / REANIMAR (pedido del bug #8: "quiero que tenga una animacion de curarme").
    // Los soldados son de bloques sin clip de "curar", asi que se arma por codigo y sin assets:
    //  - el medico se agacha y mira al herido,
    //  - un botiquin blanco con cruz roja aparece frente a sus manos y "bombea" (sube y baja, gira apenas),
    //  - cruces verdes suben desde el herido cada ~0,35 s y un anillo verde late bajo sus pies.
    // Todo se crea al empezar a atender y se destruye al terminar (Terminar). No usa Update propio: lo llama
    // PedidoDeCuracion.Tick, que corre en el mismo paso de simulacion que el juego y la suite.
    public static class CurandoAnimacion
    {
        class Estado
        {
            public GameObject botiquin, anillo;
            public float t, proximaCruz;
            public Soldier herido;
            public readonly List<(Transform tr, float vida)> cruces = new List<(Transform, float)>();
        }

        static readonly Dictionary<Soldier, Estado> activos = new Dictionary<Soldier, Estado>();
        static Material matBlanco, matRojo, matVerde;

        public static bool Animando(Soldier medico) => medico != null && activos.ContainsKey(medico);
        public static int CrucesVivas(Soldier medico) => medico != null && activos.TryGetValue(medico, out var e) ? e.cruces.Count : 0;

        static Material Mat(ref Material m, Color c)
        {
            if (m == null) m = DiamondGizmo.NuevoMaterial(c);
            return m;
        }

        static GameObject Cubo(Transform padre, Vector3 escala, Vector3 posLocal, Material mat, string nombre)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = nombre;
            var col = go.GetComponent<Collider>();
            if (col != null) Object.Destroy(col);
            go.transform.SetParent(padre, false);
            go.transform.localScale = escala;
            go.transform.localPosition = posLocal;
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }

        static Estado Crear(Soldier medico, Soldier herido)
        {
            var e = new Estado { herido = herido };
            // Botiquin: caja blanca con una cruz roja (dos barras) en la cara.
            var raiz = new GameObject("Botiquin_Curando");
            raiz.transform.SetParent(medico.transform, false);
            raiz.transform.localPosition = new Vector3(0f, 0.75f, 0.55f);
            Cubo(raiz.transform, new Vector3(0.34f, 0.24f, 0.18f), Vector3.zero, Mat(ref matBlanco, Color.white), "Caja");
            Cubo(raiz.transform, new Vector3(0.20f, 0.06f, 0.02f), new Vector3(0f, 0f, 0.10f), Mat(ref matRojo, new Color(0.9f, 0.1f, 0.1f)), "CruzH");
            Cubo(raiz.transform, new Vector3(0.06f, 0.20f, 0.02f), new Vector3(0f, 0f, 0.10f), matRojo, "CruzV");
            e.botiquin = raiz;
            // Anillo verde bajo el herido (un cilindro chato).
            var anillo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            anillo.name = "AnilloCuracion";
            var c2 = anillo.GetComponent<Collider>();
            if (c2 != null) Object.Destroy(c2);
            anillo.GetComponent<MeshRenderer>().sharedMaterial = Mat(ref matVerde, new Color(0.25f, 1f, 0.45f, 0.85f));
            anillo.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            e.anillo = anillo;
            return e;
        }

        public static void Tick(Soldier medico, Soldier herido, float dt)
        {
            if (medico == null || herido == null) return;
            if (!activos.TryGetValue(medico, out var e) || e.botiquin == null || e.herido != herido)
            {
                Terminar(medico);
                e = Crear(medico, herido);
                activos[medico] = e;
            }
            e.t += dt;

            // El medico agachado mirando al herido (la IA del medico esta Pasiva mientras atiende, no pelea por la postura).
            if (medico.Motor != null)
            {
                medico.Motor.SetCrouching(true);
                var hacia = herido.transform.position - medico.transform.position; hacia.y = 0f;
                if (hacia.sqrMagnitude > 0.01f)
                    medico.transform.rotation = Quaternion.Slerp(medico.transform.rotation, Quaternion.LookRotation(hacia), Mathf.Clamp01(dt * 8f));
            }

            // Botiquin "bombeando".
            float bombeo = Mathf.Sin(e.t * 9f);
            e.botiquin.transform.localPosition = new Vector3(0f, 0.62f + 0.07f * bombeo, 0.55f);
            e.botiquin.transform.localRotation = Quaternion.Euler(10f * bombeo, 0f, 6f * Mathf.Sin(e.t * 4.5f));

            // Anillo que late bajo el herido.
            float lat = 0.5f + 0.5f * Mathf.Sin(e.t * 6f);
            e.anillo.transform.position = herido.transform.position + Vector3.up * 0.05f - Vector3.up * 0.75f;
            float r = 1.1f + 0.35f * lat;
            e.anillo.transform.localScale = new Vector3(r, 0.02f, r);

            // Cruces verdes que suben.
            e.proximaCruz -= dt;
            if (e.proximaCruz <= 0f)
            {
                e.proximaCruz = 0.35f;
                var cruz = new GameObject("CruzVerde");
                Cubo(cruz.transform, new Vector3(0.28f, 0.08f, 0.08f), Vector3.zero, matVerde, "H");
                Cubo(cruz.transform, new Vector3(0.08f, 0.28f, 0.08f), Vector3.zero, matVerde, "V");
                cruz.transform.position = herido.transform.position + new Vector3(Random.Range(-0.45f, 0.45f), 0.6f, Random.Range(-0.45f, 0.45f));
                e.cruces.Add((cruz.transform, 0f));
            }
            for (int i = e.cruces.Count - 1; i >= 0; i--)
            {
                var (tr, vida) = e.cruces[i];
                vida += dt;
                if (tr == null || vida > 1.1f) { if (tr != null) Object.Destroy(tr.gameObject); e.cruces.RemoveAt(i); continue; }
                tr.position += Vector3.up * dt * 1.4f;
                tr.localScale = Vector3.one * Mathf.Lerp(1f, 0.3f, vida / 1.1f);
                var cam = SP.Core.CamaraPrincipal.Actual;
                if (cam != null) tr.rotation = Quaternion.LookRotation(tr.position - cam.transform.position);
                e.cruces[i] = (tr, vida);
            }
        }

        public static void Terminar(Soldier medico)
        {
            if (medico == null || !activos.TryGetValue(medico, out var e)) return;
            activos.Remove(medico);
            if (e.botiquin != null) Object.Destroy(e.botiquin);
            if (e.anillo != null) Object.Destroy(e.anillo);
            foreach (var (tr, _) in e.cruces) if (tr != null) Object.Destroy(tr.gameObject);
            if (medico.Motor != null) medico.Motor.SetCrouching(false);
        }
    }

    // Cartel central de un renglon con fondo (bug #8: "si intento moverme un cartel que diga: aguarde, esta siendo curado...").
    // Independiente del cartel de interaccion (InteractHintView), que se reescribe cada frame con la pista de la mira.
    public class AvisoCentral : MonoBehaviour
    {
        static AvisoCentral instancia;
        UnityEngine.UI.Text texto;
        UnityEngine.UI.Image fondo;
        float hasta;

        public static string TextoActual => instancia != null && instancia.fondo != null && instancia.fondo.gameObject.activeSelf ? instancia.texto.text : null;

        static AvisoCentral Asegurar()
        {
            if (instancia != null) return instancia;
            var go = new GameObject("AvisoCentral", typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler));
            var c = go.GetComponent<Canvas>(); c.renderMode = RenderMode.ScreenSpaceOverlay; c.sortingOrder = 850;
            var sc = go.GetComponent<UnityEngine.UI.CanvasScaler>(); sc.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize; sc.referenceResolution = new Vector2(1920f, 1080f);
            instancia = go.AddComponent<AvisoCentral>();
            var f = new GameObject("Fondo", typeof(RectTransform), typeof(UnityEngine.UI.Image));
            f.transform.SetParent(go.transform, false);
            var rt = f.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.30f);
            rt.sizeDelta = new Vector2(1180f, 96f);
            instancia.fondo = f.GetComponent<UnityEngine.UI.Image>();
            instancia.fondo.color = new Color(0.05f, 0.25f, 0.12f, 0.88f);
            instancia.fondo.raycastTarget = false;
            var t = new GameObject("Texto", typeof(RectTransform), typeof(UnityEngine.UI.Text));
            t.transform.SetParent(f.transform, false);
            var trt = t.GetComponent<RectTransform>(); trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one; trt.offsetMin = new Vector2(20f, 6f); trt.offsetMax = new Vector2(-20f, -6f);
            instancia.texto = t.GetComponent<UnityEngine.UI.Text>();
            instancia.texto.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            instancia.texto.fontSize = 34; instancia.texto.fontStyle = FontStyle.Bold;
            instancia.texto.alignment = TextAnchor.MiddleCenter; instancia.texto.color = Color.white; instancia.texto.raycastTarget = false;
            // El aviso largo ("...APRIETE [ESPACIO] PARA DETENERLO") se cortaba en una sola linea: ahora parte en dos y achica la letra si hace falta.
            instancia.texto.horizontalOverflow = HorizontalWrapMode.Wrap;
            instancia.texto.verticalOverflow = VerticalWrapMode.Truncate;
            instancia.texto.resizeTextForBestFit = true;
            instancia.texto.resizeTextMinSize = 20; instancia.texto.resizeTextMaxSize = 34;
            instancia.texto.lineSpacing = 0.9f;
            f.SetActive(false);
            return instancia;
        }

        public static void Mostrar(string mensaje, float segundos, Color? colorDeFondo = null)
        {
            var a = Asegurar();
            a.texto.text = mensaje;
            a.fondo.color = colorDeFondo ?? new Color(0.05f, 0.25f, 0.12f, 0.88f);
            a.fondo.gameObject.SetActive(true);
            a.hasta = Time.unscaledTime + segundos;
        }

        public static void Ocultar()
        {
            if (instancia != null && instancia.fondo != null) instancia.fondo.gameObject.SetActive(false);
        }

        void Update()
        {
            if (fondo != null && fondo.gameObject.activeSelf && Time.unscaledTime > hasta) fondo.gameObject.SetActive(false);
        }
    }
}
