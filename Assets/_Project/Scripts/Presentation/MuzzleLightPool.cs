using System.Collections.Generic;
using UnityEngine;

namespace SP.Presentation
{
    // Un destello plano pegado a la boca no ilumina nada. Una Light real
    // encendida uno o dos frames cambia por completo la percepcion de
    // potencia, sobre todo con poca luz ambiente.
    //
    // Nunca se instancia una Light por disparo: crear y destruir luces en
    // combate es de lo mas caro que hay (fuerza recalcular el conjunto de
    // luces por objeto). Pool chico y reciclado, igual que los escombros.
    public static class MuzzleLightPool
    {
        public const int Budget = 6;
        const float FlashSeconds = 0.05f;

        static readonly List<MuzzleFlashLight> all = new List<MuzzleFlashLight>();
        static Transform root;

        public static int TotalCount => all.Count;
        public static int ActiveCount
        {
            get
            {
                int n = 0;
                foreach (var l in all) if (l != null && l.IsOn) n++;
                return n;
            }
        }

        static void EnsureRoot()
        {
            if (root != null) return;
            all.RemoveAll(x => x == null);
            var go = new GameObject("MuzzleLightPool");
            // Ver DebrisPool: los flags no se heredan y DontSave impediria
            // destruirlo al cargar escena.
            go.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
            root = go.transform;
        }

        public static void Flash(Vector3 position, Color color, float intensity = 9f, float range = 14f)
        {
            EnsureRoot();
            all.RemoveAll(x => x == null);

            MuzzleFlashLight pick = null;
            foreach (var l in all) if (!l.IsOn) { pick = l; break; }

            if (pick == null)
            {
                if (all.Count < Budget) pick = Create();
                else
                {
                    pick = all[0];
                    float earliestOffAt = pick != null ? pick.OffAt : float.MaxValue;
                    for (int i = 1; i < all.Count; i++)
                    {
                        if (all[i] != null && all[i].OffAt < earliestOffAt)
                        {
                            earliestOffAt = all[i].OffAt;
                            pick = all[i];
                        }
                    }
                }
            }

            pick.Flash(position, color, intensity, range, FlashSeconds);
        }

        static MuzzleFlashLight Create()
        {
            var go = new GameObject("MuzzleFlashLight");
            go.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
            go.transform.SetParent(root, false);
            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.shadows = LightShadows.None;
            light.enabled = false;
            var f = go.AddComponent<MuzzleFlashLight>();
            all.Add(f);
            return f;
        }
    }

    public class MuzzleFlashLight : MonoBehaviour
    {
        Light lightRef;
        float offAt;
        public bool IsOn => lightRef != null && lightRef.enabled;
        public float OffAt => offAt;

        public void Flash(Vector3 position, Color color, float intensity, float range, float seconds)
        {
            if (lightRef == null) lightRef = GetComponent<Light>();
            transform.position = position;
            lightRef.color = color;
            lightRef.intensity = intensity;
            lightRef.range = range;
            lightRef.enabled = true;
            offAt = Time.time + seconds;
        }

        void Update()
        {
            if (lightRef == null || !lightRef.enabled) return;
            if (Time.time >= offAt) lightRef.enabled = false;
        }

        // El test headless corre en Edit mode, donde Time.time no avanza
        // entre llamadas: hace falta poder apagarla a mano.
        public void ForceOff()
        {
            if (lightRef == null) lightRef = GetComponent<Light>();
            if (lightRef != null) lightRef.enabled = false;
        }
    }
}
