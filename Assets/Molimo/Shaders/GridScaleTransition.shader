Shader "Custom/UI/GridScaleTransition"
{
    // =========================================================================
    //  Grid Scale Transition — quadrados arredondados que crescem.
    //
    //  Características:
    //    - Cantos arredondados via SDF de rounded box.
    //    - Gap configurável entre células (linhas de grelha).
    //    - Bordas suaves (softness configurável em pixels).
    //    - Varredura direcional angular.
    //    - _Progress ACEITA OVERSHOOT (> 1) para eliminar costuras.
    //    - Compatível com UI Image / Canvas.
    //
    //  NOTA SOBRE _Progress:
    //    O valor pode ir até 1.5 (Range no Inspector) ou além (se usar Float).
    //    Valores > 1 expandem as células para lá das fronteiras, garantindo
    //    cobertura total SEM linhas de costura entre células adjacentes.
    // =========================================================================

    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}

        [Header(Transition)]
        _Progress      ("Progress (0-1.5)", Range(0, 1.5)) = 0.0
        _Angle         ("Angle (degrees)", Range(0, 360))  = 0.0
        _SweepSoftness ("Sweep Softness", Range(0.02, 1))  = 0.15

        [Header(Grid)]
        _GridSize      ("Grid Size", Float)                    = 20.0
        _AspectRatio   ("Aspect Ratio", Float)                 = 1.777
        _CellGap       ("Cell Gap", Range(0, 0.3))             = 0.03
        _CornerRadius  ("Corner Radius", Range(0, 1))          = 0.35
        _EdgeSoftness  ("Edge Softness (px)", Range(0.5, 6))   = 2.0

        [Header(Appearance)]
        _Color         ("Fill Color", Color)                   = (0.85, 0.42, 0.30, 0.92)

        [Header(UI Stencil)]
        _StencilComp      ("Stencil Comparison", Float) = 8
        _Stencil          ("Stencil ID", Float) = 0
        _StencilOp        ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask  ("Stencil Read Mask", Float) = 255
        _ColorMask        ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue"             = "Transparent"
            "IgnoreProjector"   = "True"
            "RenderType"        = "Transparent"
            "PreviewType"       = "Plane"
            "CanUseSpriteAtlas" = "True"
            "RenderPipeline"    = "UniversalPipeline"
        }

        Stencil
        {
            Ref       [_Stencil]
            Comp      [_StencilComp]
            Pass      [_StencilOp]
            ReadMask  [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull      Off
        Lighting  Off
        ZWrite    Off
        ZTest     Always
        Blend     SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "GridScaleTransitionUI"

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #pragma target   3.0

            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            // ---------- Texturas / Samplers ----------
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            // ---------- Uniforms por material ----------
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float  _Progress;
                float  _Angle;
                float  _SweepSoftness;
                float  _GridSize;
                float  _AspectRatio;
                float  _CellGap;
                float  _CornerRadius;
                float  _EdgeSoftness;
                half4  _Color;
            CBUFFER_END

            // ---------- Uniforms globais da UI ----------
            float4 _ClipRect;

            // ---------- Estruturas ----------
            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color      : COLOR;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 color      : COLOR;
                float2 uv         : TEXCOORD0;
                float4 screenPos  : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            // -----------------------------------------------------------------
            //  Vertex
            // -----------------------------------------------------------------
            Varyings vert (Attributes IN)
            {
                Varyings OUT = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                VertexPositionInputs vpi = GetVertexPositionInputs(IN.positionOS.xyz);

                OUT.positionCS = vpi.positionCS;
                OUT.screenPos  = ComputeScreenPos(OUT.positionCS);
                OUT.uv         = TRANSFORM_TEX(IN.uv, _MainTex);
                OUT.color      = IN.color;
                return OUT;
            }

            // -----------------------------------------------------------------
            //  SDF de caixa arredondada centrada na origem.
            //    p      = ponto em espaço local (centro = 0)
            //    halfSz = meia-largura da caixa
            //    r      = raio do canto (0 = canto afiado)
            //  Devolve < 0 dentro, 0 na fronteira, > 0 fora.
            // -----------------------------------------------------------------
            float sdRoundedBox(float2 p, float2 halfSz, float r)
            {
                // halfSz pode exceder a célula (overshoot) — o SDF lida bem
                // com isso porque é apenas geometria.
                r = max(r, 0.0);
                float2 q = abs(p) - halfSz + r;
                return min(max(q.x, q.y), 0.0) + length(max(q, 0.0)) - r;
            }

            // -----------------------------------------------------------------
            //  Fragment
            // -----------------------------------------------------------------
            half4 frag (Varyings IN) : SV_Target
            {
                // =============================================================
                // 1) Sprite base (compatibilidade UI Image)
                // =============================================================
                half4 texColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv);

                // =============================================================
                // 2) UV de ecrã normalizado [0..1]
                // =============================================================
                float2 screenUV = IN.screenPos.xy / max(IN.screenPos.w, 1e-5);

                // =============================================================
                // 3) Varredura direcional (aspect-corrigida)
                // =============================================================
                float  angleRad = radians(_Angle);
                float2 sweepDir = float2(cos(angleRad), sin(angleRad));

                float2 centered   = (screenUV - 0.5) * float2(_AspectRatio, 1.0);
                float  sweepCoord = dot(centered, sweepDir);

                // Range 2.2 cobre toda a diagonal máxima com margem.
                float  threshold = (_Progress - 0.5) * 2.2;
                float  band      = max(_SweepSoftness, 1e-3);

                // localCover ∈ [0, +∞) — SEM saturate.
                // Valores > 1 empurram as células para além das fronteiras,
                // garantindo cobertura total (elimina costuras) quando o
                // _Progress faz overshoot acima de 1.0.
                float localCover = (threshold - sweepCoord) / band + 0.5;

                // =============================================================
                // 4) Coordenadas de célula
                // =============================================================
                float2 gridUV = screenUV;
                gridUV.x *= _AspectRatio;          // células quadradas em ecrã
                float2 scaled = gridUV * _GridSize;

                // cellUV ∈ [-1, +1] com centro em 0
                float2 cellUV = frac(scaled) * 2.0 - 1.0;

                // 1 pixel em unidades de cellUV
                float pixelSize     = max(fwidth(scaled.x), fwidth(scaled.y));
                float pixelInCellUV = pixelSize * 2.0 + 1e-6;

                // =============================================================
                // 5) Meia-largura e raio (SEM clamp em halfSize)
                // =============================================================
                float maxHalf = 1.0 - _CellGap;

                // Pode ultrapassar maxHalf quando localCover > 1 (overshoot).
                // É exatamente isso que fecha a costura entre células.
                float halfSize = localCover * maxHalf;

                // Raio proporcional ao tamanho, limitado pela meia-largura.
                // max(halfSize, 0) evita NaN se localCover for negativo
                // (acontece no início do fade-out quando há overshoot inicial).
                float radius = min(_CornerRadius * maxHalf, max(halfSize, 0.0));

                // =============================================================
                // 6) SDF do quadrado arredondado
                // =============================================================
                float sd      = sdRoundedBox(cellUV, halfSize.xx, radius);
                float pixelSD = sd / pixelInCellUV;

                // =============================================================
                // 7) Alpha via softness (o saturate final controla tudo)
                //    pixelSD < 0 dentro | = 0 na aresta | > 0 fora
                // =============================================================
                float alpha = saturate(0.5 - pixelSD / max(_EdgeSoftness, 0.01));

                // =============================================================
                // 8) Composição final
                // =============================================================
                half4 outColor;
                outColor.rgb = _Color.rgb;
                outColor.a   = _Color.a * alpha * texColor.a * IN.color.a;

                // =============================================================
                // 9) RectMask2D
                // =============================================================
                #ifdef UNITY_UI_CLIP_RECT
                float2 clipUV = saturate(
                    (_ClipRect.zw - _ClipRect.xy - abs(IN.positionCS.xy - (_ClipRect.zw + _ClipRect.xy) * 0.5))
                    / max(_ClipRect.zw - _ClipRect.xy, 1e-5) + 0.5);
                outColor.a *= clipUV.x * clipUV.y;
                #endif

                // =============================================================
                // 10) Alpha clip
                // =============================================================
                #ifdef UNITY_UI_ALPHACLIP
                clip(outColor.a - 0.001);
                #endif

                return outColor;
            }
            ENDHLSL
        }
    }

    Fallback "UI/Default"
}