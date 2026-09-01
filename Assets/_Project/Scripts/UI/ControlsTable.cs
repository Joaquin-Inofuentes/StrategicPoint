using System;
using System.Collections.Generic;
using System.Text;

namespace SP.UI
{
    // Los seis contextos de entrada reales. Son flags porque un mismo
    // atajo puede leerse en varios (por ejemplo [TAB], que se procesa en
    // Update() antes de cualquier corte por asiento o por modo).
    [Flags]
    public enum ControlContext
    {
        FpsAPie = 1,
        Rts = 2,
        VehiculoConductor = 4,
        VehiculoArtillero = 8,
        VehiculoPasajero = 16,
        VehiculoRts = 32
    }

    // Una fila de la tabla. Inmutable: la tabla se arma una sola vez en el
    // inicializador estatico y nadie la puede mutar despues.
    public readonly struct ControlEntry
    {
        public readonly string Key;
        public readonly string Description;
        public readonly ControlContext Contexts;
        public readonly string ActionId;

        public ControlEntry(string key, string description, ControlContext contexts, string actionId = null)
        {
            Key = key;
            Description = description;
            Contexts = contexts;
            ActionId = actionId;
        }
    }

    // Unica fuente de verdad de los atajos del juego.
    //
    // Antes habia dos listas hardcodeadas y ya habian divergido: el texto
    // del panel de pausa (armado a mano en HeadlessTestRunner) y los cuatro
    // literales del cartel contextual (PlayerInputDriver.BuildFpsInstruction
    // y los role/RTS de UpdateInVehicle/UpdateRts). Al panel de pausa le
    // faltaban Q, C, F1/F2/F3, H, Espacio, Ctrl+1..9, el zoom con clic
    // derecho, la [T] del artillero y la [R] de municion.
    //
    // La tabla de abajo es un inventario literal de las lecturas de teclado
    // y mouse de PlayerInputDriver (mas la [ESC] de PauseController): no hay
    // atajos "de diseño" que el codigo no lea.
    //
    // Clase estatica y sin estado a proposito: no toca MonoBehaviour ni
    // Object.GetInstanceID(), asi que no tiene nada que perder en el domain
    // reload al entrar a Play.
    public static class ControlsTable
    {
        // Atajos compuestos: las teclas se separan con '/' y se renderizan
        // como "[1][2][3]", el mismo estilo que ya usaban los literales.
        const char KeySeparator = '/';

        // El separador exacto del cartel contextual (tres espacios a cada
        // lado del punto medio), tal cual lo escribia BuildFpsInstruction.
        const string LineSeparator = "   ·   ";

        // El cartel de abajo es una sola linea en pantalla: mostrar los 15
        // atajos de "a pie" ahi seria ilegible. La tabla esta ordenada por
        // relevancia, asi que cortar por arriba deja lo que mas se usa.
        const int DefaultLineEntries = 7;

        public static readonly ControlContext[] AllContexts =
        {
            ControlContext.FpsAPie,
            ControlContext.Rts,
            ControlContext.VehiculoConductor,
            ControlContext.VehiculoArtillero,
            ControlContext.VehiculoPasajero,
            ControlContext.VehiculoRts
        };

        const ControlContext Todos =
            ControlContext.FpsAPie | ControlContext.Rts | ControlContext.VehiculoConductor |
            ControlContext.VehiculoArtillero | ControlContext.VehiculoPasajero | ControlContext.VehiculoRts;

        const ControlContext AdentroDelVehiculo =
            ControlContext.VehiculoConductor | ControlContext.VehiculoArtillero |
            ControlContext.VehiculoPasajero | ControlContext.VehiculoRts;

        // Los tres asientos en vista FPS (el mismo corte que usa
        // UpdateInVehicle antes de la rama de Rig.Mode == ControlMode.Rts).
        const ControlContext AsientosFps =
            ControlContext.VehiculoConductor | ControlContext.VehiculoArtillero |
            ControlContext.VehiculoPasajero;

        // Fuera del vehiculo: F1/F2/F3, Q y C se leen en Update() DESPUES
        // del "if (currentSeat.HasValue) { UpdateInVehicle(); return; }",
        // asi que adentro del tanque no existen.
        const ControlContext APieOTactico = ControlContext.FpsAPie | ControlContext.Rts;

        // Las dos vistas de camara con paneo top-down.
        const ControlContext VistasRts = ControlContext.Rts | ControlContext.VehiculoRts;

