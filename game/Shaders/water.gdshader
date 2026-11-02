// Sea surface (ADR-077). Colour, opacity and foam come from a baked sea depth map (WaterDepthMap)
// rather than the depth buffer, so the shader works on every renderer. Three layers of ripples
// drift across each other; waves roll in towards the shore and break there into lacy foam.
shader_type spatial;
render_mode blend_mix, depth_draw_opaque, cull_back, diffuse_burley, specular_schlick_ggx, shadows_disabled;

uniform vec4 shore_color : source_color = vec4(0.52, 0.78, 0.70, 1.0);
uniform vec4 shallow_color : source_color = vec4(0.14, 0.50, 0.56, 1.0);
uniform vec4 deep_color : source_color = vec4(0.05, 0.20, 0.30, 1.0);
uniform vec4 foam_color : source_color = vec4(0.95, 0.96, 0.92, 1.0);
// 0 at the shoreline and on land, 1 at full depth. Clamped at the edges, so the open sea is deep.
uniform sampler2D depth_map : hint_default_black, filter_linear, repeat_disable;
// Slope in red and green, foam lace in blue (WaterTextures).
uniform sampler2D ripples : hint_default_black, filter_linear_mipmap_anisotropic, repeat_enable;
// World size covered by depth_map, which starts at the world origin.
uniform float map_size = 2048.0;
uniform float shallow_alpha : hint_range(0.0, 1.0) = 0.45;
uniform float deep_alpha : hint_range(0.0, 1.0) = 0.97;
// How far out, as a share of depth_map's range, waves show and break.
uniform float foam_reach : hint_range(0.0, 0.5) = 0.13;
uniform float wave_speed = 0.7;
uniform float wave_strength : hint_range(0.0, 2.0) = 0.75;
// Camera distances over which the fine ripples and foam fade and the surface turns rougher.
uniform float wave_fade_start = 150.0;
uniform float wave_fade_end = 1200.0;
uniform float near_roughness : hint_range(0.0, 1.0) = 0.07;
uniform float far_roughness : hint_range(0.0, 1.0) = 0.22;

varying vec3 world_position;

void vertex() {
	world_position = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
}

void fragment() {
	vec2 p = world_position.xz;
	float depth = texture(depth_map, p / map_size).r;
	float t = TIME * wave_speed;
	// Far from the camera a ripple spans only a few pixels, and sharp sun glints on it flicker.
	float far = smoothstep(wave_fade_start, wave_fade_end, distance(world_position, CAMERA_POSITION_WORLD));

	vec4 swell = texture(ripples, p * 0.0031 + vec2(0.006, 0.004) * t);
	vec4 chop = texture(ripples, p * 0.017 + vec2(-0.011, 0.016) * t);
	vec4 fine = texture(ripples, p * 0.071 + vec2(0.031, -0.022) * t);
	vec2 slope = (swell.rg - 0.5) * 0.9 + (chop.rg - 0.5) * mix(0.7, 0.25, far) + (fine.rg - 0.5) * 0.45 * (1.0 - far);
	slope *= wave_strength;
	vec3 world_normal = normalize(vec3(-slope.x, 1.0, -slope.y));
	NORMAL = normalize((VIEW_MATRIX * vec4(world_normal, 0.0)).xyz);

	vec3 water = mix(shallow_color.rgb, deep_color.rgb, smoothstep(0.04, 0.75, depth));
	water = mix(shore_color.rgb, water, smoothstep(0.0, 0.09, depth));
	water *= 0.93 + 0.14 * swell.b;

	// Waves roll in as depth falls, and only in the shallows.
	float crest = smoothstep(0.55, 0.95, sin(depth * 62.0 + t * 1.5 + chop.b * 5.0));
	float lace = smoothstep(0.32, 0.6, fine.b * 0.6 + chop.b * 0.4);
	float rolling = crest * lace * (1.0 - smoothstep(0.0, foam_reach, depth)) * smoothstep(0.0, 0.015, depth + 0.004);
	float lip = (1.0 - smoothstep(0.0, 0.014, depth)) * smoothstep(0.25, 0.5, chop.b);
	float foam = clamp(rolling * (1.0 - far) + lip * (1.0 - far * 0.7), 0.0, 1.0);
	ALBEDO = mix(water, foam_color.rgb, foam * 0.9);

	// Water looks more opaque at grazing angles.
	float fresnel = pow(1.0 - clamp(dot(NORMAL, VIEW), 0.0, 1.0), 4.0);
	float body = mix(shallow_alpha, deep_alpha, smoothstep(0.0, 0.4, depth)) + fresnel * 0.3;
	ALPHA = clamp(max(body, foam), 0.0, 1.0);
	ROUGHNESS = mix(mix(near_roughness, far_roughness, far), 0.7, foam);
	SPECULAR = 0.5;
}
