using UnityEngine;
using UnityEngine.Rendering;
using SP.Actors;
using SP.Combat;

namespace SP.Presentation
{
    // Pedido explicito: "cilindros para enemigos y aliados que no estan en
    // donde miro, a 5 grados izq y der de donde miro" -- una columna alta y
    // transparente sobre cada soldado (propio o enemigo, menos el poseido
    // por el jugador) para ubicarlo rapido desde lejos o fuera de la mira.
    // Se apaga sola en cuanto el jugador lo tiene encima de la mira (adentro
    // del cono de AnguloDeMira grados): ahi ya lo esta viendo/apuntando, y
    // la columna solo taparia la vista.
    public class UnitLocatorCylinder : MonoBehaviour
    {
        Soldier soldier;
        GameObject columna;
        Material material;

        const string MarkerName = "LocatorCylinder";
        public const float AnguloDeMira = 5f;
        const float DistanciaVisible = 90f;
        const float Altura = 14f;
        const float RadioVisual = 0.35f;

        static readonly Color ColorEnemigo = new Color(0.95f, 0.2f, 0.15f, 0.16f);
        static readonly Color ColorAliado = new Color(0.3f, 0.85f, 1f, 0.16f);

        // Throttle igual que EnemyAlertIndicatorView: con muchos soldados en
        // pantalla, el angulo/distancia contra camara no necesita mirarse
        // cada frame -- el jugador no gira tan rapido como para notar 0.15 s.
        const float LodCheckInterval = 0.15f;
        float lodTimer;
        bool dead;

        void OnEnable()
        {
            dead = false;
            if (soldier == null) soldier = GetComponent<Soldier>();
            if (soldier == null) { enabled = false; return; }
            if (columna == null) Construir();
        }

        void OnDisable() { if (columna != null) columna.SetActive(false); }

        void OnDestroy()
        {
            if (material == null) return;
            if (Application.isPlaying) Destroy(material);
            else DestroyImmediate(material);
            material = null;
        }

        void Construir()
        {
            columna = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            columna.name = MarkerName;
            var col = columna.GetComponent<Collider>();
            if (col != null) { if (Application.isPlaying) Destroy(col); else DestroyImmediate(col); }
            columna.transform.SetParent(transform, false);
            columna.transform.localPosition = new Vector3(0f, Altura * 0.5f, 0f);
            columna.transform.localScale = new Vector3(RadioVisual, Altura * 0.5f, RadioVisual);

            material = CoverHologram.NuevoTransparente(soldier.Team == TeamId.Enemy ? ColorEnemigo : ColorAliado);
            var r = columna.GetComponent<MeshRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = ShadowCastingMode.Off;
            columna.SetActive(false);
        }

        void Update()
        {
            if (dead || columna == null || soldier == null || soldier.Health == null) return;

            lodTimer -= Time.deltaTime;
            if (lodTimer > 0f) return;
            lodTimer = LodCheckInterval;

            if (!soldier.Health.IsAlive) { dead = true; columna.SetActive(false); return; }
            // El propio poseido no necesita ubicarse a si mismo.
            if (soldier.Brain != null && soldier.Brain.IsPossessedByPlayer) { columna.SetActive(false); return; }

            var cam = SP.Core.CamaraPrincipal.Actual;
            if (cam == null) { columna.SetActive(false); return; }

            var haciaSoldado = transform.position - cam.transform.position;
            float distancia = haciaSoldado.magnitude;
            if (distancia > DistanciaVisible) { columna.SetActive(false); return; }

            float angulo = Vector3.Angle(cam.transform.forward, haciaSoldado);
            columna.SetActive(angulo > AnguloDeMira);
        }
    }
}
