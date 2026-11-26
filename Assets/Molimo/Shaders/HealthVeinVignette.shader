Shader "Molimo/UI/HealthVeinVignette"
{
    Properties
    {
        [PerRendererData] _MainTex ("Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        [Header(Vignette Hole)]
        _HoleRadius   ("Hole Radius - 0 closed, 1 open", Range(0, 1))   = 1
        _Feather      ("Feather", Range(0, 0.5))                        = 0.08
        _HoleAspect   ("Hole Aspect", Float)                            = 1.0

        [Header(Pulse)]
        _PulseIntensity ("Pulse Intensity", Range(0, 2))                = 1

        // Propriedades internas exigidas pelo sistema de UI do Unity.
        [HideInInspector] _StencilComp   ("Stencil Comparison", Float) = 8
        [HideInInspector] _Stencil       ("Stencil ID",         Float) = 0
        [HideInInspector] _StencilOp     ("Stencil Operation",  Float) = 0
        [HideInInspector] _StencilWriteMask ("Stencil Write Mask", Float) = 255
        [HideInInspector] _StencilReadMask  ("Stencil Read Mask",  Float) = 255
        [HideInInspector] _ColorMask     ("Color Mask",         Float) = 15
    }

    SubShader
    {
        Tags
        {
            "Queue"             = "Transparent"
            "RenderType"        = "Transparent"
            "IgnoreProjector"   = "True"
            "PreviewType"       = "Plane"
            "CanUseSpriteAtlas" = "True"
            "RenderPipeline"    = "UniversalPipeline" // URP
        }

        Stencil
        {
            Ref   [_Stencil]
            Comp  [_StencilComp]
            Pass  [_StencilOp]
            ReadMask  [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "HealthVeinVignette"

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
                float4 color       : COLOR;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            // TODAS as propriedades dinâmicas devem estar neste bloco (Regra do URP)
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float  _HoleRadius;
                float  _Feather;
                float  _HoleAspect;
                float  _PulseIntensity;
                
                float  _StencilComp;
                float  _Stencil;
                float  _StencilOp;
                float  _StencilWriteMask;
                float  _StencilReadMask;
                float  _ColorMask;
            CBUFFER_END

            // O _Color tem que ficar FORA do UnityPerMaterial na UI
            float4 _Color;

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionHCS = TransformObjectToHClip(v.positionOS.xyz);
                
                // CORREÇÃO AQUI: TRANSFORM_TEX em vez de TRANSFORM_UV
                o.uv          = TRANSFORM_TEX(v.uv, _MainTex); 
                
                o.color       = v.color * _Color;
                return o;
            }

            float4 frag(Varyings i) : SV_Target
            {
                float4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv) * i.color;

                float2 c = i.uv - 0.5;
                c.x *= _HoleAspect;
                float d = length(c);

                float r    = _HoleRadius * 0.5;
                float mask = smoothstep(r - _Feather, r + _Feather, d);

                float  alpha = tex.a * mask * _PulseIntensity;
                float3 rgb   = tex.rgb * (1.0 + (_PulseIntensity - 1.0) * 0.4);

                return float4(rgb, saturate(alpha));
            }
            ENDHLSL
        }
    }
    Fallback "UI/Default"
}