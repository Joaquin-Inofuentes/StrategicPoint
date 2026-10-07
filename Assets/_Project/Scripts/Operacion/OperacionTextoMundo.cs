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
            if (Application.isPlaying) AsignarATodos();
            Font.textureRebuilt += AlReconstruir;
        }

        void OnDisable() { Font.textureRebuilt -= AlReconstruir; }

        void AlReconstruir(Font f) { if (f == fuente) Aplicar(); }

        float proximaRevision;
        void Update()
        {
            if (material != null && fuente != null && material.mainTexture != fuente.material.mainTexture) Aplicar();
            // #112/#118: al jugar, TextMesh vuelve al material de la fuente (GUI/Text Shader, que ignora la profundidad) y las letras del piso
            // y los carteles se pintaban encima de todo. Se reasigna el material con prueba de profundidad a todos los TextMesh del mundo.
            if (Application.isPlaying && Time.unscaledTime >= proximaRevision)
            {
                proximaRevision = Time.unscaledTime + 1f;
                AsignarATodos();
            }
        }

        public static int TextosCorregidos { get; private set; }
        public void AsignarATodos()
        {
            if (material == null) return;
            var todos = FindObjectsByType<TextMesh>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < todos.Length; i++)
            {
                var mr = todos[i].GetComponent<MeshRenderer>();
                if (mr == null || mr.sharedMaterial == material) continue;
                mr.sharedMaterial = material;
                TextosCorregidos++;
            }
        }

        void Aplicar()
        {
            if (material == null) return;
            if (fuente == null) fuente = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (fuente != null && fuente.material != null) material.mainTexture = fuente.material.mainTexture;
        }
    }
}
