using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using SP.Actors;
using SP.Combat;
using SP.Core;
using SP.Player;

namespace SP.Tutorial
{
    // Gestos de las ordenes radiales, la seleccion en FPS, el tanque y la torreta fija. La mira es real (se gira al
    // soldado y se sube o baja la camara con MirarHacia, asi el juego calcula 'lo apuntado' igual que con una persona);
    // la eleccion en el anillo del radial se dispara con EjecutarOrdenRadial, el mismo camino que ejecuta el menu al soltar Q.
    public partial class TutorialAutoPlayer
    {
        static List<Soldier> AliadosVivos(Soldier yo)
        {
            var l = new List<Soldier>();
            foreach (var s in ActorRegistry.All)
                if (s != null && s != yo && s.Team == TeamId.Player && s.Role != RoleType.Civilian && s.Health != null && s.Health.IsAlive && s.gameObject.activeInHierarchy) l.Add(s);
            return l;
        }

        static Vector3 AlSuelo(Vector3 p) => new Vector3(p.x, 0.05f, p.z);

        // Apunta a 'objetivo()' (se recalcula en cada intento, por si se mueve) y ejecuta la orden radial; reintenta un rato.
        IEnumerator Radial(PlayerInputDriver d, int cat, int sub, System.Func<Vector3?> objetivo = null, float tope = 12f)
        {
            float limite = Time.time + tope;
            while (Time.time < limite)
            {
                var p = objetivo != null ? objetivo() : null;
                if (p.HasValue) yield return MirarHacia(d, p.Value);
                yield return new WaitForSeconds(0.15f);
                if (d.EjecutarOrdenRadial(cat, sub)) yield break;
                yield return new WaitForSeconds(0.6f);
            }
        }

        IEnumerator GestoSeleccionarReal(PlayerInputDriver d)
        {
            yield return new WaitForSeconds(2.5f);   // el paso adelanta a los aliados 9 m
            float limite = Time.time + 20f;
            while (Time.time < limite && d.Selection != null && d.Selection.Selected.Count == 0)
            {
                var a = AliadosVivos(d.Brain.Current);
                if (a.Count > 0) yield return MirarHacia(d, a[0].transform.position + Vector3.up * 0.9f);
                Entrada.Apretar(Key.LeftShift);
                yield return new WaitForSeconds(0.1f);
                Entrada.BotonDerecho(true);
                yield return new WaitForSeconds(0.15f);
                Entrada.BotonDerecho(false);
                Entrada.Soltar(Key.LeftShift);
                yield return new WaitForSeconds(0.6f);
            }
        }

        IEnumerator GestoMoverFpsReal(PlayerInputDriver d)
        {
            var zona = GameObject.Find("Tut_ZonaB");
            var destino = zona != null ? zona.transform.position : d.Brain.Current.transform.position + d.Brain.Current.transform.forward * 8f;
            yield return MirarHacia(d, AlSuelo(destino));
            Entrada.BotonDerecho(true);
            yield return new WaitForSeconds(0.15f);
            Entrada.BotonDerecho(false);
            yield return new WaitForSeconds(0.5f);
        }

        IEnumerator GestoIrYAtacarReal(PlayerInputDriver d)
        {
            var yo = d.Brain.Current;
            var frente = Vector3.ProjectOnPlane(d.Rig.Cam.transform.forward, Vector3.up).normalized;
            yield return Radial(d, 0, 0, () => AlSuelo(yo.transform.position + frente * 10f));
            yield return new WaitForSeconds(1f);
            var e = GameObject.Find("Tut_Enemigo_Ataque");
            if (e != null) yield return Radial(d, 2, 0, () => GameObject.Find("Tut_Enemigo_Ataque")?.transform.position + Vector3.up * 1.0f);
        }

        IEnumerator GestoCubrirseReal(PlayerInputDriver d)
        {
            var tm = TutorialManager.Instance;
            // Si un aliado queda trabado camino a la cobertura, se repite la orden (otra cobertura la segunda vez), como haria una persona.
            for (int intento = 0; intento < 3 && !tm.Flags.aliadosEnCobertura; intento++)
            {
                string nombre = "Tut_Cobertura_" + (intento % 2 == 0 ? 1 : 2);
                if (intento > 0 && d.Squad != null)
                {
                    // Al que quedo trabado se lo manda primero junto a los sacos (IR ALLI solo a el), para que su cobertura mas cercana sea esa.
                    var sacos = GameObject.Find(nombre) ?? GameObject.Find("Tut_Cobertura_1");
                    for (int i = 0; i < d.Squad.Count && sacos != null; i++)
                    {
                        var a = d.Squad[i];
                        if (a == null || a == d.Brain.Current || a.Health == null || !a.Health.IsAlive || a.Brain.EnCobertura) continue;
                        var atras = sacos.transform.position - sacos.transform.forward * 2.5f;
                        yield return Radial(d, 0, i + 1, () => AlSuelo(atras), 4f);
                    }
                    yield return new WaitForSeconds(6f);
                }
                yield return Radial(d, 1, 0, () =>
                {
                    var c = GameObject.Find(nombre) ?? GameObject.Find("Tut_Cobertura_1");
                    return c != null ? c.transform.position + Vector3.up * 0.5f : (Vector3?)null;
                });
                float espera = Time.time + 18f;
                while (Time.time < espera && !tm.Flags.aliadosEnCobertura) yield return new WaitForSeconds(0.5f);
            }
        }

