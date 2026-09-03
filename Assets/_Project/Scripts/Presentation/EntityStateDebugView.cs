using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using SP.Actors;
using SP.Ai;
using SP.Vehicles;
using SP.Core;

namespace SP.Presentation
{
    // Pedido explicito: una esfera chica flotando arriba de cada soldado
    // (aliado/enemigo), del vehiculo y de cada obstaculo, que cambia de
    // color segun su estado actual. Es PURAMENTE una ayuda de depuracion
    // visual -- no es UI de juego, no afecta gameplay, solo pinta lo que
    // la maquina de estados ya decidio, para poder confirmar a simple
    // vista (sin abrir el debugger) que un aliado que dispara "para
    // cualquier lado" en realidad SI tiene (o no tiene) un enemigo
    // trabado como target.
    //
    // Autoinstalable con el mismo patron que WorldUiDirector: no hace
    // falta cablearla a mano en cada escena (SC_Gameplay, SC_TestLevel,
    // la que arma HeadlessTestRunner). [F10] la oculta/muestra en
    // runtime si estorba.
    public class EntityStateDebugView : MonoBehaviour
    {
        public static bool Visible { get; private set; } = true;

        const float SoldierHeight = 2.15f;
        const float VehicleHeight = 2.7f;
        const float ObstacleHeight = 2.0f;
        const float SphereDiameter = 0.32f;

        static Material sharedMaterial;
        static MaterialPropertyBlock propertyBlock;
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int ColorId = Shader.PropertyToID("_Color");

        static Material SharedMaterial => sharedMaterial != null ? sharedMaterial : (sharedMaterial = SafeMaterial.CreateShared());

        static readonly Color DeadColor = new Color(0.12f, 0.12f, 0.12f);
        static readonly Color PossessedColor = Color.white;
        static readonly Color PatrolColor = new Color(0.55f, 0.65f, 0.95f);
        static readonly Color IdleColor = new Color(0.6f, 0.6f, 0.6f);
        static readonly Color MovingColor = new Color(0.35f, 0.8f, 0.95f);
        static readonly Color FollowColor = new Color(0.4f, 0.85f, 0.65f);
        static readonly Color ChaseColor = new Color(0.95f, 0.75f, 0.15f);
        static readonly Color MovingToAttackColor = new Color(0.95f, 0.55f, 0.15f);
        static readonly Color AttackColor = new Color(0.95f, 0.15f, 0.15f);

        static readonly Color VehicleEmptyColor = new Color(0.6f, 0.6f, 0.6f);
        static readonly Color VehicleOccupiedColor = new Color(0.95f, 0.75f, 0.15f);
        static readonly Color VehicleCombatColor = new Color(0.95f, 0.15f, 0.15f);
        static readonly Color VehiclePlayerColor = new Color(0.2f, 0.9f, 0.95f);
        static readonly Color VehicleAgonyColor = new Color(0.5f, 0.2f, 0.05f);
        static readonly Color VehicleDestroyedColor = new Color(0.08f, 0.08f, 0.08f);

        static readonly Color ObstacleIntactColor = new Color(0.4f, 0.85f, 0.45f);
        static readonly Color ObstacleDamagedColor = new Color(0.95f, 0.8f, 0.25f);
        static readonly Color ObstacleWreckedColor = new Color(0.95f, 0.45f, 0.15f);

        readonly Dictionary<Object, Transform> spheres = new Dictionary<Object, Transform>();
        readonly List<Object> staleKeys = new List<Object>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AutoInstall()
        {
            // -= antes de += por lo mismo que WorldUiDirector: con "Enter
            // Play Mode" sin domain reload los estaticos sobreviven y la
            // suscripcion se duplicaria en cada entrada a Play.
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            EnsureExists();
        }

        static EntityStateDebugView active;
        static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => EnsureExists();

        static void EnsureExists()
        {
            if (active != null) return;
            if (FindAnyObjectByType<EntityStateDebugView>() != null) return;
            var go = new GameObject("EntityStateDebugView");
            active = go.AddComponent<EntityStateDebugView>();
        }

        void OnDestroy()
        {
            if (active == this) active = null;
        }

        Transform GetOrCreateSphere(Object key)
        {
            if (spheres.TryGetValue(key, out var existing) && existing != null) return existing;

            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "StateDebugSphere";
            var col = go.GetComponent<Collider>();
            if (col != null)
            {
                if (Application.isPlaying) Destroy(col);
                else DestroyImmediate(col);
            }
            go.GetComponent<MeshRenderer>().sharedMaterial = SharedMaterial;
            go.transform.SetParent(transform, false);
            go.transform.localScale = Vector3.one * SphereDiameter;
            spheres[key] = go.transform;
            return go.transform;
        }

