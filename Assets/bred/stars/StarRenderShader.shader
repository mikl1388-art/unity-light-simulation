// ReSharper disable InconsistentNaming
Shader "Custom/StarRenderShader"
{
    Properties
    {
        _MainTex ("Star Texture", 2D) = "white" {}
        _Size ("Star Size", Float) = 0.05
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend One One // Аддитивное смешивание (звёзды будут красиво светиться при наложении)
        ZWrite Off
        Cull Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct Star
            {
                float4 posVel; // xy - position, zw - velocity
                float4 data;   // x - mass
            };

            StructuredBuffer<Star> _Input;
            float _Size;
            sampler2D _MainTex;

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            // Вершинный шейдер. Мы будем генерировать четырёхугольники (билборды) для каждой звезды
            v2f vert (uint vertexID : SV_VertexID, uint instanceID : SV_InstanceID)
            {
                v2f o;
                
                // instanceID — это индекс нашей звезды в ComputeBuffer!
                Star star = _Input[instanceID];
                float2 starPos = star.posVel.xy;

                // Смещение для 4-х вершин одного спрайта звезды (vertexID будет от 0 до 3)
                // float2 offsets[4] = {
                //     float2(-0.5, -0.5),
                //     float2( 0.5, -0.5),
                //     float2(-0.5,  0.5),
                //     float2( 0.5,  0.5)
                // };
                
                float2 offsets[4] = {
                    float2(-5, -5),
                    float2( 5, -5),
                    float2(-5,  5),
                    float2( 5,  5)
                };

                float2 uvCoords[4] = {
                    float2(0, 0),
                    float2(1, 0),
                    float2(0, 1),
                    float2(1, 1)
                };

                // Вычисляем позицию вершины со смещением относительно центра звезды
                float2 finalPos = starPos + offsets[vertexID] * _Size;

                // Переводим в пространство экрана
                o.pos = UnityObjectToClipPos(float4(finalPos, 0, 1));
                o.uv = uvCoords[vertexID];

                return o;
            }

            float4 frag (v2f i) : SV_Target
            {
                // Рисуем текстуру звезды
                return tex2D(_MainTex, i.uv);
            }
            ENDHLSL
        }
    }
}