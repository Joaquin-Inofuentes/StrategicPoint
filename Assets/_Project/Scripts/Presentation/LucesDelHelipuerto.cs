using UnityEngine;

namespace SP.Presentation
{
    // Bug #063: "quiero puntitos de luminarias chiquitas donde aterriza el helicoptero". Balizas chiquitas alrededor del
    // helipuerto (las arma OperacionBuilder.AmpliarCiudad): las de las esquinas quedan fijas en ambar y las de los lados
    // corren en secuencia en verde, como las luces de aproximacion. Solo cambia el material compartido cuando una baliza
    // se prende o se apaga (nada de materiales por instancia).
    public class LucesDelHelipuerto : MonoBehaviour
    {
        public Material encendida, apagada, esquina;
        public float velocidad = 9f;        // balizas por segundo que avanza la secuencia
        public int largoDelTren = 5;

        MeshRenderer[] balizas;
        bool[] prendida;

        void Start()
        {
            balizas = GetComponentsInChildren<MeshRenderer>(true);
            prendida = new bool[balizas.Length];
        }

        void Update()
        {
            if (balizas == null || balizas.Length == 0 || encendida == null || apagada == null) return;
            int n = balizas.Length;
            float cabeza = Time.time * velocidad % n;
            for (int i = 0; i < n; i++)
            {
                var r = balizas[i];
                if (r == null || (esquina != null && r.sharedMaterial == esquina)) continue;
                float atras = (cabeza - i + n) % n;
                bool on = atras < largoDelTren;
                if (on == prendida[i]) continue;
                prendida[i] = on;
                r.sharedMaterial = on ? encendida : apagada;
            }
        }
    }
}
