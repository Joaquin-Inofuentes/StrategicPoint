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
