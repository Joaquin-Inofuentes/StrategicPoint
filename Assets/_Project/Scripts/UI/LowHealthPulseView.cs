using UnityEngine;
using UnityEngine.UI;
using SP.Presentation;

namespace SP.UI
{
    // Pulso rojo en los bordes de la pantalla + latido sonoro cuando al
    // soldado poseido le queda poca vida. Hasta ahora la unica senal de
    // "estas por morir" era mirar el numero de la barra de vida: en medio
    // de un tiroteo nadie lo mira, y la muerte llegaba sin aviso. El pulso
    // es ambiental (periferia + oido), no pide atencion del ojo.
    //
    // Deliberadamente NO comparte la Image de DamageVignetteView: aquella
    // escribe image.color directo en cada frame de su corrutina de flash,
    // asi que si el latido escribiera encima de la misma Image las dos
    // senales se pisarian y quedaria un parpadeo sucio. Con dos Images
    // separadas el flash de dano queda oscuro y el estado critico rojo:
    // dos senales legibles en vez de una sola embarrada.
    public class LowHealthPulseView : MonoBehaviour
    {
        // Debajo de este porcentaje de vida empieza el pulso. Un tercio de
        // la vida deja margen real para reaccionar (buscar cobertura o
        // cambiar de cuerpo) sin encender la alarma en cada rasguno.
        const float CriticalFrac = 0.3f;

        // Pico moderado: tiene que leerse por el rabillo del ojo sin tapar
        // lo que esta pasando en el centro de la pantalla, que es
        // justamente lo que hay que mirar cuando quedas con poca vida.
        const float PeakAlpha = 0.35f;

        // Latidos por segundo en el borde del umbral y al borde de morir.
        const float SlowRate = 1.6f;
        const float FastRate = 3.4f;

        // PUBLICO a proposito: un campo privado asignado al construir la
        // escena en el editor no sobrevive el domain reload al entrar en
        // Play (bug recurrente del proyecto). Serializado, la referencia
        // llega entera; si igual queda null, se resuelve sola en Update.
        public SP.Player.PlayerBrain Brain;

        // La Image vive en un GameObject HIJO ("Pulse"), no en este mismo
        // GO: apagar una Image que comparte GameObject con su vista se
        // termina haciendo con SetActive(false) y eso apaga tambien el
        // componente, que deja de recibir Update y no se puede reencender.
        Image pulse;

        // Numero de ciclo del latido ya sonado. Sin esto el clip se
        // dispararia en CADA frame del pico, no una vez por latido.
        int lastBeatCycle = int.MinValue;

        static Texture2D cachedTexture;
        static Sprite cachedSprite;

        // Expuesto para poder verificar el efecto objetivamente desde los
        // tests, sin depender de mirar la pantalla.
        public float CurrentAlpha => pulse != null ? pulse.color.a : 0f;

        public void Bind(Image image)
        {
            pulse = image;
            if (pulse == null) return;
            Prepare(pulse);
        }

        void OnEnable()
        {
            // Auto-reparacion por la misma razon que el campo Brain: la
            // referencia asignada en el editor se pierde en el reload.
            if (pulse == null)
            {
                var child = transform.Find("Pulse");
                if (child != null) pulse = child.GetComponent<Image>();
                if (pulse != null) Prepare(pulse);
            }

            // Arranca apagado y sin ciclo pendiente: al reactivarse la
            // vista el primer latido tiene que sonar cuando corresponda,
            // no arrastrar el conteo de la corrida anterior.
            lastBeatCycle = int.MinValue;
            SetAlpha(0f);
        }

        void OnDisable() => SetAlpha(0f);

