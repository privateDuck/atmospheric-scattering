#ifndef SKYBOXSHADER_INCLUDED
#define SKYBOXSHADER_INCLUDED

static float PI = 3.14159265359;
// Based on Unity's built in panoramic skybox shader
// Unity built-in shader source. Copyright (c) 2016 Unity Technologies. MIT license

// #include "UnityCG.cginc"

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderVariablesFunctions.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RealtimeLights.hlsl"
#include "ExtraFunctions.hlsl"

sampler2D _MainTex;
sampler2D blueNoise;
sampler2D transmittanceLUT;

float4 _MainTex_TexelSize;
half4 _MainTex_HDR;
half4 _Tint;
half _Exposure;
float _Rotation;
float ditherStrength;

float3 planetCenter;
float planetRadius;
float atmosphereThickness;
uniform float sunDiscSize;
uniform float sunDiscBlurA;
uniform float sunDiscBlurB;
float4 nightColor;
float nightColorWeight;

struct VertexInput
{
    float4 vertex : POSITION;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct VertexOut
{
    float4 vertex : SV_POSITION;
    float3 texcoord : TEXCOORD0;
    float3 positionWS : TEXCOORD1;
    float3 eyeRay : TEXCOORD2;
    UNITY_VERTEX_OUTPUT_STEREO
};

inline half3 DecodeHDR(half4 data, half4 decodeInstructions, int colorspaceIsGamma)
{
    // Take into account texture alpha if decodeInstructions.w is true(the alpha value affects the RGB channels)
    half alpha = decodeInstructions.w * (data.a - 1.0) + 1.0;

    // If Linear mode is not supported we can skip exponent part
    if (colorspaceIsGamma)
        return (decodeInstructions.x * alpha) * data.rgb;

    return (decodeInstructions.x * pow(alpha, decodeInstructions.y)) * data.rgb;
}

// Decodes HDR textures
// handles dLDR, RGBM formats
inline half3 DecodeHDR(half4 data, half4 decodeInstructions)
{
#if defined(UNITY_COLORSPACE_GAMMA)
    return DecodeHDR(data, decodeInstructions, 1);
#else
    return DecodeHDR(data, decodeInstructions, 0);
#endif
}

float2 ToRadialCoords(float3 coords)
{
    float3 normalizedCoords = normalize(coords);
    float latitude = acos(normalizedCoords.y);
    float longitude = atan2(normalizedCoords.z, normalizedCoords.x);
    float2 sphereCoords = float2(longitude, latitude) * float2(0.5 / PI, 1.0 / PI);
    sphereCoords.x = 0.5 - sphereCoords.x;
    sphereCoords.y = 1.0 - sphereCoords.y;
    return sphereCoords;
}

float3 RotateAroundYInDegrees(float3 vertex, float degrees)
{
    float alpha = degrees * PI / 180.0;
    float sina, cosa;
    sincos(alpha, sina, cosa);
    float2x2 m = float2x2(cosa, -sina, sina, cosa);
    return float3(mul(m, vertex.xz), vertex.y).xzy;
}

float3 sampleSunTransmittanceLUT(float3 pos, float3 dir)
{
    float dstFromCentre = length(pos - planetCenter);
    float height = dstFromCentre - planetRadius;
    float height01 = saturate(height / atmosphereThickness);

    float uvX = 1 - (dot((pos - planetCenter) / dstFromCentre, dir) * 0.5 + 0.5);
    return tex2Dlod(transmittanceLUT, float4(uvX, height01, 0, 0)).rgb;
}

float3 sunDiscBloom(float3 rayDir, float3 sunDir)
{
    const float sunSolidAngle = sunDiscSize * PI / 180.0;
    const float minSunCos = cos(sunSolidAngle);

    float cosTheta = dot(rayDir, sunDir);

    // rayHit sun area
    if (cosTheta > minSunCos)
        return 2.0;

    float offset = minSunCos - cosTheta;
    float bloom = exp(-offset * 1000 * sunDiscBlurA) * 0.5;
    //float invBloom = 1.0 / (0.02 + offset * 100 * sunDiscBlurB) * 0.01;
    return bloom; // + invBloom;
}

VertexOut SkyBoxVertex(VertexInput input)
{
    VertexOut output;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
    float3 rotated = RotateAroundYInDegrees(input.vertex.xyz, _Rotation);
    float3 posWS = TransformObjectToWorld(rotated);

    output.vertex = TransformWorldToHClip(posWS);
    output.texcoord = input.vertex.xyz;
    float3 eyeRay = normalize(mul((float3x3)unity_ObjectToWorld, input.vertex.xyz));
    output.eyeRay = eyeRay;

    output.positionWS = posWS;
    return output;
}

half4 SkyBoxFragment(VertexOut input) : SV_TARGET
{
    float3 camPos = GetCameraPositionWS();
    float3 viewDirection = normalize(input.positionWS - camPos); // normalize(input.eyeRay.xyz);
    float2 radialUV = ViewDirToUV(viewDirection);

    float2 tc = ToRadialCoords(viewDirection);

    half4 tex = tex2D(_MainTex, radialUV);

    // Light mainLight = GetMainLight();
    float3 dirToSun = _MainLightPosition.xyz;

    float3 sun = sunDiscBloom(viewDirection, dirToSun);
    float3 transmittance = sampleSunTransmittanceLUT(camPos, viewDirection);
    float skyBrightness = max(tex.r, max(tex.g, tex.b));
    skyBrightness = saturate(skyBrightness * 5);

    tex.rgb += Dithering(ToRadialCoords(input.texcoord * 2), blueNoise, ditherStrength) * skyBrightness;
    tex += float4(sun * transmittance, 1.0);

    half3 c = DecodeHDR(tex, _MainTex_HDR);
    // c	+= float4(sun*transmittance,1.0);
    // c = c * _Tint.rgb * unity_ColorSpaceDouble.rgb;
    c *= _Exposure;

    float3 localY = normalize(camPos - planetCenter);
    float sunDivergence = saturate(dot(-localY, dirToSun));
    float viewConvergence = saturate(dot(localY, viewDirection) + 0.5f);
    // sky[id.xy] = float4(1 - saturate(col.r*20),0,0, 1);
    float nightColorFactor = sunDivergence * viewConvergence;
    float3 finalCol = c + nightColor.rgb * nightColorWeight * nightColorFactor;
    return half4(finalCol, 1);
}

#endif