Shader "Diceforge/Living Forest Splash"
{
    Properties
    {
        _Still("Reduced motion",Float)=0
        _RingStrength("Ripple strength",Range(0,1))=1
        _Opacity("Splash opacity",Range(0,1))=1
        _FoamColor("Foam color",Color)=(.68,.88,.83,1)
        _Spray("Ballistic droplets",Float)=0
    }
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
            float _Still,_RingStrength,_Opacity,_Spray;
            half4 _FoamColor;
            CBUFFER_END
            float _ForestTime;
            struct A{float4 positionOS:POSITION;float2 uv:TEXCOORD0;float2 velocity:TEXCOORD1;float4 color:COLOR;};
            struct V{float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;float age:TEXCOORD1;};
            V Vert(A a)
            {
                V o;float3 world=TransformObjectToWorld(a.positionOS.xyz);
                float phase=frac(_ForestTime*(1-saturate(_Still))*.95+a.color.r);
                float age=phase*.68;
                world+=_Spray*float3(-a.velocity.x*age,(.95+a.color.g*.35)*age-2.65*age*age,-a.velocity.y*age);
                o.age=phase;o.positionCS=TransformWorldToHClip(world);o.uv=a.uv;return o;
            }
            half4 Frag(V i):SV_Target
            {
                float2 p=(i.uv-.5)*2;float r=length(p);float t=_ForestTime*(1-saturate(_Still));
                if(_Spray>.5)
                {
                    float alpha=(1-smoothstep(.20,1,r))*smoothstep(0,.12,i.age)*(1-smoothstep(.55,.95,i.age));
                    return half4(_FoamColor.rgb,alpha*_Opacity);
                }
                float a=atan2(p.y,p.x);float irregular=r+.035*sin(a*7+t)+.025*sin(a*13-t);
                float ring=pow(saturate(sin(irregular*24-t*3)),14)*(1-smoothstep(.45,.94,r))*.40;
                float core=(1-smoothstep(.12,.48,irregular))*(.40+.10*sin(p.x*35+sin(p.y*26+t)));
                return half4(_FoamColor.rgb,saturate(core+ring*_RingStrength)*_Opacity*_FoamColor.a);
            }
            ENDHLSL
        }
    }
}

