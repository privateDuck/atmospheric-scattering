#ifndef ATM_TRANSMITTANCE_FUNCTIONS_INCLUDED
#define ATM_TRANSMITTANCE_FUNCTIONS_INCLUDED

#include <Atm_Variables.hlsl>
#include "Atm_Math.hlsl"

float3 GetExtinction(float3 rayPos)
{

	// height from the surface
	float height = length(rayPos) - planetRadius;
	float height01 = saturate(height / atmosphereThickness);

	float densityR = exp(-height01 / rayleighDensityAvg);
	float densityM = exp(-height01 / mieDensityAvg);
	float densityO = max(0, 1 - abs(height01 - ozonePeakDensityAltitude) / ozoneDensityFalloff);

	float3 rayleighExtinction = densityR * (rayleighCoefficients + rayleighAbsorption);
	float3 mieExtinction = densityM * (mieCoefficient + mieAbsorption);
	float3 ozoneExtinction = densityO * (ozoneCoeffient + ozoneAbsorption);

	float3 extinction = rayleighExtinction + mieExtinction + ozoneExtinction;
	return extinction;
}

#define SunRayMarchSteps 40
float3 getSunTransmittance(float3 pos, float rayLength, float3 sunDir)
{

	float stepSize = rayLength / SunRayMarchSteps;
	//float3 transmittance = float3(1, 1, 1);
	float3 opticalDepth = 0;

	[unroll(SunRayMarchSteps)] 
	for (int i = 0; i < SunRayMarchSteps; i++)
	{
		pos += sunDir * stepSize;
		float3 extinction = GetExtinction(pos);

		//transmittance *= exp(-extinction / atmosphereThickness * stepSize);
		opticalDepth += extinction;
	}
	return exp(-(opticalDepth / atmosphereThickness * stepSize));
}

#endif