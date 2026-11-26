Shader "Custom/URP_GlintSweepWithEmission"
{
    Properties
    {
        _MainTex ("Base Map (RGB)", 2D) = "white" {}
        _BaseColor ("Base Color", Color) = (1, 1, 1, 1)

        // --- NOVO: Emissor ---
        [HDR] _EmissionColor ("Emissor (HDR)", Color) = (0, 0, 0, 1)
        _EmissionMap ("Mapa Emissor (Opcional)", 2D) = "white" {}

        // --- Glint Original ---
        [HDR] _GlintColor ("Glint Color", Color) = (1, 1, 1, 1)
        _SweepSpeed ("Sweep Speed", Float) = 1.5
        _SweepWidth ("Sweep Width", Range(0.01, 0.5)) = 0.15
        _PauseInterval ("Pause Interval", Float) = 2.0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        LOD 100

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
            };

            TEXTURE2D(_MainTex);      SAMPLER(sampler_MainTex);
            TEXTURE2D(_EmissionMap);  SAMPLER(sampler_EmissionMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _BaseColor;

                // Emissor
                float4 _EmissionColor;

                // Glint Parameters
                float4 _GlintColor;
                float _SweepSpeed;
                float _SweepWidth;
                float _PauseInterval;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // 1. Textura e Cor Base
                half4 albedo = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv) * _BaseColor;

                // 2. Emissor (Luz Fixa/Mapeada + HDR para Bloom)
                half3 emissionTex = SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, input.uv).rgb;
                half3 emissionEffect = emissionTex * _EmissionColor.rgb;

                // 3. Lógica do Glint (Sweep / Varredura com Pausa)
                float sweepDuration = 1.5 / max(_SweepSpeed, 0.001);
                float totalCycle = sweepDuration + _PauseInterval;
                float timeInCycle = fmod(_Time.y, totalCycle);

                half3 glintEffect = half3(0, 0, 0);

                if (timeInCycle < sweepDuration)
                {
                    // Progresso da linha de brilho ao longo da diagonal UV
                    float progress = timeInCycle / sweepDuration;
                    float uvPosition = input.uv.x + (input.uv.y * 0.5); // Inclinação do corte
                    float sweepPos = lerp(-0.5, 1.5, progress);

                    // Largura e suavidade da barra de brilho
                    float dist = abs(uvPosition - sweepPos);
                    float glintMask = smoothstep(_SweepWidth, 0.0, dist);

                    glintEffect = glintMask * _GlintColor.rgb;
                }

                // 4. Combinação Final (Base + Emissor + Glint)
                half3 finalColor = albedo.rgb + emissionEffect + glintEffect;

                return half4(finalColor, albedo.a);
            }
            ENDHLSL
        }
    }
}