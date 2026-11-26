Shader "Custom/GradientCube"
{
    Properties
    {
        [Header(Color Settings)]
        _ColorTop ("Color Top (com Alpha)", Color) = (1, 0, 0, 1)
        _ColorBottom ("Color Bottom (com Alpha)", Color) = (0, 0, 1, 0)

        [Header(World Gradient Controls)]
        _GradientOffset ("Height Offset (Altura da Linha)", Float) = 0.0
        _GradientSmoothness ("Smoothness (Suavizador)", Range(0.01, 500.0)) = 1.0

        [Header(Flip UV Controls)]
        [Toggle] _FlipU ("Flip UV Horizontal (World X)", Float) = 0
        [Toggle] _FlipV ("Flip UV Vertical (World Y)", Float) = 0
    }

    SubShader
    {
        Tags 
        { 
            "RenderType" = "Transparent" 
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent"
        }

        // Configuração de Blending para Transparência
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _ColorTop;
            float4 _ColorBottom;
            float _GradientOffset;
            float _GradientSmoothness;
            float _FlipU;
            float _FlipV;
        CBUFFER_END

        struct Attributes
        {
            float4 positionOS   : POSITION;
        };

        struct Varyings
        {
            float4 positionHCS  : SV_POSITION;
            float3 positionWS   : TEXCOORD0;
        };
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            Varyings vert(Attributes input)
            {
                Varyings output;

                // Converte a posição local para Espaço de Tela (Clip Space)
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                
                // Converte a posição local para Espaço de Mundo (World Space)
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // Obtém a posição de mundo
                float3 worldPos = input.positionWS;

                // Aplicação do Flip nas coordenadas de Mundo
                if (_FlipU > 0.5) worldPos.x = -worldPos.x;
                if (_FlipV > 0.5) worldPos.y = -worldPos.y;

                // Ajusta a altura da linha baseando-se no offset e calcula o fator com a suavidade
                float height = (worldPos.y - _GradientOffset) / max(_GradientSmoothness, 0.0001);
                
                // Normaliza o fator entre 0 e 1 de forma suave
                float factor = saturate(height + 0.5);

                // Interpola entre a cor inferior e superior (incluindo o Alpha)
                half4 gradientColor = lerp(_ColorBottom, _ColorTop, factor);

                return gradientColor;
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}