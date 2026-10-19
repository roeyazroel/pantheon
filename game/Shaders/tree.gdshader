// Trees and ground cover (ADR-033, ADR-053). The mesh (TreeMesh, GroundCoverMesh) tags each
// vertex's part in UV.y: 0 the trunk or stem, 1 the leaves. Each instance carries its leaves'
// colour (linear) in INSTANCE_CUSTOM.rgb and its sway's phase in .a; each node its trunk's colour.
// Everything bends with the wind by its height, most at the top, and leaves flutter.
shader_type spatial;
render_mode diffuse_lambert, specular_disabled, cull_disabled;

uniform vec2 wind = vec2(0.15, 0.0);
uniform float gust_speed = 1.2;
uniform float reference_height = 7.0;
instance uniform vec3 trunk_color = vec3(0.12, 0.08, 0.05);

varying vec3 tint;
varying float leaf;

void vertex() {
	leaf = UV.y;
	tint = INSTANCE_CUSTOM.rgb;
	float phase = INSTANCE_CUSTOM.a * TAU;
	float rise = max(VERTEX.y, 0.0) / reference_height;
	float gust = 0.55 + 0.45 * sin(TIME * gust_speed + phase) + 0.2 * sin(TIME * gust_speed * 2.7 + phase * 1.3);
	vec3 sway = vec3(wind.x, 0.0, wind.y) * gust * rise * rise;
	float flutter = 0.04 * leaf * (0.3 + length(wind) * 3.0);
	sway += vec3(sin(TIME * 4.1 + phase + VERTEX.x * 2.3), 0.0, cos(TIME * 3.7 + phase + VERTEX.z * 2.1)) * flutter;
	// The instance is turned and scaled evenly, so its rotation's transpose over the scale squared undoes it.
	mat3 basis = mat3(MODEL_MATRIX);
	VERTEX += transpose(basis) * sway / dot(basis[0], basis[0]);
}

void fragment() {
	ALBEDO = mix(trunk_color, tint, leaf);
	ROUGHNESS = 0.95;
}
