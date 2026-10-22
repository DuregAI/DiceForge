Shader "Diceforge/Diorama/Soft Foliage"
{
    Properties
    {
        _BaseMap("Leaf pigment", 2D) = "white" {}
        _BaseColor("Pigment tint", Color) = (1,1,1,1)
        _WindStrength("Bend distance", Float) = .025
        _AutoHeight("Automatic tree height (0 uses vertex mask)", Float) = 0
        _Translucency("Leaf backlight", Range(0, .5)) = .12
        _Variation("Pigment variation", Range(0,1)) = .15
        _Saturation("Pigment saturation", Range(0,1)) = .9
        _ToonStrength("Soft tonal shaping", Range(0,1)) = .22
        _Wrap("Diffuse wrap", Range(0,.5)) = .12
        _AmbientStrength("Probe ambient strength", Range(0,2)) = .8
        _Sheen("Broad leaf sheen", Range(0,.15)) = .025
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Cull Off
        ZWrite On
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        #include "Assets/_Project/07_Art/LivingForest/Shaders/LivingWind.hlsl"
        TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            float _WindStrength, _AutoHeight;
            half _Translucency, _Variation, _Saturation, _ToonStrength;
            half _Wrap, _AmbientStrength, _Sheen;
        CBUFFER_END

        struct Attributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
            float2 uv : TEXCOORD0;
            half4 color : COLOR;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };
        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float3 positionWS : TEXCOORD0;
            half3 normalWS : TEXCOORD1;
            float2 uv : TEXCOORD2;
            half fog : TEXCOORD3;
            half variation : TEXCOORD4;
            half3 vertexLighting : TEXCOORD5;
            UNITY_VERTEX_INPUT_INSTANCE_ID
            UNITY_VERTEX_OUTPUT_STEREO
        };
        Varyings LeafVertex(Attributes input)
        {
            Varyings output = (Varyings)0;
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_TRANSFER_INSTANCE_ID(input, output);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
            output.positionWS = ForestWind(input.positionOS.xyz, input.color, _WindStrength, _AutoHeight);
            output.positionCS = TransformWorldToHClip(output.positionWS);
            output.normalWS = TransformObjectToWorldNormal(input.normalOS);
            output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
            output.variation = input.color.g;
            output.fog = ComputeFogFactor(output.positionCS.z);
            #if defined(_ADDITIONAL_LIGHTS_VERTEX)
                output.vertexLighting = VertexLighting(output.positionWS, output.normalWS);
            #endif
            return output;
        }

        // A broad, continuous tonal shoulder preserves round forms and stable moving shadows.
        half3 LeafLight(Light light, half3 normal, half3 view, half3 pigment)
        {
            half ndl = dot(normal, light.direction);
            half diffuse = saturate((ndl + _Wrap) / (1 + _Wrap));
            diffuse = lerp(diffuse, smoothstep(.12h, .88h, diffuse), _ToonStrength);
            half backlight = pow(saturate(-ndl), 1.5h) * _Translucency;
            half3 halfVector = SafeNormalize(light.direction + view);
            half sheen = pow(saturate(dot(normal, halfVector)), 14.0h) * _Sheen * saturate(ndl);
            return (pigment * (diffuse + backlight) + sheen) * light.color
                * light.distanceAttenuation * light.shadowAttenuation;
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForwardOnly" }
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex LeafVertex
            #pragma fragment LeafFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION

            half4 LeafFragment(Varyings input, FRONT_FACE_TYPE face : FRONT_FACE_SEMANTIC) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.normalWS = NormalizeNormalPerPixel(input.normalWS) * IS_FRONT_VFACE(face, 1, -1);
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                #if defined(_MAIN_LIGHT_SHADOWS_SCREEN)
                    inputData.shadowCoord = ComputeScreenPos(TransformWorldToHClip(input.positionWS));
                #else
                    inputData.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                #endif
                half3 pigment = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).rgb * _BaseColor.rgb;
                pigment = lerp(dot(pigment, half3(.2126h,.7152h,.0722h)), pigment, _Saturation);
                pigment *= lerp(1.0h, .88h + .24h * input.variation, _Variation);
                AmbientOcclusionFactor ao = GetScreenSpaceAmbientOcclusion(inputData.normalizedScreenSpaceUV);
                half4 shadowMask = half4(1,1,1,1);
                half3 color = pigment * max(0, SampleSH(inputData.normalWS)) * _AmbientStrength * ao.indirectAmbientOcclusion;
                Light mainLight = GetMainLight(inputData, shadowMask, ao);
                color += LeafLight(mainLight, inputData.normalWS, inputData.viewDirectionWS, pigment);

                #if defined(_ADDITIONAL_LIGHTS)
                    // URP 17.6: Forward+ directional lights are outside LIGHT_LOOP_BEGIN.
                    #if USE_CLUSTER_LIGHT_LOOP
                        [loop] for (uint lightIndex = 0; lightIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); lightIndex++)
                        {
                            CLUSTER_LIGHT_LOOP_SUBTRACTIVE_LIGHT_CHECK
                            Light light = GetAdditionalLight(lightIndex, inputData, shadowMask, ao);
                            color += LeafLight(light, inputData.normalWS, inputData.viewDirectionWS, pigment);
                        }
                    #endif
                    uint pixelLightCount = GetAdditionalLightsCount();
                    LIGHT_LOOP_BEGIN(pixelLightCount)
                        Light light = GetAdditionalLight(lightIndex, inputData, shadowMask, ao);
                        color += LeafLight(light, inputData.normalWS, inputData.viewDirectionWS, pigment);
                    LIGHT_LOOP_END
                #elif defined(_ADDITIONAL_LIGHTS_VERTEX)
                    color += pigment * input.vertexLighting * ao.directAmbientOcclusion;
                #endif
                return half4(MixFog(color, input.fog), 1);
            }
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ColorMask 0
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex LeafShadowVertex
            #pragma fragment EmptyFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            float3 _LightDirection;
            float3 _LightPosition;
            float4 LeafShadowVertex(Attributes input) : SV_POSITION
            {
                UNITY_SETUP_INSTANCE_ID(input);
                float3 world = ForestWind(input.positionOS.xyz, input.color, _WindStrength, _AutoHeight);
                float3 normal = TransformObjectToWorldNormal(input.normalOS);
                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                    float3 direction = normalize(_LightPosition - world);
                #else
                    float3 direction = _LightDirection;
                #endif
                return ApplyShadowClamping(TransformWorldToHClip(ApplyShadowBias(world, normal, direction)));
            }
            half4 EmptyFragment() : SV_Target { return 0; }
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ColorMask R
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex LeafVertex
            #pragma fragment LeafDepthFragment
            #pragma multi_compile_instancing
            half4 LeafDepthFragment(Varyings input) : SV_Target { return 0; }
            ENDHLSL
        }
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormalsOnly" }
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex LeafVertex
            #pragma fragment LeafNormalsFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            half4 LeafNormalsFragment(Varyings input, FRONT_FACE_TYPE face : FRONT_FACE_SEMANTIC) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float3 normal = NormalizeNormalPerPixel(input.normalWS) * IS_FRONT_VFACE(face, 1, -1);
                #if defined(_GBUFFER_NORMALS_OCT)
                    float2 oct = PackNormalOctQuadEncode(normal);
                    return half4(PackFloat2To888(saturate(oct * .5 + .5)), 0);
                #else
                    return half4(normal, 0);
                #endif
            }
            ENDHLSL
        }
    }
    FallBack Off
}
