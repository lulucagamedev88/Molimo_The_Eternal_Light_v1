Shader "CustomRenderTexture/CloudShadow"
{
    Properties
    {
        _Speed ("Speed", Vector) = (0.1,0,0.1,0)
        _MainTex ("InputTex", 2D) = "white" {}
        _MainTex2("InputTex", 2D) = "white" {}
    }

    SubShader
    {
        Blend One Zero
        Lighting Off
        Pass
        {
            Name "CloudShadow"

            CGPROGRAM
            #include "UnityCustomRenderTexture.cginc"
            #pragma vertex CustomRenderTextureVertexShader
            #pragma fragment frag
            #pragma target 3.0

            float4    _Speed;
            sampler2D _MainTex;
            sampler2D _MainTex2;

            float4 frag(v2f_customrendertexture IN) : COLOR
            {
                float2 scroll1 = _Time.y * _Speed.xy;
                float2 scroll2 = _Time.y * _Speed.zw;

                float c1 = tex2D(_MainTex,  IN.localTexcoord.xy + scroll1).r;
                float c2 = tex2D(_MainTex2, IN.localTexcoord.xy + scroll2).r;

                // 0.1 = sombra máxima | 1.0 = sem sombra
                float v = max(0.1, c1 * c2);
                return float4(v, v, v, 1);
            }
            ENDCG
        }
    }
}