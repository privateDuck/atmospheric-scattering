#ifndef ATM_FOG_INCLUDED
#define ATM_FOG_INCLUDED

#include "Atm_Variables.hlsl"
#include "Atm_Math.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderVariablesFunctions.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RealtimeLights.hlsl"

struct VertexInput
{
    float4 vertex : POSITION;
    float2 uv : TEXCOORD0;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct VertexOut
{
    float2 uv : TEXCOORD0;
    float3 viewVec : TEXCOORD1;
    float4 vertex : SV_POSITION;
    UNITY_VERTEX_OUTPUT_STEREO
};


sampler2D _MainTex;
float4 _MainTex_TexelSize;

sampler3D aerialLuminance;
sampler3D aerialTransmittance;

float fogStrength;

float GetDepth(float2 uv)
{
    #if UNITY_REVERSED_Z
        return SampleSceneDepth(uv);
    #else
        return lerp(UNITY_NEAR_CLIP_VALUE,1,SampleSceneDepth(uv));
    #endif
}

float3 GetWorldPos(float2 uv)
{
    float depth = GetDepth(uv);
    return ComputeWorldSpacePosition(uv,depth,UNITY_MATRIX_I_VP);
}

VertexOut FogVertex(VertexInput input)
{
    VertexOut output;
    output.vertex = TransformWorldToHClip(input.vertex.xyz);
    output.uv = input.uv;
    return output;
}

half4 FogFragment(VertexOut input) : SV_TARGET
{
    float4 sceneColor = tex2D(_MainTex, input.uv);

    float f = _ProjectionParams.z;
    float n = _ProjectionParams.y;
    float4 zBufferParam = float4((f-n)/n, 1, (f-n)/n*f, 1/f);
    float sceneDepth = GetDepth(input.uv);
    float depth01 = Linear01Depth(sceneDepth,zBufferParam);
	//return depth01;
    float3 camPos = GetCameraPositionWS();
    float3 viewDirection = normalize(GetWorldPos(input.uv) - camPos);

    float depthT = saturate((sceneDepth - n)/(3500 - n));
    float2 hitInfo = raySphere(planetCenter,atmosphereThickness+planetRadius,camPos,viewDirection);
    float dstToAtm = hitInfo.x;
    float dstThrAtm = hitInfo.y;
    float3 luminance = float3(0,0,0);

    if(dstThrAtm > 0 && dstToAtm < sceneDepth)
    {
        float3 inPoint = camPos + viewDirection*dstToAtm;
        float3 outPoint = camPos + viewDirection* min(dstToAtm + dstThrAtm, sceneDepth);

        float3 transmittance = tex3Dlod(aerialTransmittance,float4(input.uv,depth01,0)).rgb;
        luminance = tex3Dlod(aerialLuminance,float4(input.uv,depth01,0)).rgb;

        luminance += sceneColor.rgb * transmittance;
    }
    
    half3 finalCol = sceneColor.rgb + fogStrength * luminance;
    return half4(finalCol,1.0);
}
#endif