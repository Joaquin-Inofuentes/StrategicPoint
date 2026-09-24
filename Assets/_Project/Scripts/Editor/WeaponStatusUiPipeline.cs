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
    // redimensiona una sola vez al layout fijo de 2 filas que
    // WeaponStatusView.cs ya asume. Los hijos Icono/Reloj/Extras* los
    // arma el propio script en runtime (no hace falta tocarlos aca).
    // Idempotente: correrlo de nuevo deja los mismos valores.
    public static class WeaponStatusUiPipeline
    {
        const string ScenePath = "Assets/_Project/Scenes/SC_Gameplay.unity";

        [MenuItem("Strategic Point/UI/3. Simplificar panel de arma (2 renglones)")]
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
            // Mismo margen inferior/derecho de siempre (10.2 / 11.5). Panel
            // 226x54: renglon de arriba (texto + F+cuchillo + G+granada) y
            // renglon de abajo (barra), con el icono del arma a la
            // izquierda abarcando las dos filas.
            panelRt.offsetMin = new Vector2(-237.5f, 10.2f);
            panelRt.offsetMax = new Vector2(-11.5f, 64.2f);

            var textT = panel.transform.Find("Text");
            if (textT != null)
            {
                var rt = (RectTransform)textT;
                rt.anchorMin = rt.anchorMax = new Vector2(0f, 0f);
                rt.pivot = new Vector2(0f, 0f);
                rt.sizeDelta = new Vector2(78f, 24f);
                rt.anchoredPosition = new Vector2(46f, 26f);
                var text = textT.GetComponent<Text>();
                if (text != null) { text.alignment = TextAnchor.MiddleLeft; text.fontSize = 20; text.fontStyle = FontStyle.Bold; }
            }

            var barT = panel.transform.Find("BarBG");
            if (barT != null)
            {
                var rt = (RectTransform)barT;
                rt.anchorMin = rt.anchorMax = new Vector2(0f, 0f);
                rt.pivot = new Vector2(0f, 0f);
                rt.sizeDelta = new Vector2(176f, 6f);
                rt.anchoredPosition = new Vector2(46f, 4f);

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
            Debug.Log("[WeaponStatusUiPipeline] Panel de arma a 2 renglones y escena guardada.");
        }
    }
}
