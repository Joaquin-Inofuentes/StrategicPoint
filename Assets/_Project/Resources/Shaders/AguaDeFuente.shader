// Agua de las fuentes de la ciudad (URP, sin luz, translucida): ondas que se desplazan, brillo en las crestas y borde que se aclara
// mirando de costado. _Flujo = 0 es la superficie del espejo de agua; _Flujo = 1 es el chorro (las franjas suben y se ven mas claras).
// Vive en Resources/ para que entre en la build aunque ningun material de escena lo referencie (se crea en runtime: SP.Presentation.FuentesDeAgua).
Shader "SP/AguaDeFuente"
{
    Properties
    {
        _ColorProfundo ("Color profundo", Color) = (0.08, 0.35, 0.62, 1)
        _ColorClaro ("Color claro", Color) = (0.55, 0.88, 1, 1)
        _Alfa ("Opacidad", Range(0, 1)) = 0.93
        _Escala ("Escala de las ondas", Float) = 2.4
        _Velocidad ("Velocidad", Float) = 1.2
        _Brillo ("Brillo de crestas", Float) = 0.7
        _Flujo ("0 = superficie, 1 = chorro", Range(0, 1)) = 0
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            Name "Agua"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _ColorProfundo;
                half4 _ColorClaro;
                half _Alfa;
                float _Escala;
                float _Velocidad;
                half _Brillo;
                half _Flujo;
            CBUFFER_END

            struct Attributes { float4 posOS : POSITION; float3 nOS : NORMAL; };
            struct Varyings { float4 posCS : SV_POSITION; float3 posWS : TEXCOORD0; float3 nWS : TEXCOORD1; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.posWS = TransformObjectToWorld(v.posOS.xyz);
                o.nWS = TransformObjectToWorldNormal(v.nOS);
                o.posCS = TransformWorldToHClip(o.posWS);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float t = _Time.y * _Velocidad;
                float3 p = i.posWS;
                // Superficie: tres ondas cruzadas. Chorro: franjas verticales que suben con un poco de ruido horizontal.
                float s1 = sin(p.x * _Escala + t * 1.7) * sin(p.z * _Escala * 1.31 - t * 1.2);
                float s2 = sin((p.x + p.z) * _Escala * 0.73 + t * 1.1);
                float s3 = sin((p.x - p.z) * _Escala * 1.9 - t * 2.3);
                float superficie = (s1 + s2 * 0.6 + s3 * 0.4) * 0.5 + 0.5;
                float chorro = sin(p.y * 11.0 - t * 7.0 + sin((p.x + p.z) * 5.0) * 1.3) * 0.5 + 0.5;
                float onda = lerp(superficie, chorro, _Flujo);

                float3 v = normalize(GetCameraPositionWS() - p);
                float fres = pow(1.0 - saturate(abs(dot(normalize(i.nWS), v))), 3.0);
                half3 col = lerp(_ColorProfundo.rgb, _ColorClaro.rgb, saturate(onda * 0.55 + fres * 0.35));
                col += smoothstep(0.9, 1.0, onda) * _Brillo * _ColorClaro.rgb * 0.3;   // destellos en las crestas
                half a = saturate(_Alfa + fres * 0.18 + _Flujo * 0.1);
                return half4(col, a);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
