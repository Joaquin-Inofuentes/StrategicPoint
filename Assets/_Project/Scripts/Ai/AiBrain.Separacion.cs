using System;
using System.Collections.Generic;
using UnityEngine;
using SP.Actors;
using SP.Core;

namespace SP.Ai
{
    // Bug #086 ("los enemigos se solapan"): el NavMeshAgent solo planifica (sin evitacion, ver SyncAgentSettings) y NavService.BlocksMovement
    // deja a los soldados atravesarse, asi que todos los que convergen al mismo blanco terminaban uno encima del otro (medido en Resistir:
    // hasta 13 pares a menos de 0,7 m y un minimo de 0,00 m). Ahora cada soldado recibe un empuje lateral desde los del MISMO bando que
    // tiene a menos de RadioDeSeparacion, proporcional a cuanto se pisan, y lo aplica por Motor.Move (o sea por el Deslizador: no atraviesa
    // paredes). No se aplica a retenidos (curando/curados), ni a los montados o poseidos.
    public partial class AiBrain
    {
        public const float RadioDeSeparacion = 1.5f;
        public const float RadioDeSeparacionEnCobertura = 0.9f;   // en su cobertura se separa menos (no se lo saca del punto), pero tampoco se queda apilado
        public const float PesoDeSeparacion = 1.3f;
        public static bool SeparacionActiva = true;

        static readonly List<Soldier> vecinosDeSeparacion = new List<Soldier>(8);
        Func<Soldier, bool> filtroDeSeparacion;
        public int EmpujesDeSeparacion { get; private set; }
        float tUltimaSeparacion = -99f;   // la logica de cobertura no lo tironea de vuelta a su punto mientras se esta separando de un companero
        bool SeparandoseDeUnCompanero => Time.time - tUltimaSeparacion < 0.25f;

        void TickSeparacion(float dt)
        {
            if (!SeparacionActiva || self == null || self.Motor == null || Pasivo || MontadoEnVehiculo || IsPossessedByPlayer) return;
            var m = self.Motor;
            if (m.Retenido || m.Vaulting || m.IsJumping || m.Atado) return;

            if (filtroDeSeparacion == null)
                filtroDeSeparacion = s => s != self && s.Team == self.Team && s.Health != null && s.Health.IsAlive && s.gameObject.activeInHierarchy
                                          && !(s.Brain != null && s.Brain.MontadoEnVehiculo);
            var p = self.transform.position;
            float radio = enCobertura ? RadioDeSeparacionEnCobertura : RadioDeSeparacion;
            SpatialGrid.QueryInRange(p, radio, vecinosDeSeparacion, filtroDeSeparacion);
            if (vecinosDeSeparacion.Count == 0) return;

            Vector3 empuje = Vector3.zero;
            for (int i = 0; i < vecinosDeSeparacion.Count; i++)
            {
                var d = p - vecinosDeSeparacion[i].transform.position; d.y = 0f;
                float dist = d.magnitude;
                // Exactamente encima: una direccion fija por Id (no azarosa, asi dos corridas dan lo mismo).
                Vector3 dir = dist > 0.01f ? d / dist : new Vector3(Mathf.Cos(self.Id * 2.4f), 0f, Mathf.Sin(self.Id * 2.4f));
                empuje += dir * ((radio - Mathf.Min(dist, radio)) / radio * PesoDeSeparacion);
            }
            if (empuje.sqrMagnitude < 0.0001f) return;
            if (empuje.sqrMagnitude > 1f) empuje.Normalize();
            m.Move(empuje, dt);
            EmpujesDeSeparacion++;
            tUltimaSeparacion = Time.time;
        }
    }
}
