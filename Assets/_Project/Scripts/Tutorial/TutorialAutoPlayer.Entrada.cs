using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using SP.Actors;
using SP.Core;
using SP.Player;

namespace SP.Tutorial
{
    // Gestos con teclado y mouse virtuales (ver EntradaVirtual): el juego los lee como si los hiciera una persona.
    public partial class TutorialAutoPlayer
    {
        EntradaVirtual Entrada => EntradaVirtual.Asegurar(gameObject);

        IEnumerator Mantener(float segundos, params Key[] teclas)
        {
            Entrada.Apretar(teclas);
            yield return new WaitForSeconds(segundos);
            Entrada.Soltar(teclas);
            yield return new WaitForSeconds(0.1f);
        }

        IEnumerator Tocar(Key tecla) { yield return Mantener(0.12f, tecla); }

        static float Elevacion(Vector3 v) => Mathf.Asin(Mathf.Clamp(v.normalized.y, -1f, 1f)) * Mathf.Rad2Deg;

        // Gira al soldado y sube o baja la mira hasta que el centro de la pantalla apunte a 'objetivo'.
        IEnumerator MirarHacia(PlayerInputDriver d, Vector3 objetivo)
        {
            var cam = d.Rig.Cam.transform;
            var motor = d.Brain.Current.Motor;
            float k = 0f;
            for (int i = 0; i < 16; i++)
            {
                Vector3 dir = objetivo - cam.position;
                float yaw = Vector3.SignedAngle(Vector3.ProjectOnPlane(cam.forward, Vector3.up), Vector3.ProjectOnPlane(dir, Vector3.up), Vector3.up);
                motor.RotateYaw(yaw);
                if (k == 0f)
                {
                    float antes = Elevacion(cam.forward);
                    d.Rig.AddPitch(3f);
                    yield return null; yield return null;
                    k = Elevacion(cam.forward) - antes > 0f ? 1f : -1f;
                    d.Rig.AddPitch(-3f);
                    yield return null;
                }
                float dif = Elevacion(dir) - Elevacion(cam.forward);
                d.Rig.AddPitch(k * dif);
                yield return null; yield return null; yield return null; yield return null;
                if (Mathf.Abs(yaw) < 0.7f && Mathf.Abs(dif) < 1f) break;
            }
        }

        // Camina (W apretada) hacia un punto hasta quedar a 'distancia' o agotar el tiempo.
        IEnumerator CaminarHasta(PlayerInputDriver d, Vector3 destino, float distancia, float maxSegundos)
        {
            float limite = Time.time + maxSegundos;
            Entrada.Apretar(Key.W);
            while (Time.time < limite)
            {
                var yo = d.Brain.Current.transform.position;
                var plano = destino - yo; plano.y = 0f;
                if (plano.magnitude <= distancia) break;
                float yaw = Vector3.SignedAngle(Vector3.ProjectOnPlane(d.Rig.Cam.transform.forward, Vector3.up), plano, Vector3.up);
                if (Mathf.Abs(yaw) > 4f) d.Brain.Current.Motor.RotateYaw(yaw);
                yield return null;
            }
            Entrada.Soltar(Key.W);
            yield return new WaitForSeconds(0.1f);
        }

        // Dispara con el clic izquierdo hasta que 'listo' sea verdad; si se vacia el cargador, recarga con R como haria una persona.
        IEnumerator DispararHasta(System.Func<bool> listo, float maxSegundos)
        {
            float limite = Time.time + maxSegundos;
            var w = PlayerInputDriver.Activo?.Brain.Current.Weapon;
            Entrada.BotonIzquierdo(true);
            while (Time.time < limite && !listo())
            {
                if (w != null && w.CurrentAmmo <= 0 && !w.IsReloading)
                {
                    Entrada.BotonIzquierdo(false);
                    yield return Tocar(Key.R);
                    while (w.IsReloading && Time.time < limite + 3f) yield return null;
                    Entrada.BotonIzquierdo(true);
                }
                // Cada tanda suelta y vuelve a apretar el clic: las armas semiautomaticas disparan una vez por clic.
                yield return new WaitForSeconds(0.08f);
                Entrada.BotonIzquierdo(false);
                yield return new WaitForSeconds(0.06f);
                Entrada.BotonIzquierdo(true);
            }
            Entrada.BotonIzquierdo(false);
            yield return new WaitForSeconds(0.15f);
        }

        IEnumerator GestoWasdReal()
        {
            foreach (var k in new[] { Key.W, Key.A, Key.S, Key.D }) yield return Mantener(0.45f, k);
        }

        IEnumerator GestoCorrerReal() { yield return Mantener(1.8f, Key.LeftShift, Key.W); }

