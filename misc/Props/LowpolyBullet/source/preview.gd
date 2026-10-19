extends SceneTree

var world: Node3D

func _initialize() -> void:
	call_deferred("build_preview")

func add_prop(file: String, position: Vector3, angles: Vector3, size: float) -> Node3D:
	var packed := load("res://assets/weapons/ammo/" + file) as PackedScene
	assert(packed != null, "Could not import " + file)
	var prop := packed.instantiate() as Node3D
	world.add_child(prop)
	prop.position = position
	prop.rotation_degrees = angles
	prop.scale = Vector3.ONE * size
	return prop

func add_light(angles: Vector3, color: Color, energy: float, shadows: bool) -> void:
	var light := DirectionalLight3D.new()
	world.add_child(light)
	light.rotation_degrees = angles
	light.light_color = color
	light.light_energy = energy
	light.shadow_enabled = shadows
	light.directional_shadow_max_distance = 12
	light.shadow_bias = .03

func add_label(text: String, position: Vector2, size: int, color: Color) -> void:
	var label := Label.new()
	label.text = text
	label.position = position
	label.add_theme_font_size_override("font_size", size)
	label.add_theme_color_override("font_color", color)
	root.add_child(label)

func build_preview() -> void:
	root.size = Vector2i(1440, 1080)
	root.content_scale_size = Vector2i(1440, 1080)
	world = Node3D.new()
	root.add_child(world)
	var env := WorldEnvironment.new()
	env.environment = Environment.new()
	env.environment.background_mode = Environment.BG_COLOR
	env.environment.background_color = Color("20292c")
	env.environment.ambient_light_source = Environment.AMBIENT_SOURCE_COLOR
	env.environment.ambient_light_color = Color("b3c5c9")
	env.environment.ambient_light_energy = .55
	env.environment.reflected_light_source = Environment.REFLECTION_SOURCE_SKY
	var sky := Sky.new()
	var sky_material := ProceduralSkyMaterial.new()
	sky_material.sky_top_color = Color("a6c0cf")
	sky_material.sky_horizon_color = Color("e6e5d8")
	sky_material.ground_bottom_color = Color("384348")
	sky_material.ground_horizon_color = Color("aaa89c")
	sky.sky_material = sky_material
	env.environment.sky = sky
	world.add_child(env)
	add_light(Vector3(-45, -28, 0), Color("fff0d1"), 1.35, true)
	add_light(Vector3(-25, 135, 0), Color("b9d8f3"), .75, false)
	add_light(Vector3(-65, -160, 0), Color("fff1c9"), .55, false)
	var floor_mesh := MeshInstance3D.new()
	floor_mesh.mesh = PlaneMesh.new()
	floor_mesh.mesh.size = Vector2(200, 200)
	var floor_material := StandardMaterial3D.new()
	floor_material.albedo_color = Color("303c3f")
	floor_material.roughness = .9
	floor_mesh.material_override = floor_material
	world.add_child(floor_mesh)
	add_prop("bullet_ak_triple.glb", Vector3.ZERO, Vector3.ZERO, 36)
	var camera := Camera3D.new()
	world.add_child(camera)
	camera.projection = Camera3D.PROJECTION_ORTHOGONAL
	camera.size = 3.45
	camera.position = Vector3(0, 4.8, 3.0)
	camera.look_at(Vector3(0, .10, 0))
	camera.near = .01
	camera.current = true
	add_label("FIELD KIT  /  AMMUNITION", Vector2(72, 58), 24, Color("a4b8b9"))
	add_label("THREE ROUNDS / HORIZONTAL", Vector2(70, 96), 42, Color("f0ede2"))
	add_label("12 SIDES   /   FLAT SHADING   /   EMBEDDED MATERIALS", Vector2(72, 157), 19, Color("9badae"))
	add_label("3 COMPLETE ROUNDS   /   PARALLEL   /   1,554 TRIANGLES", Vector2(72, 991), 20, Color("bec9c6"))
	add_label("GLB / OBJ", Vector2(1230, 991), 20, Color("cba765"))
	for frame in range(30):
		await process_frame
	await RenderingServer.frame_post_draw
	var capture := root.get_texture().get_image()
	var path := "res://misc/Props/LowpolyBullet/previews/bullet_triple_preview.png"
	assert(capture.save_png(path) == OK, "Could not save preview")
	print("PASS: Three-round horizontal GLB imported and rendered. Preview saved: " + path)
	quit()
