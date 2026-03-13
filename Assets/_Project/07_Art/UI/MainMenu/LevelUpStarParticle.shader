Shader "Hidden/Diceforge/LevelUpStarParticle"
{
    Properties
    {
        _MainTex ("Particle Texture", 2D) = "white" {}
        _Tint ("Tint", Color) = (1, 1, 1, 1)
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
        }

        Blend SrcAlpha One
        Cull Off
        Lighting Off
        ZWrite Off
        ZTest Always

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
                fixed4 color : COLOR;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };

            sampler2D _MainTex;
            fixed4 _Tint;

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color * _Tint;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 tex = tex2D(_MainTex, i.uv);
                float2 uv = (i.uv * 2.0) - 1.0;
                float radial = length(uv);
                float halo = 1.0 - smoothstep(0.3, 1.0, radial);
                float alpha = saturate((tex.a * 0.92 + halo * 0.18) * i.color.a);
                fixed3 rgb = tex.rgb * i.color.rgb * (alpha * 1.2);
                return fixed4(rgb, alpha);
            }
            ENDCG
        }
    }
}