        IEnumerator GestoAgacharseReal() { yield return Mantener(0.2f, Key.LeftCtrl); yield return new WaitForSeconds(0.5f); yield return Mantener(0.2f, Key.LeftCtrl); }

        IEnumerator GestoDispararReal(PlayerInputDriver d)
        {
            var dummy = TutorialManager.Instance?.Dummy;
            var pared = TutorialManager.Instance?.Pared;
            var caja = TutorialManager.Instance?.Destruible;
            var yo = d.Brain.Current;
            if (dummy != null)
            {
                yield return MirarHacia(d, dummy.transform.position);
                yield return CaminarHasta(d, dummy.transform.position, 10f, 10f);
                yield return MirarHacia(d, dummy.transform.position + Vector3.up * 0.3f);
                var s = dummy.GetComponent<Soldier>();
                yield return DispararHasta(() => s != null && !s.Health.IsAlive, 12f);
            }
            if (pared != null)
            {
                yield return MirarHacia(d, pared.transform.position);
                yield return CaminarHasta(d, pared.transform.position, 9f, 10f);
                yield return MirarHacia(d, pared.transform.position + Vector3.up * 0.5f);
                yield return DispararHasta(() => TutorialManager.Instance.Flags.disparoPared, 4f);
            }
            if (caja != null)
            {
                yield return MirarHacia(d, caja.transform.position);
                yield return CaminarHasta(d, caja.transform.position, 8f, 10f);
                yield return MirarHacia(d, caja.transform.position + Vector3.up * 0.5f);
                yield return DispararHasta(() => caja == null || !caja.activeInHierarchy, 8f);
            }
        }

        IEnumerator GestoMiraReal(PlayerInputDriver d)
        {
            var dummy = TutorialManager.Instance?.Dummy;
            if (dummy != null) yield return MirarHacia(d, dummy.transform.position + Vector3.up * 1.0f);
            Entrada.BotonDerecho(true);
            yield return new WaitForSeconds(1.6f);
            Entrada.BotonIzquierdo(true);
            yield return new WaitForSeconds(0.35f);
            Entrada.BotonIzquierdo(false);
            yield return new WaitForSeconds(0.2f);
            Entrada.BotonDerecho(false);
            yield return new WaitForSeconds(0.4f);
        }

        IEnumerator GestoArsenalReal(PlayerInputDriver d)
        {
            yield return Tocar(Key.Digit2);
            yield return new WaitForSeconds(0.4f);
            Entrada.BotonIzquierdo(true); yield return new WaitForSeconds(0.3f); Entrada.BotonIzquierdo(false);
            yield return Tocar(Key.R);
            yield return new WaitForSeconds(1.0f);
            Entrada.BotonDerecho(true); yield return new WaitForSeconds(1.4f); Entrada.BotonDerecho(false);
            yield return new WaitForSeconds(0.4f);
        }

        IEnumerator GestoCuchilloReal(PlayerInputDriver d)
        {
            yield return Tocar(Key.F);
            yield return new WaitForSeconds(0.7f);
            var e = TutorialManager.Instance?.EnemigoCuchillo;
            if (e == null) yield break;
            var s = e.GetComponent<Soldier>();
            yield return MirarHacia(d, e.transform.position + Vector3.up * 1.0f);
            yield return CaminarHasta(d, e.transform.position, 1.6f, 8f);
            float limite = Time.time + 9f;
            while (Time.time < limite && s != null && s.Health.IsAlive)
            {
                yield return MirarHacia(d, e.transform.position + Vector3.up * 1.0f);
                yield return Tocar(Key.F);
                yield return new WaitForSeconds(0.7f);
            }
        }

        IEnumerator GestoGranadaReal(PlayerInputDriver d)
        {
            var e = TutorialManager.Instance?.EnemigoGranada;
            if (e != null) yield return MirarHacia(d, e.transform.position + Vector3.up * 3f);
            Entrada.Apretar(Key.G);
            yield return new WaitForSeconds(1.5f);
            Entrada.Soltar(Key.G);
            yield return new WaitForSeconds(3.2f);
        }

        IEnumerator GestoSuministrosReal(PlayerInputDriver d)
        {
            var caja = CajaDeSuministros.Todas.Count > 0 ? CajaDeSuministros.Todas[0] : null;
            if (caja == null) yield break;
            yield return MirarHacia(d, caja.transform.position);
            yield return CaminarHasta(d, caja.transform.position, 0.6f, 9f);
        }

        IEnumerator GestoVistaTacticaReal() { yield return Mantener(0.9f, Key.Tab); }
    }
}
