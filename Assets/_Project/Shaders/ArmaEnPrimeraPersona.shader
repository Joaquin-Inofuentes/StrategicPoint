// Del plan del usuario: "Al hacer zoom debe poner el arma adelante de todo".
//
// El arma de primera persona cuelga de la camara a 0,65 m y mide 0,22 de
// largo, asi que su cara delantera cae a ~0,76 m. El jugador puede pegarse
// a una pared hasta ~0,5 m de la camara: ahi el arma queda literalmente
// DENTRO de la pared y, compartiendo el mismo buffer de profundidad que el
// mundo, la pared gana y el arma desaparece. Comprobado con una captura
// contra el Muro: la pantalla entera es pared y no se ve nada del arma.
//
// Subir el renderQueue no alcanza -- la cola cambia el ORDEN de dibujado,
// no el test de profundidad: la pared se dibuja primero, escribe su
// profundidad, y el arma se descarta igual. Y URP/Lit no expone _ZTest, asi
// que tampoco se puede forzar por material desde codigo.
//
// De ahi este shader. Un arma en primera persona no es geometria del mundo:
// es interfaz, y se dibuja siempre por delante. Es lo que hace cualquier
// shooter.
Shader "SP/ArmaEnPrimeraPersona"
{
    Properties
    {
        // SOLO _BaseColor, a proposito. Si ademas se declara _Color, el
        // atajo material.color de Unity se queda con ese y el shader pinta
        // con _BaseColor, que sigue en blanco: el arma salia palida y sin
        // el color de su tipo. Con una sola propiedad, material.color cae
        // en _BaseColor y el codigo que ya existe funciona sin tocarlo.
        // [MainColor] es lo que hace que material.color apunte aca. Sin
        // el atributo, ese atajo busca un _Color que no existe y el
        // color asignado por codigo se pierde: el arma quedaba blanca
        // por mas que el catalogo le pusiera el celeste del rifle.
        [MainColor] _BaseColor ("Color", Color) = (1,1,1,1)
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Overlay" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "ArmaSiempreAdelante"
            // Lo unico que de verdad importa: pasar siempre el test de
            // profundidad. ZWrite queda apagado para no ensuciar el buffer
            // con una profundidad que miente sobre donde esta el arma.
            ZTest Always
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings   { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; };

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
            CBUFFER_END

            Varyings vert (Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                return o;
            }

            half4 frag (Varyings i) : SV_Target
            {
                // Sombreado minimo: sin ningun gradiente el cubo se ve como
                // una silueta plana y no se le distinguen las caras.
                float luz = saturate(dot(normalize(i.normalWS), normalize(float3(0.3, 1.0, 0.2))));
                float sombra = 0.55 + 0.45 * luz;
                half4 c = _BaseColor;
                return half4(c.rgb * sombra, 1);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
