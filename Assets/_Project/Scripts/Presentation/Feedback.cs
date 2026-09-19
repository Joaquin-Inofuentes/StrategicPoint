using System.Collections.Generic;
using UnityEngine;
using SP.Core;
using SP.UI;

namespace SP.Presentation
{
    // Punto unico de feedback de las ACCIONES del juego (pedido: "toda
    // accion tenga feedback visual y auditivo"). Cada accion llama a
    // Feedback.Accion y sale, siempre, por tres canales a la vez:
    //   - sonido  (3D si hay posicion, 2D si es de interfaz)
    //   - visual en el MUNDO (etiqueta flotante + pulso en el piso)
    //   - visual en la INTERFAZ (aviso arriba, por la cola de alertas)
    // mas una linea en el log de flujo.
    public static class Feedback
    {
        public static readonly Color Ok = new Color(0.35f, 0.90f, 0.45f);
        public static readonly Color Cover = new Color(0.30f, 0.80f, 1f);
        public static readonly Color Warn = new Color(1f, 0.78f, 0.20f);
        public static readonly Color Bad = new Color(1f, 0.30f, 0.25f);
        public static readonly Color Info = new Color(0.85f, 0.85f, 0.90f);

        // Cuantas acciones con feedback se dispararon (los tests lo leen).
        public static int Contador { get; private set; }
        public static string UltimoTexto { get; private set; }
        public static SfxKind? UltimoSonido { get; private set; }

        public static void Reset() { Contador = 0; UltimoTexto = null; UltimoSonido = null; }

        // Solo lo visual (etiqueta en el mundo, pulso y aviso): para acciones cuyo SONIDO ya lo pone otro
        // (la recarga y el desenfunde de cada arma suenan desde WeaponHolder).
        public static void Visual(string texto, Vector3? enMundo = null, Color? color = null, bool aviso = true, bool pulso = false)
        {
            Contador++;
            UltimoTexto = texto;
            var c = color ?? Info;
            bool conTexto = !string.IsNullOrEmpty(texto);
            if (Application.isPlaying && conTexto)
            {
                if (enMundo.HasValue)
                {
                    WorldTag.Spawn(enMundo.Value + Vector3.up * 2.3f, texto, c);
                    if (pulso) OrderMarkerFx.Spawn(enMundo.Value, c, 0.9f);
                }
                if (aviso) AlertQueue.Push(texto, AlertPriority.Media, 1.3f);
            }
            if (conTexto) GameLog.Line("[FB] " + texto);
        }

        public static void Accion(SfxKind sonido, string texto, Vector3? enMundo = null, Color? color = null,
                                  bool aviso = true, bool pulso = false, float volumen = 0.6f)
        {
            Contador++;
            UltimoTexto = texto;
            UltimoSonido = sonido;
            var c = color ?? Info;

            bool conTexto = !string.IsNullOrEmpty(texto);
            if (Application.isPlaying)
            {
                if (enMundo.HasValue) AudioDirector.PlayAt(sonido, enMundo.Value, volumen, 0.7f);
                else AudioDirector.PlayUi2D(sonido, volumen, 0.7f);

                if (enMundo.HasValue && conTexto)
                {
                    WorldTag.Spawn(enMundo.Value + Vector3.up * 2.3f, texto, c);
                    if (pulso) OrderMarkerFx.Spawn(enMundo.Value, c, 0.9f);
                }
                if (aviso && conTexto) AlertQueue.Push(texto, AlertPriority.Media, 1.3f);
            }
            if (conTexto) GameLog.Line("[FB] " + texto);
        }
    }

    // Etiqueta de texto flotante en el mundo (TextMesh billboard): sube y se
    // desvanece. Pool chico y reciclado: nunca crece.
    public class WorldTag : MonoBehaviour
    {
        const int Budget = 20;
        static readonly List<WorldTag> pool = new List<WorldTag>();
        static int next;
        static Font font;

        TextMesh mesh;
        float age;
        const float Life = 1.4f;
        Vector3 start;
        Color color;

        public static int ActiveCount { get { int n = 0; foreach (var t in pool) if (t != null && t.gameObject.activeSelf) n++; return n; } }

        public static void ResetPool() { pool.Clear(); next = 0; }

        public static WorldTag Spawn(Vector3 pos, string text, Color color)
        {
            pool.RemoveAll(t => t == null);
            WorldTag tag;
            if (pool.Count < Budget)
            {
                var go = new GameObject("WorldTag");
                go.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
                tag = go.AddComponent<WorldTag>();
                if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                tag.mesh = go.AddComponent<TextMesh>();
                tag.mesh.font = font;
                tag.mesh.fontSize = 64;
                tag.mesh.characterSize = 0.06f;
                tag.mesh.anchor = TextAnchor.MiddleCenter;
                tag.mesh.alignment = TextAlignment.Center;
                var r = go.GetComponent<MeshRenderer>();
                r.sharedMaterial = font.material;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                pool.Add(tag);
            }
            else
            {
                next = (next + 1) % pool.Count;
                tag = pool[next];
            }
            tag.mesh.text = text;
            tag.color = color;
            tag.mesh.color = color;
            tag.start = pos;
            tag.age = 0f;
            tag.transform.position = pos;
            tag.gameObject.SetActive(true);
            return tag;
        }

        void LateUpdate()
        {
            age += Time.deltaTime;
            if (age >= Life) { gameObject.SetActive(false); return; }
            float k = age / Life;
            transform.position = start + Vector3.up * (k * 1.1f);
            var cam = SP.Core.CamaraPrincipal.Actual;
            if (cam != null)
            {
                transform.rotation = Quaternion.LookRotation(transform.position - cam.transform.position, cam.transform.up);
                float d = Vector3.Distance(cam.transform.position, transform.position);
                transform.localScale = Vector3.one * Mathf.Clamp(d * 0.09f, 0.7f, 4f);
            }
            var c = color; c.a = 1f - Mathf.SmoothStep(0.55f, 1f, k);
            mesh.color = c;
        }
    }
}
