using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using SP.Actors;

namespace SP.UI
{
    // Item 215: tarjetas de grupo de control.
    //
    // Los grupos (Ctrl+1..9) existian pero eran INVISIBLES: el jugador
    // tenia que acordarse de memoria que habia guardado en cada numero, y
    // no habia forma de saber si al grupo 3 le quedaba alguien vivo salvo
    // recuperarlo y mirar. Esto muestra, por grupo: el numero, cuantos
    // viven y su vida promedio.
    public class GroupCardsView : MonoBehaviour
    {
        public const int SlotCount = 9;

        // Publico para que Unity lo serialice: asignado al construir la
        // escena, un campo privado se perderia en el domain reload.
        public Text[] Slots;

        // Refresco a intervalo y no por frame: son 9 slots leyendo la vida
        // de hasta 50 soldados; hacerlo 60 veces por segundo es tirar
        // trabajo para informacion que cambia despacio.
        const float RefreshInterval = 0.25f;
        float timer;

        readonly List<List<Soldier>> pending = new List<List<Soldier>>();
        bool hasData;

        void OnEnable()
        {
            if (Slots == null || Slots.Length == 0)
            {
                // Auto-reparacion por nombre, el patron del proyecto.
                var found = new List<Text>();
                for (int i = 0; i < SlotCount; i++)
                {
                    var t = transform.Find("Slot_" + (i + 1));
                    found.Add(t != null ? t.GetComponent<Text>() : null);
                }
                Slots = found.ToArray();
            }
            timer = 0f;
        }

        public void Bind(Text[] slots)
        {
            Slots = slots;
            HideAll();
        }

        // El driver le pasa los grupos; esta vista no sale a buscarlos, asi
        // no se acopla al SelectionController ni hace barridos de escena.
        public void SetGroups(IReadOnlyList<List<Soldier>> groups)
        {
            pending.Clear();
            if (groups != null)
                for (int i = 0; i < groups.Count; i++) pending.Add(groups[i]);
            hasData = true;
        }

        void Update()
        {
            if (!hasData || Slots == null) return;
            timer -= Time.unscaledDeltaTime;
            if (timer > 0f) return;
            timer = RefreshInterval;
            Refresh();
        }

        public void Refresh()
        {
            if (Slots == null) return;

            for (int i = 0; i < Slots.Length && i < SlotCount; i++)
            {
                var label = Slots[i];
                if (label == null) continue;

                List<Soldier> group = i < pending.Count ? pending[i] : null;
                int vivos;
                float vidaPromedio;
                Summarize(group, out vivos, out vidaPromedio);

                if (vivos == 0)
                {
                    // Un slot vacio se oculta en vez de mostrar "0": nueve
                    // ceros permanentes en pantalla son ruido, no dato.
                    label.gameObject.SetActive(false);
                    continue;
                }

                label.gameObject.SetActive(true);
                label.text = (i + 1) + ":  " + vivos + "  ·  " + Mathf.RoundToInt(vidaPromedio * 100f) + "%";
                // Rojo cuando el grupo esta muy castigado: es la lectura que
                // realmente importa de un vistazo.
                label.color = vidaPromedio > 0.6f ? new Color(0.85f, 0.9f, 0.85f)
                    : vidaPromedio > 0.3f ? new Color(0.95f, 0.85f, 0.35f)
                    : new Color(0.95f, 0.35f, 0.3f);
            }
        }

        void HideAll()
        {
            if (Slots == null) return;
            foreach (var s in Slots) if (s != null) s.gameObject.SetActive(false);
        }

        // Funcion pura, para poder verificarla sin escena.
        public static void Summarize(List<Soldier> group, out int vivos, out float vidaPromedio)
        {
            vivos = 0;
            vidaPromedio = 0f;
            if (group == null) return;

            float suma = 0f;
            for (int i = 0; i < group.Count; i++)
            {
                var s = group[i];
                if (s == null || s.Health == null || !s.Health.IsAlive) continue;
                vivos++;
                if (s.Health.MaxHealth > 0) suma += (float)s.Health.Current / s.Health.MaxHealth;
            }
            if (vivos > 0) vidaPromedio = suma / vivos;
        }
    }
}
