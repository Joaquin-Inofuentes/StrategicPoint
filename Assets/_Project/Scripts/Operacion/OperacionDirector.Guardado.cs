using System;
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

        // ---------------------------------------------------------------
        // Cola #2 (2026-11-27): "que mi ultimo guardado guarde tambien el progreso del nivel, no solo aparecer en esa zona (mori en el blindado y al
        // cargar estaba la computadora sin explotar pero la pude traspasar igual)". SaltarA rearmaba la fase pero no las estructuras de los objetivos
        // ya cumplidos: al continuar en la huida la salida norte quedaba abierta y la terminal entera (y desde el objetivo 5 la salida seguia en pie).
        // Claves nuevas del estado "Operacion" (todas opcionales: una partida vieja sin ellas se deduce por la fase):
        //   terminalVolada  True/False  la computadora del centro de datos exploto (no se dibuja ni choca)
        //   muroSalidaCaido True/False  Muro_Salida_Norte demolido
        //   tomaJefeHecha   True/False  la presentacion del BLINDADO ya se vio (no se repite)
        //   cargaPorton     volado=..;mecha=..  la carga del porton norte del cuartel (objetivo 1, con la mecha encendida)
        // ---------------------------------------------------------------
        void CapturarProgreso(Dictionary<string, string> kv)
        {
            var ic = System.Globalization.CultureInfo.InvariantCulture;
            kv["terminalVolada"] = TerminalDestruida.ToString();
            kv["muroSalidaCaido"] = (!MuroDeSalidaEnPie() && (muroDeSalida != null || salidaAbierta)).ToString();
            kv["tomaJefeHecha"] = TomaDelJefeHecha.ToString();
            if (cargaPorton != null) kv["cargaPorton"] = $"volado={cargaPorton.EstaVolada};mecha={cargaPorton.MechaRestante.ToString("0.00", ic)}";
        }

        void RestaurarProgreso(Dictionary<string, string> kv, FaseOperacion fase)
        {
            int i = Indice(fase);
            // Terminal y salida norte: el hackeo termina el objetivo 3, asi que desde la huida en adelante siempre estan voladas (partida vieja: por la fase).
            bool terminalVolada = kv.TryGetValue("terminalVolada", out var tv) ? tv == "True" || i > Indice(FaseOperacion.CentroDeDatos) : i > Indice(FaseOperacion.CentroDeDatos);
            if (terminalVolada) DejarTerminalVoladaYa();
            if (fase == FaseOperacion.Huir)
            {
                // La toma del jefe ya se vio si se guardo en una subfase posterior al acercamiento (o arriba del tanque): no vuelve a dispararse.
                bool toma = (kv.TryGetValue("tomaJefeHecha", out var th) && th == "True") || Subfase >= (int)SubfaseHuida.Jefe || EnTanque;
                TomaDelJefeActiva = false;
                if (toma) TomaDelJefeHecha = true;
            }
            if (fase == FaseOperacion.Infiltrar && cargaPorton != null && kv.TryGetValue("cargaPorton", out var cp))
            {
                float mecha = 0f;
                int im = cp.IndexOf("mecha=", StringComparison.Ordinal);
                if (im >= 0) float.TryParse(cp.Substring(im + 6).Split(';')[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out mecha);
                if (!cp.Contains("volado=True") && mecha > 0f) cargaPorton.RestaurarMecha(mecha);
            }
        }

        // La terminal se ve y es solida (un collider no-trigger encendido) / no queda nada de ella (ni dibujo ni choque). Lo usan los checks y el resumen.
        public bool TerminalEnPieYSolida
        {
            get
            {
                if (computadora == null) return false;
                bool dibujo = false, solido = false;
                foreach (var r in computadora.GetComponentsInChildren<Renderer>(true)) if (r.enabled && r.gameObject.activeInHierarchy && !(r is ParticleSystemRenderer)) { dibujo = true; break; }
                foreach (var c in computadora.GetComponentsInChildren<Collider>(true)) if (c.enabled && !c.isTrigger && c.gameObject.activeInHierarchy) { solido = true; break; }
                return dibujo && solido;
            }
        }
        public bool TerminalSinRastro
        {
            get
            {
                if (computadora == null) return true;
                foreach (var r in computadora.GetComponentsInChildren<Renderer>(true)) if (r.enabled && r.gameObject.activeInHierarchy && !(r is ParticleSystemRenderer)) return false;
                foreach (var c in computadora.GetComponentsInChildren<Collider>(true)) if (c.enabled && !c.isTrigger && c.gameObject.activeInHierarchy) return false;
                return true;
            }
        }
        public bool MuroDeSalidaSigueEnPie => MuroDeSalidaEnPie();
        public Collider ColliderDelMuroDeSalida { get { MuroDeSalidaEnPie(); return muroDeSalida != null ? muroDeSalida.GetComponent<Collider>() : null; } }

        // Resumen canonico del progreso de los objetivos (lo compara el check #996: igual antes de guardar y despues de continuar).
        public string ResumenDeProgreso()
        {
            string terminal = TerminalSinRastro ? "volada" : TerminalEnPieYSolida ? "enPie" : "INCOHERENTE";
            string muro = MuroDeSalidaEnPie() ? "enPie" : "caido";
            return $"fase={Fase}/{Subfase};porton={(portonCuartel != null && portonCuartel.activeSelf ? "cerrado" : "abierto")};generador={(generador != null && generador.EstaCaido ? "caido" : "enPie")}"
                 + $";torres={IndicesCaidos(torres)};puestos={ContarVolados()}/{CargasRequeridas};terminal={terminal};muroNorte={muro}"
                 + $";toma={(Fase == FaseOperacion.Huir ? TomaDelJefeHecha.ToString() : "-")};enTanque={EnTanque}";
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
