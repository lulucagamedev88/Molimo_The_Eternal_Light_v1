Shader "Simple Toon/URP/STTerrain"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}

        [Header(Layered Texture)][Space(5)]
        [Toggle] _LayerEnable  ("Enable Layers",       Float) = 0
        _SecondaryTex          ("Secondary Texture",   2D)    = "white" {}
        _MaskTex               ("Mask (Black/White)",  2D)    = "white" {}
        _LayerBlend            ("Blend Strength",      Range(0,1)) = 1
        [Toggle] _MaskInvert   ("Invert Mask",         Float) = 0

        [Header(Colorize)][Space(5)]
        _Color ("Color", COLOR) = (1,1,1,1)
        _AmbientCol ("Ambient", Range(0,1)) = 0

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

        [Header(AntiTile)][Space(5)]
        [Toggle] _ATEnable    ("Enable AntiTile",    Float) = 1
        _ATScale              ("Texture Scale",      Range(1, 200)) = 20
        _ATRandomize          ("Position Randomize", Range(0, 1))   = 0.5
        _ATRotation           ("Rotation Randomize", Range(0, 1))   = 0.3
        _ATSmoothness         ("Smoothness",         Range(0.001, 0.5)) = 0.15

        [Header(WorldGradient)][Space(5)]
        [Toggle] _GradEnable    ("Enable Gradient",    Float) = 0
        [Toggle] _GradAxis      ("Axis", Float) = 0
        _GradColor1             ("Color Start",        COLOR) = (1,1,1,1)
        _GradColor2             ("Color End",          COLOR) = (1,1,1,1)
        _GradCenter             ("Center (World Pos)", Float) = 0
        _GradSize               ("Size (World Units)", Float) = 20
        _GradSmoothness         ("Smoothness",         Range(0, 1)) = 0.5
        _GradBlendMode          ("Blend Mode", Float) = 3
        _GradBlend              ("Blend Strength",     Range(0, 1)) = 1

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
                float2 uvRaw : TEXCOORD3;
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
                o.uvRaw = v.uv;
                o.worldNormal = normalInput.normalWS;
                o.worldPos = vertexInput.positionWS;
                o.viewDir = GetWorldSpaceNormalizeViewDir(vertexInput.positionWS);

                return o;
            }

            float4 frag (v2f i) : SV_Target
            {
                float3 normal = normalize(i.worldNormal);
                float3 viewDir = normalize(i.viewDir);

                float4 shadowCoord = TransformWorldToShadowCoord(i.worldPos);
                Light mainLight = GetMainLight(shadowCoord);

                float3 lightDir = normalize(mainLight.direction);
                float atten = mainLight.distanceAttenuation * mainLight.shadowAttenuation;

                float NdotL = dot(normal, lightDir);
                float3 halfVec = normalize(lightDir + viewDir);
                float NdotH = dot(normal, halfVec);
                float VdotN = dot(viewDir, normal);
                float FdotV = dot(GetWorldSpaceNormalizeViewDir(i.worldPos), -viewDir);

                float3 ambientLight = SampleSH(normal);

                // ---- Toon step consciente do dia/noite ----
                float toonStep = ComputeToonShadowStep(mainLight, NdotL, atten);

                // ---- Layered Textures (Primary + Secondary + Mask) ----
                float4 texcol = SampleLayeredTexture(i.uv, i.uvRaw) * _Color;

                texcol.rgb = ApplyWorldGradient(texcol.rgb, i.worldPos);

                // Mistura rígida sombra/iluminado
                float3 finalRGB = lerp(texcol.rgb * ambientLight,
                                       texcol.rgb * (mainLight.color + ambientLight),
                                       toonStep);

                // ---- Fake Emissive Light ----
                float3 fakeLight = ComputeFakeEmissiveLight(i.worldPos, normal, toonStep);
                finalRGB += texcol.rgb * fakeLight;

                // ---- Cloud Shadow ----
                float cloudShadow = SampleCloudShadow(i.worldPos);
                finalRGB *= cloudShadow;

                float4 blendCol = float4(finalRGB, 1.0);
                float4 postCol = PostEffects(blendCol, toonStep, atten, NdotL, NdotH, VdotN, FdotV);

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