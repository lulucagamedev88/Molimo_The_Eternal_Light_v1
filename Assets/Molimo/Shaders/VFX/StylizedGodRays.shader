Shader "Molimo/Environment/StylizedGodRays"
{
    Properties
    {
        [HDR] _Color ("Cor do Raio (HDR)", Color) = (1, 0.9, 0.7, 1)
        _Tiling ("Tiling (UV)", Vector) = (1, 1, 0, 0)
        _Rotation ("Rotação (Graus)", Range(0, 360)) = 45.0
        _RayDensity ("Densidade dos Raios", Float) = 8.0
        _RayWidth ("Largura dos Raios", Range(0.1, 1)) = 0.8
        _Softness ("Suavidade das Bordas", Range(0.1, 5)) = 2.0
        _FadePower ("Desvanecer na Base", Range(0.1, 5)) = 2.0
        _Intensity ("Intensidade Global", Range(0, 5)) = 1.5
    }

    SubShader
    {
        Tags 
        { 
            "RenderType" = "Transparent" 
            "Queue" = "Transparent" 
            "RenderPipeline" = "UniversalPipeline" 
        }
        
        // 🌟 AQUI ESTÁ A MÁGICA PARA MESCLAR COM O FUNDO
        // SrcAlpha = Usa a transparência do raio
        // One = Soma à cor que já está atrás
        Blend SrcAlpha One 
        
        ZWrite Off
        Cull Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float4 _Tiling;
                float _Rotation;
                float _RayDensity;
                float _RayWidth;
                float _Softness;
                float _FadePower;
                float _Intensity;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // 1. Aplica o Tiling
                float2 tiledUV = IN.uv * _Tiling.xy;

                // 2. Aplica a Rotação
                float2 center = float2(0.5, 0.5);
                tiledUV -= center;
                float rad = radians(_Rotation);
                float s = sin(rad);
                float c = cos(rad);
                tiledUV = float2(tiledUV.x * c - tiledUV.y * s, tiledUV.x * s + tiledUV.y * c);
                tiledUV += center;

                // 3. Cria as faixas verticais
                float val = tiledUV.x * _RayDensity;

                // 4. Cria a onda triangular suave
                float fracVal = frac(val);
                float ray = 1.0 - abs(fracVal * 2.0 - 1.0);

                // 5. Aplica largura e suavidade
                ray = smoothstep(1.0 - _RayWidth, 1.0, ray);
                ray = pow(ray, _Softness);

                // 6. Desvanecimentos
                float fadeBottom = pow(saturate(IN.uv.y), _FadePower);
                float fadeEdges = smoothstep(0.0, 0.1, IN.uv.x) * smoothstep(0.0, 0.1, 1.0 - IN.uv.x);

                // 7. Máscara final (Alpha)
                float alpha = ray * fadeBottom * fadeEdges;
                half3 finalColor = _Color.rgb * _Intensity;

                // 🌟 RETORNA A COR E A TRANSPARÊNCIA (Alpha) EM VEZ DE 1.0
                return half4(finalColor, alpha);
            }
            ENDHLSL
        }
    }
}