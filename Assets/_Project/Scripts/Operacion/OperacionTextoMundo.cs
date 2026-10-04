using UnityEngine;

namespace SP.Operacion
{
    // Los carteles del nivel son TextMesh. Con la fuente integrada el shader de texto (GUI/Text Shader) ignora la profundidad y las letras
    // de un cartel lejano se pintan encima de todo. Este material usa Sprites/Default (con prueba de profundidad) y toma la textura
    // de la fuente; como esa textura es dinamica (no se puede guardar en el .mat), se reasigna aca: en el editor y al jugar.
    [ExecuteAlways]
    public class OperacionTextoMundo : MonoBehaviour
    {
        public Material material;
        static Font fuente;

        void OnEnable()
        {
            Aplicar();
            Font.textureRebuilt += AlReconstruir;
        }

        void OnDisable() { Font.textureRebuilt -= AlReconstruir; }

        void AlReconstruir(Font f) { if (f == fuente) Aplicar(); }

        void Update()
        {
            if (material != null && fuente != null && material.mainTexture != fuente.material.mainTexture) Aplicar();
        }

        void Aplicar()
        {
            if (material == null) return;
            if (fuente == null) fuente = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (fuente != null && fuente.material != null) material.mainTexture = fuente.material.mainTexture;
        }
    }
}
