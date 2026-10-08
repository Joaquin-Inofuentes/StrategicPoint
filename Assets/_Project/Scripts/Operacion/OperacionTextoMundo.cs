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

        // Al reconstruirse el atlas de la fuente (crece al pedir mas letras/tamanos) las letras de los carteles quedaban con las UV del atlas
        // viejo y se leian mezcladas en la build: se marca y, en el proximo Update, se regeneran las mallas de todos los TextMesh.
        bool regenerar; float regenerarHasta;
        void AlReconstruir(Font f) { Aplicar(); regenerar = true; texturasSucias = true; }
        bool texturasSucias = true;
        static MaterialPropertyBlock bloque;

        static readonly System.Collections.Generic.List<TextMesh> bufferTextos = new System.Collections.Generic.List<TextMesh>();
        void RegenerarMallas()
        {
            var todos = FindObjectsByType<TextMesh>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            bufferTextos.Clear(); bufferTextos.AddRange(todos);
            for (int i = 0; i < bufferTextos.Count; i++)
            {
                var tm = bufferTextos[i]; if (tm == null || string.IsNullOrEmpty(tm.text)) continue;
                var t = tm.text; tm.text = ""; tm.text = t;       // invalida la malla: se rearma con el atlas actual
            }
        }

        float proximaRevision;
        void Update()
        {
            if (material != null && fuente != null && material.mainTexture != fuente.material.mainTexture) { Aplicar(); regenerar = true; }
            if (Application.isPlaying)
            {
                if (regenerarHasta == 0f) regenerarHasta = Time.unscaledTime + 6f;
                // los primeros segundos (el atlas se llena al cargar el nivel) se rearma cada ~1,5 s; despues solo si el atlas se reconstruyo.
                if (texturasSucias) { texturasSucias = false; AsignarATodos(); }
                if (regenerar || (Time.unscaledTime < regenerarHasta && Time.frameCount % 90 == 0)) { regenerar = false; RegenerarMallas(); }
            }
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
                if (mr == null) continue;
                if (mr.sharedMaterial != material) { mr.sharedMaterial = material; TextosCorregidos++; }
                // La textura se toma de la fuente de CADA TextMesh (en la build la fuente integrada del cartel no es la misma instancia que
                // la del material y las letras salian mezcladas): se la fija por renderer con un MaterialPropertyBlock.
                var f = todos[i].font;
                if (f != null && f.material != null)
                {
                    if (bloque == null) bloque = new MaterialPropertyBlock();
                    mr.GetPropertyBlock(bloque);
                    bloque.SetTexture("_MainTex", f.material.mainTexture);
                    mr.SetPropertyBlock(bloque);
                }
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
