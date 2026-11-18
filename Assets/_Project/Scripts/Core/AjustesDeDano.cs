using UnityEngine;
using SP.Actors;
using SP.Combat;
using SP.Vehicles;

namespace SP.Core
{
    // Pedido 2026-11-13: "sliders para regular dano propio y dano enemigo; ese coeficiente se aplica global; con sliders y numeros en Configuraciones".
    //  - Enemigo: coeficiente sobre TODO el dano que recibe tu bando (soldados y vehiculos) de enemigos, torretas, helicopteros y explosiones sin duenio.
    //  - Propio:  coeficiente sobre TODO el dano que le hace tu bando a los enemigos (y a lo neutral).
    // Se guardan en PlayerPrefs. Health.TakeDamage los aplica a cada golpe, asi que valen en todos los niveles y para cualquier arma.
    public static class AjustesDeDano
    {
        // R14 (#16): "que el daño enemigo y propio maximo sea 30 para jugar con extremos" (el tope era 3).
        public const float Minimo = 0.1f, Maximo = 30f, PropioPorDefecto = 1f, EnemigoPorDefecto = 0.5f;
        const string ClavePropio = "ajuste_dano_propio", ClaveEnemigo = "ajuste_dano_enemigo";
        static float propio = -1f, enemigo = -1f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reiniciar() { propio = -1f; enemigo = -1f; forzado = false; }

        // Ronda 13 (T3): valores de PRUEBA, no persistentes (no tocan PlayerPrefs). Los checks y la suite headless los usan para no depender de los sliders del usuario.
        static bool forzado;
        public static bool EstaForzado => forzado;
        public static void ForzarParaPrueba(float valorPropio, float valorEnemigo) { forzado = true; propio = Limpiar(valorPropio); enemigo = Limpiar(valorEnemigo); }
        public static void Liberar() { forzado = false; propio = -1f; enemigo = -1f; }

        public static float Propio
        {
            get { if (propio < 0f) propio = Limpiar(Leer(ClavePropio, PropioPorDefecto)); return propio; }
            set { propio = Limpiar(value); if (!forzado) Guardar(ClavePropio, propio); }
        }

        public static float Enemigo
        {
            get { if (enemigo < 0f) enemigo = Limpiar(Leer(ClaveEnemigo, EnemigoPorDefecto)); return enemigo; }
            set { enemigo = Limpiar(value); if (!forzado) Guardar(ClaveEnemigo, enemigo); }
        }

        public static float Limpiar(float v) => Mathf.Round(Mathf.Clamp(float.IsNaN(v) ? 1f : v, Minimo, Maximo) * 100f) / 100f;

        static float Leer(string clave, float defecto) { try { return PlayerPrefs.GetFloat(clave, defecto); } catch { return defecto; } }
        static void Guardar(string clave, float v) { try { PlayerPrefs.SetFloat(clave, v); PlayerPrefs.Save(); } catch { } }

        public static void Restablecer() { Propio = PropioPorDefecto; Enemigo = EnemigoPorDefecto; }

        // Id de atacante de GUION (heridas del tutorial, destruccion de la cinematica): nunca se escala.
        public const int IdGuion = -9;

        // Coeficiente de un golpe segun quien lo da y a quien le llega. 1 = sin cambio (props, civiles, fuego amigo).
        public static float Multiplicador(int atacanteId, Health victima)
        {
            if (victima == null || atacanteId == IdGuion) return 1f;
            TeamId equipo;
            var s = victima.GetComponent<Soldier>();
            if (s != null) { if (s.Role == RoleType.Civilian) return 1f; equipo = s.Team; }
            else
            {
                var v = victima.GetComponent<Vehicle>();
                if (v == null) v = victima.GetComponentInParent<Vehicle>();
                if (v == null) return 1f;
                equipo = v.Bando;
            }
            var atacante = atacanteId >= 0 ? ActorRegistry.FindById(atacanteId) : null;
            bool atacaTuBando = atacante != null && atacante.Team == TeamId.Player;
            if (equipo == TeamId.Player) return atacaTuBando ? 1f : Enemigo;   // la victima es tu bando: lo que no es tuyo es del enemigo
            return atacaTuBando || atacante == null ? Propio : 1f;            // la victima es enemiga: lo tuyo (o sin duenio) usa tu coeficiente
        }
    }
}
