shader_type spatial;
render_mode diffuse_burley, specular_schlick_ggx;

uniform vec3 color_a : source_color = vec3(0.04, 0.05, 0.08);
uniform vec3 color_b : source_color = vec3(0.9, 0.92, 0.95);
uniform float checker_scale = 8.0;
uniform float roughness_value : hint_range(0.0, 1.0) = 0.72;
uniform float metallic_value : hint_range(0.0, 1.0) = 0.0;

varying vec3 world_position;

void vertex() {
	world_position = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
}

void fragment() {
	vec3 cell = floor(world_position * checker_scale);
	float checker = mod(cell.x + cell.y + cell.z, 2.0);
	ALBEDO = mix(color_a, color_b, checker);
	ROUGHNESS = roughness_value;
	METALLIC = metallic_value;
}
