// Sea surface. Colour, opacity and shoreline foam come from a baked sea depth map
// (WaterDepthMap) rather than the depth buffer, so the shader works on every renderer.
shader_type spatial;
render_mode blend_mix, depth_draw_opaque, cull_back, diffuse_burley, specular_schlick_ggx, shadows_disabled;

uniform vec4 shallow_color : source_color = vec4(0.25, 0.64, 0.72, 1.0);
uniform vec4 deep_color : source_color = vec4(0.07, 0.25, 0.36, 1.0);
uniform vec4 foam_color : source_color = vec4(0.93, 0.95, 0.92, 1.0);
// 0 at the shoreline and on land, 1 at full depth. Clamped at the edges, so the open sea is deep.
uniform sampler2D depth_map : hint_default_black, filter_linear, repeat_disable;
// World size covered by depth_map, which starts at the world origin.
uniform float map_size = 2048.0;
uniform float shallow_alpha : hint_range(0.0, 1.0) = 0.5;
uniform float deep_alpha : hint_range(0.0, 1.0) = 0.97;
uniform float foam_width : hint_range(0.0, 0.5) = 0.06;
uniform float wave_scale = 0.045;
uniform float wave_speed = 0.7;
uniform float wave_strength : hint_range(0.0, 1.0) = 0.22;

varying vec3 world_position;

void vertex() {
	world_position = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
}

// Slope of three directional swells; cheap, tile-free and good enough for a god's-eye view.
vec2 wave_slope(vec2 p, float t) {
	vec2 slope = vec2(0.8, 0.6) * cos(dot(p, vec2(0.8, 0.6)) + t * 1.3);
	slope += vec2(-0.6, 0.8) * cos(dot(p, vec2(-0.6, 0.8)) * 1.7 + t * 1.7) * 0.6;
	slope += vec2(0.28, -0.96) * cos(dot(p, vec2(0.28, -0.96)) * 2.9 + t * 2.3) * 0.35;
	return slope;
}

void fragment() {
	float depth = texture(depth_map, world_position.xz / map_size).r;
	float t = TIME * wave_speed;

	vec2 slope = wave_slope(world_position.xz * wave_scale, t) * wave_strength;
	vec3 world_normal = normalize(vec3(-slope.x, 1.0, -slope.y));
	NORMAL = normalize((VIEW_MATRIX * vec4(world_normal, 0.0)).xyz);

	float shore = 1.0 - smoothstep(0.0, foam_width, depth);
	float ripple = 0.65 + 0.35 * sin(world_position.x * 0.31 + world_position.z * 0.23 + TIME * 1.6);
	vec3 water = mix(shallow_color.rgb, deep_color.rgb, smoothstep(0.0, 0.85, depth));
	ALBEDO = mix(water, foam_color.rgb, clamp(shore * ripple, 0.0, 1.0) * 0.75);

	// Water looks more opaque at grazing angles.
	float fresnel = pow(1.0 - clamp(dot(NORMAL, VIEW), 0.0, 1.0), 4.0);
	ALPHA = clamp(mix(shallow_alpha, deep_alpha, smoothstep(0.0, 0.6, depth)) + fresnel * 0.3, 0.0, 1.0);
	ROUGHNESS = 0.06;
	SPECULAR = 0.55;
}
