using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using SP.Actors;
using SP.Vehicles;

namespace SP.UI
{
    // Panel de TECLAS dentro del vehiculo (pedido: "cuando estes en el tanque
    // aparezcan las teclas para decir que salgan todos o entren todos y poder
    // cambiar o intercambiar si el puesto esta ocupado"). Se arma por codigo
    // debajo del Canvas principal y se actualiza cada frame:
    //  - una fila por asiento con su ocupante y si la tecla CAMBIA (libre) o
    //    INTERCAMBIA (ocupado);
    //  - las acciones de grupo: todos suben / todos bajan / llamar a uno;
    //  - la tecla apretada parpadea (feedback visual de cada accion).
    //
    // Pedido explicito: "la UI del tanque que este colapsada... y se expanda
    // solo si mantengo presionada TAB. Sino sea solo un icono de TAB y texto
    // que diga GUIA" -- antes esto quedaba siempre desplegado tapando media
    // pantalla mientras se manejaba. Colapsado muestra una tecla TAB (mismo
    // dibujo procedural que ya usa el tutorial, SP.Tutorial.TutorialTextures)
    // + "GUIA"; con TAB sostenido se expande al texto completo de siempre.
    public class VehicleKeysPanel : MonoBehaviour
    {
        public const string Nombre = "VehicleKeysPanel";

        Text texto;
        RawImage iconoTab;
        Text textoGuia;
        RectTransform rt;
        bool expandido;
        static readonly Vector2 TamanoColapsado = new Vector2(150f, 40f);
        static readonly Vector2 TamanoExpandido = new Vector2(330f, 232f);
        readonly Dictionary<string, float> destello = new Dictionary<string, float>();

        static readonly string[] NombresDeAsiento = { "Conductor", "Cañón", "Metralleta", "Pasajero" };
        static readonly VehicleSeatRole[] Asientos =
            { VehicleSeatRole.Driver, VehicleSeatRole.Gunner, VehicleSeatRole.Passenger1, VehicleSeatRole.Passenger2 };
        static readonly string[] Teclas = { "1", "2", "3", "4" };

        public string UltimoTexto => texto != null ? texto.text : "";
        public bool Visible => gameObject.activeSelf;

