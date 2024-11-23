#ifndef ATMOSPHEREVARIABLES_INCLUDED
#define ATMOSPHEREVARIABLES_INCLUDED

// Only some variables are shared between the atmosphere shaders and raymarching compute shaders

static const float PI = 3.14159265359;

// View Frustrum Parameters
float3 topLeftDir;
float3 topRightDir;
float3 bottomLeftDir;
float3 bottomRightDir;

// Planet Properties
float atmosphereThickness;
float atmosphereRadius;
float planetRadius;
float3 planetCenter;

// Scattering parameters
float3 rayleighCoefficients;
float3 rayleighAbsorption;
float rayleighDensityAvg;

float mieCoefficient;
float mieAbsorption;
float mieDensityAvg;

float3 ozoneCoeffient;
float3 ozoneAbsorption;
float ozonePeakDensityAltitude;
float ozoneDensityFalloff;

// Ext
float3 dirToSun;
float3 camPos;

#ifdef Transmittance_Compute
RWTexture2D<float3> transmittanceLUT;
#else
sampler2D transmittanceLUT;
#endif

#endif