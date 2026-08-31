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

            // La esfera dice DONDE, pero se colapsa rapido y es dificil
            // leer HASTA DONDE llego. El anillo se expande exactamente
            // hasta explosionRadius sobre el suelo y se queda ahi un
            // instante: es lo que permite aprender el alcance real y
            // evitar el fuego amigo.
            SpawnShockwaveRing(position, radius);
            DecalPool.Spawn(DecalKind.Crater, new Vector3(position.x, 0.02f, position.z), Vector3.up, radius * 1.4f);
            SpawnDustCloud(position, radius);

            // Escombros del punto de impacto, del pool compartido.
            for (int i = 0; i < 10; i++)
            {
                var dir = (Random.insideUnitSphere + Vector3.up).normalized;
                DebrisPool.Spawn(position, dir * Random.Range(4f, 9f), new Color(0.4f, 0.32f, 0.24f), Random.Range(0.1f, 0.22f));
            }
        }

        public static void SpawnShockwaveRing(Vector3 center, float radius)
        {
            var go = new GameObject("ShockwaveRing");
            go.transform.position = new Vector3(center.x, 0.06f, center.z);
            var line = go.AddComponent<LineRenderer>();
            line.loop = true;
            line.useWorldSpace = true;
            line.widthMultiplier = 0.18f;
            line.positionCount = 36;
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            line.sharedMaterial = new Material(shader) { color = ExplosionColor };
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            var fx = go.AddComponent<ImpactFx>();
            fx.StartCoroutine(fx.ExpandRing(line, new Vector3(center.x, 0.06f, center.z), radius));
        }

        IEnumerator ExpandRing(LineRenderer line, Vector3 center, float targetRadius)
        {
            const float expandTime = 0.28f;
            const float holdTime = 0.12f;

            float t = 0f;
            while (t < expandTime)
            {
                t += Time.deltaTime;
                DrawRing(line, center, Mathf.Lerp(0f, targetRadius, t / expandTime));
                yield return null;
            }
            // Se planta EXACTAMENTE en el radio real antes de irse: ese
            // instante es el que enseña el alcance.
            DrawRing(line, center, targetRadius);
            yield return new WaitForSeconds(holdTime);

            t = 0f;
            while (t < 0.2f)
            {
                t += Time.deltaTime;
                line.widthMultiplier = Mathf.Lerp(0.18f, 0f, t / 0.2f);
                yield return null;
            }

            if (Application.isPlaying) Destroy(gameObject);
            else DestroyImmediate(gameObject);
        }

        static void DrawRing(LineRenderer line, Vector3 center, float radius)
        {
            int n = line.positionCount;
            for (int i = 0; i < n; i++)
            {
                float a = (float)i / n * Mathf.PI * 2f;
                line.SetPosition(i, center + new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius));
            }
        }

        static readonly Color DustColor = new Color(0.62f, 0.56f, 0.46f);

        // Nube breve que ensucia la zona y se disipa. Va por el mismo
        // presupuesto de escombros para que no se acumule: una explosion
        // no puede costar mas que su cupo.
        static void SpawnDustCloud(Vector3 center, float radius)
        {
            for (int i = 0; i < 6; i++)
            {
                var offset = Random.insideUnitSphere * radius * 0.6f;
                offset.y = Mathf.Abs(offset.y) * 0.3f;
                DebrisPool.Spawn(center + offset, Vector3.up * Random.Range(0.4f, 1.1f), DustColor, Random.Range(0.5f, 0.9f), 1.4f);
            }
        }

        // Antes todos los impactos generaban el mismo efecto: un obus de
        // tanque se sentia igual que una bala de pistola y se perdia la
        // jerarquia entre armas. El daño ya viaja en el Projectile.
        public static void SpawnScaledByDamage(Vector3 position, Color color, int damage)
        {
            float scale = Mathf.Lerp(0.35f, 1.4f, Mathf.InverseLerp(5f, 60f, damage));
            Spawn(position, color, 0.55f * scale, 0.35f);
        }

        static readonly Color ArmorSparkColor = new Color(1f, 0.9f, 0.55f);

        // EnvironmentHitKind ya distinguia el vehiculo, pero el efecto era
        // el mismo que contra el suelo: no se percibia que el blindaje
        // resiste. Chispas rapidas que salen rebotadas, no polvo de tierra.
        public static void SpawnArmorSparks(Vector3 position, Vector3 surfaceNormal)
        {
            Spawn(position, ArmorSparkColor, 0.3f, 0.12f);
            for (int i = 0; i < 5; i++)
            {
                var dir = Vector3.Slerp(surfaceNormal, Random.onUnitSphere, 0.55f).normalized;
                DebrisPool.Spawn(position, dir * Random.Range(6f, 11f), ArmorSparkColor, Random.Range(0.05f, 0.09f), 0.5f);
            }
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
