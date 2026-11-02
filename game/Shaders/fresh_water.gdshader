// Rivers, lakes and floods (ADR-022, ADR-077). The vertex colour's red channel is the water's depth
// over the ground (0 at the bank, 1 at WaterSurfaceMesh.DeepestDrawn), so the shallows are clear and
// the middle dark, on every renderer, and the water fades out at its banks, which hides the shape of
// the mesh's cells. Green and blue hold which way and how fast it flows: ripples and streaks of foam
// are carried along by two phases of a flow map, each hidden while it restarts.
shader_type spatial;
render_mode blend_mix, depth_draw_opaque, cull_back, diffuse_burley, specular_schlick_ggx, shadows_disabled;

#include "res://Shaders/world_look.gdshaderinc"

uniform vec4 shallow_color : source_color = vec4(0.30, 0.46, 0.38, 1.0);
uniform vec4 deep_color : source_color = vec4(0.05, 0.20, 0.23, 1.0);
uniform vec4 foam_color : source_color = vec4(0.90, 0.93, 0.90, 1.0);
// Slope in red and green, foam lace in blue (WaterTextures).
uniform sampler2D ripples : hint_default_black, filter_linear_mipmap_anisotropic, repeat_enable;
uniform float shallow_alpha : hint_range(0.0, 1.0) = 0.42;
uniform float deep_alpha : hint_range(0.0, 1.0) = 0.93;
// Floods are muddy and do not foam.
uniform float muddiness : hint_range(0.0, 1.0) = 0.0;
uniform vec4 mud_color : source_color = vec4(0.42, 0.36, 0.24, 1.0);
uniform float wave_scale = 0.055;
uniform float wave_speed = 0.32;
uniform float wave_strength : hint_range(0.0, 2.0) = 0.55;
// How far the ripples are carried over one phase, in the ripples' own units.
uniform float flow_reach = 0.16;

varying vec3 world_position;
varying float depth;
varying vec2 flow;

void vertex() {
	world_position = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
	depth = COLOR.r;
	flow = COLOR.gb * 2.0 - 1.0;
}

// The ripples carried along by the flow: two phases, half a cycle apart.
vec4 carried(vec2 p, vec2 along, float t) {
	float phase = fract(t);
	float other = fract(t + 0.5);
	vec4 a = texture(ripples, p - along * (phase - 0.5) * flow_reach);
	vec4 b = texture(ripples, p + vec2(0.37, 0.19) - along * (other - 0.5) * flow_reach);
	return mix(a, b, abs(phase * 2.0 - 1.0));
}

void fragment() {
	float t = TIME * wave_speed;
	vec2 p = world_position.xz * wave_scale;
	float speed = length(flow);
	vec4 near = carried(p, flow, t);
	vec4 fine = carried(p * 3.1, flow * 3.1, t * 1.3);
	vec2 slope = ((near.rg - 0.5) + (fine.rg - 0.5) * 0.6) * wave_strength;
	vec3 world_normal = normalize(vec3(-slope.x, 1.0, -slope.y));
	NORMAL = normalize((VIEW_MATRIX * vec4(world_normal, 0.0)).xyz);

	vec3 water = mix(shallow_color.rgb, deep_color.rgb, smoothstep(0.0, 0.8, depth));
	water = mix(water, mud_color.rgb, muddiness);
	// Streaks where the water runs fast over the shallows, a thin line along its edge.
	float streaks = smoothstep(0.5, 0.72, near.b * 0.5 + fine.b * 0.5) * smoothstep(0.25, 0.8, speed) * (1.0 - smoothstep(0.25, 0.8, depth));
	float edge = (1.0 - smoothstep(0.06, 0.16, depth)) * smoothstep(0.3, 0.6, near.b) * 0.5;
	float foam = clamp(streaks + edge, 0.0, 1.0) * (1.0 - muddiness);
	ALBEDO = look_shaded(mix(water, foam_color.rgb, foam * 0.7), world_position.xz);

	float fresnel = pow(1.0 - clamp(dot(NORMAL, VIEW), 0.0, 1.0), 4.0);
	float body = mix(shallow_alpha, deep_alpha, smoothstep(0.0, 0.7, depth)) + fresnel * 0.25;
	// Gone at the bank, so no cell of the mesh shows.
	ALPHA = clamp(max(body, foam), 0.0, 1.0) * smoothstep(0.0, 0.07, depth);
	ROUGHNESS = mix(0.1, 0.55, foam);
	SPECULAR = 0.45;
}
