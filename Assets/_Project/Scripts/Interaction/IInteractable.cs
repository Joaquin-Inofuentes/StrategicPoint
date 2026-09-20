using SP.Player;

namespace SP.Interaction
{
    // Interfaz comun para "objetos con los que el jugador interactua" (cajas de
    // suministro, puntos de cobertura manuales, rehenes, destructibles, etc.).
    //
    // A la fecha de este archivo el proyecto NO tiene ningun objeto de este tipo
    // implementado todavia: los sistemas existentes (Demolicion, Coberturas,
    // PedidoDeCuracion/Revivir, CajaDeSuministros) funcionan por proximidad
    // automatica o por la tecla [E] (KeyBindings.Interactuar), no por un
    // "interactuable" individual por objeto. Esta interfaz queda lista para que
    // el proximo interactuable que se agregue (p.ej. un rehen) se enchufe aca en
    // vez de sumar otro KeyCode.Q suelto.
    //
    // El detector central (PlayerInputDriver.Interaccion.cs) la consulta desde
    // el TAP de [Q] (ver ResolverGestoDeQ) antes de caer al comportamiento
    // existente (ciclar aliado / radial de ordenes).
    public interface IInteractable
    {
        // Texto corto para el prompt de UI, ej. "Destruir", "Cubrirse",
        // "Revivir", "Registrar rehen", "Recoger suministro".
        string GetPrompt(PlayerInputDriver player);

        // Si hoy, con este jugador, se puede interactuar (distancia, estado, etc.).
        bool CanInteract(PlayerInputDriver player);

        // Ejecuta la interaccion. Se llama solo si CanInteract devolvio true.
        void Interact(PlayerInputDriver player);
    }
}
