using UnityEngine;
using UnityEngine.Rendering;

namespace SP.Presentation
{
    // Fuente unica de materiales para ParticleSystem con blend alfa real y
    // colorOverLifetime funcionando de verdad.
    //
    // BUG REAL encontrado en el polvo del helicoptero ("las particulas del
    // helicoptero estan muy rotas"): ArmarPolvo() usaba
    // SafeMaterial.Create(...), que clona el material Lit/opaco de un
    // primitivo solido. Ese shader (Lit) NO lee el color de vertice que
    // colorOverLifetime escribe por particula, y su Surface es Opaque (sin
    // blend alfa) -- las particulas no se desvanecian nunca, aparecian y
    // desaparecian de golpe como cuadrados blancos/tierra solidos
    // rotando para encarar la camara. Eso era lo que se veia "roto".
    //
    // El shader de particulas de URP (Particles/Unlit) esta hecho
    // exactamente para esto: lee el color de vertice (colorOverLifetime,
    // colorBySpeed) y soporta transparencia de fabrica.
    public static class ParticleMaterialFactory
    {
        static Shader shaderCache;

        static Shader ShaderParticulas()
        {
            if (shaderCache != null && shaderCache.isSupported) return shaderCache;
            shaderCache = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shaderCache == null || !shaderCache.isSupported) shaderCache = Shader.Find("Particles/Standard Unlit");
            if (shaderCache == null || !shaderCache.isSupported) shaderCache = Shader.Find("Sprites/Default");
            return shaderCache;
        }

        // color: tinte base (se multiplica con colorOverLifetime en el shader de particulas).
        // textura: opcional (sprite de humo/chispa/etc); null = blanco solido (sirve para mallas 3D como esferas/cubos).
        public static Material CreateTransparent(Color color, Texture2D textura = null)
        {
            var shader = ShaderParticulas();
            Material mat;
            bool esShaderDeParticulas = shader != null && shader.isSupported && shader.name.Contains("Particles");
            if (shader != null && shader.isSupported)
            {
                mat = new Material(shader);
                if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f); // 1 = Transparent
                if (mat.HasProperty("_Blend")) mat.SetFloat("_Blend", 0f);     // 0 = Alpha
                if (mat.HasProperty("_ColorMode")) mat.SetFloat("_ColorMode", 0f); // Multiply: respeta colorOverLifetime
                mat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
                mat.SetOverrideTag("RenderType", "Transparent");
                mat.renderQueue = (int)RenderQueue.Transparent;
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                if (!esShaderDeParticulas)
                {
                    // Sprites/Default u otro fallback: no tiene _Surface/_Blend, pero
                    // ya viene transparente de fabrica.
                }
            }
            else
            {
                mat = SafeMaterial.Create(color);
            }
            mat.hideFlags = HideFlags.HideAndDontSave;

            if (textura != null)
            {
                if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", textura);
                else if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", textura);
            }

            mat.color = color;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            return mat;
        }

        static Mesh cuboMesh, icosferaMesh;

        // BUG DE PERFORMANCE encontrado al medir esta ronda ("revisa q el
        // performance sea optimizado"): la esfera PRIMITIVA de Unity
        // (GameObject.CreatePrimitive(PrimitiveType.Sphere)) tiene 768
        // triangulos -- pensada para un objeto solido de la escena, no
        // para una particula diminuta de fondo. El polvo del helicoptero
        // sostiene ~150 particulas a la vez: 150 x 768 = ~115.000
        // triangulos solo en polvo, medido con ~10 FPS de caida de cerca.
        // MallaEsfera() ahora devuelve un icosaedro de 20 triangulos (una
        // "esfera baja poli") en vez de la esfera pesada: a los tamaños
        // chicos con los que se usan estas particulas (chispas, polvo) la
        // diferencia visual es imperceptible, pero es 38 veces mas barato.
        public static Mesh MallaEsfera()
        {
            if (icosferaMesh != null) return icosferaMesh;
            float phi = (1f + Mathf.Sqrt(5f)) / 2f;
            var v = new[]
            {
                new Vector3(-1, phi, 0), new Vector3(1, phi, 0), new Vector3(-1, -phi, 0), new Vector3(1, -phi, 0),
                new Vector3(0, -1, phi), new Vector3(0, 1, phi), new Vector3(0, -1, -phi), new Vector3(0, 1, -phi),
                new Vector3(phi, 0, -1), new Vector3(phi, 0, 1), new Vector3(-phi, 0, -1), new Vector3(-phi, 0, 1),
            };
            for (int i = 0; i < v.Length; i++) v[i] = v[i].normalized * 0.5f; // radio 0.5: mismo tamaño de referencia que el primitivo (diametro 1 a escala 1)
            var tris = new[]
            {
                0,11,5, 0,5,1, 0,1,7, 0,7,10, 0,10,11,
                1,5,9, 5,11,4, 11,10,2, 10,7,6, 7,1,8,
                3,9,4, 3,4,2, 3,2,6, 3,6,8, 3,8,9,
                4,9,5, 2,4,11, 6,2,10, 8,6,7, 9,8,1,
            };
            var mesh = new Mesh { name = "IcosferaBaja" };
            mesh.vertices = v;
            mesh.triangles = tris;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            icosferaMesh = mesh;
            return icosferaMesh;
        }

        // Mallas de baja resolucion para ParticleSystemRenderMode.Mesh (particulas
        // que se ven como cubos 3D reales en vez de cuadrados de cartel mirando
        // siempre a camara). Se toma de un primitivo temporal: la malla es un
        // asset built-in de Unity (no se instancia), solo se destruye el
        // GameObject portador. El cubo YA es barato de fabrica (12 triangulos),
        // a diferencia de la esfera, asi que no hace falta reemplazarlo.
        public static Mesh MallaCubo()
        {
            if (cuboMesh != null) return cuboMesh;
            var temp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cuboMesh = temp.GetComponent<MeshFilter>().sharedMesh;
            if (Application.isPlaying) Object.Destroy(temp); else Object.DestroyImmediate(temp);
            return cuboMesh;
        }
    }
}
