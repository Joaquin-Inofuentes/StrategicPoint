using System.Collections.Generic;
using UnityEngine;
using SP.Actors;
using SP.Ai;
using SP.Combat;
using SP.Interaction;
using SP.Vehicles;
using SP.UI;
using SP.Core;

namespace SP.Player
{
    // PlayerInputDriver (parte): detector central de IInteractable.
    //
    // Archivo NUEVO y aditivo: no reemplaza nada de PlayerInputDriver.Radial.cs,
    // solo agrega un paso previo que ResolverGestoDeQ consulta en el TAP de [Q]
    // (ver el cambio quirurgico de dos lineas en ese archivo). Si hay un
    // IInteractable valido en la mira o pegado al jugador, un TAP de [Q] lo
    // dispara; si no hay ninguno, [Q] sigue comportandose exactamente igual que
    // antes (ciclar aliado / abrir el radial de ordenes).
    //
    // A la fecha ningun objeto del proyecto implementa IInteractable todavia
    // (ver comentario en IInteractable.cs), asi que este detector hoy nunca
    // encuentra nada y el TAP de [Q] cae siempre al comportamiento previo: queda
    // listo para el proximo interactuable que se agregue.
    public partial class PlayerInputDriver
    {
        // Reusa el mismo radio que ya usan pickups/vehiculo/torreta (interactRadius,
        // ver el campo en PlayerInputDriver.cs): un IInteractable pegado al jugador
        // se detecta al mismo alcance que el resto de las interacciones por [E],
        // en vez de inventar otra distancia que hay que sincronizar aparte.
        // Busca primero en lo que se esta apuntando (raycast de mira), y si no
        // hay nada ahi, en lo mas cercano dentro de interactRadius.
        public IInteractable ObjetivoInteractuable()
        {
            var aim = UltimaMira;
            if (aim.HitTransform != null)
            {
                var enMira = aim.HitTransform.GetComponentInParent<IInteractable>();
                if (enMira != null && enMira.CanInteract(this)) return enMira;
            }

            if (Brain == null || Brain.Current == null) return null;
            var origen = Brain.Current.transform.position;
            var candidatos = Physics.OverlapSphere(origen, interactRadius, ~0, QueryTriggerInteraction.Collide);
            IInteractable mejor = null;
            float mejorDist = float.MaxValue;
            foreach (var c in candidatos)
            {
                var it = c.GetComponentInParent<IInteractable>();
                if (it == null || !it.CanInteract(this)) continue;
                float d = Vector3.Distance(origen, c.transform.position);
                if (d < mejorDist) { mejorDist = d; mejor = it; }
            }
            return mejor;
        }

        // Prompt de UI consistente: "[Q] " + GetPrompt(). Se muestra desde el
        // mismo lugar que ya arma el texto de instrucciones (BuildFpsInstruction
        // lo puede anteponer llamando a esto).
        public string PromptDeInteraccion()
        {
            var it = ObjetivoInteractuable();
            return it == null ? null : "[Q] " + it.GetPrompt(this);
        }

        // DOBLE toque de [Q] (dos toques seguidos, el segundo a menos de 1 s del primero, sin elegir nada en el radial):
        // TODA la escuadra te sigue, si o si. Un toque solo NO hace nada (antes seguian con un toque y salia sin querer).
        // Pedido explicito; reemplaza la accion contextual de la ronda 12 (atacar al apuntado, curar, seguir a un aliado,
        // subir al tanque), que sigue disponible manteniendo [Q] en el radial. Unica excepcion: si apuntas a un aliado
        // CAIDO y hay medico, UN toque lo reanima (es lo que promete el cartel "[Q] REVIVIR").
        // Devuelve en UltimaAccionRapida lo que hizo (para la suite y el tutorial).
        public string UltimaAccionRapida { get; private set; }

        // Tiempo maximo entre los dos toques.
        public const float VentanaDobleToque = 1f;
        float ultimoToqueQ = -99f;

        // Registra un toque de [Q]; devuelve true si este es el SEGUNDO seguido dentro de la ventana.
        bool EsDobleToqueDeQ()
        {
            float ahora = Time.unscaledTime;
            if (ahora - ultimoToqueQ <= VentanaDobleToque) { ultimoToqueQ = -99f; return true; }
            ultimoToqueQ = ahora;
            return false;
        }

