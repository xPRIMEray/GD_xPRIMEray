shader_type spatial;
render_mode unshaded, cull_disabled;

uniform sampler2D portal_tex : source_color;
uniform float distortion_strength : hint_range(0.0, 0.35) = 0.075;

void fragment() {
	vec2 center = vec2(0.5, 0.5);
	vec2 delta = UV - center;
	float radius = length(delta) * 2.0;
	float rim = smoothstep(0.18, 1.0, radius);
	float warp = 1.0 + (distortion_strength * rim * radius * radius);
	vec2 warped_uv = center + (delta / warp);
	vec3 color = texture(portal_tex, clamp(warped_uv, vec2(0.0), vec2(1.0))).rgb;
	ALBEDO = color;
	EMISSION = color * 0.9;
}
