Shader "Simple Toon/URP/STWitchEye"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}

        [Header(Colorize)][Space(5)]
_Color ("Color", COLOR) = (1, 1, 1, 1)
        _AmbientCol("Ambient", Range(0, 1)) = 0

        [Header(Detail)][Space(5)]
        [Toggle] _Segmented("Segmented", Float) = 1
        _Steps("Steps", Range(1, 25)) = 3
        _StpSmooth("Smoothness", Range(0, 1)) = 0
        _Offset("Lit Offset", Range(-1, 1.1)) = 0

        [Header(Light)][Space(5)]
        [Toggle] _Clipped("Clipped", Float) = 0
        _MinLight("Min Light", Range(0, 1)) = 0
        _MaxLight("Max Light", Range(0, 1)) = 1
        _Lumin("Luminocity", Range(0, 2)) = 0

        [Header(Shine)][Space(5)]
        [HDR] _ShnColor("Color", COLOR) = (1, 1, 0, 1)
        [Toggle] _ShnOverlap("Overlap", Float) = 0
        _ShnIntense("Intensity", Range(0, 1)) = 0
        _ShnRange("Range", Range(0, 1)) = 0.15
        _ShnSmooth("Smoothness", Range(0, 1)) = 0

        [Header(Cloud Shadow)][Space(5)]
        [Toggle] _CloudShadowEnable("Enable Cloud Shadow", Float) = 0
        _CloudShadowTex("Cloud Shadow Tex (CRT)", 2D) = "white" { }
_CloudShadowStrength("Strength", Range(0, 1)) = 1
        _CloudShadowWorldSize("World Size (XZ)", Float) = 50
        _CloudShadowCenterX("World Center X", Float) = 0
        _CloudShadowCenterZ("World Center Z", Float) = 0
        _CloudShadowTiling("Tiling", Float) = 1
        [Toggle] _CloudShadowInvert("Invert (white=cloud)", Float) = 0

        [Header(Fake Emissive Light)][Space(5)]
        [Toggle] _FakeLightEnable("Enable Fake Light", Float) = 0
        [Toggle] _FakeLightOnlyShadow("Only Shadow Area", Float) = 1
        _FakeLightNdotL("Normal Influence", Range(0, 1)) = 0.5
        _FakeLightNightThreshold("Night Threshold", Range(0, 1)) = 0.15

        // ============================================================
        //  CLOTH SWAY (WitchEye) — deformação de vértices lowpoly
        // ============================================================
        [Header(Cloth Sway)][Space(5)]
        [Toggle] _ClothEnable("Enable Cloth Sway", Float) = 1
        _ClothTopY("Top Y (local, pinned)", Float) = 1.0
        _ClothBottomY("Bottom Y (local, free)", Float) = 0.0
        _ClothAmount("Sway Amount", Range(0, 0.5)) = 0.06
        _ClothSpeed("Sway Speed", Range(0, 5)) = 1.4
        _ClothFrequency("Sway Frequency", Range(0, 10)) = 3.0
        _ClothSecondary("Secondary Wave Mix", Range(0, 1)) = 0.4
        _ClothWindDir("Wind Direction (XYZ)", Vector) = (1, 0, 0, 0)
        _ClothWindStrength("Wind Strength", Range(0, 1)) = 0.35
        _ClothVerticalWobble("Vertical Wobble", Range(0, 1)) = 0.25
        _ClothRetroQuantize("Retro Quantize (steps)", Range(0, 12)) = 0

        // Escondidos (mantêm layout do CBUFFER partilhado com STTerrain)
        [HideInInspector] _ATEnable("ATEnable", Float) = 0
        [HideInInspector] _ATScale("ATScale", Float) = 20
        [HideInInspector] _ATRandomize("ATRandomize", Float) = 0
        [HideInInspector] _ATRotation("ATRotation", Float) = 0
        [HideInInspector] _ATSmoothness("ATSmoothness", Float) = 0.15

        [HideInInspector] _GradEnable("GradEnable", Float) = 0
        [HideInInspector] _GradAxis("GradAxis", Float) = 0
        [HideInInspector] _GradColor1("GradColor1", COLOR) = (1, 1, 1, 1)
        [HideInInspector] _GradColor2("GradColor2", COLOR) = (1, 1, 1, 1)
        [HideInInspector] _GradCenter("GradCenter", Float) = 0
        [HideInInspector] _GradSize("GradSize", Float) = 20
        [HideInInspector] _GradSmoothness("GradSmoothness", Float) = 0.5
        [HideInInspector] _GradBlend("GradBlend", Float) = 1

        // Layered textures (mantêm layout do CBUFFER partilhado)
        [HideInInspector] _SecondaryTex("SecondaryTex", 2D) = "white" { }
