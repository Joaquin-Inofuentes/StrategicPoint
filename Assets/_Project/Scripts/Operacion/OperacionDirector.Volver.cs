using UnityEngine;
using SP.UI;

namespace SP.Operacion
{
    // Ronda 13 T10: la Operacion le da al unico cartel «DEBES VOLVER A LA GUERRA» (CartelVolverView) lo que necesita.
    // El estado lo calcula TickAlejamiento (OperacionDirector.cs); el cartel decide cuando mostrarse.
    public partial class OperacionDirector : IObjetivoParaVolver
    {
        bool volverActiva; float volverDistancia, volverUmbral = 70f; Vector3 volverPunto;

        bool IObjetivoParaVolver.Activa => volverActiva;
        float IObjetivoParaVolver.Distancia() => volverDistancia;
        float IObjetivoParaVolver.Umbral() => volverUmbral;
        Vector3 IObjetivoParaVolver.Punto() => volverPunto;
    }
}
