Shader "Custom/SolidSlice"
{
    Properties
    {
        _Color ("Main Color", Color) = (1,1,1,1)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }

        Pass
        {
            ColorMask 0 
            ZWrite On   
            Cull Back   
        }

        Pass
        {
            Cull Front  
            ZWrite Off  // Вернули твое исправление!
            ZTest Less  
            
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            
            struct appdata { float4 vertex : POSITION; };
            struct v2f { float4 pos : SV_POSITION; };
            
            float4 _Color;
            
            v2f vert(appdata v) {
                v2f o; o.pos = UnityObjectToClipPos(v.vertex); return o;
            }
            
            fixed4 frag(v2f i) : SV_Target {
                float3 col = _Color.rgb;
                
                // Жестко заданные настройки прямо в коде:
                float saturation = 0.55; // От 0 (серый) до 1 (кислотный)
                float brightness = 0.7;  // Яркость
                
                // 1. Вычисляем серый оттенок (яркость цвета)
                float luminance = dot(col, float3(0.299, 0.587, 0.114));
                
                // 2. Смешиваем серый и оригинальный цвет (гасим "вырвиглазность")
                col = lerp(float3(luminance, luminance, luminance), col, saturation);
                
                // 3. Немного приглушаем общую яркость
                col *= brightness;
                
                return float4(col, 1.0); 
            }
            ENDCG
        }
    }
}