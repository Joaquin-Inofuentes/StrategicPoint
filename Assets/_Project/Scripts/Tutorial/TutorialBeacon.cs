using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using SP.Presentation;

namespace SP.Tutorial
{
    // Baliza del mundo para "mira aca": una columna translucida, un anillo en
    // el piso que pulsa y una etiqueta que siempre mira a la camara. Puede
    // seguir a un objeto que se mueve (un aliado, el tanque).
    public class TutorialBeacon : MonoBehaviour
    {
        static readonly List<TutorialBeacon> activas = new List<TutorialBeacon>();
        public static IReadOnlyList<TutorialBeacon> Activas => activas;

        public string Texto => mesh != null ? mesh.text : "";
        public Transform Sigue;
        public Vector3 Posicion;
        public float Radio = 1.4f;

        GameObject columna, anillo, etiqueta;
        TextMesh mesh, sombraMesh;
        public float AlfaEtiqueta { get; private set; } = 1f;

        // Item 11: la etiqueta se desvanece cuando cae sobre la mira (centro de la pantalla) para no tapar el blanco.
        // Radio de 55 px totalmente transparente (piso de 12 %), y a partir de 190 px del centro opaca del todo.
        public const float RadioDeMiraPx = 55f, RadioLibrePx = 190f, AlfaMinimo = 0.12f;
        public static float AlfaSegunDistanciaAMira(float distanciaPx)
            => Mathf.Lerp(AlfaMinimo, 1f, Mathf.Clamp01((distanciaPx - RadioDeMiraPx) / (RadioLibrePx - RadioDeMiraPx)));
        Color color;
        float t;
        static Font fuente;

        public static TutorialBeacon Crear(string texto, Color color, Vector3 posicion, Transform sigue = null, float radio = 1.4f, float alto = 16f)
        {
            var go = new GameObject("TutorialBeacon_" + texto);
            var b = go.AddComponent<TutorialBeacon>();
            b.Sigue = sigue;
            b.Posicion = posicion;
            b.Radio = radio;
            b.color = color;
            b.Armar(texto, alto);
            activas.Add(b);
            return b;
        }

        public static void QuitarTodas()
        {
            for (int i = activas.Count - 1; i >= 0; i--)
                if (activas[i] != null) Destroy(activas[i].gameObject);
            activas.Clear();
        }

        public void Quitar()
        {
            activas.Remove(this);
            Destroy(gameObject);
        }

        void Armar(string texto, float alto)
        {
            var mat = CoverHologram.NuevoTransparente(new Color(color.r, color.g, color.b, 0.22f));
            columna = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Destroy(columna.GetComponent<Collider>());
            columna.transform.SetParent(transform, false);
            columna.transform.localScale = new Vector3(Radio * 0.9f, alto * 0.5f, Radio * 0.9f);
            columna.transform.localPosition = new Vector3(0f, alto * 0.5f, 0f);
            var r = columna.GetComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;

            var matAnillo = CoverHologram.NuevoTransparente(new Color(color.r, color.g, color.b, 0.55f));
            anillo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Destroy(anillo.GetComponent<Collider>());
            anillo.transform.SetParent(transform, false);
            anillo.transform.localPosition = new Vector3(0f, 0.05f, 0f);
            anillo.transform.localScale = new Vector3(Radio * 2f, 0.02f, Radio * 2f);
            var ra = anillo.GetComponent<MeshRenderer>();
            ra.sharedMaterial = matAnillo;
            ra.shadowCastingMode = ShadowCastingMode.Off;

            if (fuente == null) fuente = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            etiqueta = new GameObject("Etiqueta");
            etiqueta.transform.SetParent(transform, false);
            etiqueta.transform.localPosition = new Vector3(0f, 3.4f, 0f);
            mesh = etiqueta.AddComponent<TextMesh>();
            mesh.font = fuente;
            mesh.text = texto;
            mesh.fontSize = 64;
            mesh.characterSize = 0.06f;
            mesh.anchor = TextAnchor.MiddleCenter;
            mesh.alignment = TextAlignment.Center;
            mesh.color = color;
            var rm = etiqueta.GetComponent<MeshRenderer>();
            rm.sharedMaterial = fuente.material;
            rm.shadowCastingMode = ShadowCastingMode.Off;
            mesh.fontStyle = FontStyle.Bold;

            // Sombra oscura detras del texto para que se lea sobre cualquier fondo.
            var sombra = new GameObject("Sombra");
            sombra.transform.SetParent(etiqueta.transform, false);
            sombra.transform.localPosition = new Vector3(0.05f, -0.05f, 0.02f);
            var ms = sombra.AddComponent<TextMesh>();
            ms.font = fuente; ms.text = texto; ms.fontSize = 64; ms.characterSize = 0.06f;
            ms.anchor = TextAnchor.MiddleCenter; ms.alignment = TextAlignment.Center;
            ms.fontStyle = FontStyle.Bold; ms.color = new Color(0f, 0f, 0f, 0.9f);
            sombraMesh = ms;
            var rs = sombra.GetComponent<MeshRenderer>();
            rs.sharedMaterial = fuente.material;
            rs.shadowCastingMode = ShadowCastingMode.Off;
        }

        void LateUpdate()
        {
            t += Time.deltaTime;
            if (Sigue != null)
            {
                Posicion = Sigue.position;
                var p = Posicion; p.y = 0f;
                transform.position = p;
            }
            else transform.position = Posicion;

            if (anillo != null)
            {
                float k = Radio * 2f * (1f + Mathf.Sin(t * 4f) * 0.08f);
                anillo.transform.localScale = new Vector3(k, 0.02f, k);
            }
            var cam = SP.Core.CamaraPrincipal.Actual;
            if (cam != null && etiqueta != null)
            {
                float d = Vector3.Distance(cam.transform.position, etiqueta.transform.position);
                etiqueta.transform.rotation = Quaternion.LookRotation(etiqueta.transform.position - cam.transform.position, cam.transform.up);
                etiqueta.transform.localScale = Vector3.one * Mathf.Clamp(d * 0.07f, 0.8f, 6f);
                var sp = cam.WorldToScreenPoint(etiqueta.transform.position);
                float alfa = 1f;
                if (sp.z > 0f)
                {
                    var centro = new Vector2(cam.pixelWidth * 0.5f, cam.pixelHeight * 0.5f);
                    alfa = AlfaSegunDistanciaAMira(Vector2.Distance(new Vector2(sp.x, sp.y), centro));
                }
                if (!Mathf.Approximately(alfa, AlfaEtiqueta))
                {
                    AlfaEtiqueta = alfa;
                    var cm = color; cm.a = alfa; mesh.color = cm;
                    if (sombraMesh != null) sombraMesh.color = new Color(0f, 0f, 0f, 0.9f * alfa);
                }
                // La columna se aclara al acercarse para no tapar la vista.
                var rc = columna.GetComponent<MeshRenderer>();
                float a = Mathf.Clamp01((d - 3f) / 10f) * 0.22f;
                var c = rc.sharedMaterial.GetColor("_BaseColor"); c.a = a;
                rc.sharedMaterial.SetColor("_BaseColor", c);
            }
        }

        void OnDestroy() => activas.Remove(this);
    }
}