[HideInInspector] _MaskTex      ("MaskTex",      2D) = "white" { }
[HideInInspector] _LayerEnable  ("LayerEnable", Float) = 0
[HideInInspector] _LayerBlend("LayerBlend", Float) = 1
[HideInInspector] _MaskInvert("MaskInvert", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
        }

        // =================================================================
        //  PASS 1 — ForwardLit
        // =================================================================
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

            // ============================================================
            //  CLOTH SWAY — variáveis
            //  NOTA: se o teu STCore.hlsl usa CBUFFER_START(UnityPerMaterial),
            //  move estas declarações para lá para preservar o SRP Batcher.
            //  Se não usares SRP Batcher, funciona perfeitamente assim.
            // ============================================================
            float  _ClothEnable;
float _ClothTopY;
float _ClothBottomY;
float _ClothAmount;
float _ClothSpeed;
float _ClothFrequency;
float _ClothSecondary;
float4 _ClothWindDir;
float _ClothWindStrength;
float _ClothVerticalWobble;
float _ClothRetroQuantize;

// ============================================================
//  CLOTH SWAY — deslocamento de vértice (object space)
//
//  Modelo:
//    • Topo (Y alto) fica preso ao corpo        → peso 0
//    • Fundo (Y baixo) fica solto e ondula       → peso 1
//    • Duas ondas senoidais (primária + secundária)
//    • Viés direcional do vento (XZ)
//    • Pequena ondulação vertical opcional
//    • Quantização opcional (retro PS1)
//
//  Tudo em object space: a roupa segue a bruxa quando ela roda.
//  Custo: ~4 senos e 2 saturates por vértice. Irrelevante em lowpoly.
// ============================================================
float3 ApplyClothSway(float3 positionOS)
{
    if (_ClothEnable < 0.5) return positionOS;

    // 1) Peso vertical: 0 no topo (preso), 1 no fundo (solto).
    float range = max(_ClothTopY - _ClothBottomY, 1e-4);
    float weight = saturate((_ClothTopY - positionOS.y) / range);

    // 2) Fase base — desloca por tempo + posição local.
    //    Usar positionOS.y na fase faz o pano ondular em "degraus"
    //    ao longo do comprimento — chave para o look lowpoly.
    float t = _Time.y * _ClothSpeed;
    float phase = t + positionOS.y * _ClothFrequency;

    // 3) Onda primária (respiração base).
    float primary = sin(phase);

    // 4) Onda secundária — freq. desalinhada + offset na horizontal,
    //    quebra o padrão e evita o "robótico" das senoides simples.
    float secondary = sin(phase * 1.618 + positionOS.x * _ClothFrequency * 0.7);

    // 5) Mistura das duas ondas.
    float wave = primary + secondary * _ClothSecondary;

    // 6) Viés do vento — constante + pequena pulsação.
    float2 windDir = _ClothWindDir.xz;
    float windLen = length(windDir);
    if (windLen > 1e-4) windDir /= windLen;
    float gust = 0.85 + 0.15 * sin(t * 0.6);
    float2 windOffset = windDir * (_ClothWindStrength * gust);

    // 7) Offset final em XZ. O eixo X recebe o grosso da ondulação;
    //    Z apanha a secundária para dar profundidade.
    float2 offsetXZ;
    offsetXZ.x = wave * _ClothAmount + secondary * _ClothAmount * 0.25;
    offsetXZ.y = primary * _ClothAmount * 0.35;
    offsetXZ += windOffset * _ClothAmount * 0.75;

    // 8) Ondulação vertical (bainha a "respirar").
    float offsetY = secondary * _ClothAmount * _ClothVerticalWobble;

    // 9) Aplica com peso — quanto mais perto do fundo, mais mexe.
    positionOS.xz += offsetXZ * weight;
    positionOS.y += offsetY * weight;

    // 10) Quantização "retro" opcional (estilo PS1).
    //     _ClothRetroQuantize = 0 → desligado (fluido).
    //     _ClothRetroQuantize = 4 → pano aos saltos, muito retro.
    if (_ClothRetroQuantize > 0.5)
    {
        float steps = _ClothRetroQuantize;
        positionOS.xz = floor(positionOS.xz * steps + 0.5) / steps;
    }

    return positionOS;
}

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

