namespace SP.Combat
{
    // Los tres primeros se mantienen (los serializa el prefab de armas del piso);
    // los nuevos se agregan al final para no correr ningun valor guardado.
    public enum WeaponKind { Rifle, Pistol, Heavy, Smg, Rocket, Shotgun, Sniper }

    // Estilo de mirilla que se dibuja al apuntar con clic derecho. Cada arma
    // tiene la suya (ver WeaponCatalog.Spec.Reticle y UI/MirillaView).
    public enum ReticleStyle { Punto, Cruz, Anillo, Chevron, Mildot, Circulo, Telescopica }
}
