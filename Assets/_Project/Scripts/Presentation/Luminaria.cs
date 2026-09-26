using UnityEngine;
using SP.Core;

namespace SP.Presentation
{
    public class Luminaria : MonoBehaviour
    {
        public int Health = 1;
        private bool isBroken = false;

        public void TakeDamage(int amount, Vector3 hitPoint)
        {
            if (isBroken) return;

            Health -= amount;
            if (Health <= 0)
            {
                isBroken = true;
                EventBus.Instance.Publish(new LuminariaRotaEvent(hitPoint));
                BreakLamp(hitPoint);
            }
        }

        private void BreakLamp(Vector3 hitPoint)
        {
            var lightComponent = GetComponent<Light>();
            if (lightComponent != null)
            {
                lightComponent.enabled = false;
            }

            var renderer = GetComponent<Renderer>();
            if (renderer != null && renderer.material != null)
            {
                renderer.material.SetColor("_EmissionColor", Color.black);
            }

            // Spawn sparks and sound
            ImpactFx.SpawnArmorSparks(hitPoint, Vector3.up); // Using ArmorSparks as it's from ImpactFxPool and matches metal sparks.
            
            // Audio is usually played through something like AudioDirector or ImpactFx.
            // But if it requires SfxKind.ImpactMetal, we should do what Projectile does.
            // Let's see how Projectile played SfxKind.ImpactMetal.
        }
    }
}