v2f vert(appdata v)
{
    v2f o;

    // ⬇️ CLOTH SWAY — desloca o vértice antes de qualquer cálculo.
    v.vertex.xyz = ApplyClothSway(v.vertex.xyz);

    VertexPositionInputs vertexInput = GetVertexPositionInputs(v.vertex.xyz);
    VertexNormalInputs normalInput = GetVertexNormalInputs(v.normal);

    o.pos = vertexInput.positionCS;
    o.uv = TRANSFORM_TEX(v.uv, _MainTex);
    o.worldNormal = normalInput.normalWS;
    o.worldPos = vertexInput.positionWS;
    o.viewDir = GetWorldSpaceNormalizeViewDir(vertexInput.positionWS);

    return o;
}

float4 frag(v2f i) : SV_Target
            {
                float3 normal  = normalize(i.worldNormal);
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

float toon = Toon(NdotL, atten);

float4 texcol = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv) * _Color;

float3 ambientLight = SampleSH(normal) * _AmbientCol + (1.0 - _AmbientCol);

float3 litCol = texcol.rgb * (mainLight.color + ambientLight);
float3 shadowCol = texcol.rgb * ambientLight;
float3 finalRGB = lerp(shadowCol, litCol, toon);

// ---- Fake Emissive Light (consciente do dia/noite) ----
float shadowStep = ComputeToonShadowStep(mainLight, NdotL, atten);
float3 fakeLight = ComputeFakeEmissiveLight(i.worldPos, normal, shadowStep);
finalRGB += texcol.rgb * fakeLight;

float cloudShadow = SampleCloudShadow(i.worldPos);
finalRGB *= cloudShadow;

float4 blendCol = float4(finalRGB, 1.0);
float4 postCol = PostEffects(blendCol, toon, atten, NdotL, NdotH, VdotN, FdotV);

postCol.a = 1.0;
return postCol;
            }
            ENDHLSL
        }

        // =================================================================
        //  PASS 2 — ShadowCaster
        //  IMPORTANTE: aplica o MESMO deslocamento — senão a sombra fica
        //  "reta" enquanto a roupa ondula (salta à vista em toon shading).
        // =================================================================
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

            // As mesmas variáveis do Pass ForwardLit.
            // (Não podemos incluir STCore.hlsl aqui sem herdar funcionalidades
            //  não usadas neste pass — declaramos só o que precisamos.)
            float  _ClothEnable;
float _ClothTopY;
float _ClothBottomY;
float _ClothAmount;
float _ClothSpeed;
float _ClothFrequency;
float _ClothSecondary;
float4 _ClothWindDir;
float _ClothWindStrength;
float _ClothVerticalWobble;
float _ClothRetroQuantize;

// Cópia exacta da função (tem de ser idêntica à do ForwardLit
// para que a sombra corresponda ao deslocamento visível).
float3 ApplyClothSway(float3 positionOS)
{
    if (_ClothEnable < 0.5) return positionOS;

    float range = max(_ClothTopY - _ClothBottomY, 1e-4);
    float weight = saturate((_ClothTopY - positionOS.y) / range);

    float t = _Time.y * _ClothSpeed;
    float phase = t + positionOS.y * _ClothFrequency;

    float primary = sin(phase);
    float secondary = sin(phase * 1.618 + positionOS.x * _ClothFrequency * 0.7);

    float wave = primary + secondary * _ClothSecondary;

    float2 windDir = _ClothWindDir.xz;
    float windLen = length(windDir);
    if (windLen > 1e-4) windDir /= windLen;
    float gust = 0.85 + 0.15 * sin(t * 0.6);
    float2 windOffset = windDir * (_ClothWindStrength * gust);

    float2 offsetXZ;
    offsetXZ.x = wave * _ClothAmount + secondary * _ClothAmount * 0.25;
    offsetXZ.y = primary * _ClothAmount * 0.35;
    offsetXZ += windOffset * _ClothAmount * 0.75;

    float offsetY = secondary * _ClothAmount * _ClothVerticalWobble;

    positionOS.xz += offsetXZ * weight;
    positionOS.y += offsetY * weight;

    if (_ClothRetroQuantize > 0.5)
    {
        float steps = _ClothRetroQuantize;
        positionOS.xz = floor(positionOS.xz * steps + 0.5) / steps;
    }

    return positionOS;
}

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

    // ⬇️ Aplica o mesmo deslocamento — a sombra acompanha o pano.
    input.positionOS.xyz = ApplyClothSway(input.positionOS.xyz);

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