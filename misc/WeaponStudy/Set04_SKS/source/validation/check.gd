extends SceneTree

var errors := 0

func _initialize():
	var base := ProjectSettings.globalize_path("res://../../models/")
	var manifest = JSON.parse_string(FileAccess.get_file_as_string("res://../../manifest.json"))
	for model in manifest.models:
		var doc := GLTFDocument.new()
		var state := GLTFState.new()
		var result := doc.append_from_file(base + model.id + ".glb", state)
		if result != OK:
			push_error("Import failed: " + model.id)
			errors += 1
			continue
		var scene := doc.generate_scene(state)
		var stats := inspect(scene)
		if stats.x != int(model.parts) or stats.y != int(model.triangles):
			push_error("Imported geometry differs from manifest: " + model.id)
			errors += 1
		for socket in ["MuzzleSocket", "GripSocket", "SupportSocket"]:
			if scene.find_child(socket, true, false) == null:
				push_error("Missing socket: " + model.id + " " + socket)
				errors += 1
		if state.get_materials().size() != int(model.materials):
			push_error("Unexpected material count: " + model.id)
			errors += 1
		print("PASS ", model.id, " meshes=", stats.x, " triangles=", stats.y, " materials=", state.get_materials().size())
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
				if not position.is_finite(): errors += 1
			for normal in arrays[Mesh.ARRAY_NORMAL]:
				if not normal.is_finite() or abs(normal.length() - 1.0) > 0.001: errors += 1
	for child in node.get_children(): stats += inspect(child)
	return stats
