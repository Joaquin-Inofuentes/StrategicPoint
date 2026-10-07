using System.Collections;
using UnityEngine;
using SP.Core;

namespace SP.Presentation
{
    public class Luminaria : MonoBehaviour
    {
        public int Health = 1;
        private bool isBroken = false;
        public bool Rota => isBroken;

        // #104: duracion del parpadeo de la lampara agonizando (luz cosmetica, la funcional se apaga en el acto).
        public const float SegundosDeParpadeo = 0.4f;
        static readonly Color VidrioClaro = new Color(0.78f, 0.93f, 1f, 1f);
        static readonly Color AscuaDeLente = new Color(0.62f, 0.10f, 0.03f, 1f);

        // La luz cuenta para la vision de los enemigos (IluminacionTactica): romper la lampara la apaga y te esconde.
        void OnEnable() { var l = GetComponent<Light>(); if (l != null && !isBroken) IluminacionTactica.Registrar(l); }
        void OnDisable() { var l = GetComponent<Light>(); if (l != null) IluminacionTactica.Quitar(l); }

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

        // La lente (hijo "Lente" del faro de reflector) o, si no hay, el renderer propio.
        Renderer Lente()
        {
            var t = transform.Find("Lente");
            if (t != null) { var r = t.GetComponent<Renderer>(); if (r != null) return r; }
            return GetComponent<Renderer>();
        }

        private void BreakLamp(Vector3 hitPoint)
        {
            // Funcional: la luz se apaga en el acto (los reflectores y la vision enemiga dependen de eso).
            var lightComponent = GetComponent<Light>();
            if (lightComponent != null) lightComponent.enabled = false;

            var centro = transform.position;
            var frente = transform.forward;
            var boca = centro + frente * 0.45f;

            // Lente rota: vidrio oscuro con el borde al rojo de las ascuas (no negro puro, se sigue viendo de noche).
            var lente = Lente();
            if (lente != null)
            {
                var m = lente.material;
                var oscuro = new Color(0.14f, 0.13f, 0.14f, 1f);
                if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", oscuro);
                if (m.HasProperty("_Color")) m.SetColor("_Color", oscuro);
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", AscuaDeLente);
            }
            // Carcasa torcida por el impacto.
            transform.rotation = transform.rotation * Quaternion.Euler(20f, 7f, 12f);

            // Estallido de vidrio: 12-16 esquirlas claras que salen hacia adelante y caen.
            int piezas = Random.Range(12, 17);
            for (int i = 0; i < piezas; i++)
            {
                var dir = (frente * Random.Range(0.5f, 1.6f) + Random.insideUnitSphere).normalized;
                dir.y = Mathf.Max(dir.y, -0.2f);
                var c = Color.Lerp(VidrioClaro, Color.white, Random.value * 0.5f);
                DebrisPool.Spawn(boca + Random.insideUnitSphere * 0.25f, dir * Random.Range(2.5f, 7f), c, Random.Range(0.05f, 0.11f), Random.Range(1.4f, 2.4f));
            }

            // Chispas mas grandes (las de siempre, dos veces, mas un puñado de brasas) y un fogonazo.
            ImpactFx.SpawnArmorSparks(boca, frente);
            ImpactFx.SpawnArmorSparks(centro, Vector3.up);
            for (int i = 0; i < 8; i++)
            {
                var dir = Vector3.Slerp(frente, Random.onUnitSphere, 0.7f).normalized;
                DebrisPool.Spawn(boca, dir * Random.Range(3f, 8f), new Color(1f, Random.Range(0.55f, 0.85f), 0.2f), Random.Range(0.07f, 0.13f), Random.Range(0.6f, 1.1f));
            }
            SpriteFx.Lanzar("spark_01", boca, new Color(1f, 0.95f, 0.7f, 1f), 0.9f, 0.3f, 0.18f, 0f, default, 0f, 6);

            // Humo breve que sube de la carcasa.
            for (int i = 0; i < 3; i++)
                SpriteFx.Lanzar("smoke_02", centro + Random.insideUnitSphere * 0.15f, new Color(0.38f, 0.37f, 0.36f, 0.7f), 0.5f, Random.Range(1.6f, 2.4f), Random.Range(1.2f, 1.8f), Random.Range(-40f, 40f), Vector3.up * Random.Range(0.8f, 1.5f), 1.2f, 1);

            // Vidrio roto + chisporroteo.
            AudioDirector.PlayAt(SfxKind.VidrioRoto, centro, 1f, 1f, PerfilEspacial.Disparo);

            // Parpadeo de la lampara agonizando (luz cosmetica de 0,4 s).
            if (isActiveAndEnabled) StartCoroutine(Parpadeo(boca, lightComponent));
        }

        IEnumerator Parpadeo(Vector3 punto, Light original)
        {
            var go = new GameObject("ParpadeoDeFaro");
            go.transform.SetParent(transform, true);
            go.transform.position = punto;
            var l = go.AddComponent<Light>();
            l.type = LightType.Point; l.range = original != null ? Mathf.Min(original.range, 14f) : 12f;
            l.color = original != null ? original.color : new Color(1f, 0.92f, 0.65f);
            l.shadows = LightShadows.None;
            float t = 0f;
            while (t < SegundosDeParpadeo)
            {
                t += Time.deltaTime;
                // Parpadeo irregular: encendido/apagado a trompicones y cada vez mas debil.
                float debil = 1f - t / SegundosDeParpadeo;
                bool on = Mathf.PerlinNoise(t * 38f, 0.37f) > 0.45f;
                l.intensity = on ? Mathf.Lerp(1.5f, 8f, debil) : 0f;
                yield return null;
            }
            Destroy(go);
        }
    }
}
