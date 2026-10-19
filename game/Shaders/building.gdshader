// Houses, temples and walls (spec section 61, ADR-052). The mesh (HouseMesh, CivicMesh) tags each
// vertex with its colour slot in UV.y. Each instance passes:
//   COLOR: rgb the walls (linear);
//   INSTANCE_CUSTOM: rgb the roof (linear), w the patterns (walls + 8 x roof, SurfacePattern).
// The fragment stage paints a pattern worked out in the building's own space: daub, planks,
// coursed stone, thatch, slate or timber grain. A building under way shows only what is built:
// everything above `built` of its height is hidden.
shader_type spatial;
render_mode diffuse_burley, specular_schlick_ggx;

uniform vec3 trim;
uniform vec3 door;
uniform vec3 window_color;
uniform vec3 stone;
instance uniform float built = 1.0;

varying vec3 local_position;
varying vec3 local_normal;
varying vec3 base_color;
varying float pattern;

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

	ALBEDO = color;
	ROUGHNESS = rough;
	SPECULAR = 0.3;
}
