## Headless scene operations for Wick MCP server (Sub-spec C).
##
## Invoked via: godot --headless --path <project> --script addons/wick/scene_ops.gd -- <operation> <json_args>
##
## Writes a single JSON result line to stdout:
##   {"ok":true,"result":{...}}  or  {"ok":false,"error":{"code":"...","message":"..."}}
##
## Operations: create_scene, add_node, set_properties, save_scene, load_resource
extends SceneTree


func _init() -> void:
	var args := _parse_args()
	if args.is_empty():
		_error("invalid_args", "Usage: --script scene_ops.gd -- <operation> <json_args>")
		return

	var operation: String = args[0]
	var json_str: String = args[1] if args.size() > 1 else "{}"
	var parsed = JSON.parse_string(json_str)
	if parsed == null:
		_error("invalid_args", "Failed to parse JSON arguments")
		return

	var params: Dictionary = parsed if parsed is Dictionary else {}

	match operation:
		"create_scene":
			_create_scene(params)
		"add_node":
			_add_node(params)
		"set_properties":
			_set_properties(params)
		"save_scene":
			_save_scene(params)
		"load_resource":
			_load_resource(params)
		_:
			_error("invalid_args", "Unknown operation: %s" % operation)

	quit()


## Parse CLI args after the "--" separator.
func _parse_args() -> Array[String]:
	var all_args := OS.get_cmdline_args()
	var result: Array[String] = []
	var after_separator := false
	for arg in all_args:
		if arg == "--":
			after_separator = true
			continue
		if after_separator:
			result.append(arg)

	# Also check user args (Godot 4.x provides get_cmdline_user_args for args after --)
	if result.is_empty():
		var user_args := OS.get_cmdline_user_args()
		for arg in user_args:
			result.append(arg)

	return result


# ── Operations ────────────────────────────────────────────────────────────────


func _create_scene(params: Dictionary) -> void:
	var path: String = params.get("path", "")
	var root_type: String = params.get("root_type", "")

	if path.is_empty() or root_type.is_empty():
		_error("invalid_args", "create_scene requires 'path' and 'root_type'")
		return

	if not ClassDB.class_exists(root_type):
		# Check global script classes as fallback
		var found_script := false
		for cls in ProjectSettings.get_global_class_list():
			if cls.get("class", "") == root_type:
				found_script = true
				push_warning("Wick: Instantiating user script class '%s' — this executes project code." % root_type)
				break
		if not found_script:
			_error("type_not_found", "Type '%s' not found in ClassDB or global script classes" % root_type)
			return

	var root_node := _instantiate_node(root_type)
	if root_node == null:
		_error("internal", "Failed to instantiate type '%s'" % root_type)
		return

	root_node.name = path.get_file().get_basename()

	var scene := PackedScene.new()
	var err := scene.pack(root_node)
	root_node.queue_free()

	if err != OK:
		_error("internal", "Failed to pack scene: error %d" % err)
		return

	err = ResourceSaver.save(scene, path)
	if err != OK:
		_error("save_failed", "Failed to save scene to '%s': error %d" % [path, err])
		return

	_success({"scene_path": path, "node_name": root_node.name, "node_type": root_type})


func _add_node(params: Dictionary) -> void:
	var scene_path: String = params.get("scene_path", "")
	var parent_path: String = params.get("parent_path", ".")
	var type: String = params.get("type", "")
	var node_name: String = params.get("name", "")
	var properties: Dictionary = params.get("properties", {})

	if scene_path.is_empty() or type.is_empty():
		_error("invalid_args", "add_node requires 'scene_path' and 'type'")
		return

	if not ClassDB.class_exists(type):
		_error("type_not_found", "Type '%s' not found in ClassDB" % type)
		return

	var packed := _load_scene(scene_path)
	if packed == null:
		return  # _load_scene already emitted the error

	var scene_root: Node = packed.instantiate()
	var parent: Node = scene_root.get_node_or_null(NodePath(parent_path))
	if parent == null:
		scene_root.queue_free()
		_error("node_not_found", "Parent node '%s' not found in scene" % parent_path)
		return

	var new_node := _instantiate_node(type)
	if new_node == null:
		scene_root.queue_free()
		_error("internal", "Failed to instantiate type '%s'" % type)
		return

	if not node_name.is_empty():
		new_node.name = node_name
	new_node.owner = scene_root

	for key in properties:
		if new_node.has_method("set"):
			new_node.set(key, _coerce_value(properties[key]))

	parent.add_child(new_node)
	new_node.owner = scene_root

	var err := _save_packed(scene_root, scene_path)
	var result_name := new_node.name
	scene_root.queue_free()

	if err != OK:
		_error("save_failed", "Failed to save scene after adding node: error %d" % err)
		return

	_success({"scene_path": scene_path, "node_name": result_name, "node_type": type})


