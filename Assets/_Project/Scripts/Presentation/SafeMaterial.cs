using UnityEngine;
using UnityEngine.Rendering;

namespace SP.Presentation
{
    // Fuente unica y a prueba de fallos para materiales solidos de FX
    // (anillos, marcadores, impactos, debris, indicadores...). Los ~20
    // sitios que antes hacian Shader.Find("Universal Render Pipeline/
    // Unlit") o "/Lit" a mano por su cuenta podian terminar con el
    // shader de error (magenta o azul solido, reportado varias veces)
    // si esa busqueda por nombre no resolvia bien en el momento --
    // variantes sin compilar, orden de carga, lo que sea. En vez de
    // adivinar el nombre correcto para la pipeline activa, se CLONA el
    // material que UNITY MISMO le pone a un primitivo nuevo: ya esta
    // resuelto para la pipeline real del proyecto sin que este codigo
    // tenga que saber su nombre.
    public static class SafeMaterial
    {
        static Material template;

        // WP11 (diagnostico de fugas): si el editor pone un diccionario aca, cada material creado por las fabricas (SafeMaterial.Create, CreateLinea,
        // DiamondGizmo.NuevoMaterial...) suma uno a quien lo pidio (los dos primeros metodos de la pila). Solo en el editor y apagado por defecto.
        public static System.Collections.Generic.Dictionary<string, int> Origenes;

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        public static void RegistrarOrigen(int saltar = 2)
        {
            if (Origenes == null) return;
            var st = new System.Diagnostics.StackTrace(saltar, false);
            string k = "";
            for (int i = 0; i < 2 && i < st.FrameCount; i++) { var m = st.GetFrame(i).GetMethod(); k += (i > 0 ? " <- " : "") + (m.DeclaringType != null ? m.DeclaringType.Name : "?") + "." + m.Name; }
            Origenes.TryGetValue(k, out int n); Origenes[k] = n + 1;
        }

        // EL AGUJERO QUE TENIA ESTO. Clonar el material del primitivo es
        // buena idea, pero no siempre devuelve el material de la pipeline
        // activa: cuando Unity todavia no resolvio URP para esa llamada,
        // CreatePrimitive entrega el "Default-Material" del pipeline
        // integrado, con shader "Standard". Ese shader NO existe bajo URP y
        // se dibuja magenta. Y como el template es un static cacheado, un
        // solo momento malo dejaba TODOS los FX de la sesion en magenta:
        // impactos, fogonazos, marcadores de orden, anillos de seleccion.
        // Era justo el sintoma que este archivo dice evitar.
        //
        // Ahora el template se VALIDA antes de darse por bueno, y si no
        // sirve se cae en cascada al shader por defecto de la pipeline
        // activa y recien despues a la busqueda por nombre.
        static bool EsUsable(Material m)
        {
            if (m == null || m.shader == null || !m.shader.isSupported) return false;
            if (m.shader.name.Contains("InternalError")) return false;
            // Bajo una SRP, los shaders del pipeline integrado no dibujan.
            if (GraphicsSettings.currentRenderPipeline != null &&
                (m.shader.name == "Standard" || m.shader.name.StartsWith("Legacy Shaders/")))
                return false;
            return true;
        }

        static Material Template
        {
            get
            {
                if (EsUsable(template)) return template;

                var temp = GameObject.CreatePrimitive(PrimitiveType.Cube);
                var candidato = temp.GetComponent<Renderer>().sharedMaterial;
                bool sirve = EsUsable(candidato);
                if (sirve) { template = new Material(candidato); template.hideFlags = HideFlags.HideAndDontSave; }
                if (Application.isPlaying) Object.Destroy(temp);
                else Object.DestroyImmediate(temp);
                if (sirve) return template;

                var rp = GraphicsSettings.currentRenderPipeline;
                var shader = rp != null ? rp.defaultShader : null;
                if (shader == null || !shader.isSupported) shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null || !shader.isSupported) shader = Shader.Find("Sprites/Default");

                Debug.LogWarning("[SafeMaterial] El material del primitivo no servia para la pipeline activa (" +
                                 (candidato != null && candidato.shader != null ? candidato.shader.name : "nulo") +
                                 "); se cae al shader " + (shader != null ? shader.name : "NINGUNO") + ".");

                template = new Material(shader);
                template.hideFlags = HideFlags.HideAndDontSave;
                return template;
            }
        }

        // brillo/metalico casi nulo a proposito: sombreado suave y
        // colores vivos, sin brillo especular que los lave palidos ni
        // reflejos de ambiente que los tiñan de azul.
        public static Material Create(Color color)
        {
            RegistrarOrigen();
            var mat = new Material(Template);

            // ESTO ES LO QUE FALTABA, y es la causa REAL del magenta que se
            // veia en impactos, escombros y marcadores de orden.
            //
            // Un Material creado en runtime no lo referencia ningun asset:
            // para Unity es basura recolectable. Los objetos de los pools SI
            // sobreviven (llevan DontSave), pero sus materiales no, y en
            // cuanto corre Resources.UnloadUnusedAssets -- que el Editor
            // dispara solo, y Unity tambien al cargar escenas -- el material
            // se libera y el renderer queda con sharedMaterial NULO. Un
            // renderer sin material se dibuja magenta.
            //
            // Se veia como "shader roto" y no lo era: ninguna busqueda de
            // materiales rotos los encontraba, porque el material ya no
            // existia. HideAndDontSave saca al material de esa recoleccion
            // sin meterlo en la escena.
            mat.hideFlags = HideFlags.HideAndDontSave;

            mat.color = color;
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.05f);
            if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", 0.05f);
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0f);
            return mat;
        }

        // Material SIN LUZ para LineRenderers. Create() clona el material Lit de la pipeline y un
        // LineRenderer no tiene normales utiles: el sombreado lo deja gris/negro por mas blanco que sea
        // el color (pedido explicito: "la linea del camino quiero que sea blanca, no gris o negra").
        // Sprites/Default es unlit, viene siempre incluido y respeta el color de vertices del
        // LineRenderer (startColor/endColor) y el alfa; se usa el Unlit de URP solo si faltara.
        public static Material CreateLinea(Color color)
        {
            RegistrarOrigen();
            var shader = Shader.Find("Sprites/Default");
            if (shader == null || !shader.isSupported) shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null || !shader.isSupported) return Create(color);   // ultimo recurso: el de siempre

            var mat = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            mat.color = color;
            return mat;
        }

        // Para el puñado de sitios que compartian UN material entre
        // muchas instancias (SelectionRingFx, OrderMarkerFx: pool con
        // color fijo, se tiñe despues por MaterialPropertyBlock).
        public static Material CreateShared() => Create(Color.white);
    }
}
