shader_type spatial;

// distort view ray before sampling the scene
vec3 warp_ray(vec3 ray, float beta, float gamma) {
    float r = length(ray);
    float k = pow(r, gamma) * beta;
    ray += k * normalize(ray);
    return normalize(ray);
}

void vertex() {
	// Called for every vertex the material is visible on.
}

void fragment() {
	// Called for every pixel the material is visible on.
}

//void light() {
//	// Called for every pixel for every light affecting the material.
//	// Uncomment to replace the default light processing function with this one.
//}
