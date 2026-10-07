using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using SP.Actors;
using SP.Combat;
using SP.Core;
using SP.Mision;
using SP.Player;
using SP.Presentation;
using SP.Tutorial;
using SP.UI;
using SP.Vehicles;

namespace SP.Operacion
{
    // Fase 2 (PUESTOS, WP9a #082/#097): MURALLA DE CONTENCION. Una sola capa transversal (z -170) con dos bunkers en los flancos; solo el ASALTO
    // coloca la carga de cada uno (CargaExplosiva), arde la mecha de 20 s mientras sale el contraataque (4 enemigos la primera carga, 6 y una
    // camioneta la segunda) y el bunker vuela. Con los dos volados el porton blindado se hunde y empieza el Centro de Datos. El orden es libre.
    public partial class OperacionDirector
    {
        public const int TotalDePuestos = 2;
        public const int ContraatacantesPrimera = 4, ContraatacantesSegunda = 6;

        int plantadas;                                       // cargas colocadas hasta ahora (la 1.a manda 4 enemigos, la 2.a manda 6 y una camioneta)
        readonly List<OperacionAuto> camionetasDePuestos = new List<OperacionAuto>();
        public int ContraatacantesActivados { get; private set; }
        public int CamionetasDePuestosCreadas { get; private set; }

        void SuscribirPuestos()
        {
            if (puestos == null) return;
            foreach (var p in puestos)
            {
                if (p == null || p.carga == null) continue;
                p.carga.AlPlantar -= AlPlantarCarga; p.carga.AlPlantar += AlPlantarCarga;
                p.carga.AlExplotar -= AlExplotarCarga; p.carga.AlExplotar += AlExplotarCarga;
                p.carga.Habilitado = true;
            }
        }

        void DesuscribirPuestos()
        {
            if (puestos == null) return;
            foreach (var p in puestos)
            {
                if (p == null || p.carga == null) continue;
                p.carga.AlPlantar -= AlPlantarCarga;
                p.carga.AlExplotar -= AlExplotarCarga;
            }
        }

        int IndiceDeCarga(CargaExplosiva c)
        {
            if (puestos != null) for (int i = 0; i < puestos.Length; i++) if (puestos[i] != null && puestos[i].carga == c) return i;
            return -1;
        }

        public int ContarVolados()
        {
            int n = 0;
            if (puestos != null) foreach (var p in puestos) if (p != null && p.carga != null && p.carga.EstaVolada) n++;
            return n;
        }

        // Los 12 guardias de los puestos a cubierto: 5 por bunker + 2 de la trinchera. Los 3 del corredor se atrincheran aparte (no hace falta matarlos).
        public List<Soldier> GuardiasDeLosPuestos()
        {
            var l = new List<Soldier>();
            if (puestos != null) foreach (var p in puestos) if (p != null && p.guardias != null) l.AddRange(p.guardias);
            if (soldadosDeTrinchera != null) l.AddRange(soldadosDeTrinchera);
            l.RemoveAll(s => s == null);
            return l;
        }

        List<Soldier> GuardiasDeLaMuralla()
        {
            var l = GuardiasDeLosPuestos();
            if (soldadosDelCorredor != null) foreach (var s in soldadosDelCorredor) if (s != null) l.Add(s);
            return l;
        }

        // La baliza marca el punto de carga que sigue sin colocar mas cercano a la escuadra.
        void BalizaDelPuesto()
        {
            PuestoDeVoladura mejor = null; float md = float.MaxValue;
            var yo = Poseido();
            var desde = yo != null ? yo.transform.position : transform.position;
            if (puestos != null)
                foreach (var p in puestos)
                {
                    if (p == null || p.carga == null || p.carga.EstaPlantada) continue;
                    float d = Plano(desde, p.carga.transform.position).sqrMagnitude;
                    if (d < md) { md = d; mejor = p; }
                }
            if (mejor == null) { if (baliza != null) { baliza.Quitar(); baliza = null; } return; }
            PonerBaliza(mejor.carga.Titulo, new Color(1f, 0.82f, 0.3f), mejor.carga.transform.position, null, 2.6f);
        }

        // ---- 2. puestos ----
        float proximaBaliza;

