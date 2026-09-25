using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using SP.Core;
using SP.Player;
using SP.Presentation;
using SP.UI;

namespace SP.Mision
{
    // Cinematica de APERTURA: pedido explicito ("una simple cinematica donde muestra primero donde
    // apareces, luego a donde debes ir, una primera toma del rehen, y un movimiento de camara hacia
    // donde debe ir el helicoptero... y al terminar pida un click o apretar una tecla para
    // arrancar"). Recorre CinematicaIntroPath.Waypoints en orden (cada uno con su propio tiempo de
    // viaje/espera y su subtitulo opcional, ver CinematicaWaypoint), con las mismas barras de cine
    // que CinematicaDeVictoria, y termina esperando una tecla o un click antes de devolver el
    // control al jugador.
    public class CinematicaDeIntro : MonoBehaviour
    {
        public bool EnCurso { get; private set; }
        public bool Terminada { get; private set; }
        public int WaypointActual { get; private set; } = -1;

        GameObject lienzo;
        Image barraArriba, barraAbajo, fondoSubtitulo;
        Text subtitulo, cartelFinal;
        PlayerInputDriver driverRef;
        System.Action alTerminarRef;

        public void Iniciar(PlayerInputDriver driver, CinematicaIntroPath ruta, System.Action alTerminar)
        {
            if (EnCurso) return;
            driverRef = driver;
            alTerminarRef = alTerminar;
            StartCoroutine(Rutina(driver, ruta, alTerminar));
        }

