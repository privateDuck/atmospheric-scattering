// Thanks to: https://sebh.github.io/publications/egsr2020.pdf
#include "Atm_Variables.hlsl"
#include "Atm_Math.hlsl"
// All functions that take a ray position as parameter expects the position to be relative to the world origin

struct ScatteringParameters {
	float3 rayleigh;
	float mie;
	float3 extinction;
};

struct ScatteringResult {
	float3 luminance;
	float3 transmittance;
};

float3 calculateViewDir(float2 texCoord) {
	float3 a = lerp(topLeftDir, topRightDir, texCoord.x);
	float3 b = lerp(bottomLeftDir, bottomRightDir, texCoord.x);
	float3 viewVector = lerp(a, b, texCoord.y);
	return normalize(viewVector);
}
/* 
ScatteringParameters getScatteringValues(float3 rayPos) {
	ScatteringParameters scattering;

	// height from the surface
	float height = length(rayPos - planetCenter) - planetRadius;
	float height01 = saturate(height / atmosphereThickness);

	float rayleighDensity = exp(-height01 / rayleighDensityAvg);
	float mieDensity = exp(-height01 / mieDensityAvg);
	float ozoneDensity = saturate(1 - abs(ozonePeakDensityAltitude - height01) * ozoneDensityFalloff);

	float mie = mieCoefficient * mieDensity;
	float3 rayleigh = rayleighCoefficients * rayleighDensity;

	scattering.mie = mie;
	scattering.rayleigh = rayleigh;
	
	scattering.extinction = mieDensity * (mieCoefficient + mieAbsorption) + rayleighCoefficients * rayleighDensity + ozoneAbsorption * ozoneDensity;
	return scattering;
}
 */
float3 GetDensities(float3 pos)
{
	float height = length(pos - planetCenter) - planetRadius;
	float height01 = saturate(height / atmosphereThickness);

	float densityR = exp(-height01/rayleighDensityAvg);
	float densityM = exp(-height01/mieDensityAvg);
	float densityO = max(0, 1 - abs(height01 - ozonePeakDensityAltitude) / ozoneDensityFalloff);

	return float3(densityR,densityM,densityO);
}

// Thanks to https://www.shadertoy.com/view/slSXRW
float getMiePhase(float cosTheta) {
	const float g = 0.8;
	const float scale = 3.0/(8.0*PI);
	
	float num = (1.0-g*g)*(1.0+cosTheta*cosTheta);
	float denom = (2.0+g*g)*pow(abs(1.0 + g*g - 2.0*g*cosTheta), 1.5);
	
	return scale*num/denom;
}

float getRayleighPhase(float cosTheta) {
	const float k = 3.0/(16.0*PI);
	return k*(1.0+cosTheta*cosTheta);
}

float3 getSunTransmittanceLUT(float3 pos, float3 dir) {
	float dstFromCentre = length(pos - planetCenter);
	float height = dstFromCentre - planetRadius;
	float height01 = saturate(height / atmosphereThickness);

	float uvX = 1 - (dot((pos - planetCenter) / dstFromCentre, dir) * 0.5 + 0.5);
	return tex2Dlod(transmittanceLUT, float4(uvX, height01, 0, 0)).rgb;
}

ScatteringResult raymarch(float3 rayPos, float3 rayDir, float rayLength, int numSteps, float earthShadowRadius) {
	float3 luminance = 0;
	float3 transmittance = 1;

	float stepSize = rayLength / numSteps;
	float scaledStepSize = stepSize / atmosphereThickness;

	float cosTheta = dot(rayDir, dirToSun);
	//float rayleighPhaseValue = getRayleighPhase(-cosTheta);
	float rayleighPhaseValue = 1;
	float miePhase = getMiePhase(cosTheta);

	// Step through the atmosphere
	for (int stepIndex = 0; stepIndex < numSteps; stepIndex ++) {
		
		// At each step, light travelling from the sun may be scattered into the path toward the camera (in scattering)
		// Some of this in-scattered light may be scattered away as it travels toward the camera (out scattering)
		// Some light may also previously have been out-scattered while travelling through the atmosphere from the sun

		//ScatteringParameters scattering = getScatteringValues(rayPos);

		float3 densities = GetDensities(rayPos);
		float3 rayleighExtinction = densities.x * (rayleighCoefficients + rayleighAbsorption);
		float3 mieExtinction = densities.y * (mieCoefficient + mieAbsorption);
		float3 ozoneExtinction = densities.z * (ozoneCoeffient + ozoneAbsorption);

		float3 extinction = rayleighExtinction + mieExtinction + ozoneExtinction;
		// The proportion of light transmitted along the ray from the current sample point to the previous one
		float3 sampleTransmittance = exp(-extinction * scaledStepSize);
		
		// The proportion of light that reaches this point from the sun
		// float3 sunTransmittance = getSunTransmittance(rayPos, dirToSun);
		float3 sunTransmittance = getSunTransmittanceLUT(rayPos, dirToSun);

		// Earth shadow
		if (rayIntersectSphere(planetCenter, rayPos, dirToSun, earthShadowRadius) > 0) {
			sunTransmittance = 0;
		}


		// Amount of light scattered in towards the camera at current sample point
		float3 inScattering = (rayleighExtinction * rayleighPhaseValue + mieExtinction * miePhase) * sunTransmittance;

		// Increase the luminance by the in-scattered light
		// Note, the simple way of doing that would be like this: luminance += inScattering * transmittance * scaledStepSize;
		// The two lines below do essentially the same thing, but converge quicker with lower step counts
		// inScattering * (1 - transmittance) / extinction;
		float3 scatteringIntegral = (inScattering - inScattering * sampleTransmittance) / max(0.0001, extinction);
		luminance += scatteringIntegral*transmittance;
		

		// Update the transmittance along the ray from the current point in the atmosphere back to the camera
		transmittance *= sampleTransmittance;

		// Move to next sample point along ray
		rayPos += rayDir * stepSize;
	}
	
	ScatteringResult result;
	result.luminance = luminance;
	result.transmittance = transmittance;
	return result;
}
