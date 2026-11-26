Shader "Simple Toon/URP/STFoliage"
{
    Properties
    {
        [Header(Texture)][Space(5)]
_MainTex ("Foliage Texture (RGBA)", 2D) = "white" { }
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
        _AmbientCol("Ambient", Range(0, 1)) = 0

        [Header(Toon)][Space(5)]
        [Toggle] _Segmented("Segmented", Float) = 1
        _Steps("Steps", Range(1, 25)) = 3
        _Offset("Lit Offset", Range(-1, 1.1)) = 0

        [Header(Light)][Space(5)]
        [Toggle] _Clipped("Clipped", Float) = 0
        _MinLight("Min Light", Range(0, 1)) = 0.2
        _MaxLight("Max Light", Range(0, 1)) = 1
        _Lumin("Luminocity", Range(0, 2)) = 0

        [Header(Wind)][Space(5)]
        [Toggle] _WindEnable("Enable Wind", Float) = 1
        _WindTrunkAmplitude("Trunk Amplitude (vertex B)", Range(0, 1)) = 0.06
        _WindTrunkSpeed("Trunk Speed", Range(0, 1)) = 0.3
        _WindFoliageAmplitude("Foliage Amplitude (vertex R)", Range(0, 1)) = 0.05
        _WindFoliageSpeed("Foliage Speed", Range(0, 1)) = 0.3
        _WindNoiseScale("Noise Scale", Range(0.1, 10)) = 2.0
        _WindDirection("Wind Direction (XYZ)", Vector) = (1, 0, 0.5, 0)

        [Header(Fake Emissive Light)][Space(5)]
        [Toggle] _FakeLightEnable("Enable Fake Light", Float) = 0
        [Toggle] _FakeLightOnlyShadow("Only Shadow Area", Float) = 1
        _FakeLightNdotL("Normal Influence", Range(0, 1)) = 0.5
        _FakeLightNightThreshold("Night Threshold", Range(0, 1)) = 0.15
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

        Cull Off
        ZWrite On
        ZTest LEqual

        // ==================================================================
        // Bloco compartilhado: includes, noise, wind, CBUFFER
        // ==================================================================
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

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

float _WindEnable;
float _WindTrunkAmplitude;
float _WindTrunkSpeed;
float _WindFoliageAmplitude;
float _WindFoliageSpeed;
float _WindNoiseScale;
float4 _WindDirection;

float _FakeLightEnable;
float _FakeLightOnlyShadow;
float _FakeLightNdotL;
float _FakeLightNightThreshold;
CBUFFER_END

// ---------------- Simplex Noise 2D ----------------
float3 mod2D289(float3 x) { return x - floor(x * (1.0 / 289.0)) * 289.0; }
        float2 mod2D289(float2 x) { return x - floor(x * (1.0 / 289.0)) * 289.0; }
float3 permute(float3 x) { return mod2D289(((x * 34.0) + 1.0) * x); }

float snoise(float2 v)
{
    const float4 C = float4(0.211324865405187, 0.366025403784439,
                           -0.577350269189626, 0.024390243902439);
    float2 i = floor(v + dot(v, C.yy));
    float2 x0 = v - i + dot(i, C.xx);
    float2 i1 = (x0.x > x0.y) ? float2(1.0, 0.0) : float2(0.0, 1.0);

    float4 x12 = x0.xyxy + C.xxzz;
    x12.xy -= i1;
    i = mod2D289(i);

    float3 p = permute(permute(i.y + float3(0.0, i1.y, 1.0))
                     + i.x + float3(0.0, i1.x, 1.0));

    float3 m = max(0.5 - float3(dot(x0, x0),
                                dot(x12.xy, x12.xy),
                                dot(x12.zw, x12.zw)), 0.0);
    m = m * m;
    m = m * m;

    float3 x = 2.0 * frac(p * C.www) - 1.0;
    float3 h = abs(x) - 0.5;
    float3 ox = floor(x + 0.5);
    float3 a0 = x - ox;

    m *= 1.79284291400159 - 0.85373472095314 * (a0 * a0 + h * h);

    float3 g;
    g.x = a0.x * x0.x + h.x * x0.y;
    g.yz = a0.yz * x12.xz + h.yz * x12.yw;
    return 130.0 * dot(m, g);
}

// ---------------- Wind Application ----------------
// vertexColor.r -> folhas (flutter via simplex)
// vertexColor.b -> tronco/galhos (sway senoidal na direção do vento)
float3 ApplyFoliageWind(float3 positionOS, float3 normalOS, float4 vertexColor)
{
    if (_WindEnable < 0.5) return positionOS;

    float3 windDir = normalize(_WindDirection.xyz + 1e-5);

    // --- Tronco / galhos (canal B) ---
    float trunkTime = _TimeParameters.x * (2.0 * _WindTrunkSpeed);
    float3 trunkOffset = windDir * sin(trunkTime)
                       * _WindTrunkAmplitude * vertexColor.b;
    trunkOffset.y = 0.0;

    // --- Folhas (canal R + simplex noise) ---
    float3 worldPos = TransformObjectToWorld(positionOS);
    float2 panner = (_TimeParameters.x * _WindFoliageSpeed) * float2(2.0, 2.0)
                    + worldPos.xz * _WindNoiseScale;
    float noiseVal = snoise(panner);
    float3 foliageOffset = normalOS * noiseVal
                         * _WindFoliageAmplitude * vertexColor.r;

    return positionOS + trunkOffset + foliageOffset;
}
ENDHLSL

// ==================================================================
// Pass 1: ForwardLit (Toon principal)
// ==================================================================
Pass
{
    Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

    Cull Off
    ZWrite On
    ZTest LEqual

    HLSLPROGRAM
#pragma vertex vert
#pragma fragment frag
#pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
#pragma multi_compile _ _ADDITIONAL_LIGHTS
#pragma multi_compile_fragment _ _SHADOWS_SOFT
#pragma multi_compile_instancing

# include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

#define MAX_FAKE_LIGHTS 20

    float4 _FakeLightPosArray [MAX_FAKE_LIGHTS];
    float4 _FakeLightColorArray [MAX_FAKE_LIGHTS];
    float4 _FakeLightDataArray [MAX_FAKE_LIGHTS];

            // ---- Toon ----
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
        float3 total = float3(0, 0, 0);
        float ndlAmt = saturate(_FakeLightNdotL);

        [unroll]
        for (int li = 0; li < MAX_FAKE_LIGHTS; li++)
        {
            float4 posData = _FakeLightPosArray[li];
            if (posData.w < 0.5) continue;

            float4 colData = _FakeLightColorArray[li];
            float4 dataData = _FakeLightDataArray[li];

            float3 emitterPos = posData.xyz;
            float3 delta = emitterPos - worldPos;
            float dist = length(delta);
            float3 L = delta / max(dist, 1e-4);

            float radius = max(dataData.x, 0.0001);
            float soft = max(dataData.y, 0.001);
            float inner = radius * (1.0 - soft);
            float outer = radius;
            float radial = 1.0 - smoothstep(inner, outer, dist);

            float NdotL = saturate(dot(worldNormal, L));
            NdotL = lerp(1.0, NdotL, ndlAmt);

            total += colData.rgb * (NdotL * radial * shadowMask);
        }
        return total;
    }

            struct Attributes
{
    float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 color      : COLOR;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings
{
    float4 positionCS  : SV_POSITION;
                float2 uv          : TEXCOORD0;
                float3 worldNormal : TEXCOORD1;
                float3 worldPos    : TEXCOORD2;
                float gradU       : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
};

Varyings vert(Attributes input)
{
    Varyings output = (Varyings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);

    float3 finalPosOS = ApplyFoliageWind(input.positionOS.xyz, input.normalOS, input.color);

    VertexPositionInputs vpi = GetVertexPositionInputs(finalPosOS);
    VertexNormalInputs vni = GetVertexNormalInputs(input.normalOS);

    output.positionCS = vpi.positionCS;
    output.uv = TRANSFORM_TEX(input.uv, _MainTex);
    output.worldNormal = vni.normalWS;
    output.worldPos = vpi.positionWS;

    float worldYAdd = vpi.positionWS.y * _HeightVariation * 0.05;
    output.gradU = saturate(input.uv.y + worldYAdd);

    return output;
}

float4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);

float4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);

if (_AlphaClip > 0.5)
    clip(tex.a - _Cutoff);

float3 texRGB = tex.rgb * _Color.rgb;

// ---- Gradiente topo/base ----
float band = max(_GradSmoothness, 0.0001) * 0.5;
float inner = _GradBias - band;
float outer = _GradBias + band;
float gv = smoothstep(inner, outer, i.gradU);
float3 gradCol = lerp(_BottomColor.rgb, _TopColor.rgb, gv);

float3 finalAlbedo = saturate(texRGB * gradCol * 2.0);

// ---- Iluminação ----
float3 normal = normalize(i.worldNormal);
float4 shadowCoord = TransformWorldToShadowCoord(i.worldPos);
Light mainLight = GetMainLight(shadowCoord);

float3 lightDir = normalize(mainLight.direction);
float atten = mainLight.distanceAttenuation * mainLight.shadowAttenuation;

float NdotL = dot(normal, lightDir);
float toon = Toon(NdotL, atten);

float3 ambientLight = SampleSH(normal) * _AmbientCol + (1.0 - _AmbientCol);

float3 litCol = finalAlbedo * (mainLight.color + ambientLight);
float3 shadowCol = finalAlbedo * ambientLight;
float3 finalRGB = lerp(shadowCol, litCol, toon);

// ---- Fake Emissive Light (foliage: blend normal com UP) ----
float3 fakeNormal = normalize(lerp(normal, float3(0, 1, 0), 0.7));
float shadowStep = ComputeToonShadowStep(mainLight, NdotL, atten);
float3 fakeLight = ComputeFakeEmissiveLight(i.worldPos, fakeNormal, shadowStep);
finalRGB += finalAlbedo * fakeLight;

finalRGB += _Lumin;

return float4(finalRGB, 1.0);
            }
            ENDHLSL
        }

        // ==================================================================
        // Pass 2: ShadowCaster (com vento, para a sombra acompanhar)
        // ==================================================================
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
#pragma fragment frag
#pragma multi_compile_instancing

