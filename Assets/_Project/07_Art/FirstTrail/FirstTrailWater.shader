Shader "Diceforge/First Trail Creek"
{
    Properties
    {
        _DeepColor("Deep water", Color) = (0.08,0.30,0.31,1)
        _ShallowColor("Shallow water", Color) = (0.22,0.45,0.39,1)
        _FoamColor("Reflected light", Color) = (0.59,0.79,0.62,1)
        _Still("Reduced motion", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry+10" }
        Pass
        {
            Tags { "LightMode"="UniversalForwardOnly" }
            Cull Off ZWrite On
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _DeepColor, _ShallowColor, _FoamColor;
                float _Still;
            CBUFFER_END
            struct Attributes {float4 positionOS:POSITION;float2 uv:TEXCOORD0;};
            struct Varyings {float4 positionCS:SV_POSITION;float3 positionWS:TEXCOORD0;float2 uv:TEXCOORD1;float fog:TEXCOORD2;};
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionWS=TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS=TransformWorldToHClip(output.positionWS);
                output.uv=input.uv;output.fog=ComputeFogFactor(output.positionCS.z);return output;
            }
            half4 Frag(Varyings input):SV_Target
            {
                float t=_Time.y*(1-saturate(_Still));
                float edge=smoothstep(.34,.50,abs(input.uv.x-.5));
                float ripple=sin(input.uv.y*37-t*1.5+sin(input.uv.x*19+input.uv.y*5+t*.32)*1.5);
                float brokenLight=smoothstep(.50,.95,sin(input.uv.x*54+input.uv.y*13));
                float glint=pow(saturate(ripple),38)*.035*brokenLight;
                half3 color=lerp(_DeepColor.rgb,_ShallowColor.rgb,.30+edge*.55+ripple*.018);
                color=lerp(color,_FoamColor.rgb,glint+edge*.12);
                Light sun=GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                half3 lighting=SampleSH(half3(0,1,0))*.65+sun.color*(.28+.35*sun.shadowAttenuation);
                return half4(MixFog(color*lighting,input.fog),1);
            }
            ENDHLSL
        }
    }
}