        public static VehicleKeysPanel Asegurar(Transform canvasRaiz)
        {
            if (canvasRaiz == null) return null;
            var existente = canvasRaiz.Find(Nombre);
            if (existente != null) return existente.GetComponent<VehicleKeysPanel>();

            var go = new GameObject(Nombre, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(canvasRaiz, false);
            var rt = (RectTransform)go.transform;
            // Arriba a la izquierda: abajo a la derecha vive el panel de
            // velocidad y tripulacion, y arriba a la derecha el minimapa.
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(16f, -140f);   // debajo del panel de mision
            rt.sizeDelta = new Vector2(330f, 232f);
            var bg = go.GetComponent<Image>();
            bg.color = new Color(0.04f, 0.05f, 0.07f, 0.72f);
            bg.raycastTarget = false;

            var tgo = new GameObject("Texto", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            tgo.transform.SetParent(go.transform, false);
            var trt = (RectTransform)tgo.transform;
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
            trt.offsetMin = new Vector2(12f, 8f); trt.offsetMax = new Vector2(-12f, -8f);
            var t = tgo.GetComponent<Text>();
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.fontSize = 13;
            t.supportRichText = true;
            t.alignment = TextAnchor.UpperLeft;
            t.color = Color.white;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;

            // Estado colapsado (por defecto): la tecla TAB dibujada (mismo
            // estilo que el tutorial) + "GUIA". Nada de texto de ordenes
            // hasta que se sostenga TAB.
            var iconoGO = new GameObject("IconoTab", typeof(RectTransform), typeof(RawImage));
            iconoGO.transform.SetParent(go.transform, false);
            var iconoRt = (RectTransform)iconoGO.transform;
            iconoRt.anchorMin = iconoRt.anchorMax = new Vector2(0f, 0.5f);
            iconoRt.pivot = new Vector2(0f, 0.5f);
            iconoRt.anchoredPosition = new Vector2(10f, 0f);
            iconoRt.sizeDelta = new Vector2(48f, 28f);
            var raw = iconoGO.GetComponent<RawImage>();
            raw.texture = SP.Tutorial.TutorialTextures.Tecla(48, 28, SP.Tutorial.TutorialTextures.Estado.Normal);
            raw.raycastTarget = false;

            var tabLabelGO = new GameObject("Texto", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            tabLabelGO.transform.SetParent(iconoGO.transform, false);
            var tabLabelRt = (RectTransform)tabLabelGO.transform;
            tabLabelRt.anchorMin = Vector2.zero; tabLabelRt.anchorMax = Vector2.one;
            tabLabelRt.offsetMin = tabLabelRt.offsetMax = Vector2.zero;
            var tabLabel = tabLabelGO.GetComponent<Text>();
            tabLabel.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            tabLabel.fontSize = 12;
            tabLabel.fontStyle = FontStyle.Bold;
            tabLabel.alignment = TextAnchor.MiddleCenter;
            tabLabel.color = Color.white;
            tabLabel.text = "TAB";
            tabLabel.raycastTarget = false;

            var guiaGO = new GameObject("TextoGuia", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            guiaGO.transform.SetParent(go.transform, false);
            var guiaRt = (RectTransform)guiaGO.transform;
            guiaRt.anchorMin = new Vector2(0f, 0f); guiaRt.anchorMax = new Vector2(1f, 1f);
            guiaRt.offsetMin = new Vector2(68f, 0f); guiaRt.offsetMax = new Vector2(-8f, 0f);
            var tg = guiaGO.GetComponent<Text>();
            tg.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            tg.fontSize = 15;
            tg.fontStyle = FontStyle.Bold;
            tg.alignment = TextAnchor.MiddleLeft;
            tg.color = new Color(0.85f, 0.9f, 1f);
            tg.text = "GUIA";
            tg.raycastTarget = false;

            var panel = go.AddComponent<VehicleKeysPanel>();
            panel.texto = t;
            panel.iconoTab = raw;
            panel.textoGuia = tg;
            panel.rt = rt;
            panel.AplicarExpandido(false);
            go.SetActive(false);
            return panel;
        }

        void AplicarExpandido(bool v)
        {
            expandido = v;
            texto.gameObject.SetActive(v);
            iconoTab.gameObject.SetActive(!v);
            textoGuia.gameObject.SetActive(!v);
            rt.sizeDelta = v ? TamanoExpandido : TamanoColapsado;
        }

        // Lo llama PlayerInputDriver cada frame con [TAB] sostenido o no.
        public void SetExpandido(bool v)
        {
            if (v != expandido) AplicarExpandido(v);
        }

        public void SetVisible(bool v)
        {
            if (gameObject.activeSelf != v) gameObject.SetActive(v);
        }

        // Hace parpadear una tecla ("G", "I", "1"...) un instante.
        public void Destellar(string tecla) => destello[tecla] = Time.unscaledTime + 0.35f;

        string Chip(string tecla, bool activa = false)
        {
            bool flash = destello.TryGetValue(tecla, out var hasta) && Time.unscaledTime < hasta;
            string col = flash ? "#ffe14d" : (activa ? "#7fe8a2" : "#cfd6e4");
            return $"<color={col}><b>[{tecla}]</b></color>";
        }

        public void Actualizar(Vehicle v, VehicleSeatRole? miAsiento, int aliadosEnCamino)
        {
            if (texto == null || v == null) return;
            var sb = new StringBuilder(512);
            sb.AppendLine("<size=13><color=#8ea0b8><b>TANQUE · ORDENES POR RADIAL</b></color></size>");
            sb.AppendLine($"{Chip("Q")} mantener → <b>TANQUE</b>: subir todos · bajar todos · tanque allí · bajarme" + (aliadosEnCamino > 0 ? $"  <color=#ffe14d>({aliadosEnCamino} en camino)</color>" : ""));
            sb.AppendLine($"{Chip("Q")} mantener → <b>POSEER</b>: pasar a otro soldado");
            sb.AppendLine($"{Chip("E")} bajar del tanque");
            sb.AppendLine($"{Chip("Espacio")} freno (conductor)");
            sb.AppendLine("<size=13><color=#8ea0b8><b>ASIENTOS · si esta ocupado, INTERCAMBIAN</b></color></size>");
            for (int i = 0; i < Asientos.Length; i++)
            {
                var ocupante = v.SoldierInSeat(Asientos[i]);
                bool mio = miAsiento.HasValue && miAsiento.Value == Asientos[i];
                string quien = ocupante == null ? "<color=#6b7688>libre</color>"
                    : (mio ? "<color=#7fe8a2>vos</color>" : $"<color=#ffd27a>{ocupante.DisplayName}</color>");
                string accion = mio ? "" : (ocupante == null ? "  <color=#6b7688>(ir)</color>" : "  <color=#ffb35c>(intercambiar)</color>");
                sb.AppendLine($"{Chip(Teclas[i], mio)} {(mio ? "► " : "")}{NombresDeAsiento[i]}: {quien}{accion}");
            }
            texto.text = sb.ToString();
        }
    }
}
