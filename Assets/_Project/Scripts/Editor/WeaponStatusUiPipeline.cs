using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace SP.EditorTools
{
    // Pedido explicito: "la UI de la esquina inferior derecha tambien
    // simplficalo y q use iconos mejor mas vistoso intenta evitar textos".
    // El panel base (fondo + Text + BarBG/BarFill) vive a mano en
    // SC_Gameplay (nunca tuvo su propio pipeline, a diferencia del
    // roster) -- este script lo redimensiona una sola vez al layout fijo
    // que WeaponStatusView.cs ya asume (icono de arma grande + numeros de
    // municion + barra sin rotulo + iconos de cuchillo/granada arriba).
    // Los hijos Icono/Reloj/ExtrasCuchillo/ExtrasGranada/ExtrasGranadaCount
    // los arma el propio script en runtime (no hace falta tocarlos aca).
    // Idempotente: correrlo de nuevo deja los mismos valores.
    public static class WeaponStatusUiPipeline
    {
        const string ScenePath = "Assets/_Project/Scenes/SC_Gameplay.unity";

        [MenuItem("Strategic Point/UI/3. Simplificar panel de arma (iconos)")]
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
            // Mismo margen inferior/derecho de siempre (10.2 / 11.5), panel
            // mas angosto (190, antes 220) y mas alto (72, antes 46) para
            // que entren dos filas de iconos arriba del numero de municion.
            panelRt.offsetMin = new Vector2(-201.5f, 10.2f);
            panelRt.offsetMax = new Vector2(-11.5f, 82.2f);

            var textT = panel.transform.Find("Text");
            if (textT != null)
            {
                var rt = (RectTransform)textT;
                rt.anchorMin = rt.anchorMax = new Vector2(0f, 0f);
                rt.pivot = new Vector2(0f, 0f);
                rt.sizeDelta = new Vector2(136f, 40f);
                rt.anchoredPosition = new Vector2(48f, 10f);
                var text = textT.GetComponent<Text>();
                if (text != null) { text.alignment = TextAnchor.MiddleLeft; text.fontSize = 22; text.fontStyle = FontStyle.Bold; }
            }

            var barT = panel.transform.Find("BarBG");
            if (barT != null)
            {
                var rt = (RectTransform)barT;
                rt.anchorMin = rt.anchorMax = new Vector2(0f, 0f);
                rt.pivot = new Vector2(0f, 0f);
                rt.sizeDelta = new Vector2(178f, 6f);
                rt.anchoredPosition = new Vector2(6f, 2f);

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
            Debug.Log("[WeaponStatusUiPipeline] Panel de arma simplificado y escena guardada.");
        }
    }
}