        void TickPuestos()
        {
            int volados = ContarVolados();
            PuestoActual = volados;
            // Con el contexto cambiante (se coloca, se vuela, la escuadra se mueve) la baliza se refresca cada segundo.
            if (Time.time >= proximaBaliza) { proximaBaliza = Time.time + 1f; BalizaDelPuesto(); }

            float prog = 0f;
            string det = "";
            float menorMecha = float.MaxValue; string ladoMecha = null;
            for (int i = 0; i < puestos.Length; i++)
            {
                var p = puestos[i];
                var c = p.carga;
                if (c == null) continue;
                float f = c.EstaVolada ? 1f : c.Situacion == CargaExplosiva.Estado.Ardiendo ? 0.5f + 0.5f * (1f - Mathf.Clamp01(c.MechaRestante / c.segundosDeMecha)) : 0.5f * c.Progreso01;
                prog += f / puestos.Length;
                det += (i > 0 ? " · " : "") + $"Puesto {Capital(p.lado)}: {c.TextoDeEstado}";
                if (c.Situacion == CargaExplosiva.Estado.Ardiendo && c.MechaRestante < menorMecha) { menorMecha = c.MechaRestante; ladoMecha = p.lado; }
            }
            det += " · solo el ASALTO coloca la carga [E]";
            if (hud != null)
            {
                hud.Objetivo(TituloObjetivo(2, "VOLAR LOS PUESTOS"), det, prog, new Color(1f, 0.82f, 0.3f));
                if (ladoMecha != null)
                {
                    bool urgente = menorMecha <= CargaExplosiva.SegundosDeAdvertencia;
                    hud.Timer("MECHA · PUESTO " + ladoMecha, Mathf.Max(0, Mathf.CeilToInt(menorMecha)), urgente ? new Color(1f, 0.3f, 0.2f) : new Color(1f, 0.85f, 0.4f));
                }
                else hud.OcultarTimer();
            }
        }

        // ---- Contraataque mientras arde la mecha ----
        void AlPlantarCarga(CargaExplosiva c)
        {
            int i = IndiceDeCarga(c);
            if (i < 0) return;
            var p = puestos[i];
            int orden = plantadas++;                         // 0 = la primera carga
            int cantidad = orden == 0 ? ContraatacantesPrimera : ContraatacantesSegunda;
            LanzarContraataque(p, cantidad, c.transform.position);
            if (orden >= 1) LanzarCamionetaDePuesto(p);
            BalizaDelPuesto();
            SesionLog.Evento($"{c.Titulo} colocada (orden {orden + 1}): contraataque de {cantidad}{(orden >= 1 ? " y una camioneta" : "")}");
        }

        void LanzarContraataque(PuestoDeVoladura p, int cantidad, Vector3 meta)
        {
            if (p.contraataque == null || p.contraataque.soldados == null) return;
            var elegidos = new List<Soldier>();
            foreach (var s in p.contraataque.soldados)
            {
                if (elegidos.Count >= cantidad) break;
                if (s == null || s.gameObject.activeSelf || s.Health == null || !s.Health.IsAlive) continue;
                elegidos.Add(s);
            }
            if (p.puerta != null) { var cuerpoPuerta = p.puerta.gameObject; cuerpoPuerta.SetActive(false); }   // la puerta lateral se abre (se apaga la hoja)
            var ola = new OleadaDeReserva
            {
                aviso = $"¡CONTRAATAQUE POR LA PUERTA LATERAL DEL PUESTO {p.lado}!\nAGUANTA HASTA QUE EXPLOTE LA CARGA",
                soldados = elegidos.ToArray(),
                destino = null,
            };
            Activar(ola, meta);
            ContraatacantesActivados += elegidos.Count;
            GameLog.Line($"[Operacion] contraataque del puesto {p.lado}: {elegidos.Count} enemigos");
        }

        void LanzarCamionetaDePuesto(PuestoDeVoladura p)
        {
            if (autoPlantilla == null || p.salidaCamioneta == null || p.paradaCamioneta == null) return;
            var hacia = p.paradaCamioneta.position - p.salidaCamioneta.position; hacia.y = 0f;
            var rot = hacia.sqrMagnitude > 0.01f ? Quaternion.LookRotation(hacia.normalized, Vector3.up) : Quaternion.identity;
            var salida = BuscarLugarParaCamioneta(new Vector3(p.salidaCamioneta.position.x, 0.6f, p.salidaCamioneta.position.z), rot);
            var parada = BuscarLugarParaCamioneta(new Vector3(p.paradaCamioneta.position.x, 0.6f, p.paradaCamioneta.position.z), rot);
            var go = InstanciarCamioneta(salida, rot);
            go.name = "Camioneta_Puesto_" + p.lado + "_" + (++CamionetasDePuestosCreadas);
            var a = go.GetComponent<OperacionAuto>();
            if (a != null) { a.ConfigurarAsalto(parada); camionetasDePuestos.Add(a); }
            AlertQueue.Push("¡UNA CAMIONETA DE ASALTO POR LA ZONA DE APROXIMACION!", AlertPriority.Alta, 3f);
            GameLog.Line($"[Operacion] camioneta de asalto del puesto {p.lado}");
        }

