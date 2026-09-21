using UnityEngine;

namespace SP.Player
{
    public partial class PlayerInputDriver
    {
        // Servicio unico de la escena: se registra al activarse en vez de que cada consumidor lo busque con un barrido.
        // El suite (Edit mode, donde no corren Awake/OnEnable) llama Registrar() a mano.
        public static PlayerInputDriver Activo { get; private set; }
        public static void ReiniciarActivo() => Activo = null;
        public void Registrar()
        {
            if (Activo != null && Activo != this) SP.Core.GameLog.Line($"[AVISO] Segundo PlayerInputDriver en la escena: {name}");
            Activo = this;
        }
        void QuitarRegistro() { if (Activo == this) Activo = null; }
    }
}
