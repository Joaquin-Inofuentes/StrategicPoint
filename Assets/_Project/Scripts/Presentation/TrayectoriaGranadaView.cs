using System.Collections.Generic;
using UnityEngine;
using SP.Combat;

namespace SP.Presentation
{
    // Vista previa de la granada mientras se MANTIENE [G]: la curva exacta (la misma simulacion que
    // usa la granada de verdad, Granada.Simular) dibujada como una linea con cuentas, y en el
    // punto donde cae un anillo del radio de la explosion y una marca central. Se ve desde la
    // camara sobre el hombro y desde la mira.
    public class TrayectoriaGranadaView : MonoBehaviour
    {
        const int Cuentas = 26;
        const int SegmentosAnillo = 44;

        public static TrayectoriaGranadaView Instancia { get; private set; }

        LineRenderer linea;
        LineRenderer anillo;
        LineRenderer anilloInterior;
        Transform marca;
        readonly Transform[] cuentas = new Transform[Cuentas];
        readonly List<Vector3> puntos = new List<Vector3>(96);

        public bool Visible { get; private set; }
        public Vector3 Caida { get; private set; }
        public int CantidadDePuntos => puntos.Count;

        public static TrayectoriaGranadaView Asegurar()
        {
            if (Instancia != null) return Instancia;
            var go = new GameObject("TrayectoriaGranada");
            go.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
            Instancia = go.AddComponent<TrayectoriaGranadaView>();
            Instancia.Construir();
            Instancia.Ocultar();
            return Instancia;
        }

        // Mismo bug que WorldTag (Feedback.cs): sin escena propia ni root que la
        // agrupe, sobrevive a un SceneManager.LoadScene en modo Single como huerfano.
        public static void ClearAll()
        {
            if (Instancia == null) return;
            var go = Instancia.gameObject;
            Instancia = null;
            if (Application.isPlaying) Object.Destroy(go);
            else Object.DestroyImmediate(go);
        }

        LineRenderer NuevaLinea(string nombre, Color c, float ancho, bool cerrada)
        {
            var go = new GameObject(nombre);
            go.transform.SetParent(transform, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.loop = cerrada;
            lr.startWidth = lr.endWidth = ancho;
            lr.numCornerVertices = 3;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.sharedMaterial = SafeMaterial.Create(c);
            lr.startColor = lr.endColor = c;
            return lr;
        }

        void Construir()
        {
            linea = NuevaLinea("Curva", new Color(1f, 0.85f, 0.35f), 0.07f, false);
            anillo = NuevaLinea("AnilloDeExplosion", new Color(1f, 0.35f, 0.15f), 0.14f, true);
            anillo.positionCount = SegmentosAnillo;
            anilloInterior = NuevaLinea("AnilloInterior", new Color(1f, 0.85f, 0.35f), 0.08f, true);
            anilloInterior.positionCount = SegmentosAnillo;

            var m = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(m.GetComponent<Collider>());
            m.name = "MarcaDeCaida";
            m.transform.SetParent(transform, false);
            m.transform.localScale = Vector3.one * 0.32f;
            m.GetComponent<Renderer>().sharedMaterial = SafeMaterial.Create(new Color(1f, 0.3f, 0.15f));
            m.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            marca = m.transform;

            for (int i = 0; i < Cuentas; i++)
            {
                var c = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                Destroy(c.GetComponent<Collider>());
                c.name = "Cuenta" + i;
                c.transform.SetParent(transform, false);
                c.transform.localScale = Vector3.one * 0.17f;
                c.GetComponent<Renderer>().sharedMaterial = SafeMaterial.Create(new Color(1f, 0.95f, 0.75f));
                c.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                cuentas[i] = c.transform;
            }
        }

        public void Mostrar(Vector3 origen, Vector3 velocidad, Transform ignorar)
        {
            Granada.Simular(origen, velocidad, ignorar, puntos, out var caida, out var normal);
            Caida = caida;
            Visible = true;

            linea.enabled = true;
            linea.positionCount = puntos.Count;
            for (int i = 0; i < puntos.Count; i++) linea.SetPosition(i, puntos[i]);

            // Cuentas repartidas a lo largo de la curva (cada 2 puntos = 80 ms de vuelo).
            for (int i = 0; i < Cuentas; i++)
            {
                int idx = 2 + i * 2;
                bool usar = idx < puntos.Count - 1;
                cuentas[i].gameObject.SetActive(usar);
                if (usar) cuentas[i].position = puntos[idx];
            }

            // Anillo del radio real de la explosion apoyado en el piso de caida.
            var centro = caida + normal * 0.06f;
            var eje1 = Vector3.Cross(normal, Vector3.right);
            if (eje1.sqrMagnitude < 0.01f) eje1 = Vector3.Cross(normal, Vector3.forward);
            eje1.Normalize();
            var eje2 = Vector3.Cross(normal, eje1);
            float pulso = 1f + Mathf.Sin(Time.unscaledTime * 8f) * 0.02f;
            anillo.enabled = anilloInterior.enabled = true;
            for (int i = 0; i < SegmentosAnillo; i++)
            {
                float a = i * Mathf.PI * 2f / SegmentosAnillo;
                var dir = eje1 * Mathf.Cos(a) + eje2 * Mathf.Sin(a);
                anillo.SetPosition(i, centro + dir * Granada.RadioDeExplosion * pulso);
                anilloInterior.SetPosition(i, centro + dir * Granada.RadioDeExplosion * 0.45f * pulso);
            }
            marca.gameObject.SetActive(true);
            marca.position = centro + normal * 0.05f;
        }

        public void Ocultar()
        {
            Visible = false;
            if (linea != null) linea.enabled = false;
            if (anillo != null) anillo.enabled = false;
            if (anilloInterior != null) anilloInterior.enabled = false;
            if (marca != null) marca.gameObject.SetActive(false);
            for (int i = 0; i < Cuentas; i++) if (cuentas[i] != null) cuentas[i].gameObject.SetActive(false);
        }
    }
}
