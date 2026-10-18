// Unlit overlay lines draped over the terrain (region boundaries and the hover outline).
// Each vertex is pulled towards the camera by a fraction of its view depth. Its screen position
// stays the same, but it wins the depth test against terrain LOD that sits slightly above the
// true surface, while hills in front still hide it.
shader_type spatial;
render_mode unshaded, blend_mix, cull_disabled, depth_draw_never, shadows_disabled, fog_disabled;

uniform vec4 line_color : source_color = vec4(1.0);
uniform float depth_bias : hint_range(0.0, 0.1) = 0.004;

void vertex() {
	vec4 view_position = MODELVIEW_MATRIX * vec4(VERTEX, 1.0);
	view_position.xyz *= 1.0 - depth_bias;
	POSITION = PROJECTION_MATRIX * view_position;
}

void fragment() {
	ALBEDO = line_color.rgb;
	ALPHA = line_color.a;
}