func _set_properties(params: Dictionary) -> void:
	var scene_path: String = params.get("scene_path", "")
	var node_path: String = params.get("node_path", ".")
	var properties: Dictionary = params.get("properties", {})

	if scene_path.is_empty() or properties.is_empty():
		_error("invalid_args", "set_properties requires 'scene_path' and non-empty 'properties'")
		return

	var packed := _load_scene(scene_path)
	if packed == null:
		return

	var scene_root: Node = packed.instantiate()
	var target: Node = scene_root.get_node_or_null(NodePath(node_path))
	if target == null:
		scene_root.queue_free()
		_error("node_not_found", "Node '%s' not found in scene" % node_path)
		return

	for key in properties:
		target.set(key, _coerce_value(properties[key]))

	var err := _save_packed(scene_root, scene_path)
	var node_name := target.name
	var node_type := target.get_class()
	scene_root.queue_free()

	if err != OK:
		_error("save_failed", "Failed to save scene after setting properties: error %d" % err)
		return

	_success({"scene_path": scene_path, "node_name": node_name, "node_type": node_type})


func _save_scene(params: Dictionary) -> void:
	var scene_path: String = params.get("scene_path", "")
	var save_as: String = params.get("save_as", "")

	if scene_path.is_empty():
		_error("invalid_args", "save_scene requires 'scene_path'")
		return

	var packed := _load_scene(scene_path)
	if packed == null:
		return

	var target_path := save_as if not save_as.is_empty() else scene_path
	var err := ResourceSaver.save(packed, target_path)
	if err != OK:
		_error("save_failed", "Failed to save scene to '%s': error %d" % [target_path, err])
		return

	_success({"scene_path": target_path})


func _load_resource(params: Dictionary) -> void:
	var scene_path: String = params.get("scene_path", "")
	var node_path: String = params.get("node_path", ".")
	var property: String = params.get("property", "")
	var resource_path: String = params.get("resource_path", "")

	if scene_path.is_empty() or property.is_empty() or resource_path.is_empty():
		_error("invalid_args", "load_resource requires 'scene_path', 'property', and 'resource_path'")
		return

	# resource_path must be a project-relative res:// URI. Reject anything that could
	# reach outside the project tree (file://, user://, absolute paths, ..).
	if not _is_safe_res_path(resource_path):
		_error("invalid_args",
			"resource_path must be a project-local res:// path without '..': '%s'" % resource_path)
		return

	var packed := _load_scene(scene_path)
	if packed == null:
		return

	var res := ResourceLoader.load(resource_path)
	if res == null:
		_error("resource_not_found", "Resource not found at '%s'" % resource_path)
		return

	var scene_root: Node = packed.instantiate()
	var target: Node = scene_root.get_node_or_null(NodePath(node_path))
	if target == null:
		scene_root.queue_free()
		_error("node_not_found", "Node '%s' not found in scene" % node_path)
		return

	target.set(property, res)

	var err := _save_packed(scene_root, scene_path)
	var node_name := target.name
	var node_type := target.get_class()
	scene_root.queue_free()

	if err != OK:
		_error("save_failed", "Failed to save scene after loading resource: error %d" % err)
		return

	_success({"scene_path": scene_path, "node_name": node_name, "node_type": node_type})


# ── Helpers ───────────────────────────────────────────────────────────────────


func _is_safe_res_path(path: String) -> bool:
	# Must start with res://, must not contain .., must not contain other URI schemes.
	if not path.begins_with("res://"):
		return false
	if path.contains(".."):
		return false
	return true


func _instantiate_node(type_name: String) -> Node:
	if ClassDB.class_exists(type_name) and ClassDB.can_instantiate(type_name):
		var obj = ClassDB.instantiate(type_name)
		if obj is Node:
			return obj
	return null


func _load_scene(path: String) -> PackedScene:
	if not ResourceLoader.exists(path):
		_error("scene_not_found", "Scene file not found: '%s'" % path)
		return null
	var res := ResourceLoader.load(path)
	if res is PackedScene:
		return res
	_error("scene_not_found", "Resource at '%s' is not a PackedScene" % path)
	return null


func _save_packed(scene_root: Node, path: String) -> int:
	var packed := PackedScene.new()
	var err := packed.pack(scene_root)
	if err != OK:
		return err
	return ResourceSaver.save(packed, path)


## Coerce string property values to appropriate Godot types.
## For v1 this handles basic numeric coercion; complex types (Vector2, Color, etc.)
## stay as strings and Godot's property setter handles the conversion.
func _coerce_value(value: Variant) -> Variant:
	if value is String:
		var s: String = value
		if s.is_valid_float():
			return s.to_float()
		if s.is_valid_int():
			return s.to_int()
		if s == "true":
			return true
		if s == "false":
			return false
	return value


func _success(result: Dictionary) -> void:
	print(JSON.stringify({"ok": true, "result": result}))


func _error(code: String, message: String) -> void:
	print(JSON.stringify({"ok": false, "error": {"code": code, "message": message}}))
