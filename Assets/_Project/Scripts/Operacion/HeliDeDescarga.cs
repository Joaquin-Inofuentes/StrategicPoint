using System.Collections.Generic;
using UnityEngine;

namespace SP.Operacion
{
    // Pedido 2026-11-07 ("los portones de la zona del tanque aparecen de la nada: quiero que sean helicopteros que sueltan enemigos en la zona"):
    // cada refuerzo llega ahora por aire. Un helicoptero (copia visual del Halcon, SIN armas ni vida: no es un blanco) entra, queda suspendido 14 m
    // sobre el punto con dos sogas colgando y polvo en el piso mientras sale el soldado, y se retira. Reemplaza a la compuerta de EntradaDeReserva.
    public sealed class HeliDeDescarga : MonoBehaviour
    {
        public const float Altura = 14f, SegundosSuspendido = 4.2f, DistanciaDeEntrada = 110f, Velocidad = 46f;
        public const int MaximoSimultaneos = 3;
        public static int Activos { get; private set; }
        public static int Lanzados { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reiniciar() { Activos = 0; Lanzados = 0; }

        Vector3 punto, dirEntrada; float t, tLlego = -1f; Transform rotorP, rotorC; LineRenderer[] sogas; bool saliendo;

        public static HeliDeDescarga Soltar(Vector3 punto)
        {
            var d = OperacionDirector.Instancia;
            var plantilla = d != null ? d.halcon : null;
            if (plantilla == null || Activos >= MaximoSimultaneos) return null;
            var go = Instantiate(plantilla.gameObject, plantilla.parent);   // la plantilla esta INACTIVA: nada corre hasta que se limpia el clon
            go.name = "HeliDeDescarga";
            for (int pasada = 0; pasada < 4; pasada++)
            {
                foreach (var c in go.GetComponentsInChildren<Collider>(true)) DestroyImmediate(c);
                foreach (var m in go.GetComponentsInChildren<MonoBehaviour>(true)) if (m != null) { try { DestroyImmediate(m); } catch { } }
                foreach (var r in go.GetComponentsInChildren<Rigidbody>(true)) if (r != null) { try { DestroyImmediate(r); } catch { } }
                foreach (var a in go.GetComponentsInChildren<AudioSource>(true)) if (a != null) DestroyImmediate(a);
            }
            var h = go.AddComponent<HeliDeDescarga>();
            h.punto = punto;
            var centro = -new Vector3(punto.x, 0f, punto.z); if (centro.sqrMagnitude < 1f) centro = Vector3.forward;
            float ang = Random.Range(0f, Mathf.PI * 2f);
            h.dirEntrada = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang));
            go.transform.position = punto + h.dirEntrada * DistanciaDeEntrada + Vector3.up * (Altura + 10f);
            go.SetActive(true);
            h.rotorP = go.transform.Find("RotorPrincipal"); h.rotorC = go.transform.Find("RotorCola");
            h.sogas = new LineRenderer[2];
            for (int i = 0; i < 2; i++)
            {
                var so = new GameObject("Soga").AddComponent<LineRenderer>(); so.transform.SetParent(go.transform, false);
                so.positionCount = 2; so.startWidth = so.endWidth = 0.06f; so.useWorldSpace = true; so.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                so.sharedMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { color = new Color(0.12f, 0.1f, 0.08f) };
                so.enabled = false; h.sogas[i] = so;
            }
            Activos++; Lanzados++;
            return h;
        }

        void OnDestroy() { Activos = Mathf.Max(0, Activos - 1); }

        void Update()
        {
            float dt = Time.deltaTime; t += dt;
            if (rotorP != null) rotorP.Rotate(0f, 1500f * dt, 0f, Space.Self);
            if (rotorC != null) rotorC.Rotate(1800f * dt, 0f, 0f, Space.Self);
            var meta = punto + Vector3.up * Altura;
            if (!saliendo)
            {
                transform.position = Vector3.MoveTowards(transform.position, meta, Velocidad * dt);
                var mira = -dirEntrada; transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(mira, Vector3.up) * Quaternion.Euler(tLlego < 0f ? 9f : 0f, 0f, 0f), 1f - Mathf.Exp(-3f * dt));
                if (tLlego < 0f && (transform.position - meta).sqrMagnitude < 4f) { tLlego = t; foreach (var s in sogas) s.enabled = true; }
                if (tLlego >= 0f && t - tLlego >= SegundosSuspendido) { saliendo = true; foreach (var s in sogas) s.enabled = false; }
            }
            else
            {
                transform.position += (dirEntrada + Vector3.up * 0.35f).normalized * Velocidad * 1.3f * dt;
                if ((transform.position - punto).sqrMagnitude > (DistanciaDeEntrada * 1.1f) * (DistanciaDeEntrada * 1.1f) || t > 40f) { Destroy(gameObject); return; }
            }
            if (sogas != null && sogas[0] != null && sogas[0].enabled)
            {
                var d = transform.right * 0.7f;
                sogas[0].SetPosition(0, transform.position - Vector3.up * 0.8f - d); sogas[0].SetPosition(1, punto + d * 0.3f);
                sogas[1].SetPosition(0, transform.position - Vector3.up * 0.8f + d); sogas[1].SetPosition(1, punto - d * 0.3f);
                if (Time.frameCount % 12 == 0) SP.Presentation.ImpactFx.Spawn(punto + new Vector3(Random.Range(-1.5f, 1.5f), 0.3f, Random.Range(-1.5f, 1.5f)), new Color(0.6f, 0.55f, 0.45f), 1.6f, 0.6f);
            }
        }
    }
}
