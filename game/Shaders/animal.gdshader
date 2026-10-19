// Animals (spec section 61, ADR-054). The mesh (AnimalMesh) tags each vertex's part in UV.x and its
// colour slot in UV.y (0 the coat, 1 light, 2 dark). Each instance passes its coat in COLOR (linear)
// and in INSTANCE_CUSTOM: x its motion's cycle, y its motion (AnimalMotion: grazing, walking,
// flying), z 1 for a stag's antlers. A deer's legs swing in pairs as it walks and its head lowers to
// graze; a bird's wings beat.
shader_type spatial;
render_mode diffuse_burley, specular_schlick_ggx, cull_disabled;

uniform vec3 light_color;
uniform vec3 dark_color;

varying vec3 base_color;

const float LEG_TOP = 0.92;

// Turns a point about an axis through `pivot` along X: a positive angle lowers what lies ahead (+Z).
vec3 turn_x(vec3 v, vec3 pivot, float a) {
	vec3 d = v - pivot;
	float c = cos(a);
	float s = sin(a);
	return pivot + vec3(d.x, d.y * c - d.z * s, d.y * s + d.z * c);
}

// Turns a point about an axis through `pivot` along Z: a positive angle lifts what lies to +X.
vec3 turn_z(vec3 v, vec3 pivot, float a) {
	vec3 d = v - pivot;
	float c = cos(a);
	float s = sin(a);
	return pivot + vec3(d.x * c - d.y * s, d.x * s + d.y * c, d.z);
}

void vertex() {
	int part = int(UV.x + 0.5);
	int slot = int(UV.y + 0.5);
	float cycle = INSTANCE_CUSTOM.x;
	int motion = int(INSTANCE_CUSTOM.y + 0.5);
	bool antlers = INSTANCE_CUSTOM.z > 0.5;
	float wave = sin(TAU * cycle);
	vec3 v = VERTEX;
	if (motion == 2) {
		float flap = 0.8 * wave;
		if (part == 5) {
			v = turn_z(v, vec3(0.05, 0.02, 0.0), flap);
		} else if (part == 6) {
			v = turn_z(v, vec3(-0.05, 0.02, 0.0), -flap);
		}
	} else {
		float swing = motion == 1 ? 0.45 * wave : 0.0;
		float side = sign(VERTEX.x);
		if (part == 2) {
			v = turn_x(v, vec3(0.0, LEG_TOP, 0.42), swing * side);
		} else if (part == 3) {
			v = turn_x(v, vec3(0.0, LEG_TOP, -0.42), -swing * side);
		} else if (part == 1 || part == 4) {
			float lower = motion == 0 ? 1.15 + 0.07 * sin(TAU * cycle * 2.0) : 0.08 * wave;
			v = turn_x(v, vec3(0.0, 1.2, 0.5), lower);
		}

		if (part == 4 && !antlers) {
			v = vec3(0.0);
		}
	}

	VERTEX = v;
	base_color = slot == 0 ? COLOR.rgb : slot == 1 ? light_color : dark_color;
}

void fragment() {
	ALBEDO = base_color;
	ROUGHNESS = 0.9;
}
