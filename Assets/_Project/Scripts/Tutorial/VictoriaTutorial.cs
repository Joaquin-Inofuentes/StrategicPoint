using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
using SP.Presentation;

namespace SP.Tutorial
{
    // Efecto de victoria y cierre del tutorial (paso 17):
    //   - fanfarria + destello dorado + cartel grande
    //   - fuegos artificiales de cubitos de colores sobre la meta
    //   - confeti de la interfaz cayendo
    //   - panel final con el resumen (tiempo, pasos, bajas) y dos botones:
    //     REPETIR TUTORIAL y VOLVER AL MENU
    public class VictoriaTutorial : MonoBehaviour
    {
        struct Cubito { public Transform T; public Vector3 V; public float Vida, Max, Escala; public MeshRenderer R; }
        class Confeti { public RectTransform Rt; public float Vel, Giro, Fase; }

        TutorialManager manager;
        Vector3 centro;
        float t;
        bool panelListo, finalizado;
        readonly List<Cubito> cubitos = new List<Cubito>();
        readonly List<Confeti> confeti = new List<Confeti>();
        readonly List<Material> materiales = new List<Material>();
        readonly float[] rafagas = { 0.0f, 0.5f, 1.0f, 1.5f, 2.1f, 2.8f, 3.6f };
        int proxRafaga;
        RectTransform panel;
        Font fuente;
        float confetiHasta = 7f;

        public bool PanelVisible => panel != null && panel.gameObject.activeSelf;
        public int CubitosVivos => cubitos.Count;
        public int ConfetiActivo => confeti.Count;
        public Button BotonRepetir { get; private set; }
        public Button BotonMenu { get; private set; }
        public static VictoriaTutorial Actual { get; private set; }

        public static VictoriaTutorial Iniciar(TutorialManager m, Vector3 centro)
        {
            var go = new GameObject("VictoriaTutorial");
            var v = go.AddComponent<VictoriaTutorial>();
            v.manager = m;
            v.centro = centro;
            v.Comenzar();
            Actual = v;
            return v;
        }

