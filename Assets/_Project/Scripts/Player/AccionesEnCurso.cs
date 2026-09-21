using System.Collections.Generic;
using UnityEngine;
using SP.Actors;

namespace SP.Player
{
    // Ronda 13 (puntos 2 y 3): registro de "que esta haciendo ahora cada soldado" cuando la accion tarda (reanimar,
    // curar, detonar una carga, afinar la punteria). Lo alimentan los sistemas que ya cronometran esas acciones
    // (PlayerInputDriver, PedidoDeCuracion, RescateAutomatico, DemoledorAsalto) llamando a Reportar cada frame que la
    // accion sigue viva; una accion que deja de reportarse caduca sola a los 0,35 s, asi nadie tiene que acordarse de
    // cerrarla (soltar la tecla, morir, cancelar). Lo leen las vistas (barra en el mundo entre el actor y el objetivo,
    // con temporizador, y el texto de estado abajo a la izquierda) y la suite.
    //
    // El reloj es realtimeSinceStartup a proposito: avanza igual en Play, en pausa y en la suite headless de Edit mode.
    public static class AccionesEnCurso
    {
        public struct Accion
        {
            public Soldier Actor;
            public string VerboEs;        // clave de Loc: "REVIVIENDO", "CURANDO", "DETONANDO"...
            public Transform Objetivo;    // opcional: se sigue si se mueve
            public Vector3 PuntoObjetivo;
            public float Progreso01;
            public float Restante;        // segundos que faltan
            public float Estampa;

            public Vector3 Punto => Objetivo != null ? Objetivo.position : PuntoObjetivo;
        }

        public const float Vigencia = 0.35f;

        static readonly Dictionary<int, Accion> porActor = new Dictionary<int, Accion>();
        static readonly List<int> vencidas = new List<int>();

        public static void Reportar(Soldier actor, string verboEs, Vector3 puntoObjetivo, float progreso01, float restanteSegundos, Transform objetivo = null)
        {
            if (actor == null) return;
            porActor[actor.Id] = new Accion
            {
                Actor = actor,
                VerboEs = verboEs,
                Objetivo = objetivo,
                PuntoObjetivo = puntoObjetivo,
                Progreso01 = Mathf.Clamp01(progreso01),
                Restante = Mathf.Max(0f, restanteSegundos),
                Estampa = Time.realtimeSinceStartup,
            };
        }

        public static void Terminar(Soldier actor)
        {
            if (actor != null) porActor.Remove(actor.Id);
        }

        public static void Limpiar() => porActor.Clear();

        static bool Vigente(in Accion a) =>
            a.Actor != null && Time.realtimeSinceStartup - a.Estampa <= Vigencia
            && a.Actor.Health != null && a.Actor.Health.IsAlive;

        public static bool De(Soldier actor, out Accion accion)
        {
            accion = default;
            if (actor == null || !porActor.TryGetValue(actor.Id, out var a)) return false;
            if (!Vigente(a)) { porActor.Remove(actor.Id); return false; }
            accion = a;
            return true;
        }

        // Copia las acciones vivas a "destino" (limpia las caducadas de paso). Devuelve cuantas hay.
        public static int Vigentes(List<Accion> destino)
        {
            destino.Clear();
            vencidas.Clear();
            foreach (var kv in porActor)
            {
                if (Vigente(kv.Value)) destino.Add(kv.Value); else vencidas.Add(kv.Key);
            }
            for (int i = 0; i < vencidas.Count; i++) porActor.Remove(vencidas[i]);
            return destino.Count;
        }

        public static int Cantidad { get { var l = new List<Accion>(); return Vigentes(l); } }
    }
}
