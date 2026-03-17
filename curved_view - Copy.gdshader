shader_type canvas_item;

uniform sampler2D SCREEN_TEXTURE : hint_screen_texture, filter_linear_mipmap;
uniform float beta  : hint_range(-2.0, 2.0) = 0.0;
uniform float gamma : hint_range(0.0, 8.0)  = 2.0;
uniform float warp_scale : hint_range(0.0, 1.0) = 0.4;

void fragment() {
    // Base UV for current pixel
    vec2 uv = SCREEN_UV;

    // Direction from screen center
    vec2 d = uv - vec2(0.5);

    float r = length(d);
    // Curvature factor: r^gamma * beta
    float k = pow(r, gamma) * beta;

    vec2 warped_uv = uv + normalize(d) * k * warp_scale;

    // Optional: clamp to avoid sampling outside the screen
    warped_uv = clamp(warped_uv, vec2(0.0), vec2(1.0));

    COLOR = texture(SCREEN_TEXTURE, warped_uv);
}