Shader "Diceforge/Living Forest Foliage"
{
    Properties
    {
        _BaseMap("Albedo",2D)="white"{}
        _BaseColor("Tint",Color)=(.5,.65,.22,1)
        _WindStrength("Bend distance",Float)=.035
        _AutoHeight("Automatic tree height (0 uses vertex mask)",Float)=0
        _Translucency("Leaf backlight",Range(0,1))=.22
        _Variation("Leaf variation",Range(0,1))=.3
        _Saturation("Pigment saturation",Range(0,1))=.8
        _Emblem("Cloth leaf emblem",Float)=0
    }
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry"}
        Cull Off
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        #include "LivingWind.hlsl"
        TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
        CBUFFER_START(UnityPerMaterial)
        float4 _BaseMap_ST;
        half4 _BaseColor;
        float _WindStrength, _AutoHeight, _Translucency, _Variation, _Saturation, _Emblem;
        CBUFFER_END
        struct A {float4 positionOS:POSITION;float3 normalOS:NORMAL;float2 uv:TEXCOORD0;half4 color:COLOR;UNITY_VERTEX_INPUT_INSTANCE_ID};
        struct V {float4 positionCS:SV_POSITION;float3 world:TEXCOORD0;half3 normal:TEXCOORD1;float2 uv:TEXCOORD2;float fog:TEXCOORD3;half variation:TEXCOORD4;};
        V Vert(A a)
        {
            UNITY_SETUP_INSTANCE_ID(a);
            V o;o.world=ForestWind(a.positionOS.xyz,a.color,_WindStrength,_AutoHeight);
            o.positionCS=TransformWorldToHClip(o.world);o.normal=TransformObjectToWorldNormal(a.normalOS);
            o.uv=TRANSFORM_TEX(a.uv,_BaseMap);o.fog=ComputeFogFactor(o.positionCS.z);o.variation=a.color.g;return o;
        }
        ENDHLSL
        Pass
        {
            Tags {"LightMode"="UniversalForwardOnly"}
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_instancing
            half4 Frag(V i, FRONT_FACE_TYPE face:FRONT_FACE_SEMANTIC):SV_Target
            {
                half3 n=normalize(i.normal)*IS_FRONT_VFACE(face,1,-1);
                Light sun=GetMainLight(TransformWorldToShadowCoord(i.world));
                half3 albedo=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv).rgb*_BaseColor.rgb;
                albedo=lerp(dot(albedo,half3(.2126,.7152,.0722)),albedo,_Saturation);
                if(_Emblem>.5)
                {
                    float2 p=i.uv-float2(.49,.53);
                    float stem=(1-smoothstep(.012,.024,abs(p.x)))*(1-smoothstep(.22,.25,abs(p.y)));
                    float mark=stem;
                    for(int j=0;j<3;j++)
                    {
                        float y=-.12+j*.12;
                        float2 q=p-float2(.07,y);float2 v=float2((q.x+q.y)*.707,(q.y-q.x)*.707);
                        mark=max(mark,1-smoothstep(.88,1,length(v/float2(.047,.092))));
                        q=p-float2(-.07,y+.04);v=float2((q.x-q.y)*.707,(q.y+q.x)*.707);
                        mark=max(mark,1-smoothstep(.88,1,length(v/float2(.047,.092))));
                    }
                    albedo=lerp(albedo,half3(.95,.73,.32),mark);
                }
                albedo*=lerp(1,.8+i.variation*.35,_Variation);
                half direct=saturate(dot(n,sun.direction))*.78;
                half transmission=pow(saturate(dot(-n,sun.direction)),1.5)*_Translucency;
                half3 color=albedo*(SampleSH(n)*.62+sun.color*((direct+transmission)*sun.shadowAttenuation+.12));
                return half4(MixFog(color,i.fog),1);
            }
            ENDHLSL
        }
        Pass
        {
            Tags {"LightMode"="ShadowCaster"}
            ZWrite On ColorMask 0
            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            float3 _LightDirection;
            float3 _LightPosition;
            float4 ShadowVert(A a):SV_POSITION
            {
                UNITY_SETUP_INSTANCE_ID(a);
                float3 world=ForestWind(a.positionOS.xyz,a.color,_WindStrength,_AutoHeight);
                float3 normal=TransformObjectToWorldNormal(a.normalOS);
                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                float3 direction=normalize(_LightPosition-world);
                #else
                float3 direction=_LightDirection;
                #endif
                float4 clip=TransformWorldToHClip(ApplyShadowBias(world,normal,direction));
                #if UNITY_REVERSED_Z
                clip.z=min(clip.z,UNITY_NEAR_CLIP_VALUE);
                #else
                clip.z=max(clip.z,UNITY_NEAR_CLIP_VALUE);
                #endif
                return clip;
            }
            half4 ShadowFrag():SV_Target{return 0;}
            ENDHLSL
        }
        Pass
        {
            Tags {"LightMode"="DepthOnly"}
            ZWrite On ColorMask 0
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment DepthFrag
            #pragma multi_compile_instancing
            half4 DepthFrag(V i):SV_Target{return 0;}
            ENDHLSL
        }
    }
}
