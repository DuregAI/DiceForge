Shader "Hidden/Diceforge/ChestShopSweep"
{
    Properties
    {
        _PrimaryColor ("Primary Color", Color) = (1.0, 0.86, 0.32, 0.09)
        _AccentColor ("Accent Color", Color) = (0.38, 0.95, 0.82, 0.06)
        _GlowColor ("Glow Color", Color) = (1.0, 0.98, 0.84, 0.045)
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
        }

        Cull Off
        ZWrite Off
        ZTest Always
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            fixed4 _PrimaryColor;
            fixed4 _AccentColor;
            fixed4 _GlowColor;

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            float easedOscillation(float phase)
            {
                float wave = 0.5 + 0.5 * sin(phase);
                return wave * wave * (3.0 - 2.0 * wave);
            }

            float sweepBand(float coordinate, float center, float width)
            {
                return smoothstep(width, 0.0, abs(coordinate - center));
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 uv = i.uv;
                float basePhase = _Time.y * 1.05;

                float posA = lerp(-0.22, 1.22, easedOscillation(basePhase));
                float posB = lerp(1.18, -0.18, easedOscillation(basePhase + 1.5707963));

                float coordA = uv.x * 0.92 + uv.y * 0.34;
                float coordB = uv.x * 0.88 - uv.y * 0.28 + 0.16;

                float sweepA = sweepBand(coordA, posA, 0.14);
                float sweepB = sweepBand(coordB, posB, 0.11);
                float centerGlow = saturate(1.0 - distance(uv, float2(0.5, 0.5)) * 1.95);
                float vignette = smoothstep(0.0, 0.20, uv.x) * smoothstep(0.0, 0.20, uv.y) * smoothstep(0.0, 0.20, 1.0 - uv.x) * smoothstep(0.0, 0.20, 1.0 - uv.y);

                fixed4 color = 0;
                color += _PrimaryColor * sweepA;
                color += _AccentColor * sweepB;
                color += _GlowColor * (centerGlow * 0.18);
                color.a *= vignette;
                color.rgb *= color.a + 0.06;
                return saturate(color);
            }
            ENDCG
        }
    }
}
