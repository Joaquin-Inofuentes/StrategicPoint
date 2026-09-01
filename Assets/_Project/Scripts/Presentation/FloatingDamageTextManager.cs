using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using SP.Actors;
using SP.Core;

namespace SP.Presentation
{
    // Numero de daño que sube y se desvanece sobre el objetivo golpeado.
    // Antes no habia ninguna confirmacion visual de cuanto daño hacia
    // cada disparo -- el jugador no podia aprender el sistema de combate
    // ni comparar armas entre si.
    //
    // Si un mismo objetivo recibe varios impactos seguidos (rafaga, o
    // varios aliados disparandole a la vez), se ACUMULA en un solo texto
    // creciente en vez de apilar un texto por bala: eso evita el ruido
    // ilegible del combate sostenido. Cada texto sale de un pool fijo, no
    // de Instantiate por impacto, porque en un combate de muchas unidades
    // pueden dispararse decenas de estos por segundo.
    public class FloatingDamageTextManager : MonoBehaviour
    {
        [SerializeField] float mergeWindow = 0.35f;
        [SerializeField] float riseDistance = 1.2f;
        [SerializeField] float lifeTime = 0.8f;

        public const int Budget = 32;

        readonly List<Text> pool = new List<Text>();
        readonly List<int> activeOrder = new List<int>();
        readonly Dictionary<int, Entry> activeByTarget = new Dictionary<int, Entry>();

        class Entry
        {
            public Text Text;
            public int Total;
            public float MergeUntil;
            public Coroutine Routine;
        }

        IDisposable sub;

        void OnEnable()
        {
            sub?.Dispose();
            sub = EventBus.Instance.Subscribe<DamageTakenEvent>(OnDamage);
        }

        void OnDisable() => sub?.Dispose();

        // Cada elemento del pool es un Canvas WorldSpace chico con un
        // texto adentro, exactamente el mismo patron que ya usa
        // HealthBarView para las barras de vida flotantes -- asi el
        // numero vive en el mundo (sigue al objetivo) en vez de en
        // pantalla, y no necesita ningun prefab serializado: se
        // construye por codigo como el resto de la UI de este proyecto.
        // El Canvas padre es lo que se activa/desactiva (es lo que
        // corresponde a "un item del pool en uso"), no el Text hijo --
        // por eso la disponibilidad se mide en el padre.
        Text GetFromPool()
        {
            foreach (var t in pool)
                if (t != null && !t.transform.parent.gameObject.activeSelf) return t;

            if (pool.Count >= Budget)
            {
                if (activeOrder.Count == 0) return null;
                int oldestTargetId = activeOrder[0];
                activeOrder.RemoveAt(0);
                if (!activeByTarget.TryGetValue(oldestTargetId, out var oldEntry)) return null;
                if (oldEntry.Routine != null) StopCoroutine(oldEntry.Routine);
                activeByTarget.Remove(oldestTargetId);
                oldEntry.Text.transform.parent.gameObject.SetActive(false);
                return oldEntry.Text;
            }

            var canvasGO = new GameObject("FloatingDamageText", typeof(Canvas));
            canvasGO.transform.SetParent(transform, false);
            var canvas = canvasGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            var rt = canvasGO.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(2f, 0.6f);
            canvasGO.transform.localScale = Vector3.one * 0.02f;

            var textGO = new GameObject("Text", typeof(Text));
            textGO.transform.SetParent(canvasGO.transform, false);
            var text = textGO.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.alignment = TextAnchor.MiddleCenter;
            text.fontSize = 44;
            text.fontStyle = FontStyle.Bold;
            text.color = new Color(1f, 0.85f, 0.2f);
            var textRt = textGO.GetComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = Vector2.zero;
            textRt.offsetMax = Vector2.zero;

            // Es el texto mas expuesto de todo el HUD: flota directo sobre
            // el mundo 3D sin ningun panel de fondo, así que puede caer
            // sobre cielo, tierra o el cuerpo de un enemigo por igual.
            var outline = textGO.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.8f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);

            canvasGO.SetActive(false);
            pool.Add(text);
            return text;
        }

        void OnDamage(DamageTakenEvent evt)
        {
            if (!Application.isPlaying) return;
            var target = ActorRegistry.FindById(evt.TargetId);
            if (target == null) return;

            if (activeByTarget.TryGetValue(evt.TargetId, out var entry) && Time.time <= entry.MergeUntil)
            {
                entry.Total += evt.Amount;
                entry.Text.text = entry.Total.ToString();
                entry.MergeUntil = Time.time + mergeWindow;
                return;
            }

            var text = GetFromPool();
            if (text == null) return;

            text.transform.parent.gameObject.SetActive(true);
            text.text = evt.Amount.ToString();
            text.color = new Color(1f, 0.85f, 0.2f, 1f);
            text.transform.position = target.transform.position + Vector3.up * 1.9f;

            var newEntry = new Entry { Text = text, Total = evt.Amount, MergeUntil = Time.time + mergeWindow };
            activeByTarget[evt.TargetId] = newEntry;
            activeOrder.Add(evt.TargetId);
            newEntry.Routine = StartCoroutine(RiseAndFade(newEntry, evt.TargetId, target.transform));
        }

        IEnumerator RiseAndFade(Entry entry, int targetId, Transform target)
        {
            // Espera a que termine la ventana de fusion antes de empezar a
            // desvanecer: si llegan mas impactos mientras tanto, OnDamage
            // ya los sumo al mismo texto y estira MergeUntil de nuevo.
            while (Time.time <= entry.MergeUntil) yield return null;

            Vector3 start = target != null ? target.position + Vector3.up * 1.9f : entry.Text.transform.position;
            float t = 0f;
            var color = entry.Text.color;
            while (t < lifeTime)
            {
                t += Time.deltaTime;
                float k = t / lifeTime;
                entry.Text.transform.position = start + Vector3.up * (riseDistance * k);
                entry.Text.color = new Color(color.r, color.g, color.b, Mathf.Lerp(1f, 0f, k));
                var cam = Camera.main;
                if (cam != null) entry.Text.transform.rotation = cam.transform.rotation;
                yield return null;
            }

            entry.Text.transform.parent.gameObject.SetActive(false);
            entry.Text.color = new Color(color.r, color.g, color.b, 1f);
            if (activeByTarget.TryGetValue(targetId, out var current) && current == entry)
            {
                activeByTarget.Remove(targetId);
                activeOrder.Remove(targetId);
            }
        }
    }
}
