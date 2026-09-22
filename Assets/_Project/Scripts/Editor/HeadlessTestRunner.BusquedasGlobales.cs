using System.Collections.Generic;
using UnityEngine;
using SP.Combat;
using SP.Player;
using System.Text.RegularExpressions;

namespace SP.EditorTools
{
    // Red de seguridad del plan "eliminar los Find de runtime": el codigo de runtime solo puede BAJAR su cantidad de
    // busquedas globales de escena. Cada fase del plan baja el presupuesto; la meta es quedar en la lista blanca.
    public static partial class HeadlessTestRunner
    {
        // Presupuesto decreciente (baja al cerrar cada fase). Fuera de la lista blanca.
        const int PresupuestoBusquedasGlobales = 0;
        const int PresupuestoReintentosPorFrame = 0;

        // Barrido legitimo: archivo -> cantidad maxima de llamadas, con el motivo. Ninguna corre por frame.
        static readonly Dictionary<string, int> BusquedaPermitida = new Dictionary<string, int>
        {
            { "Core/ActorRegistry.cs", 1 },            // Rebarrer: capta los soldados que arrancan desactivados (throttled a 0,5 s)
            { "Core/WorldSystemsRegistry.cs", 5 },     // EnsurePopulated: una sola vez, guardado por flag
            { "Core/ApoyoEnElPiso.cs", 2 },            // arranque, una vez
            { "Core/Coberturas.cs", 3 },               // Registrar(), arranque
            { "Core/NavService.cs", 1 },               // rehorneado del navmesh, no por frame
            { "Core/MetricasDeBuild.cs", 4 },          // diagnostico a pedido
            { "Presentation/LimpiezaDeEscena.cs", 2 }, // utilidad de limpieza, una vez
            { "Presentation/ImpactFx.cs", 1 },         // DestroyOrphans: huerfanos tras Enter Play Mode sin recarga, al rearmar el pool
            { "Presentation/OrderMarkerFx.cs", 1 },    // idem
            { "Presentation/KillCylinderFx.cs", 1 },   // idem
            { "Presentation/DebrisPool.cs", 1 },       // idem
            { "Presentation/DecalPool.cs", 1 },        // idem
            { "Presentation/WorldUiDirector.cs", 5 },  // EnsurePopulated: una sola vez, guardado por flag (igual que WorldSystemsRegistry)
            { "Vehicles/TorretaFija.cs", 1 },          // InstalarEnEscena: una vez al arrancar, los emplazamientos son arte sin componente
            { "Core/Loc.cs", 1 },                      // pasada de traduccion de los Text (sin registro de Text)
            { "Presentation/FuentesBelicas.cs", 2 },   // pasada de fuentes sobre Text/TextMesh (sin registro)
            { "Mision/CinematicaDeVictoria.cs", 2 },   // una vez al empezar y una al terminar la cinematica
            { "Mision/MisionDirector.cs", 1 },         // Casas(): cachea el resultado (nunca uno vacio) y solo se llama al crear un enemigo, no por frame
            { "UI/MenuDeOrdenes.cs", 1 },              // AsegurarEnEscena: una vez al arrancar, busca el canvas del HUD
            { "UI/AjustesDeJuego.cs", 1 },             // AplicarEscala: al cambiar la escala de interfaz
        };

        static readonly Regex PatronBusquedaGlobal = new Regex(@"GameObject\.Find\(|FindObjectsByType|FindAnyObjectByType|FindFirstObjectByType|Resources\.FindObjectsOfTypeAll");
        static readonly Regex PatronReintento = new Regex(@"if \(.*== null\).*=\s*.*Find(Any|First)ObjectByType");

        static void ContarBusquedasGlobales(out int globales, out int reintentos, out string detalle, out string excesos)
        {
            globales = 0; reintentos = 0;
            var porArchivo = new SortedDictionary<string, int>();
            var sbExc = new System.Text.StringBuilder();
            foreach (var f in System.IO.Directory.GetFiles("Assets/_Project/Scripts", "*.cs", System.IO.SearchOption.AllDirectories))
            {
                var ruta = f.Replace("\\", "/");
                if (ruta.Contains("/Editor/")) continue;
                var rel = ruta.Substring("Assets/_Project/Scripts/".Length);
                bool permitido = BusquedaPermitida.TryGetValue(rel, out int maxPermitido);
                int enElArchivo = 0, reintentosDelArchivo = 0;
                foreach (var linea in System.IO.File.ReadAllLines(f))
                {
                    if (linea.TrimStart().StartsWith("//")) continue;
                    var codigo = linea.Split(new[] { "//" }, System.StringSplitOptions.None)[0];
                    int n = PatronBusquedaGlobal.Matches(codigo).Count;
                    if (n == 0) continue;
                    enElArchivo += n;
                    if (PatronReintento.IsMatch(codigo)) reintentosDelArchivo++;
                }
                if (enElArchivo == 0) continue;
                porArchivo[rel] = enElArchivo;
                if (permitido)
                {
                    if (enElArchivo > maxPermitido) sbExc.Append(rel).Append(" (").Append(enElArchivo).Append(" de ").Append(maxPermitido).Append(") ");
                    continue;
                }
                globales += enElArchivo;
                reintentos += reintentosDelArchivo;
            }
            var sb = new System.Text.StringBuilder();
            foreach (var kv in porArchivo) sb.Append(kv.Key).Append('=').Append(kv.Value).Append(' ');
            detalle = sb.ToString();
            excesos = sbExc.ToString();
        }

