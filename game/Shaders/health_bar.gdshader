// A Creature's health over its head in a fight (ADR-062): a bar that always faces the camera and
// draws over everything, green when well and red when near defeat, on a dark track with a rim.
shader_type spatial;
render_mode unshaded, depth_test_disabled, cull_disabled, shadows_disabled, fog_disabled;

instance uniform float health : hint_range(0.0, 1.0) = 1.0;

void vertex() {
	// Face the camera, keeping the bar's own size whatever the Creature's.
	MODELVIEW_MATRIX = VIEW_MATRIX * mat4(INV_VIEW_MATRIX[0], INV_VIEW_MATRIX[1], INV_VIEW_MATRIX[2], MODEL_MATRIX[3]);
}

void fragment() {
	float rim = step(UV.x, 0.015) + step(0.985, UV.x) + step(UV.y, 0.12) + step(0.88, UV.y);
	vec3 fill = mix(vec3(0.95, 0.25, 0.18), vec3(0.45, 0.9, 0.4), smoothstep(0.2, 0.7, health));
	vec3 color = UV.x < health ? fill : vec3(0.06, 0.06, 0.07);
	ALBEDO = mix(color, vec3(0.0), clamp(rim, 0.0, 1.0));
	ALPHA = 0.92;
}
