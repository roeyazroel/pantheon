// The player's angel (ADR-074). The mesh (AngelMesh) tags each vertex with its part in UV2.x (0 robe,
// 1 skin, 2 hair, 3 wing, 4 halo, 5 thorn, 6 eye, 7 sash, 8 blade, 9 veil, 10 chain, 11 crown) and its
// side in UV2.y (-1 the viewer's left, 1 the right). Its look follows its god's deeds: `lean` from -1
// (harm) to 1 (kindness), `formed` how much the god has done, and `conflict` how torn it is, which splits
// it, the viewer's left darker; `marks` shows the traits' marks. The wings beat about the shoulders;
// `appear` dissolves it in and out.
shader_type spatial;
render_mode diffuse_burley, cull_disabled, specular_schlick_ggx;

uniform float lean = 0.0;
uniform float formed = 0.0;
uniform float conflict = 0.0;
uniform int marks = 0;
uniform float appear = 1.0;
uniform float wingbeat = 0.0;
uniform vec3 shoulder = vec3(0.14, 1.5, -0.1);

varying float part;
varying float side;
varying float here;
varying vec2 coord;
varying vec3 body;

// Colours from the darkest stage to the lightest, in sRGB; the middle is an unformed angel's silver.
vec3 ramp(vec3 a, vec3 b, vec3 c, vec3 d, vec3 e, float l) {
	float x = clamp((l + 1.0) * 2.0, 0.0, 4.0);
	vec3 s = x < 1.0 ? mix(a, b, x) : x < 2.0 ? mix(b, c, x - 1.0) : x < 3.0 ? mix(c, d, x - 2.0) : mix(d, e, x - 3.0);
	return pow(s, vec3(2.2));
}

vec3 robe_of(float l) { return ramp(vec3(0.10, 0.08, 0.09), vec3(0.23, 0.19, 0.22), vec3(0.66, 0.67, 0.72), vec3(0.86, 0.83, 0.75), vec3(0.93, 0.91, 0.86), l); }
vec3 skin_of(float l) { return ramp(vec3(0.31, 0.26, 0.29), vec3(0.52, 0.46, 0.47), vec3(0.82, 0.78, 0.75), vec3(0.93, 0.82, 0.71), vec3(0.97, 0.88, 0.78), l); }
vec3 hair_of(float l) { return ramp(vec3(0.04, 0.03, 0.03), vec3(0.17, 0.14, 0.15), vec3(0.60, 0.61, 0.67), vec3(0.81, 0.66, 0.37), vec3(0.95, 0.89, 0.66), l); }
vec3 wing_of(float l) { return ramp(vec3(0.08, 0.06, 0.07), vec3(0.23, 0.18, 0.21), vec3(0.78, 0.80, 0.85), vec3(0.88, 0.85, 0.79), vec3(0.94, 0.93, 0.90), l); }
vec3 edge_of(float l) { return ramp(vec3(1.0, 0.35, 0.12), vec3(0.56, 0.23, 0.15), vec3(0.60, 0.63, 0.69), vec3(0.80, 0.70, 0.48), vec3(0.91, 0.81, 0.53), l); }
vec3 halo_of(float l) { return ramp(vec3(1.0, 0.24, 0.12), vec3(0.82, 0.38, 0.18), vec3(0.79, 0.81, 0.86), vec3(0.94, 0.76, 0.33), vec3(1.0, 0.89, 0.55), l); }
vec3 trim_of(float l) { return ramp(vec3(0.48, 0.11, 0.07), vec3(0.29, 0.23, 0.25), vec3(0.56, 0.58, 0.64), vec3(0.78, 0.60, 0.23), vec3(0.91, 0.76, 0.35), l); }

bool is_part(float p, float n) { return abs(p - n) < 0.5; }

void vertex() {
	part = UV2.x;
	side = UV2.y;
	coord = UV;
	here = clamp(lean + side * conflict * 0.95, -1.0, 1.0);
	if (is_part(part, 3.0)) {
		// A wing turns about its shoulder, more towards its tip; a dark angel's droop.
		vec3 pivot = vec3(shoulder.x * sign(side), shoulder.y, shoulder.z);
		vec3 local = VERTEX - pivot;
		float reach = clamp(length(local.xy), 0.0, 1.3);
		float angle = sign(side) * (sin(wingbeat) * 0.2 - max(-here, 0.0) * 0.32) * reach;
		float c = cos(angle);
		float s = sin(angle);
		local.xy = vec2(local.x * c - local.y * s, local.x * s + local.y * c);
		local.z -= sin(wingbeat + 0.6) * 0.12 * reach * reach;
		VERTEX = pivot + local;
	} else if (is_part(part, 0.0) && VERTEX.y < 0.7) {
		// The hem stirs.
		float loose = (0.7 - VERTEX.y) / 0.7;
		VERTEX.x += sin(TIME * 1.7 + VERTEX.z * 6.0 + VERTEX.x * 4.0) * 0.02 * loose;
		VERTEX.z += cos(TIME * 1.3 + VERTEX.x * 5.0) * 0.025 * loose;
	}
	body = VERTEX;
}

