extends SceneTree

var errors := 0

func _initialize():
	var base := ProjectSettings.globalize_path("res://../../models/")
	for id in ["ak47", "m870", "rpg7"]:
		var doc := GLTFDocument.new()
		var state := GLTFState.new()
		var result := doc.append_from_file(base + id + ".glb", state)
		if result != OK:
			push_error("Import failed: " + id)
			errors += 1
			continue
		var scene := doc.generate_scene(state)
		var stats := inspect(scene)
		if stats.x == 0 or stats.y == 0:
			push_error("Empty mesh: " + id)
			errors += 1
		if scene.find_child("MuzzleSocket", true, false) == null:
			push_error("Missing muzzle socket: " + id)
			errors += 1
		print("PASS ", id, " meshes=", stats.x, " triangles=", stats.y, " materials=", state.get_materials().size())
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
			for normal in arrays[Mesh.ARRAY_NORMAL]:
				if not normal.is_finite() or abs(normal.length() - 1.0) > 0.001:
					errors += 1
	for child in node.get_children():
		stats += inspect(child)
	return stats
