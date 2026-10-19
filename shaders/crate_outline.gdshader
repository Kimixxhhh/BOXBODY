shader_type canvas_item;
render_mode unshaded;

uniform sampler2D mask_texture : filter_nearest, repeat_disable;
uniform vec2 pixel_size;
uniform float outline_width = 2.5;

void fragment() {
    vec4 center = texture(mask_texture, UV);
    float edge = 0.0;
    float halo = 0.0;
    float interior = 1.0;
    for (int i = 0; i < 16; i++) {
        float a = float(i) * 6.2831853 / 16.0;
        vec2 dir = vec2(cos(a), sin(a)) * pixel_size;
        edge = max(edge, texture(mask_texture, UV + dir * outline_width).r);
        halo = max(halo, texture(mask_texture, UV + dir * (outline_width + 2.0)).r);
        interior = min(interior, texture(mask_texture, UV + dir * 1.25).r);
    }
    // The narrow inner rim stays readable even against the requested white floor.
    float outer_rim = max(edge, halo * 0.18) * (1.0 - center.r);
    float inner_rim = center.r * (1.0 - interior);
    float alpha = max(outer_rim, inner_rim) * (1.0 - center.g);
    COLOR = vec4(vec3(1.0), alpha);
}
