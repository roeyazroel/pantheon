// Houses, temples and walls (spec section 61, ADR-052, ADR-077). The mesh (HouseMesh, CivicMesh) tags
// each vertex with its colour slot in UV.y, and how far up a roof's slope it lies in UV.x. Each
// instance passes:
//   COLOR: rgb the walls (linear);
//   INSTANCE_CUSTOM: rgb the roof (linear), w the patterns (walls + 8 x roof, SurfacePattern).
// The fragment stage paints a pattern worked out in the building's own space: daub, planks,
// coursed stone, thatch, slate or timber grain, and weathers it: damp and dirt at the foot of the
// walls, moss low on the roofs, darker eaves and a paler ridge, each house a little its own. A
// building under way shows only what is built: everything above `built` of its height is hidden.
shader_type spatial;
render_mode diffuse_burley, specular_schlick_ggx;

#include "res://Shaders/world_look.gdshaderinc"

uniform vec3 trim;
uniform vec3 door;
uniform vec3 window_color;
uniform vec3 stone;
uniform vec3 lamplight : source_color = vec3(1.0, 0.56, 0.2);
uniform float built = 1.0;

varying vec3 local_position;
varying vec3 local_normal;
varying vec3 base_color;
varying float pattern;
varying float up_roof;
varying float roofed;
varying float weather;
varying vec2 ground;

const float SINK = 0.6;
const float TOP = 7.2;

float hash(vec2 p) {
	return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453);
}

float noise(vec2 p) {
	vec2 i = floor(p);
	vec2 f = fract(p);
	f = f * f * (3.0 - 2.0 * f);
	return mix(mix(hash(i), hash(i + vec2(1.0, 0.0)), f.x), mix(hash(i + vec2(0.0, 1.0)), hash(i + vec2(1.0, 1.0)), f.x), f.y);
}

// The coordinate along a surface, round it: X on faces that look along Z, Z on those that look along X.
float along(vec3 p, vec3 n) {
	return abs(n.x) > abs(n.z) ? p.z : p.x;
}

void vertex() {
	int slot = int(UV.y + 0.5);
	int packed = int(INSTANCE_CUSTOM.w + 0.5);
	local_position = VERTEX;
	local_normal = NORMAL;
	up_roof = UV.x;
	roofed = slot == 1 ? 1.0 : 0.0;
	weather = hash(MODEL_MATRIX[3].xz * 0.37);
	ground = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xz;
	switch (slot) {
		case 0: base_color = COLOR.rgb; pattern = float(packed % 8); break;
		case 1: base_color = INSTANCE_CUSTOM.rgb; pattern = float(packed / 8); break;
		case 2: base_color = trim; pattern = 5.0; break;
		case 3: base_color = door; pattern = 1.0; break;
		case 4: base_color = window_color; pattern = 6.0; break;
		default: base_color = stone; pattern = 2.0; break;
	}

	if (built < 0.999 && VERTEX.y > mix(-SINK, TOP, built)) {
		VERTEX = vec3(0.0);
	}
}

void fragment() {
	vec3 p = local_position;
	vec3 n = normalize(local_normal);
	vec3 color = base_color;
	float rough = 0.9;
	int kind = int(pattern + 0.5);
	if (kind == 0) {
		// Daub: blotched plaster over wattle.
		color *= 0.86 + 0.2 * noise(vec2(along(p, n) * 2.5, p.y * 2.5));
		color *= 0.93 + 0.07 * noise(vec2(along(p, n) * 9.0, p.y * 1.2));
	} else if (kind == 1) {
		// Planks: upright boards with dark seams and grain.
		float u = along(p, n) / 0.27;
		float f = fract(u);
		float seam = smoothstep(0.0, 0.08, f) * smoothstep(1.0, 0.92, f);
		color *= (0.7 + 0.3 * seam) * (0.88 + 0.22 * hash(vec2(floor(u), 7.0))) * (0.93 + 0.07 * noise(vec2(u * 3.0, p.y * 11.0)));
	} else if (kind == 2) {
		// Stone: courses of blocks set in mortar.
		float row = floor(p.y / 0.36);
		float u = along(p, n) / 0.6 + row * 0.47;
		vec2 f = vec2(fract(u), fract(p.y / 0.36));
		float mortar = smoothstep(0.0, 0.06, f.x) * smoothstep(1.0, 0.94, f.x) * smoothstep(0.0, 0.1, f.y) * smoothstep(1.0, 0.9, f.y);
		color *= (0.62 + 0.38 * mortar) * (0.84 + 0.3 * hash(vec2(floor(u), row)));
	} else if (kind == 3) {
		// Thatch: layers down the slope, combed into strands.
		float layer = fract(p.y * 2.6 + noise(p.xz * 2.5) * 0.7);
		float strands = noise(vec2(along(p, n) * 26.0, p.y * 3.0));
		color *= (0.74 + 0.26 * smoothstep(0.0, 0.4, layer)) * (0.82 + 0.3 * strands);
	} else if (kind == 4) {
		// Slate: overlapping rows of tiles.
		float row = floor(p.y / 0.22);
		float u = along(p, n) / 0.32 + row * 0.5;
		vec2 f = vec2(fract(u), fract(p.y / 0.22));
		float edge = smoothstep(0.0, 0.05, f.x) * smoothstep(1.0, 0.95, f.x) * smoothstep(0.0, 0.14, f.y);
		color *= (0.62 + 0.38 * edge) * (0.86 + 0.26 * hash(vec2(floor(u), row)));
		rough = 0.65;
	} else if (kind == 5) {
		// Timber: grain along the beam.
		color *= 0.82 + 0.18 * noise(vec2((p.x + p.z) * 3.0, p.y * 14.0));
	} else {
		// Windows: dark, a little glossy.
		rough = 0.3;
	}

	// Weathering, each house its own. Roofs: darker at the eaves, paler towards the ridge, mossy low
	// down. Walls: damp at the foot.
	float blotch = noise(p.xz * 1.3 + vec2(weather * 40.0));
	if (roofed > 0.5) {
		color *= mix(0.78, 1.08, smoothstep(0.0, 0.9, up_roof));
		float moss = smoothstep(0.55, 0.8, blotch) * (1.0 - smoothstep(0.1, 0.65, up_roof)) * smoothstep(0.35, 0.8, weather);
		color = mix(color, color * vec3(0.62, 0.78, 0.5), moss * 0.7);
		color *= 0.9 + 0.22 * blotch;
		// Rain darkens the thatch; winter's frost dusts the roofs, thickest high up.
		color *= 1.0 - 0.16 * look_sky.y;
		color = mix(color, vec3(0.74, 0.78, 0.84), look_season.z * 0.5 * smoothstep(0.25, 0.8, up_roof + blotch * 0.4));
	} else {
		color *= mix(0.68, 1.0, smoothstep(-0.25, 0.9, p.y));
		color *= 0.94 + 0.12 * blotch;
	}

	color *= 0.92 + 0.16 * weather;
	ALBEDO = look_shaded(color, ground);
	ROUGHNESS = look_rough(rough);
	SPECULAR = 0.3;
	// Windows light, house by house as the dark comes on, each lamp a little unsteady (ADR-079), and go
	// out again as the village falls asleep, a few lit again before dawn (ADR-080).
	if (kind == 6) {
		float lit = smoothstep(weather * 0.8 + 0.05, weather * 0.8 + 0.2, look_day.x);
		EMISSION = lamplight * lit * (2.6 + 0.5 * sin(TIME * (2.0 + weather * 3.0) + weather * 30.0));
	}
}
