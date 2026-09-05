// Hermano de SP/ArmaEnPrimeraPersona, con la unica diferencia que importa:
// este SI muestra una textura.
//
// Hizo falta uno aparte porque el del arma declara solo _BaseColor y su
// vertex ni siquiera lee UVs, asi que material.mainTexture ahi no hace
// nada: se le asignaba la RenderTexture de la optica y el tubo salia
// blanco. Todo lo demas (ZTest Always, ZWrite Off) es igual y por el mismo
// motivo: una mira que se mete adentro de la pared no sirve de nada.
Shader "SP/MiraOptica"
{
    Properties
    {
        // [MainTexture] es lo que hace que material.mainTexture apunte
        // aca, igual que [MainColor] para material.color en el shader del
        // arma. Sin el atributo, el atajo busca un _MainTex que no existe
        // y la asignacion se pierde en silencio.
        [MainTexture] _BaseMap ("Imagen de la optica", 2D) = "white" {}
        [MainColor] _BaseColor ("Color", Color) = (1,1,1,1)
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Overlay" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "MiraSiempreAdelante"
            ZTest Always
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; };
            struct Varyings   { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; float2 uv : TEXCOORD1; };

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _BaseMap_ST;
            CBUFFER_END

            Varyings vert (Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
                return o;
            }

            half4 frag (Varyings i) : SV_Target
            {
                half4 img = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv);
                // El sombreado va MUY suave: sobre la imagen de la optica
                // un gradiente fuerte se lee como suciedad en el vidrio.
                float luz = saturate(dot(normalize(i.normalWS), normalize(float3(0.3, 1.0, 0.2))));
                float sombra = 0.85 + 0.15 * luz;
                return half4(img.rgb * _BaseColor.rgb * sombra, 1);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
