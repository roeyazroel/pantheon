// The sky (ADR-079): a gradient the hour sets, a soft glow and a disc for the sun or moon, and, in the
// sky itself but not in the light it gives the land, high cloud and stars. The clouds drift with the
// shadows they cast (world_look.gdshaderinc).
shader_type sky;

global uniform vec4 look_season;
global uniform vec4 look_sky;
global uniform sampler2D look_clouds : filter_linear_mipmap, repeat_enable;

uniform vec3 top_color : source_color = vec3(0.17, 0.35, 0.64);
uniform vec3 horizon_color : source_color = vec3(0.74, 0.8, 0.84);
uniform vec3 ground_color : source_color = vec3(0.09, 0.2, 0.28);
// The sun or moon, set twice a second: a sky that read the light itself would be refiltered every frame.
uniform vec3 light_direction = vec3(0.0, 1.0, 0.0);
uniform vec3 light_color : source_color = vec3(1.0);
uniform float light_energy = 1.0;

float hash(vec3 p) {
	return fract(sin(dot(p, vec3(12.9898, 78.233, 37.719))) * 43758.5453);
}

void sky() {
	vec3 dir = EYEDIR;
	float up = max(dir.y, 0.0);
	vec3 color = mix(horizon_color, top_color, pow(up, 0.5));
	color = mix(color, ground_color, smoothstep(0.0, -0.3, dir.y));

	float facing = max(dot(dir, light_direction), 0.0);
	float glow = pow(facing, 18.0) * 0.22 + pow(facing, 220.0) * 0.45;
	float disc = smoothstep(0.99962, 0.99978, facing);
	// The glow is the sky's own, so the moon's stays faint; the disc is bright whatever its energy.
	color += light_color * (glow * light_energy + disc * mix(6.0, 1.6, look_season.w));

	if (!AT_CUBEMAP_PASS && dir.y > 0.0) {
		float night = look_season.w;
		if (night > 0.01) {
			vec3 cell = floor(dir * 170.0);
			float star = hash(cell);
			float twinkle = 0.7 + 0.3 * sin(TIME * (1.0 + star * 3.0) + star * 40.0);
			color += vec3(0.86, 0.9, 1.0) * smoothstep(0.9972, 1.0, star) * night * smoothstep(0.02, 0.3, up) * twinkle;
		}

		// High cloud, flattened towards the horizon, lit by the sky around it.
		vec2 plane = dir.xz / (up + 0.12);
		float clouds = texture(look_clouds, plane * 0.09 + look_sky.zw * 2.0).r * 0.7 + texture(look_clouds, plane * 0.31 + look_sky.zw * 3.0).r * 0.3;
		float cover = smoothstep(0.7 - look_sky.x * 0.4, 0.86 - look_sky.x * 0.4, clouds) * smoothstep(0.0, 0.12, up);
		color = mix(color, mix(horizon_color, vec3(1.0), 0.45 * (1.0 - look_season.w)) * mix(1.0, 0.4, look_sky.y), cover * 0.8);
	}

	COLOR = color;
}
