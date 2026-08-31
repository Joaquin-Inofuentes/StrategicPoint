using UnityEngine;

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

        static Material Template
        {
            get
            {
                if (template == null)
                {
                    var temp = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    template = new Material(temp.GetComponent<Renderer>().sharedMaterial);
                    if (Application.isPlaying) Object.Destroy(temp);
                    else Object.DestroyImmediate(temp);
                }
                return template;
            }
        }

        // brillo/metalico casi nulo a proposito: sombreado suave y
        // colores vivos, sin brillo especular que los lave palidos ni
        // reflejos de ambiente que los tiñan de azul.
        public static Material Create(Color color)
        {
            var mat = new Material(Template);
            mat.color = color;
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.05f);
            if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", 0.05f);
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0f);
            return mat;
        }

        // Para el puñado de sitios que compartian UN material entre
        // muchas instancias (SelectionRingFx, OrderMarkerFx: pool con
        // color fijo, se tiñe despues por MaterialPropertyBlock).
        public static Material CreateShared() => Create(Color.white);
    }
}
