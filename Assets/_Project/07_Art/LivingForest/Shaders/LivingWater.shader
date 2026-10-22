Shader "Diceforge/Living Forest Water"
{
    Properties
    {
        _ShallowColor("Shallow absorption",Color)=(.34,.66,.55,1)
        _DeepColor("Deep absorption",Color)=(.025,.18,.23,1)
        _FoamColor("Contact foam",Color)=(.79,.87,.74,1)
        _Density("Absorption density",Float)=1.2
        _Roughness("Surface roughness",Range(.03,.5))=.14
        _Refraction("Refraction",Range(0,.04))=.008
        _ReflectionStrength("Reflection",Range(0,1))=.72
        _WaveStrength("Wave normals",Range(0,.4))=.1
        _FlowSpeed("Flow speed",Float)=.16
        _OpenWaterDepth("Depth when no lake bed is visible",Range(.1,8))=1.6
        _LakeRippleScale("Lake ripple frequency",Range(.25,2))=1
        _LakeFoamAmount("Quiet lake contact foam",Range(0,.5))=.14
        _LakeReflectionBlur("Lake reflection softness",Range(0,2))=1
        _LakeReflectionFloor("Lake minimum reflection",Range(0,.4))=.12
        _CausticMap("Shallow bed light pattern",2D)="black"{}
        _CausticStrength("Shallow bed light strength",Range(0,.5))=.16
        _River("River UV flow",Float)=0
        _Still("Reduced motion",Float)=0
        [HideInInspector]_PlanarReflectionTex("Planar reflection",2D)="black"{}
        [HideInInspector]_HasReflection("Reflection ready",Float)=0
        [HideInInspector]_PlanarReflectionMaxMip("Available reflection mip",Float)=0
    }
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent-20"}
        Pass
        {
            Tags {"LightMode"="UniversalForwardOnly"}
            Blend SrcAlpha OneMinusSrcAlpha ZWrite Off Cull Off
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"
            TEXTURE2D(_PlanarReflectionTex);SAMPLER(sampler_PlanarReflectionTex);
            TEXTURE2D(_CausticMap);SAMPLER(sampler_CausticMap);
            CBUFFER_START(UnityPerMaterial)
            half4 _ShallowColor,_DeepColor,_FoamColor;
            float _Density,_Roughness,_Refraction,_ReflectionStrength,_WaveStrength,_FlowSpeed,_River,_Still,_HasReflection;
            float _OpenWaterDepth,_LakeRippleScale,_LakeFoamAmount,_LakeReflectionBlur,_PlanarReflectionMaxMip;
            float _LakeReflectionFloor,_CausticStrength;
            CBUFFER_END
            float _ForestTime,_ForestMotion;
            struct A {float4 positionOS:POSITION;float3 normalOS:NORMAL;float2 uv:TEXCOORD0;half4 color:COLOR;};
            struct V {float4 positionCS:SV_POSITION;float3 world:TEXCOORD0;float2 uv:TEXCOORD1;float fog:TEXCOORD2;half3 normal:TEXCOORD3;half3 rapids:TEXCOORD4;};
            V Vert(A a)
            {
                V o;o.world=TransformObjectToWorld(a.positionOS.xyz);
                float t=_ForestTime*(1-saturate(_Still));
                // Metre-based longitudinal UVs carry small waves downstream. Banks
                // stay pinned; the authored mesh supplies the larger standing fall.
                o.world.y+=_River*a.color.r*.009*sin(a.uv.y*18-t*_FlowSpeed*18+a.uv.x*4)*sin(a.uv.x*PI);
                o.positionCS=TransformWorldToHClip(o.world);
                o.uv=a.uv;o.rapids=a.color.rgb;o.fog=ComputeFogFactor(o.positionCS.z);o.normal=TransformObjectToWorldNormal(a.normalOS);return o;
            }
            float Hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
            bool HasSceneDepth(float raw)
            {
                // A cleared depth pixel is sky / no opaque geometry, never a lake bed.
                #if UNITY_REVERSED_Z
                return raw>0.000001;
                #else
                return raw<0.999999;
                #endif
            }
            float3 SceneWorld(float2 uv,float raw)
            {
                #if !UNITY_REVERSED_Z
                raw=lerp(UNITY_NEAR_CLIP_VALUE,1,raw);
                #endif
                return ComputeWorldSpacePosition(uv,raw,UNITY_MATRIX_I_VP);
            }
            float Noise(float2 p)
            {
                float2 c=floor(p),f=frac(p);f=f*f*(3-2*f);
                return lerp(lerp(Hash(c),Hash(c+float2(1,0)),f.x),lerp(Hash(c+float2(0,1)),Hash(c+1),f.x),f.y);
            }
            float Foam(float2 uv,float t,half3 rapids)
            {
                float downstream=uv.y-t*_FlowSpeed;
                float curl=(Noise(float2(uv.y*1.1,t*.17))-.5)*.8;
                float2 q=float2(uv.x*12+curl,downstream*3.8);
                float broken=Noise(q)*.65+Noise(q*2.17+8)*.35;
                float filaments=smoothstep(.48,.76,broken);
                float fine=smoothstep(.26,.70,Noise(float2(uv.x*48,downstream*23)));
                float crest=pow(saturate(sin(uv.y*13+sin(uv.x*9)*1.2)),9);
                float edges=pow(abs(uv.x*2-1),10)*smoothstep(.35,.70,broken);
                // Green is a stationary downstream rock-wake mask; blue is the
                // falling sheet. Foam texture advects through both without rings.
                return saturate(filaments*(.20+rapids.r*.40)+crest*rapids.r*.16
                    +rapids.g*(.25+filaments*.50)+rapids.b*(.20+filaments*.25)+edges*.48)
                    *lerp(.18,1,fine);
            }
            float Caustic(float2 p,float t)
            {
                float a=SAMPLE_TEXTURE2D(_CausticMap,sampler_CausticMap,p+float2(t*.012,-t*.009)).r;
                float b=SAMPLE_TEXTURE2D(_CausticMap,sampler_CausticMap,p*1.13+float2(-t*.008,t*.011)).r;
                return min(a,b)*2;
            }
            half4 Frag(V i):SV_Target
            {
                float t=_ForestTime*(1-saturate(_Still));
                float2 screen=GetNormalizedScreenSpaceUV(i.positionCS);
                float raw=SampleSceneDepth(screen);
                bool hasBed=HasSceneDepth(raw);
                float3 bed=i.world-float3(0,max(.1,_OpenWaterDepth),0);
                if(hasBed)bed=SceneWorld(screen,raw);
                float waterEye=-TransformWorldToView(i.world).z;
                float eyeGap=hasBed?max(0,-TransformWorldToView(bed).z-waterEye):1000;
                float depth=max(0,i.world.y-bed.y);
                float2 p=i.world.xz;
                float flow=t*_FlowSpeed;
                float waveA=dot(p,float2(5.3,2.7))+flow;
                float waveB=dot(p,float2(-3.1,7.4))-flow*.79;
                float waveC=dot(p,float2(13.7,-8.2))+flow*1.73;
                float2 slopes=float2(cos(waveA)+.42*cos(waveC),.75*cos(waveB)+.28*cos(waveC))*_WaveStrength;
                slopes+=_River*float2(sin(i.uv.x*27+sin(i.uv.y*3-flow*3)),cos(i.uv.y*19-flow*19))*.10;
                if(_River<.5)
                {
                    // Broad crossing ripples carry the reflection; fine ripples remain secondary.
                    float2 q=p*max(.25,_LakeRippleScale);
                    float broadA=dot(q,float2(2.2,.9))+flow*.8;
                    float broadB=dot(q,float2(-1.3,3.1))-flow*.57;
                    float fine=dot(q,float2(8.7,-6.2))+flow*1.1;
                    slopes=(float2(.925,.378)*cos(broadA)
                        +float2(-.387,.922)*cos(broadB)*.48
                        +float2(.814,-.580)*cos(fine)*.10)*_WaveStrength;
                }
                float3 view=GetWorldSpaceNormalizeViewDir(i.world);
                // The imported creek ribbon has downward-wound faces. Water is two-sided,
                // but Fresnel must use the side facing the viewer, including on the fall.
                float3 geometric=normalize(i.normal);
                geometric=dot(geometric,view)<0?-geometric:geometric;
                float3 normal=normalize(geometric+float3(slopes.x,0,slopes.y));
                float2 distort=slopes*_Refraction*saturate(depth*7);
                float2 refractedUV=screen;
                if(hasBed)
                {
                    float2 candidate=screen+distort;
                    if(all(candidate>0) && all(candidate<1))
                    {
                        float displacedRaw=SampleSceneDepth(candidate);
                        if(HasSceneDepth(displacedRaw))
                        {
                            float3 displacedBed=SceneWorld(candidate,displacedRaw);
                            float displacedEye=-TransformWorldToView(displacedBed).z;
                            // Never drag a foreground bank or hero into the water silhouette.
                            bool behindWater=displacedEye>waterEye+.01;
                            bool submerged=_River>.5 || displacedBed.y<i.world.y+.015;
                            if(behindWater && submerged)refractedUV=candidate;
                        }
                    }
                }
                half3 scene=hasBed?SampleSceneColor(refractedUV):_ShallowColor.rgb*.45;
                float absorption=1-exp(-min(depth,12)*max(.01,_Density));
                half3 transmitted=scene*lerp(half3(1,1,1),_ShallowColor.rgb,.32);
                half3 color=lerp(transmitted,_DeepColor.rgb,absorption);
                #if defined(_MAIN_LIGHT_SHADOWS_SCREEN)
                float4 shadowCoord=ComputeScreenPos(TransformWorldToHClip(i.world));
                #else
                float4 shadowCoord=TransformWorldToShadowCoord(i.world);
                #endif
                Light sun=GetMainLight(shadowCoord);
                float fresnel=.035+.965*pow(1-saturate(dot(normal,view)),5);
                half3 sky=lerp(half3(.28,.43,.40),half3(.69,.80,.84),saturate(reflect(-view,normal).y));
                float flatSurface=lerp(1,smoothstep(-.02,.025,i.world.y),_River);
                // A non-mipped reflection has max mip zero. Keep river reflections unchanged.
                float reflectionMip=_River>.5?0:min(max(0,_PlanarReflectionMaxMip),min(3,max(0,_Roughness*_LakeReflectionBlur*12)));
                half3 planar=SAMPLE_TEXTURE2D_LOD(_PlanarReflectionTex,sampler_PlanarReflectionTex,saturate(screen+slopes*lerp(.035,.015,_River)),reflectionMip).rgb;
                half3 reflection=lerp(sky,planar,saturate(_HasReflection*flatSurface));
                float reflectionFloor=_River>.5?.12:_LakeReflectionFloor;
                color=lerp(color,reflection,saturate((fresnel+reflectionFloor)*_ReflectionStrength));
                float3 halfway=SafeNormalize(sun.direction+view);
                float ndh=saturate(dot(normal,halfway));
                float rough=max(.03,_Roughness);float a2=rough*rough*rough*rough;
                float d=(ndh*ndh*(a2-1)+1);
                float spec=min(6,a2/(PI*d*d+.00005))*.025;
                color+=sun.color*spec*sun.shadowAttenuation;
                // Soft broken caustics belong on the sunlit shallow bed, not a bright wire grid.
                float causticBreak=smoothstep(.25,.65,.5+.5*sin(bed.x*1.2+sin(bed.z*.7+t*.13)));
                if(_River<.5 && hasBed && depth>0 && depth<1.5)
                    color+=Caustic(bed.xz*.65,t)*causticBreak*_CausticStrength*half3(.6,.8,.5)
                        *(1-absorption)*saturate(depth*10)*(1-smoothstep(.8,1.5,depth))*sun.shadowAttenuation;
                float edge=(1-smoothstep(.008,.09,eyeGap));
                float breakUp=smoothstep(.0,.8,sin(p.x*29+sin(p.y*19+t)*2)+sin(p.y*31-t*.7));
                float foam=edge*breakUp*lerp(.48,.16,_River);
                if(_River>.5)foam=max(foam,Foam(i.uv,t,i.rapids));
                else
                {
                    // Quiet water has sparse, broad contact patches instead of a dotted white rim.
                    float shorePatches=smoothstep(.62,.82,Noise(p*4.2+float2(t*.018,-t*.012)));
                    foam=hasBed?(1-smoothstep(.012,.11,eyeGap))*shorePatches*_LakeFoamAmount:0;
                    // The fall feeds an off-centre, widening foam plume. It drifts
                    // into the lake instead of expanding as perfectly round rings.
                    float downstream=-p.y-2.58;
                    float center=2.20+downstream*.36+.07*sin(downstream*3);
                    float width=.16+max(0,downstream)*.32;
                    float plume=exp(-pow((p.x-center)/width,2))*smoothstep(-.08,.10,downstream)
                        *(1-smoothstep(.3,1.75,downstream));
                    float curl=sin(downstream*8-t*1.2)*.55;
                    float drifting=Noise(float2((p.x-center)*31+curl,downstream*20-t*3));
                    foam=max(foam,plume*smoothstep(.52,.78,drifting)*.38);
                    // A broken asymmetric boil ties the falling sheet to the lake.
                    float2 impact=float2((p.x-2.20)/.31,(p.y+2.72)/.28);
                    float churn=Noise(impact*5+float2(t*1.4,-t*2.8));
                    float boil=(1-smoothstep(.18,1.18,length(impact)+churn*.22))
                        *(.48+.45*smoothstep(.22,.72,churn));
                    foam=max(foam,boil);
                }
                color=lerp(color,_FoamColor.rgb,saturate(foam));
                float alpha=1;
                if(_River>.5)
                    alpha=(1-smoothstep(5.25,6.35,i.uv.y))*smoothstep(0,.035,i.uv.x)*(1-smoothstep(.965,1,i.uv.x));
                // A 70m lake quad spans the entire fog range. Vertex-clamped fog
                // interpolates a false haze over the foreground; evaluate at the pixel.
                float fog=ComputeFogFactorZ0ToFar(max(waterEye-_ProjectionParams.y,0));
                return half4(MixFog(color,fog),alpha);
            }
            ENDHLSL
        }
    }
}
