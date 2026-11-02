// The light of a miracle (spec section 61, ADR-055, ADR-079): a dome or a pillar that glows most at its
// rim, where the eye looks along it, so a great dome over a village reads as a shining edge rather than
// a veil over everything. Threads of brighter light rise through it, and it melts into the ground at
// its foot. A pillar also fades towards its top. `fade` swells it in and out over the miracle's life.
shader_type spatial;
render_mode unshaded, blend_add, cull_disabled, depth_draw_never, shadows_disabled, fog_disabled;

#include "res://Shaders/world_look.gdshaderinc"

uniform vec3 color : source_color = vec3(1.0);
uniform float strength = 0.6;
uniform float rim = 2.5;
uniform float fade = 0.0;
// The mesh's height and how much it fades towards its top (0 none, 1 to nothing).
uniform float span = 1.0;
uniform float top_fade = 0.0;

varying float up;

void vertex() {
	up = clamp(VERTEX.y / span + 0.5, 0.0, 1.0);
}

void fragment() {
	float edge = pow(1.0 - abs(dot(normalize(NORMAL), normalize(VIEW))), rim);
	float broad = texture(look_clouds, vec2(UV.x * 3.0, UV.y * 0.6 - TIME * 0.22)).g;
	float fine = texture(look_clouds, vec2(UV.x * 7.0 + 0.37, UV.y * 1.4 - TIME * 0.41)).g;
	float threads = smoothstep(0.42, 0.72, broad * 0.6 + fine * 0.4);
	ALBEDO = color * (1.0 + threads * 1.4);
	float body = mix(0.05, 1.0, edge) * (0.38 + threads * 1.5);
	ALPHA = clamp(strength * fade * body * mix(1.0, 1.0 - up, top_fade) * smoothstep(0.0, 0.05, up), 0.0, 1.0);
}
