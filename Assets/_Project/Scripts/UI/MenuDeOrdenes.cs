using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

namespace SP.UI
{
    // Menu de ordenes rapidas que se despliega manteniendo [Q]. Un toque
    // corto de la misma tecla sigue ciclando de soldado como siempre.
    //
    // Por que numeros y no un radial con el mouse: en FPS el mouse esta
    // capturado apuntando el arma, y soltarlo para elegir una orden es
    // exactamente la interrupcion que este menu viene a evitar. Con la
    // izquierda ya puesta en [Q], el 1..5 queda al lado.
    public class MenuDeOrdenes : MonoBehaviour
    {
        public const int CantidadDeOpciones = 5;

        // El orden importa: es el que ve el jugador y el que interpreta
        // PlayerInputDriver.EjecutarOrdenDelMenu.
        public static readonly string[] Opciones =
        {
            "1  FORMACION EN LINEA",
            "2  FORMACION EN CUÑA",
            "3  SIGANME",
            "4  ALTO",
            "5  NECESITO CURARME",
        };

        CanvasGroup group;
        Text lista;

        public bool Abierto { get; private set; }

        public void Bind(Text texto, CanvasGroup canvasGroup)
        {
            lista = texto;
            group = canvasGroup;
            Escribir();
            Cerrar();
        }

        void OnEnable()
        {
            if (lista == null) lista = GetComponentInChildren<Text>(true);
            if (group == null) group = GetComponent<CanvasGroup>();
            Escribir();
            // Un menu que sobrevive prendido a un domain reload o a una
            // recarga de escena taparia el HUD sin que nadie lo haya
            // abierto: arranca siempre cerrado.
            Cerrar();
        }

        void Escribir()
        {
            if (lista == null) return;
            lista.text = "ORDENES\n" + string.Join("\n", Opciones);
        }

        public void Abrir()
        {
            Abierto = true;
            if (group != null) group.alpha = 1f;
        }

        public void Cerrar()
        {
            Abierto = false;
            if (group != null) group.alpha = 0f;
        }

        // Construye el panel si la escena no lo trae y deja cableado el
        // driver. Existe porque SC_Gameplay NO la arma HeadlessTestRunner
        // (esa solo escribe SC_TestLevel): sin esto la funcionalidad
        // andaria en la escena de pruebas y no en el juego. Mismo patron
        // que SpriteBlanco.RepararTodo y ApoyoEnElPiso.ApoyarATodos.
        public static MenuDeOrdenes AsegurarEnEscena()
        {
            var driver = Object.FindAnyObjectByType<SP.Player.PlayerInputDriver>();
            var existente = Object.FindAnyObjectByType<MenuDeOrdenes>(FindObjectsInactive.Include);
            if (existente != null)
            {
                if (driver != null && driver.OrdenesMenu == null) driver.OrdenesMenu = existente;
                return existente;
            }

            var canvas = Object.FindAnyObjectByType<Canvas>();
            if (canvas == null) return null;

            var menu = Construir(canvas.transform);
            if (driver != null) driver.OrdenesMenu = menu;
            return menu;
        }

        // Al costado izquierdo y a media altura: es la unica franja libre
        // con el roster arriba a la izquierda y el minimapa abajo a la
        // derecha.
        public static MenuDeOrdenes Construir(Transform padre)
        {
            var go = new GameObject("MenuDeOrdenes", typeof(RectTransform), typeof(CanvasGroup), typeof(MenuDeOrdenes));
            go.transform.SetParent(padre, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.anchoredPosition = new Vector2(16f, 0f);
            rt.sizeDelta = new Vector2(300f, 160f);

            var bg = new GameObject("BG", typeof(Image));
            bg.transform.SetParent(go.transform, false);
            bg.GetComponent<Image>().color = FondoOpaco.Color;
            Estirar(bg.GetComponent<RectTransform>());

            var textoGO = new GameObject("Text", typeof(Text));
            textoGO.transform.SetParent(go.transform, false);
            var texto = textoGO.GetComponent<Text>();
            texto.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            texto.alignment = TextAnchor.MiddleLeft;
            texto.color = FondoOpaco.ColorDeTexto;
            texto.fontSize = 16;
            var textoRt = textoGO.GetComponent<RectTransform>();
            Estirar(textoRt);
            textoRt.offsetMin = new Vector2(14f, 8f);
            textoRt.offsetMax = new Vector2(-14f, -8f);

            var menu = go.GetComponent<MenuDeOrdenes>();
            menu.Bind(texto, go.GetComponent<CanvasGroup>());
            return menu;
        }

        static void Estirar(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        // Devuelve 1..5, o 0 si este frame no se eligio nada. Estatico
        // porque no depende de la vista: la suite lo puede probar sin
        // tener un canvas armado.
        public static int LeerTecla()
        {
            var kb = Keyboard.current;
            if (kb == null) return 0;
            if (kb.digit1Key.wasPressedThisFrame) return 1;
            if (kb.digit2Key.wasPressedThisFrame) return 2;
            if (kb.digit3Key.wasPressedThisFrame) return 3;
            if (kb.digit4Key.wasPressedThisFrame) return 4;
            if (kb.digit5Key.wasPressedThisFrame) return 5;
            return 0;
        }
    }
}
