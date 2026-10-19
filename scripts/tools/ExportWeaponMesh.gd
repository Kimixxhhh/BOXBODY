extends SceneTree

# Standalone maintenance bridge: extract actual scene geometry for the native
# desktop Viewport3D. No game scene, image file, or icon is generated.
var surfaces: Array = []
var vertex_count := 0

func _initialize() -> void:
	var args := OS.get_cmdline_user_args()
	if args.size() != 2:
		fail("需要武器场景和输出路径。")
		return
	var packed = load(args[0])
	if not packed is PackedScene:
		fail("无法读取武器场景，请先在 Godot 中导入资源。")
		return
	var model = packed.instantiate()
	if not model is Node3D:
		model.free()
		fail("武器场景的根节点必须是 Node3D。")
		return
	# Do not enter the tree: gameplay _ready/process callbacks never run.
	walk(model, Transform3D.IDENTITY)
	model.free()
	if surfaces.is_empty() or vertex_count > 300000:
		fail("场景没有可预览的三角网格，或超过 30 万顶点。")
		return
	var output := FileAccess.open(args[1], FileAccess.WRITE)
	if output == null:
		fail("无法创建模型预览数据。")
		return
	output.store_string(JSON.stringify({"surfaces": surfaces}))
	output.close()
	quit(0)

func walk(node: Node, parent_transform: Transform3D) -> void:
	if vertex_count > 300000:
		return
	var transform := parent_transform
	if node is Node3D:
		if not node.visible:
			return
		transform = transform * node.transform
	if node is MeshInstance3D and node.mesh != null:
		for surface_index in node.mesh.get_surface_count():
			if node.mesh.surface_get_primitive_type(surface_index) != Mesh.PRIMITIVE_TRIANGLES:
				continue
			var arrays = node.mesh.surface_get_arrays(surface_index)
			var vertices: PackedVector3Array = arrays[Mesh.ARRAY_VERTEX]
			var normals = arrays[Mesh.ARRAY_NORMAL]
			var indices = arrays[Mesh.ARRAY_INDEX]
			vertex_count += vertices.size()
			if vertex_count > 300000:
				return
			var positions: Array = []
			var directions: Array = []
			var triangles: Array = []
			var normal_transform := transform.basis.inverse().transposed()
			for i in vertices.size():
				var point: Vector3 = transform * vertices[i]
				positions.append_array([point.x, point.y, point.z])
				if normals != null and normals.size() == vertices.size():
					var direction: Vector3 = (normal_transform * normals[i]).normalized()
					directions.append_array([direction.x, direction.y, direction.z])
			if indices != null and not indices.is_empty():
				for i in range(0, indices.size() - 2, 3):
					triangles.append_array([indices[i], indices[i+2], indices[i+1]])
			else:
				for i in range(0, vertices.size() - 2, 3):
					triangles.append_array([i, i+2, i+1])
			var material = node.get_active_material(surface_index)
			var color := Color(0.55, 0.58, 0.56)
			if material is BaseMaterial3D:
				color = material.albedo_color
			surfaces.append({"positions": positions, "normals": directions, "indices": triangles,
				"color": [color.r, color.g, color.b, color.a]})
	for child in node.get_children():
		walk(child, transform)

func fail(message: String) -> void:
	printerr(message)
	quit(1)