        // Un unico Update, en el objeto de HUD. La alternativa (un
        // componente por soldado que vigile su propia vida) serian
        // decenas de Updates para alimentar una sola Image.
        void Update()
        {
            if (pulse == null) return;

            if (!Application.isPlaying) { Silence(); return; }

            // Interruptor global de efectos de camara: los efectos
            // pulsantes son de las principales causas de mareo, asi que
            // tienen que poder apagarse sin apagar nada mas del juego.
            if (!SP.CameraSystem.CameraFxSettings.Enabled) { Silence(); return; }

            if (Brain == null)
            {
                // Resolucion perezosa y cacheada: la busqueda por escena es
                // cara, no puede correr todos los frames. FindFirstObjectByType
                // esta obsoleto en esta version; FindAnyObjectByType alcanza
                // porque el PlayerBrain es unico en la escena.
                Brain = Object.FindAnyObjectByType<SP.Player.PlayerBrain>();
                if (Brain == null) { Silence(); return; }
            }

            var soldier = Brain.Current;
            if (soldier == null) { Silence(); return; }

            var health = soldier.Health;
            // Muerto o sin Health: el pulso se apaga solo, sin necesidad de
            // escuchar el evento de muerte ni el de cambio de poseido.
            if (health == null || !health.IsAlive || health.MaxHealth <= 0) { Silence(); return; }

            float frac = (float)health.Current / health.MaxHealth;
            // Curarse por encima del umbral (o poseer un soldado sano)
            // apaga el pulso de verdad y lo deja en cero, no en el ultimo
            // valor que hubiera quedado del seno.
            if (frac >= CriticalFrac) { Silence(); return; }

            // Cuanto menos vida queda, mas rapido late. Es la variable que
            // comunica "esto empeora" sin ningun texto ni numero.
            float t = 1f - Mathf.Clamp01(frac / CriticalFrac);
            float rate = Mathf.Lerp(SlowRate, FastRate, t);

            float wave = Mathf.Abs(Mathf.Sin(Time.time * Mathf.PI * rate));
            SetAlpha(wave * PeakAlpha);

            // |sin(pi * x)| tiene sus picos en x = 0.5 + k, asi que el
            // numero entero de "medio ciclo corrido" identifica al latido
            // actual: mientras no cambie, el golpe ya sono.
            int cycle = Mathf.FloorToInt(Time.time * rate + 0.5f);
            if (cycle != lastBeatCycle)
            {
                lastBeatCycle = cycle;
                GenericSfx.PlayOneShot2D(GenericSfx.Get(SfxKind.Heartbeat), 0.5f, 1f, "Heartbeat");
            }
        }

        // Apagado completo: alfa en cero y conteo reseteado, para que al
        // volver a caer en critico el primer latido suene enseguida en vez
        // de esperar al pico del ciclo que quedo a medias.
        void Silence()
        {
            lastBeatCycle = int.MinValue;
            SetAlpha(0f);
        }

        void SetAlpha(float a)
        {
            if (pulse == null) return;
            var c = pulse.color;
            // Solo se toca el alfa: el tinte rojo lo fija Prepare una vez,
            // asi un cambio de paleta no se pierde en cada frame.
            if (Mathf.Approximately(c.a, a)) return;
            pulse.color = new Color(c.r, c.g, c.b, a);
        }

        static void Prepare(Image image)
        {
            // Sprite propio (no el de DamageVignetteView) para que las dos
            // senales sigan siendo independientes tambien en el arte.
            if (image.sprite == null) image.sprite = GetOrBuildSprite();
            image.type = Image.Type.Simple;
            image.color = new Color(0.85f, 0.05f, 0.05f, 0f);
            // El HUD no debe robarle clics al juego.
            image.raycastTarget = false;
        }

        static Sprite GetOrBuildSprite()
        {
            if (cachedSprite != null) return cachedSprite;
            var tex = GetOrBuildTexture();
            cachedSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
            return cachedSprite;
        }

        // Textura generada por codigo (el prototipo no depende de assets
        // importados): transparente en el centro y opaca cerca del borde,
        // para que el rojo quede en la periferia -- que es donde la vision
        // detecta movimiento sin tener que mirarlo -- y no tape la mira.
        static Texture2D GetOrBuildTexture()
        {
            if (cachedTexture != null) return cachedTexture;

            const int size = 128;
            var tex = new Texture2D(size, size, TextureFormat.Alpha8, false);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float nx = (x - size * 0.5f) / (size * 0.5f);
                    float ny = (y - size * 0.5f) / (size * 0.5f);
                    float dist = Mathf.Sqrt(nx * nx + ny * ny);
                    // Umbrales corridos bien afuera: esta textura cuadrada
                    // se estira sobre un canvas 16:9, y un degradado que
                    // arranca cerca del centro terminaria pintando media
                    // pantalla en vez de un marco.
                    float a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.6f, 1.2f, dist));
                    tex.SetPixel(x, y, new Color(0f, 0f, 0f, a));
                }
            }
            tex.Apply();
            cachedTexture = tex;
            return tex;
        }
    }
}