        // Para las pruebas (y para no dejar jamas una sesion automatizada colgada esperando una
        // tecla que nadie va a apretar): corta la cinematica YA y devuelve el control, sin pasar
        // por el resto de los waypoints ni por la espera final.
        public void Saltar()
        {
            if (!EnCurso) return;
            StopAllCoroutines();
            SP.Ai.AiBrain.IAPausada = false;
            RomboVisibilidad.Suprimidos = false;
            foreach (var cv in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                if (cv != null && (lienzo == null || cv.gameObject != lienzo) && cv.renderMode != RenderMode.WorldSpace) cv.enabled = true;
            if (driverRef != null)
            {
                driverRef.enabled = true;
                if (driverRef.Rig != null) driverRef.Rig.enabled = true;
            }
            if (lienzo != null) { Destroy(lienzo); lienzo = null; }
            Terminada = true;
            EnCurso = false;
            WaypointActual = -1;
            alTerminarRef?.Invoke();
        }

        void ArmarLienzo()
        {
            lienzo = new GameObject("CinematicaIntroLienzo", typeof(Canvas), typeof(CanvasScaler));
            var c = lienzo.GetComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            c.sortingOrder = 900;
            var sc = lienzo.GetComponent<CanvasScaler>();
            sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            sc.referenceResolution = new Vector2(1920f, 1080f);
            sc.matchWidthOrHeight = 0.5f;

            barraArriba = Rect(lienzo.transform, "BarraArriba", new Vector2(0f, 1f), new Vector2(1f, 1f), Color.black);
            barraAbajo = Rect(lienzo.transform, "BarraAbajo", new Vector2(0f, 0f), new Vector2(1f, 0f), Color.black);
            barraArriba.rectTransform.pivot = new Vector2(0.5f, 1f); barraAbajo.rectTransform.pivot = new Vector2(0.5f, 0f);
            barraArriba.rectTransform.sizeDelta = new Vector2(0f, 0f); barraAbajo.rectTransform.sizeDelta = new Vector2(0f, 0f);

            fondoSubtitulo = Rect(lienzo.transform, "FondoSubtitulo", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Color(0f, 0f, 0f, 0f));
            fondoSubtitulo.rectTransform.pivot = new Vector2(0.5f, 0f);
            fondoSubtitulo.rectTransform.anchoredPosition = new Vector2(0f, 60f);
            fondoSubtitulo.rectTransform.sizeDelta = new Vector2(1400f, 110f);

            var subGO = new GameObject("Subtitulo", typeof(RectTransform), typeof(Text), typeof(Shadow));
            subGO.transform.SetParent(fondoSubtitulo.transform, false);
            subtitulo = subGO.GetComponent<Text>();
            subtitulo.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            subtitulo.fontSize = 34; subtitulo.fontStyle = FontStyle.Bold; subtitulo.alignment = TextAnchor.MiddleCenter;
            subtitulo.color = new Color(1f, 1f, 1f, 0f); subtitulo.raycastTarget = false;
            var subRt = subtitulo.rectTransform; subRt.anchorMin = Vector2.zero; subRt.anchorMax = Vector2.one;
            subRt.offsetMin = subRt.offsetMax = Vector2.zero;

            var finGO = new GameObject("CartelFinal", typeof(RectTransform), typeof(Text), typeof(Shadow));
            finGO.transform.SetParent(lienzo.transform, false);
            cartelFinal = finGO.GetComponent<Text>();
            cartelFinal.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            cartelFinal.fontSize = 46; cartelFinal.fontStyle = FontStyle.Bold; cartelFinal.alignment = TextAnchor.MiddleCenter;
            cartelFinal.color = new Color(1f, 1f, 1f, 0f); cartelFinal.raycastTarget = false;
            var finRt = cartelFinal.rectTransform; finRt.anchorMin = finRt.anchorMax = new Vector2(0.5f, 0.5f);
            finRt.sizeDelta = new Vector2(1500f, 160f);
            cartelFinal.text = "PRESIONA UNA TECLA O HACE CLICK PARA COMENZAR";
        }

        static Image Rect(Transform padre, string nombre, Vector2 aMin, Vector2 aMax, Color color)
        {
            var g = new GameObject(nombre, typeof(RectTransform), typeof(Image));
            g.transform.SetParent(padre, false);
            var img = g.GetComponent<Image>(); img.color = color; img.raycastTarget = false;
            var r = img.rectTransform; r.anchorMin = aMin; r.anchorMax = aMax; r.offsetMin = r.offsetMax = Vector2.zero;
            return img;
        }

        IEnumerator Rutina(PlayerInputDriver driver, CinematicaIntroPath ruta, System.Action alTerminar)
        {
            EnCurso = true;
            SP.Ai.AiBrain.IAPausada = true;
            RomboVisibilidad.Suprimidos = true;
            ArmarLienzo();

            if (driver != null)
            {
                driver.enabled = false;
                if (driver.Rig != null) driver.Rig.enabled = false;
            }
            foreach (var cv in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                if (cv != null && cv.gameObject != lienzo && cv.renderMode != RenderMode.WorldSpace) cv.enabled = false;
            AlertQueue.Clear();

            var cam = SP.Core.CamaraPrincipal.Actual != null ? SP.Core.CamaraPrincipal.Actual : (driver != null && driver.Rig != null ? driver.Rig.Cam : null);

            // Barras de cine adentro (0.5 s).
            float tFade = 0f;
            while (tFade < 0.5f)
            {
                tFade += Time.deltaTime;
                float b = Mathf.Clamp01(tFade / 0.5f) * 110f;
                barraArriba.rectTransform.sizeDelta = new Vector2(0f, b);
                barraAbajo.rectTransform.sizeDelta = new Vector2(0f, b);
                yield return null;
            }

            if (cam == null || ruta == null || ruta.Waypoints == null || ruta.Waypoints.Length == 0)
            {
                yield return EsperarTeclaYCerrar(driver, alTerminar);
                yield break;
            }

            for (int i = 0; i < ruta.Waypoints.Length; i++)
            {
                var wp = ruta.Waypoints[i];
                if (wp == null) continue;
                WaypointActual = i;

                Vector3 desdePos = cam.transform.position;
                Quaternion desdeRot = cam.transform.rotation;
                Vector3 hastaPos = wp.transform.position;
                Vector3 dirMira = (wp.mirarPunto - hastaPos);
                Quaternion hastaRot = dirMira.sqrMagnitude > 0.01f ? Quaternion.LookRotation(dirMira.normalized) : wp.transform.rotation;

                float duracion = Mathf.Max(0.05f, wp.segundosParaLlegar);
                float t = 0f;
                while (t < duracion)
                {
                    t += Time.deltaTime;
                    float k = Mathf.Clamp01(t / duracion);
                    float suave = k * k * (3f - 2f * k);   // smoothstep: sin el arranque/frenado brusco de un lerp lineal
                    cam.transform.position = Vector3.Lerp(desdePos, hastaPos, suave);
                    cam.transform.rotation = Quaternion.Slerp(desdeRot, hastaRot, suave);
                    yield return null;
                }
                cam.transform.position = hastaPos;
                cam.transform.rotation = hastaRot;

                bool conSubtitulo = !string.IsNullOrEmpty(wp.subtitulo);
                if (conSubtitulo) yield return MostrarSubtitulo(wp.subtitulo);

                float espera = Mathf.Max(0f, wp.segundosDeEspera - (conSubtitulo ? 0.4f : 0f));
                float te = 0f;
                while (te < espera) { te += Time.deltaTime; yield return null; }

                if (conSubtitulo) yield return OcultarSubtitulo();
            }

            yield return EsperarTeclaYCerrar(driver, alTerminar);
        }

        IEnumerator MostrarSubtitulo(string texto)
        {
            subtitulo.text = texto;
            float t = 0f;
            while (t < 0.4f)
            {
                t += Time.deltaTime;
                float a = Mathf.Clamp01(t / 0.4f);
                subtitulo.color = new Color(1f, 1f, 1f, a);
                fondoSubtitulo.color = new Color(0f, 0f, 0f, a * 0.55f);
                yield return null;
            }
        }

        IEnumerator OcultarSubtitulo()
        {
            float t = 0f;
            while (t < 0.4f)
            {
                t += Time.deltaTime;
                float a = 1f - Mathf.Clamp01(t / 0.4f);
                subtitulo.color = new Color(1f, 1f, 1f, a);
                fondoSubtitulo.color = new Color(0f, 0f, 0f, a * 0.55f);
                yield return null;
            }
        }

        // Pedido explicito: "al terminar pida un click o apretar una tecla para arrancar". El
        // cartel final parpadea hasta que se detecte cualquier tecla o el boton izquierdo del mouse.
        IEnumerator EsperarTeclaYCerrar(PlayerInputDriver driver, System.Action alTerminar)
        {
            WaypointActual = -1;
            float tIn = 0f;
            while (tIn < 0.4f) { tIn += Time.deltaTime; cartelFinal.color = new Color(1f, 1f, 1f, Mathf.Clamp01(tIn / 0.4f)); yield return null; }

            while (true)
            {
                var kb = Keyboard.current;
                var ms = Mouse.current;
                if ((kb != null && kb.anyKey.wasPressedThisFrame) || (ms != null && ms.leftButton.wasPressedThisFrame)) break;
                float parpadeo = 0.65f + 0.35f * Mathf.Sin(Time.time * 4f);
                cartelFinal.color = new Color(1f, 1f, 1f, parpadeo);
                yield return null;
            }

            float tOut = 0f;
            while (tOut < 0.3f)
            {
                tOut += Time.deltaTime;
                float b = (1f - Mathf.Clamp01(tOut / 0.3f)) * 110f;
                barraArriba.rectTransform.sizeDelta = new Vector2(0f, b);
                barraAbajo.rectTransform.sizeDelta = new Vector2(0f, b);
                cartelFinal.color = new Color(1f, 1f, 1f, 1f - Mathf.Clamp01(tOut / 0.3f));
                yield return null;
            }

            SP.Ai.AiBrain.IAPausada = false;
            RomboVisibilidad.Suprimidos = false;
            foreach (var cv in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                if (cv != null && cv.gameObject != lienzo && cv.renderMode != RenderMode.WorldSpace) cv.enabled = true;
            if (driver != null)
            {
                driver.enabled = true;
                if (driver.Rig != null) driver.Rig.enabled = true;
            }
            if (lienzo != null) Destroy(lienzo);
            Terminada = true;
            EnCurso = false;
            alTerminar?.Invoke();
        }
    }
}
