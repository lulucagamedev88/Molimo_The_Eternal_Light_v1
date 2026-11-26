#ifndef ST_CORE_INCLUDED
#define ST_CORE_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

// ===================== Textures =====================
TEXTURE2D(_MainTex);
SAMPLER(sampler_MainTex);

TEXTURE2D(_CloudShadowTex);
SAMPLER(sampler_CloudShadowTex);

TEXTURE2D(_EmissiveTex);
SAMPLER(sampler_EmissiveTex);

// ===== Layered Textures (Terrain) =====
TEXTURE2D(_SecondaryTex);
SAMPLER(sampler_SecondaryTex);

TEXTURE2D(_MaskTex);
SAMPLER(sampler_MaskTex);

// ===================== Fake Emissive Lights =====================
#define MAX_FAKE_LIGHTS 20

float4 _FakeLightPosArray  [MAX_FAKE_LIGHTS];   // xyz = pos, w = 1 (ativo)
float4 _FakeLightColorArray[MAX_FAKE_LIGHTS];   // rgb = cor * intensidade
float4 _FakeLightDataArray [MAX_FAKE_LIGHTS];   // x = radius, y = softness

CBUFFER_START(UnityPerMaterial)
    float4 _MainTex_ST;

    // ===== Emissive =====
    float  _EmissiveEnable;
    float4 _EmissiveColor;
    float  _EmissiveIntensity;

    // ===== Layered Textures (Terrain) =====
    float4 _SecondaryTex_ST;
    float4 _MaskTex_ST;
    float  _LayerEnable;
    float  _LayerBlend;
    float  _MaskInvert;

    float4 _Color;
    float _ColIntense;
    float _ColBright;
    float _AmbientCol;

    float _Segmented;
    float _Steps;
    float _StpSmooth;
    float _Offset;

    float _Clipped;
    float _MinLight;
    float _MaxLight;
    float _Lumin;

    float4 _OtlColor;
    float _OtlWidth;

    float4 _ShnColor;
    float _ShnOverlap;
    float _ShnIntense;
    float _ShnRange;
    float _ShnSmooth;

    // ===== Anti-Tile =====
    float _ATEnable;
    float _ATScale;
    float _ATRandomize;
    float _ATRotation;
    float _ATSmoothness;

    // ===== World Gradient =====
    float  _GradEnable;
    float  _GradAxis;
    float4 _GradColor1;
    float4 _GradColor2;
    float  _GradCenter;
    float  _GradSize;
    float  _GradSmoothness;
    float  _GradBlendMode;
    float  _GradBlend;

    // ===== Cloud Shadow =====
    float _CloudShadowEnable;
    float _CloudShadowStrength;
    float _CloudShadowWorldSize;
    float _CloudShadowCenterX;
    float _CloudShadowCenterZ;
    float _CloudShadowInvert;
    float _CloudShadowTiling;

    // ===== Fake Emissive Light (globais) =====
    float _FakeLightEnable;
    float _FakeLightOnlyShadow;
    float _FakeLightNdotL;
    float _FakeLightNightThreshold;
CBUFFER_END

// =====================================================
// Toon
// =====================================================
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

float4 ColorBlend(float4 col1, float4 col2, float toon)
{
    return lerp(col1, col2, toon);
}

float4 PostEffects(float4 baseCol, float toon, float atten, float NdotL, float NdotH, float VdotN, float FdotV)
{
    float shine = pow(max(0.0, NdotH), max(0.001, 1.0 - _ShnRange));
    shine = smoothstep(1.0 - _ShnRange - _ShnSmooth, 1.0 - _ShnRange, shine);
    shine *= _ShnIntense;

    if (_ShnOverlap < 0.5)
        shine *= step(0.5, toon);

    float4 finalCol = baseCol + (_ShnColor * shine);
    finalCol.rgb += _Lumin;
    return finalCol;
}

// =====================================================
// Anti-Tile
// =====================================================
float2 ST_Hash22(float2 p)
{
    float3 p3 = frac(float3(p.x, p.y, p.x) * float3(0.1031, 0.1030, 0.0973));
    p3 = p3 + dot(p3, float3(p3.y, p3.z, p3.x) + 33.33);
    return frac((float2(p3.x, p3.x) + float2(p3.y, p3.z)) * float2(p3.z, p3.y));
}

