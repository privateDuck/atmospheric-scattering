Shader "Hidden/Atmosphere/Skybox" 
{
	Properties 
	{
	    _Tint ("Tint Color", Color) = (.5, .5, .5, .5)
	    [Gamma] _Exposure ("Exposure", Range(0, 8)) = 1.0
	    _Rotation ("Rotation", Range(0, 360)) = 0
	    [NoScaleOffset] _MainTex ("Spherical  (HDR)", 2D) = "grey" {}
    	}

    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
    	Cull Off ZWrite Off

        Pass
        {
            HLSLPROGRAM
	
            #pragma vertex SkyBoxVertex
            #pragma fragment SkyBoxFragment
            
			#include <Include/Atm_Skybox.hlsl>

            ENDHLSL
        }
    }
}
