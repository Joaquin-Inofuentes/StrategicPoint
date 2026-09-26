using System.Collections.Generic;
using UnityEngine;

namespace SP.Presentation
{
    public static class DustEmitter
    {
        static ParticleSystem sys;
        static Transform root;
        static Camera mainCam;

        public static void ResetIfStale()
        {
            if (root != null) return;
            var go = new GameObject("DustEmitter");
            go.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
            root = go.transform;

            sys = go.AddComponent<ParticleSystem>();
            
            var main = sys.main;
            main.maxParticles = 20;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startSize = 1.2f;
            main.startLifetime = 0.8f;
            main.startSpeed = 0.2f;
            main.startColor = new Color(0.6f, 0.55f, 0.45f, 0.6f);
            main.gravityModifier = -0.1f; // drifts up slightly

            var emission = sys.emission;
            emission.enabled = false;
            
            var shape = sys.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.2f;

            var rend = go.GetComponent<ParticleSystemRenderer>();
            // Use the built-in particle material since SafeMaterial is opaque and unlit cube
            // By default, ParticleSystemRenderer gets a default particle material.
        }

        public static int ParticleCount => sys != null ? sys.particleCount : 0;

        public static void Emit(Vector3 position)
        {
            if (mainCam == null) mainCam = Camera.main;
            
            // Respect LOD distance
            if (mainCam != null && Vector3.SqrMagnitude(mainCam.transform.position - position) > 50f * 50f)
                return;

            ResetIfStale();

            var emitParams = new ParticleSystem.EmitParams();
            emitParams.position = position + Vector3.up * 0.1f;
            sys.Emit(emitParams, 1);
        }
    }
}
