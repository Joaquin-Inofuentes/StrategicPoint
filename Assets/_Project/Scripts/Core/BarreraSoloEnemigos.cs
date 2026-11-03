using System.Collections.Generic;
using UnityEngine;

namespace SP.Core
{
    // Collider que bloquea al jugador y a sus aliados pero DEJA PASAR a los enemigos: lo llevan las entradas de las reservas (puntos de
    // reaparicion) para que no se pueda entrar a esos recintos. El movimiento (SP.Core.Deslizador) consulta este registro y, si quien
    // se mueve es un enemigo, ignora estos colliders. El resto de la fisica (balas, vehiculos) los trata como cualquier pared.
    [DisallowMultipleComponent]
    public class BarreraSoloEnemigos : MonoBehaviour
    {
        static readonly HashSet<Collider> registro = new HashSet<Collider>();
        public static bool Contiene(Collider c) => c != null && registro.Contains(c);

        Collider propio;
        void OnEnable() { propio = GetComponent<Collider>(); if (propio != null) registro.Add(propio); }
        void OnDisable() { if (propio != null) registro.Remove(propio); }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reiniciar() => registro.Clear();

        public static BarreraSoloEnemigos Crear(Transform padre, Vector3 localCentro, Vector3 tamano, string nombre = "BarreraSoloEnemigos")
        {
            var go = new GameObject(nombre);
            go.transform.SetParent(padre, false);
            go.transform.localPosition = localCentro;
            var bc = go.AddComponent<BoxCollider>(); bc.size = tamano;
            return go.AddComponent<BarreraSoloEnemigos>();
        }
    }
}
