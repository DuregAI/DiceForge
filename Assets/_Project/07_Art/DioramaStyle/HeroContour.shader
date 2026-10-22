Shader "Diceforge/Diorama/Hero Contour"
{
    Properties
    {
        _ContourColor("Warm silhouette color", Color) = (0.12, 0.075, 0.035, 1)
        _ContourWidthPixels("Silhouette width (render pixels)", Range(0, 3)) = 0.7
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry+1" }
        Pass
        {
            Name "HeroSilhouette"
            Tags { "LightMode"="UniversalForwardOnly" }
            Cull Front
            ZWrite Off
            ZTest LEqual

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex ContourVertex
            #pragma fragment ContourFragment
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _ContourColor;
                float _ContourWidthPixels;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings ContourVertex(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                Varyings output;
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                float4 positionCS = TransformWorldToHClip(positionWS);
                float4 normalCS = mul(GetWorldToHClipMatrix(), float4(normalWS, 0));

                // Derivative of perspective division also works for orthographic cameras.
                // Normalize in pixel space, so the width does not change with aspect or distance.
                float2 screenSize = max(GetScaledScreenParams().xy, float2(1, 1));
                float2 pixelDirection = (normalCS.xy * positionCS.w - positionCS.xy * normalCS.w) * screenSize;
                float lengthSquared = dot(pixelDirection, pixelDirection);
                pixelDirection *= rsqrt(max(lengthSquared, 1e-8));
                positionCS.xy += pixelDirection * (2.0 * _ContourWidthPixels / screenSize) * positionCS.w;

                output.positionCS = positionCS;
                return output;
            }

            half4 ContourFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                return half4(_ContourColor.rgb, 1);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
