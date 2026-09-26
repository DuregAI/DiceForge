Shader "Diceforge/Waterfall Splash"
{
    Properties {_Still("Reduced motion",Float)=0}
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "Queue"="Transparent+10" "RenderType"="Transparent"}
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            float _Still;
            CBUFFER_END
            struct A{float4 positionOS:POSITION;float2 uv:TEXCOORD0;};
            struct V{float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;};
            V Vert(A a){V o;o.positionCS=TransformObjectToHClip(a.positionOS.xyz);o.uv=a.uv;return o;}
            half4 Frag(V i):SV_Target
            {
                float2 p=(i.uv-.5)*2;float r=length(p);float t=_Time.y*(1-saturate(_Still));
                float a=atan2(p.y,p.x);float irregular=r+.035*sin(a*7+t)+.025*sin(a*13-t);
                float ring=pow(saturate(sin(irregular*24-t*3)),14)*(1-smoothstep(.45,.94,r))*.40;
                float core=(1-smoothstep(.12,.48,irregular))*(.40+.10*sin(p.x*35+sin(p.y*26+t)));
                return half4(.68,.88,.83,saturate(core+ring));
            }
            ENDHLSL
        }
    }
}
