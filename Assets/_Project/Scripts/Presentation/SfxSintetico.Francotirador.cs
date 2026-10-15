using UnityEngine;

namespace SP.Presentation
{
    // Bug #241 ("el disparo del francotirador deberia tener un efecto visual y sonoro especial y la recarga un efecto sonoro fuerte"):
    // el estampido del fusil de cerrojo: chasquido supersonico, golpe grave y ecos que rebotan en los edificios.
    public static partial class SfxSintetico
    {
        static AudioClip estampidoFrancotirador;

        public static AudioClip EstampidoDeFrancotirador()
        {
            if (estampidoFrancotirador != null) return estampidoFrancotirador;
            var b = Buffer(2.6f);
            // Chasquido supersonico (agudo, casi instantaneo).
            Ruido(b, 0f, 0.05f, 0.2f, 0.2f, 0.96f, 70f, 1.2f, 811, 0.0002f);
            Metal(b, 0f, 0.12f, new[] { 1800f, 3100f, 4700f }, 55f, 0.45f, 812, 0.03f);
            // Golpe grave de la carga: sube la presion y cae.
            Tono(b, 0f, 0.7f, 150f, 34f, 5.5f, 1.15f, 0.12f, 0.002f);
            Ruido(b, 0f, 0.45f, 0.9f, 0.9f, 0.995f, 7f, 0.9f, 813, 0.001f);
            // Ecos: el estampido vuelve rebotado desde los edificios, cada vez mas apagado y grave.
            float[] t = { 0.19f, 0.41f, 0.72f, 1.18f };
            for (int i = 0; i < t.Length; i++)
            {
                float g = 0.55f / (1f + i * 0.9f);
                Ruido(b, t[i], 0.35f, 0.85f + i * 0.03f, 0.9f, 0.995f, 5f + i, g, 820 + i, 0.012f);
                Tono(b, t[i], 0.3f, 90f - i * 10f, 40f, 6f, g * 0.6f, 0.05f, 0.01f);
            }
            // Cola de rumor lejano.
            Ruido(b, 0.3f, 2.2f, 0.985f, 0.985f, 0.9993f, 2.4f, 0.28f, 830, 0.3f);
            estampidoFrancotirador = Cerrar(b, "Shot_Sniper_Boom", 0.95f);
            return estampidoFrancotirador;
        }

        public static void ReiniciarFrancotirador() { estampidoFrancotirador = null; }
    }

    // Efecto visual del disparo del francotirador: fogonazo largo azulado, onda de presion en el piso, destello de luz y polvo.
    public static class SniperFx
    {
        public static readonly Color Tinte = new Color(0.78f, 0.9f, 1f, 1f);
        public static int Disparos { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reiniciar() { Disparos = 0; SfxSintetico.ReiniciarFrancotirador(); }

        public static void Disparo(Vector3 boca, Vector3 dir, bool propio, bool conVisual = true)
        {
            if (!Application.isPlaying) return;
            if (dir.sqrMagnitude < 0.0001f) dir = Vector3.forward;
            dir.Normalize();
            Disparos++;
            // Visual: fogonazo grande y largo hacia adelante + chispas + una onda que se expande por el piso desde la boca (con la mira
            // de zoom puesta no se dibuja: taparia el visor, igual que el fogonazo normal del bug #204).
            if (conVisual) {
            ImpactFx.SpawnMuzzleBlast(boca, dir, 1.6f, Tinte);
            float pisoY = DestruccionDeVehiculo.AlturaDelPiso(boca);
            ImpactFx.SpawnShockwaveRing(new Vector3(boca.x, pisoY + 0.1f, boca.z), Tinte, 5.5f, 0.5f, 0f, 0.8f);
            ImpactFx.SpawnShockwaveRing(new Vector3(boca.x, pisoY + 0.12f, boca.z), new Color(1f, 1f, 1f, 1f), 3.2f, 0.35f, 0.05f, 0.5f);
            // Estela: una raya luminosa corta delante de la boca (el aire quemado por la bala supersonica).
            for (int i = 0; i < 4; i++)
                SpriteFx.Lanzar("flare_01", boca + dir * (1.2f + i * 1.7f), new Color(0.85f, 0.93f, 1f, 0.9f - i * 0.18f), 0.9f, 0.2f, 0.18f, 0f, dir * 40f, 0f, 9);
            MuzzleLightPool.Flash(boca, Tinte);
            }
            // Sonido: el estampido propio (grave + ecos), por encima del disparo base.
            AudioDirector.PlayClipAt(SfxSintetico.EstampidoDeFrancotirador(), boca, propio ? 0.95f : 1f, 0.35f, PerfilEspacial.Disparo);
            // La camara del que dispara recibe un golpe fuerte.
            if (propio)
            {
                var yo = SP.Player.PlayerInputDriver.Activo;
                if (yo != null && yo.Rig != null) yo.Rig.KickRecoil(5.5f);
            }
        }
    }
}
