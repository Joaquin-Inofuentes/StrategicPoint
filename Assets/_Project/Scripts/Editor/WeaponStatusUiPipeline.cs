using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace SP.EditorTools
{
    // Pedido explicito (ronda 1): "simplficalo y q use iconos, mas
    // vistoso, intenta evitar textos". Pedido explicito (ronda 2):
    // "2 renglones: la barra de municion [abajo] y arriba municion texto
    // y F icono de cuchillo y G icono de granada". El panel base (fondo +
    // Text + BarBG/BarFill) vive a mano en SC_Gameplay (nunca tuvo su
    // propio pipeline, a diferencia del roster) -- este script lo
    // redimensiona una sola vez al layout fijo que WeaponStatusView.cs ya
    // asume. Los hijos Icono/Reloj/Extras* los arma el propio script en
    // runtime (no hace falta tocarlos aca). Idempotente: correrlo de
    // nuevo deja los mismos valores.
    //
    // Pedido explicito (ronda nueva): "los iconos agrandalos y toda la ui
    // de esa esquina la mitad de ancho... mas grande los iconoes y
    // textos... q ocupen todo el espacio". Una mitad LITERAL de 226 (113px)
    // no entra ni con los iconos originales, mucho menos agrandados -- en
    // vez de eso el panel paso de ancho-y-bajo (226x54, todo en una linea)
    // a angosto-y-alto (136x112, un 40% menos de ancho): el icono del arma
    // ocupa toda la columna izquierda y a la derecha se apila municion /
    // cuchillo / granada / barra, cada renglon usando el ancho completo en
    // vez de quedar todo apretado en una sola fila.
    public static class WeaponStatusUiPipeline
    {
        const string ScenePath = "Assets/_Project/Scenes/SC_Gameplay.unity";

        [MenuItem("Strategic Point/UI/3. Simplificar panel de arma (angosto, apilado)")]
        public static void Simplificar()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (!scene.name.Contains("Gameplay"))
            {
                EditorSceneManager.OpenScene(ScenePath);
                scene = EditorSceneManager.GetActiveScene();
            }

            var panel = GameObject.Find("WeaponStatus");
            if (panel == null)
            {
                Debug.LogError("[WeaponStatusUiPipeline] No se encontro 'WeaponStatus' en la escena.");
                return;
            }

            var panelRt = panel.GetComponent<RectTransform>();
            // Mismo margen inferior/derecho de siempre (10.2 / 11.5).
            panelRt.offsetMin = new Vector2(-147.5f, 10.2f);
            panelRt.offsetMax = new Vector2(-11.5f, 122.2f);

            var textT = panel.transform.Find("Text");
            if (textT != null)
            {
                var rt = (RectTransform)textT;
                rt.anchorMin = rt.anchorMax = new Vector2(0f, 0f);
                rt.pivot = new Vector2(0f, 0f);
                rt.sizeDelta = new Vector2(70f, 26f);
                rt.anchoredPosition = new Vector2(60f, 84f);
                var text = textT.GetComponent<Text>();
                // BUG REAL (mismo que ya se piso una vez en
                // RosterUiPipeline): con VerticalWrapMode.Truncate (el
                // default de Unity) un fontSize que no entra JUSTO en el
                // alto del rect no se recorta, DESAPARECE por completo sin
                // ningun error. A fontSize 22 el rect de 26px de alto lo
                // disparaba. Overflow vertical es la red de seguridad
                // permanente -- y el horizontal hace falta ademas porque
                // "8/8 · 24" a este tamaño no entra en 70px de ancho: con
                // el wrap por defecto la reserva ("24") se cortaba a un
                // SEGUNDO renglon que caia pisando la fila de F/cuchillo.
                if (text != null)
                {
                    text.alignment = TextAnchor.MiddleLeft;
                    text.fontSize = 22;
                    text.fontStyle = FontStyle.Bold;
                    text.verticalOverflow = VerticalWrapMode.Overflow;
                    text.horizontalOverflow = HorizontalWrapMode.Overflow;
                }
            }

            var barT = panel.transform.Find("BarBG");
            if (barT != null)
            {
                var rt = (RectTransform)barT;
                rt.anchorMin = rt.anchorMax = new Vector2(0f, 0f);
                rt.pivot = new Vector2(0f, 0f);
                rt.sizeDelta = new Vector2(72f, 10f);
                rt.anchoredPosition = new Vector2(60f, 4f);

                // Un "Rotulo" de texto podia haber quedado de una sesion de Play anterior
                // guardada por error: WeaponStatusView ya no lo usa (reemplazado por el
                // icono de reloj de arena), asi que si aparece se lo saca.
                var rotulo = barT.Find("Rotulo");
                if (rotulo != null) Object.DestroyImmediate(rotulo.gameObject);
            }

            // Mismo saneo por si "Extras" (el texto viejo de cuchillo/granada) quedo
            // guardado en la escena de una sesion anterior.
            var extrasViejo = panel.transform.Find("Extras");
            if (extrasViejo != null) Object.DestroyImmediate(extrasViejo.gameObject);

            EditorUtility.SetDirty(panel);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[WeaponStatusUiPipeline] Panel de arma angosto y apilado, escena guardada.");
        }
    }
}
