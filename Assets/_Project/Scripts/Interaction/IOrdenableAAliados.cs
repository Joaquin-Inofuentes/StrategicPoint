using UnityEngine;
using SP.Actors;

namespace SP.Interaction
{
    // Un interactuable que el jugador puede MANDAR a hacer a un aliado: apuntarle, tocar [Q] (o elegir INTERACTUAR en el radial)
    // y el aliado que corresponde camina hasta alli y lo hace solo. Pedido explicito: "solo apuntarle al interactuable y apretar Q
    // y que haga el resto".
    public interface IOrdenableAAliados
    {
        string NombreParaOrden { get; }          // "PANEL PUESTO 1", "COMPUTADORA"
        string QuienDebe { get; }                // "FLANQUEADOR" o "CUALQUIER ALIADO"
        Transform Raiz { get; }
        bool Completo { get; }
        bool Habilitado { get; }
        float Progreso01 { get; }

        // Puede este soldado hacerlo (rol, vivo, bando)?
        bool PuedeOperar(Soldier s);

        // El mejor de la lista (el mas cercano que pueda). Null si ninguno sirve.
        Soldier ElegirOperador(System.Collections.Generic.IList<Soldier> candidatos);

        // Manda a 's': camina hasta el interactuable y lo opera solo. Devuelve false (con motivo) si no se puede.
        bool EnviarA(Soldier s, out string motivo);
    }
}
