using UnityEngine;
using SP.Actors;
using SP.Combat;

namespace SP.Presentation
{
    // Bug #253 ("quiero ver sus cadaveres y con animacion de pidiendo ayuda"): un aliado caido (todavia reviviable) se queda en el piso a la vista
    // y PIDE AYUDA: levanta un brazo que saluda de lado a lado, el cuerpo tiembla apenas y un "AYUDA" parpadea sobre el. Todo es procedural
    // (el pack de animaciones no trae una de pedir ayuda) y se apaga solo cuando lo reviven. Los enemigos muertos NO lo piden.
    public class AyudaDeCaido : MonoBehaviour
    {
        public const float AlturaDelBrazo = 0.35f, LargoDelBrazo = 0.75f, VelocidadDeSaludo = 5.5f, GradosDeSaludo = 28f;

        Soldier soldado;
        Transform brazo, cartel;
        TextMesh texto;
        Material matBrazo;
        float fase;

        public static bool DebePedirAyuda(Soldier s) =>
            s != null && s.Health != null && !s.Health.IsAlive && s.Team == TeamId.Player;

        public static AyudaDeCaido Activar(Soldier s)
        {
            if (s == null) return null;
            var a = s.GetComponent<AyudaDeCaido>();
            if (a == null) a = s.gameObject.AddComponent<AyudaDeCaido>();
            a.soldado = s;
            a.enabled = true;
            return a;
        }

        void Awake() { soldado = GetComponent<Soldier>(); fase = Random.Range(0f, 6.28f); }

        void OnDisable() { Ocultar(); }

        void Construir()
        {
            // Brazo: un cubo largo que cuelga de un pivote (el "hombro") a la altura del suelo del torso.
            var piv = new GameObject("AyudaDeCaido_Brazo") { hideFlags = HideFlags.DontSave };
            piv.transform.SetParent(transform, false);
            piv.transform.localPosition = new Vector3(0.25f, AlturaDelBrazo, 0.1f);
            brazo = piv.transform;
            var cubo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cubo.name = "Brazo";
            Destroy(cubo.GetComponent<Collider>());
            cubo.transform.SetParent(piv.transform, false);
            cubo.transform.localScale = new Vector3(0.14f, LargoDelBrazo, 0.14f);
            cubo.transform.localPosition = new Vector3(0f, LargoDelBrazo * 0.5f, 0f);
            var r = cubo.GetComponent<Renderer>();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var sh = Shader.Find("Universal Render Pipeline/Unlit");
            matBrazo = new Material(sh != null ? sh : Shader.Find("Sprites/Default")) { color = new Color(0.88f, 0.70f, 0.55f) };
            r.sharedMaterial = matBrazo;

            var c = new GameObject("AyudaDeCaido_Cartel") { hideFlags = HideFlags.DontSave };
            c.transform.SetParent(transform, false);
            c.transform.localPosition = new Vector3(0f, 1.55f, 0f);
            cartel = c.transform;
            texto = c.AddComponent<TextMesh>();
            texto.text = "¡AYUDA!";
            texto.anchor = TextAnchor.MiddleCenter;
            texto.alignment = TextAlignment.Center;
            texto.characterSize = 0.035f;
            texto.fontSize = 60;
            texto.color = new Color(1f, 0.3f, 0.25f);
            c.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        void Ocultar()
        {
            if (brazo != null) brazo.gameObject.SetActive(false);
            if (cartel != null) cartel.gameObject.SetActive(false);
        }

        void LateUpdate()
        {
            if (soldado == null) { soldado = GetComponent<Soldier>(); if (soldado == null) return; }
            if (!DebePedirAyuda(soldado)) { Ocultar(); return; }
            if (brazo == null || cartel == null) Construir();
            if (!brazo.gameObject.activeSelf) brazo.gameObject.SetActive(true);
            if (!cartel.gameObject.activeSelf) cartel.gameObject.SetActive(true);

            float t = Time.time * VelocidadDeSaludo + fase;
            // Brazo levantado (casi vertical) que saluda de lado a lado.
            brazo.localRotation = Quaternion.Euler(0f, 0f, 160f + Mathf.Sin(t) * GradosDeSaludo);
            // El cartel parpadea suave y mira siempre a la camara.
            float pulso = 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(Time.time * 2.4f + fase));
            var col = texto.color; col.a = pulso; texto.color = col;
            var cam = SP.Core.CamaraPrincipal.Actual;
            if (cam != null) cartel.rotation = cam.transform.rotation;
            cartel.localScale = Vector3.one * Mathf.Lerp(1f, 1.8f, cam != null ? Mathf.Clamp01((cam.transform.position - cartel.position).magnitude / 40f) : 0f);
        }
    }
}
