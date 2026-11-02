// A village's fields (ADR-077): worked ground lying on the land. UV runs across the field, and the
// rows follow its U axis; the vertex colour is the crop. Furrows of bare soil show between the rows,
// the crop grows unevenly, and the field's edge is ragged so that it melts into the ground around it.
// Drawn over the ground and a little towards the eye, as the roads are.
shader_type spatial;
render_mode blend_mix, depth_draw_never, cull_disabled, diffuse_burley, specular_disabled;

uniform sampler2D noise : filter_linear_mipmap, repeat_enable;
uniform vec3 soil : source_color = vec3(0.32, 0.24, 0.17);
uniform float rows = 9.0;

varying vec3 world;
varying vec3 crop;

void vertex() {
	world = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
	crop = COLOR.rgb;
	vec3 eye = CAMERA_POSITION_WORLD - world;
	float away = length(eye);
	VERTEX += inverse(mat3(MODEL_MATRIX)) * (eye / away) * min(3.0, 0.15 + away * 0.004);
}

void fragment() {
	vec2 ground = world.xz;
	float broad = texture(noise, ground * 0.021).r;
	float blotch = texture(noise, ground * 0.09).g;
	float grain = texture(noise, ground * 0.5).b;
	// The rows bend a little with the land, and fade to one tone far off, where they would shimmer.
	float furrow = smoothstep(-0.35, 0.55, sin((UV.y * rows + (broad - 0.5) * 1.2) * TAU));
	float far = smoothstep(180.0, 700.0, length(world - CAMERA_POSITION_WORLD));
	vec3 growing = crop * (0.72 + 0.5 * blotch) * (0.82 + 0.36 * broad) * (0.9 + 0.2 * grain);
	vec3 bare = soil * (0.8 + 0.4 * grain);
	float patchy = smoothstep(0.3, 0.5, blotch + grain * 0.2);
	ALBEDO = mix(mix(bare, growing, furrow * patchy), growing * 0.86, far);
	float edge = min(min(UV.x, 1.0 - UV.x), min(UV.y, 1.0 - UV.y));
	ALPHA = smoothstep(0.0, 0.07 + 0.08 * grain, edge + (blotch - 0.5) * 0.1);
	ROUGHNESS = 1.0;
	// The field faces up whichever way its triangles are wound.
	NORMAL = normalize((VIEW_MATRIX * vec4(0.0, 1.0, 0.0, 0.0)).xyz);
}
