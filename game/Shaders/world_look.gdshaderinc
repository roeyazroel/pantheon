// The world's look, shared by every shader of the land (ADR-078), so the whole picture turns with
// the year and the weather together. WorldLookView sets these each frame.
//   look_season: x spring, y autumn, z winter, each 0..1 and fading into the next;
//   look_sky: x how much of the sky is cloud, y how wet the land is, zw how far the clouds have drifted.
// Colours here are linear.
global uniform vec4 look_season;
global uniform vec4 look_sky;
global uniform sampler2D look_clouds : filter_linear_mipmap, repeat_enable;

// Soft patches over the land, 0..1.
float look_patches(vec2 ground) {
	return texture(look_clouds, ground * 0.019).g * 0.6 + texture(look_clouds, ground * 0.083).g * 0.4;
}

// How much of the sun a cloud takes from a place, 0..1: its shadow drifts over the land.
float look_cloud_shadow(vec2 ground) {
	vec2 uv = ground * 0.00038 + look_sky.zw;
	float clouds = texture(look_clouds, uv).r * 0.7 + texture(look_clouds, uv * 3.1 + 0.37).r * 0.3;
	float edge = 0.66 - look_sky.x * 0.42;
	return smoothstep(edge, edge + 0.1, clouds);
}

// A surface under the clouds' shadow, which takes the sun but leaves the sky's light. Under a sky
// that is all cloud there is no shadow to see: the sun itself is dimmed.
vec3 look_shaded(vec3 albedo, vec2 ground) {
	return albedo * (1.0 - 0.4 * (1.0 - look_sky.y) * look_cloud_shadow(ground));
}

// Grass as the year turns it: fresh in spring, straw and rust in autumn, dead and pale in winter.
vec3 look_turned(vec3 albedo, vec2 ground) {
	float light = dot(albedo, vec3(0.3, 0.59, 0.11));
	float patches = look_patches(ground);
	vec3 color = mix(albedo, albedo * vec3(0.9, 1.14, 0.8), look_season.x);
	color = mix(color, vec3(light) * vec3(1.46, 1.04, 0.5), look_season.y * (0.35 + 0.55 * smoothstep(0.3, 0.65, patches)));
	return mix(color, vec3(light) * vec3(1.3, 1.12, 0.84), look_season.z * 0.88);
}

// The ground: only what grows turns; wet earth darkens; frost lies in winter where the land is level.
vec3 look_ground(vec3 albedo, vec2 ground, float level) {
	float growing = smoothstep(0.0, 0.035, albedo.g - max(albedo.r, albedo.b * 1.1));
	vec3 color = mix(albedo, look_turned(albedo, ground), growing);
	color *= 1.0 - 0.16 * look_sky.y;
	float lying = texture(look_clouds, ground * 0.0065).g * 0.65 + texture(look_clouds, ground * 0.031).g * 0.35;
	float frost = look_season.z * smoothstep(0.66, 0.36, lying) * smoothstep(0.72, 0.95, level);
	return look_shaded(mix(color, vec3(0.7, 0.72, 0.76), frost * 0.62), ground);
}

// Wet surfaces shine.
float look_rough(float roughness) {
	return roughness * (1.0 - 0.5 * look_sky.y);
}
