using System.Collections;
using UnityEngine;
using SP.Core;

namespace SP.Presentation
{
    // Bug #274: "al dar una cuchillada el tiempo se relentiza y se escucha un desgarro, un buen efecto sonoro; mata de una cuchillada de [F];
    // por 1 segundo se relentiza la escena y luego vuelve a la realidad con un fiush y el logro de ganar".
    // Solo cuando la cuchillada del jugador MATA. La escena corre a ~15 % durante 1 s real, suena el desgarro al empezar, y al volver a la
    // velocidad normal un barrido (fiush) y el sonido de logro con su cartel.
    public sealed class CuchilladaLenta : MonoBehaviour
    {
        public const float Escala = 0.15f;
        public const float SegundosReales = 1.0f;

        public static bool Activa { get; private set; }
        public static int Disparadas { get; private set; }
        public static string UltimoLogro { get; private set; } = "";
        static CuchilladaLenta instancia;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reiniciar() { Activa = false; Disparadas = 0; UltimoLogro = ""; instancia = null; }

        public static void ResetearContadores() { Disparadas = 0; UltimoLogro = ""; }

        // Llamado por CuchilloFx cuando el jugador mata de una cuchillada.
        public static void Disparar(Vector3 donde)
        {
            if (!Application.isPlaying || Activa) return;
            if (Time.timeScale < 0.5f) return;   // pausa, camara lenta final o pantalla de resultado: no se pisa
            if (instancia == null)
            {
                var go = new GameObject("CuchilladaLenta") { hideFlags = HideFlags.HideAndDontSave };
                instancia = go.AddComponent<CuchilladaLenta>();
            }
            Disparadas++;
            instancia.StartCoroutine(instancia.Rutina(donde));
        }

        IEnumerator Rutina(Vector3 donde)
        {
            Activa = true;
            float previa = Time.timeScale;
            Time.timeScale = Escala;
            AudioDirector.PlayAt(SfxKind.KnifeHit, donde, 1f, 0.55f);   // el desgarro: el golpe de cuchillo grave y lento
            GenericSfx.PlayOneShot2D(GenericSfx.Get(SfxKind.Explosion), 0.18f * AudioDirector.GainFor(SfxChannel.Sfx), 0.5f, "Desgarro");
            yield return new WaitForSecondsRealtime(SegundosReales);
            Terminar();
            // Vuelve a la realidad: barrido y logro.
            GenericSfx.PlayOneShot2D(GenericSfx.Get(SfxKind.CameraSwoosh), 0.8f * AudioDirector.GainFor(SfxChannel.Sfx), 1.25f, "Fiush");
            GenericSfx.PlayOneShot2D(GenericSfx.Get(SfxKind.Logro), 0.7f * AudioDirector.GainFor(SfxChannel.Sfx), 1.12f, "Logro");
            UltimoLogro = "LOGRO · CUCHILLADA LETAL";
            SP.UI.AvisoUnico.Mostrar(UltimoLogro, 2.2f, new Color(1f, 0.85f, 0.25f), SP.UI.AvisoUnico.PrioridadAlta);
            if (previa <= 0.001f) yield break;
        }

        void Terminar()
        {
            if (!Activa) return;
            Activa = false;
            if (Mathf.Approximately(Time.timeScale, Escala)) Time.timeScale = 1f;   // el vigilante del modo dios lo vuelve a x2 si corresponde
        }

        void OnDisable() { Terminar(); }
    }
}
