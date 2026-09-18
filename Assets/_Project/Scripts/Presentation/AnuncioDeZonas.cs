using UnityEngine;
using SP.Player;
using SP.UI;

namespace SP.Presentation
{
    // Avisa en que bloque del nivel esta el jugador (el nivel mide 320 m de
    // largo: sin esto no hay sensacion de avance). Al entrar en un bloque
    // nuevo: aviso arriba + sonido; si tiene un tanque enemigo, alerta roja.
    // Se agrega solo desde GameplaySceneBootstrap; no depende de la escena.
    public class AnuncioDeZonas : MonoBehaviour
    {
        struct Zona
        {
            public string Nombre, Consejo;
            public float ZMin, ZMax;
            public bool TanqueEnemigo;
        }

        // Mismos limites que LevelBlockoutBuilder (bloques de sur a norte).
        static readonly Zona[] Zonas =
        {
            new Zona { Nombre = "1 · BASE", ZMin = -30f, ZMax = 15f, Consejo = "Tanque propio: [E] para subir" },
            new Zona { Nombre = "2 · CAMPO DE TIRO", ZMin = 15f, ZMax = 66f, Consejo = "[Shift]+[T] sobre una cobertura la toma un aliado" },
            new Zona { Nombre = "3 · PASO DEL CAÑON", ZMin = 66f, ZMax = 90f, Consejo = "Hueco de 18 m: pasa el tanque" },
            new Zona { Nombre = "4 · ALDEA", ZMin = 90f, ZMax = 145f, Consejo = "Casas y calle central" },
            new Zona { Nombre = "5 · PUESTO AVANZADO", ZMin = 145f, ZMax = 192f, TanqueEnemigo = true, Consejo = "Usa tu tanque: el cañon derriba coberturas" },
            new Zona { Nombre = "6 · CHICANE", ZMin = 192f, ZMax = 222f, Consejo = "Muros en S: el tanque tiene que girar" },
            new Zona { Nombre = "7 · FORTIN", ZMin = 222f, ZMax = 272f, TanqueEnemigo = true, Consejo = "Las brechas (muro rojizo) se rompen a cañonazos" },
            new Zona { Nombre = "8 · DEPOSITO FINAL", ZMin = 272f, ZMax = 310f, TanqueEnemigo = true, Consejo = "Ultimo bloque" },
        };

        public static int ZonaActual { get; private set; } = -1;
        public static string NombreActual => ZonaActual >= 0 ? Zonas[ZonaActual].Nombre : "";

        PlayerInputDriver driver;
        float proximo;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetearZona() => ZonaActual = -1;

        public static AnuncioDeZonas Asegurar()
        {
            var a = FindFirstObjectByType<AnuncioDeZonas>();
            if (a != null) return a;
            return new GameObject("AnuncioDeZonas").AddComponent<AnuncioDeZonas>();
        }

        static int IndiceDe(float z)
        {
            for (int i = 0; i < Zonas.Length; i++)
                if (z >= Zonas[i].ZMin && z < Zonas[i].ZMax) return i;
            return -1;
        }

        void Update()
        {
            if (Time.time < proximo) return;
            proximo = Time.time + 0.5f;
            if (driver == null) driver = FindFirstObjectByType<PlayerInputDriver>();
            if (driver == null || driver.Brain == null || driver.Brain.Current == null) return;

            // Adentro del tanque el soldado esta apagado y quieto: se usa el tanque.
            Vector3 p = driver.Vehicle != null && driver.Vehicle.PlayerAboard
                ? driver.Vehicle.transform.position
                : driver.Brain.Current.transform.position;

            int idx = IndiceDe(p.z);
            if (idx < 0 || idx == ZonaActual) return;
            bool primera = ZonaActual < 0;
            ZonaActual = idx;
            if (primera) return;   // el primer bloque ya lo dice el cartel de mision

            var z = Zonas[idx];
            Feedback.Accion(SfxKind.Select, $"BLOQUE {z.Nombre}", null, Feedback.Info, aviso: true, pulso: false, volumen: 0.5f);
            if (z.TanqueEnemigo)
                Feedback.Accion(SfxKind.EnemySpotted, "¡HAY UN TANQUE ENEMIGO EN ESTE BLOQUE!", null, Feedback.Bad, aviso: true, pulso: false, volumen: 0.6f);
            else if (!string.IsNullOrEmpty(z.Consejo))
                AlertQueue.Push(z.Consejo, AlertPriority.Baja, 2.2f);
        }
    }
}
