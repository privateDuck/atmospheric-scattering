#ifndef ATM_MATH_INCLUDED
#define ATM_MATH_INCLUDED

//static const float PI = 3.14159265359;
// Returns vector (dstToSphere, dstThroughSphere)
// If ray origin is inside sphere, dstToSphere = 0
// If ray misses sphere, dstToSphere = infinity; dstThroughSphere = 0
float2 raySphere(float3 sphereCentre, float sphereRadius, float3 rayOrigin, float3 rayDir) {
	float3 offset = rayOrigin - sphereCentre;
	float a = 1; // Set to dot(rayDir, rayDir) if rayDir might not be normalized
	float b = 2 * dot(offset, rayDir);
	float c = dot (offset, offset) - sphereRadius * sphereRadius;
	float d = b * b - 4 * a * c; // Discriminant from quadratic formula

	// Number of intersections: 0 when d < 0; 1 when d = 0; 2 when d > 0
	if (d > 0) {
		float s = sqrt(d);
		float dstToSphereNear = max(0, (-b - s) / (2 * a));
		float dstToSphereFar = (-b + s) / (2 * a);

		// Ignore intersections that occur behind the ray
		if (dstToSphereFar >= 0) {
			return float2(dstToSphereNear, dstToSphereFar - dstToSphereNear);
		}
	}
	// Ray did not intersect sphere
	return float2(1.#INF, 0);
}

// From https://gamedev.stackexchange.com/questions/96459/fast-ray-sphere-collision-code.
// Returns dst to intersection of ray and sphere (works for point inside or outside of sphere)
// Returns -1 if ray does not intersect sphere
float rayIntersectSphere(float3 sphereCenter, float3 rayPos, float3 rayDir, float radius) {
	float3 m = rayPos - sphereCenter;
	float b = dot(m, rayDir);
	float c = dot(m, m) - radius * radius;
	if (c > 0 && b > 0) {
		return -1;
	}

	float discr = b * b - c;
	if (discr < 0) {
		return -1;
	}
	// Special case: inside sphere, use far discriminant
	if (discr > b * b) {
		return (-b + sqrt(discr));
	}
	return -b - sqrt(discr);
}

#endif