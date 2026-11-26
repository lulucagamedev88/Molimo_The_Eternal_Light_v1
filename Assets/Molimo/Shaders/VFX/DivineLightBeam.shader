Shader "Custom/DivineLightBeam"
{
    Properties
    {
        [HDR] _BaseColor("Beam Color", Color) = (1, 0.75, 0.35, 1)
        _Intensity("Intensity", Range(0, 20)) = 5

        _VerticalGradient("Vertical Gradient", Range(0.1, 8)) = 2
        _EdgeSoftness("Edge Softness", Range(0.1, 8)) = 2

        _NoiseScale("Noise Scale", Range(0, 20)) = 3
        _NoiseStrength("Noise Strength", Range(0, 1)) = 0.15
        _NoiseSpeed("Noise Speed", Range(-5, 5)) = 0.3
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent"
        }

        // Additive = brilho
        Blend One One

        // O beam não precisa escrever profundidade
        ZWrite Off

        // Visível pelos dois lados
        Cull Off

        Pass
        {
            Name "DivineLightBeam"

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
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)

                half4 _BaseColor;
                float _Intensity;

                float _VerticalGradient;
                float _EdgeSoftness;

                float _NoiseScale;
                float _NoiseStrength;
                float _NoiseSpeed;

            CBUFFER_END


            Varyings vert(Attributes IN)
            {
                Varyings OUT;

                OUT.positionHCS =
                    TransformObjectToHClip(IN.positionOS.xyz);

                OUT.uv = IN.uv;

                return OUT;
            }


            // ----------------------------------------
            // SIMPLE NOISE
            // ----------------------------------------

            float hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));

                p += dot(
                    p,
                    p + 45.32
                );

                return frac(p.x * p.y);
            }


            float noise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);

                f = f * f * (3.0 - 2.0 * f);

                float a = hash21(i);
                float b = hash21(i + float2(1, 0));
                float c = hash21(i + float2(0, 1));
                float d = hash21(i + float2(1, 1));

                return lerp(
                    lerp(a, b, f.x),
                    lerp(c, d, f.x),
                    f.y
                );
            }


            half4 frag(Varyings IN) : SV_Target
            {
                float2 uv = IN.uv;


                // ========================================
                // 1. VERTICAL GRADIENT
                // ========================================
                //
                // 0 = transparente
                // 0.5 = forte
                // 1 = transparente
                //

                float vertical =
                    1.0 - abs(uv.y * 2.0 - 1.0);

                vertical =
                    pow(
                        saturate(vertical),
                        _VerticalGradient
                    );


                // ========================================
                // 2. HORIZONTAL GRADIENT
                // ========================================
                //
                // Suaviza as bordas laterais
                //

                float horizontal =
                    1.0 - abs(uv.x * 2.0 - 1.0);

                horizontal =
                    pow(
                        saturate(horizontal),
                        _EdgeSoftness
                    );


                // ========================================
                // 3. NOISE
                // ========================================

                float2 noiseUV =
                    uv * _NoiseScale;

                noiseUV.y +=
                    _Time.y * _NoiseSpeed;

                float n =
                    noise(noiseUV);

                n = lerp(
                    1.0 - _NoiseStrength,
                    1.0,
                    n
                );


                // ========================================
                // 4. FINAL ALPHA
                // ========================================

                float alpha =
                    vertical *
                    horizontal *
                    n;


                // ========================================
                // 5. FINAL COLOR
                // ========================================

                float3 color =
                    _BaseColor.rgb *
                    _Intensity *
                    alpha;


                return half4(
                    color,
                    alpha
                );
            }

            ENDHLSL
        }
    }
}