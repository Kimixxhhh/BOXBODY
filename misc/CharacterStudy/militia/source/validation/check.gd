extends SceneTree

var errors := 0
var mesh_count := 0
var triangle_count := 0
var material_counts := {"ForestCamouflage": 0, "Skin": 0, "BlackBoots": 0}

func check(condition: bool, message: String):
	if not condition:
		push_error(message)
		errors += 1

func _initialize():
	var path := ProjectSettings.globalize_path("res://../../../../assets/characters/militia.glb")
	var doc := GLTFDocument.new()
	var state := GLTFState.new()
	var result := doc.append_from_file(path, state)
	check(result == OK, "Could not import edited militia GLB")
	if result != OK:
		quit(errors)
		return
	var scene := doc.generate_scene(state)
	inspect(scene)
	check(mesh_count == 9, "Object count changed")
	check(triangle_count == 160, "Unexpected triangle count after splitting sleeve boundaries")
	check(state.get_materials().size() == 3, "Expected three materials")
	check(material_counts == {"ForestCamouflage": 6, "Skin": 3, "BlackBoots": 2}, "Material assignments differ from requested body parts")
	print("MILITIA_IMPORT meshes=", mesh_count, " triangles=", triangle_count, " materials=", material_counts, " errors=", errors)
	scene.free()
	quit(errors)

func inspect(node: Node):
	if node is MeshInstance3D:
		mesh_count += 1
		if str(node.name).begins_with("handLeft") or str(node.name).begins_with("handRight"):
			check(node.mesh.get_surface_count() == 2, "Forearm must contain separate camouflage and skin surfaces")
			var sleeve: Array = node.mesh.surface_get_arrays(0)
			var hand: Array = node.mesh.surface_get_arrays(1)
			check(node.mesh.surface_get_material(1).resource_name == "Skin", "Hand tip must use head skin material")
			var max_sleeve_y := -INF
			var min_skin_y := INF
			for vertex in sleeve[Mesh.ARRAY_VERTEX]: max_sleeve_y = max(max_sleeve_y, vertex.y)
			for vertex in hand[Mesh.ARRAY_VERTEX]: min_skin_y = min(min_skin_y, vertex.y)
			check(abs(max_sleeve_y - min_skin_y) < 0.0001, "Skin and sleeve boundaries must meet without a gap")
		for i in node.mesh.get_surface_count():
			var arrays: Array = node.mesh.surface_get_arrays(i)
			triangle_count += arrays[Mesh.ARRAY_INDEX].size() / 3
			var material := node.mesh.surface_get_material(i) as StandardMaterial3D
			check(material != null, "Missing material")
			if material == null: continue
			check(material_counts.has(material.resource_name), "Unexpected material name")
			if material_counts.has(material.resource_name): material_counts[material.resource_name] += 1
			if material.resource_name == "ForestCamouflage":
				check(material.albedo_texture != null, "Camouflage texture missing after import")
				if material.albedo_texture != null:
					check(material.albedo_texture.get_width() == 1254 and material.albedo_texture.get_height() == 1254, "Camouflage texture dimensions mismatch")
				check(arrays[Mesh.ARRAY_TEX_UV].size() == arrays[Mesh.ARRAY_VERTEX].size(), "Missing UVs")
				for uv in arrays[Mesh.ARRAY_TEX_UV]: check(uv.is_finite() and uv.x >= 0 and uv.y >= 0 and uv.x <= 1 and uv.y <= 1, "Invalid texture coordinates")
	for child in node.get_children():inspect(child)
