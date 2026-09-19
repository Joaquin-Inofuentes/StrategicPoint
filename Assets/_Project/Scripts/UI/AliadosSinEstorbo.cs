using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using SP.Actors;
using SP.Core;
using SP.Combat;

namespace SP.UI
{
    // Con la camara sobre el hombro, un aliado (vivo o caido) que se para pegado a la camara llenaba media pantalla
    // con un bloque verde. Mientras este a menos de 'Distancia' de la camara, su malla deja de dibujarse (sigue
    // proyectando sombra, asi que no "desaparece" del mundo) y vuelve sola al alejarse.
    public class AliadosSinEstorbo : MonoBehaviour
    {
        public const float Distancia = 3.3f;
        static AliadosSinEstorbo instancia;
        readonly Dictionary<Renderer, ShadowCastingMode> ocultos = new Dictionary<Renderer, ShadowCastingMode>();
        readonly List<Renderer> quitar = new List<Renderer>();
        float proximo;
        public int Ocultos => ocultos.Count;

        public static AliadosSinEstorbo Instancia => instancia;

        public static void Asegurar()
        {
            if (instancia != null || !Application.isPlaying) return;
            instancia = new GameObject("AliadosSinEstorbo").AddComponent<AliadosSinEstorbo>();
        }

        void LateUpdate() => Actualizar(Camera.main);

        public void Actualizar(Camera cam)
        {
            if (Time.unscaledTime < proximo) return;
            proximo = Time.unscaledTime + 0.1f;
            if (cam == null) { Restaurar(); return; }
            var driver = FindDriver();
            var poseido = driver != null && driver.Brain != null ? driver.Brain.Current : null;
            var camPos = cam.transform.position;
            float d2 = Distancia * Distancia;
            var todos = ActorRegistry.All;
            for (int i = 0; i < todos.Count; i++)
            {
                var s = todos[i];
                if (s == null || s == poseido || s.Team != TeamId.Player) continue;
                var p = s.transform.position + Vector3.up * 0.8f;
                if ((p - camPos).sqrMagnitude > d2) continue;
                foreach (var r in s.GetComponentsInChildren<Renderer>(false))
                    if (r != null && r.shadowCastingMode != ShadowCastingMode.ShadowsOnly && !ocultos.ContainsKey(r))
                    { ocultos[r] = r.shadowCastingMode; r.shadowCastingMode = ShadowCastingMode.ShadowsOnly; }
            }
            // los que ya se alejaron vuelven a dibujarse
            quitar.Clear();
            foreach (var kv in ocultos)
            {
                var r = kv.Key;
                if (r == null) { quitar.Add(r); continue; }
                var s = r.GetComponentInParent<Soldier>();
                bool lejos = s == null || (s.transform.position + Vector3.up * 0.8f - camPos).sqrMagnitude > d2 * 1.4f || s == poseido;
                if (lejos) { r.shadowCastingMode = kv.Value; quitar.Add(r); }
            }
            foreach (var r in quitar) ocultos.Remove(r);
        }

        void Restaurar()
        {
            foreach (var kv in ocultos) if (kv.Key != null) kv.Key.shadowCastingMode = kv.Value;
            ocultos.Clear();
        }

        SP.Player.PlayerInputDriver driverCache;
        SP.Player.PlayerInputDriver FindDriver()
        {
            if (driverCache == null) driverCache = FindAnyObjectByType<SP.Player.PlayerInputDriver>();
            return driverCache;
        }
        void OnDestroy() { Restaurar(); if (instancia == this) instancia = null; }
    }
}
