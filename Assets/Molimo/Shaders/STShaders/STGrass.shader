Shader "Simple Toon/URP/STGrass"
{
    Properties
    {
        [Header(Texture)][Space(5)]
        _MainTex ("Grass Texture (RGBA)", 2D) = "white" { }
        _Color("Base Tint", COLOR) = (1, 1, 1, 1)

        [Header(Alpha)][Space(5)]
        [Toggle] _AlphaClip("Alpha Clip", Float) = 1
        _Cutoff("Alpha Cutoff", Range(0, 1)) = 0.5

        [Header(Gradient)][Space(5)]
        _TopColor("Tip Color (Top)", COLOR) = (0.70, 1.00, 0.40, 1)
        _BottomColor("Root Color (Bottom)", COLOR) = (0.10, 0.25, 0.08, 1)
        _GradBias("Gradient Position", Range(0, 1)) = 0.5
        _GradSmoothness("Gradient Smoothness", Range(0.001, 1)) = 0.8
        _HeightVariation("Height Variation (World Y)", Range(0, 1)) = 0.1

        [Header(Colorize)][Space(5)]
        _AmbientCol ("Ambient", Range(0,1)) = 0

        [Header(Toon)][Space(5)]
        [Toggle] _Segmented("Segmented", Float) = 1
        _Steps("Steps", Range(1, 25)) = 3
        _Offset("Lit Offset", Range(-1, 1.1)) = 0

        [Header(Light)][Space(5)]
        [Toggle] _Clipped ("Clipped", Float) = 0
        _MinLight("Min Light", Range(0, 1)) = 0.2
        _MaxLight("Max Light", Range(0, 1)) = 1
        _Lumin("Luminocity", Range(0, 2)) = 0

        [Header(Fake Emissive Light)][Space(5)]
        [Toggle] _FakeLightEnable     ("Enable Fake Light",   Float) = 0
        [Toggle] _FakeLightOnlyShadow ("Only Shadow Area",    Float) = 1
        _FakeLightNdotL               ("Normal Influence",    Range(0,1)) = 0.5
        _FakeLightNightThreshold      ("Night Threshold",     Range(0,1)) = 0.15
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "TransparentCutout"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "AlphaTest"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Cull Off
            ZWrite On

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            #define MAX_FAKE_LIGHTS 20

            float4 _FakeLightPosArray  [MAX_FAKE_LIGHTS];
            float4 _FakeLightColorArray[MAX_FAKE_LIGHTS];
            float4 _FakeLightDataArray [MAX_FAKE_LIGHTS];

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _Color;
                float4 _BottomColor;
                float4 _TopColor;
                float _AlphaClip;
                float _Cutoff;
                float _GradBias;
                float _GradSmoothness;
                float _HeightVariation;

                float _AmbientCol;
                float _Segmented;
                float _Steps;
                float _Offset;
                float _Clipped;
                float _MinLight;
                float _MaxLight;
                float _Lumin;

                float _FakeLightEnable;
                float _FakeLightOnlyShadow;
                float _FakeLightNdotL;
                float _FakeLightNightThreshold;
            CBUFFER_END

            float Toon(float NdotL, float atten)
            {
                float minL = _MinLight;
                float maxL = max(_MinLight, _MaxLight);
                float stepsVal = _Segmented > 0.5 ? max(_Steps, 1.0) : 1.0;

                float light = NdotL * 0.5 + 0.5 + _Offset;
                light = saturate(light);

                if (_Clipped > 0.5)
                    light = smoothstep(minL, maxL, light);
                else
                    light = clamp(light, minL, maxL);

                if (_Segmented > 0.5)
                {
                    light = floor(light * stepsVal) / max(stepsVal - 1.0, 0.0001);
                    light = saturate(light);
                }
                return saturate(light * atten);
            }

            float ComputeToonShadowStep(Light mainLight, float NdotL, float atten)
            {
                float mainLum = max(max(mainLight.color.r, mainLight.color.g), mainLight.color.b);
                float dayFactor = smoothstep(0.0, max(_FakeLightNightThreshold, 0.001), mainLum);
                float baseStep = step(0.01, NdotL * atten + _Offset);
                return baseStep * dayFactor;
            }

            float3 ComputeFakeEmissiveLight(float3 worldPos, float3 worldNormal, float shadowStep)
            {
                if (_FakeLightEnable < 0.5)
                    return float3(0, 0, 0);

                float shadowMask = lerp(1.0, 1.0 - shadowStep, saturate(_FakeLightOnlyShadow));
                float3 total     = float3(0, 0, 0);
                float  ndlAmt    = saturate(_FakeLightNdotL);

                [unroll]
                for (int li = 0; li < MAX_FAKE_LIGHTS; li++)
                {
                    float4 posData = _FakeLightPosArray[li];
                    if (posData.w < 0.5) continue;

                    float4 colData  = _FakeLightColorArray[li];
                    float4 dataData = _FakeLightDataArray[li];

                    float3 emitterPos = posData.xyz;
                    float3 delta      = emitterPos - worldPos;
                    float  dist       = length(delta);
                    float3 L          = delta / max(dist, 1e-4);

                    float radius = max(dataData.x, 0.0001);
                    float soft   = max(dataData.y, 0.001);
                    float inner  = radius * (1.0 - soft);
                    float outer  = radius;
                    float radial = 1.0 - smoothstep(inner, outer, dist);

                    float NdotL = saturate(dot(worldNormal, L));
                    NdotL       = lerp(1.0, NdotL, ndlAmt);

                    total += colData.rgb * (NdotL * radial * shadowMask);
                }
                return total;
            }

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv     : TEXCOORD0;
                float3 normal : NORMAL;
                // Removido o float4 color : COLOR; (era usado só para o vento)
            };

            struct v2f
            {
                float4 pos         : SV_POSITION;
                float2 uv          : TEXCOORD0;
                float3 worldNormal : TEXCOORD1;
                float3 worldPos    : TEXCOORD2;
                float3 viewDir     : TEXCOORD3;
                float  gradU       : TEXCOORD4;
            };

            v2f vert(appdata v)
            {
                v2f o;

                // 🌟 O capim agora é estático (sem cálculo de vento)
                float3 posOS = v.vertex.xyz;

                VertexPositionInputs vpi = GetVertexPositionInputs(posOS);
                VertexNormalInputs  vni = GetVertexNormalInputs(v.normal);

                o.pos = vpi.positionCS;
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.worldNormal = vni.normalWS;
                o.worldPos = vpi.positionWS;
                o.viewDir = GetWorldSpaceNormalizeViewDir(vpi.positionWS);

                float worldYAdd = vpi.positionWS.y * _HeightVariation * 0.05;
                o.gradU = saturate(v.uv.y + worldYAdd);

                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                float4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);

                if (_AlphaClip > 0.5)
                {
                    clip(tex.a - _Cutoff);
                }

                float3 texRGB = tex.rgb * _Color.rgb;

                float band  = max(_GradSmoothness, 0.0001) * 0.5;
                float inner = _GradBias - band;
                float outer = _GradBias + band;

                float  gv      = smoothstep(inner, outer, i.gradU);
                float3 gradCol = lerp(_BottomColor.rgb, _TopColor.rgb, gv);

                float3 finalAlbedo = saturate(texRGB * gradCol * 2.0);

                float3 normal = normalize(i.worldNormal);

                float4 shadowCoord = TransformWorldToShadowCoord(i.worldPos);
                Light mainLight = GetMainLight(shadowCoord);

                float3 lightDir = normalize(mainLight.direction);
                float  atten    = mainLight.distanceAttenuation * mainLight.shadowAttenuation;

                float NdotL = dot(normal, lightDir);
                float toon  = Toon(NdotL, atten);

                float3 ambientLight = SampleSH(normal) * _AmbientCol + (1.0 - _AmbientCol);

                float3 litCol    = finalAlbedo * (mainLight.color + ambientLight);
                float3 shadowCol = finalAlbedo * ambientLight;
                float3 finalRGB  = lerp(shadowCol, litCol, toon);

                // ---- Fake Emissive Light (consciente do dia/noite) ----
                // Capim tem normais horizontais -> blend com UP para receber luz de cima
                float3 fakeNormal = normalize(lerp(normal, float3(0, 1, 0), 0.7));

                float shadowStep = ComputeToonShadowStep(mainLight, NdotL, atten);
                float3 fakeLight = ComputeFakeEmissiveLight(i.worldPos, fakeNormal, shadowStep);
                finalRGB += finalAlbedo * fakeLight;

                finalRGB += _Lumin;

                return float4(finalRGB, 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Off

            HLSLPROGRAM
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _Color;
                float4 _BottomColor;
                float4 _TopColor;
                float _AlphaClip;
                float _Cutoff;
                float _GradBias;
                float _GradSmoothness;
                float _HeightVariation;

                float _AmbientCol;
                float _Segmented;
                float _Steps;
                float _Offset;
                float _Clipped;
                float _MinLight;
                float _MaxLight;
                float _Lumin;

                float _FakeLightEnable;
                float _FakeLightOnlyShadow;
                float _FakeLightNdotL;
                float _FakeLightNightThreshold;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
            };

            Varyings ShadowPassVertex(Attributes input)
            {
                Varyings output;
                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = vertexInput.positionCS;
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                return output;
            }

            half4 ShadowPassFragment(Varyings input) : SV_Target
            {
                if (_AlphaClip > 0.5)
                {
                    float a = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv).a;
                    clip(a - _Cutoff);
                }
                return 0;
            }
            ENDHLSL
        }
    }
}