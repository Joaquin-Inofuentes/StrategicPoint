using UnityEngine;
using SP.Actors;
using SP.Combat;
using SP.Core;
using SP.Presentation;
using SP.Vehicles;

namespace SP.Operacion
{
    // Objetivo cumplible de las escenas de blockout (SC_Blockout_01..05). Sin el, la unica forma de ganar era matar a TODOS los
    // enemigos (BattleManager) y el "objetivo" era solo un letrero. Se agrega por codigo desde BlockoutScenesBuilder.
    //   * LlegarALaZona: gana cuando un soldado propio vivo (no civil) entra en el radio de la zona.
    //   * ConRehen:      ademas el civil tiene que estar vivo y dentro del radio de la zona de regreso; si muere, se pierde.
    //   * Resistir:      no hay zona; gana el BattleManager al caer el ultimo atacante (aqui solo se muestra el objetivo).
    // Con requisito de vehiculo, la zona no cuenta hasta que ese vehiculo este destruido.
    public sealed class BlockoutObjetivo : MonoBehaviour
    {
        public enum Modo { LlegarALaZona, ConRehen, Resistir }

        public Modo modo = Modo.LlegarALaZona;
        public string titulo = "";
        public string texto = "";
        public Transform zona;
        public float radio = 6f;
        public Vehicle vehiculoQueHayQueDestruir;
        public Soldier rehen;

        bool terminado, avisoInicial, avisoDeZona;
        float espera = 1.2f;

        public bool Terminado => terminado;

        void Update()
        {
            if (terminado) return;
            if (espera > 0f) { espera -= Time.deltaTime; return; }

            if (!avisoInicial)
            {
                avisoInicial = true;
                Aviso(titulo + ": " + texto, 7f);
            }
            if (modo == Modo.Resistir) return;

            var outcome = GameOutcomeController.Activo;
            if (outcome == null) return;

            if (modo == Modo.ConRehen && rehen != null)
            {
                var h = rehen.GetComponent<Health>();
                if (h != null && !h.IsAlive) { terminado = true; outcome.ShowDefeat("El rehen murio"); return; }
            }

            if (zona == null) return;
            bool vehiculoOk = vehiculoQueHayQueDestruir == null || vehiculoQueHayQueDestruir.IsDestroyed;
            if (!vehiculoOk)
            {
                if (!avisoDeZona && SoldadoEnZona(false) != null) { avisoDeZona = true; Aviso("Antes hay que destruir el tanque enemigo", 4f); }
                return;
            }
            if (modo == Modo.ConRehen)
            {
                var h = rehen != null ? rehen.GetComponent<Health>() : null;
                if (rehen == null || h == null || !h.IsAlive) return;
                if (Vector3.Distance(Plano(rehen.transform.position), Plano(zona.position)) > radio) return;
            }
            if (SoldadoEnZona(true) == null) return;

            terminado = true;
            Aviso("OBJETIVO CUMPLIDO", 3f);
            outcome.ShowVictory();
        }

        // Un soldado propio vivo y que no sea el civil dentro del radio.
        Soldier SoldadoEnZona(bool incluyeCualquiera)
        {
            if (zona == null) return null;
            foreach (var s in ActorRegistry.All)
            {
                if (s == null || s.Team != TeamId.Player || s.Role == RoleType.Civilian) continue;
                var h = s.GetComponent<Health>();
                if (h == null || !h.IsAlive) continue;
                if (Vector3.Distance(Plano(s.transform.position), Plano(zona.position)) <= radio) return s;
            }
            return null;
        }

        static Vector3 Plano(Vector3 v) { v.y = 0f; return v; }

        static void Aviso(string mensaje, float segundos)
        {
            var boot = GameplaySceneBootstrap.Activo;
            if (boot != null && boot.ObjectiveBanner != null) boot.ObjectiveBanner.Show(mensaje, segundos);
        }
    }
}
