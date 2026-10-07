using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using SP.Actors;
using SP.Combat;
using SP.Core;
using SP.Interaction;
using SP.Presentation;

namespace SP.Player
{
    // P7 (#120c): cambio de aliado con [C], en FPS.
    //   - TOQUE (< 0,3 s): posee al aliado vivo mas cercano.
    //   - MANTENER: se resaltan los rombos de TODOS los aliados (incluidos milicianos), visibles a traves de paredes (es una capa de UI sobre la
    //     pantalla, no objetos del mundo); el candidato es el mas centrado en pantalla ("el mas visible", aunque este ocluido) y lleva el rombo
    //     grande y dorado. Al SOLTAR se posee al candidato. Pista: "SOLTA [C] PARA CONTROLAR A X".
    // Usa solo IsPressed (con el editor sin foco los eventos de teclado inyectados llegan como isPressed pero no como wasPressedThisFrame).
    // Lo crea PlayerInputDriver y lo alimenta cada cuadro con Actualizar(apretada, dt); las pruebas llaman a Actualizar con valores simulados.
    public class CambioDeAliado : MonoBehaviour
    {
        public const float SegundosParaMantener = 0.3f;
        public const float MargenDePantalla = 56f, MargenHorizontalDeEtiqueta = 190f;

        public static CambioDeAliado Instancia { get; private set; }
        // Costura de pruebas: simula la tecla apretada.
        public static bool PruebaApretada;

        public bool Resaltando { get; private set; }
        public Soldier Candidato { get; private set; }
        public int RombosVisibles { get; private set; }
        public string Pista { get; private set; } = "";
        public int Cambios { get; private set; }
        public Soldier UltimoElegido { get; private set; }   // a quien se paso en el ultimo cambio (para las pruebas)

        PlayerInputDriver drv;
        bool estabaApretada;
        float segundosApretada;
        Canvas lienzo;
        RectTransform raiz;
        Text pista;
        readonly List<Image> rombos = new List<Image>();
        readonly List<Text> nombres = new List<Text>();
        readonly List<Soldier> candidatos = new List<Soldier>();
        readonly List<Vector2> pantallas = new List<Vector2>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reiniciar() { Instancia = null; PruebaApretada = false; }

        public static CambioDeAliado Asegurar(PlayerInputDriver d)
        {
            if (Instancia != null) { Instancia.drv = d; return Instancia; }
            var go = new GameObject("CambioDeAliado", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler)) { hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild };
            var c = go.AddComponent<CambioDeAliado>();
            c.drv = d;
            c.Armar(go);
            Instancia = c;
            return c;
        }

