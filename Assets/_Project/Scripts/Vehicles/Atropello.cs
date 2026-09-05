using UnityEngine;
using SP.Actors;
using SP.Combat;
using SP.Core;

namespace SP.Vehicles
{
    // Atropellar con el vehiculo en movimiento.
    //
    // KnockNearbyProps ya hacia este barrido para los objetos livianos
    // (bidones, cajones) desde que el vehiculo dejo de atravesar el
    // escenario. Lo que faltaba era que los soldados existieran para ese
    // barrido: un tanque a fondo pasaba por encima de un enemigo sin que
    // pasara absolutamente nada.
    //
    // Vive aparte de VehicleMotor a proposito: el motor es acelerar,
    // frenar y deslizar contra geometria, y no tiene por que saber de
    // equipos, vida ni cuerpos derribados.
    public static class Atropello
    {
        // Debajo de esto no cuenta como atropello. Empujar a alguien
        // maniobrando en el lugar no deberia matarlo, igual que apoyarse
        // contra un bidon no lo voltea.
        public const float VelocidadMinima = 3f;
        // 8 m/s dan 240 de daño, mas que la vida de cualquier soldado del
        // juego (180): a velocidad de marcha el atropello mata. A 3 m/s
        // son 90, duele y no mata.
        public const int DanioPorMetroPorSegundo = 30;
        // Margen sobre el propio casco. El soldado mide 0,90 de ancho, asi
        // que medio cuerpo mas un poco de holgura.
        public const float MargenSobreElCasco = 0.7f;

        // Cuanto se desplaza el cuerpo en la direccion del golpe.
        public const float EmpujeDelCuerpo = 0.9f;

        // Devuelve a cuantos atropello este paso.
        public static int Barrer(Transform vehiculo, Collider casco, float velocidad, Vehicle datos)
        {
            if (vehiculo == null) return 0;
            if (Mathf.Abs(velocidad) < VelocidadMinima) return 0;

            float alcance = MargenSobreElCasco;
            if (casco != null)
            {
                var e = casco.bounds.extents;
                alcance += Mathf.Max(e.x, e.z);
            }

            Vector3 direccion = vehiculo.forward * Mathf.Sign(velocidad);
            int danio = Mathf.RoundToInt(DanioPorMetroPorSegundo * Mathf.Abs(velocidad));
            var tripulacion = EquipoDeLaTripulacion(datos);
            int atropellados = 0;

            var todos = ActorRegistry.All;
            for (int i = 0; i < todos.Count; i++)
            {
                var s = todos[i];
                // Los que van adentro estan inactivos, asi que este filtro
                // ya los deja afuera sin preguntarle nada al vehiculo.
                if (s == null || !s.gameObject.activeInHierarchy) continue;
                if (s.Health == null || !s.Health.IsAlive) continue;
                if (tripulacion.HasValue && s.Team == tripulacion.Value) continue;

                var d = s.transform.position - vehiculo.position;
                d.y = 0f;
                if (d.sqrMagnitude > alcance * alcance) continue;

                int quien = datos != null && datos.Driver != null ? datos.Driver.Id : -1;
                s.Health.TakeDamage(danio, quien);
                GameLog.Line($"{s.DisplayName} fue atropellado a {Mathf.Abs(velocidad):0.0} m/s ({danio} de daño)");
                if (!s.Health.IsAlive) Derribar(s, direccion);
                atropellados++;
            }
            return atropellados;
        }

        // El equipo de quien va adentro. Sin nadie adentro devuelve null y
        // el vehiculo atropella a cualquiera: no hay a quien respetarle el
        // bando.
        public static TeamId? EquipoDeLaTripulacion(Vehicle datos)
        {
            if (datos == null) return null;
            if (datos.Driver != null) return datos.Driver.Team;
            var dentro = datos.Occupants;
            if (dentro != null && dentro.Count > 0) return dentro[0].Team;
            return null;
        }

        // El cuerpo queda tirado y desplazado en la direccion del golpe.
        // No se delega en la caida normal de CubeFxReactor: esa es una
        // corrutina (solo Play) y ademas es una caida hacia adelante
        // igual para todas las muertes -- un atropello tiene direccion.
        public static void Derribar(Soldier victima, Vector3 direccion)
        {
            if (victima == null) return;
            var plano = new Vector3(direccion.x, 0f, direccion.z);
            if (plano.sqrMagnitude < 0.0001f) plano = Vector3.forward;
            plano.Normalize();

            // Girar 90 grados alrededor del eje perpendicular al golpe deja
            // el cuerpo acostado apuntando hacia donde lo mando el
            // vehiculo, en vez de siempre boca abajo.
            var eje = Vector3.Cross(Vector3.up, plano);
            victima.transform.rotation = Quaternion.AngleAxis(90f, eje) * victima.transform.rotation;
            victima.transform.position += plano * EmpujeDelCuerpo;
        }
    }
}
