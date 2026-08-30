using System.Collections;
using UnityEngine;

namespace SP.Presentation
{
    // Mini-explosión al impactar: una esfera que se agranda rápido y
    // después se achica hasta desaparecer, en el punto exacto del choque.
    // Un color distinto por tipo de superficie (enemigo/vehículo/obstáculo/
    // suelo) para que se note a simple vista qué le pegó a qué, igual que
    // el flash de la mirilla pero en el mundo, no en la UI.
    public class ImpactFx : MonoBehaviour
    {
        public static readonly Color EnemyColor = new Color(0.95f, 0.25f, 0.15f);
        public static readonly Color VehicleColor = new Color(0.3f, 0.55f, 0.95f);
        public static readonly Color ObstacleColor = new Color(0.75f, 0.75f, 0.78f);
        public static readonly Color GroundColor = new Color(0.55f, 0.42f, 0.28f);

        static bool shaderWarmed;

        public static void Spawn(Vector3 position, Color color, float peakScale = 0.55f, float duration = 0.35f)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "ImpactFx";
            var col = go.GetComponent<Collider>();
            if (col != null)
            {
                if (Application.isPlaying) Object.Destroy(col);
                else Object.DestroyImmediate(col);
            }

            go.transform.position = position;
            go.transform.localScale = Vector3.zero;

            var rend = go.GetComponent<MeshRenderer>();
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            var mat = new Material(shader) { color = color };
            rend.sharedMaterial = mat;

            var fx = go.AddComponent<ImpactFx>();
            fx.StartCoroutine(fx.GrowAndShrink(peakScale, duration));
        }

        // Igual que OrderMarkerFx.Prewarm: compila el shader lejos y chico
        // apenas se arma el nivel, así el primer impacto real del jugador
        // no se traba (ni sale negro si justo se saca una captura ahí).
        public static void Prewarm()
        {
            if (shaderWarmed) return;
            shaderWarmed = true;
            Spawn(new Vector3(0f, -500f, 0f), EnemyColor, 0.1f, 0.05f);
        }

        IEnumerator GrowAndShrink(float peakScale, float duration)
        {
            float growTime = duration * 0.35f;
            float shrinkTime = duration - growTime;

            float t = 0f;
            while (t < growTime)
            {
                t += Time.deltaTime;
                float k = t / growTime;
                transform.localScale = Vector3.one * Mathf.Lerp(0f, peakScale, k);
                yield return null;
            }

            t = 0f;
            while (t < shrinkTime)
            {
                t += Time.deltaTime;
                float k = t / shrinkTime;
                transform.localScale = Vector3.one * Mathf.Lerp(peakScale, 0f, k);
                yield return null;
            }

            if (Application.isPlaying) Destroy(gameObject);
            else DestroyImmediate(gameObject);
        }

        public static readonly Color ExplosionColor = new Color(0.95f, 0.55f, 0.1f);

        // Granada de tanque: la esfera representa el radio de daño real
        // (no un tamaño cosmético fijo), y se "achica bruscamente" en vez
        // de un lerp parejo como el impacto chico normal -- crece rápido,
        // aguanta un instante en el pico, y colapsa de golpe.
        public static void SpawnExplosion(Vector3 position, float radius)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "ImpactFx";
            var col = go.GetComponent<Collider>();
            if (col != null)
            {
                if (Application.isPlaying) Object.Destroy(col);
                else Object.DestroyImmediate(col);
            }

            go.transform.position = position;
            go.transform.localScale = Vector3.zero;

            var rend = go.GetComponent<MeshRenderer>();
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            var mat = new Material(shader) { color = ExplosionColor };
            rend.sharedMaterial = mat;

            var fx = go.AddComponent<ImpactFx>();
            // Diámetro = 2x radio (la esfera primitiva de Unity tiene 1
            // unidad de diámetro con escala 1).
            fx.StartCoroutine(fx.ExplodeAndCollapse(radius * 2f));
        }

        IEnumerator ExplodeAndCollapse(float peakDiameter)
        {
            const float growTime = 0.12f;
            const float holdTime = 0.05f;
            const float collapseTime = 0.1f;

            float t = 0f;
            while (t < growTime)
            {
                t += Time.deltaTime;
                transform.localScale = Vector3.one * Mathf.Lerp(0f, peakDiameter, t / growTime);
                yield return null;
            }
            transform.localScale = Vector3.one * peakDiameter;

            yield return new WaitForSeconds(holdTime);

            t = 0f;
            while (t < collapseTime)
            {
                t += Time.deltaTime;
                // Ease-in cúbico: arranca lento y se precipita al final,
                // se siente más "de golpe" que un lerp lineal parejo.
                float k = t / collapseTime;
                transform.localScale = Vector3.one * Mathf.Lerp(peakDiameter, 0f, k * k * k);
                yield return null;
            }

            if (Application.isPlaying) Destroy(gameObject);
            else DestroyImmediate(gameObject);
        }
    }
}
