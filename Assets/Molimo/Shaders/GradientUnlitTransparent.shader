Shader "Custom/GradientUnlitTransparent"
{
    Properties
    {
        [Header(Gradient Colors)]
        _ColorA ("Color A (Start)", Color) = (1, 0, 0, 1)
        _ColorB ("Color B (End)", Color)   = (0, 0, 1, 0)

        [Header(Gradient Settings)]
        _Direction ("Gradient Direction (0 = Vertical, 1 = Horizontal)", Range(0, 1)) = 0
        _Offset ("Split Line Offset (-1 to 1)", Range(-1, 1)) = 0
        _Sharpness ("Sharpness (0 = Soft, 1 = Hard Line)", Range(0.001, 1)) = 0.5
        _Intensity ("Intensity Multiplier", Float) = 1.0
    }

    SubShader
    {
        Tags
        {
            "RenderType"       = "Transparent"
            "Queue"            = "Transparent"
            "RenderPipeline"   = "UniversalPipeline"
            "IgnoreProjector"  = "True"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        Lighting Off

        Pass
        {
            Name "UnlitGradient"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            // ----------------------------------------------------------------
            // Properties (SRP Batcher compatible)
            // ----------------------------------------------------------------
            CBUFFER_START(UnityPerMaterial)
                float4 _ColorA;
                float4 _ColorB;
                float  _Direction;
                float  _Offset;
                float  _Sharpness;
                float  _Intensity;
            CBUFFER_END

            // ----------------------------------------------------------------
            // Vertex Input / Output
            // ----------------------------------------------------------------
            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            // ----------------------------------------------------------------
            // Vertex Shader
            // ----------------------------------------------------------------
            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                VertexPositionInputs posInputs = GetVertexPositionInputs(IN.positionOS.xyz);
                OUT.positionHCS = posInputs.positionCS;
                OUT.uv          = IN.uv;
                return OUT;
            }

            // ----------------------------------------------------------------
            // Fragment Shader
            // ----------------------------------------------------------------
            half4 frag (Varyings IN) : SV_Target
            {
                // Eixo do gradiente: 0 = vertical (uv.y), 1 = horizontal (uv.x)
                half coord = lerp(IN.uv.y, IN.uv.x, _Direction);

                // Normaliza o offset para deslocar a linha divisória.
                // Mapeia [-1..1] -> [1..0] para manter _Offset = 0 como centro.
                half centered = coord - (0.5 + _Offset * 0.5);

                // Remapeia [-0.5..0.5] -> [0..1] para a interpolação
                // _Sharpness = 1 -> transição dura no offset
                // _Sharpness < 1 -> transição suave
                half t = saturate(centered / max(_Sharpness, 0.0001) * 0.5 + 0.5);

                // Interpolação RGB
                half3 rgb = lerp(_ColorA.rgb, _ColorB.rgb, t);

                // Interpolação independente do Alpha
                half a = lerp(_ColorA.a, _ColorB.a, t);

                // Aplica intensidade
                rgb *= _Intensity;

                return half4(rgb, a);
            }
            ENDHLSL
        }

        // Pass de Shadow Caster
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

            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
            ENDHLSL
        }

        // Pass de Depth
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R
            Cull Off

            HLSLPROGRAM
            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment

            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthOnlyPass.hlsl"
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Unlit"
}