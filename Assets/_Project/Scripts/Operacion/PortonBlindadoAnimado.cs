using System.Collections;
using UnityEngine;
using SP.Presentation;

namespace SP.Operacion
{
    // Bug #309 ("cambia la puerta, mejor geometria, animacion de que se abre o rompe y cinematica de 4 segundos"): el arte del portón
    // blindado vive en un GO HERMANO del bloqueo invisible `portonBlindado` (que conserva collider y referencia serializada). Dos hojas
    // con pivote en las bisagras; al volar la carga se abren de golpe hacia afuera, inclinadas y caidas (rotas), y quedan asi.
    public class PortonBlindadoAnimado : MonoBehaviour
    {
        public Transform hojaIzq, hojaDer;
        public Renderer cerradura;

        public const float SegundosDeApertura = 1.2f;
        public const float YawIzq = -105f, YawDer = 98f, Inclinacion = 9f, Caida = 0.3f;

        public bool Abierto { get; private set; }
        public bool Abriendo { get; private set; }

        Quaternion cerradaIzq, cerradaDer;
        Vector3 posIzq, posDer;
        bool guardado;

        void Guardar()
        {
            if (guardado || hojaIzq == null || hojaDer == null) return;
            cerradaIzq = hojaIzq.localRotation; cerradaDer = hojaDer.localRotation;
            posIzq = hojaIzq.localPosition; posDer = hojaDer.localPosition;
            guardado = true;
        }

        // Estado final sin animacion (saltos de fase, restauracion de partida).
        public void DejarAbierto()
        {
            Guardar();
            if (!guardado) return;
            Poner(1f);
            ApagarCerradura();
            Abierto = true; Abriendo = false;
        }

        // Vuelve a cerrado (reinicio de la operacion).
        public void Cerrar()
        {
            Guardar();
            StopAllCoroutines();
            if (guardado) Poner(0f);
            if (cerradura != null) cerradura.enabled = true;
            Abierto = false; Abriendo = false;
        }

        public void Volar()
        {
            Guardar();
            if (!guardado || Abierto || Abriendo) return;
            StartCoroutine(Abrir());
        }

        void Poner(float k)
        {
            hojaIzq.localRotation = cerradaIzq * Quaternion.Euler(Inclinacion * k, YawIzq * k, 0f);
            hojaDer.localRotation = cerradaDer * Quaternion.Euler(Inclinacion * k, YawDer * k, 0f);
            hojaIzq.localPosition = posIzq + Vector3.down * Caida * k;
            hojaDer.localPosition = posDer + Vector3.down * Caida * k;
        }

        void ApagarCerradura() { if (cerradura != null) cerradura.enabled = false; }

        IEnumerator Abrir()
        {
            Abriendo = true;
            var centro = transform.position;
            ImpactFx.SpawnExplosion(centro + Vector3.up * 1.5f, 4f, false);
            ImpactFx.SpawnShockwaveRing(centro, 7f);
            ApagarCerradura();
            float t = 0f;
            bool humo = false;
            while (t < SegundosDeApertura)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / (SegundosDeApertura * 0.6f));
                Poner(1f - (1f - k) * (1f - k));   // ease-out: arranca de golpe y se asienta
                if (!humo && t > 0.15f)
                {
                    humo = true;
                    for (int i = 0; i < 8; i++)
                        ImpactFx.Spawn(centro + new Vector3(Random.Range(-6f, 6f), Random.Range(0.5f, 3.5f), Random.Range(-1f, 1f)), new Color(0.35f, 0.33f, 0.3f), 1.6f, 1.2f);
                }
                yield return null;
            }
            Poner(1f);
            Abierto = true; Abriendo = false;
        }
    }
}
