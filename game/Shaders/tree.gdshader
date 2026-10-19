// Trees (ADR-033): the trunk keeps its vertex colour; the crown (UV.x 1) takes the tint each instance
// carries in its custom data, so one mesh shows every shade, snowy or not.
shader_type spatial;
render_mode diffuse_lambert, specular_disabled;

varying vec3 tint;

void vertex() {
	tint = INSTANCE_CUSTOM.rgb;
}

void fragment() {
	ALBEDO = mix(COLOR.rgb, tint, UV.x);
	ROUGHNESS = 0.95;
}
