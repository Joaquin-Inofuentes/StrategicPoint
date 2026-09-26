using UnityEngine;
using UnityEngine.UI;
using SP.Core;
using SP.Mision;
using SP.CameraSystem;

namespace SP.UI
{
    public class CartelVolverView : MonoBehaviour
    {
        RectTransform panel;
        Text texto;
        RectTransform flecha;
        Image flechaImg, fondoImg;

        float lastDist;
        float tiempoAlejandose;
        float alpha;

        public static CartelVolverView Crear()
        {
            var feedback = CapasDeHud.Instancia != null ? CapasDeHud.Instancia.Hud_Feedback : null;
            if (feedback == null) return null;

            var go = new GameObject("CartelVolver", typeof(RectTransform), typeof(CartelVolverView));
            go.transform.SetParent(feedback.transform, false);
            var view = go.GetComponent<CartelVolverView>();
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.75f);
            rt.anchorMax = new Vector2(0.5f, 0.75f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(400f, 60f);
            view.panel = rt;

            var fondo = new GameObject("Fondo", typeof(RectTransform), typeof(Image));
            fondo.transform.SetParent(rt, false);
            var fondoRt = fondo.GetComponent<RectTransform>();
            fondoRt.anchorMin = Vector2.zero; fondoRt.anchorMax = Vector2.one;
            fondoRt.sizeDelta = Vector2.zero;
            view.fondoImg = fondo.GetComponent<Image>();
            view.fondoImg.color = new Color(0.1f, 0.05f, 0.05f, 0.8f);

            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var txtGo = new GameObject("Texto", typeof(RectTransform), typeof(Text), typeof(Shadow));
            txtGo.transform.SetParent(rt, false);
            view.texto = txtGo.GetComponent<Text>();
            view.texto.font = font;
            view.texto.fontSize = 24;
            view.texto.fontStyle = FontStyle.Bold;
            view.texto.color = new Color(1f, 0.3f, 0.25f);
            view.texto.alignment = TextAnchor.MiddleCenter;
            view.texto.text = Loc.T("VUELVE AL OBJETIVO");
            var txtRt = view.texto.rectTransform;
            txtRt.anchorMin = Vector2.zero; txtRt.anchorMax = Vector2.one;
            txtRt.sizeDelta = Vector2.zero;

            var flGo = new GameObject("Flecha", typeof(RectTransform), typeof(Image));
            flGo.transform.SetParent(rt, false);
            view.flecha = flGo.GetComponent<RectTransform>();
            view.flecha.anchorMin = new Vector2(0.5f, 0f);
            view.flecha.anchorMax = new Vector2(0.5f, 0f);
            view.flecha.anchoredPosition = new Vector2(0f, -30f);
            view.flecha.sizeDelta = new Vector2(40f, 40f);
            view.flechaImg = flGo.GetComponent<Image>();
            // Triangle shape roughly:
            view.flechaImg.color = new Color(1f, 0.3f, 0.25f);

            view.SetAlpha(0f);
            return view;
        }

        void SetAlpha(float a)
        {
            alpha = a;
            if (fondoImg != null) fondoImg.color = new Color(0.1f, 0.05f, 0.05f, 0.8f * a);
            if (texto != null) { var c = texto.color; c.a = a; texto.color = c; }
            if (flechaImg != null) { var c = flechaImg.color; c.a = a; flechaImg.color = c; }
        }

        float GetUmbral(FaseDeMision fase)
        {
            var d = MisionDirector.Instancia;
            switch(fase)
            {
                case FaseDeMision.Infiltrar: return 50f;
                case FaseDeMision.Resistir: return d != null ? d.RadioResistencia : 32f;
                case FaseDeMision.Rescatar: return 40f;
                case FaseDeMision.Escapar: return 60f;
                default: return 100f;
            }
        }

        void Update()
        {
            var dir = MisionDirector.Instancia;
            if (dir == null || !MisionDirector.Activo)
            {
                if (alpha > 0f) SetAlpha(0f);
                return;
            }

            float dist = dir.DistanciaAlObjetivo();
            if (dist > lastDist + 0.5f * Time.deltaTime)
            {
                tiempoAlejandose += Time.deltaTime;
            }
            else if (dist < lastDist - 0.5f * Time.deltaTime)
            {
                tiempoAlejandose = 0f;
            }
            lastDist = dist;

            bool debeMostrar = tiempoAlejandose >= 4f && dist > GetUmbral(dir.Fase);
            
            float targetAlpha = debeMostrar ? 1f : 0f;
            if (Mathf.Abs(alpha - targetAlpha) > 0.01f)
            {
                SetAlpha(Mathf.MoveTowards(alpha, targetAlpha, Time.deltaTime * 2f));
            }

            if (alpha > 0f)
            {
                // Pulse
                float scale = 1f + 0.15f * (0.5f + 0.5f * Mathf.Sin(Time.time * 6f));
                panel.localScale = Vector3.one * scale;

                // Flecha
                if (Camera.main != null)
                {
                    Vector3 objPos = dir.PuntoObjetivoActual();
                    Vector3 playerPos = dir.PosicionDelJugador();
                    Vector3 toObj = (objPos - playerPos);
                    toObj.y = 0f;
                    if (toObj.sqrMagnitude > 0.1f)
                    {
                        toObj.Normalize();
                        Vector3 camFwd = Camera.main.transform.forward;
                        camFwd.y = 0f;
                        if (camFwd.sqrMagnitude > 0.1f)
                        {
                            camFwd.Normalize();
                            float ang = Vector3.SignedAngle(camFwd, toObj, Vector3.up);
                            flecha.localRotation = Quaternion.Euler(0f, 0f, -ang);
                        }
                    }
                }
            }
        }
    }
}
