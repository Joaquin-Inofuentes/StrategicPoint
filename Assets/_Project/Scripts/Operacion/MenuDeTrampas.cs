using UnityEngine;
using UnityEngine.InputSystem;
using SP.Core;

namespace SP.Operacion
{
    // Bug #147 ("quiero modo cheat para cambiar entre momentos, saltearme escenas o ir a una parte ya avanzada"): [F10] abre un panel con los seis
    // objetivos de la operacion (y los dos puestos de la muralla). Elegir uno es lo mismo que arrancar la prueba por CLI: deja hecho todo lo
    // anterior y teletransporta a la escuadra. Tambien: modo dios y saltar la cinematica. Usar una trampa pasa la partida a "modo prueba" (no pisa
    // la partida guardada real).
    public class MenuDeTrampas : MonoBehaviour
    {
        static readonly string[] Opciones =
        {
            "1 · OBJ 1  Infiltrar el cuartel (torretas)",
            "2 · OBJ 2  Muralla: puesto OESTE",
            "3 · OBJ 2  Muralla: puesto ESTE",
            "4 · OBJ 3  Centro de datos",
            "5 · OBJ 4  Huir en el tanque",
            "6 · OBJ 5  Resistir en la ciudad",
            "7 · OBJ 6  Subir al helicoptero",
        };
        public static bool Abierto { get; private set; }
        bool abierto;
        float escalaPrevia = 1f;
        CursorLockMode lockPrevio; bool cursorVisiblePrevio;
        string mensaje = "";
        float tMensaje;
        GUIStyle estilo, estiloTitulo;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reiniciar() { Abierto = false; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Crear()
        {
            if (FindFirstObjectByType<MenuDeTrampas>() != null) return;
            var go = new GameObject("MenuDeTrampas");
            go.AddComponent<MenuDeTrampas>();
            DontDestroyOnLoad(go);
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb == null || OperacionDirector.Instancia == null) { if (abierto) Cerrar(); return; }
            if (kb.f10Key.wasPressedThisFrame) { if (abierto) Cerrar(); else Abrir(); }
            if (!abierto) return;
            if (kb.escapeKey.wasPressedThisFrame) { Cerrar(); return; }
            Key[] teclas = { Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4, Key.Digit5, Key.Digit6, Key.Digit7 };
            for (int i = 0; i < teclas.Length; i++) if (kb[teclas[i]].wasPressedThisFrame) { Ir(i); return; }
            if (kb.gKey.wasPressedThisFrame) { Mensaje(ModoDios.Alternar() ? "MODO DIOS: ACTIVADO" : "MODO DIOS: APAGADO"); }
            if (kb.cKey.wasPressedThisFrame) { CinematicaDeOperacion.Saltar(); Mensaje("CINEMATICA SALTADA"); }
        }

        void Abrir()
        {
            abierto = true; Abierto = true;
            escalaPrevia = Time.timeScale > 0f ? Time.timeScale : 1f; Time.timeScale = 0f;   // el juego queda quieto mientras se elige
            lockPrevio = Cursor.lockState; cursorVisiblePrevio = Cursor.visible;
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
        }

        void Cerrar()
        {
            abierto = false; Abierto = false;
            if (Time.timeScale == 0f) Time.timeScale = escalaPrevia;
            Cursor.lockState = lockPrevio; Cursor.visible = cursorVisiblePrevio;
        }

        void Mensaje(string m) { mensaje = m; tMensaje = Time.unscaledTime + 3f; }

        void Ir(int i)
        {
            var d = OperacionDirector.Instancia;
            if (d == null) return;
            int objetivo = i == 0 ? 1 : i <= 2 ? 2 : i;   // 0 = obj 1; 1 y 2 = obj 2 (puesto oeste / este); 3..6 = obj 3..6
            int puesto = i == 2 ? 2 : 1;
            PartidaGuardada.ModoPrueba = true;
            CinematicaDeOperacion.Saltar();
            d.SaltarA(OperacionDirector.Orden[objetivo - 1], true, 0);
            if (objetivo == 2) d.SaltarAPuesto(Mathf.Clamp(puesto, 1, OperacionDirector.TotalDePuestos) - 1);
            Mensaje("SALTO A: " + Opciones[i].Substring(4));
            Cerrar();
        }

        void OnGUI()
        {
            if (!abierto && Time.unscaledTime >= tMensaje) return;
            if (estilo == null)
            {
                estilo = new GUIStyle(GUI.skin.button) { fontSize = 16, alignment = TextAnchor.MiddleLeft, fixedHeight = 34 };
                estiloTitulo = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            }
            if (!abierto)
            {
                GUI.Label(new Rect(Screen.width * 0.5f - 300f, 60f, 600f, 30f), mensaje, estiloTitulo);
                return;
            }
            float w = 460f, h = 80f + Opciones.Length * 38f + 120f;
            var r = new Rect((Screen.width - w) * 0.5f, (Screen.height - h) * 0.5f, w, h);
            GUI.Box(r, "");
            GUI.Label(new Rect(r.x, r.y + 8f, w, 30f), "MODO TRAMPA  ·  F10 cierra", estiloTitulo);
            float y = r.y + 50f;
            for (int i = 0; i < Opciones.Length; i++)
            {
                if (GUI.Button(new Rect(r.x + 16f, y, w - 32f, 34f), Opciones[i], estilo)) { Ir(i); return; }
                y += 38f;
            }
            y += 10f;
            if (GUI.Button(new Rect(r.x + 16f, y, w - 32f, 34f), "G · Modo dios: " + (ModoDios.Activo ? "SI" : "NO"), estilo)) Mensaje(ModoDios.Alternar() ? "MODO DIOS: ACTIVADO" : "MODO DIOS: APAGADO");
            y += 38f;
            if (GUI.Button(new Rect(r.x + 16f, y, w - 32f, 34f), "C · Saltar la cinematica", estilo)) { CinematicaDeOperacion.Saltar(); Mensaje("CINEMATICA SALTADA"); }
            y += 38f;
            GUI.Label(new Rect(r.x + 16f, y, w - 32f, 30f), "Teclas 1-7 eligen · Esc cierra", estiloTitulo);
        }
    }
}