        // En Edit mode no corren Awake/OnEnable: la suite registra a mano los servicios de la escena que armo.
        static void ReRegistrarServiciosDeLaSuite(PlayerInputDriver inputDriver, ProjectilePool pool)
        {
            inputDriver.Registrar(); inputDriver.Brain.Registrar(); pool.Registrar();
            var outcome = UnityEngine.Object.FindFirstObjectByType<SP.Presentation.GameOutcomeController>(); if (outcome != null) outcome.Registrar();
            var roster = UnityEngine.Object.FindFirstObjectByType<SP.UI.RosterView>(); if (roster != null) roster.RegistrarActivo();
            var mini = UnityEngine.Object.FindFirstObjectByType<SP.UI.MinimapFollow>();
            if (mini != null)
            {
                mini.RegistrarActivo();
                var marco = GameObject.Find("MinimapBorder"); if (marco != null) mini.Marco = marco.GetComponent<RectTransform>();   // en runtime viene serializado
            }
            var arma = UnityEngine.Object.FindFirstObjectByType<SP.UI.WeaponStatusView>(); if (arma != null) arma.RegistrarActivo();
            var dios = UnityEngine.Object.FindFirstObjectByType<SP.UI.ModoDiosView>(); if (dios != null) dios.RegistrarActivo();
            var menu = UnityEngine.Object.FindFirstObjectByType<SP.UI.MenuDeOrdenes>(FindObjectsInactive.Include); if (menu != null) menu.RegistrarActivo();
            var ajustes = UnityEngine.Object.FindFirstObjectByType<SP.Ai.AjustesDeEscuadra>(FindObjectsInactive.Include); if (ajustes != null) ajustes.RegistrarActivo();
        }

        // Busquedas por nombre en la jerarquia (Transform.Find): quedan las de arranque (OnEnable/Awake/Ensure*) y las
        // "crear si no existe" que se guardan en un campo. Ninguna puede correr por frame. Tope = las de hoy: solo baja.
        // Ronda 13: +3 por el ConfirmExitPanel del menu principal (MainMenuController.Awake), mismo patron de
        // arranque que el resto del presupuesto -- no corre por frame.
        const int PresupuestoFindPorNombre = 118;
        static readonly Regex PatronFindPorNombre = new Regex(@"(?<!Shader)(?<!GameObject)(?<!Object)\.Find\(\s*[""\w]");
        static readonly Regex PatronMetodoPorFrame = new Regex(@"\b(void|bool|float|Vector\d)\s+(Update|LateUpdate|FixedUpdate|UpdateFrom|UpdateInVehicle|TickPlayerAim|Tick)\s*\(");
        static readonly Regex PatronCabeceraMetodo = new Regex(@"^\s*(public |private |protected |internal |static |override |virtual )*[\w<>\[\],\.?]+\s+\w+\s*\([^;]*\)\s*$");

        static void ContarFindPorNombre(out int total, out string enPorFrame)
        {
            total = 0;
            var sb = new System.Text.StringBuilder();
            foreach (var f in System.IO.Directory.GetFiles("Assets/_Project/Scripts", "*.cs", System.IO.SearchOption.AllDirectories))
            {
                var ruta = f.Replace("\\", "/");
                if (ruta.Contains("/Editor/") || ruta.Contains("/Demo/")) continue;
                bool enMetodoPorFrame = false; int inicioDelMetodo = -1;
                var lineas = System.IO.File.ReadAllLines(f);
                for (int i = 0; i < lineas.Length; i++)
                {
                    var linea = lineas[i];
                    if (linea.TrimStart().StartsWith("//")) continue;
                    var codigo = linea.Split(new[] { "//" }, System.StringSplitOptions.None)[0];
                    if (PatronCabeceraMetodo.IsMatch(codigo) && !codigo.Contains("Find("))
                    {
                        enMetodoPorFrame = PatronMetodoPorFrame.IsMatch(codigo);
                        inicioDelMetodo = i;
                    }
                    if (PatronFindPorNombre.IsMatch(codigo))
                    {
                        total += PatronFindPorNombre.Matches(codigo).Count;
                        if (enMetodoPorFrame && i - inicioDelMetodo < 400) sb.Append(ruta.Substring(ruta.LastIndexOf('/') + 1)).Append(':').Append(i + 1).Append(' ');
                    }
                }
            }
            enPorFrame = sb.ToString();
        }

        static void CheckFindPorNombre()
        {
            ContarFindPorNombre(out int total, out string enPorFrame);
            UnityEngine.Debug.Log($"[BUSQUEDAS] Transform.Find por nombre={total}/{PresupuestoFindPorNombre} por-frame=[{enPorFrame}]");
            Check($"Los Transform.Find por nombre no crecen ({total} de {PresupuestoFindPorNombre})", total <= PresupuestoFindPorNombre);
            Check("Ningun Transform.Find por nombre corre por frame: " + enPorFrame, enPorFrame.Length == 0);
        }

        static void CheckBusquedasGlobales()
        {
            CheckFindPorNombre();
            ContarBusquedasGlobales(out int globales, out int reintentos, out string detalle, out string excesos);
            UnityEngine.Debug.Log($"[BUSQUEDAS] globales={globales}/{PresupuestoBusquedasGlobales} reintentos={reintentos}/{PresupuestoReintentosPorFrame} :: {detalle}");
            Check($"Las busquedas globales de escena no crecen ({globales} de {PresupuestoBusquedasGlobales})", globales <= PresupuestoBusquedasGlobales);
            Check("Ningun archivo de la lista blanca supera su cupo de busquedas: " + excesos, excesos.Length == 0);
            Check($"Los reintentos 'if (x == null) x = Find...' por frame no crecen ({reintentos} de {PresupuestoReintentosPorFrame})", reintentos <= PresupuestoReintentosPorFrame);
        }
    }
}
