// Rivers, lakes and floods (ADR-022). The vertex colour's red channel is the water's depth over
// the ground (0 at the bank, 1 at WaterSurfaceMesh.DeepestDrawn), so the shallows are clear and
// the middle dark, on every renderer.
shader_type spatial;
render_mode blend_mix, depth_draw_opaque, cull_back, diffuse_burley, specular_schlick_ggx, shadows_disabled;

uniform vec4 shallow_color : source_color = vec4(0.36, 0.62, 0.62, 1.0);
uniform vec4 deep_color : source_color = vec4(0.10, 0.30, 0.38, 1.0);
uniform vec4 foam_color : source_color = vec4(0.90, 0.93, 0.90, 1.0);
uniform float shallow_alpha : hint_range(0.0, 1.0) = 0.35;
uniform float deep_alpha : hint_range(0.0, 1.0) = 0.92;
// Floods are muddy and do not foam.
uniform float muddiness : hint_range(0.0, 1.0) = 0.0;
uniform vec4 mud_color : source_color = vec4(0.42, 0.36, 0.24, 1.0);
uniform float wave_scale = 0.12;
uniform float wave_speed = 1.4;
uniform float wave_strength : hint_range(0.0, 1.0) = 0.15;

varying vec3 world_position;
varying float depth;

void vertex() {
	world_position = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
	depth = COLOR.r;
}

void fragment() {
	float t = TIME * wave_speed;
	vec2 p = world_position.xz * wave_scale;
	vec2 slope = vec2(cos(p.x * 1.3 + t) + 0.6 * cos(p.y * 1.9 - t * 1.1), cos(p.y * 1.1 + t * 0.8) + 0.5 * cos(p.x * 2.3 + t * 1.3)) * wave_strength;
	vec3 world_normal = normalize(vec3(-slope.x, 1.0, -slope.y));
	NORMAL = normalize((VIEW_MATRIX * vec4(world_normal, 0.0)).xyz);

	vec3 water = mix(shallow_color.rgb, deep_color.rgb, smoothstep(0.0, 0.8, depth));
	water = mix(water, mud_color.rgb, muddiness);
	float edge = (1.0 - smoothstep(0.0, 0.05, depth)) * (1.0 - muddiness);
	float ripple = 0.6 + 0.4 * sin(world_position.x * 0.7 + world_position.z * 0.5 + TIME * 2.2);
	ALBEDO = mix(water, foam_color.rgb, clamp(edge * ripple, 0.0, 1.0) * 0.25);

	float fresnel = pow(1.0 - clamp(dot(NORMAL, VIEW), 0.0, 1.0), 4.0);
	ALPHA = clamp(mix(shallow_alpha, deep_alpha, smoothstep(0.0, 0.7, depth)) + fresnel * 0.25, 0.0, 1.0);
	ROUGHNESS = 0.12;
	SPECULAR = 0.4;
}
