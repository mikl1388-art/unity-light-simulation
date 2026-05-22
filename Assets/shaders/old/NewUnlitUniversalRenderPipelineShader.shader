Shader "Custom/NewUnlitUniversalRenderPipelineShader"
{
    // БЛОК СВОЙСТВ (То, что видно в Инспекторе Unity)
    Properties
    {
        _MainColor ("Object Color", Color) = (1, 1, 1, 1)
        _LightColor ("Light Color", Color) = (0, 1, 1, 1) // По умолчанию бирюзовый
        _LightSpeed ("Light Speed", Range(0.1, 5.0)) = 1.0 // Ползунок скорости
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" }

        Pass
        {
            HLSLPROGRAM
            // Объявляем наши функции для видеокарты
            #pragma vertex vert
            #pragma fragment frag

            // Включаем базовые математические функции Unity
            #include "UnityCG.cginc"

            float4 _MainColor;
            float4 _LightColor;
            float _LightSpeed;

            // 1. Что приходит ИЗ модели (CPU) В вершинный шейдер
            struct appdata
            {
                float4 vertex : POSITION; // Координаты вершины
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;    // Координаты текстуры
            };

            // 2. Что передается ИЗ вершинного шейдера В фрагментный
            struct v2f
            {
                float4 vertex : SV_POSITION; // Позиция на экране
                float3 worldNormal : TEXCOORD1;
                float2 uv : TEXCOORD0;       // Передаем UV дальше
            };

            // ВЕРШИННЫЙ ШЕЙДЕР
            v2f vert (appdata v)
            {
                v2f o;
                // Трансформируем точку из 3D-пространства в координаты экрана
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv; 
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                return o;
            }

            // ФРАГМЕНТНЫЙ (ПИКСЕЛЬНЫЙ) ШЕЙДЕР
            fixed4 frag (v2f i) : SV_Target
            {
                // Используем нашу переменную скорости для изменения времени
                float time = _Time.y * _LightSpeed;

                // Вектор света теперь динамический и зависит от нашей переменной времени
                float3 lightDir = normalize(float3(sin(time), 0.5, cos(time)));

                float3 normal = normalize(i.worldNormal);
                float lightIntensity = dot(normal, lightDir);
                lightIntensity = max(0.0, lightIntensity);

                float3 ambientLight = _LightColor.rgb / 25;

                // Финальный расчет: смешиваем цвет объекта, цвет света и интенсивность
                float3 diffuseReflection = _MainColor.rgb * _LightColor.rgb * lightIntensity;
                float3 finalColor = diffuseReflection + ambientLight.rgb * _MainColor.rgb;

                return fixed4(finalColor, 1.0);
            }
            ENDHLSL
        }
    }
}