void fragment() {
	// Godot turns a back face's normal round; the figure's own normals face out whichever way its
	// triangles wind, and a feather or a veil is lit the same from either side, so the turn is undone.
	if (!FRONT_FACING) {
		NORMAL = -NORMAL;
	}

	// Dissolves in and out.
	float grain = fract(sin(dot(floor(FRAGCOORD.xy), vec2(12.9898, 78.233))) * 43758.5453);
	if (grain > appear) {
		discard;
	}

	// A veil is sheer: half of it lets the face through, more towards its hem.
	if (is_part(part, 9.0) && grain > 0.75 - 0.35 * coord.y) {
		discard;
	}

	// The traits' marks show only for the traits the god has.
	if ((is_part(part, 8.0) && (marks & 1) == 0) || (is_part(part, 9.0) && (marks & 2) == 0) || (is_part(part, 10.0) && (marks & 4) == 0) || (is_part(part, 11.0) && (marks & 8) == 0)) {
		discard;
	}

	float dark = clamp(-here, 0.0, 1.0);
	float bright = clamp(here, 0.0, 1.0);
	// A halo breaks as its god harms, and thorns grow from it, more the darker it is.
	if (is_part(part, 4.0) && fract(coord.x * 9.0) > clamp((lean + 0.6) / 0.6, 0.0, 1.0) * (1.0 - 0.45 * conflict)) {
		discard;
	}
	if (is_part(part, 5.0) && -lean < 0.15 + 0.6 * fract(coord.x * 7.31)) {
		discard;
	}

	vec3 albedo = robe_of(here);
	vec3 glow = vec3(0.0);
	float rough = 0.85;
	vec3 through = vec3(0.0);
	if (is_part(part, 0.0)) {
		albedo = robe_of(here) * (0.86 + 0.14 * sin(coord.x * 6.2831 * 9.0 + body.y * 2.0));
		if ((marks & 32) != 0) {
			float thread = smoothstep(0.93, 1.0, sin(coord.x * 6.2831 * 14.0) * 0.5 + 0.5);
			albedo = mix(albedo, trim_of(1.0), thread);
			glow += trim_of(1.0) * thread * 0.4;
		}
		through = albedo * 0.1 * bright;
	} else if (is_part(part, 1.0)) {
		albedo = skin_of(here);
		rough = 0.7;
		bool hand = abs(side) > 0.5;
		if (hand && (marks & 64) != 0) {
			glow += halo_of(1.0) * 1.4;
		}
		// Cracks of fire in a fallen or chaotic angel's skin.
		float crack = abs(sin(body.x * 41.0 + sin(body.y * 29.0) * 2.0) * sin(body.y * 37.0 + sin(body.z * 23.0) * 2.0));
		float burning = max(smoothstep(0.35, 0.8, dark), (marks & 16) != 0 ? 0.8 : 0.0);
		glow += vec3(1.0, 0.32, 0.08) * smoothstep(0.025, 0.0, crack) * burning * 2.2;
	} else if (is_part(part, 2.0)) {
		albedo = hair_of(here);
		rough = 0.6;
	} else if (is_part(part, 3.0)) {
		float along = coord.x;
		float edge = smoothstep(0.55, 1.0, abs(coord.y)) + smoothstep(0.82, 1.0, along);
		albedo = mix(wing_of(here), edge_of(here), clamp(edge * 0.6, 0.0, 1.0)) * (0.82 + 0.18 * along);
		glow += edge_of(here) * edge * dark * 2.2;
		glow += wing_of(here) * bright * 0.05;
		through = wing_of(here) * 0.25;
		rough = 0.75;
	} else if (is_part(part, 4.0) || is_part(part, 5.0)) {
		albedo = halo_of(lean);
		glow = halo_of(lean) * (1.4 + 2.2 * abs(lean) * (0.4 + 0.6 * formed));
		rough = 0.3;
	} else if (is_part(part, 6.0)) {
		bool clear = (marks & 128) != 0;
		albedo = clear ? vec3(0.8, 0.9, 1.0) : vec3(0.08, 0.06, 0.07);
		glow = clear ? vec3(0.6, 0.85, 1.0) * 2.0 : vec3(1.0, 0.36, 0.1) * smoothstep(0.25, 0.7, dark) * 6.0;
	} else if (is_part(part, 7.0)) {
		albedo = trim_of(here);
		rough = 0.4;
	} else if (is_part(part, 8.0)) {
		albedo = vec3(0.3, 0.08, 0.04);
		glow = vec3(1.0, 0.4, 0.1) * (2.0 + 1.5 * sin(TIME * 7.0 + body.y * 12.0));
	} else if (is_part(part, 9.0)) {
		albedo = mix(vec3(0.12, 0.1, 0.14), vec3(0.62, 0.6, 0.7), bright);
		through = albedo * 0.4;
	} else if (is_part(part, 10.0)) {
		albedo = vec3(0.22, 0.2, 0.2);
		rough = 0.35;
	} else {
		albedo = mix(vec3(0.25, 0.08, 0.05), vec3(0.83, 0.66, 0.3), bright);
		rough = 0.35;
	}

	// A rim of light about the whole figure: gold, silver or red as it leans.
	float rim = pow(1.0 - clamp(dot(NORMAL, VIEW), 0.0, 1.0), 3.0);
	glow += halo_of(lean) * rim * (0.3 + 0.7 * formed) * 0.45;

	ALBEDO = albedo;
	EMISSION = glow;
	BACKLIGHT = through;
	ROUGHNESS = rough;
	SPECULAR = 0.3;
}
