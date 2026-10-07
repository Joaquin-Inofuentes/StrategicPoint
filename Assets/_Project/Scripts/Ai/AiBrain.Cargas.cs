using UnityEngine;
using UnityEngine.AI;
using SP.Combat;
using SP.Operacion;

namespace SP.Ai
{
    // Bug #106: con una carga explosiva a punto de detonar (CargaExplosiva con la mecha encendida) los aliados de la IA no reaccionaban.
    // Ahora, si un aliado esta dentro de radio + MargenDeSeguridad y quedan menos de SegundosParaAlejarse, deja lo que hace, se va corriendo
    // a un punto del NavMesh fuera del radio (del lado del jugador si se puede) y espera ahi hasta que estalle. Las ordenes (seguir, ir a
    // un punto, atacar) NO se tocan: al estallar, el flujo normal del cerebro sigue con la que tenia.
    public partial class AiBrain
    {
        CargaExplosiva cargaEvadida;
        Vector3 puntoDeEvasion;
        bool tienePuntoDeEvasion;
        public bool AlejandoseDeUnaCarga => cargaEvadida != null;
        public static int AlejamientosDeCarga { get; private set; }
        public static bool AlejarseDeCargasActivo = true;   // solo lo apagan los checks para reproducir el bug original

        // Devuelve true si este tick lo consumio (se esta alejando o esperando fuera del radio).
        bool AlejarseDeCargaArmada(float dt)
        {
            if (!AlejarseDeCargasActivo || self == null || self.Team != TeamId.Player) return false;
            var armadas = CargaExplosiva.Armadas;
            if (cargaEvadida != null && (cargaEvadida.Situacion != CargaExplosiva.Estado.Ardiendo || !CargaExplosiva.EstaArmada(cargaEvadida)))
                SoltarEvasionDeCarga();
            if (cargaEvadida == null)
            {
                for (int i = 0; i < armadas.Count; i++)
                {
                    var c = armadas[i];
                    if (c == null || c.Situacion != CargaExplosiva.Estado.Ardiendo || c.MechaRestante > CargaExplosiva.SegundosParaAlejarse) continue;
                    if (DistanciaPlana(c.Posicion) > c.RadioDePeligro + CargaExplosiva.MargenDeSeguridad) continue;
                    cargaEvadida = c;
                    tienePuntoDeEvasion = ElegirPuntoDeEvasion(c, out puntoDeEvasion);
                    AlejamientosDeCarga++;
                    ClearPath();
                    if (tienePuntoDeEvasion) PlanPathTo(puntoDeEvasion);
                    AvisoDeRadio("¡CARGA! ALEJENSE");
                    break;
                }
                if (cargaEvadida == null) return false;
            }
            // Ya sale del radio (con margen) o llego al punto: espera quieto hasta que estalle.
            float d = DistanciaPlana(cargaEvadida.Posicion);
            self.Motor.SetCrouching(false);
            if (tienePuntoDeEvasion && DistanciaPlana(puntoDeEvasion) > 1.2f && d < cargaEvadida.RadioDePeligro + CargaExplosiva.MargenDeSeguridad)
            {
                self.Motor.SetRunning(true);
                AdvanceTo(puntoDeEvasion, 1f, dt);
            }
            else if (!tienePuntoDeEvasion && d < cargaEvadida.RadioDePeligro + CargaExplosiva.MargenDeSeguridad)
            {
                // Sin NavMesh a mano: en linea recta hacia afuera.
                var lejos = self.transform.position - cargaEvadida.Posicion; lejos.y = 0f;
                if (lejos.sqrMagnitude < 0.01f) lejos = -self.transform.forward;
                self.Motor.SetRunning(true);
                self.Motor.Move(lejos.normalized, dt);
            }
            else self.Motor.SetRunning(false);
            return true;
        }

        void SoltarEvasionDeCarga()
        {
            cargaEvadida = null;
            tienePuntoDeEvasion = false;
            ClearPath();
            if (self != null && self.Motor != null) self.Motor.SetRunning(false);
        }

        float DistanciaPlana(Vector3 p)
        {
            var d = self.transform.position - p; d.y = 0f;
            return d.magnitude;
        }

        // Un punto caminable a radio + 4 m de la carga, del lado del jugador si se puede (si no, del lado donde ya esta el soldado).
        bool ElegirPuntoDeEvasion(CargaExplosiva c, out Vector3 punto)
        {
            punto = default;
            var yo = SP.Player.PlayerBrain.Activo != null ? SP.Player.PlayerBrain.Activo.Current : null;
            var baseDir = (yo != null ? yo.transform.position : self.transform.position) - c.Posicion; baseDir.y = 0f;
            if (baseDir.sqrMagnitude < 1f) baseDir = self.transform.position - c.Posicion; baseDir.y = 0f;
            if (baseDir.sqrMagnitude < 0.01f) baseDir = Vector3.back;
            baseDir.Normalize();
            float radio = c.RadioDePeligro + 4f;
            var camino = new NavMeshPath();
            float[] giros = { 0f, 35f, -35f, 70f, -70f, 110f, -110f, 180f };
            Vector3 mejor = default; float mejorLargo = float.MaxValue; bool hay = false;
            for (int i = 0; i < giros.Length; i++)
            {
                var objetivo = c.Posicion + Quaternion.Euler(0f, giros[i], 0f) * baseDir * radio;
                if (!NavMesh.SamplePosition(objetivo, out var h, 4f, NavMesh.AllAreas)) continue;
                var d = h.position - c.Posicion; d.y = 0f;
                if (d.magnitude < c.RadioDePeligro + 1.5f) continue;
                if (!NavMesh.CalculatePath(self.transform.position, h.position, NavMesh.AllAreas, camino) || camino.status != NavMeshPathStatus.PathComplete) continue;
                float largo = 0f;
                for (int k = 1; k < camino.corners.Length; k++) largo += Vector3.Distance(camino.corners[k - 1], camino.corners[k]);
                // Prefiere el lado del jugador (giros chicos): castiga cada giro de forma leve.
                float costo = largo + Mathf.Abs(giros[i]) * 0.08f;
                if (costo < mejorLargo) { mejorLargo = costo; mejor = h.position; hay = true; }
            }
            punto = mejor;
            return hay;
        }
    }
}
