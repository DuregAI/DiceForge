Shader "Diceforge/Stylized Water Surface"
{
    Properties
    {
        _DeepColor("Deep water", Color)=(.025,.24,.32,1)
        _ShallowColor("Shallow water", Color)=(.16,.65,.57,1)
        _FoamColor("Foam", Color)=(.76,.93,.84,1)
        _DepthRange("Depth color range", Float)=2
        _River("River UV flow", Float)=0
        _Still("Reduced motion", Float)=0
        _CausticStrength("Surface pattern strength", Range(0,1))=1
        _FoamStrength("Contact foam strength", Range(0,1))=1
        _SpecularStrength("Sun highlight strength", Range(0,1))=1
        _PatternScale("Surface pattern scale", Float)=1
    }
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent"}
        Pass
        {
            Tags {"LightMode"="UniversalForwardOnly"}
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            CBUFFER_START(UnityPerMaterial)
            half4 _DeepColor,_ShallowColor,_FoamColor;
            float _DepthRange,_River,_Still,_CausticStrength,_FoamStrength,_SpecularStrength,_PatternScale;
            CBUFFER_END
            struct A {float4 positionOS:POSITION;float2 uv:TEXCOORD0;};
            struct V {float4 positionCS:SV_POSITION;float3 world:TEXCOORD0;float2 uv:TEXCOORD1;float fog:TEXCOORD2;};
            V Vert(A a)
            {
                V o;o.world=TransformObjectToWorld(a.positionOS.xyz);
                o.positionCS=TransformWorldToHClip(o.world);o.uv=a.uv;o.fog=ComputeFogFactor(o.positionCS.z);return o;
            }
            float2 Hash(float2 p){return frac(sin(float2(dot(p,float2(127.1,311.7)),dot(p,float2(269.5,183.3))))*43758.5453);}
            float Caustic(float2 p,float t)
            {
                float2 cell=floor(p),f=frac(p);float d1=10,d2=10;
                for(int y=-1;y<=1;y++)for(int x=-1;x<=1;x++)
                {
                    float2 g=float2(x,y);float2 h=Hash(cell+g);
                    float2 q=g+.5+.32*sin(h*6.283+t)-f;float d=dot(q,q);
                    if(d<d1){d2=d1;d1=d;}else d2=min(d2,d);
                }
                return 1-smoothstep(.02,.12,d2-d1);
            }
            half4 Frag(V i):SV_Target
            {
                float t=_Time.y*(1-saturate(_Still));float2 screen=i.positionCS.xy/_ScaledScreenParams.xy;
                float raw=SampleSceneDepth(screen);
                #if !UNITY_REVERSED_Z
                raw=lerp(UNITY_NEAR_CLIP_VALUE,1,raw);
                #endif
                float3 bed=ComputeWorldSpacePosition(screen,raw,UNITY_MATRIX_I_VP);
                float depth=max(0,i.world.y-bed.y);
                float eyeGap=max(0,-TransformWorldToView(bed).z+TransformWorldToView(i.world).z);
                float deep=1-exp(-depth/max(.01,_DepthRange));
                float2 p=lerp(i.world.xz,float2(i.uv.x*1.8,i.uv.y),_River);
                float2 flow=float2(t*.08,-t*lerp(.10,.35,_River));
                float w=sin(p.x*3.8+p.y*1.7+t*.8)+sin(p.y*6-p.x*1.3-t*.6)*.5;
                float3 n=normalize(float3(cos(p.x*3.8+p.y*1.7+t*.8)*.09,1,cos(p.y*6-p.x*1.3-t*.6)*.075));
                Light sun=GetMainLight(TransformWorldToShadowCoord(i.world));
                float3 view=SafeNormalize(GetCameraPositionWS()-i.world);
                float spec=pow(saturate(dot(n,SafeNormalize(sun.direction+view))),95);
                float fresnel=pow(1-saturate(dot(n,view)),4);
                half3 color=lerp(_ShallowColor.rgb,_DeepColor.rgb,deep);
                color*=.82+.18*sun.shadowAttenuation;
                color+=Caustic(p*2.8*max(.1,_PatternScale)+flow,t*.45)*half3(.14,.23,.13)*(1-deep)*lerp(.42,.10,_River)*_CausticStrength;
                color=lerp(color,half3(.53,.77,.81),fresnel*.45);
                color+=sun.color*spec*.7*sun.shadowAttenuation*_SpecularStrength;
                float contact=1-smoothstep(.01,lerp(.20,.035,_River),eyeGap);
                float breakUp=smoothstep(-.3,.6,sin(p.x*21+sin(p.y*17+t)*2)+sin(p.y*24-t));
                float foam=contact*breakUp*.8*_FoamStrength;
                float stream=pow(saturate(sin(i.uv.x*39+sin(i.uv.y*4-t*1.8)*1.2)),16)*smoothstep(.15,.8,sin(i.uv.y*9-t*3+i.uv.x*8));
                float cascade=_River*smoothstep(5,5.7,i.uv.y);
                foam=max(foam,stream*_River*lerp(.16,.65,cascade)*_FoamStrength);
                color=lerp(color,_FoamColor.rgb,foam);
                float alpha=lerp(.76,.98,deep)+foam*.4;
                return half4(MixFog(color,i.fog),saturate(alpha));
            }
            ENDHLSL
        }
    }
}
