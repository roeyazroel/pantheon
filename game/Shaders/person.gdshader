// A person (spec sections 61 and 80, ADR-049). The mesh (PersonMesh) tags each vertex with its
// part in UV.x and its colour slot in UV.y. Each instance passes:
//   COLOR: rgb the clothes (linear), a the skin tone (0 lightest .. 1 darkest);
//   INSTANCE_CUSTOM: x the motion's cycle (0..1), y the motion (PersonMotion), z the body's forward
//   lean in radians, w the look (hair + 8 x headwear + 32 x dress + 64 x staff).
// The vertex stage swings the limbs about the hips and shoulders, bends the body at the hips, sits
// it down and hides the parts this person does not wear or hold.
shader_type spatial;
render_mode diffuse_burley, specular_schlick_ggx;

uniform vec3 skin_tones[6];
uniform vec3 hair_colors[8];
uniform vec3 trousers;
uniform vec3 straw;
uniform vec3 leather;
uniform vec3 wood;
uniform float leg_swing = 0.42;
uniform float arm_swing = 0.36;

varying vec3 albedo;

const float HIP = 0.88;
const float SHOULDER = 1.41;
const float NECK = 1.5;

// Turns a point about the horizontal X axis through the height h: a positive angle moves what
// hangs below backwards and what stands above forwards (the figure faces +Z).
vec3 turn(vec3 v, float h, float a) {
	float c = cos(a);
	float s = sin(a);
	float y = v.y - h;
	return vec3(v.x, h + y * c - v.z * s, y * s + v.z * c);
}

vec3 turn_normal(vec3 n, float a) {
	float c = cos(a);
	float s = sin(a);
	return vec3(n.x, n.y * c - n.z * s, n.y * s + n.z * c);
}

vec3 skin(float t) {
	float x = clamp(t, 0.0, 1.0) * 5.0;
	int i = min(int(floor(x)), 4);
	return mix(skin_tones[i], skin_tones[i + 1], x - float(i));
}

void vertex() {
	int part = int(UV.x + 0.5);
	int slot = int(UV.y + 0.5);
	vec4 custom = INSTANCE_CUSTOM;
	float cycle = fract(custom.x);
	int motion = int(custom.y + 0.5);
	float lean = custom.z;
	int look = int(custom.w + 0.5);
	int hair = look % 8;
	int headwear = (look / 8) % 4;
	bool dress = ((look / 32) % 2) == 1;
	bool staff = ((look / 64) % 2) == 1;

	float wave = sin(TAU * cycle);
	float stroke = 0.5 - 0.5 * cos(TAU * cycle);
	float leg_left = 0.0;
	float leg_right = 0.0;
	float arm_left = 0.0;
	float arm_right = 0.0;
	float nod = 0.0;
	float drop = 0.0;
	bool hoe = false;
	bool hammer = false;
	switch (motion) {
		case 1: // walking
			leg_left = -leg_swing * wave;
			leg_right = leg_swing * wave;
			arm_left = arm_swing * wave;
			arm_right = -arm_swing * wave;
			break;
		case 2: // sitting by the door
			leg_left = -1.42;
			leg_right = -1.36;
			arm_left = -0.55;
			arm_right = -0.62;
			drop = HIP - 0.13;
			lean = -0.06 + 0.02 * wave;
			break;
		case 3: // hoeing: both hands raise the hoe and bring it down
			arm_left = -(0.3 + 1.5 * stroke);
			arm_right = arm_left;
			hoe = true;
			break;
		case 4: { // hammering: a quick blow
			float lift = pow(stroke, 0.6);
			arm_right = -(0.8 + 1.7 * lift);
			arm_left = -0.75;
			hammer = true;
			break;
		}
		case 5: // chores: reaching down, one hand then the other
			arm_left = -(0.45 + 0.55 * stroke);
			arm_right = -(0.45 + 0.55 * (1.0 - stroke));
			break;
		case 6: { // praying: the arms rise to the sky and fall again
			float raise = smoothstep(0.15, 0.4, cycle) * (1.0 - smoothstep(0.6, 0.85, cycle));
			arm_left = -(0.2 + 2.55 * raise);
			arm_right = arm_left;
			nod = -0.3 * raise;
			break;
		}
		default: // standing, breathing
			arm_left = 0.04 * wave;
			arm_right = -0.04 * wave;
			break;
	}

	bool hidden = (part == 6 && headwear == 2) || (part == 7 && headwear != 1) || (part == 8 && headwear != 2)
		|| (part == 9 && !hoe) || (part == 10 && !hammer) || (part == 11 && !dress) || (part == 12 && !staff);

	vec3 v = VERTEX;
	vec3 n = NORMAL;
	if (part == 2) {
		v = turn(v, HIP, leg_left);
		n = turn_normal(n, leg_left);
	} else if (part == 3) {
		v = turn(v, HIP, leg_right);
		n = turn_normal(n, leg_right);
	} else if (part == 4 || part == 12) {
		v = turn(v, SHOULDER, arm_left);
		n = turn_normal(n, arm_left);
	} else if (part == 5 || part == 9 || part == 10) {
		v = turn(v, SHOULDER, arm_right);
		n = turn_normal(n, arm_right);
	} else if (part == 1 || part == 6 || part == 7 || part == 8) {
		v = turn(v, NECK, nod);
		n = turn_normal(n, nod);
	}

	if (part != 2 && part != 3 && part != 11) {
		v = turn(v, HIP, lean);
		n = turn_normal(n, lean);
	}

	v.y -= drop;
	VERTEX = hidden ? vec3(0.0) : v;
	NORMAL = n;

	vec3 clothes = COLOR.rgb;
	switch (slot) {
		case 0: albedo = clothes; break;
		case 1: albedo = skin(COLOR.a); break;
		case 2: albedo = mix(clothes, trousers, 0.7); break;
		case 3: albedo = hair_colors[hair]; break;
		case 4: albedo = straw; break;
		case 5: albedo = leather; break;
		case 6: albedo = wood; break;
		default: albedo = mix(clothes, vec3(0.85, 0.82, 0.75), 0.45); break;
	}
}

void fragment() {
	ALBEDO = albedo;
	ROUGHNESS = 0.88;
	SPECULAR = 0.25;
}
