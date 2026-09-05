using UnityEngine;
using UnityEngine.UI;

namespace SP.UI
{
    // BUG REAL, y afectaba a TODAS las barras del juego a la vez.
    //
    // Una Image de uGUI con type = Filled solo respeta fillAmount si tiene
    // un SPRITE. Sin sprite, Unity no pasa por GenerateFilledSprite: dibuja
    // un quad liso y IGNORA fillAmount por completo. No hay error, ni
    // advertencia, ni nada raro en el inspector -- la barra simplemente se
    // queda llena para siempre.
    //
    // Medido en SC_Gameplay: 14 Images con type=Filled y CERO con sprite.
    // O sea que estaban rotas la barra de vida flotante de cada soldado
    // (aliada y enemiga), la del jugador en el HUD, las tres del roster,
    // la del panel de escuadra cercana, la del vehiculo, la de recarga del
    // arma y la de enfriamiento de la torreta. Todas mostraban el maximo
    // pasara lo que pasara.
    //
    // El arreglo es un sprite de 1x1 blanco compartido por todas: no
    // cambia como se ven (el color lo sigue poniendo Image.color) y hace
    // que fillAmount funcione.
    public static class SpriteBlanco
    {
        static Sprite cache;

        public static Sprite Obtener()
        {
            if (cache != null) return cache;

            // Texture2D.whiteTexture es una textura del motor que siempre
            // existe y nadie destruye; envolverla en un Sprite no cuesta
            // memoria de textura.
            cache = Sprite.Create(Texture2D.whiteTexture,
                                  new Rect(0f, 0f, Texture2D.whiteTexture.width, Texture2D.whiteTexture.height),
                                  new Vector2(0.5f, 0.5f),
                                  100f, 0, SpriteMeshType.FullRect);
            cache.name = "SpriteBlanco1x1";
            cache.hideFlags = HideFlags.HideAndDontSave;
            return cache;
        }

        // Le pone el sprite a una Image que lo necesita. No toca las que ya
        // tienen uno ni las que no son Filled.
        public static void Reparar(Image img)
        {
            if (img == null) return;
            if (img.type != Image.Type.Filled) return;
            if (img.sprite != null) return;
            img.sprite = Obtener();
        }

        // Barrido de una jerarquia entera. Se llama una vez al arrancar la
        // escena, no por frame.
        public static int RepararTodo(GameObject raiz)
        {
            if (raiz == null) return 0;
            int n = 0;
            foreach (var img in raiz.GetComponentsInChildren<Image>(true))
            {
                if (img == null || img.type != Image.Type.Filled || img.sprite != null) continue;
                img.sprite = Obtener();
                n++;
            }
            return n;
        }
    }
}
