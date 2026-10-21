Shader "Diceforge/Weathered Forest Stone"
{
    Properties
    {
        _BaseColor("Limestone",Color)=(.64,.59,.45,1)
        _MossColor("Moss",Color)=(.20,.29,.07,1)
        _MossAmount("Moss coverage",Range(0,1))=.2
        _Scale("Grain scale",Float)=5
        _Smoothness("Smoothness",Range(0,1))=.16
        _Relief("Fine relief",Range(0,.5))=.12
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        CBUFFER_START(UnityPerMaterial)
        half4 _BaseColor,_MossColor;
        float _MossAmount,_Scale,_Smoothness,_Relief;
        CBUFFER_END
        struct A { float4 positionOS:POSITION;float3 normalOS:NORMAL;UNITY_VERTEX_INPUT_INSTANCE_ID };
        struct V { float4 positionCS:SV_POSITION;float3 world:TEXCOORD0;half3 normal:TEXCOORD1;float fog:TEXCOORD2; };
        float Hash(float3 p) { p=frac(p*.1031);p+=dot(p,p.yzx+33.33);return frac((p.x+p.y)*p.z); }
        float Noise(float3 p)
        {
            float3 cell=floor(p),f=frac(p);f=f*f*(3-2*f);
            return lerp(lerp(lerp(Hash(cell),Hash(cell+float3(1,0,0)),f.x),lerp(Hash(cell+float3(0,1,0)),Hash(cell+float3(1,1,0)),f.x),f.y),
                lerp(lerp(Hash(cell+float3(0,0,1)),Hash(cell+float3(1,0,1)),f.x),lerp(Hash(cell+float3(0,1,1)),Hash(cell+float3(1,1,1)),f.x),f.y),f.z);
        }
        V Vert(A a)
        {
            UNITY_SETUP_INSTANCE_ID(a);
            V o;o.world=TransformObjectToWorld(a.positionOS.xyz);o.positionCS=TransformWorldToHClip(o.world);
            o.normal=TransformObjectToWorldNormal(a.normalOS);o.fog=ComputeFogFactor(o.positionCS.z);return o;
        }
        ENDHLSL
        Pass
        {
            Tags { "LightMode"="UniversalForwardOnly" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            // The forest renderer applies contact AO after opaques. Sampling a second
            // AO texture here also leaks stale camera data into planar reflections.
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            half4 Frag(V i):SV_Target
            {
                float3 p=i.world*_Scale;
                float broad=Noise(p*.55),grain=Noise(p*7.7),pore=Noise(p*29);
                float seam=abs(Noise(p*1.8+Noise(p*.6)*2)-.5);
                float fissure=(1-smoothstep(.007,.023,seam))*smoothstep(.57,.72,Noise(p*.85+19));
                float moss=smoothstep(.5,.72,Noise(i.world*4.3+7)+max(0,i.normal.y)*.16+_MossAmount*.55-.2);
                moss*=saturate(_MossAmount*3);
                half3 stone=_BaseColor.rgb*(.78+broad*.32+grain*.10);
                stone*=1-fissure*.25;stone*=.97+.06*pore;
                half3 albedo=lerp(stone,_MossColor.rgb*(.75+grain*.5),moss);
                float3 normal=normalize((float3)i.normal);
                // Screen-space surface gradient adds grain without changing the landing mesh.
                float height=grain*.010-fissure*.008;
                float3 dx=ddx(i.world),dy=ddy(i.world);
                float3 r1=cross(dy,normal),r2=cross(normal,dx);
                float determinant=dot(dx,r1);
                float3 gradient=sign(determinant)*(ddx(height)*r1+ddy(height)*r2)/max(abs(determinant),1e-8);
                normal=SafeNormalize(normal-_Relief*clamp(gradient,-2,2));
                Light sun=GetMainLight(TransformWorldToShadowCoord(i.world));
                float diffuse=saturate(dot(normal,sun.direction));
                half3 color=albedo*(SampleSH(normal)+sun.color*(diffuse*sun.shadowAttenuation+.075));
                float3 view=GetWorldSpaceNormalizeViewDir(i.world);
                float gloss=lerp(24,96,_Smoothness);
                float spec=pow(saturate(dot(normal,SafeNormalize(view+sun.direction))),gloss)*.025*(1-moss);
                color+=sun.color*spec*sun.shadowAttenuation;
                return half4(MixFog(color,i.fog),1);
            }
            ENDHLSL
        }
        Pass
        {
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On ColorMask 0
            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            float3 _LightDirection,_LightPosition;
            float4 ShadowVert(A a):SV_POSITION
            {
                UNITY_SETUP_INSTANCE_ID(a);
                float3 world=TransformObjectToWorld(a.positionOS.xyz);
                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                float3 direction=normalize(_LightPosition-world);
                #else
                float3 direction=_LightDirection;
                #endif
                float4 clip=TransformWorldToHClip(ApplyShadowBias(world,TransformObjectToWorldNormal(a.normalOS),direction));
                #if UNITY_REVERSED_Z
                clip.z=min(clip.z,UNITY_NEAR_CLIP_VALUE);
                #else
                clip.z=max(clip.z,UNITY_NEAR_CLIP_VALUE);
                #endif
                return clip;
            }
            half4 ShadowFrag():SV_Target { return 0; }
            ENDHLSL
        }
        Pass
        {
            Tags { "LightMode"="DepthOnly" }
            ZWrite On ColorMask 0
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment DepthFrag
            #pragma multi_compile_instancing
            half4 DepthFrag(V i):SV_Target { return 0; }
            ENDHLSL
        }
    }
}
