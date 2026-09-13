using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SP.Player
{
    // Item 208: remapeo de teclas.
    //
    // Las ~35 lecturas de teclado de PlayerInputDriver estaban hardcodeadas
    // (kb.rKey, kb.eKey, ...), asi que la unica forma de cambiar un control
    // era editar codigo. Esta capa mete un nivel de indireccion: el driver
    // pregunta por una ACCION y esta clase resuelve que tecla fisica le
    // corresponde hoy.
    //
    // Por que NO se uso un InputActionAsset de Unity: en este proyecto toda
    // la escena se construye por codigo desde HeadlessTestRunner, y un
    // InputActionAsset es un asset del Editor. Meterlo obligaria a romper
    // esa invariante para una sola funcionalidad. Un diccionario con
    // respaldo en PlayerPrefs da lo mismo sin ese costo.
    //
    // Es estatico a proposito: un campo de componente no sobrevive el
    // domain reload (bug recurrente del proyecto), y ademas lo consultan
    // tanto el driver como la UI de configuracion.
    public static class KeyBindings
    {
        // Los ids son strings estables y NO el nombre de la tecla: si
        // fueran la tecla, remapear romperia la clave de PlayerPrefs.
        public const string Disparar = "disparar";
        public const string Recargar = "recargar";
        public const string Interactuar = "interactuar";
        public const string SubirBajarVehiculo = "vehiculo_entrar_salir";
        public const string Poseer = "poseer";
        public const string CiclarPosesion = "ciclar_posesion";
        public const string CiclarPosesionAtras = "ciclar_posesion_atras";
        public const string PoseerMasCercano = "poseer_cercano";
        public const string AlternarVista = "alternar_vista";
        public const string Controles = "controles";
        public const string Frenar = "frenar";
        // Era "camara_vehiculo" (V alternaba primera/tercera persona en el
        // vehiculo) -- ese toggle ya no existe, la vista de vehiculo es
        // siempre 3ra persona. V queda libre y pasa a ser el golpe de
        // cuchillo, el mismo id de PlayerPrefs se reutiliza a proposito
        // para no dejar una tecla remapeada por el jugador apuntando a
        // una accion que ya no existe.
        public const string AtaqueCuchillo = "camara_vehiculo";
        public const string Recentrar = "recentrar";
        public const string CancelarOrden = "cancelar_orden";
        public const string Reagrupar = "reagrupar";
        public const string Retirada = "retirada";
        public const string CiclarFormacion = "ciclar_formacion";
        public const string SeleccionarHeridos = "seleccionar_heridos";
        public const string SeleccionarMismoTipo = "seleccionar_mismo_tipo";
        public const string MinimapAgrandar = "minimap_agrandar";
        // N ya es SeleccionarMismoTipo: L queda libre y evita que una
        // sola tecla dispare dos acciones sin relacion (elegir unidades
        // del mismo tipo Y ciclar el minimapa) en el mismo frame.
        public const string MinimapCiclarTamano = "minimap_ciclar_tamano";

        // Valores de fabrica. Son EXACTAMENTE los que el juego ya usaba, de
        // modo que sin tocar nada el remapeo es invisible.
        static readonly Dictionary<string, Key> defaults = new Dictionary<string, Key>
        {
            { Recargar, Key.R },
            { Interactuar, Key.E },
            // BUG REAL: SubirBajarVehiculo y CancelarOrden (mas abajo)
            // compartian Key.X de fabrica -- dos acciones remapeables
            // distintas atadas a la misma tecla fisica, exactamente lo que
            // los comentarios de mas abajo (N/L, T) dicen evitar a
            // proposito. No se notaba porque hoy se leen en ramas de
            // Update() mutuamente excluyentes (FPS/vehiculo vs RTS), pero
            // KeyBindings.Set/DisplayName siguen mostrando "X" como si
            // estuviera libre para remapear cualquiera de las dos sin
            // pisar a la otra. O esta libre (no tiene default asignado).
            { SubirBajarVehiculo, Key.O },
            { Poseer, Key.F },
            { CiclarPosesion, Key.Q },
            { CiclarPosesionAtras, Key.Z },
            { PoseerMasCercano, Key.C },
            { AlternarVista, Key.Tab },
            { Controles, Key.H },
            // Pedido explicito: G -> T. No pisa el [T] de "mandar la
            // camioneta ahi" del artillero (PlayerInputDriver, tecla
            // hardcodeada aparte, kb.tKey) porque son asientos excluyentes
            // -- Frenar solo se lee en Driver, ese T solo en Gunner.
            { Frenar, Key.T },
            { AtaqueCuchillo, Key.V },
            { Recentrar, Key.Space },
            { CancelarOrden, Key.X },
            { Reagrupar, Key.Y },   // Z ya es ciclar-posesion-atras y las dos se leen en RTS
            { Retirada, Key.B },
            { CiclarFormacion, Key.K },
            { SeleccionarHeridos, Key.J },
            { SeleccionarMismoTipo, Key.N },
            { MinimapAgrandar, Key.M },
            { MinimapCiclarTamano, Key.L },
        };

        static Dictionary<string, Key> current;

        static void EnsureLoaded()
        {
            if (current != null) return;
            current = new Dictionary<string, Key>(defaults);
            foreach (var id in new List<string>(defaults.Keys))
            {
                int saved = PlayerPrefs.GetInt(PrefKey(id), -1);
                if (saved >= 0 && System.Enum.IsDefined(typeof(Key), saved)) current[id] = (Key)saved;
            }
        }

        static string PrefKey(string id) => "sp_bind_" + id;

        public static Key Get(string actionId)
        {
            EnsureLoaded();
            return current.TryGetValue(actionId, out var k) ? k : Key.None;
        }

        public static string Set(string actionId, Key key)
        {
            EnsureLoaded();
            string freedAction = null;
            if (key != Key.None)
            {
                foreach (var other in new List<string>(current.Keys))
                {
                    if (other == actionId) continue;
                    if (current[other] == key)
                    {
                        current[other] = Key.None;
                        PlayerPrefs.SetInt(PrefKey(other), (int)Key.None);
                        freedAction = other;
                        break; 
                    }
                }
            }
            current[actionId] = key;
            PlayerPrefs.SetInt(PrefKey(actionId), (int)key);
            PlayerPrefs.Save();
            return freedAction;
        }

        public static void ResetToDefaults()
        {
            EnsureLoaded();
            foreach (var kv in defaults)
            {
                current[kv.Key] = kv.Value;
                PlayerPrefs.DeleteKey(PrefKey(kv.Key));
            }
            PlayerPrefs.Save();
        }

        // Solo para tests: fuerza releer PlayerPrefs en la proxima consulta.
        public static void InvalidateCache() => current = null;

        public static IEnumerable<string> AllActions => defaults.Keys;

        // --- Lectura ---
        // El driver llama esto en vez de kb.<x>Key. Si el teclado no existe
        // (batch mode) o la accion no esta mapeada, devuelve false en vez de
        // tirar.
        public static bool WasPressed(string actionId)
        {
            var kb = Keyboard.current;
            if (kb == null) return false;
            var key = Get(actionId);
            if (key == Key.None) return false;
            return kb[key].wasPressedThisFrame;
        }

        public static bool IsPressed(string actionId)
        {
            var kb = Keyboard.current;
            if (kb == null) return false;
            var key = Get(actionId);
            if (key == Key.None) return false;
            return kb[key].isPressed;
        }

        // Item 207: mantener contra tocar. Devuelve true una sola vez, al
        // soltar, si la tecla estuvo apretada MENOS de holdSeconds. El
        // "mantener" lo consulta el llamador con IsPressed. Se resuelve al
        // SOLTAR y no al apretar, que es lo unico que permite distinguir
        // los dos gestos sin agregar latencia al camino de "mantener".
        static readonly Dictionary<string, float> pressStart = new Dictionary<string, float>();

        public static bool WasTapped(string actionId, float holdSeconds = 0.3f)
        {
            var kb = Keyboard.current;
            if (kb == null) return false;
            var key = Get(actionId);
            if (key == Key.None) return false;

            var control = kb[key];
            if (control.wasPressedThisFrame) pressStart[actionId] = Time.unscaledTime;

            if (control.wasReleasedThisFrame && pressStart.TryGetValue(actionId, out float t0))
            {
                pressStart.Remove(actionId);
                return Time.unscaledTime - t0 < holdSeconds;
            }
            return false;
        }

        public static bool IsHeld(string actionId, float holdSeconds = 0.3f)
        {
            // Registra el inicio de la pulsacion por su cuenta en vez de
            // confiar en que alguien mas haya llamado a WasTapped este
            // frame: pressStart solo se llenaba alla, asi que un llamador
            // que use unicamente IsHeld nunca veia nada. Con el registro
            // aca, cada gesto funciona pidiendo solo lo que le importa.
            var kb = Keyboard.current;
            if (kb == null) return false;
            var key = Get(actionId);
            if (key == Key.None) return false;
            var control = kb[key];
            if (control.wasPressedThisFrame) pressStart[actionId] = Time.unscaledTime;
            if (!control.isPressed) return false;
            return pressStart.TryGetValue(actionId, out float t0) && Time.unscaledTime - t0 >= holdSeconds;
        }

        // A4: cuanto lleva sostenida la tecla ahora mismo, en segundos (0 si
        // no esta apretada). IsHeld solo da el umbral cumplido o no -- esto
        // es lo que necesita un progreso 0-1 (revivir a un caido).
        public static float HeldSeconds(string actionId)
        {
            var kb = Keyboard.current;
            if (kb == null) return 0f;
            var key = Get(actionId);
            if (key == Key.None) return 0f;
            var control = kb[key];
            if (control.wasPressedThisFrame) pressStart[actionId] = Time.unscaledTime;
            if (!control.isPressed) return 0f;
            return pressStart.TryGetValue(actionId, out float t0) ? Time.unscaledTime - t0 : 0f;
        }

        // Solo para tests: simula que la tecla se apreto hace 'segundos'.
        // Sin esto no hay forma de probar el gesto de mantener en la suite,
        // que corre sin teclado real (Keyboard.current es null).
        public static void ForzarInicioDePulsacion(string actionId, float segundos)
        {
            pressStart[actionId] = Time.unscaledTime - segundos;
        }

        public static bool HayPulsacionRegistrada(string actionId, float holdSeconds)
        {
            return pressStart.TryGetValue(actionId, out float t0) && Time.unscaledTime - t0 >= holdSeconds;
        }

        // Nombre legible para la UI de configuracion y para ControlsTable.
        public static string DisplayName(string actionId)
        {
            var key = Get(actionId);
            return key == Key.None ? "-" : key.ToString().ToUpperInvariant();
        }
    }
}
