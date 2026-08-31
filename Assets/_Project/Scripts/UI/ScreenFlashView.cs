using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace SP.UI
{
    // Destello a pantalla completa, reutilizable para dos cosas distintas
    // que necesitaban lo mismo (items 181 y 184 del backlog):
    //
    // 181) El fogonazo de una explosion muy cercana. Una granada que
    //      reventaba a un metro se veia EXACTAMENTE igual que una a
    //      treinta: la unica pista era la barra de vida bajando. Ahora la
    //      pantalla se blanquea un instante segun lo cerca que cayo.
    //
    // 184) El "desenfoque en la transicion de modo". El motor no da un
    //      blur real sin encender post-procesado (y prenderlo por un
    //      efecto de 0.18 s no compensa el costo en gama baja), pero el
    //      backlog admite "flash o desenfoque": se resuelve con un flash
    //      gris neutro, muy corto y sutil, que tapa el salto duro de
    //      camara al cambiar FPS <-> RTS.
    public class ScreenFlashView : MonoBehaviour
    {
        // Acceso directo: lo llaman Projectile.Explode (una vez por
        // explosion, en pleno pico de trabajo del frame) y
        // PlayerInputDriver. Un FindAnyObjectByType ahi seria un barrido
        // de escena en el peor momento posible, igual que en CameraRig.
        public static ScreenFlashView Instance { get; private set; }

        // El Image vive en un GameObject HIJO, no en este mismo. Es una
        // regla dura del proyecto: si se desactiva una Image que comparte
        // GameObject con la vista, se desactiva TAMBIEN el componente, y
        // entonces no queda nadie vivo para volver a prenderlo -- la vista
        // muere para siempre en esa sesion. Con el Image en un hijo, se
        // puede jugar con su alfa (o incluso apagar el hijo) sin tocar a
        // este MonoBehaviour.
        //
        // El campo va privado y SIN serializar a proposito: una referencia
        // asignada al construir la escena no sobrevive el domain reload al
        // entrar a Play mode (el mismo bug que documenta CameraFxSettings),
        // asi que la red de seguridad real es la busqueda por nombre en
        // OnEnable, no el inspector.
        Image flash;

        // Expuesto para verificarlo por reflexion o desde consola: sin
        // esto no habia forma de comprobar que el destello vuelve a cero
        // sin mirar la pantalla frame a frame.
        public float CurrentAlpha => flash != null ? flash.color.a : 0f;

        public void Bind(Image image)
        {
            flash = image;
            if (flash == null) return;
            // Arranca invisible: si quedara con el alfa del prefab, el
            // primer frame de la partida se veia un fogonazo que nadie
            // disparo.
            SetAlpha(0f);
            // Ocupa la pantalla entera; si capturara raycasts se comeria
            // todos los clicks del HUD que tiene debajo.
            flash.raycastTarget = false;
        }

        void OnEnable()
        {
            Instance = this;
            if (flash == null)
            {
                var t = transform.Find("Flash");
                if (t != null) flash = t.GetComponent<Image>();
            }
            if (flash != null)
            {
                flash.raycastTarget = false;
                // Si nos desactivaron a mitad de un destello, el alfa
                // quedo pegado en el pico. Al volver, limpiar.
                SetAlpha(0f);
            }
        }

        void OnDisable()
        {
            // Desactivar el GameObject mata las corrutinas en seco, sin
            // dejarlas llegar a su linea final: el alfa se quedaba clavado
            // en el pico y la pantalla entera aparecia blanca al reactivar
            // el HUD (pasa de verdad al abrir la pantalla de victoria).
            StopAllCoroutines();
            SetAlpha(0f);
            if (Instance == this) Instance = null;
        }

        // API publica. peakAlpha es el pico instantaneo; seconds, lo que
        // tarda en apagarse.
        public void Flash(Color color, float peakAlpha, float seconds)
        {
            if (flash == null) return;

            // SIEMPRE se corta lo que hubiera antes, incluso si despues
            // salimos temprano: diez explosiones seguidas apilaban diez
            // corrutinas, cada una escribiendo el alfa en el mismo frame,
            // y la que terminaba primero lo bajaba a cero mientras las
            // otras lo volvian a subir. Resultado: pantalla blanca pegada
            // hasta que la ultima corrutina se dignaba a terminar.
            StopAllCoroutines();

            // Los destellos son efecto de camara, y los efectos de camara
            // son la principal causa de mareo. Si el jugador los apago en
            // pausa, esto no existe (y deja la pantalla limpia, no con el
            // alfa que hubiera quedado del destello anterior).
            if (!SP.CameraSystem.CameraFxSettings.Enabled)
            {
                SetAlpha(0f);
                return;
            }

            // La suite de tests corre en Edit mode y ahi StartCoroutine no
            // avanza nunca: sin esta guarda, el alfa se quedaria en el
            // pico para siempre. Se cumple igual la promesa de terminar en
            // cero exacto.
            if (!Application.isPlaying)
            {
                SetAlpha(0f);
                return;
            }

            peakAlpha = Mathf.Clamp01(peakAlpha);
            if (peakAlpha <= 0f || seconds <= 0f)
            {
                SetAlpha(0f);
                return;
            }

            flash.color = new Color(color.r, color.g, color.b, peakAlpha);
            StartCoroutine(FadeOut(color, peakAlpha, seconds));
        }

        IEnumerator FadeOut(Color color, float peakAlpha, float seconds)
        {
            // El pico ya se aplico en Flash(): la subida es instantanea a
            // proposito (un fogonazo no tiene rampa de entrada), lo unico
            // que se anima es la bajada.
            float t = 0f;
            while (t < seconds)
            {
                // unscaledDeltaTime, NO deltaTime: KillFeedbackDirector
                // pone timeScale en 0.25 en la ultima baja (el destello
                // duraba 4 veces mas de lo debido) y GameOutcomeController
                // lo pone en 0 en las pantallas finales (el destello
                // quedaba CONGELADO tapando la pantalla de victoria, sin
                // nada que lo volviera a bajar).
                t += Time.unscaledDeltaTime;
                float a = Mathf.Lerp(peakAlpha, 0f, t / seconds);
                flash.color = new Color(color.r, color.g, color.b, a);
                yield return null;
            }
            // Cierre explicito: el Lerp del ultimo frame deja restos de
            // alfa (0.004 y similares) y una capa blanca a pantalla
            // completa con alfa residual lava todo el HUD.
            SetAlpha(0f);
        }

        void SetAlpha(float a)
        {
            if (flash == null) return;
            var c = flash.color;
            flash.color = new Color(c.r, c.g, c.b, a);
        }

        // --- helpers estaticos (no explotan si no hay vista en escena) ---

        // 181: fogonazo de explosion cercana. intensity01 = 1 es a
        // quemarropa, 0 es al limite del radio. Blanco calido (tirando a
        // amarillo) porque el blanco puro se leia como un glitch de
        // render, no como fuego.
        public static void Explosion(float intensity01)
        {
            // Puede no haber vista: escenas de test, o el HUD todavia sin
            // construir cuando ya vuela metralla. No es un error, es un
            // efecto opcional -- se ignora en silencio.
            if (Instance == null) return;

            intensity01 = Mathf.Clamp01(intensity01);
            var warmWhite = new Color(1f, 0.94f, 0.82f);
            // Al borde del radio apenas se insinua (0.12) y a quemarropa
            // casi tapa la pantalla (0.75). Con un valor fijo, la
            // explosion lejana asustaba tanto como la de al lado y el
            // efecto perdia toda su funcion de aviso de proximidad.
            float peak = Mathf.Lerp(0.12f, 0.75f, intensity01);
            // Corto siempre: mas de ~0.3 s de pantalla blanca deja al
            // jugador ciego en medio de un tiroteo, que es castigo, no
            // feedback.
            float seconds = Mathf.Lerp(0.12f, 0.28f, intensity01);
            Instance.Flash(warmWhite, peak, seconds);
        }

        // 184: transicion de modo de camara. Gris neutro, mas corto y mas
        // sutil que el de explosion: aca el destello no es un evento
        // dramatico, solo tapa el salto duro de la camara al cambiar de
        // modo. Si fuera blanco y largo, cada cambio de modo parecia un
        // bombardeo.
        public static void ModeChange()
        {
            if (Instance == null) return;
            Instance.Flash(new Color(0.72f, 0.74f, 0.78f), 0.35f, 0.18f);
        }
    }
}
