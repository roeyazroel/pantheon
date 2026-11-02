// Roads (ADR-033, ADR-073): a track worn into the ground. UV.x runs across it (0 at its left edge,
// 0.5 in its middle, 1 at its right), UV.y along it in metres. Its edges are ragged and soft, grass
// creeping in at the verges and growing down its middle between two darker ruts, with stones in the
// dirt, turning with the year and darkening in the wet (ADR-078); a road a disaster has cut is darker. Drawn over the ground and a little towards the eye, so the ground's coarser
// mesh in the distance does not hide it.
shader_type spatial;
render_mode blend_mix, depth_draw_never, cull_disabled, diffuse_burley, specular_disabled;

#include "res://Shaders/world_look.gdshaderinc"

uniform sampler2D noise : filter_linear_mipmap, repeat_enable;
uniform vec3 dirt : source_color = vec3(0.6, 0.5, 0.37);
uniform vec3 rut : source_color = vec3(0.47, 0.38, 0.27);
uniform vec3 verge : source_color = vec3(0.42, 0.5, 0.24);
instance uniform float cut = 0.0;

varying vec3 world;

void vertex() {
	world = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
	vec3 eye = CAMERA_POSITION_WORLD - world;
	float away = length(eye);
	VERTEX += inverse(mat3(MODEL_MATRIX)) * (eye / away) * min(3.0, 0.15 + away * 0.004);
}

void fragment() {
	float across = UV.x;
	vec2 ground = world.xz;
	float broad = texture(noise, ground * 0.012).r;
	float blotch = texture(noise, ground * 0.05).g;
	float grain = texture(noise, ground * 0.35).b;
	vec3 fine = texture(noise, ground * 1.6).rgb;
	// 0 at an edge, 1 in the middle; the edge wanders with the noise.
	float inward = 1.0 - abs(across - 0.5) * 2.0;
	float edge = smoothstep(0.0, 0.32 + 0.2 * blotch, inward + (broad - 0.5) * 0.35);
	float ruts = (1.0 - smoothstep(0.03, 0.12, abs(abs(across - 0.5) - 0.2))) * (0.6 + 0.4 * grain);
	float middle = (1.0 - smoothstep(0.0, 0.09, abs(across - 0.5))) * smoothstep(0.45, 0.7, blotch);
	vec3 color = dirt * (0.78 + 0.4 * grain) * (0.9 + 0.2 * broad) * (0.82 + 0.36 * fine.r);
	// Stones lie in the dirt, more of them away from the ruts.
	color = mix(color, color * vec3(1.35, 1.32, 1.28), smoothstep(0.66, 0.74, fine.g) * (1.0 - ruts));
	color = mix(color, rut * (0.85 + 0.3 * grain), ruts * 0.4);
	color = mix(color, look_turned(verge, ground) * (0.75 + 0.5 * grain), max(middle * 0.85, (1.0 - smoothstep(0.1, 0.45, inward)) * 0.55));
	color = mix(color, color * 0.5, cut);
	color *= 1.0 - 0.18 * look_sky.y;
	ALBEDO = look_shaded(color, ground);
	ALPHA = edge * 0.95;
	ROUGHNESS = look_rough(1.0);
	// The track faces up whichever way its triangles are wound; its ruts and stones catch the light.
	vec2 bump = (fine.rb - 0.5) * 0.45 + vec2(ruts * 0.12 * sign(across - 0.5), 0.0);
	NORMAL = normalize((VIEW_MATRIX * vec4(bump.x, 1.0, bump.y, 0.0)).xyz);
}
