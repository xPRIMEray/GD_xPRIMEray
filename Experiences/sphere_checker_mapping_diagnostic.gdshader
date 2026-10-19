shader_type spatial;
render_mode diffuse_burley, specular_schlick_ggx;

// Diagnostic presentation only. UV cells are parameter-space cells; they are
// neither equal-area nor a globally uniform angular ruler on a sphere.
uniform int mapping_mode : hint_enum("UV Space", "Centered Object Space") = 0;
uniform vec3 color_a : source_color = vec3(0.929412, 0.980392, 1.0);
uniform vec3 color_b : source_color = vec3(0.086275, 0.545098, 0.819608);
uniform vec2 uv_cells = vec2(16.0, 8.0);
uniform float object_checker_scale = 1.4;
uniform float roughness_value : hint_range(0.0, 1.0) = 0.72;
uniform float metallic_value : hint_range(0.0, 1.0) = 0.0;

varying vec3 object_position;

void vertex() {
	object_position = VERTEX;
}

void fragment() {
	float checker;
	if (mapping_mode == 0) {
		vec2 cell = floor(UV * uv_cells);
		checker = mod(cell.x + cell.y, 2.0);
	} else {
		// The half-cell offset centers the middle Cartesian cell on the mesh origin.
		vec3 cell = floor(object_position * object_checker_scale + vec3(0.5));
		checker = mod(cell.x + cell.y + cell.z, 2.0);
	}
	ALBEDO = mix(color_a, color_b, checker);
	ROUGHNESS = roughness_value;
	METALLIC = metallic_value;
}
