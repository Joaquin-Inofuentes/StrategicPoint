using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace SP.Tutorial
{
    // Teclado y mouse VIRTUALES para el reproductor automatico del tutorial (item 95): el reproductor no llama a las
    // APIs del soldado, aprieta teclas de verdad en el Input System y el juego las lee igual que las de una persona
    // (PlayerInputDriver, KeyBindings y las evaluaciones del tutorial leen Keyboard.current / Mouse.current).
    // Solo existe mientras corre el reproductor (Play); no toca nada del juego normal.
    public class EntradaVirtual : MonoBehaviour
    {
        public static EntradaVirtual Actual { get; private set; }

        Keyboard teclado;
        Mouse raton;
        readonly HashSet<Key> abiertas = new HashSet<Key>();
        bool izquierdo, derecho;
        Vector2 movimientoPendiente;

        public static EntradaVirtual Asegurar(GameObject donde)
        {
            if (Actual != null) return Actual;
            var s = InputSystem.settings;
            s.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            s.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            var e = donde.AddComponent<EntradaVirtual>();
            e.teclado = InputSystem.AddDevice<Keyboard>("TutorialAutoKb");
            e.raton = InputSystem.AddDevice<Mouse>("TutorialAutoMouse");
            Actual = e;
            return e;
        }

        void OnDestroy()
        {
            if (teclado != null && teclado.added) InputSystem.RemoveDevice(teclado);
            if (raton != null && raton.added) InputSystem.RemoveDevice(raton);
            if (Actual == this) Actual = null;
        }

        public void Apretar(params Key[] teclas) { foreach (var k in teclas) abiertas.Add(k); Empujar(); }
        public void Soltar(params Key[] teclas) { foreach (var k in teclas) abiertas.Remove(k); Empujar(); }
        public void BotonIzquierdo(bool abajo) { izquierdo = abajo; Empujar(); }
        public void BotonDerecho(bool abajo) { derecho = abajo; Empujar(); }
        // Movimiento relativo del mouse (lo que el juego lee en mouse.delta: gira la torreta del tanque). Se envia una sola vez.
        public void Mover(Vector2 delta) { movimientoPendiente += delta; }
        public void SoltarTodo() { abiertas.Clear(); izquierdo = derecho = false; Empujar(); }
        public bool EstaApretada(Key k) => abiertas.Contains(k);

        // Se reenvia el estado en cada cuadro: asi la tecla sigue "abajo" el tiempo que haga falta y el juego ve la
        // pulsacion (wasPressedThisFrame) en el cuadro en que cambia.
        // Ademas se los vuelve a marcar como "el actual": si la persona mueve el mouse real mientras corre el reproductor,
        // Mouse.current pasaria a ser el real y los clics virtuales dejarian de leerse.
        void LateUpdate()
        {
            if (raton != null && raton.added && Mouse.current != raton) raton.MakeCurrent();
            if (teclado != null && teclado.added && Keyboard.current != teclado) teclado.MakeCurrent();
            Empujar();
        }

        void Empujar()
        {
            if (teclado != null && teclado.added)
            {
                var ks = new KeyboardState();
                foreach (var k in abiertas) ks.Set(k, true);
                InputSystem.QueueStateEvent(teclado, ks);
            }
            if (raton != null && raton.added)
            {
                var ms = new MouseState { position = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f), delta = movimientoPendiente };
                movimientoPendiente = Vector2.zero;
                if (izquierdo) ms = ms.WithButton(MouseButton.Left);
                if (derecho) ms = ms.WithButton(MouseButton.Right);
                InputSystem.QueueStateEvent(raton, ms);
            }
        }
    }
}
