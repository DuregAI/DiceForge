Shader "Hidden/Diceforge/ChestOpenBurst"
{
    Properties
    {
        _BurstStartTime ("Burst Start Time", Float) = -1000
        _GoldColor ("Gold Color", Color) = (1.0, 0.86, 0.32, 0.95)
        _MintColor ("Mint Color", Color) = (0.52, 1.0, 0.78, 0.9)
        _BlueColor ("Blue Color", Color) = (0.55, 0.78, 1.0, 0.9)
        _RoseColor ("Rose Color", Color) = (1.0, 0.62, 0.72, 0.9)
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

            float _BurstStartTime;
            fixed4 _GoldColor;
            fixed4 _MintColor;
            fixed4 _BlueColor;
            fixed4 _RoseColor;

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            float hash11(float n)
            {
                return frac(sin(n * 143.543) * 43758.5453);
            }

            float2 rotate2d(float2 p, float angle)
            {
                float s = sin(angle);
                float c = cos(angle);
                return float2(c * p.x - s * p.y, s * p.x + c * p.y);
            }

            float rectParticle(float2 uv, float2 center, float2 size, float angle)
            {
                float2 p = rotate2d(uv - center, angle);
                float2 d = abs(p) - size;
                return saturate(1.0 - max(d.x, d.y) * 80.0);
            }

            fixed4 pickColor(float index)
            {
                float selector = frac(index * 0.37);
                if (selector < 0.25)
                    return _GoldColor;
                if (selector < 0.5)
                    return _MintColor;
                if (selector < 0.75)
                    return _BlueColor;
                return _RoseColor;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float elapsed = max(0.0, _Time.y - _BurstStartTime);
                float life = saturate(1.0 - elapsed / 1.05);
                if (life <= 0.001)
                    return 0;

                float eased = 1.0 - pow(1.0 - life, 2.0);
                float2 uv = i.uv;
                float2 origin = float2(0.5, 0.5);

                float flash = smoothstep(0.55, 0.0, distance(uv, origin)) * eased * 0.32;
                float ringDistance = abs(distance(uv, origin) - elapsed * 0.42);
                float ring = smoothstep(0.09, 0.0, ringDistance) * life * 0.24;

                fixed4 color = 0;
                color += _GoldColor * flash;
                color += _MintColor * ring;

                [unroll]
                for (int idx = 0; idx < 18; idx++)
                {
                    float fi = idx + 1.0;
                    float angle = hash11(fi * 1.21) * 6.2831853;
                    float speed = lerp(0.18, 0.62, hash11(fi * 2.37));
                    float spin = hash11(fi * 3.11) * 6.2831853 + elapsed * lerp(-3.5, 3.5, hash11(fi * 4.17));
                    float2 dir = float2(cos(angle), sin(angle));
                    float2 drift = float2(dir.x, dir.y * 0.72);
                    float2 particleCenter = origin + drift * elapsed * speed + float2(0.0, -elapsed * elapsed * 0.16 * lerp(0.5, 1.15, hash11(fi * 5.31)));
                    float2 particleSize = float2(0.010, 0.023) * lerp(0.75, 1.35, hash11(fi * 6.07));
                    float particle = rectParticle(uv, particleCenter, particleSize, spin) * life;
                    color += pickColor(fi) * particle * 0.82;
                }

                color.a = saturate(color.a);
                color.rgb *= color.a + 0.18;
                return saturate(color);
            }
            ENDCG
        }
    }
}
