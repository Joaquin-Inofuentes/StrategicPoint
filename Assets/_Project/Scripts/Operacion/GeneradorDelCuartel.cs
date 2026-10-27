using System;
using UnityEngine;
using SP.Presentation;

namespace SP.Operacion
{
    // Etapa C del rediseño del cuartel (referencia: Bruneval): el objetivo 1 ya no es "derribar torretas" sino CORTAR LA ENERGIA.
    // Un grupo electrogeno en el sector del comando; al destruirlo se apagan los reflectores y se abre el porton norte. Las torretas quedan
    // como objetivo opcional (el camino viejo sigue valiendo). Es un ObstacleMarker comun (recibe disparos y explosiones).
    public class GeneradorDelCuartel : MonoBehaviour
    {
        public ObstacleMarker marca;
        public Renderer[] luces;                 // pilotos del generador: se apagan al caer
        public static event Action<GeneradorDelCuartel> Cayo;

        public bool EstaCaido { get; private set; }
        public Vector3 Cima => transform.position + Vector3.up * 2.5f;

        void OnEnable() { ObstacleMarker.Derrumbado += AlDerrumbarse; }
        void OnDisable() { ObstacleMarker.Derrumbado -= AlDerrumbarse; }

        void AlDerrumbarse(ObstacleMarker m)
        {
            if (m == null || m != marca) return;
            Caer();
        }

        // Tambien lo usan los saltos de prueba y la restauracion de partida (sin animacion).
        public void Caer()
        {
            if (EstaCaido) return;
            EstaCaido = true;
            if (luces != null) foreach (var l in luces) if (l != null) l.enabled = false;
            Cayo?.Invoke(this);
        }

        public void Reponer()
        {
            EstaCaido = false;
            if (luces != null) foreach (var l in luces) if (l != null) l.enabled = true;
        }
    }
}