        public int CamionetasDePuestosVivas { get { int n = 0; foreach (var a in camionetasDePuestos) if (a != null && !a.Muerto) n++; return n; } }

        void LimpiarCamionetasDePuestos()
        {
            foreach (var a in camionetasDePuestos) if (a != null && !a.Muerto) Destroy(a.gameObject);
            camionetasDePuestos.Clear();
        }

        // ---- El bunker voló ----
        void AlExplotarCarga(CargaExplosiva c)
        {
            int i = IndiceDeCarga(c);
            if (i < 0) return;
            var p = puestos[i];
            p.volado = true;
            int volados = ContarVolados();
            PuestoActual = volados;
            AlertQueue.Push($"PUESTO {p.lado} VOLADO", AlertPriority.Alta, 2.5f);
            if (hud != null) hud.Aviso($"PUESTO {p.lado} VOLADO", 2.5f);
            SesionLog.Evento($"Puesto {p.lado} volado ({volados}/{puestos.Length})");
            if (volados < puestos.Length) { SonarObjetivoCumplido(); BalizaDelPuesto(); return; }
            // Los dos volados: la muralla cae (el porton blindado se hunde) y sigue el Centro de Datos.
            if (portonBlindado != null && portonBlindado.activeSelf) StartCoroutine(Hundir(portonBlindado));
            LimpiarCamionetasDePuestos();
            EntrarFase(FaseOperacion.CentroDeDatos);
            if (hud != null) hud.Aviso("¡LA MURALLA CAYÓ!\nAL CENTRO DE DATOS", 3.4f);
            StartCoroutine(AvisoDiferido("CENTRO DE DATOS\nUN SOLDADO INTERACTUA 30 s · LOS DEMAS DEFIENDEN", 3.6f));
        }

        IEnumerator AvisoDiferido(string texto, float segundos)
        {
            float t0 = Time.unscaledTime;
            while (Time.unscaledTime - t0 < segundos) yield return null;
            if (hud != null && Fase == FaseOperacion.CentroDeDatos) hud.Aviso(texto, 4f);
        }

        // ---- Saltos de fase y restauracion ----
        void DejarPuestoVolado(PuestoDeVoladura p)
        {
            if (p == null) return;
            p.volado = true;
            if (p.carga != null) p.carga.VolarYa();
            if (p.guardias != null) foreach (var g in p.guardias) if (g != null) g.gameObject.SetActive(false);
            if (p.contraataque != null && p.contraataque.soldados != null) foreach (var g in p.contraataque.soldados) if (g != null) g.gameObject.SetActive(false);
            if (p.puerta != null) p.puerta.gameObject.SetActive(false);
        }

        // Todo lo que queda de la muralla cuando se pasa a un objetivo posterior: porton abajo, trinchera y corredor sin guardias, sin camionetas.
        void ApagarMuralla()
        {
            if (portonBlindado != null) portonBlindado.SetActive(false);
            if (soldadosDeTrinchera != null) foreach (var s in soldadosDeTrinchera) if (s != null) s.gameObject.SetActive(false);
            if (soldadosDelCorredor != null) foreach (var s in soldadosDelCorredor) if (s != null) s.gameObject.SetActive(false);
            LimpiarCamionetasDePuestos();
        }

        // ---- HUD: los pasos de la operacion ----
        public static readonly string[] PasosCortos =
        {
            "INFILTRAR EL CUARTEL", "VOLAR LOS DOS PUESTOS", "CENTRO DE DATOS", "EL BLINDADO", "LA CIUDAD", "EXTRACCIÓN",
        };

        // Lista compacta bajo el objetivo: ✓ hechos, ▶ el actual, • pendientes. [V] la pliega a una linea. Se llama cada frame (el HUD no reconstruye si no cambio nada).
        void ActualizarPasos()
        {
            if (hud == null || CinematicaDeOperacion.IntroActiva) return;
            if (Fase == FaseOperacion.Victoria || Fase == FaseOperacion.Derrota) { hud.Pasos(null, 0); return; }
            hud.Pasos(PasosCortos, Indice(Fase));
        }

        // La cinematica inicial termino (o se salto): el cartel de arranque que la cinematica tapaba.
        public void AlTerminarIntro()
        {
            if (Fase == FaseOperacion.Infiltrar) Aviso("OPERACION CUARTEL\nINFILTRATE Y ELIMINA A TODOS");
            ProgramarAutoguardado("fin cinemática de apertura", 0.6f);   // P10 (#120)
        }
    }
}