        IEnumerator GestoCurarAliadoReal(PlayerInputDriver d)
        {
            yield return Radial(d, 4, 2, () =>
            {
                Soldier peor = null; float f0 = 0.999f;
                foreach (var a in AliadosVivos(d.Brain.Current))
                {
                    float f = (float)a.Health.Current / Mathf.Max(1, a.Health.MaxHealth);
                    if (f < f0) { f0 = f; peor = a; }
                }
                return peor != null ? peor.transform.position + Vector3.up * 0.9f : (Vector3?)null;
            });
        }

        IEnumerator GestoReanimarReal(PlayerInputDriver d)
        {
            Vector3? Caido()
            {
                foreach (var s in Object.FindObjectsByType<Soldier>(FindObjectsInactive.Exclude))
                    if (s != null && s.Team == TeamId.Player && s.Health != null && !s.Health.IsAlive && s.Role != RoleType.Civilian)
                    {
                        var col = s.GetComponentInChildren<Collider>();
                        return col != null ? col.bounds.center : s.transform.position + Vector3.up * 0.3f;
                    }
                return null;
            }
            // La mira solo lo reconoce cerca y con la vista libre: se camina hasta estar a unos 4 m.
            var pos = Caido();
            if (pos.HasValue)
            {
                yield return MirarHacia(d, AlSuelo(pos.Value));
                yield return CaminarHasta(d, pos.Value, 4f, 20f);
            }
            yield return Radial(d, 4, 3, Caido);
        }

        static Vector3? PuntoDelMuro()
        {
            var m = GameObject.Find("Tut_MuroDemolible");
            return m != null ? m.transform.position + Vector3.up * 0.8f : (Vector3?)null;
        }

        IEnumerator GestoDemolerReal(PlayerInputDriver d)
        {
            yield return Radial(d, 7, 1, PuntoDelMuro);
            Entrada.Apretar(Key.LeftCtrl);
            yield return new WaitForSeconds(5f);
            Entrada.Soltar(Key.LeftCtrl);
            yield return new WaitForSeconds(0.3f);
        }

        IEnumerator GestoBombaAliadoReal(PlayerInputDriver d)
        {
            yield return Radial(d, 7, 0, PuntoDelMuro);
            yield return new WaitForSeconds(1.5f);
            yield return Radial(d, 7, 2, PuntoDelMuro, 4f);
            yield return new WaitForSeconds(0.6f);
            yield return Radial(d, 7, 0, PuntoDelMuro);
        }

        IEnumerator GestoTorretaFijaReal(PlayerInputDriver d)
        {
            var t = GameObject.Find("Tut_TorretaFija");
            if (t == null) yield break;
            yield return MirarHacia(d, AlSuelo(t.transform.position));
            yield return CaminarHasta(d, t.transform.position, 2.5f, 8f);
            yield return Radial(d, 8, 0, () => t.transform.position + Vector3.up * 0.4f);
            yield return new WaitForSeconds(1f);
            Entrada.BotonIzquierdo(true);
            yield return new WaitForSeconds(2f);
            Entrada.BotonIzquierdo(false);
            yield return new WaitForSeconds(0.4f);
            yield return Radial(d, 8, 1);
        }

        IEnumerator GestoEntrarTanqueReal(PlayerInputDriver d)
        {
            var v = d.Vehicle;
            if (v == null) yield break;
            yield return MirarHacia(d, AlSuelo(v.transform.position));
            yield return CaminarHasta(d, v.transform.position, 4.5f, 40f);
            yield return Radial(d, 5, 3, () => v.transform.position + Vector3.up * 1.0f);
        }

        IEnumerator GestoAliadosAlTanqueReal(PlayerInputDriver d)
        {
            yield return Radial(d, 5, 0);
        }

        IEnumerator GestoCambiarATorretaReal() { yield return Tocar(Key.Digit2); }

