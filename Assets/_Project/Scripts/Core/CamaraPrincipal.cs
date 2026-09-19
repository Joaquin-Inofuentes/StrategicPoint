using UnityEngine;

namespace SP.Core
{
    // Camara principal cacheada: Camera.main busca por tag en cada llamada; aqui se resuelve una vez y solo se vuelve
    // a buscar si la guardada se destruyo, se desactivo o dejo de ser MainCamera.
    public static class CamaraPrincipal
    {
        static Camera guardada;

        public static Camera Actual
        {
            get
            {
                if (guardada == null || !guardada.isActiveAndEnabled || !guardada.CompareTag("MainCamera"))
                    guardada = Camera.main;
                return guardada;
            }
        }
    }
}
