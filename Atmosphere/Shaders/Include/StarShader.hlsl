#ifndef STARS_SHADER_INCLUDED
#define STARS_SHADER_INCLUDED

#include <ExtraFunctions.hlsl>
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderVariablesFunctions.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RealtimeLights.hlsl"

struct Star
{
    float3 position;
    float3 color;
    float size;
};

struct VertexInput
{
    float4 position : POSITION;
    float2 texcoord : TEXCOORD0;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct VertexOut
{
    float4 positionCS : POSITION;
    float2 uv : TEXCOORD0;
    float3 positionWS : TEXCOORD1;
    float3 color : TEXCOORD2;
    UNITY_VERTEX_OUTPUT_STEREO
};

StructuredBuffer<Star> stars;
sampler2D _MainTex;
sampler2D _Sky;
float starBlendFactor;
float alphaClipThreashold;

void ConfigProcedural()
{
    
}

float GetDepth(float2 uv)
{
    #if UNITY_REVERSED_Z
        return SampleSceneDepth(uv);
    #else
        return lerp(UNITY_NEAR_CLIP_VALUE,1,SampleSceneDepth(uv));
    #endif
}

VertexOut StarsVertex(VertexInput input, uint id : SV_InstanceID)
{
    VertexOut output;
    Star star = stars[id];
    float3 position = star.position + GetCameraPositionWS();
    output.positionWS = position;
    output.positionCS = mul(UNITY_MATRIX_P,mul(UNITY_MATRIX_V,float4(position,1.0f)) + float4(input.position.xyz,0.0f)*star.size);
    //output.positionCS = TransformWorldToHClip(input.position.xyz + star.position);
    output.uv = input.texcoord;
    output.color = star.color;
    return output;
}

half4 StarsFrag(VertexOut input) : COLOR
{
    float2 ssuv = GetNormalizedScreenSpaceUV(input.positionCS);
    float f = _ProjectionParams.z;
    float n = _ProjectionParams.y;
    float4 zBufferParam = float4((f-n)/n, 1, (f-n)/n*f, 1/f);
    float sceneDepth = GetDepth(ssuv);
    float depth01 = Linear01Depth(sceneDepth,zBufferParam);
    
    //if(depth01 < 1.0f) return half4(0,0,0,0);
    clip(depth01 - 0.99);
    
    half4 col = tex2D(_MainTex,input.uv);
    float3 viewDir = -GetWorldSpaceNormalizeViewDir(input.positionWS);
    float2 SkySampleUV = ViewDirToUV(viewDir);
    float4 skyCol = tex2D(_Sky,SkySampleUV);
    float skyBrightness = max(skyCol.r,max(skyCol.g,skyCol.b));//0.21*skyCol.r + 0.72*skyCol.g + 0.07*skyCol.b;
    float alphaFactor = 1 - saturate(skyBrightness*starBlendFactor);

    

    half4 finalCol = half4(input.color,alphaFactor*col.a);
    return finalCol;
}

VertexOut StarsOpaqueVertex(VertexInput input, uint id : SV_InstanceID)
{
    VertexOut output;
    Star star = stars[id];
    float3 position = star.position + GetCameraPositionWS();
    output.positionWS = position;

    float3 viewDir = -GetWorldSpaceNormalizeViewDir(position);
    float2 SkySampleUV = ViewDirToUV(viewDir);
    float4 skyCol = tex2Dlod(_Sky,float4(SkySampleUV,0,0));
    float skyBrightness = max(skyCol.r,max(skyCol.g,skyCol.b));//0.21*skyCol.r + 0.72*skyCol.g + 0.07*skyCol.b;
    float alphaFactor = 1 - saturate(skyBrightness*starBlendFactor);

    float blendSize = star.size * alphaFactor;

    output.positionCS = mul(UNITY_MATRIX_P,mul(UNITY_MATRIX_V,float4(position,1.0f)) + float4(input.position.xyz,0.0f)*blendSize);
    //output.positionCS = TransformWorldToHClip(input.position.xyz + star.position);
    output.uv = input.texcoord;
    output.color = star.color;
    return output;
}

half4 StarsOpaqueFrag(VertexOut input) : COLOR
{
    float2 ssuv = GetNormalizedScreenSpaceUV(input.positionCS);
    float f = _ProjectionParams.z;
    float n = _ProjectionParams.y;
    float4 zBufferParam = float4((f-n)/n, 1, (f-n)/n*f, 1/f);
    float sceneDepth = GetDepth(ssuv);
    float depth01 = Linear01Depth(sceneDepth,zBufferParam);
    
    //if(depth01 < 1.0f) return half4(0,0,0,0);
    //clip(depth01 - 0.99);
    
    half4 col = tex2D(_MainTex,input.uv);

    clip(col.a - 0.2);
    half4 finalCol = half4(input.color,1);
    return finalCol;
}
#endif