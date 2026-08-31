using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using SP.Actors;
using SP.Combat;

namespace SP.UI
{
    // Item 63: marcas en el borde apuntando a los aliados que quedaron
    // fuera de encuadre.
    //
    // En primera persona el jugador perdia de vista a su escuadra apenas
    // giraba, y no habia forma de saber si estaban atras, a los costados o
    // ya muertos sin pasar a vista tactica. Estas flechas dan esa lectura
    // periferica sin cambiar de modo.
    //
    // Comparte la matematica de proyeccion con OffscreenKillMarkerView
    // (incluido el caso critico de puntos DETRAS de la camara, que hay que
    // espejar o aparecen en el borde contrario), pero es persistente: las
    // de baja son un destello puntual, estas siguen a un objetivo vivo.
    public class OffscreenAllyMarkerView : MonoBehaviour
    {
        const float EdgeMargin = 48f;
        const int MaxMarkers = 8;

        // Publico para que Unity lo serialice: asignado al construir la
        // escena, un privado se perderia en el domain reload.
        public Image[] Arrows;

        // Lo llena el driver: esta vista no sale a buscar la escuadra, para
        // no acoplarse ni hacer barridos.
        readonly List<Soldier> squad = new List<Soldier>();
        Camera cam;

        public int VisibleMarkerCount { get; private set; }

        void OnEnable()
        {
            if (Arrows == null || Arrows.Length == 0)
            {
                var found = new List<Image>();
                for (int i = 0; i < MaxMarkers; i++)
                {
                    var t = transform.Find("AllyArrow_" + i);
                    if (t != null) found.Add(t.GetComponent<Image>());
                }
                Arrows = found.ToArray();
            }
            HideAll();
        }

        void OnDisable() => HideAll();

        public void Bind(Image[] arrows)
        {
            Arrows = arrows;
            HideAll();
        }

        public void SetSquad(IEnumerable<Soldier> soldiers)
        {
            squad.Clear();
            if (soldiers == null) return;
            foreach (var s in soldiers) if (s != null) squad.Add(s);
        }

        // Un solo LateUpdate para toda la escuadra, nada por soldado.
        void LateUpdate()
        {
            if (!Application.isPlaying || Arrows == null || Arrows.Length == 0) { HideAll(); return; }
            if (cam == null) cam = Camera.main;
            if (cam == null) { HideAll(); return; }

            var parent = transform as RectTransform;
            if (parent == null) { HideAll(); return; }
            float w = parent.rect.width;
            float h = parent.rect.height;
            float halfW = Mathf.Max(0f, w * 0.5f - EdgeMargin);
            float halfH = Mathf.Max(0f, h * 0.5f - EdgeMargin);

            int used = 0;
            for (int i = 0; i < squad.Count && used < Arrows.Length; i++)
            {
                var s = squad[i];
                if (s == null || s.Health == null || !s.Health.IsAlive) continue;
                if (!s.gameObject.activeInHierarchy) continue;   // va dentro de un vehiculo

                var vp = cam.WorldToViewportPoint(s.transform.position);
                bool onScreen = vp.z > 0f && vp.x >= 0f && vp.x <= 1f && vp.y >= 0f && vp.y <= 1f;
                if (onScreen) continue;   // si se ve, no hace falta flecha

                var arrow = Arrows[used];
                if (arrow == null) continue;

                // Direccion en PIXELES del canvas, no en viewport: en
                // viewport ambos ejes van 0..1 aunque la pantalla sea 16:9,
                // y el angulo saldria sesgado hacia el eje corto.
                Vector2 dir = new Vector2((vp.x - 0.5f) * w, (vp.y - 0.5f) * h);
                if (vp.z < 0f) dir = -dir;   // detras de la camara: espejar
                if (dir.sqrMagnitude < 0.000001f) dir = Vector2.up;
                dir.Normalize();

                // Borde del RECTANGULO y no de una elipse inscrita: en las
                // diagonales la flecha quedaba muy adentro de la esquina.
                float sx = dir.x != 0f ? halfW / Mathf.Abs(dir.x) : float.MaxValue;
                float sy = dir.y != 0f ? halfH / Mathf.Abs(dir.y) : float.MaxValue;

                arrow.gameObject.SetActive(true);
                arrow.rectTransform.anchoredPosition = dir * Mathf.Min(sx, sy);
                arrow.rectTransform.localRotation =
                    Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f);

                // Color por vida: un aliado al borde de morir fuera de
                // encuadre es justamente la informacion mas urgente.
                float frac = s.Health.MaxHealth > 0 ? (float)s.Health.Current / s.Health.MaxHealth : 1f;
                arrow.color = frac > 0.6f ? new Color(0.45f, 0.75f, 0.95f, 0.85f)
                    : frac > 0.3f ? new Color(0.95f, 0.85f, 0.35f, 0.9f)
                    : new Color(0.95f, 0.3f, 0.25f, 0.95f);

                used++;
            }

            for (int i = used; i < Arrows.Length; i++)
                if (Arrows[i] != null) Arrows[i].gameObject.SetActive(false);

            VisibleMarkerCount = used;
        }

        void HideAll()
        {
            VisibleMarkerCount = 0;
            if (Arrows == null) return;
            foreach (var a in Arrows) if (a != null) a.gameObject.SetActive(false);
        }
    }
}
