Shader "Diceforge/Study Rounded Foliage"
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
        #include "../LivingForest/Shaders/LivingWind.hlsl"
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
            #pragma target 3.5
            #pragma multi_compile_fog
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_instancing
            half3 LeafLight(Light light, half3 n, half3 view, half3 albedo)
            {
                half diffuse=saturate((dot(n,light.direction)+.16)/1.16);
                half transmission=pow(saturate(dot(-n,light.direction)),2)*_Translucency;
                half3 h=SafeNormalize(light.direction+view);
                half sheen=pow(saturate(dot(n,h)),24)*.055;
                return (albedo*(diffuse*.84+transmission)+sheen)*light.color*light.distanceAttenuation*light.shadowAttenuation;
            }
            half4 Frag(V i, FRONT_FACE_TYPE face:FRONT_FACE_SEMANTIC):SV_Target
            {
                half3 n=normalize(i.normal)*IS_FRONT_VFACE(face,1,-1);
                half3 view=GetWorldSpaceNormalizeViewDir(i.world);
                half3 albedo=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv).rgb*_BaseColor.rgb;
                albedo=lerp(dot(albedo,half3(.2126,.7152,.0722)),albedo,_Saturation);
                albedo*=lerp(.76,1.12,i.variation);
                half3 color=albedo*SampleSH(n)*.85;
                color+=LeafLight(GetMainLight(TransformWorldToShadowCoord(i.world)),n,view,albedo);
                #if defined(_ADDITIONAL_LIGHTS)
                InputData inputData=(InputData)0;
                inputData.positionWS=i.world;
                inputData.normalizedScreenSpaceUV=GetNormalizedScreenSpaceUV(i.positionCS);
                #if USE_CLUSTER_LIGHT_LOOP
                [loop] for(uint lightIndex=0;lightIndex<min(URP_FP_DIRECTIONAL_LIGHTS_COUNT,MAX_VISIBLE_LIGHTS);lightIndex++)
                {
                    CLUSTER_LIGHT_LOOP_SUBTRACTIVE_LIGHT_CHECK
                    color+=LeafLight(GetAdditionalLight(lightIndex,i.world,half4(1,1,1,1)),n,view,albedo);
                }
                #endif
                uint pixelLightCount=GetAdditionalLightsCount();
                LIGHT_LOOP_BEGIN(pixelLightCount)
                    color+=LeafLight(GetAdditionalLight(lightIndex,i.world,half4(1,1,1,1)),n,view,albedo);
                LIGHT_LOOP_END
                #endif
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
