using System.Collections.Generic;
using UnityEngine;
using SP.Actors;
using SP.Combat;
using SP.Core;
using SP.Vehicles;

namespace SP.Player
{
    // Selección múltiple en vista RTS.
    public class SelectionController : MonoBehaviour
    {
        readonly List<Soldier> selected = new List<Soldier>();
        public IReadOnlyList<Soldier> Selected => selected;

        // El vehículo es seleccionable, pero por separado de los soldados
        // (mutuamente excluyente, como en cualquier RTS: no tiene sentido
        // arrastrar una selección mixta de tropas + tanque).
        public Vehicle SelectedVehicle { get; private set; }

        public void SelectSingle(Soldier s)
        {
            SelectedVehicle = null;
            selected.Clear();
            selected.Add(s);
            Publish();
        }

        public void AddToSelection(Soldier s)
        {
            SelectedVehicle = null;
            if (!selected.Contains(s)) selected.Add(s);
            Publish();
        }

        public void SelectVehicle(Vehicle v)
        {
            selected.Clear();
            SelectedVehicle = v;
            Publish();
        }

        // Para seleccionar a todos hoy hay que arrastrar un cuadro que los
        // abarque, lo que obliga a panear la camara hasta encuadrarlos.
        // Es el comando mas repetido de cualquier RTS y era el mas
        // incomodo del juego.
        public void SelectAll(IEnumerable<Soldier> squad)
        {
            SelectedVehicle = null;
            selected.Clear();
            foreach (var s in squad)
                if (s != null && s.Health != null && s.Health.IsAlive) selected.Add(s);
            Publish();
        }

        // ITEM 220 -- Seleccionar solo a los heridos.
        // Devuelve bool (y no void) porque la UI necesita distinguir "no
        // hay nadie herido" de "el filtro funciono": si el filtro no deja a
        // nadie, la seleccion previa se conserva intacta -- perderla en
        // silencio seria peor que no hacer nada -- y el HUD tiene que poder
        // avisar "nadie herido" en vez de dejar al jugador creyendo que el
        // comando no llego.
        public bool SelectWoundedOnly(float threshold01 = 0.5f)
        {
            var universe = new List<Soldier>();
            if (selected.Count > 0)
            {
                // Se copia la seleccion actual porque 'selected' recien se
                // reescribe al final, y solo si el filtro dio resultados.
                universe.AddRange(selected);
            }
            else
            {
                // Sin seleccion previa el universo es toda la escuadra
                // propia viva. Esta clase no tiene la lista de escuadra (la
                // tiene PlayerInputDriver.Squad), asi que se consulta el
                // registro central.
                CollectLivingAllies(universe);
            }

            var wounded = new List<Soldier>();
            foreach (var s in universe)
            {
                // Health puede ser null en un soldado a medio construir; y
                // los muertos los descarta IsWounded.
                if (s == null || s.Health == null) continue;
                if (IsWounded(s.Health.Current, s.Health.MaxHealth, threshold01)) wounded.Add(s);
            }

            // Cero heridos: no se toca nada y no se publica ningun evento,
            // asi el HUD tampoco parpadea. El false es la senal de aviso.
            if (wounded.Count == 0) return false;

            SelectedVehicle = null;
            selected.Clear();
            selected.AddRange(wounded);
            Publish();
            return true;
        }

        // Pura y estatica: sin escena y sin componentes, para poder
        // verificar los casos borde en un test headless.
        public static bool IsWounded(int current, int max, float threshold01)
        {
            // Un muerto nunca cuenta como herido.
            if (current <= 0) return false;
            // max <= 0 es un Health mal inicializado: dividir ahi seria una
            // division por cero. Se responde false en vez de inventar una
            // fraccion de vida.
            if (max <= 0) return false;
            return (float)current / max < threshold01;
        }

        // ITEM 214 -- Seleccion por tipo entre los visibles en pantalla.
        // "Tipo" = Soldier.Role (SP.Combat.RoleType: Assault/Sniper/Medic/
        // Enemy), que SI existe en el proyecto. Se eligio por sobre
        // WeaponHolder.CurrentWeaponKind porque el rol es fijo durante la
        // partida mientras que el arma cambia al levantar un WeaponPickup:
        // agrupar por arma haria que la misma tecla seleccionara un grupo
        // distinto despues de cada canje de armas.
        public void SelectSameTypeOnScreen(Soldier reference, Camera cam)
        {
            if (reference == null) return;
            // La camara la pasa el llamador; el fallback evita quedar sin
            // hacer nada si todavia no tiene la referencia del rig.
            if (cam == null) cam = Camera.main;
            if (cam == null) return;

            var role = reference.Role;

            // Mismo criterio de visibilidad que SelectAlliesInScreenRect
            // (PlayerInputDriver): aliado vivo y activo en la jerarquia,
            // z >= 0 para descartar lo que quedo detras de la camara, y el
            // punto proyectado dentro del rectangulo. Aca el rectangulo es
            // la pantalla entera. Se usa pixelRect y no Screen.width/height
            // porque WorldToScreenPoint devuelve pixeles del viewport de
            // esta camara, que puede no ocupar toda la ventana.
            var rect = cam.pixelRect;
            var matches = new List<Soldier>();
            var all = ActorRegistry.All;
            for (int i = 0; i < all.Count; i++)
            {
                var s = all[i];
                if (s == null || s.Team != TeamId.Player) continue;
                if (s.Health == null || !s.Health.IsAlive) continue;
                if (!s.gameObject.activeInHierarchy) continue;
                if (s.Role != role) continue;

                var sp = cam.WorldToScreenPoint(s.transform.position);
                if (sp.z < 0f) continue;
                if (sp.x < rect.xMin || sp.x > rect.xMax || sp.y < rect.yMin || sp.y > rect.yMax) continue;

                matches.Add(s);
            }

            // Nadie visible de ese tipo: se deja la seleccion como estaba.
            // El comando se dispara con un soldado de referencia ya
            // elegido, y vaciar la seleccion solo le sacaria al jugador lo
            // que ya tenia.
            if (matches.Count == 0) return;

            SelectedVehicle = null;
            selected.Clear();
            selected.AddRange(matches);
            Publish();
        }

        // Escuadra propia viva, leida del registro central en una sola
        // pasada (nada de un Find por soldado: la escena escala a 50+).
        // EnsureAllRegistered da de alta a los que arrancaron con el
        // GameObject desactivado -- los que viajan dentro del vehiculo --
        // que siguen vivos, siguen siendo escuadra y, si estan heridos,
        // tienen que poder seleccionarse. Es un barrido por invocacion,
        // igual que ActorRegistry.CountAlive.
        static void CollectLivingAllies(List<Soldier> into)
        {
            ActorRegistry.EnsureAllRegistered();
            var all = ActorRegistry.All;
            for (int i = 0; i < all.Count; i++)
            {
                var s = all[i];
                if (s == null || s.Team != TeamId.Player) continue;
                if (s.Health == null || !s.Health.IsAlive) continue;
                into.Add(s);
            }
        }

        public void Clear()
        {
            selected.Clear();
            SelectedVehicle = null;
            Publish();
        }

        void Publish()
        {
            var ids = new List<int>();
            foreach (var s in selected) ids.Add(s.Id);
            EventBus.Instance.Publish(new SelectionChangedEvent(ids));
        }
    }
}
