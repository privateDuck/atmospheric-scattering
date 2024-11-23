Shader "Hidden/Atmosphere/Fog" 
{
	Properties 
	{
	     [NoScaleOffset] _MainTex ("Tex", 2D) = "grey" {}
    	}

    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
    	Cull Off ZWrite Off

        Pass
        {
            HLSLPROGRAM
	
            #pragma vertex FogVertex
            #pragma fragment FogFragment
            
			#include <Include/Atm_Fog.hlsl>

            ENDHLSL
        }
    }
}
