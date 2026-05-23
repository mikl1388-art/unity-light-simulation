Shader "Custom/SimShader"
{
    Properties
    {
        _BaseTexture("Base texture", 2D) = "white" {}
    }

    SubShader
    {
        Tags 
        { 
            "RenderType" = "Opaque" 
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            HLSLPROGRAM

            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BaseTexture);
            SAMPLER(sampler_BaseTexture);

            struct appdata
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            v2f vert(appdata v)
            {
                v2f o = (v2f)0;

                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv;
                
                return o;
            }

            half4 frag(v2f i) : SV_Target
            {
                float4 c = SAMPLE_TEXTURE2D(_BaseTexture, sampler_BaseTexture, i.uv);
                if (c.b < 0.0) return half4(0.0, 0.0, 1.0, 1.0);
                return half4(c.r, c.r, c.r, 1.0);
            }
            ENDHLSL
        }
    }
}
