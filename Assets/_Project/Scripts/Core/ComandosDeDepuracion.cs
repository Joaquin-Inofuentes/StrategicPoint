using UnityEngine;
using SP.Actors;
using SP.Combat;

namespace SP.Core
{
    // Ronda 11 (punto 15): comandos de prueba para reproducir a mano el caso "muere el soldado que manejo" y ver si viene el medico.
    // Teclas: [F9] matar al soldado poseido, [F10] revivir a los caidos de la escuadra. Tambien se llaman desde el CLI de Unity:
    //   SP.Core.ComandosDeDepuracion.Matar();   // el poseido
    //   SP.Core.ComandosDeDepuracion.Matar(id); // un soldado concreto
    public static class ComandosDeDepuracion
    {
        // Quita TODA la vida por el mismo camino que el combate (TakeDamage), asi se publican los mismos eventos que una baja real.
        public static string Matar(int id = -1)
        {
            Soldier s = id >= 0 ? ActorRegistry.FindById(id) : OrderService_Poseido();
            if (s == null || s.Health == null) return "sin soldado";
            if (!s.Health.IsAlive) return s.DisplayName + " ya estaba caido";
            bool dios = ModoDios.Activo;
            if (dios) ModoDios.Poner(false);          // el modo dios anula el dano: para matar hay que apagarlo un instante
            s.Health.TakeDamage(s.Health.Current + s.Health.MaxHealth, s.Id);
            if (dios) ModoDios.Poner(true);
            GameLog.Line("[COMANDO] matar " + s.DisplayName);
            return "mate a " + s.DisplayName;
        }

        public static string Revivir()
        {
            int n = 0;
            var todos = ActorRegistry.All;
            for (int i = 0; i < todos.Count; i++)
            {
                var s = todos[i];
                if (s == null || s.Team != TeamId.Player || s.Role == RoleType.Civilian || s.Health == null || s.Health.IsAlive) continue;
                s.Health.Initialize(s.Id, s.Health.MaxHealth);
                s.Motor.ResetMotionState();
                s.SetBodyVisible(true);
                n++;
            }
            GameLog.Line("[COMANDO] revivir " + n);
            return "revivi " + n;
        }

        // En RTS no hay soldado manejado a mano: se cae de vuelta al primero vivo de la escuadra.
        static Soldier OrderService_Poseido()
        {
            var m = SP.Player.OrderService.ManejadoAMano;
            if (m != null) return m;
            var todos = ActorRegistry.All;
            for (int i = 0; i < todos.Count; i++)
                if (todos[i] != null && todos[i].Team == TeamId.Player && todos[i].Role != RoleType.Civilian && todos[i].Health != null && todos[i].Health.IsAlive) return todos[i];
            return null;
        }
    }
}
