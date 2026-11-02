// The fallback terrain (ADR-005): vertex colours, with the world's look (ADR-078).
shader_type spatial;
render_mode diffuse_burley;

#include "res://Shaders/world_look.gdshaderinc"

varying vec2 ground;
varying float level;
varying vec3 color;

void vertex() {
	ground = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xz;
	level = NORMAL.y;
	color = pow(COLOR.rgb, vec3(2.2));
}

void fragment() {
	ALBEDO = look_ground(color, ground, level);
	ROUGHNESS = look_rough(0.95);
	SPECULAR = 0.2;
}
