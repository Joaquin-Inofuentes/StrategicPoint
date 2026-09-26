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

            ImpactFx.SpawnArmorSparks(hitPoint, Vector3.up);
            SP.Presentation.AudioDirector.PlayAt(SP.Core.SfxKind.ImpactMetal, hitPoint, 0.5f, 0.4f);
        }
    }
}
