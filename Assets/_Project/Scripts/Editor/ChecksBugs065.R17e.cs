using System.Collections;
using System.Text;
using UnityEngine;
using SP.Core;
using SP.Actors;
using SP.Combat;
using SP.Operacion;
using SP.Player;

namespace SP.EditorTools
{
    // Ronda 17e: cierre de los huecos de feedback. Lo que se puede medir sin teclado: la orden de mover devuelve false (y no suena el tono de exito)
    // cuando el punto es inaccesible, y el cambio de tamano del minimapa informa su nuevo estado.
    public static partial class ChecksBugs065
    {
        // Tomas para el atlas: la misma vista con calidad/iluminacion/brillo distintos (no verifica nada, deja el estado como estaba).
        static IEnumerator Bug991c()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            OperacionPrueba.Arrancar(2, 1);
            ModoDios.Poner(true);
            foreach (var x in Esperar(3f)) yield return x;
            int q0 = SP.UI.AjustesDeJuego.CalidadActual; var i0 = SP.UI.AjustesDeJuego.Iluminacion; float b0 = SP.UI.AjustesDeJuego.Brillo;
            SP.UI.AjustesDeJuego.PonerCalidad(2); SP.UI.AjustesDeJuego.PonerIluminacion(SP.UI.AjustesDeJuego.NivelDeIluminacion.Alta); SP.UI.AjustesDeJuego.PonerBrillo(1f);
            foreach (var x in Esperar(1.2f)) yield return x;
            foreach (var x in CapturarPantalla("r17_calidad_alta_ilum_alta.png")) yield return x;
            SP.UI.AjustesDeJuego.PonerCalidad(0); SP.UI.AjustesDeJuego.PonerIluminacion(SP.UI.AjustesDeJuego.NivelDeIluminacion.Baja);
            foreach (var x in Esperar(1.2f)) yield return x;
            foreach (var x in CapturarPantalla("r17_calidad_baja_ilum_baja.png")) yield return x;
            SP.UI.AjustesDeJuego.PonerCalidad(2); SP.UI.AjustesDeJuego.PonerIluminacion(SP.UI.AjustesDeJuego.NivelDeIluminacion.Media); SP.UI.AjustesDeJuego.PonerBrillo(0.8f);
            foreach (var x in Esperar(1.2f)) yield return x;
            foreach (var x in CapturarPantalla("r17_brillo_80.png")) yield return x;
            SP.UI.AjustesDeJuego.PonerBrillo(1.4f);
            foreach (var x in Esperar(1.2f)) yield return x;
            foreach (var x in CapturarPantalla("r17_brillo_140.png")) yield return x;
            SP.UI.AjustesDeJuego.PonerCalidad(q0); SP.UI.AjustesDeJuego.PonerIluminacion(i0); SP.UI.AjustesDeJuego.PonerBrillo(b0);
            PlayerPrefs.DeleteKey("sp_calidad"); PlayerPrefs.DeleteKey("sp_iluminacion"); PlayerPrefs.DeleteKey("sp_brillo");
            ModoDios.Poner(false);
            Fin("OK 4 tomas de calidad, iluminacion y brillo");
        }

        static IEnumerator Bug991b()
        {
            if (!Application.isPlaying) { Fin("FALLO el editor no esta en Play"); yield break; }
            var sb = new StringBuilder(); bool ok = true;
            OperacionPrueba.Arrancar(2, 1);
            ModoDios.Poner(true);
            foreach (var x in Esperar(2.5f)) yield return x;
            var yo = W9Yo();
            Soldier aliado = null;
            var todos = ActorRegistry.All;
            for (int i = 0; i < todos.Count; i++)
            {
                var s = todos[i];
                if (s != null && s != yo && s.Team == TeamId.Player && s.gameObject.activeInHierarchy && s.Health != null && s.Health.IsAlive) { aliado = s; break; }
            }
            W8Ok(ref ok, aliado != null, sb, "hay un aliado para ordenar");
            if (aliado != null)
            {
                bool inaccesible = OrderService.IssueMoveOrder(aliado, new Vector3(9000f, 0f, 9000f));
                W8Ok(ref ok, !inaccesible, sb, "mover a un punto inaccesible devuelve false (el lote suena a rechazo, no a exito)");
                bool cerca = OrderService.IssueMoveOrder(aliado, aliado.transform.position + aliado.transform.forward * 2f);
                W8Ok(ref ok, cerca, sb, "mover a un punto cercano caminable devuelve true");
            }
            var mini = Object.FindFirstObjectByType<SP.UI.MinimapFollow>();
            if (mini != null)
            {
                bool antes = mini.Agrandado; bool despues = mini.AlternarTamano(); mini.AlternarTamano();
                W8Ok(ref ok, antes != despues, sb, $"el minimapa informa el nuevo tamano ({(despues ? "grande" : "mini")})");
            }
            ModoDios.Poner(false);
            Fin((ok ? "OK " : "FALLO ") + sb);
        }
    }
}
