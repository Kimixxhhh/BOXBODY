extends SceneTree
var errors := 0

func fail(message: String):
	errors += 1
	push_error(message)

func _initialize():
	call_deferred("validate")

func validate():
	var base := ProjectSettings.globalize_path("res://../../models/")
	var manifest = JSON.parse_string(FileAccess.get_file_as_string("res://../../manifest.json"))
	for model in manifest.models:
		var doc := GLTFDocument.new()
		var state := GLTFState.new()
		if doc.append_from_file(base + model.id + ".glb", state) != OK:
			fail("Import failed: " + model.id)
			continue
		var scene := doc.generate_scene(state)
		root.add_child(scene)
		var player := scene.find_child("AnimationPlayer", true, false) as AnimationPlayer
		if player == null:
			fail("No animation player: " + model.id)
			scene.free()
			continue
		print("IMPORTED ", model.id, " clips=", player.get_animation_list())
		var rest_transforms: Dictionary = {}
		for name in ["body", "head", "armLeft", "handLeft", "armRight", "handRight", "ShoeLeft", "ShoeRight"]:
			rest_transforms[name] = (scene.find_child(name, true, false) as Node3D).transform
		for clip in ["walk_loop", "attack"]:
			if not player.has_animation(clip):
				fail("Missing animation: " + clip)
				continue
			var animation := player.get_animation(clip)
			var expected_length: float = model.walk_duration if clip == "walk_loop" else model.attack_duration
			if abs(animation.length - expected_length) > 0.002: fail("Incorrect duration: " + clip)
			if animation.loop_mode != (Animation.LOOP_LINEAR if clip == "walk_loop" else Animation.LOOP_NONE): fail("Incorrect loop mode: " + clip)
			var moving := false
			var previous: Dictionary = {}
			if clip == "attack":
				player.play("walk_loop")
				player.seek(float(model.walk_duration) * 0.37, true)
			player.play(clip)
			for frame in range(31):
				player.seek(animation.length * frame / 30.0, true)
				for name in ["body", "head", "armLeft", "handLeft", "armRight", "handRight", "ShoeLeft", "ShoeRight"]:
					var node := scene.find_child(name, true, false) as Node3D
					if node == null:
						fail("Missing node: " + name)
						continue
					if not node.global_transform.is_finite(): fail("Invalid animated transform")
					if clip == "attack" and (frame == 0 or frame == 30):
						if not node.transform.is_equal_approx(rest_transforms[name]): fail("Walk to attack does not reset " + name)
					if frame == 0: previous[name] = node.transform
					elif not node.transform.is_equal_approx(previous[name]): moving = true
					if name.begins_with("Shoe") and node is MeshInstance3D:
						for s in node.mesh.get_surface_count():
							for v in node.mesh.surface_get_arrays(s)[Mesh.ARRAY_VERTEX]:
								if (node.global_transform * v).y < -0.001: fail("Foot below ground: " + model.id + " " + clip)
			if not moving: fail("Animation does not move: " + clip)
			print("PLAYBACK ", clip, " length=", animation.length, " tracks=", animation.get_track_count(), " loop=", animation.loop_mode)
		scene.free()
	print("GODOT_ANIMATION_VALIDATION errors=", errors)
	quit(errors)