        void Armar(GameObject go)
        {
            lienzo = go.GetComponent<Canvas>();
            lienzo.renderMode = RenderMode.ScreenSpaceOverlay;
            lienzo.sortingOrder = 58;
            var sc = go.GetComponent<CanvasScaler>();
            sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            sc.referenceResolution = new Vector2(1920f, 1080f);
            sc.matchWidthOrHeight = 0.5f;
            raiz = (RectTransform)go.transform;
            var t = new GameObject("Pista", typeof(RectTransform), typeof(Text));
            t.transform.SetParent(raiz, false);
            pista = t.GetComponent<Text>();
            pista.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            pista.fontSize = 34; pista.fontStyle = FontStyle.Bold; pista.alignment = TextAnchor.MiddleCenter;
            pista.color = new Color(1f, 0.86f, 0.22f, 1f); pista.raycastTarget = false;
            pista.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.AddComponent<Shadow>().effectColor = new Color(0f, 0f, 0f, 0.9f);
            var rt = (RectTransform)t.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.22f); rt.sizeDelta = new Vector2(1200f, 60f);
            t.SetActive(false);
            lienzo.enabled = true;
        }

        void OnDestroy() { if (Instancia == this) Instancia = null; }

        // ---- logica del gesto (publica para las pruebas) ----
        public void Actualizar(bool apretada, float dt)
        {
            apretada |= PruebaApretada;
            if (apretada)
            {
                if (!estabaApretada) segundosApretada = 0f;
                segundosApretada += dt;
                if (segundosApretada >= SegundosParaMantener) Resaltar();
            }
            else if (estabaApretada)
            {
                bool eraMantener = Resaltando;
                var elegido = Candidato;
                Apagar();
                if (eraMantener) Poseer(elegido, false);
                else Poseer(MasCercano(), true);
            }
            estabaApretada = apretada;
        }

        // Para cuando se pierde el contexto (RTS, vehiculo, muerte, pausa): se apaga sin poseer a nadie.
        public void Cancelar()
        {
            if (estabaApretada || Resaltando) { estabaApretada = false; Apagar(); }
        }

        void Poseer(Soldier s, bool toque)
        {
            if (drv == null) return;
            if (s == null) { drv.AvisarRechazo(toque ? "NO HAY ALIADO CERCA" : "NINGUN ALIADO PARA CONTROLAR"); return; }
            UltimoElegido = s;
            if (drv.TryPossess(s)) Cambios++;
        }

        void Reunir()
        {
            candidatos.Clear();
            var yo = drv != null && drv.Brain != null ? drv.Brain.Current : null;
            var todos = ActorRegistry.All;
            for (int i = 0; i < todos.Count; i++)
            {
                var a = todos[i];
                if (a == null || a == yo || a.Team != TeamId.Player || a.Role == RoleType.Civilian || a.Health == null || !a.Health.IsAlive || !a.gameObject.activeInHierarchy) continue;
                candidatos.Add(a);
            }
        }

        public Soldier MasCercano()
        {
            Reunir();
            var yo = drv != null && drv.Brain != null ? drv.Brain.Current : null;
            if (yo == null) return null;
            Soldier mejor = null; float dm = float.MaxValue;
            foreach (var a in candidatos)
            {
                float d = (a.transform.position - yo.transform.position).sqrMagnitude;
                if (d < dm) { dm = d; mejor = a; }
            }
            return mejor;
        }

        // El mas centrado en pantalla (distancia al centro del viewport), aunque haya una pared en medio. Los que quedan detras de la camara
        // cuentan solo si no hay ninguno delante.
        public Soldier MasCentrado(Camera cam)
        {
            Reunir();
            if (cam == null || candidatos.Count == 0) return MasCercano();
            Soldier mejor = null; float mejorD = float.MaxValue;
            foreach (var a in candidatos)
            {
                var vp = cam.WorldToViewportPoint(a.transform.position + Vector3.up * 1.4f);
                if (vp.z <= 0f) continue;
                float d = Vector2.Distance(new Vector2(vp.x, vp.y), new Vector2(0.5f, 0.5f));
                if (d < mejorD) { mejorD = d; mejor = a; }
            }
            return mejor != null ? mejor : MasCercano();
        }

        void Resaltar()
        {
            var cam = CamaraPrincipal.Actual != null ? CamaraPrincipal.Actual : (drv != null && drv.Rig != null ? drv.Rig.Cam : null);
            Candidato = MasCentrado(cam);
            Resaltando = true;
            Pintar(cam);
        }

        void Apagar()
        {
            Resaltando = false; Candidato = null; segundosApretada = 0f; RombosVisibles = 0; Pista = "";
            if (pista != null && pista.gameObject.activeSelf) pista.gameObject.SetActive(false);
            for (int i = 0; i < rombos.Count; i++) { if (rombos[i] != null) rombos[i].gameObject.SetActive(false); if (nombres[i] != null) nombres[i].gameObject.SetActive(false); }
        }

        void Pintar(Camera cam)
        {
            if (raiz == null) return;
            float w = raiz.rect.width, h = raiz.rect.height;
            int n = 0;
            for (int i = 0; i < candidatos.Count; i++)
            {
                var a = candidatos[i];
                while (rombos.Count <= n) NuevoRombo();
                Vector2 pos;
                bool ok = Proyectar(cam, a.transform.position + Vector3.up * 2.3f, w, h, out pos);
                var im = rombos[n]; var tx = nombres[n];
                if (!ok) { im.gameObject.SetActive(false); tx.gameObject.SetActive(false); continue; }
                bool elegido = a == Candidato;
                im.gameObject.SetActive(true); tx.gameObject.SetActive(true);
                var rt = (RectTransform)im.transform;
                rt.anchoredPosition = pos;
                float lado = elegido ? 34f : 20f;
                rt.sizeDelta = new Vector2(lado, lado);
                im.color = elegido ? new Color(1f, 0.86f, 0.15f, 1f) : new Color(0.55f, 0.9f, 1f, 0.9f);
                var rtt = (RectTransform)tx.transform;
                rtt.anchoredPosition = pos + new Vector2(0f, elegido ? -34f : -26f);
                tx.text = NombreCorto(a) + (MandoTactico_EsMiliciano(a) && !NombreCorto(a).StartsWith("MILICIANO") ? " · MILICIA" : "");
                tx.fontSize = elegido ? 24 : 18;
                tx.color = elegido ? new Color(1f, 0.86f, 0.15f, 1f) : new Color(0.85f, 0.95f, 1f, 0.95f);
                n++;
            }
            for (int i = n; i < rombos.Count; i++) { rombos[i].gameObject.SetActive(false); nombres[i].gameObject.SetActive(false); }
            RombosVisibles = n;
            Pista = Candidato != null ? $"SOLTÁ [{KeyBindings.DisplayName(KeyBindings.CambiarAliado)}] PARA CONTROLAR A {NombreCorto(Candidato)}" : "NINGÚN ALIADO PARA CONTROLAR";
            pista.text = Pista;
            if (!pista.gameObject.activeSelf) pista.gameObject.SetActive(true);
        }

        static bool MandoTactico_EsMiliciano(Soldier s) => SP.Operacion.MandoTactico.EsMiliciano(s);
        static string NombreCorto(Soldier s) => (RolRequerido.NombreCorto(s) ?? s.DisplayName ?? "?").ToUpperInvariant();

        // Pantalla (coordenadas del lienzo, origen al centro) del punto; los que estan fuera de pantalla se pegan al borde. false = detras de la camara y sin sentido.
        bool Proyectar(Camera cam, Vector3 mundo, float w, float h, out Vector2 pos)
        {
            pos = Vector2.zero;
            if (cam == null) return false;
            var vp = cam.WorldToViewportPoint(mundo);
            var p = new Vector2((vp.x - 0.5f) * w, (vp.y - 0.5f) * h);
            if (vp.z < 0f) { p = -p; if (p.sqrMagnitude < 1f) p = Vector2.down * h * 0.5f; p = p.normalized * Mathf.Max(w, h); }
            // Abajo queda la fila del roster y arriba el panel del objetivo: los rombos del borde frenan antes.
            float mx = w * 0.5f - MargenHorizontalDeEtiqueta, yMax = h * 0.5f - 90f, yMin = -(h * 0.5f - 190f);
            p.x = Mathf.Clamp(p.x, -mx, mx); p.y = Mathf.Clamp(p.y, yMin, yMax);
            pos = p;
            return true;
        }

        void NuevoRombo()
        {
            var g = new GameObject("Rombo", typeof(RectTransform), typeof(Image));
            g.transform.SetParent(raiz, false);
            var im = g.GetComponent<Image>(); im.raycastTarget = false;
            var rt = (RectTransform)g.transform; rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f); rt.localRotation = Quaternion.Euler(0f, 0f, 45f);
            var o = new GameObject("Borde", typeof(RectTransform), typeof(Image));
            o.transform.SetParent(g.transform, false);
            var ob = o.GetComponent<Image>(); ob.color = new Color(0f, 0f, 0f, 0.55f); ob.raycastTarget = false;
            var ort = (RectTransform)o.transform; ort.anchorMin = Vector2.zero; ort.anchorMax = Vector2.one; ort.offsetMin = new Vector2(-3f, -3f); ort.offsetMax = new Vector2(3f, 3f);
            o.transform.SetAsFirstSibling();
            var t = new GameObject("Nombre", typeof(RectTransform), typeof(Text));
            t.transform.SetParent(raiz, false);
            var tx = t.GetComponent<Text>();
            tx.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            tx.fontStyle = FontStyle.Bold; tx.alignment = TextAnchor.MiddleCenter; tx.raycastTarget = false;
            tx.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.AddComponent<Shadow>().effectColor = new Color(0f, 0f, 0f, 0.9f);
            var trt = (RectTransform)t.transform; trt.anchorMin = trt.anchorMax = new Vector2(0.5f, 0.5f); trt.sizeDelta = new Vector2(340f, 30f);
            rombos.Add(im); nombres.Add(tx);
        }

        void LateUpdate()
        {
            // Mientras se mantiene, los rombos siguen a los aliados (se mueven y la camara gira).
            if (Resaltando)
            {
                var cam = CamaraPrincipal.Actual != null ? CamaraPrincipal.Actual : (drv != null && drv.Rig != null ? drv.Rig.Cam : null);
                Candidato = MasCentrado(cam);
                Pintar(cam);
            }
        }
    }
}