float2 ST_Rotate2D(float2 p, float a)
{
    float s = sin(a);
    float c = cos(a);
    return float2(c * p.x - s * p.y,
                  s * p.x + c * p.y);
}

float4 SampleAntiTile(float2 uv, float scale, float randomize, float rotation, float smoothness)
{
    float s = max(scale, 1.0);
    float2 scaledUV = uv * s;
    float2 cell     = floor(scaledUV);
    float2 local    = scaledUV - cell;

    float2 rnd    = ST_Hash22(cell);
    float  angle  = (rnd.x - 0.5) * rotation * 6.2831853;
    float2 offset = (rnd - 0.5) * randomize;

    float2 centered = local - 0.5;
    centered = ST_Rotate2D(centered, angle) + 0.5 + offset;

    float4 colA = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, cell + centered);
    float4 colB = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, scaledUV);

    float dist   = length(local - 0.5);
    float radius = 0.5;
    float soft   = max(smoothness, 0.001) * radius;
    float inner  = radius - soft;
    float outer  = radius;

    float mask = 1.0 - smoothstep(inner, outer, dist);
    return lerp(colB, colA, mask);
}

// =====================================================
// World Gradient
// =====================================================
float3 SampleWorldGradient(float3 worldPos)
{
    float coord = lerp(worldPos.x, worldPos.y, saturate(_GradAxis));

    float halfSize = max(_GradSize * 0.5, 0.0001);
    float linearT  = saturate((coord - (_GradCenter - halfSize)) / max(_GradSize, 0.0001));

    float stepT = step(0.5, linearT);
    float t     = lerp(stepT, linearT, saturate(_GradSmoothness));

    return lerp(_GradColor2.rgb, _GradColor1.rgb, t);
}

float3 ApplyGradBlend(float3 base, float3 blend, float mode)
{
    float3 m_mul = base * blend;
    float3 m_scr = 1.0 - (1.0 - base) * (1.0 - blend);
    float3 m_ovl = lerp(2.0 * base * blend,
                        1.0 - 2.0 * (1.0 - base) * (1.0 - blend),
                        step(0.5, base));

    float3 b = base;
    float3 d = lerp(sqrt(max(b, 0.0001)),
                    ((16.0 * b - 12.0) * b + 4.0) * b,
                    step(0.25, b));
    float3 m_sfl = lerp(b - (1.0 - 2.0 * blend) * b * (1.0 - b),
                        b + (2.0 * blend - 1.0) * (d - b),
                        step(0.5, blend));

    float  lumBase = dot(base,  float3(0.2126, 0.7152, 0.0722));
    float  lumTint = max(dot(blend, float3(0.2126, 0.7152, 0.0722)), 0.0001);
    float3 m_col   = blend * (lumBase / lumTint);

    float3 m_mix = blend;

    float f0 = step(abs(mode - 0.0), 0.5);
    float f1 = step(abs(mode - 1.0), 0.5);
    float f2 = step(abs(mode - 2.0), 0.5);
    float f3 = step(abs(mode - 3.0), 0.5);
    float f4 = step(abs(mode - 4.0), 0.5);
    float f5 = step(abs(mode - 5.0), 0.5);

    float  sumF   = f0 + f1 + f2 + f3 + f4 + f5;
    float3 result = m_mul * f0 + m_scr * f1 + m_ovl * f2 +
                    m_sfl * f3 + m_col * f4 + m_mix * f5;

    return lerp(m_mul, result, step(0.5, sumF));
}

float3 ApplyWorldGradient(float3 baseRGB, float3 worldPos)
{
    float3 gradCol = SampleWorldGradient(worldPos);
    float3 blended = ApplyGradBlend(baseRGB, gradCol, _GradBlendMode);
    float  amt     = saturate(_GradEnable) * saturate(_GradBlend);
    return lerp(baseRGB, blended, amt);
}

