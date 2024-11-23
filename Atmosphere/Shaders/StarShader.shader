Shader "Hidden/Atmosphere/StarShader"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        Tags 
        { 
            "RenderType"="Transparent"
            "Queue" = "Transparent"
            "IgnoreProjector" = "true" 
        }
        LOD 100//

        Pass
        {
		
	        Cull Back
            Lighting Off
            ZWrite Off
            ZTest Always

            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex StarsVertex
            #pragma fragment StarsFrag
	        #pragma instancing_options procedural:ConfigProcedural

			#include <Include/StarShader.hlsl>
            
            ENDHLSL
        }
    }
}
