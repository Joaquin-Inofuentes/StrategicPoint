using System;
using System.Collections.Generic;
using SP.Actors;
using SP.Combat;

namespace SP.Core
{
    // Pedido explicito: "en rojo los enemigos solamente los que los aliados
    // hayan visto o vos hayas visto o te hayan disparado" -- antes el rombo
    // rojo mostraba a CUALQUIER enemigo dentro de rango, aunque nadie del
    // bando del jugador lo hubiera detectado nunca (vision de rayos X
    // gratis a traves de paredes y vegetacion). Este registro guarda que
    // enemigos ya fueron revelados, por cualquiera de las tres fuentes:
    //   - un aliado (AiBrain) lo senso visualmente (AiBrain.Sentidos.cs,
    //     SenseNearestEnemy -> aca via Revelar).
    //   - el jugador lo tuvo dentro de su cono de vision Y con linea de
    //     tiro libre (UnitLocatorCylinder.Update -> aca via Revelar).
    //   - le disparo al jugador (visto o no) -- se subscribe solo a
    //     DamageTakenEvent con blanco el soldado poseido.
    // Una vez revelado queda revelado para el resto de la mision: no hace
    // falta "seguir viendolo" (igual que en la vida real uno no olvida a un
    // enemigo que ya identifico).
    public static class InteligenciaDeEnemigos
    {
        static readonly HashSet<int> revelados = new HashSet<int>();
        static IDisposable subDano;

        // Los estaticos sobreviven a "Enter Play Mode" sin domain reload
        // (mismo patron que el resto del proyecto): sin este reset, los
        // enemigos revelados de una sesion de Play anterior seguirian
        // marcados como revelados en la siguiente.
        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reiniciar()
        {
            revelados.Clear();
            lucesDeAtacante.Clear();
            subDano?.Dispose();
            subDano = EventBus.Instance.Subscribe<DamageTakenEvent>(AlRecibirDano);
        }

        static void AlRecibirDano(DamageTakenEvent evt)
        {
            var yo = SP.Player.PlayerBrain.Activo != null ? SP.Player.PlayerBrain.Activo.Current : null;
            if (yo == null || evt.TargetId != yo.Id) return;
            var atacante = ActorRegistry.FindById(evt.AttackerId);
            if (atacante != null && atacante.Team == TeamId.Enemy) { Revelar(atacante.Id); IluminarAlAtacante(atacante); }
        }

        static readonly Dictionary<int, UnityEngine.GameObject> lucesDeAtacante = new Dictionary<int, UnityEngine.GameObject>();
        public const string NombreLuzDeAtacante = "LuzDeAtacante";
        public const float SegundosDeLuzDeAtacante = 7f;

        // Bug #168 ("no veo al enemigo"): de noche el que te dispara (francotirador atrincherado, tirador en altura) es azul oscuro sobre fondo
        // oscuro y no se lo encuentra aunque ya tenga rombo. Al herirte recibe una luz de rebote debil delante del pecho (la misma idea que la
        // luz de silueta de las torres) que se apaga sola; si dispara otra vez se renueva. No atraviesa paredes: es luz, no un resaltado.
        static void IluminarAlAtacante(Soldier atacante)
        {
            if (atacante == null || !UnityEngine.Application.isPlaying) return;
            if (lucesDeAtacante.TryGetValue(atacante.Id, out var previa) && previa != null) UnityEngine.Object.Destroy(previa);
            var go = new UnityEngine.GameObject(NombreLuzDeAtacante);
            go.transform.SetParent(atacante.transform, false);
            go.transform.localPosition = new UnityEngine.Vector3(0f, 0.55f, 1.1f);
            var l = go.AddComponent<UnityEngine.Light>();
            l.type = UnityEngine.LightType.Point; l.range = 9f; l.intensity = 4.2f;
            l.color = new UnityEngine.Color(1f, 0.82f, 0.7f);
            l.shadows = UnityEngine.LightShadows.None;
            lucesDeAtacante[atacante.Id] = go;
            UnityEngine.Object.Destroy(go, SegundosDeLuzDeAtacante);
        }

        public static void Revelar(int enemigoId) => revelados.Add(enemigoId);
        public static bool EstaRevelado(int enemigoId) => revelados.Contains(enemigoId);
    }
}
