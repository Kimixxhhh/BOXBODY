extends SceneTree

func _initialize() -> void:
	var project := ProjectSettings.globalize_path("res://")
	var source := project.path_join("misc/Props/StreetProps")
	var manifest = JSON.parse_string(FileAccess.get_file_as_string(source.path_join("manifest.json")))
	for record in manifest.models:
		var state := GLTFState.new()
		var document := GLTFDocument.new()
		var path := project.path_join("assets/environmentObjects/" + record.id + ".glb")
		assert(document.append_from_file(path, state) == OK, "GLB parse failed: " + path)
		var scene := document.generate_scene(state)
		assert(scene != null, "GLB scene generation failed")
		var triangles := 0
		var mesh_count := 0
		for child in scene.find_children("*", "MeshInstance3D", true, false):
			mesh_count += 1
			var mesh: Mesh = child.mesh
			for surface in mesh.get_surface_count():
				var arrays := mesh.surface_get_arrays(surface)
				triangles += arrays[Mesh.ARRAY_INDEX].size() / 3
				assert(mesh.surface_get_material(surface) != null, "Missing material")
		assert(triangles == int(record.triangles), "Triangle count mismatch")
		if record.id == "trash_bin":
			assert(scene.find_child("Lid",true,false) != null, "Missing lid pivot")
		if record.id == "chain_link_fence":
			assert(scene.find_child("Snap_Left",true,false) != null, "Missing snap marker")
		if record.id == "street_lamp":
			assert(scene.find_child("LightSocket",true,false) != null, "Missing light socket")
		print("PASS: %s / %d triangles / %d meshes" % [record.id, triangles, mesh_count])
		scene.free()
	quit()
