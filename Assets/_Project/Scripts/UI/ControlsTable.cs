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
            new ControlEntry("WASD", "panear la cámara (el doble de rápido)", VistasRts),

            new ControlEntry("Shift", "correr (vos y los aliados que te siguen)", ControlContext.FpsAPie),
            new ControlEntry("Clic", "disparar (mantener para fuego sostenido)", ControlContext.FpsAPie),
            new ControlEntry("Clic", "disparar el cañón de la torreta", ControlContext.VehiculoArtillero),
            new ControlEntry("Clic", "disparar la metralleta del tanque", ControlContext.VehiculoPasajero),
            new ControlEntry("Clic", "seleccionar al aliado o al vehículo bajo el cursor", ControlContext.Rts),

            new ControlEntry("Q", "mantener: radial de órdenes (solo ofrece lo que podés hacer con lo que apuntás); tocar: cambiar de soldado", APieOTactico | AdentroDelVehiculo, SP.Player.KeyBindings.CiclarPosesion),

            new ControlEntry("Clic der.", "mantener: mirar por la mira del arma (primera persona con zoom)", ControlContext.FpsAPie),
            new ControlEntry("Clic der.", "mantener: mirar por la mira del cañón o de la metralleta", ControlContext.VehiculoArtillero | ControlContext.VehiculoPasajero),
            new ControlEntry("Mouse", "mirar alrededor", ControlContext.FpsAPie),
            new ControlEntry("Mouse", "girar la torreta hacia donde apuntás", ControlContext.VehiculoArtillero),

            new ControlEntry("R", "recargar el arma", ControlContext.FpsAPie, SP.Player.KeyBindings.Recargar),
            new ControlEntry("R", "alternar munición explosiva / perforante", ControlContext.VehiculoArtillero),

            new ControlEntry("Ctrl", "agacharse (mantener)", ControlContext.FpsAPie),
            new ControlEntry("Espacio", "saltar", ControlContext.FpsAPie),
            new ControlEntry("F", "cuchillo: tajo rápido, sin balas (arco brillante y golpe sordo si conecta)", ControlContext.FpsAPie, SP.Player.KeyBindings.AtaqueCuchillo),
            new ControlEntry("G", "granada: mantener para ver la curva y el radio, soltar para lanzar (clic der. o Esc la guardan)", ControlContext.FpsAPie, SP.Player.KeyBindings.Granada),
            new ControlEntry("1/2/3", "cambiar de arma según tu clase", ControlContext.FpsAPie),

            new ControlEntry("E", "tocar: subir al tanque, usar la ametralladora fija o equipar el arma del piso; mantener 5 s junto a un caído: reanimarlo", ControlContext.FpsAPie, SP.Player.KeyBindings.Interactuar),
            new ControlEntry("E", "mantener (medio segundo): habilidad de clase (el médico cura y reanima, el asalto y el francotirador afinan la puntería)", ControlContext.FpsAPie, SP.Player.KeyBindings.Interactuar),
            new ControlEntry("E", "bajarse del vehículo", AdentroDelVehiculo, SP.Player.KeyBindings.Interactuar),

            new ControlEntry("[ ]", "subir o bajar al soldado que manejas en el orden de la escuadra", ControlContext.FpsAPie),
            new ControlEntry(", .", "junto a una caja de suministros: cambiar el arma principal (arsenal)", ControlContext.FpsAPie),
            new ControlEntry("C", "mantener: ver las coberturas del piso y las rutas de patrulla enemigas", Todos, SP.Player.KeyBindings.VerTactico),
            new ControlEntry("F4", "modo dios: nadie de tu bando recibe daño (otra vez para apagar)", Todos),

            new ControlEntry("Espacio", "frenar (mantener)", ControlContext.VehiculoConductor, SP.Player.KeyBindings.Frenar),
            new ControlEntry("1/2/3/4", "cambiar de asiento: conductor, cañón, metralleta, pasajero (si está ocupado por un aliado, intercambian)", AsientosFps),

            new ControlEntry("Arrastrar", "seleccionar a todos los aliados del recuadro", ControlContext.Rts),
            new ControlEntry("Clic der.", "mover a la selección al punto bajo el cursor (o atacar al enemigo señalado)", ControlContext.Rts),
            new ControlEntry("Shift+Clic", "sumar a la selección sin perder lo ya elegido", ControlContext.Rts),
            new ControlEntry("Ctrl+A", "seleccionar a toda la escuadra viva", ControlContext.Rts),
            new ControlEntry("X", "cancelar la orden de la selección y volver a patrullar", ControlContext.Rts),
            new ControlEntry("Espacio", "recentrar la cámara en la escuadra", ControlContext.Rts, SP.Player.KeyBindings.Recentrar),
            new ControlEntry("F", "focalizar en el seleccionado o poseído", ControlContext.Rts, SP.Player.KeyBindings.FocalizarRts),
            new ControlEntry("Rueda", "acercar y alejar la cámara hacia donde está el cursor", VistasRts),
            new ControlEntry("`", "mantener: rotar la cámara con el mouse", VistasRts, SP.Player.KeyBindings.RotarCamaraRts),

            new ControlEntry("1 / 2 / 3", "seleccionar al soldado 1, 2 o 3 de la escuadra (también F1 / F2 / F3; Shift suma; doble toque lleva la cámara)", ControlContext.Rts),
            new ControlEntry("Ctrl+4..9", "guardar la selección como grupo de control", ControlContext.Rts),
            new ControlEntry("4..9", "recuperar el grupo de control (doble toque: además lleva la cámara ahí)", ControlContext.Rts),
            new ControlEntry("Shift+Clic der.", "encolar el destino detrás de las órdenes ya dadas", ControlContext.Rts),
            new ControlEntry("Clic der.", "mantener: vista previa de la formación antes de soltar la orden", ControlContext.Rts),

            new ControlEntry("Y", "reagrupar a la selección dispersa", ControlContext.Rts, SP.Player.KeyBindings.Reagrupar),
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
