Shader "Custom/WallStripes"
{
    Properties
    {
        _Color ("Main Color", Color) = (1,1,1,1)
        _MainTex ("Albedo (RGB)", 2D) = "white" {}
        _StripeColor ("Stripe Color", Color) = (1,1,1,1)
        _StripeWidth ("Stripe Width", Range(0, 1)) = 0.2
        _StripeSpacing ("Stripe Spacing", Range(0, 2)) = 0.5
        _StripeOffset ("Stripe Offset", Float) = 0
        _PulseSpeed ("Pulse Speed", Float) = 1
        _EmissionColor ("Emission Color", Color) = (0,0,0,0)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows
        #pragma target 3.0

        sampler2D _MainTex;

        struct Input
        {
            float2 uv_MainTex;
            float3 worldPos;
        };

        fixed4 _Color;
        fixed4 _StripeColor;
        fixed4 _EmissionColor;
        float _StripeWidth;
        float _StripeSpacing;
        float _StripeOffset;
        float _PulseSpeed;

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            fixed4 c = tex2D (_MainTex, IN.uv_MainTex) * _Color;
            
            // Создаем полоски
            float stripePattern = sin((IN.worldPos.y + _StripeOffset) * _StripeSpacing * 10);
            stripePattern = abs(stripePattern);
            
            // Пульсация полосок
            float pulse = (sin(_Time.y * _PulseSpeed) + 1) * 0.5;
            float stripeThreshold = _StripeWidth * (0.8 + pulse * 0.2);
            
            // Применяем цвет полосок
            if (stripePattern < stripeThreshold)
            {
                o.Albedo = _StripeColor.rgb;
                o.Emission = _EmissionColor.rgb;
            }
            else
            {
                o.Albedo = c.rgb;
                o.Emission = _EmissionColor.rgb * 0.3;
            }
            
            o.Alpha = c.a;
        }
        ENDCG
    }
    FallBack "Diffuse"
}