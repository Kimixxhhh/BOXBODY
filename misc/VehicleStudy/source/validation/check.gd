extends SceneTree

var errors := 0

func expect(condition: bool, message: String):
	if not condition:
		push_error(message)
		errors += 1

func _initialize():
	var base := ProjectSettings.globalize_path("res://../../models/")
	var manifest = JSON.parse_string(FileAccess.get_file_as_string("res://../../manifest.json"))
	for model in manifest.models:
		var doc := GLTFDocument.new()
		var state := GLTFState.new()
		var result := doc.append_from_file(base + model.id + ".glb", state)
		expect(result == OK, "GLB import failed: " + model.id)
		if result != OK: continue
		var scene := doc.generate_scene(state)
		var stats := inspect(scene)
		expect(stats.x == int(model.parts) and stats.y == int(model.triangles), "Geometry count mismatch: " + model.id)
		expect(state.get_materials().size() == int(model.materials), "Material count mismatch: " + model.id)
		for wheel_name in model.wheelGroups:
			var wheel := scene.find_child(wheel_name, true, false) as Node3D
			expect(wheel != null, "Missing wheel: " + wheel_name)
			if wheel == null: continue
			expect(wheel.get_child_count() == 9, "Wheel geometry is not grouped: " + wheel_name)
			var tire := wheel.get_child(0) as MeshInstance3D
			var bounds := tire.get_aabb()
			var center := bounds.get_center()
			expect(abs(center.y) < 0.0001 and abs(center.z) < 0.0001, "Wheel pivot is not at axle: " + wheel_name)
			expect(abs(wheel.position.y - bounds.size.y / 2.0) < 0.0001, "Wheel does not touch ground: " + wheel_name)
			expect((wheel.position.z < 0) == ("Front" in wheel_name), "Front direction mismatch")
			expect((wheel.position.x < 0) == ("Left" in wheel_name), "Wheel side mismatch")
		for socket in ["GroundOrigin", "FrontSocket", "RearSocket"]:
			expect(scene.find_child(socket, true, false) != null, "Missing marker: " + socket)
		print("PASS ", model.id, " meshes=", stats.x, " triangles=", stats.y, " materials=", state.get_materials().size(), " wheels=4")
		scene.free()
	print("GODOT_ASSET_VALIDATION errors=", errors)
	quit(errors)

func inspect(node: Node) -> Vector2i:
	var stats := Vector2i.ZERO
	if node is MeshInstance3D:
		stats.x += 1
		var mesh := node.mesh as ArrayMesh
		for i in mesh.get_surface_count():
			var arrays := mesh.surface_get_arrays(i)
			stats.y += arrays[Mesh.ARRAY_INDEX].size() / 3
			for position in arrays[Mesh.ARRAY_VERTEX]:
				expect(position.is_finite(), "Non-finite position")
			for normal in arrays[Mesh.ARRAY_NORMAL]:
				expect(normal.is_finite() and abs(normal.length() - 1.0) < 0.001, "Invalid normal")
	for child in node.get_children(): stats += inspect(child)
	return stats
