using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using SP.Actors;
using SP.Core;
using SP.Operacion;

namespace SP.Presentation
{
    // Bug #189 ("al apuntar X soldados a coberturas, que se iluminen esas coberturas segun la cantidad de soldados y cercania, asi se como
    // se cubriran"): en la vista tactica, con soldados seleccionados y el cursor sobre una cobertura, se encienden los puntos de cobertura que
    // tomarian (los mismos que elige IssueCoverOrderForSelection). Cada punto es un aro en el piso: mas grande y mas opaco cuanto mas
    // soldados irian a el, y verde si queda cerca de la seleccion o ambar si queda lejos. Se apaga solo si deja de refrescarse.
    public class PrevisualizacionDeCoberturas : MonoBehaviour
    {
        const int Maximo = 16;
        const float RadioBase = 0.9f, RadioPorSoldado = 0.35f, CercaDeLaSeleccion = 14f;
        static readonly Color Cerca = new Color(0.30f, 1f, 0.45f), Lejos = new Color(1f, 0.75f, 0.20f);

        static PrevisualizacionDeCoberturas instancia;
        public static int Resaltadas => instancia != null ? instancia.activas : 0;
        public static float AlphaDelPunto(int i) => instancia != null && i >= 0 && i < Maximo ? instancia.alphas[i] : 0f;
        public static float RadioDelPunto(int i) => instancia != null && i >= 0 && i < Maximo ? instancia.radios[i] : 0f;

        readonly Transform[] pads = new Transform[Maximo];
        readonly MeshRenderer[] rends = new MeshRenderer[Maximo];
        readonly float[] alphas = new float[Maximo], radios = new float[Maximo];
        MaterialPropertyBlock bloque;
        int activas;
        float ultimoRefresco = -10f;

        static PrevisualizacionDeCoberturas Asegurar()
        {
            if (instancia != null) return instancia;
            var go = new GameObject("PrevisualizacionDeCoberturas") { hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild };
            var p = instancia = go.AddComponent<PrevisualizacionDeCoberturas>();
            p.bloque = new MaterialPropertyBlock();
            for (int i = 0; i < Maximo; i++)
            {
                var q = new GameObject("Pad" + i);
                q.transform.SetParent(go.transform, false);
                q.AddComponent<MeshFilter>().sharedMesh = AnilloDeAviso.MallaPlana();
                var mr = q.AddComponent<MeshRenderer>();
                mr.sharedMaterial = AnilloDeAviso.MaterialDeAro();
                mr.shadowCastingMode = ShadowCastingMode.Off; mr.receiveShadows = false;
                q.SetActive(false);
                p.pads[i] = q.transform; p.rends[i] = mr;
            }
            return p;
        }

        // Llamar cada frame mientras el cursor apunta a la cobertura. Devuelve cuantos puntos quedaron encendidos.
        public static int Mostrar(IReadOnlyList<Soldier> seleccion, Vector3 puntoApuntado, Transform obstaculo, float radioPiso)
        {
            if (seleccion == null || seleccion.Count == 0) { Ocultar(); return 0; }
            if (!Coberturas.TryPuntoApuntado(puntoApuntado, obstaculo, radioPiso, out var basePunto, out _)) { Ocultar(); return 0; }
            var indices = Coberturas.IndicesCercanos(basePunto, seleccion.Count, 9f);
            if (indices.Count == 0) { Ocultar(); return 0; }

            var p = Asegurar();
            p.ultimoRefresco = Time.time;

            // Cuantos soldados le tocan a cada punto (si hay menos puntos que soldados, el ultimo se reparte, igual que la orden real).
            var porPunto = new Dictionary<int, int>();
            var centro = Vector3.zero;
            for (int i = 0; i < seleccion.Count; i++)
            {
                if (seleccion[i] != null) centro += seleccion[i].transform.position;
                int idx = indices[Mathf.Min(i, indices.Count - 1)];
                porPunto.TryGetValue(idx, out int n); porPunto[idx] = n + 1;
            }
            centro /= seleccion.Count;

            int k = 0;
            foreach (var kv in porPunto)
            {
                if (k >= Maximo) break;
                var pos = Coberturas.Puntos[kv.Key];
                float dist = Vector3.Distance(new Vector3(pos.x, 0f, pos.z), new Vector3(centro.x, 0f, centro.z));
                float cerca = Mathf.Clamp01(1f - dist / CercaDeLaSeleccion);
                float peso = Mathf.Clamp01(kv.Value / (float)seleccion.Count);
                float radio = RadioBase + RadioPorSoldado * Mathf.Min(kv.Value, 6);
                float pulso = 0.85f + 0.15f * Mathf.Sin(Time.time * 6f + k);
                var c = Color.Lerp(Lejos, Cerca, cerca);
                c.a = Mathf.Lerp(0.45f, 1f, peso) * pulso;
                p.alphas[k] = c.a; p.radios[k] = radio;

                var t = p.pads[k];
                t.gameObject.SetActive(true);
                t.position = new Vector3(pos.x, AnilloDeAviso.AlturaDelPisoEn(pos) + 0.14f, pos.z);
                t.localScale = new Vector3(radio * 2f, 1f, radio * 2f);
                p.bloque.SetColor("_BaseColor", c); p.bloque.SetColor("_Color", c);
                p.rends[k].SetPropertyBlock(p.bloque);
                k++;
            }
            for (int j = k; j < Maximo; j++) if (p.pads[j].gameObject.activeSelf) p.pads[j].gameObject.SetActive(false);
            p.activas = k;
            return k;
        }

        public static void Ocultar()
        {
            if (instancia == null || instancia.activas == 0) return;
            for (int i = 0; i < Maximo; i++) if (instancia.pads[i].gameObject.activeSelf) instancia.pads[i].gameObject.SetActive(false);
            instancia.activas = 0;
        }

        void Update()
        {
            if (activas > 0 && Time.time - ultimoRefresco > 0.25f) Ocultar();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reiniciar() => instancia = null;
    }
}
