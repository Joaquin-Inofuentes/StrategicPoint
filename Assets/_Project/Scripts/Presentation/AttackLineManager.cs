using System.Collections.Generic;
using UnityEngine;
using SP.Core;
using SP.Actors;
using SP.Ai;
using SP.CameraSystem;
using SP.Player;

namespace SP.Presentation
{
    public class AttackLineManager : MonoBehaviour
    {
        static readonly Color LineColor = new Color(0.9f, 0.15f, 0.12f);
        static readonly Color TacBlue = new Color(0.12f, 0.45f, 0.9f);
        static readonly Color TacRed = new Color(0.9f, 0.15f, 0.12f);

        readonly Dictionary<int, LineRenderer> lines = new Dictionary<int, LineRenderer>();
        
        LineRenderer[] tacLines = new LineRenderer[16];
        Dictionary<int, float> recentEnemies = new Dictionary<int, float>();
        System.IDisposable damageSub;

        void OnEnable()
        {
            damageSub = EventBus.Instance.Subscribe<DamageTakenEvent>(OnDamageTaken);
        }

        void OnDisable()
        {
            damageSub?.Dispose();
        }

        void OnDamageTaken(DamageTakenEvent e)
        {
            Soldier p = GetPlayer();
            if (p != null && (e.TargetId == p.Id || e.AttackerId == p.Id))
            {
                int enemyId = e.TargetId == p.Id ? e.AttackerId : e.TargetId;
                var s = ActorRegistry.Get(enemyId);
                if (s != null && s.Team != TeamId.Player) recentEnemies[enemyId] = Time.time;
            }
        }

        Soldier GetPlayer()
        {
            foreach (var s in ActorRegistry.All)
                if (s != null && s.Brain != null && s.Brain.IsPossessedByPlayer) return s;
            return null;
        }

        void Update()
        {
            var rig = CameraRig.Instance;
            if (rig != null && rig.Mode == ControlMode.Rts)
            {
                ClearTacLines();
                UpdateAttackLines();
                return;
            }

            if (lines.Count > 0) RemoveAllLines();

            bool cHeld = KeyBindings.IsPressed(KeyBindings.VerTactico);
            if (cHeld) UpdateTacLines();
            else ClearTacLines();
        }

        void UpdateAttackLines()
        {
            foreach (var soldier in ActorRegistry.All)
            {
                if (soldier == null) continue;
                var brain = soldier.Brain;
                bool hasEnemyLocked = brain != null && brain.CurrentTarget != null &&
                    (brain.State == AiState.Attack || brain.State == AiState.Chase || brain.State == AiState.MovingToAttackOrder) &&
                    soldier.gameObject.activeInHierarchy;

                if (!hasEnemyLocked)
                {
                    RemoveLine(soldier.Id);
                    continue;
                }

                if (!lines.TryGetValue(soldier.Id, out var lr) || lr == null)
                {
                    lr = CreateLine(LineColor);
                    lines[soldier.Id] = lr;
                }

                lr.SetPosition(0, soldier.transform.position + Vector3.up * 0.5f);
                lr.SetPosition(1, brain.CurrentTarget.transform.position + Vector3.up * 0.5f);
            }
        }

        void UpdateTacLines()
        {
            var p = GetPlayer();
            if (p == null) { ClearTacLines(); return; }

            int lidx = 0;
            // Allies
            foreach (var s in ActorRegistry.All)
            {
                if (s == p || s == null || s.Team != TeamId.Player || s.Health == null || !s.Health.IsAlive || !s.gameObject.activeInHierarchy) continue;
                if (lidx >= 16) break;
                DrawTac(lidx++, p.transform.position, s.transform.position, TacBlue);
            }
            
            // Enemies
            foreach (var s in ActorRegistry.All)
            {
                if (s == null || s.Team == TeamId.Player || s.Health == null || !s.Health.IsAlive || !s.gameObject.activeInHierarchy) continue;
                if (lidx >= 16) break;

                bool visible = false; // Could check visibility
                bool recent = recentEnemies.TryGetValue(s.Id, out float t) && (Time.time - t) <= 6f;
                if (recent || visible)
                {
                    DrawTac(lidx++, p.transform.position, s.transform.position, TacRed);
                }
            }

            for (int i = lidx; i < 16; i++)
            {
                if (tacLines[i] != null) tacLines[i].gameObject.SetActive(false);
            }
        }

        void DrawTac(int idx, Vector3 a, Vector3 b, Color c)
        {
            if (tacLines[idx] == null) {
                tacLines[idx] = CreateLine(c);
            }
            var lr = tacLines[idx];
            lr.gameObject.SetActive(true);
            lr.startColor = c; lr.endColor = c;
            lr.material.color = c;
            lr.SetPosition(0, a + Vector3.up * 0.5f);
            lr.SetPosition(1, b + Vector3.up * 0.5f);
        }

        void ClearTacLines()
        {
            for (int i=0; i<16; i++) {
                if (tacLines[i] != null) tacLines[i].gameObject.SetActive(false);
            }
        }

        void RemoveAllLines()
        {
            foreach (var actorId in new List<int>(lines.Keys)) RemoveLine(actorId);
        }

        void RemoveLine(int actorId)
        {
            if (!lines.TryGetValue(actorId, out var lr)) return;
            lines.Remove(actorId);
            if (lr == null) return;
            
            var mat = Application.isPlaying ? lr.material : lr.sharedMaterial;
            if (Application.isPlaying) { if (mat != null) Destroy(mat); Destroy(lr.gameObject); }
            else { if (mat != null) DestroyImmediate(mat); DestroyImmediate(lr.gameObject); }
        }

        static LineRenderer CreateLine(Color c)
        {
            var go = new GameObject("AttackLine");
            var lr = go.AddComponent<LineRenderer>();
            lr.positionCount = 2;
            lr.widthMultiplier = 0.05f;
            lr.useWorldSpace = true;
            lr.material = SafeMaterial.Create(c);
            lr.startColor = c;
            lr.endColor = c;
            return lr;
        }

        public static void Prewarm()
        {
            var lr = CreateLine(LineColor);
            lr.transform.position = new Vector3(0f, -500f, 0f);
            lr.SetPosition(0, lr.transform.position);
            lr.SetPosition(1, lr.transform.position + Vector3.right * 0.01f);
            var mat = Application.isPlaying ? lr.material : lr.sharedMaterial;
            if (Application.isPlaying) { if (mat != null) Destroy(mat); Object.Destroy(lr.gameObject); }
            else { if (mat != null) DestroyImmediate(mat); Object.DestroyImmediate(lr.gameObject); }
        }
    }
}
