using UnityEngine;
using UnityEngine.UI;
using SP.Core;

namespace SP.Mision
{
    // Cartel de la mision (arriba al centro, debajo de "ENEMIGOS · ESCUADRA"): capitulo,
    // objetivo con su distancia o su temporizador, barra de progreso y la dificultad elegida.
    public class MisionHud : MonoBehaviour
    {
        MisionDirector director;
        Text titulo, detalle, dificultad;
        RectTransform relleno, barra;
        Image panel;

        public string TextoTitulo => titulo != null ? titulo.text : "";
        public string TextoDetalle => detalle != null ? detalle.text : "";
        public string TextoDificultad => dificultad != null ? dificultad.text : "";

        public static MisionHud Crear(MisionDirector d)
        {
            var driver = FindAnyObjectByType<SP.Player.PlayerInputDriver>();
            Transform raiz = driver != null && driver.AimUiRef != null ? driver.AimUiRef.transform.parent : null;
            if (raiz == null) return null;

            var go = new GameObject("MisionHud", typeof(RectTransform), typeof(Image), typeof(MisionHud));
            go.transform.SetParent(raiz, false);
            var h = go.GetComponent<MisionHud>();
            h.director = d;
            h.panel = go.GetComponent<Image>();
            h.panel.color = new Color(0.05f, 0.07f, 0.1f, 0.62f);
            h.panel.raycastTarget = false;
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(20f, -20f);
            rt.sizeDelta = new Vector2(690f, 92f);

            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            h.titulo = Texto(go.transform, font, 17, new Vector2(0f, 30f), new Vector2(670f, 24f), new Color(1f, 0.82f, 0.3f));
            h.detalle = Texto(go.transform, font, 21, new Vector2(0f, 6f), new Vector2(670f, 28f), Color.white);
            h.dificultad = Texto(go.transform, font, 13, new Vector2(0f, -36f), new Vector2(670f, 18f), new Color(0.72f, 0.8f, 0.9f));

            var fondoBarra = new GameObject("Barra", typeof(RectTransform), typeof(Image));
            fondoBarra.transform.SetParent(go.transform, false);
            fondoBarra.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);
            h.barra = fondoBarra.GetComponent<RectTransform>();
            h.barra.anchorMin = h.barra.anchorMax = new Vector2(0.5f, 0.5f);
            h.barra.anchoredPosition = new Vector2(0f, -19f);
            h.barra.sizeDelta = new Vector2(480f, 7f);
            var fill = new GameObject("Relleno", typeof(RectTransform), typeof(Image));
            fill.transform.SetParent(fondoBarra.transform, false);
            fill.GetComponent<Image>().color = new Color(1f, 0.82f, 0.3f);
            h.relleno = fill.GetComponent<RectTransform>();
            h.relleno.anchorMin = Vector2.zero; h.relleno.anchorMax = new Vector2(0f, 1f);
            h.relleno.pivot = new Vector2(0f, 0.5f);
            h.relleno.offsetMin = h.relleno.offsetMax = Vector2.zero;
            h.relleno.sizeDelta = new Vector2(0f, 0f);

            var p = Dificultad.PerfilActual;
            h.dificultad.text = $"DIFICULTAD {p.Nombre} · enemigos vida {Pct(p.VidaEnemigos)} daño {Pct(p.DanoEnemigos)} · aliados {Pct(p.VidaAliados)}/{Pct(p.DanoAliados)} · vos {Pct(p.VidaJugador)}/{Pct(p.DanoJugador)}";
            h.Refrescar();
            return h;
        }

        static string Pct(float f)
        {
            int p = Mathf.RoundToInt((f - 1f) * 100f);
            return p == 0 ? "x1" : (p > 0 ? "+" : "") + p + "%";
        }

        static Text Texto(Transform padre, Font font, int tam, Vector2 pos, Vector2 caja, Color color)
        {
            var go = new GameObject("Texto", typeof(RectTransform), typeof(Text), typeof(Shadow));
            go.transform.SetParent(padre, false);
            var t = go.GetComponent<Text>();
            t.font = font; t.fontSize = tam; t.fontStyle = FontStyle.Bold; t.color = color;
            t.alignment = TextAnchor.MiddleCenter; t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Overflow; t.verticalOverflow = VerticalWrapMode.Overflow;
            var rt = t.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos; rt.sizeDelta = caja;
            return t;
        }

        void SetBarra(float f01, Color c)
        {
            if (relleno == null) return;
            relleno.sizeDelta = new Vector2(barra.sizeDelta.x * Mathf.Clamp01(f01), 0f);
            relleno.GetComponent<Image>().color = c;
        }

        public void Refrescar()
        {
            if (director == null || titulo == null) return;
            var f = director.Fase;
            bool visible = f != FaseDeMision.Victoria && f != FaseDeMision.Derrota;
            if (panel.enabled != visible) { panel.enabled = visible; foreach (var t in GetComponentsInChildren<Graphic>(true)) t.enabled = visible; }
            if (!visible) return;

            float d = director.DistanciaAlObjetivo();
            var amarillo = new Color(1f, 0.82f, 0.3f);
            switch (f)
            {
                case FaseDeMision.Infiltrar:
                    titulo.text = "OBJETIVO 1/4 · INFILTRAR";
                    detalle.text = $"Avanza entre las lineas enemigas hasta el CENTRO de la aldea · {Mathf.RoundToInt(d)} m";
                    SetBarra(0f, amarillo);
                    break;
                case FaseDeMision.Resistir:
                {
                    titulo.text = "OBJETIVO 2/4 · RESISTIR EN EL CENTRO";
                    bool dentro = d <= director.RadioResistencia;
                    detalle.text = dentro ? $"Resiste {Mathf.CeilToInt(director.Restante)} s · oleadas {director.OleadasLanzadas}/3"
                                          : "VOLVE AL CENTRO: el temporizador esta detenido";
                    SetBarra(1f - director.Restante / director.SegundosDeResistencia, dentro ? amarillo : new Color(1f, 0.3f, 0.25f));
                    break;
                }
                case FaseDeMision.Rescatar:
                    titulo.text = "OBJETIVO 3/4 · RESCATAR AL CIVIL";
                    detalle.text = $"Acercate al civil y quedate junto a el · {Mathf.RoundToInt(d)} m";
                    SetBarra(0f, amarillo);
                    break;
                default:
                {
                    titulo.text = "OBJETIVO 4/4 · ESCAPAR EN EL HELICOPTERO";
                    string civil = director.Civil != null && director.Civil.Health != null
                        ? $" · civil {director.Civil.Health.Current}/{director.Civil.Health.MaxHealth}" : "";
                    bool cerca = d <= director.RadioDeAlertaDelHeli;
                    detalle.text = (cerca ? "¡LLEGANDO! " : "Lleva al civil al helicoptero (punto de origen) · ") + Mathf.RoundToInt(d) + " m" + civil;
                    SetBarra(cerca ? 1f - Mathf.Clamp01(d / director.RadioDeAlertaDelHeli) : 0f, cerca ? new Color(1f, 0.3f, 0.25f) : new Color(0.35f, 1f, 0.5f));
                    detalle.color = cerca ? new Color(1f, 0.45f, 0.35f) : Color.white;
                    break;
                }
            }
        }
    }
}