        void Comenzar()
        {
            fuente = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            TutorialLog.Escribir("[VICTORIA]", "EFECTO DE VICTORIA: fanfarria + fuegos artificiales + confeti + panel final");
            AudioDirector.PlayUi2D(SfxKind.TutVictory, 0.9f, 0.3f);
            manager.Ui.DestelloDePaso("¡TUTORIAL COMPLETADO!", new Color(1f, 0.86f, 0.30f));
            manager.Ui.Mensaje("¡TUTORIAL COMPLETADO!", new Color(1f, 0.86f, 0.30f));
            foreach (var c in new[] { new Color(1f, 0.25f, 0.25f), new Color(1f, 0.85f, 0.2f), new Color(0.3f, 0.95f, 0.4f), new Color(0.3f, 0.75f, 1f), new Color(0.9f, 0.4f, 1f), Color.white })
            {
                var m = SafeMaterial.Create(c);
                m.color = c;
                if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
                if (m.HasProperty("_EmissionColor")) { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", c * 1.5f); }
                materiales.Add(m);
            }
            // Confeti de interfaz.
            var cont = manager.Ui.transform;
            for (int i = 0; i < 110; i++)
            {
                var go = new GameObject("Confeti", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
                go.transform.SetParent(cont, false);
                var img = go.GetComponent<RawImage>();
                img.raycastTarget = false;
                img.texture = Texture2D.whiteTexture;
                img.color = Color.HSVToRGB(Random.value, 0.75f, 1f);
                var rt = (RectTransform)go.transform;
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
                float w = Random.Range(7f, 16f);
                rt.sizeDelta = new Vector2(w, w * Random.Range(0.5f, 1.4f));
                rt.anchoredPosition = new Vector2(Random.Range(-700f, 700f), Random.Range(20f, 500f));
                rt.localRotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
                confeti.Add(new Confeti { Rt = rt, Vel = Random.Range(140f, 340f), Giro = Random.Range(-260f, 260f), Fase = Random.value * 6f });
            }
        }

        void Rafaga()
        {
            var p = centro + new Vector3(Random.Range(-9f, 9f), Random.Range(6f, 12f), Random.Range(-4f, 6f));
            AudioDirector.PlayAt(SfxKind.CannonCrack, p, 0.5f, 0.6f);
            int n = 46;
            for (int i = 0; i < n; i++)
            {
                var c = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Destroy(c.GetComponent<Collider>());
                c.transform.position = p;
                float s = Random.Range(0.25f, 0.6f);
                c.transform.localScale = Vector3.one * s;
                var r = c.GetComponent<MeshRenderer>();
                r.sharedMaterial = materiales[Random.Range(0, materiales.Count)];
                r.shadowCastingMode = ShadowCastingMode.Off;
                var dir = Random.onUnitSphere;
                cubitos.Add(new Cubito { T = c.transform, V = dir * Random.Range(4f, 11f), Vida = 0f, Max = Random.Range(1.4f, 2.4f), Escala = s, R = r });
            }
        }

        void MostrarPanel()
        {
            panelListo = true;
            manager.Ui.OcultarPanel();
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            if (manager != null && manager.Ui != null)
            {
                var drv = SP.Player.PlayerInputDriver.Activo;
                if (drv != null) drv.enabled = false;
            }

            var go = new GameObject("PanelVictoria", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(manager.Ui.transform, false);
            panel = (RectTransform)go.transform;
            panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0.5f);
            panel.sizeDelta = new Vector2(560f, 330f);
            panel.anchoredPosition = new Vector2(0f, -40f);
            go.GetComponent<Image>().color = new Color(0.05f, 0.07f, 0.11f, 0.94f);

            Texto(panel, "Titulo", "¡TUTORIAL COMPLETADO!", 36, FontStyle.Bold, new Color(1f, 0.86f, 0.30f), new Vector2(0f, 122f), new Vector2(520f, 50f));
            int m = Mathf.FloorToInt(manager.TiempoTotal / 60f), s = Mathf.FloorToInt(manager.TiempoTotal % 60f);
            Texto(panel, "Resumen", $"Pasos completados: {manager.Total} / {manager.Total}\nTiempo total: {m:00}:{s:00}\nEnemigos derribados: {manager.Bajas}",
                  20, FontStyle.Normal, Color.white, new Vector2(0f, 42f), new Vector2(520f, 100f));
            Texto(panel, "Consejo", "Ya sabes moverte, disparar, dar órdenes y manejar el tanque.", 15, FontStyle.Italic, new Color(0.7f, 0.78f, 0.9f), new Vector2(0f, -22f), new Vector2(520f, 30f));
            BotonRepetir = Boton(panel, "REPETIR TUTORIAL", new Vector2(-138f, -100f), new Color(0.20f, 0.55f, 0.85f), manager.Reintentar);
            BotonMenu = Boton(panel, "VOLVER AL MENÚ", new Vector2(138f, -100f), new Color(0.25f, 0.65f, 0.40f), manager.VolverAlMenu);
            TutorialLog.Escribir("[VICTORIA]", "Panel final visible: REPETIR TUTORIAL / VOLVER AL MENU");
        }

        Text Texto(Transform padre, string n, string txt, int tam, FontStyle est, Color col, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(n, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            go.transform.SetParent(padre, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos; rt.sizeDelta = size;
            var t = go.GetComponent<Text>();
            t.font = fuente; t.text = txt; t.fontSize = tam; t.fontStyle = est; t.color = col;
            t.alignment = TextAnchor.MiddleCenter; t.raycastTarget = false;
            return t;
        }

        Button Boton(Transform padre, string txt, Vector2 pos, Color col, UnityEngine.Events.UnityAction accion)
        {
            var go = new GameObject("Boton_" + txt, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            go.transform.SetParent(padre, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos; rt.sizeDelta = new Vector2(240f, 56f);
            go.GetComponent<Image>().color = col;
            var b = go.GetComponent<Button>();
            b.targetGraphic = go.GetComponent<Image>();
            b.onClick.AddListener(accion);
            SP.UI.ButtonSfx.Attach(b);
            Texto(go.transform, "Texto", txt, 20, FontStyle.Bold, Color.white, Vector2.zero, new Vector2(230f, 50f));
            return b;
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            t += dt;
            while (proxRafaga < rafagas.Length && t >= rafagas[proxRafaga]) { Rafaga(); proxRafaga++; }

            for (int i = cubitos.Count - 1; i >= 0; i--)
            {
                var c = cubitos[i];
                if (c.T == null) { cubitos.RemoveAt(i); continue; }
                c.Vida += dt;
                c.V += Vector3.down * 9f * dt;
                c.T.position += c.V * dt;
                c.T.Rotate(c.V * 40f * dt, Space.Self);
                float k = 1f - Mathf.Clamp01((c.Vida - c.Max * 0.6f) / (c.Max * 0.4f));
                c.T.localScale = Vector3.one * (c.Escala * Mathf.Max(0.05f, k));
                if (c.Vida >= c.Max) { Destroy(c.T.gameObject); cubitos.RemoveAt(i); continue; }
                cubitos[i] = c;
            }

            foreach (var c in confeti)
            {
                if (c.Rt == null) continue;
                var p = c.Rt.anchoredPosition;
                p.y -= c.Vel * dt;
                p.x += Mathf.Sin(t * 2f + c.Fase) * 60f * dt;
                if (p.y < -820f) p.y = t < confetiHasta ? Random.Range(20f, 200f) : -820f;
                c.Rt.anchoredPosition = p;
                c.Rt.Rotate(0f, 0f, c.Giro * dt);
            }

            if (!panelListo && t >= 1.6f) MostrarPanel();
            if (panelListo && !finalizado && t >= 1.8f) { finalizado = true; manager.Finalizar(); }
            if (panel != null)
            {
                float e = Mathf.SmoothStep(0.85f, 1f, Mathf.Clamp01((t - 1.6f) * 3f));
                panel.localScale = new Vector3(e, e, 1f);
            }
        }

        void OnDestroy()
        {
            foreach (var c in cubitos) if (c.T != null) Destroy(c.T.gameObject);
            foreach (var c in confeti) if (c.Rt != null) Destroy(c.Rt.gameObject);
            if (Actual == this) Actual = null;
        }
    }
}
