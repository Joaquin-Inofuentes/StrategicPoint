using System.Collections.Generic;
using UnityEngine;

namespace SP.Presentation
{
    // Estallido generico de particulas de un solo uso, pooleado con tope
    // duro (mismo criterio que OrderMarkerFx/DebrisPool/ImpactFx). Se
    // reusa para los 3 momentos que pedian "un sistema de particulas
    // interesante" que no tenian ninguno propio todavia: recoger
    // municion, matar un enemigo y rescatar al civil -- cada uno con su
    // propio color/tamaño/velocidad, pero la misma maquina de pooling.
    public static class SparkleBurstFx
    {
        public const int Budget = 24;

        static readonly List<ParticleSystem> all = new List<ParticleSystem>();
        static readonly Queue<ParticleSystem> free = new Queue<ParticleSystem>();
        static readonly List<ParticleSystem> inUse = new List<ParticleSystem>();
        static readonly List<float> liberarEn = new List<float>(); // paralelo a inUse: Time.time en que se libera
        static Transform root;

        public static int ActiveCount { get { Purge(); return inUse.Count; } }

        static void Purge()
        {
            all.RemoveAll(x => x == null);
            for (int i = inUse.Count - 1; i >= 0; i--)
                if (inUse[i] == null) { inUse.RemoveAt(i); liberarEn.RemoveAt(i); }
        }

        static void EnsureRoot()
        {
            if (root != null) return;
            var go = new GameObject("SparkleBurstPool");
            go.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
            root = go.transform;
        }

        static ParticleSystem Create()
        {
            var go = new GameObject("SparkleBurst");
            go.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
            go.transform.SetParent(root, false);

            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.loop = false;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 64;

            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.3f;

            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;

            var sizeOverLifetime = ps.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.2f));

            var rend = go.GetComponent<ParticleSystemRenderer>();
            rend.renderMode = ParticleSystemRenderMode.Mesh;
            rend.mesh = ParticleMaterialFactory.MallaEsfera();
            rend.material = ParticleMaterialFactory.CreateTransparent(Color.white);
            rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            rend.receiveShadows = false;

            go.SetActive(false);
            all.Add(ps);
            return ps;
        }

        static ParticleSystem Take()
        {
            EnsureRoot();
            Purge();
            Tick(); // libera los estallidos ya vencidos antes de pedir/reciclar uno

            ParticleSystem p = null;
            while (free.Count > 0 && p == null) p = free.Dequeue();
            if (p == null)
            {
                if (all.Count < Budget || inUse.Count == 0) p = Create();
                else { p = inUse[0]; inUse.RemoveAt(0); liberarEn.RemoveAt(0); p.gameObject.SetActive(false); }
            }
            inUse.Add(p);
            liberarEn.Add(0f);
            return p;
        }

        // position: donde estalla. color: tinte base. duration: vida de cada particula.
        // radius: alcance del estallido. count: cuantas particulas. upBias: cuanto tira hacia arriba (gravedad simulada al reves).
        public static void Spawn(Vector3 position, Color color, float duration = 0.8f, float radius = 2.5f, int count = 26, float upBias = 2.5f)
        {
            var ps = Take();
            if (ps == null) return;
            int idx = inUse.IndexOf(ps);

            ps.transform.position = position;
            ps.gameObject.SetActive(true);

            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(duration * 0.6f, duration);
            main.startSpeed = new ParticleSystem.MinMaxCurve(radius * 0.8f, radius * 1.8f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.18f, 0.4f);
            main.startColor = color;
            main.gravityModifier = -0.35f; // negativo: en vez de caer, flota/sube -- lectura de "energia liberada", no de escombro pesado

            var velOverLifetime = ps.velocityOverLifetime;
            velOverLifetime.enabled = true;
            velOverLifetime.space = ParticleSystemSimulationSpace.World;
            velOverLifetime.y = new ParticleSystem.MinMaxCurve(upBias * 0.5f, upBias);

            var colorOverLifetime = ps.colorOverLifetime;
            var gradiente = new Gradient();
            gradiente.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.8f, 0.5f), new GradientAlphaKey(0f, 1f) });
            colorOverLifetime.color = gradiente;

            ps.Clear(true);
            ps.Emit(Mathf.Clamp(count, 1, 64));

            if (idx >= 0) liberarEn[idx] = Time.time + duration + 0.15f;
        }

        // No hay Update() propio (esto no es un MonoBehaviour): un
        // director liviano en ImpactFx-style no hace falta -- basta con
        // barrer los vencidos la proxima vez que se pide uno nuevo, y
        // ademas WorldTickers.cs (si existe) podria no conocer esta
        // clase. Se resuelve con un ticket simple: cualquier Spawn()
        // nuevo primero libera lo que ya vencio.
        public static void Tick()
        {
            Purge();
            for (int i = inUse.Count - 1; i >= 0; i--)
            {
                if (Time.time < liberarEn[i]) continue;
                var p = inUse[i];
                inUse.RemoveAt(i);
                liberarEn.RemoveAt(i);
                if (p != null) { p.gameObject.SetActive(false); free.Enqueue(p); }
            }
        }

        public static void ClearAll()
        {
            for (int i = 0; i < all.Count; i++)
                if (all[i] != null) { if (Application.isPlaying) Object.Destroy(all[i].gameObject); else Object.DestroyImmediate(all[i].gameObject); }
            all.Clear();
            free.Clear();
            inUse.Clear();
            liberarEn.Clear();
            if (root != null)
            {
                if (Application.isPlaying) Object.Destroy(root.gameObject);
                else Object.DestroyImmediate(root.gameObject);
                root = null;
            }
        }
    }
}
