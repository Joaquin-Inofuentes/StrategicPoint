using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SP.Actors;
using SP.CameraSystem;
using SP.Combat;
using SP.Core;
using SP.Player;

namespace SP.Operacion
{
    // P10 (#120): guardar partida. El autoguardado se programa al empezar cada objetivo y al terminar cada cinematica (apertura, toma del jefe,
    // cinematica final de la huida); espera a que no haya una cinematica ni una pausa y a que el jugador pueda guardarse (a pie, en el suelo, vivo).
    // La restauracion la arma PartidaGuardada (SesionLog.Restaurar + RestaurarEstado de aca + AlRestaurarPartida).
    public partial class OperacionDirector
    {
        Coroutine autoPendiente;
        string motivoAutoPendiente = "";
        public string MotivoAutoPendiente => motivoAutoPendiente;
        public bool AutoguardadoPendiente => autoPendiente != null;
        public const float SegundosMaximosParaAutoguardar = 90f;

        // Si ya habia un autoguardado esperando, los motivos se combinan ("fin CineDeHuida + inicio objetivo 5").
        public void ProgramarAutoguardado(string motivo, float demora = 1.5f)
        {
            if (!Application.isPlaying || !PartidaGuardada.AutoguardadoActivo || PartidaGuardada.Restaurando) return;
            if (autoPendiente != null)
            {
                if (!motivoAutoPendiente.Contains(motivo)) motivoAutoPendiente += " + " + motivo;
                return;
            }
            motivoAutoPendiente = motivo;
            autoPendiente = StartCoroutine(AutoguardarCuandoSePueda(demora));
        }

        IEnumerator AutoguardarCuandoSePueda(float demora)
        {
            float t0 = Time.unscaledTime;
            yield return new WaitForSeconds(demora);
            while (Time.unscaledTime - t0 < SegundosMaximosParaAutoguardar)
            {
                bool pausado = Time.timeScale <= 0.0001f;
                if (!pausado && PartidaGuardada.MotivoDeBloqueo() == null)
                {
                    PartidaGuardada.AutoGuardar(motivoAutoPendiente);
                    break;
                }
                // Termino la mision: ya no se guarda nada.
                if (Fase == FaseOperacion.Victoria || Fase == FaseOperacion.Derrota) break;
                yield return new WaitForSeconds(0.5f);
            }
            autoPendiente = null;
            motivoAutoPendiente = "";
        }

        // Cuando SaltarA (StopAllCoroutines) corta la rutina, el estado "pendiente" tiene que liberarse.
        void LiberarAutoguardadoCortado()
        {
            autoPendiente = null;
            motivoAutoPendiente = "";
        }

        // Solo para los checks: el reloj efectivo del helicoptero en un valor conocido.
        public void FijarRelojEfectivoParaPrueba(float s) { RelojEfectivo = s; }

        // Despues de restaurar mision + soldados + vehiculos (PartidaGuardada.Restaurar).
        public void AlRestaurarPartida(PartidaGuardadaDatos d)
        {
            // Resistir con mando: los atacantes de oleadas sin terminar se relanzan desde cero (los enemigos de la zona se reinician).
            if (Fase == FaseOperacion.Resistir && MandoActivo && Subfase >= 1) DesarmarAtacantesSueltos();
            // Camara: si guardaste en vista tactica, vuelve ahi (sobre el soldado que manejas); en FPS ya la dejo SesionLog.Restaurar.
            var drv = PlayerInputDriver.Activo;
            var yo = Poseido();
            if (d.estado != null && d.estado.jugador != null && d.estado.jugador.modo == "Rts" && drv != null && drv.Rig != null && yo != null && yo.Health.IsAlive && drv.Rig.Mode != ControlMode.Rts)
                drv.Rig.SetMode(ControlMode.Rts, yo.transform.position);
            ForzarHud = true;
            Aviso("PARTIDA CARGADA\nOBJETIVO " + (Indice(Fase) + 1) + "/" + TotalObjetivos);
        }

        // Soldados de reserva de la ciudad que SesionLog dejo activos y vivos: son atacantes de una oleada que habia salido; se guardan de nuevo
        // (inactivos y con la vida llena) para que TomarAtacante los reutilice cuando la oleada vuelva a salir.
        void DesarmarAtacantesSueltos()
        {
            if (oleadasDeLaCiudad == null) return;
            foreach (var ol in oleadasDeLaCiudad)
            {
                if (ol == null || ol.soldados == null) continue;
                foreach (var r in ol.soldados)
                {
                    if (r == null || r.Health == null || !r.Health.IsAlive || !r.gameObject.activeSelf) continue;
                    r.Health.RestaurarVida(r.Health.MaxHealth);
                    r.gameObject.SetActive(false);
                }
            }
        }
    }
}
