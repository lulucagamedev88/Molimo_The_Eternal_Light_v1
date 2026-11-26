Shader "Simple Toon/URP/STDefault"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}

        [Header(Colorize)][Space(5)]
        _Color ("Color", COLOR) = (1,1,1,1)
        _AmbientCol ("Ambient", Range(0,1)) = 0

        [Header(Emissive)][Space(5)]
        [Toggle] _EmissiveEnable ("Enable Emissive", Float) = 0
        [HDR] _EmissiveColor ("Emissive Color", COLOR) = (0,0,0,1)
        _EmissiveTex ("Emissive Texture", 2D) = "black" {}
        _EmissiveIntensity ("Emissive Intensity", Range(0,10)) = 1

        [Header(Detail)][Space(5)]
        [Toggle] _Segmented ("Segmented", Float) = 1
        _Steps ("Steps", Range(1,25)) = 3
        _StpSmooth ("Smoothness", Range(0,1)) = 0
        _Offset ("Lit Offset", Range(-1,1.1)) = 0

        [Header(Light)][Space(5)]
        [Toggle] _Clipped ("Clipped", Float) = 0
        _MinLight ("Min Light", Range(0,1)) = 0
        _MaxLight ("Max Light", Range(0,1)) = 1
        _Lumin ("Luminocity", Range(0,2)) = 0

        [Header(Shine)][Space(5)]
        [HDR] _ShnColor ("Color", COLOR) = (1,1,0,1)
        [Toggle] _ShnOverlap ("Overlap", Float) = 0
        _ShnIntense ("Intensity", Range(0,1)) = 0
        _ShnRange ("Range", Range(0,1)) = 0.15
        _ShnSmooth ("Smoothness", Range(0,1)) = 0

        [Header(Cloud Shadow)][Space(5)]
        [Toggle] _CloudShadowEnable ("Enable Cloud Shadow", Float) = 0
        _CloudShadowTex ("Cloud Shadow Tex (CRT)", 2D) = "white" {}
        _CloudShadowStrength ("Strength", Range(0,1)) = 1
        _CloudShadowWorldSize ("World Size (XZ)", Float) = 50
        _CloudShadowCenterX ("World Center X", Float) = 0
        _CloudShadowCenterZ ("World Center Z", Float) = 0
        _CloudShadowTiling ("Tiling", Float) = 1
        [Toggle] _CloudShadowInvert ("Invert (white=cloud)", Float) = 0

        [Header(Fake Emissive Light)][Space(5)]
        [Toggle] _FakeLightEnable     ("Enable Fake Light",   Float) = 0
        [Toggle] _FakeLightOnlyShadow ("Only Shadow Area",    Float) = 1
        _FakeLightNdotL               ("Normal Influence",    Range(0,1)) = 0.5
        _FakeLightNightThreshold      ("Night Threshold",     Range(0,1)) = 0.15

        // Escondidos (mantêm layout do CBUFFER partilhado com STTerrain)
        [HideInInspector] _ATEnable    ("ATEnable",    Float) = 0
        [HideInInspector] _ATScale     ("ATScale",     Float) = 20
        [HideInInspector] _ATRandomize ("ATRandomize", Float) = 0
        [HideInInspector] _ATRotation  ("ATRotation",  Float) = 0
        [HideInInspector] _ATSmoothness("ATSmoothness",Float) = 0.15

        [HideInInspector] _GradEnable    ("GradEnable",    Float) = 0
        [HideInInspector] _GradAxis      ("GradAxis",      Float) = 0
        [HideInInspector] _GradColor1    ("GradColor1",    COLOR) = (1,1,1,1)
        [HideInInspector] _GradColor2    ("GradColor2",    COLOR) = (1,1,1,1)
        [HideInInspector] _GradCenter    ("GradCenter",    Float) = 0
        [HideInInspector] _GradSize      ("GradSize",      Float) = 20
        [HideInInspector] _GradSmoothness("GradSmoothness",Float) = 0.5
        [HideInInspector] _GradBlend     ("GradBlend",     Float) = 1

        // Layered textures (mantêm layout do CBUFFER partilhado)
        [HideInInspector] _SecondaryTex ("SecondaryTex", 2D) = "white" {}
        [HideInInspector] _MaskTex      ("MaskTex",      2D) = "white" {}
        [HideInInspector] _LayerEnable  ("LayerEnable",  Float) = 0
        [HideInInspector] _LayerBlend   ("LayerBlend",   Float) = 1
        [HideInInspector] _MaskInvert   ("MaskInvert",   Float) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT

            #include "STCore.hlsl"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 worldNormal : NORMAL;
                float3 worldPos : TEXCOORD1;
                float3 viewDir : TEXCOORD2;
            };

            v2f vert (appdata v)
            {
                v2f o;
                VertexPositionInputs vertexInput = GetVertexPositionInputs(v.vertex.xyz);
                VertexNormalInputs normalInput = GetVertexNormalInputs(v.normal);

                o.pos = vertexInput.positionCS;
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.worldNormal = normalInput.normalWS;
                o.worldPos = vertexInput.positionWS;
                o.viewDir = GetWorldSpaceNormalizeViewDir(vertexInput.positionWS);

                return o;
            }

            float4 frag (v2f i) : SV_Target
            {
                float3 normal  = normalize(i.worldNormal);
                float3 viewDir = normalize(i.viewDir);

                float4 shadowCoord = TransformWorldToShadowCoord(i.worldPos);
                Light mainLight = GetMainLight(shadowCoord);

                float3 lightDir = normalize(mainLight.direction);
                float  atten    = mainLight.distanceAttenuation * mainLight.shadowAttenuation;

                float NdotL = dot(normal, lightDir);
                float3 halfVec = normalize(lightDir + viewDir);
                float NdotH = dot(normal, halfVec);
                float VdotN = dot(viewDir, normal);
                float FdotV = dot(GetWorldSpaceNormalizeViewDir(i.worldPos), -viewDir);

                float toon = Toon(NdotL, atten);

                float4 texcol = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv) * _Color;

                float3 ambientLight = SampleSH(normal) * _AmbientCol + (1.0 - _AmbientCol);

                float3 litCol    = texcol.rgb * (mainLight.color + ambientLight);
                float3 shadowCol = texcol.rgb * ambientLight;
                float3 finalRGB  = lerp(shadowCol, litCol, toon);

                // ---- Fake Emissive Light (consciente do dia/noite) ----
                float shadowStep = ComputeToonShadowStep(mainLight, NdotL, atten);
                float3 fakeLight = ComputeFakeEmissiveLight(i.worldPos, normal, shadowStep);
                finalRGB += texcol.rgb * fakeLight;

                // ---- Emissive (cor + textura) ----
                if (_EmissiveEnable > 0.5)
                {
                    float3 emissiveTex = SAMPLE_TEXTURE2D(_EmissiveTex, sampler_EmissiveTex, i.uv).rgb;
                    float3 emissive    = emissiveTex * _EmissiveColor.rgb * _EmissiveIntensity;
                    finalRGB += emissive;
                }

                float cloudShadow = SampleCloudShadow(i.worldPos);
                finalRGB *= cloudShadow;

                float4 blendCol = float4(finalRGB, 1.0);
                float4 postCol  = PostEffects(blendCol, toon, atten, NdotL, NdotH, VdotN, FdotV);

                postCol.a = 1.0;
                return postCol;
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags{"LightMode" = "ShadowCaster"}

            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
            };

            Varyings ShadowPassVertex(Attributes input)
            {
                Varyings output;
                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = vertexInput.positionCS;
                return output;
            }

            half4 ShadowPassFragment(Varyings input) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }
    }
}