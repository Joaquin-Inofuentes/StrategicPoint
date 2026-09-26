using UnityEngine;
using System.Collections.Generic;
using SP.Core;
using SP.Actors;

namespace SP.Presentation
{
    [DefaultExecutionOrder(100)]
    public class MinimapAttackerRings : MonoBehaviour
    {
        public static MinimapAttackerRings Activo { get; private set; }
        public static void ReiniciarActivo() => Activo = null;

        class RingState
        {
            public LineRenderer Line;
            public float TimeAlive;
            public bool Active;
        }

        const int MAX_RINGS = 4;
        RingState[] rings;
        System.IDisposable damageSubscription;

        void Awake()
        {
            Activo = this;
            rings = new RingState[MAX_RINGS];
            int minimapLayer = LayerMask.NameToLayer("Minimap");
            if (minimapLayer == -1) minimapLayer = 0;

            // Usa un material simple para que se vea el color
            Material lineMat = new Material(Shader.Find("Sprites/Default"));
            Color ringColor = Color.red;

            for (int i = 0; i < MAX_RINGS; i++)
            {
                var go = new GameObject($"MinimapAttackerRing_{i}");
                go.transform.SetParent(transform);
                go.layer = minimapLayer;

                var lr = go.AddComponent<LineRenderer>();
                lr.useWorldSpace = true;
                lr.loop = true;
                lr.positionCount = 32;
                lr.startWidth = 0.4f;
                lr.endWidth = 0.4f;
                lr.material = lineMat;
                lr.startColor = ringColor;
                lr.endColor = ringColor;
                
                go.SetActive(false);
                rings[i] = new RingState { Line = lr, Active = false };
            }
        }

        void OnEnable()
        {
            damageSubscription = EventBus.Subscribe<DamageTakenEvent>(OnDamageTaken);
        }

        void OnDisable()
        {
            damageSubscription?.Dispose();
            damageSubscription = null;
        }

        void OnDestroy()
        {
            if (Activo == this) Activo = null;
        }

        void OnDamageTaken(DamageTakenEvent evt)
        {
            if (evt.AttackerId <= 0) return;

            var brain = SP.Player.PlayerBrain.Activo;
            if (brain == null || brain.Current == null) return;
            if (evt.TargetId != brain.Current.Id) return;

            Vector3 attackerPos = Vector3.zero;
            bool found = false;
            
            // Buscar la posicion del atacante
            var soldiers = Object.FindObjectsByType<Soldier>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            foreach (var s in soldiers)
            {
                if (s.Id == evt.AttackerId)
                {
                    attackerPos = s.transform.position;
                    found = true;
                    break;
                }
            }

            if (!found) return;

            SpawnRing(attackerPos);
        }

        void SpawnRing(Vector3 pos)
        {
            RingState oldest = null;
            RingState toUse = null;
            float maxTime = -1f;

            foreach (var r in rings)
            {
                if (!r.Active)
                {
                    toUse = r;
                    break;
                }
                if (r.TimeAlive > maxTime)
                {
                    maxTime = r.TimeAlive;
                    oldest = r;
                }
            }

            if (toUse == null) toUse = oldest;
            
            toUse.Active = true;
            toUse.TimeAlive = 0f;
            toUse.Line.gameObject.SetActive(true);
            toUse.Line.transform.position = pos;
            UpdateRing(toUse);
        }

        void Update()
        {
            for (int i = 0; i < MAX_RINGS; i++)
            {
                var r = rings[i];
                if (!r.Active) continue;

                r.TimeAlive += Time.deltaTime;
                if (r.TimeAlive >= 1.2f)
                {
                    r.Active = false;
                    r.Line.gameObject.SetActive(false);
                }
                else
                {
                    UpdateRing(r);
                }
            }
        }

        void UpdateRing(RingState r)
        {
            float t = r.TimeAlive / 1.2f;
            float radius = Mathf.Lerp(0f, 8f, t);
            float alpha = 1f - t;
            
            Color c = r.Line.startColor;
            c.a = alpha;
            r.Line.startColor = c;
            r.Line.endColor = c;

            Vector3 center = r.Line.transform.position;
            center.y = 55f; // Altura tipica del minimapa

            for (int i = 0; i < 32; i++)
            {
                float angle = i * Mathf.PI * 2f / 32f;
                r.Line.SetPosition(i, center + new Vector3(Mathf.Cos(angle) * radius, 0, Mathf.Sin(angle) * radius));
            }
        }
    }
}