        // Ordenada por relevancia descendente para el cartel contextual:
        // LineFor() corta por arriba y FullText() la reagrupa por contexto,
        // asi que este orden tambien manda adentro de cada grupo del panel.
        static readonly ControlEntry[] Entries =
        {
            new ControlEntry("TAB", "alternar entre vista FPS y vista táctica RTS", Todos, SP.Player.KeyBindings.AlternarVista),

            new ControlEntry("WASD", "moverse", ControlContext.FpsAPie),
            new ControlEntry("WASD", "conducir: acelerar, retroceder y girar", ControlContext.VehiculoConductor),
            new ControlEntry("WASD", "panear la cámara", VistasRts),

            new ControlEntry("Clic", "disparar (mantener para fuego sostenido)", ControlContext.FpsAPie),
            new ControlEntry("Clic", "disparar el cañón de la torreta", ControlContext.VehiculoArtillero),
            new ControlEntry("Clic", "seleccionar al aliado o al vehículo bajo el cursor", ControlContext.Rts),

            new ControlEntry("Mouse", "mirar alrededor", ControlContext.FpsAPie),
            new ControlEntry("Mouse", "girar la torreta hacia donde apuntás", ControlContext.VehiculoArtillero),

            new ControlEntry("R", "recargar el arma", ControlContext.FpsAPie, SP.Player.KeyBindings.Recargar),
            new ControlEntry("R", "alternar munición explosiva / perforante", ControlContext.VehiculoArtillero),

            new ControlEntry("1/2/3", "cambiar de arma: fusil, pistola, pesada", ControlContext.FpsAPie),

            new ControlEntry("E", "subir al vehículo cercano, o equipar el arma del piso", ControlContext.FpsAPie),
            new ControlEntry("E", "bajarse del vehículo", AdentroDelVehiculo),

            new ControlEntry("F", "poseer al aliado al que estás apuntando", ControlContext.FpsAPie, SP.Player.KeyBindings.Poseer),
            new ControlEntry("F", "poseer al aliado bajo el cursor, o tomar el mando del vehículo ocupado", ControlContext.Rts, SP.Player.KeyBindings.Poseer),

            new ControlEntry("T", "ordenarle al aliado libre más cercano que vaya al punto apuntado", ControlContext.FpsAPie),
            new ControlEntry("T/Clic der.", "mover la selección al punto, o atacar al enemigo señalado", ControlContext.Rts),
            new ControlEntry("T", "mandar el vehículo al punto del suelo que señalás", ControlContext.VehiculoArtillero),

            new ControlEntry("G", "frenar (mantener)", ControlContext.VehiculoConductor),
            new ControlEntry("G", "ordenarle al aliado más cercano que suba al vehículo apuntado", ControlContext.FpsAPie),
            new ControlEntry("G", "subir la selección al vehículo señalado, o bajar a todos si ya está ocupado", ControlContext.Rts),

            new ControlEntry("Clic der.", "zoom de mira (mantener)", ControlContext.FpsAPie | ControlContext.VehiculoArtillero),

            new ControlEntry("2", "pasar al asiento de artillero (si está libre)", ControlContext.VehiculoConductor),
            new ControlEntry("1", "pasar al asiento de conductor (si está libre)", ControlContext.VehiculoArtillero),
            new ControlEntry("V", "alternar cámara en primera persona / exterior", AsientosFps, SP.Player.KeyBindings.CamaraVehiculo),

            new ControlEntry("Arrastrar", "seleccionar a todos los aliados del recuadro", ControlContext.Rts),
            new ControlEntry("Shift+Clic", "sumar a la selección sin perder lo ya elegido", ControlContext.Rts),
            new ControlEntry("Ctrl+A", "seleccionar a toda la escuadra viva", ControlContext.Rts),
            new ControlEntry("X", "cancelar la orden de la selección y volver a patrullar", ControlContext.Rts),
            new ControlEntry("Espacio", "recentrar la cámara en la escuadra", ControlContext.Rts, SP.Player.KeyBindings.Recentrar),
            new ControlEntry("Rueda", "acercar y alejar la cámara", VistasRts),

            new ControlEntry("Ctrl+1..9", "guardar la selección como grupo de control", ControlContext.Rts),
            new ControlEntry("1..9", "recuperar el grupo de control (doble toque: además lleva la cámara ahí)", ControlContext.Rts),
            new ControlEntry("Shift+Clic der.", "encolar el destino detrás de las órdenes ya dadas", ControlContext.Rts),
            new ControlEntry("Clic der.", "mantener: vista previa de la formación antes de soltar la orden", ControlContext.Rts),
            new ControlEntry("Clic der.", "mandar la camioneta al punto del suelo apuntado", ControlContext.FpsAPie),

            new ControlEntry("Q", "ciclar la posesión al siguiente aliado vivo", APieOTactico, SP.Player.KeyBindings.CiclarPosesion),
            new ControlEntry("Z", "ciclar la posesión al aliado vivo anterior", APieOTactico, SP.Player.KeyBindings.CiclarPosesionAtras),
            new ControlEntry("C", "poseer al aliado vivo más cercano", APieOTactico, SP.Player.KeyBindings.PoseerMasCercano),
            new ControlEntry("F1/F2/F3", "poseer directamente al soldado 1, 2 o 3 de la escuadra", APieOTactico),

            new ControlEntry("U", "ordenarle a un aliado que suba al vehiculo, de a uno", ControlContext.FpsAPie | AdentroDelVehiculo),
            new ControlEntry("I", "bajar a todos los aliados del vehiculo", ControlContext.FpsAPie),

            new ControlEntry("Y", "reagrupar a la seleccion dispersa", ControlContext.Rts, SP.Player.KeyBindings.Reagrupar),
            new ControlEntry("B", "retirada: alejar a la seleccion del enemigo mas cercano", ControlContext.Rts, SP.Player.KeyBindings.Retirada),
            new ControlEntry("K", "ciclar la formacion con la que se emiten las ordenes", ControlContext.Rts, SP.Player.KeyBindings.CiclarFormacion),
            new ControlEntry("J", "seleccionar solo a los heridos", ControlContext.Rts, SP.Player.KeyBindings.SeleccionarHeridos),
            new ControlEntry("N", "seleccionar a todos los del mismo tipo en pantalla", ControlContext.Rts, SP.Player.KeyBindings.SeleccionarMismoTipo),

            new ControlEntry("H", "abrir y cerrar esta lista de controles sin pausar el juego", Todos, SP.Player.KeyBindings.Controles),
            new ControlEntry("ESC", "pausa y libera el cursor; dentro de los menús vuelve un paso atrás", Todos),
            new ControlEntry("Clic", "capturar el cursor para poder mirar con el mouse", AsientosFps | ControlContext.FpsAPie)
        };

