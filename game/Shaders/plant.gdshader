// Plants (ADR-073): trees, bushes, ferns, grass, reeds, flowers and stones, grown in code. UV2.x is
// a vertex's part (0 bark, 1 leaves, 2 a blade, 3 stone, 4 a petal) and UV2.y the light that reaches
// it (0 deep in a crown, 1 in the open; up a blade, its height). Leaves are cut from the leaf atlas
// and let light through; bark shows one of three patterns. Each instance carries its colour (linear)
// in INSTANCE_CUSTOM.rgb and its sway's phase in .a; each node its bark's colour and pattern.
shader_type spatial;
render_mode diffuse_burley, cull_disabled, specular_schlick_ggx;

uniform sampler2D leaf_atlas : source_color, filter_linear_mipmap_anisotropic, repeat_disable;
uniform sampler2D bark : filter_linear_mipmap, repeat_enable;
uniform vec2 wind = vec2(0.15, 0.0);
uniform float gust_speed = 1.2;
uniform float reference_height = 7.0;
uniform float flutter_strength = 1.0;
uniform float leaf_brightness = 2.7;
instance uniform vec3 trunk_color = vec3(0.12, 0.08, 0.05);
instance uniform float bark_style = 0.0;

varying vec3 tint;
varying float part;
varying float light;

void vertex() {
	part = UV2.x;
	light = UV2.y;
	tint = INSTANCE_CUSTOM.rgb;
	float phase = INSTANCE_CUSTOM.a * TAU;
	float rise = max(VERTEX.y, 0.0) / reference_height;
	float gust = 0.55 + 0.45 * sin(TIME * gust_speed + phase) + 0.2 * sin(TIME * gust_speed * 2.7 + phase * 1.3);
	vec3 sway = vec3(wind.x, 0.0, wind.y) * gust * rise * rise;
	// Leaves and blades flutter on their own, faster than the boughs sway.
	float leafy = (part > 0.5 && part < 2.5) || part > 3.5 ? 1.0 : 0.0;
	float flutter = 0.05 * leafy * (0.3 + length(wind) * 3.0) * flutter_strength;
	sway += vec3(sin(TIME * 4.1 + phase + VERTEX.x * 2.3), 0.4 * sin(TIME * 3.3 + VERTEX.z * 1.7), cos(TIME * 3.7 + phase + VERTEX.z * 2.1)) * flutter;
	// The instance is turned and scaled evenly, so its rotation's transpose over the scale squared undoes it.
	mat3 basis = mat3(MODEL_MATRIX);
	VERTEX += transpose(basis) * sway / dot(basis[0], basis[0]);
}

void fragment() {
	// Godot turns a back face's normal round; a card or a blade is lit the same from either side, as
	// the face of its crown or tuft, so the turn is undone.
	if (!FRONT_FACING && part > 0.5 && part < 2.5 || !FRONT_FACING && part > 3.5) {
		NORMAL = -NORMAL;
	}

	vec3 albedo;
	float alpha = 1.0;
	float rough = 0.9;
	vec3 through = vec3(0.0);
	float shade = mix(0.62, 1.0, light);
	if (part < 0.5) {
		vec3 pattern = texture(bark, UV).rgb;
		float grain = bark_style < 0.5 ? pattern.r : (bark_style < 1.5 ? pattern.g : pattern.b);
		albedo = trunk_color * (0.45 + 1.0 * grain) * shade;
		rough = 0.96;
	} else if (part < 1.5) {
		vec4 leaves = texture(leaf_atlas, UV);
		alpha = leaves.a;
		albedo = tint * leaves.rgb * leaf_brightness * shade;
		through = tint * 0.7 * shade;
		rough = 0.78;
	} else if (part < 2.5) {
		// A blade: dark at its foot, light at its tip.
		albedo = tint * mix(0.45, 1.2, light);
		through = tint * 0.35;
		rough = 0.82;
	} else if (part < 3.5) {
		float grain = texture(bark, UV * 0.37).r;
		vec3 stone = tint * (0.7 + 0.5 * grain);
		// Moss and lichen gather on the tops of stones.
		float moss = smoothstep(0.86, 0.97, light) * smoothstep(0.35, 0.65, texture(bark, UV * 0.11 + 0.3).r);
		albedo = mix(stone, vec3(0.16, 0.24, 0.09), moss * 0.75);
		rough = 0.92;
	} else {
		albedo = tint;
		through = tint * 0.4;
		rough = 0.7;
	}

	ALBEDO = albedo;
	ALPHA = alpha;
	ALPHA_SCISSOR_THRESHOLD = 0.5;
	BACKLIGHT = through;
	ROUGHNESS = rough;
	SPECULAR = 0.25;
	AO = shade;
	AO_LIGHT_AFFECT = 0.15;
}
