using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using SP.Player;

namespace SP.UI
{
    // La mitad visible del item 208. KeyBindings guarda y resuelve los
    // mapeos, pero sin esto el jugador no tiene forma de cambiarlos: seguia
    // haciendo falta editar codigo o PlayerPrefs a mano, o sea que el item
    // quedaba a medias.
    //
    // Flujo: se hace clic en la fila de una accion, la fila pasa a decir
    // "PRESIONA UNA TECLA", y la proxima tecla que se apriete queda
    // asignada. ESC cancela.
    public class KeyRebindView : MonoBehaviour
    {
        // Publico para que Unity lo serialice: un campo privado asignado al
        // construir la escena no sobrevive el domain reload.
        public Button[] Rows;
        public Text[] Labels;
        public string[] ActionIds;

        int listeningRow = -1;
        int listenStartFrame = -1;

        public bool IsListening => listeningRow >= 0;

        // Expuesto para verificacion objetiva sin depender de la pantalla.
        public string TextOfRow(int i) => Labels != null && i >= 0 && i < Labels.Length && Labels[i] != null ? Labels[i].text : null;

        void OnEnable()
        {
            // Al reabrir el panel se cancela cualquier escucha colgada: si
            // no, quedaria capturando teclas de gameplay.
            listeningRow = -1;
            RefreshAll();
        }

        void OnDisable() => listeningRow = -1;

        public void Bind(Button[] rows, Text[] labels, string[] actionIds)
        {
            Rows = rows;
            Labels = labels;
            ActionIds = actionIds;
            HookRows();
            RefreshAll();
        }

        void HookRows()
        {
            if (Rows == null) return;
            for (int i = 0; i < Rows.Length; i++)
            {
                if (Rows[i] == null) continue;
                int captured = i; // captura por valor: sin esto todas las filas remapearian la ultima
                Rows[i].onClick.RemoveAllListeners();
                Rows[i].onClick.AddListener(() => BeginListening(captured));
            }
        }

        public void BeginListening(int row)
        {
            if (ActionIds == null || Labels == null || row < 0 || row >= ActionIds.Length || row >= Labels.Length) return;
            RefreshAll(); // limpia cualquier "PRESIONA UNA TECLA" que hubiera quedado de una fila anterior
            listenStartFrame = Time.frameCount; // (del Bug 4, si se aplica en el mismo commit)
            listeningRow = row;
            if (Labels[row] != null)
                Labels[row].text = NameOf(ActionIds[row]) + ":  PRESIONA UNA TECLA";
        }

        // Funcion pura, sin estado de escena: separa la REGLA (ignorar el
        // frame en el que empezo a escuchar) de la lectura real del teclado,
        // para poder probarla sin Keyboard.current.
        public static bool ShouldIgnoreCapture(int listenStartFrame, int currentFrame) =>
            currentFrame <= listenStartFrame;

        void Update()
        {
            if (!IsListening) return;
            var kb = Keyboard.current;
            if (kb == null) return;

            if (kb.escapeKey.wasPressedThisFrame)
            {
                listeningRow = -1;
                RefreshAll();
                return;
            }

            if (ShouldIgnoreCapture(listenStartFrame, Time.frameCount)) return; // el click que activo la escucha no cuenta como la tecla nueva

            // anyKey no sirve para saber CUAL: hay que recorrer los
            // controles del teclado y quedarse con el primero que se apreto
            // en este frame.
            foreach (var control in kb.allKeys)
            {
                if (!control.wasPressedThisFrame) continue;
                AssignKey(control.keyCode);
                return;
            }
        }

        void AssignKey(Key key)
        {
            if (listeningRow < 0 || ActionIds == null) return;
            string action = ActionIds[listeningRow];

            string freed = KeyBindings.Set(action, key);
            if (freed != null)
                GameLog.Line($"{NameOf(freed)} quedo sin tecla asignada (la tomo {NameOf(action)})");

            listeningRow = -1;
            RefreshAll();
        }

        public void RefreshAll()
        {
            if (ActionIds == null || Labels == null) return;
            for (int i = 0; i < ActionIds.Length && i < Labels.Length; i++)
            {
                if (Labels[i] == null) continue;
                Labels[i].text = NameOf(ActionIds[i]) + ":  " + KeyBindings.DisplayName(ActionIds[i]);
            }
        }

        // Nombres legibles: el id es un slug estable, no algo para mostrar.
        static readonly Dictionary<string, string> nombres = new Dictionary<string, string>
        {
            { KeyBindings.Recargar, "Recargar" },
            { KeyBindings.Interactuar, "Interactuar" },
            { KeyBindings.SubirBajarVehiculo, "Subir/bajar del vehiculo" },
            { KeyBindings.Poseer, "Poseer" },
            { KeyBindings.CiclarPosesion, "Ciclar posesion" },
            { KeyBindings.CiclarPosesionAtras, "Ciclar posesion (atras)" },
            { KeyBindings.PoseerMasCercano, "Poseer al mas cercano" },
            { KeyBindings.AlternarVista, "Alternar vista FPS/RTS" },
            { KeyBindings.Controles, "Ver controles" },
            { KeyBindings.Frenar, "Frenar" },
            { KeyBindings.CamaraVehiculo, "Camara del vehiculo" },
            { KeyBindings.Recentrar, "Recentrar camara" },
            { KeyBindings.CancelarOrden, "Cancelar orden" },
            { KeyBindings.Reagrupar, "Reagrupar" },
            { KeyBindings.Retirada, "Retirada" },
            { KeyBindings.CiclarFormacion, "Ciclar formacion" },
            { KeyBindings.SeleccionarHeridos, "Seleccionar heridos" },
            { KeyBindings.SeleccionarMismoTipo, "Seleccionar mismo tipo" },
        };

        public static string NameOf(string actionId) =>
            nombres.TryGetValue(actionId, out var n) ? n : actionId;
    }
}
