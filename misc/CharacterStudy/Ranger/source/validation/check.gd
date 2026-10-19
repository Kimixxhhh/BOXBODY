extends SceneTree

var errors := 0
var meshes := 0
var triangles := 0
var materials := {"DesertCamouflage": 0, "Skin": 0, "BlackBoots": 0}

func check(condition: bool, message: String):
	if not condition:
		push_error(message)
		errors += 1

func _initialize():
	var path := ProjectSettings.globalize_path("res://../../../../assets/characters/Ranger.glb")
	var document := GLTFDocument.new()
	var state := GLTFState.new()
	var result := document.append_from_file(path, state)
	check(result == OK, "Cannot import Ranger GLB")
	if result != OK:
		quit(errors)
		return
	var scene := document.generate_scene(state)
	inspect(scene)
	check(meshes == 9 and triangles == 174, "Ranger geometry count mismatch")
	check(materials == {"DesertCamouflage": 6, "Skin": 3, "BlackBoots": 2}, "Material assignments mismatch")
	check(state.get_materials().size() == 3, "Unexpected material count")
	print("RANGER_IMPORT meshes=", meshes, " triangles=", triangles, " materials=", materials, " errors=", errors)
	scene.free()
	quit(errors)

func inspect(node: Node):
	if node is MeshInstance3D:
		meshes += 1
		for i in node.mesh.get_surface_count():
			var arrays: Array = node.mesh.surface_get_arrays(i)
			triangles += arrays[Mesh.ARRAY_INDEX].size() / 3
			var material := node.mesh.surface_get_material(i) as StandardMaterial3D
			check(material != null, "Missing standard material")
			if material == null: continue
			check(materials.has(material.resource_name), "Unexpected material")
			if materials.has(material.resource_name): materials[material.resource_name] += 1
			if material.resource_name == "DesertCamouflage":
				check(material.albedo_texture != null, "Embedded desert texture did not load")
				if material.albedo_texture != null:
					check(material.albedo_texture.get_width() == 1254 and material.albedo_texture.get_height() == 1254, "Texture size mismatch")
				check(arrays[Mesh.ARRAY_TEX_UV].size() == arrays[Mesh.ARRAY_VERTEX].size(), "Missing texture coordinates")
	for child in node.get_children(): inspect(child)
