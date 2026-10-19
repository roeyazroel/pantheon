// The translucent light of a miracle (spec section 61, ADR-055): a dome or a pillar of light that
// glows most at its rim, where the eye looks along it, so a great dome over a village reads as a
// shining edge rather than a veil over everything. A pillar also fades towards its top. `fade`
// swells it in and out over the miracle's life.
shader_type spatial;
render_mode unshaded, blend_add, cull_disabled, depth_draw_never, shadows_disabled, fog_disabled;

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
	ALBEDO = color;
	ALPHA = strength * fade * mix(0.1, 1.0, edge) * mix(1.0, 1.0 - up, top_fade);
}
