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
        // Unico de la escena: se registra al activarse en vez de que cada consumidor lo busque con un barrido.
        public static AnuncioDeZonas Activo { get; private set; }
        public static void ReiniciarActivo() => Activo = null;
        public void RegistrarActivo()
        {
            Activo = this;
        }
        void OnDisable() { if (Activo == this) Activo = null; }
        void OnEnable() => RegistrarActivo();
        struct Zona
        {
            public string Nombre, Consejo;
            public float ZMin, ZMax;
            public bool TanqueEnemigo;
        }

        // Mismos limites que LevelBlockoutBuilder (bloques de sur a norte). Tres caminos: ver RutasDelNivel.
        static readonly Zona[] Zonas =
        {
            new Zona { Nombre = "1 · BASE", ZMin = -30f, ZMax = 15f, Consejo = "Cada camino pide un rol: ASALTO abre las compuertas del oeste · FRANCOTIRADOR apaga las luces del este · MEDICO sostiene la carretera" },
            new Zona { Nombre = "2 · CAMPO DE TIRO", ZMin = 15f, ZMax = 66f, Consejo = "Mantene [C] para ver las coberturas · [Q] radial > CUBRIRSE" },
            new Zona { Nombre = "3 · PASO DEL CAÑON", ZMin = 66f, ZMax = 90f, Consejo = "Oeste: COMPUERTA blindada (solo el Asalto la demuele) · vigias en las torres: se los iguala con el Francotirador" },
            new Zona { Nombre = "4 · ALDEA", ZMin = 90f, ZMax = 145f, Consejo = "Las calles cruzan los tres caminos: cambia de flanco... y de soldado (1/2/3) segun lo que venga" },
            new Zona { Nombre = "5 · PUESTO AVANZADO", ZMin = 145f, ZMax = 192f, TanqueEnemigo = true, Consejo = "Polvorines (barriles con cartel amarillo): un tiro del Francotirador y vuela el grupo entero" },
            new Zona { Nombre = "6 · CHICANE", ZMin = 192f, ZMax = 222f, Consejo = "Muros en S en el centro; cada flanco tiene su puerta · la luz te delata: mira el cartel A LA LUZ / EN LA SOMBRA" },
            new Zona { Nombre = "7 · FORTIN", ZMin = 222f, ZMax = 272f, TanqueEnemigo = true, Consejo = "Antena de radio al este del fortin: demolerla evita los refuerzos de la vuelta · porton norte sellado: carga del Asalto" },
            new Zona { Nombre = "8 · REFUGIO DEL REHEN", ZMin = 272f, ZMax = 310f, TanqueEnemigo = true, Consejo = "La baliza cian marca al rehen. Al sacarlo, el enemigo CIERRA el camino por el que subiste: volve por otro" },
        };

        public static int ZonaActual { get; private set; } = -1;
        public static string NombreActual => ZonaActual >= 0 ? Zonas[ZonaActual].Nombre : "";

        PlayerInputDriver driver;
        float proximo;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetearZona() => ZonaActual = -1;

        // Los carteles de bloque son del nivel de SC_Gameplay; otros niveles (Operacion Cuartel) los apagan.
        public static bool Desactivado;

        public static AnuncioDeZonas Asegurar()
        {
            if (Desactivado) return null;
            if (Activo != null) return Activo;
            var a = new GameObject("AnuncioDeZonas").AddComponent<AnuncioDeZonas>();
            a.RegistrarActivo();
            return a;
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
            if (driver == null) driver = PlayerInputDriver.Activo;
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
