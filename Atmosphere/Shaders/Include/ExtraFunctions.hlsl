#ifndef EXTRAFUNCS_DEFINED 
#define EXTRAFUNCS_DEFINED

static const float rcpPI = 0.31830988618f;

// Returns (Phi, Theta)
float2 ViewDirToAngles(float3 dir)
{
    float phi = acos(dir.y);
    //dir = reflect(dir,float3(1,0,0));
    float theta = atan2(dir.z,dir.x);
    return float2(phi, theta);
}

// Returns (u, v)
float2 AnglesToUV(float phi,float theta)
{
    float u = 0.5 - theta*0.5f*rcpPI;
    float v = 1.0 - phi*rcpPI;
    return float2(u, v);
}

void ViewDirToAngles_float(float3 dir, out float2 angles)
{
    float phi = acos(dir.y);
    float theta = acos(dir.x/acos(phi));
    angles = float2(phi, theta);
}

void AnglesToUV_float(float phi,float theta, out float2 uv)
{
    float u = theta*0.5f*rcpPI;
    float v = 0.5f - phi*rcpPI;
    uv = float2(u, v);
}

void ViewDirToUV_float(float3 dir, out float2 uv)
{
    float2 angles = ViewDirToAngles(dir);
    uv = AnglesToUV(angles.x,angles.y);
}

float2 ViewDirToUV(float3 dir)
{
    float2 angles = ViewDirToAngles(dir);
    float2 uv = AnglesToUV(angles.x,angles.y);
    return uv;
}

float RemapToTriCoord(float v)
{
    float r2 = 0.5f*v;
    float f1 = sqrt(r2);
    float f2 = 1.0 - sqrt(r2 - 0.25);
    return (v < 0.5) ? f1 : f2;
}

half3 GetBlueNoise(float2 uv, sampler2D blueNoise)
{
    uv = (uv);// * blueNoise_TexelSize.xy;
    half3 noise = tex2D(blueNoise,uv).rgb;
    float3 m = (float3)0;
    m.x = RemapToTriCoord(noise.x);
    m.y = RemapToTriCoord(noise.y);
    m.z = RemapToTriCoord(noise.z);
    return (m * 2.0) - 0.5;
}

half3 Dithering(float2 uv, sampler2D blueNoise, float ditherStrength)
{
    half3 weightedNoise = GetBlueNoise(uv, blueNoise) / 255.0 * ditherStrength;
    return weightedNoise;
}
#endif