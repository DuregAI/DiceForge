Shader "Diceforge/First Trail Lake"
{
    Properties
    {
        _BaseColor("Water", Color) = (0.10,0.40,0.48,1)
        _Still("Reduced motion", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS:POSITION; };
            struct Varyings { float4 positionCS:SV_POSITION; float3 world:TEXCOORD0; float fog:TEXCOORD1; };
            CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            float _Still;
            CBUFFER_END
            Varyings Vert(Attributes input)
            {
                Varyings o;o.world=TransformObjectToWorld(input.positionOS.xyz);
                o.positionCS=TransformWorldToHClip(o.world);o.fog=ComputeFogFactor(o.positionCS.z);return o;
            }
            half4 Frag(Varyings i):SV_Target
            {
                float2 p=i.world.xz;float t=_Time.y*(1-saturate(_Still))*.18;
                float ripple=sin(p.x*7+sin(p.y*5+t))*sin(p.y*8+cos(p.x*4-t));
                float caustic=pow(saturate(1-abs(ripple)*4),10);
                float shore=1-smoothstep(1.02,1.55,length(p/float2(3.75,2.25)));
                half3 c=_BaseColor.rgb+half3(.045,.14,.10)*shore;
                c+=half3(.16,.24,.16)*caustic*(.12+.28*shore);
                c+=pow(saturate(ripple),35)*.035;
                return half4(MixFog(c,i.fog),1);
            }
            ENDHLSL
        }
    }
}
