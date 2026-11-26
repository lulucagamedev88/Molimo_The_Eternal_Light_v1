Shader "CustomMagicBall"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        [HDR] _Color ("Tint", Color) = (1,1,1,1)
        [HDR] _EmissionColor ("Emission Color", Color) = (1,1,1,1)
        [HDR] _FresnelColor ("Fresnel Color", Color) = (1,1,1,1)
        _FresnelPower ("Fresnel Power", Range(0.1, 8)) = 2.0
        _NoiseScale ("Noise Scale (UV)", Float) = 8.0
        _NoiseSpeed ("Noise Speed", Float) = 1.0
        _LightInfluence ("Light Influence", Range(0,1)) = 1.0
        _ColorLevels ("Color Levels", Range(2, 64)) = 16.0
        _GlowIntensity ("Glow Intensity", Range(0, 5)) = 1.5
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "MagicBall"
            Tags { "LightMode" = "UniversalForward" }

            Cull Back
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4  _Color;
                half4  _EmissionColor;
                half4  _FresnelColor;
                float  _FresnelPower;
                float  _NoiseScale;
                float  _NoiseSpeed;
                float  _LightInfluence;
                float  _ColorLevels;
                float  _GlowIntensity;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                float3 normalOS   : NORMAL;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
                float3 normalWS    : TEXCOORD1;
                float3 positionWS  : TEXCOORD2;
            };

            // ---------- Noise ----------
            float hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float valueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);

                float a = hash21(i);
                float b = hash21(i + float2(1, 0));
                float c = hash21(i + float2(0, 1));
                float d = hash21(i + float2(1, 1));

                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            // ---------- Vertex ----------
            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs posInputs = GetVertexPositionInputs(IN.positionOS.xyz);

                OUT.positionHCS = posInputs.positionCS;
                OUT.positionWS  = posInputs.positionWS;
                OUT.uv          = TRANSFORM_TEX(IN.uv, _MainTex);
                OUT.normalWS    = TransformObjectToWorldNormal(IN.normalOS);

                return OUT;
            }

            // ---------- Fragment ----------
            half4 frag(Varyings IN) : SV_Target
            {
                // UV do objeto (esfera padrão: longitude = x, latitude = y)
                half4 tex       = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv);
                half3 baseColor = tex.rgb * _Color.rgb;

                half3 normalWS  = normalize(IN.normalWS);
                half3 viewDirWS = normalize(GetWorldSpaceViewDir(IN.positionWS));

                // ---- Sample Lit (luz principal da URP) ----
                Light mainLight = GetMainLight();
                half3 lightDir  = normalize(mainLight.direction);
                half3 lightCol  = mainLight.color;

                half NdotL    = saturate(dot(normalWS, lightDir));
                half3 diffuse = lightCol * NdotL * _LightInfluence
                              + (1.0 - _LightInfluence);

                // ---- Fresnel (aura) ----
                half  fresnel     = pow(1.0 - saturate(dot(normalWS, viewDirWS)), _FresnelPower);
                half3 fresnelGlow = fresnel * _FresnelColor.rgb;

                // ---- Ruído animado nas UVs do objeto ----
                float2 noiseUV = IN.uv * _NoiseScale + _Time.y * _NoiseSpeed;
                float  noise   = valueNoise(noiseUV);
                noise = valueNoise(noiseUV + noise * 0.5);

                // ---- Composição ----
                half3 finalColor  = baseColor * diffuse;
                finalColor += fresnelGlow * _EmissionColor.rgb;
                finalColor += _EmissionColor.rgb * noise * fresnel * _GlowIntensity;

                // ---- Quantização (pixel art look) ----
                finalColor = floor(finalColor * _ColorLevels) / _ColorLevels;

                return half4(finalColor, 1.0);
            }
            ENDHLSL
        }
    }

    Fallback Off
}