using UnityEngine;
using SP.Actors;
using SP.Player;

namespace SP.Presentation
{
    // Bug #089: "quiero un sonido gratificante y anillos en el piso al revivir". Reanimacion.Revivido es el camino UNICO de
    // finalizacion (tecla [E], habilidad del medico, orden de reanimar, rescate automatico, reinicio de partida), asi que esto
    // se engancha ahi y los demas sitios ya no tocan SfxKind.Revive por su cuenta (sonaba en 3 de los 6 caminos y duplicado).
    //
    //  - Sonido: SfxSintetico.Reanimar (golpe grave + suspiro + acorde mayor), en 3D sobre el revivido.
    //  - Anillos: 3 anillos verde claro escalonados cada 0,15 s, de 0,4 a 3,0 m en 1,0 s, alfa 0,9 a 0, pegados al piso.
    public static class FeedbackDeRevivir
    {
        public static readonly Color VerdeClaro = new Color(0.55f, 1f, 0.65f);
        public const int Anillos = 3;
        public const float EscalonSegundos = 0.15f;
        public const float RadioFinal = 3f;
        public const float DuracionDeCadaAnillo = 1.0f;
        public const float AlturaSobreElPiso = 0.05f;
        public const float Volumen = 0.9f;

        public static int Cantidad { get; private set; }
        public static Soldier Ultimo { get; private set; }

        // El evento es estatico: sin recarga de dominio sobrevive al Play anterior, asi que se des-suscribe antes de suscribir.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Registrar()
        {
            Reanimacion.Revivido -= AlRevivir;
            Reanimacion.Revivido += AlRevivir;
        }

        public static void Reiniciar() { Cantidad = 0; Ultimo = null; }

        static float SueloBajo(Soldier s)
        {
            var p = s.transform.position;
            if (Physics.Raycast(p + Vector3.up * 0.3f, Vector3.down, out var hit, 3f, ~(1 << 2), QueryTriggerInteraction.Ignore)
                && hit.collider.GetComponentInParent<Soldier>() == null)
                return hit.point.y;
            return p.y - 0.8f;   // el pivote del soldado esta 0,8 m sobre los pies
        }

        static void AlRevivir(Soldier s)
        {
            if (s == null || !Application.isPlaying) return;
            Cantidad++;
            Ultimo = s;
            var pos = s.transform.position;
            AudioDirector.PlayAt(SfxKind.Revive, pos, Volumen, 0.9f);
            var centro = new Vector3(pos.x, SueloBajo(s) + AlturaSobreElPiso, pos.z);
            for (int i = 0; i < Anillos; i++)
                ImpactFx.SpawnShockwaveRing(centro, VerdeClaro, RadioFinal, DuracionDeCadaAnillo, i * EscalonSegundos);
        }
    }
}
