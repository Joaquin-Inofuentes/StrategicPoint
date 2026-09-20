using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using SP.Core;
using SP.Player;

namespace SP.UI
{
    // Marca en el borde de pantalla que apunta hacia de donde vino el
    // ultimo golpe. Antes, recibir un disparo desde fuera de camara no
    // daba ninguna pista de la direccion: el jugador giraba al azar
    // buscando al atacante y a menudo moria mientras lo hacia.
    public class DamageDirectionView : MonoBehaviour
    {
        Image arrow;
        PlayerBrain brain;
        Coroutine routine;
        static Texture2D cachedWedgeTexture;

        public void Bind(Image arrowImage, PlayerBrain playerBrain)
        {
            arrow = arrowImage;
            brain = playerBrain;
            // BUG REAL: un Image sin sprite se dibuja como un cuadrado
            // blanco solido -- rotarlo no comunica ninguna direccion (un
            // cuadrado se ve igual a 0 y a 180 grados). Sin una forma con
            // una punta real (cuña apuntando hacia arriba) el indicador de
            // direccion de daño era indistinguible de un simple parpadeo.
            if (arrow.sprite == null)
            {
                var tex = GetOrBuildWedgeTexture();
                arrow.sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
                arrow.type = Image.Type.Simple;
                arrow.preserveAspect = true;
            }
            arrow.gameObject.SetActive(false);
        }

        // Cuña triangular apuntando hacia arriba (mismo enfoque que
        // DamageVignetteView.GetOrBuildTexture: una textura Alpha8 armada
        // una sola vez a mano, sin depender de ningun asset de sprite).
        // Generada en un cuadrado; PointInTriangle decide, pixel a pixel,
        // si cae dentro del triangulo (apex arriba, base abajo).
        static Texture2D GetOrBuildWedgeTexture()
        {
            if (cachedWedgeTexture != null) return cachedWedgeTexture;

            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.Alpha8, false);
            // Vertices en espacio normalizado [-1,1], Y hacia arriba.
            const float ax = 0f, ay = 0.95f;
            const float bx = -0.65f, by = -0.85f;
            const float cx = 0.65f, cy = -0.85f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float nx = (x + 0.5f) / size * 2f - 1f;
                    float ny = (y + 0.5f) / size * 2f - 1f;
                    bool inside = PointInTriangle(nx, ny, ax, ay, bx, by, cx, cy);
                    tex.SetPixel(x, y, new Color(0f, 0f, 0f, inside ? 1f : 0f));
                }
            }
            tex.Apply();
            cachedWedgeTexture = tex;
            return tex;
        }

        static bool PointInTriangle(float px, float py, float ax, float ay, float bx, float by, float cx, float cy)
        {
            float d1 = Sign(px, py, ax, ay, bx, by);
            float d2 = Sign(px, py, bx, by, cx, cy);
            float d3 = Sign(px, py, cx, cy, ax, ay);
            bool hasNeg = d1 < 0f || d2 < 0f || d3 < 0f;
            bool hasPos = d1 > 0f || d2 > 0f || d3 > 0f;
            return !(hasNeg && hasPos);
        }

        static float Sign(float x1, float y1, float x2, float y2, float x3, float y3)
            => (x1 - x3) * (y2 - y3) - (x2 - x3) * (y1 - y3);

        IDisposable sub;

        // Initialize() se llama al armar la escena en el Editor (fuera de
        // Play mode), y esa suscripcion al EventBus NO sobrevive al
        // domain reload al entrar en Play -- mismo motivo por el que
        // AimUI/DamageVignetteView vuelven a suscribirse solas en
        // OnEnable en vez de confiar en la suscripcion original.
        void OnEnable()
        {
            // `arrow` (asignado por Bind() al armar la escena en Editor)
            // es un campo privado comun -- no sobrevive al domain reload
            // de entrar en Play mode, igual que `brain`. Se re-busca por
            // nombre entre los hijos.
            if (arrow == null)
            {
                var t = transform.Find("Arrow");
                if (t != null) arrow = t.GetComponent<Image>();
            }
            if (brain == null) brain = FindAnyObjectByType<PlayerBrain>();
            if (sub == null) Initialize();
        }

        public void Initialize()
        {
            sub?.Dispose();
            sub = EventBus.Instance.Subscribe<DamageTakenEvent>(OnDamage);
        }

        void OnDestroy() => sub?.Dispose();

        void OnDamage(DamageTakenEvent evt)
        {
            if (!Application.isPlaying || arrow == null || brain == null || brain.Current == null) return;
            if (evt.TargetId != brain.Current.Id) return;

            var attacker = ActorRegistry.FindById(evt.AttackerId);
            if (attacker == null) return;

            // Angulo entre hacia-donde-mira el jugador y hacia-donde-esta
            // el atacante, medido en el plano horizontal (Y no importa
            // para "de que lado viene el tiro").
            Vector3 toAttacker = attacker.transform.position - brain.Current.transform.position;
            toAttacker.y = 0f;
            if (toAttacker.sqrMagnitude < 0.0001f) return;

            Vector3 forward = brain.Current.transform.forward;
            forward.y = 0f;

            float signedAngle = Vector3.SignedAngle(forward, toAttacker, Vector3.up);
            // La flecha en el Canvas rota en el plano de pantalla: un
            // atacante a la derecha (angulo positivo en mundo) debe
            // rotar la flecha en sentido horario, por eso el signo se
            // invierte respecto del giro de mundo.
            arrow.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -signedAngle);

            if (routine != null) StopCoroutine(routine);
            routine = StartCoroutine(ShowAndHide());
        }

        IEnumerator ShowAndHide()
        {
            arrow.gameObject.SetActive(true);
            arrow.color = new Color(0.95f, 0.25f, 0.2f, 1f);
            const float holdTime = 0.9f;
            float t = 0f;
            while (t < holdTime)
            {
                t += Time.unscaledDeltaTime;
                arrow.color = new Color(0.95f, 0.25f, 0.2f, Mathf.Lerp(1f, 0f, t / holdTime));
                yield return null;
            }
            arrow.gameObject.SetActive(false);
        }
    }
}