        IEnumerator GestoMiraTanqueReal()
        {
            Entrada.BotonDerecho(true); yield return new WaitForSeconds(1.6f); Entrada.BotonDerecho(false);
            yield return new WaitForSeconds(0.4f);
            yield return Tocar(Key.Digit3);
            yield return new WaitForSeconds(0.5f);
            Entrada.BotonDerecho(true); yield return new WaitForSeconds(1.6f); Entrada.BotonDerecho(false);
            yield return new WaitForSeconds(0.4f);
            yield return Tocar(Key.Digit2);
            yield return new WaitForSeconds(0.6f);
        }

        static SP.Vehicles.TurretWeapon CanonDe(SP.Vehicles.Vehicle v)
        {
            if (v == null) return null;
            foreach (var t in v.GetComponentsInChildren<SP.Vehicles.TurretWeapon>(true))
                if (t.name == "TurretPivot") return t;
            return null;
        }

        // Apunta el canon con movimientos del mouse virtual (mouse.delta, lo mismo que lee el juego para girar la torreta).
        IEnumerator ApuntarCanon(PlayerInputDriver d, Vector3 objetivo)
        {
            var tur = CanonDe(d.Vehicle);
            if (tur == null) yield break;
            float sens = Mathf.Max(0.01f, d.TurretSensitivity);
            for (int i = 0; i < 40; i++)
            {
                Vector3 dir = objetivo - tur.transform.position;
                float yawObjetivo = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
                float pitchObjetivo = -Mathf.Asin(Mathf.Clamp(dir.normalized.y, -1f, 1f)) * Mathf.Rad2Deg;
                float gy = Mathf.DeltaAngle(tur.DesiredYaw, yawObjetivo);
                float gp = pitchObjetivo - tur.DesiredPitch;
                if (Mathf.Abs(gy) < 0.8f && Mathf.Abs(gp) < 0.8f && tur.IsOnTarget(3f)) break;
                Entrada.Mover(new Vector2(gy / sens, -gp / sens));
                yield return null; yield return null; yield return null;
            }
        }

        IEnumerator GestoAvanzarYDispararReal(PlayerInputDriver d)
        {
            var v = d.Vehicle;
            var tm = TutorialManager.Instance;
            if (v == null) yield break;
            float limite = Time.time + 15f;
            while (Time.time < limite && v.Driver == null) yield return null;
            var adelante = Vector3.ProjectOnPlane(v.transform.forward, Vector3.up).normalized;
            for (int i = 0; i < 4 && !tm.Flags.ordenDeAvanzar; i++)
            {
                yield return ApuntarCanon(d, AlSuelo(v.transform.position + adelante * 22f));
                yield return new WaitForSeconds(0.8f);
                if (d.EjecutarOrdenRadial(5, 2)) break;
                yield return new WaitForSeconds(1f);
            }
            limite = Time.time + 95f;
            while (Time.time < limite && tm.PasoActual != null && tm.PasoActual.Id == "avanzar_disparar")
            {
                Soldier objetivo = null; float mejor = float.MaxValue;
                foreach (var s in ActorRegistry.All)
                {
                    if (s == null || s.Team != TeamId.Enemy || s.Health == null || !s.Health.IsAlive || !s.gameObject.activeInHierarchy) continue;
                    float dist = (s.transform.position - v.transform.position).sqrMagnitude;
                    if (dist < mejor) { mejor = dist; objetivo = s; }
                }
                if (objetivo == null) { yield return new WaitForSeconds(0.5f); continue; }
                yield return ApuntarCanon(d, objetivo.transform.position + Vector3.up * 0.8f);
                Entrada.BotonIzquierdo(true);
                yield return new WaitForSeconds(0.12f);
                Entrada.BotonIzquierdo(false);
                yield return new WaitForSeconds(1.3f);
            }
        }

        IEnumerator GestoFinalReal(PlayerInputDriver d)
        {
            var meta = GameObject.Find("Tut_Meta");
            var v = d.Vehicle;
            if (meta == null || v == null) yield break;
            var tm = TutorialManager.Instance;
            float limite = Time.time + 80f;
            while (Time.time < limite && tm.PasoActual != null && tm.PasoActual.Id == "final")
            {
                yield return ApuntarCanon(d, AlSuelo(meta.transform.position));
                yield return new WaitForSeconds(0.8f);
                d.EjecutarOrdenRadial(5, 2);
                yield return new WaitForSeconds(5f);
            }
        }

        IEnumerator GestoBajarTanqueReal(PlayerInputDriver d)
        {
            yield return Radial(d, 5, 1);
            yield return new WaitForSeconds(1.5f);
            yield return Radial(d, 5, 4);
        }
    }
}
