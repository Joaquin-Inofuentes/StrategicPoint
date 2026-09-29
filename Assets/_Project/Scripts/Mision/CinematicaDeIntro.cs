using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using SP.Combat;
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
        Image barraArriba, barraAbajo;
        Text subtitulo, cartelFinal, cartelSiguiente;
        PlayerInputDriver driverRef;
        System.Action alTerminarRef;

        // BUG REAL reportado jugando: "la UI esta rota, se ve la de FPS en la
        // cinematica". EsCanvasDePausa() (ver comentario mas abajo) exceptuaba
        // del apagado a CUALQUIER canvas que tuviera un PauseController colgando
        // -- pero en SC_Gameplay el PauseController no vive en un canvas propio,
        // sino como un hijo mas del MISMO Canvas compartido que tiene el HUD
        // entero (crosshair, minimapa, municion, etc.), asi que la excepcion
        // terminaba dejando ENCENDIDO todo el HUD de gameplay durante la
        // cinematica. La solucion no puede ser volver a apagar ese canvas entero
        // (el panel de pausa quedaria invisible otra vez, el bug original que
        // EsCanvasDePausa arreglaba) ni dejarlo prendido (este bug). En cambio,
        // se apaga cada HIJO de ese canvas salvo el propio PauseController, y se
        // recuerda cuales se tocaron para reactivar solo esos al terminar/saltar
        // la cinematica (no todos: algunos ya estaban apagados por su propio
        // estado de juego, p.ej. Hud_RTS si el jugador esta en modo FPS).
        readonly System.Collections.Generic.List<GameObject> ocultadosPorCinematica = new System.Collections.Generic.List<GameObject>();

        void OcultarHudCompartidoConPausa()
        {
            foreach (var cv in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            {
                if (cv == null || cv.gameObject == lienzo || cv.renderMode == RenderMode.WorldSpace) continue;
                if (EsCanvasDeMenuDedicado(cv)) continue;
                var pause = cv.GetComponentInChildren<SP.Presentation.PauseController>(true);
                if (pause == null) { cv.enabled = false; continue; }
                foreach (Transform hijo in cv.transform)
                {
                    if (hijo == pause.transform) continue;
                    if (!hijo.gameObject.activeSelf) continue;
                    hijo.gameObject.SetActive(false);
                    ocultadosPorCinematica.Add(hijo.gameObject);
                }
            }
        }

        // Se llama DESPUES de devolverle el control al driver: RosterView.OnEnable reconstruye las
        // filas leyendo PlayerInputDriver.Activo, y con el driver aun apagado el roster quedaba vacio.
        void RestaurarHudOcultado()
        {
            foreach (var cv in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                if (cv != null && cv.gameObject != lienzo && cv.renderMode != RenderMode.WorldSpace) cv.enabled = true;
            foreach (var go in ocultadosPorCinematica)
                if (go != null) go.SetActive(true);
            ocultadosPorCinematica.Clear();
        }

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
            if (driverRef != null)
            {
                driverRef.enabled = true;
                if (driverRef.Rig != null) driverRef.Rig.enabled = true;
            }
            RestaurarHudOcultado();
            if (lienzo != null) { Destroy(lienzo); lienzo = null; }
            Terminada = true;
            EnCurso = false;
            WaypointActual = -1;
            alTerminarRef?.Invoke();
        }

        // Canvas de menu propio y separado del HUD compartido (p.ej. un
        // Hud_Menu bien cableado en CapasDeHud): a este si conviene dejarlo
        // prendido entero. El caso del PauseController colgando del canvas
        // COMPARTIDO se maneja aparte, en OcultarHudCompartidoConPausa().
        static bool EsCanvasDeMenuDedicado(Canvas cv)
        {
            return SP.UI.CapasDeHud.Instancia != null && SP.UI.CapasDeHud.Instancia.Hud_Menu != null
                && cv.transform.IsChildOf(SP.UI.CapasDeHud.Instancia.Hud_Menu.transform);
        }

        // Pedido explicito: "un sonido de disparo al saltear tomas de
        // escena" -- feedback audible cada vez que se corta la transicion
        // o la espera de una toma con ESPACIO/click. Reusa el mismo clip
        // sintetico del rifle (GenericSfx.GetWeaponShot) en vez de armar un
        // tono nuevo, asi suena a disparo de verdad y no a otro "bip" mas.
        static void SonarSalteoDeToma()
        {
            var clip = GenericSfx.GetWeaponShot(WeaponKind.Rifle);
            AudioDirector.Instance?.PlayFlat(clip, SfxChannel.Ui, 0.7f, 1f);
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

            // Pedido explicito: "en la cinematica aparece un recuadro
            // molesto, eliminalo" -- ese recuadro era FondoSubtitulo, un
            // rectangulo negro semitransparente atras del subtitulo. Se
            // saca el fondo y se deja solo el texto (con su propia Shadow
            // para seguir leyendose contra cualquier escena de fondo).
            var subGO = new GameObject("Subtitulo", typeof(RectTransform), typeof(Text), typeof(Shadow));
            subGO.transform.SetParent(lienzo.transform, false);
            subtitulo = subGO.GetComponent<Text>();
            subtitulo.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            subtitulo.fontSize = 34; subtitulo.fontStyle = FontStyle.Bold; subtitulo.alignment = TextAnchor.MiddleCenter;
            subtitulo.color = new Color(1f, 1f, 1f, 0f); subtitulo.raycastTarget = false;
            var subRt = subtitulo.rectTransform; subRt.anchorMin = new Vector2(0.5f, 0f); subRt.anchorMax = new Vector2(0.5f, 0f);
            subRt.pivot = new Vector2(0.5f, 0f);
            subRt.anchoredPosition = new Vector2(0f, 60f);
            subRt.sizeDelta = new Vector2(1400f, 110f);
            var subSombra = subGO.GetComponent<Shadow>();
            subSombra.effectColor = new Color(0f, 0f, 0f, 0.85f);
            subSombra.effectDistance = new Vector2(2f, -2f);

            var finGO = new GameObject("CartelFinal", typeof(RectTransform), typeof(Text), typeof(Shadow));
            finGO.transform.SetParent(lienzo.transform, false);
            cartelFinal = finGO.GetComponent<Text>();
            cartelFinal.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            cartelFinal.fontSize = 46; cartelFinal.fontStyle = FontStyle.Bold; cartelFinal.alignment = TextAnchor.MiddleCenter;
            cartelFinal.color = new Color(1f, 1f, 1f, 0f); cartelFinal.raycastTarget = false;
            var finRt = cartelFinal.rectTransform; finRt.anchorMin = finRt.anchorMax = new Vector2(0.5f, 0.5f);
            finRt.sizeDelta = new Vector2(1500f, 160f);
            cartelFinal.text = "PRESIONA UNA TECLA O HACE CLICK PARA COMENZAR";

            var sigGO = new GameObject("CartelSiguiente", typeof(RectTransform), typeof(Text), typeof(Shadow));
            sigGO.transform.SetParent(lienzo.transform, false);
            cartelSiguiente = sigGO.GetComponent<Text>();
            cartelSiguiente.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            cartelSiguiente.fontSize = 28; cartelSiguiente.fontStyle = FontStyle.Bold; cartelSiguiente.alignment = TextAnchor.LowerRight;
            cartelSiguiente.color = new Color(1f, 1f, 1f, 0f); cartelSiguiente.raycastTarget = false;
            var sigRt = cartelSiguiente.rectTransform; sigRt.anchorMin = sigRt.anchorMax = new Vector2(1f, 0f);
            sigRt.pivot = new Vector2(1f, 0f);
            sigRt.anchoredPosition = new Vector2(-40f, 40f);
            sigRt.sizeDelta = new Vector2(600f, 60f);
            cartelSiguiente.text = "[ESPACIO / CLIC] SIGUIENTE";
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
            OcultarHudCompartidoConPausa();
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
                bool skipping = false;
                
                cartelSiguiente.color = new Color(1f, 1f, 1f, 0.6f);

                while (t < duracion)
                {
                    bool skipPressed = (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame) || (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame);
                    if (skipPressed)
                    {
                        skipping = true;
                        SonarSalteoDeToma();
                        break;
                    }

                    t += Time.deltaTime;
                    float k = Mathf.Clamp01(t / duracion);
                    float suave = k * k * (3f - 2f * k);   // smoothstep: sin el arranque/frenado brusco de un lerp lineal
                    cam.transform.position = Vector3.Lerp(desdePos, hastaPos, suave);
                    cam.transform.rotation = Quaternion.Slerp(desdeRot, hastaRot, suave);
                    yield return null;
                }

                if (skipping)
                {
                    cartelSiguiente.color = new Color(1f, 1f, 1f, 0f);
                    Vector3 skipDesdePos = cam.transform.position;
                    Quaternion skipDesdeRot = cam.transform.rotation;
                    float skipDur = 0.5f;
                    float skipT = 0f;
                    while (skipT < skipDur)
                    {
                        skipT += Time.deltaTime;
                        float k = Mathf.Clamp01(skipT / skipDur);
                        float suave = k * k * (3f - 2f * k);
                        cam.transform.position = Vector3.Lerp(skipDesdePos, hastaPos, suave);
                        cam.transform.rotation = Quaternion.Slerp(skipDesdeRot, hastaRot, suave);
                        yield return null;
                    }
                }

                cam.transform.position = hastaPos;
                cam.transform.rotation = hastaRot;

                bool conSubtitulo = !string.IsNullOrEmpty(wp.subtitulo);
                if (conSubtitulo && !skipping) yield return MostrarSubtitulo(wp.subtitulo);

                float espera = Mathf.Max(0f, wp.segundosDeEspera - (conSubtitulo ? 0.4f : 0f));
                float te = 0f;
                while (te < espera && !skipping)
                {
                    bool skipPressed = (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame) || (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame);
                    if (skipPressed) { skipping = true; SonarSalteoDeToma(); break; }
                    te += Time.deltaTime;
                    yield return null;
                }

                if (conSubtitulo) subtitulo.color = new Color(1f, 1f, 1f, 0f);
                cartelSiguiente.color = new Color(1f, 1f, 1f, 0f);
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
                bool escPressed = kb != null && kb.escapeKey.wasPressedThisFrame;
                if ((kb != null && kb.anyKey.wasPressedThisFrame && !escPressed) || (ms != null && ms.leftButton.wasPressedThisFrame)) break;
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
            if (driver != null)
            {
                driver.enabled = true;
                if (driver.Rig != null) driver.Rig.enabled = true;
            }
            RestaurarHudOcultado();
            if (lienzo != null) Destroy(lienzo);
            Terminada = true;
            EnCurso = false;
            alTerminar?.Invoke();
        }
    }
}