        // Todos los atajos que se leen en ese contexto, en orden de
        // relevancia. Devuelve un iterador: no copia la tabla.
        public static IEnumerable<ControlEntry> For(ControlContext ctx)
        {
            for (int i = 0; i < Entries.Length; i++)
            {
                if ((Entries[i].Contexts & ctx) != 0) yield return Entries[i];
            }
        }

        // La linea corta del cartel de abajo, con el formato de siempre:
        // "[TECLA] descripcion   ·   [TECLA] descripcion".
        public static string LineFor(ControlContext ctx) => LineFor(ctx, DefaultLineEntries);

        // maxEntries <= 0 devuelve la linea entera, sin cortar.
        public static string LineFor(ControlContext ctx, int maxEntries)
        {
            var sb = new StringBuilder();
            int shown = 0;
            foreach (var e in For(ctx))
            {
                if (maxEntries > 0 && shown >= maxEntries) break;
                if (shown > 0) sb.Append(LineSeparator);
                sb.Append(DisplayKeyFor(e)).Append(' ').Append(e.Description);
                shown++;
            }
            return sb.ToString();
        }

        // El texto multilinea del panel de pausa: un encabezado por
        // contexto y una linea por atajo.
        public static string FullText()
        {
            var sb = new StringBuilder();
            for (int i = 0; i < AllContexts.Length; i++)
            {
                var ctx = AllContexts[i];
                if (i > 0) sb.Append('\n');
                sb.Append(HeaderFor(ctx)).Append('\n');
                foreach (var e in For(ctx))
                    sb.Append(DisplayKeyFor(e)).Append(' ').Append(e.Description).Append('\n');
            }
            return sb.ToString();
        }

        public static string DisplayKeyFor(ControlEntry e)
        {
            if (e.ActionId != null)
            {
                return FormatKey(SP.Player.KeyBindings.DisplayName(e.ActionId));
            }
            return FormatKey(e.Key);
        }

        public static string HeaderFor(ControlContext ctx)
        {
            switch (ctx)
            {
                case ControlContext.FpsAPie: return "A PIE (FPS)";
                case ControlContext.Rts: return "VISTA TÁCTICA (RTS)";
                case ControlContext.VehiculoConductor: return "VEHÍCULO — CONDUCTOR";
                case ControlContext.VehiculoArtillero: return "VEHÍCULO — ARTILLERO";
                case ControlContext.VehiculoPasajero: return "VEHÍCULO — PASAJERO";
                case ControlContext.VehiculoRts: return "VEHÍCULO — VISTA TÁCTICA";
                default: return "CONTROLES";
            }
        }

        // "1/2/3" -> "[1][2][3]", "TAB" -> "[TAB]".
        public static string FormatKey(string key)
        {
            if (string.IsNullOrEmpty(key)) return string.Empty;
            var sb = new StringBuilder();
            var parts = key.Split(KeySeparator);
            for (int i = 0; i < parts.Length; i++)
                sb.Append('[').Append(parts[i]).Append(']');
            return sb.ToString();
        }

        // Chequeo barato para el runner de tests: toda entrada declara al
        // menos un contexto, y cada uno de los seis tiene al menos una.
        public static bool Validate(out string problem)
        {
            for (int i = 0; i < Entries.Length; i++)
            {
                if (Entries[i].Contexts == 0)
                {
                    problem = $"La entrada [{Entries[i].Key}] no declara ningun contexto.";
                    return false;
                }
                if (string.IsNullOrEmpty(Entries[i].Key) || string.IsNullOrEmpty(Entries[i].Description))
                {
                    problem = $"Entrada incompleta en el indice {i}.";
                    return false;
                }
            }

            for (int i = 0; i < AllContexts.Length; i++)
            {
                bool any = false;
                foreach (var unused in For(AllContexts[i])) { any = true; break; }
                if (!any)
                {
                    problem = $"El contexto {AllContexts[i]} no tiene ningun atajo.";
                    return false;
                }
            }

            problem = null;
            return true;
        }
    }
}