        // Q tocada: orden CONTEXTUAL segun lo que se apunta (ver PlayerInputDriver.Contextual.cs). Antes un toque solo no hacia
        // nada y hacia falta el doble toque; el doble toque sigue sirviendo (en terreno ya es seguir).
        // Bug #096: "margen de 1 segundo para que Q ataque al enemigo que recien apuntaste". La mira salta de un enemigo al
        // terreno en cuanto el cursor se corre un poco; sin esto, un Q tardio mandaba a SEGUIR en vez de atacar. Se recuerda el
        // ultimo enemigo bajo la mira y vale un toque simple de Q sobre terreno/obstaculo durante MargenDeQ segundos.
        public const float MargenDeQ = 1f;
        Soldier ultimoEnemigoApuntado;
        float ultimoEnemigoEn = -99f;
        float ultimoToqueDeQEn = -99f;
        public Soldier UltimoEnemigoApuntado => ultimoEnemigoApuntado;

        void RecordarEnemigoApuntado(AimResult r)
        {
            if (r.Type != AimTargetType.Enemy || r.Soldier == null) return;
            ultimoEnemigoApuntado = r.Soldier;
            ultimoEnemigoEn = Time.unscaledTime;
        }

        // Para los checks y el codigo de prueba: simula que se apunto a este enemigo hace 'haceSegundos'.
        public void RecordarEnemigoApuntadoHace(Soldier s, float haceSegundos)
        {
            ultimoEnemigoApuntado = s;
            ultimoEnemigoEn = Time.unscaledTime - haceSegundos;
        }

        // Si lo apuntado ahora es "nada" (None/Ground/Cubrirse/Obstacle; NO aliados, caidos ni interactuables) y hace <= MargenDeQ
        // se apuntaba a un enemigo que sigue vivo, devuelve ESE enemigo como si se lo estuviera apuntando. 'consumir' lo olvida,
        // asi un segundo Q no lo vuelve a atacar.
        AimResult ConMargenDeQ(AimResult aim, bool consumir)
        {
            if (ultimoEnemigoApuntado == null) return aim;
            bool nada = aim.Type == AimTargetType.None || aim.Type == AimTargetType.Ground || aim.Type == AimTargetType.Cubrirse || aim.Type == AimTargetType.Obstacle;
            bool vigente = Time.unscaledTime - ultimoEnemigoEn <= MargenDeQ;
            var e = ultimoEnemigoApuntado;
            if (!nada || !vigente || e.Health == null || !e.Health.IsAlive || !e.gameObject.activeInHierarchy) return aim;
            if (consumir) ultimoEnemigoApuntado = null;
            return new AimResult { Type = AimTargetType.Enemy, Soldier = e, Point = e.transform.position, HitTransform = e.transform };
        }

        void AccionRapidaDeQ()
        {
            UltimaAccionRapida = null;
            var aim = aimCongelado ?? ultimoResultadoDeMira;
            // El margen solo vale para un toque SIMPLE: si el toque anterior fue hace menos de VentanaDobleToque es el doble toque
            // de SIGANME y manda la mira real (terreno = seguir), no el enemigo de hace un instante.
            bool toqueSimple = Time.unscaledTime - ultimoToqueDeQEn > VentanaDobleToque;
            ultimoToqueDeQEn = Time.unscaledTime;
            if (toqueSimple) aim = ConMargenDeQ(aim, true);
            UltimaAccionRapida = AccionContextualDeQ(aim);
            SesionLog.Evento(System.FormattableString.Invariant($"Q CONTEXTUAL: {UltimaAccionRapida ?? "(nada)"} · apuntando a {aim.Type}{(aim.Soldier != null ? ":" + aim.Soldier.DisplayName : "")} en ({aim.Point.x:0.0},{aim.Point.z:0.0})"));
            if (UltimaAccionRapida == null && EsDobleToqueDeQ() && SeguirTodosSiOSi() > 0) UltimaAccionRapida = "SEGUIR TODOS";
        }

        // Llamado desde el TAP de [Q] (ResolverGestoDeQ). Devuelve true si
        // encontro y ejecuto un interactuable -- en ese caso el llamador NO debe
        // hacer tambien el comportamiento previo (ciclar aliado / radial).
        public bool TryInteractuarConMira()
        {
            var it = ObjetivoInteractuable();
            if (it == null) return false;
            it.Interact(this);
            return true;
        }

        // Ronda 13 (punto 10): apuntar (click derecho) hace al arma MUCHO mas precisa. El arma lee esto en su dispersion,
        // el crecimiento por tiro y la recuperacion; la respiracion de la mira tambien baja a la mitad.
        void ActualizarPrecisionAlApuntar()
        {
            if (Rig == null || Brain == null || Brain.Current == null || Brain.Current.Weapon == null) return;
            bool ads = Rig.EstaConZoom && !TorretaFijaActiva;
            Brain.Current.Weapon.SetApuntado(ads ? Rig.AdsBlendSuave : 0f);
        }
    }
}