        void SetSphereColor(Transform sphere, Color c)
        {
            var rend = sphere.GetComponent<MeshRenderer>();
            if (rend == null) return;
            if (propertyBlock == null) propertyBlock = new MaterialPropertyBlock();
            rend.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor(BaseColorId, c);
            propertyBlock.SetColor(ColorId, c);
            rend.SetPropertyBlock(propertyBlock);
        }

        void Update()
        {
            if (UnityEngine.InputSystem.Keyboard.current != null &&
                UnityEngine.InputSystem.Keyboard.current.f10Key.wasPressedThisFrame)
                Visible = !Visible;

            RefreshSoldiers();
            RefreshVehicles();
            RefreshObstacles();
            CleanupStale();
        }

        void RefreshSoldiers()
        {
            var all = ActorRegistry.All;
            for (int i = 0; i < all.Count; i++)
            {
                var s = all[i];
                if (s == null) continue;
                var sphere = GetOrCreateSphere(s);
                bool show = Visible && s.gameObject.activeInHierarchy;
                sphere.gameObject.SetActive(show);
                if (!show) continue;

                sphere.position = s.transform.position + Vector3.up * SoldierHeight;
                SetSphereColor(sphere, ColorForSoldier(s));
            }
        }

        static Color ColorForSoldier(Soldier s)
        {
            if (s.Health == null || !s.Health.IsAlive) return DeadColor;

            var brain = s.Brain;
            if (brain == null) return IdleColor;
            if (brain.IsPossessedByPlayer) return PossessedColor;

            switch (brain.State)
            {
                case AiState.Dead: return DeadColor;
                case AiState.Patrol: return PatrolColor;
                case AiState.Idle: return IdleColor;
                case AiState.MovingToOrder: return MovingColor;
                case AiState.Follow: return FollowColor;
                case AiState.Chase: return ChaseColor;
                case AiState.MovingToAttackOrder: return MovingToAttackColor;
                case AiState.Attack: return AttackColor;
                default: return IdleColor;
            }
        }

        void RefreshVehicles()
        {
            var vehicles = WorldSystemsRegistry.Vehicles;
            for (int i = 0; i < vehicles.Count; i++)
            {
                var v = vehicles[i];
                if (v == null) continue;
                var sphere = GetOrCreateSphere(v);
                bool show = Visible && v.gameObject.activeInHierarchy;
                sphere.gameObject.SetActive(show);
                if (!show) continue;

                sphere.position = v.transform.position + Vector3.up * VehicleHeight;
                SetSphereColor(sphere, ColorForVehicle(v));
            }
        }

        static Color ColorForVehicle(Vehicle v)
        {
            if (v.IsDestroyed) return v.IsInAgony ? VehicleAgonyColor : VehicleDestroyedColor;
            if (v.PlayerAboard) return VehiclePlayerColor;

            var turretAi = v.GetComponentInChildren<TurretAI>();
            if (turretAi != null && turretAi.IsEngaging) return VehicleCombatColor;

            return v.OccupantCount > 0 ? VehicleOccupiedColor : VehicleEmptyColor;
        }

        void RefreshObstacles()
        {
            var obstacles = WorldSystemsRegistry.Obstacles;
            for (int i = 0; i < obstacles.Count; i++)
            {
                var o = obstacles[i];
                if (o == null) continue;
                var sphere = GetOrCreateSphere(o);
                bool show = Visible && o.gameObject.activeInHierarchy && !o.IsCollapsed;
                sphere.gameObject.SetActive(show);
                if (!show) continue;

                sphere.position = o.transform.position + Vector3.up * ObstacleHeight;
                SetSphereColor(sphere, ColorForObstacle(o));
            }
        }

        static Color ColorForObstacle(ObstacleMarker o)
        {
            switch (o.Stage)
            {
                case 0: return ObstacleIntactColor;
                case 1: return ObstacleDamagedColor;
                default: return ObstacleWreckedColor;
            }
        }

        // Barre esferas cuya fuente (soldado/vehiculo/obstaculo) ya no
        // existe -- un soldado destruido de verdad (no solo muerto:
        // Destroy() real, poco comun pero posible) dejaria la esfera
        // huerfana flotando para siempre si no se limpia.
        void CleanupStale()
        {
            staleKeys.Clear();
            foreach (var kv in spheres)
                if (kv.Key == null || kv.Value == null) staleKeys.Add(kv.Key);

            for (int i = 0; i < staleKeys.Count; i++)
            {
                if (spheres.TryGetValue(staleKeys[i], out var t) && t != null)
                {
                    if (Application.isPlaying) Destroy(t.gameObject);
                    else DestroyImmediate(t.gameObject);
                }
                spheres.Remove(staleKeys[i]);
            }
        }
    }
}