// =====================================================
// Cloud Shadow
// =====================================================
float SampleCloudShadow(float3 worldPos)
{
    float2 center   = float2(_CloudShadowCenterX, _CloudShadowCenterZ);
    float  size     = max(_CloudShadowWorldSize, 0.0001);
    float  tiling   = max(_CloudShadowTiling, 0.0001);

    float2 uv = ((worldPos.xz - center) / size) * tiling + 0.5;
    float2 uvClamped = saturate(uv);

    float inside = step(0.0, uv.x) * step(uv.x, 1.0) *
                   step(0.0, uv.y) * step(uv.y, 1.0);

    float cloud = SAMPLE_TEXTURE2D(_CloudShadowTex, sampler_CloudShadowTex, uvClamped).r;

    cloud = lerp(cloud, 1.0 - cloud, saturate(_CloudShadowInvert));

    float amt = saturate(_CloudShadowEnable) * saturate(_CloudShadowStrength) * inside;
    return lerp(1.0, cloud, amt);
}

// =====================================================
// Layered Textures (Primary + Secondary + Mask) - Terrain
// =====================================================
float4 SampleLayeredTexture(float2 uvMain, float2 uvObj)
{
    // Primary (mantém AntiTile opcional)
    float4 texcolDirect = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uvMain);
    float4 texcolAT     = SampleAntiTile(uvMain, _ATScale, _ATRandomize, _ATRotation, _ATSmoothness);
    float4 texPrimary   = lerp(texcolDirect, texcolAT, saturate(_ATEnable));

    // Secondary (obedece UV do objeto)
    float2 secUV        = TRANSFORM_TEX(uvObj, _SecondaryTex);
    float4 texSecondary = SAMPLE_TEXTURE2D(_SecondaryTex, sampler_SecondaryTex, secUV);

    // Mask (obedece 100% à UV do objeto)
    float2 maskUV       = TRANSFORM_TEX(uvObj, _MaskTex);
    float  maskVal      = SAMPLE_TEXTURE2D(_MaskTex, sampler_MaskTex, maskUV).r;
    maskVal             = lerp(maskVal, 1.0 - maskVal, saturate(_MaskInvert));

    // Blend: branco = Secondary, preto = Primary
    float  blendAmt     = saturate(_LayerEnable) * saturate(_LayerBlend);
    return lerp(texPrimary, texSecondary, maskVal * blendAmt);
}

// =====================================================
// Additional Lights (URP nativo)
// =====================================================
float ComputeAdditionalLightToon(float3 normal, float3 worldPos)
{
    float result = 0.0;

#ifdef _ADDITIONAL_LIGHTS
    uint count = GetAdditionalLightsCount();

    for (uint i = 0u; i < count; ++i)
    {
        Light light = GetAdditionalLight(i, worldPos);
        float3 L       = normalize(light.direction);
        float  NdotL   = dot(normal, L);
        float  atten   = light.distanceAttenuation * light.shadowAttenuation;
        float toon = step(0.01, NdotL * atten + _Offset);
        toon *= smoothstep(0.0, 0.15, atten);
        result = max(result, toon);
    }
#endif

    return result;
}

// =====================================================
// Fake Emissive Light - MÚLTIPLAS PointLights
// =====================================================
float3 ComputeFakeEmissiveLight(float3 worldPos, float3 worldNormal, float shadowStep)
{
    if (_FakeLightEnable < 0.5)
        return float3(0, 0, 0);

    float shadowMask = lerp(1.0, 1.0 - shadowStep, saturate(_FakeLightOnlyShadow));
    float3 total     = float3(0, 0, 0);
    float  ndlAmt    = saturate(_FakeLightNdotL);

    [unroll]
    for (int i = 0; i < MAX_FAKE_LIGHTS; i++)
    {
        float4 posData = _FakeLightPosArray[i];
        if (posData.w < 0.5) continue;

        float4 colData  = _FakeLightColorArray[i];
        float4 dataData = _FakeLightDataArray[i];

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

// =====================================================
// Helper: calcula o shadowStep "consciente do dia/noite"
// =====================================================
float ComputeToonShadowStep(Light mainLight, float NdotL, float atten)
{
    float mainLum = max(max(mainLight.color.r, mainLight.color.g), mainLight.color.b);
    float dayFactor = smoothstep(0.0, max(_FakeLightNightThreshold, 0.001), mainLum);
    float baseStep = step(0.01, NdotL * atten + _Offset);
    return baseStep * dayFactor;
}

#endif