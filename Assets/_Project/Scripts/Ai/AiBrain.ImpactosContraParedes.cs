using System;
using System.Collections.Generic;
using UnityEngine;
using SP.Core;
using SP.Actors;
using SP.Vehicles;

namespace SP.Ai
{
    // AiBrain (parte): "los enemigos disparan a un edificio" (bug #193). Si la linea de tiro dice que hay vista pero las balas de este soldado se
    // estrellan una y otra vez contra una pared (un blanco tapado que el raycast no ve: borde de azotea, helicoptero detras de un techo, colisionador
    // que no cuenta como pared), deja de gastar municion contra el edificio: descarta ese blanco unos segundos y se reposiciona.
    public partial class AiBrain
    {
        public const int GolpesParaDescartar = 3;
        public const float VentanaDeGolpes = 3f, SegundosDeDescarteContraPared = 8f;
        const float AlturaMinimaDeParedSobreElPiso = 0.45f, FraccionDelTiroQueEsPared = 0.8f;

        static IDisposable subImpactosContraParedes;
        int golpesContraPared;
        float inicioDeLaVentanaDeGolpes;
        readonly Dictionary<Vehicle, float> vehiculosDescartados = new Dictionary<Vehicle, float>();
        public int DescartesPorDispararAParedes { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void IniciarImpactosContraParedes()
        {
            subImpactosContraParedes?.Dispose();
            subImpactosContraParedes = EventBus.Instance.Subscribe<EnvironmentHitEvent>(AlImpactarEnElEntorno);
        }

        static void AlImpactarEnElEntorno(EnvironmentHitEvent e)
        {
            if (e.Kind == EnvironmentHitKind.Vehicle) return;
            var tirador = ActorRegistry.FindById(e.ShooterId);
            if (tirador == null || tirador.Brain == null) return;
            tirador.Brain.RegistrarImpactoContraElEntorno(e.Kind, e.Point);
        }

        bool VehiculoDescartado(Vehicle v)
        {
            if (v == null || vehiculosDescartados.Count == 0) return false;
            if (!vehiculosDescartados.TryGetValue(v, out var hasta)) return false;
            if (Time.time < hasta) return true;
            vehiculosDescartados.Remove(v);
            return false;
        }

        void RegistrarImpactoContraElEntorno(EnvironmentHitKind clase, Vector3 punto)
        {
            if (self == null || Pasivo || !self.Health.IsAlive || State != AiState.Attack && vehiculoAmenaza == null) return;

            // El blanco: el soldado fijado o, si no hay, el vehiculo al que le dispara.
            Vector3 posBlanco;
            if (target != null) posBlanco = target.transform.position;
            else if (vehiculoAmenaza != null) posBlanco = vehiculoAmenaza.transform.position;
            else return;

            var origen = self.transform.position;
            float distBlanco = Vector3.Distance(origen, posBlanco);
            float distImpacto = Vector3.Distance(origen, punto);
            // Solo cuenta lo que se estrella EN EL MEDIO (el blanco queda detras): una bala que erra y pega en la pared que esta detras del blanco no.
            if (distImpacto >= distBlanco * FraccionDelTiroQueEsPared) return;
            // Pared y no piso: obstaculo marcado, o un impacto bien por encima de donde pisa el tirador (el pivote esta ~0,8 m sobre el piso).
            bool pared = clase == EnvironmentHitKind.Obstacle || punto.y > origen.y - 0.8f + AlturaMinimaDeParedSobreElPiso;
            if (!pared) return;

            if (golpesContraPared == 0 || Time.time - inicioDeLaVentanaDeGolpes > VentanaDeGolpes) { golpesContraPared = 0; inicioDeLaVentanaDeGolpes = Time.time; }
            golpesContraPared++;
            if (golpesContraPared < GolpesParaDescartar) return;

            golpesContraPared = 0;
            DescartesPorDispararAParedes++;
            if (target != null)
            {
                ignorados[target.Id] = Time.time + SegundosDeDescarteContraPared;
                GameLog.Line($"{self.DisplayName} deja de dispararle a una pared: descarta a {target.DisplayName} {SegundosDeDescarteContraPared:0} s");
                target = null;
                sensedTarget = null;
                forceSense = true;
            }
            else if (vehiculoAmenaza != null)
            {
                vehiculosDescartados[vehiculoAmenaza] = Time.time + SegundosDeDescarteContraPared;
                vehiculoAmenaza = null;
                RestaurarArmaTrasCohete();
            }
        }
    }
}