# include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            struct Attributes
{
    float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 color      : COLOR;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings
{
    float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
};

float3 _LightDirection;

Varyings vert(Attributes input)
{
    Varyings output = (Varyings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);

    float3 finalPosOS = ApplyFoliageWind(input.positionOS.xyz, input.normalOS, input.color);
    float3 positionWS = TransformObjectToWorld(finalPosOS);
    float3 normalWS = TransformObjectToWorldDir(input.normalOS);

    output.positionCS = TransformWorldToHClip(
        ApplyShadowBias(positionWS, normalWS, _LightDirection));

#if UNITY_REVERSED_Z
                    output.positionCS.z = min(output.positionCS.z,
                                              output.positionCS.w * UNITY_NEAR_CLIP_VALUE);
#else
    output.positionCS.z = max(output.positionCS.z,
                              output.positionCS.w * UNITY_NEAR_CLIP_VALUE);
#endif

    output.uv = TRANSFORM_TEX(input.uv, _MainTex);
    return output;
}

half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

if (_AlphaClip > 0.5)
{
    float a = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv).a * _Color.a;
    clip(a - _Cutoff);
}
return 0;
            }
            ENDHLSL
        }

        // ==================================================================
        // Pass 3: DepthOnly (necessário para foliage com cutout)
        // ==================================================================
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask 0
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
#pragma fragment frag
#pragma multi_compile_instancing

            struct Attributes
{
    float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 color      : COLOR;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings
{
    float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
};

Varyings vert(Attributes input)
{
    Varyings output = (Varyings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);

    float3 finalPosOS = ApplyFoliageWind(input.positionOS.xyz, input.normalOS, input.color);

    output.positionCS = TransformObjectToHClip(finalPosOS);
    output.uv = TRANSFORM_TEX(input.uv, _MainTex);

    return output;
}

half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

if (_AlphaClip > 0.5)
{
    float a = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv).a * _Color.a;
    clip(a - _Cutoff);
}
return 0;
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}