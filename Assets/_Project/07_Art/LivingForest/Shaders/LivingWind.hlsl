#ifndef LIVING_FOREST_WIND
#define LIVING_FOREST_WIND
float _ForestTime;
float _ForestWindStrength;
float _ForestMotion;
float3 ForestWind(float3 positionOS, half4 vertexColor, float strength, float autoHeight)
{
    float3 origin = TransformObjectToWorld(float3(0,0,0));
    float weight = autoHeight > .001 ? pow(saturate(positionOS.y / autoHeight),1.8) : vertexColor.r;
    float phase = origin.x * .71 + origin.z * .93;
    float t = _ForestTime;
    float gust = .65 + .35 * sin(t * .31 + phase * .2);
    float sway = sin(t * 1.15 + phase) + .25 * sin(t * 2.37 + positionOS.y * 2.7 + positionOS.z * 3.1 + positionOS.x * 3.7 + phase);
    float3 world = TransformObjectToWorld(positionOS);
    world.xz += float2(.86,.43) * (sway * gust * weight * strength * _ForestWindStrength * _ForestMotion);
    world.y -= abs(sway) * weight * strength * .055 * _ForestMotion;
    return world;
}
#endif
