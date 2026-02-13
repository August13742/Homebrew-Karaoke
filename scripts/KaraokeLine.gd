extends Control

# --- Configuration ---
var font: Font
var font_size: int = 48
var active_color: Color = Color(1, 1, 0)
var inactive_color: Color = Color(1, 1, 1)

# --- State ---
var _content_node: Control
var _words: Array = [] 
var _word_nodes: Array = [] 
var _total_width: float = 0.0
var _current_active_index: int = -1

func setup(words_data: Array, theme_font: Font, p_size: int, col_active: Color, col_inactive: Color, max_width: float = 0.0) -> void:
	# Clear previous children
	for child in get_children():
		child.queue_free()
	_word_nodes.clear()
	_current_active_index = -1
	
	_words = words_data
	font = theme_font
	font_size = p_size
	active_color = col_active
	inactive_color = col_inactive
	
	_content_node = Control.new()
	_content_node.name = "Content"
	add_child(_content_node)
	
	var space_width: float = font.get_string_size(" ", HORIZONTAL_ALIGNMENT_LEFT, -1, font_size).x
	var current_x: float = 0.0
	
	for i in range(_words.size()):
		var w_data: Dictionary = _words[i]
		var text: String = w_data["text"]
		var word_width: float = font.get_string_size(text, HORIZONTAL_ALIGNMENT_LEFT, -1, font_size).x
		
		var root: Control = Control.new()
		root.position = Vector2(current_x, 0)
		root.custom_minimum_size = Vector2(word_width, font_size)
		_content_node.add_child(root)
		
		var lbl_in: Label = Label.new()
		lbl_in.text = text
		lbl_in.add_theme_font_override("font", font)
		lbl_in.add_theme_font_size_override("font_size", font_size)
		lbl_in.modulate = inactive_color
		lbl_in.modulate.a = 0.3 
		lbl_in.position = Vector2(0, 0)
		root.add_child(lbl_in)
		
		var clipper: Control = Control.new()
		clipper.clip_contents = true
		clipper.mouse_filter = Control.MOUSE_FILTER_IGNORE
		clipper.position = Vector2(0, 0)
		clipper.size = Vector2(0, font_size * 2.5) # Extra height for bounce
		root.add_child(clipper)
		
		var lbl_act: Label = Label.new()
		lbl_act.text = text
		lbl_act.add_theme_font_override("font", font)
		lbl_act.add_theme_font_size_override("font_size", font_size)
		lbl_act.modulate = active_color
		lbl_act.position = Vector2(0, 0)
		lbl_act.set_anchors_and_offsets_preset(Control.PRESET_TOP_LEFT)
		clipper.add_child(lbl_act)
		
		_word_nodes.append({
			"root": root,
			"clipper": clipper,
			"width": word_width,
			"data": w_data,
			"active_lbl": lbl_act,
			"inactive_lbl": lbl_in
		})
		
		current_x += word_width + space_width

	_total_width = current_x - space_width 
	# Root size reported to container remains constrained or matches max_width
	custom_minimum_size = Vector2(min(_total_width, max_width if max_width > 0 else 9999.0), font_size)
	update_width(max_width)

func update_width(max_width: float) -> void:
	if not _content_node:
		return
		
	if max_width > 0 and _total_width > max_width:
		var s: float = max_width / _total_width
		_content_node.scale = Vector2(s, s)
		# Center the content node within the root
		_content_node.position.x = (custom_minimum_size.x - _total_width * s) / 2.0
		# Pivot from center of content for internal word animations
		_content_node.pivot_offset = Vector2(0, font_size / 2.0) 
	else:
		_content_node.scale = Vector2.ONE
		_content_node.position.x = (custom_minimum_size.x - _total_width) / 2.0
		_content_node.pivot_offset = Vector2.ZERO

func update_time(time: float, is_active_line: bool) -> void:
	var target_alpha: float = 1.0 if is_active_line else 0.3
	modulate.a = move_toward(modulate.a, target_alpha, 0.1) 
	
	if not is_active_line:
		# VISUAL POLISH: Force all words to 100% fill if the line is inactive
		# but its end time has passed. This ensures smooth transitions.
		for node: Dictionary in _word_nodes:
			if time >= node["data"]["end"]:
				node["clipper"].size.x = node["width"]
				
		if _current_active_index != -1:
			_reset_word_visuals(_current_active_index)
			_current_active_index = -1
		return

	var active_idx: int = -1
	for i: int in range(_word_nodes.size()):
		var node: Dictionary = _word_nodes[i]
		var w_start: float = node["data"]["start"]
		var w_end: float = node["data"]["end"]
		
		var pct: float = 0.0
		if time >= w_end:
			pct = 1.0
		elif time > w_start:
			pct = (time - w_start) / (w_end - w_start)
			active_idx = i
		
		node["clipper"].size.x = node["width"] * pct
	
	# Handle Word Transitions (Tweens)
	if active_idx != _current_active_index:
		if _current_active_index != -1:
			_reset_word_visuals(_current_active_index)
		
		if active_idx != -1:
			_animate_word_active(active_idx)
		
		_current_active_index = active_idx

func _animate_word_active(idx: int) -> void:
	var node: Dictionary = _word_nodes[idx]
	var root: Control = node["root"]
	root.pivot_offset = Vector2(node["width"]/2.0, font_size/2.0)
	
	var tween: Tween = create_tween()
	tween.set_parallel(true)
	tween.set_trans(Tween.TRANS_ELASTIC)
	tween.set_ease(Tween.EASE_OUT)
	
	# Scale animation (Dialogic-style "pop")
	tween.tween_property(root, "scale", Vector2(1.2, 1.2), 0.4).from(Vector2(1.0, 1.0))
	# Subtle vertical jump
	tween.tween_property(root, "position:y", -10.0, 0.3).from(0.0)
	# Brighten active label
	tween.tween_property(node["active_lbl"], "modulate", Color(1.2, 1.2, 1.2), 0.2)

func _reset_word_visuals(idx: int) -> void:
	var node: Dictionary = _word_nodes[idx]
	var root: Control = node["root"]
	
	var tween: Tween = create_tween()
	tween.set_parallel(true)
	tween.set_trans(Tween.TRANS_SINE)
	tween.set_ease(Tween.EASE_IN_OUT)
	
	tween.tween_property(root, "scale", Vector2(1.0, 1.0), 0.2)
	tween.tween_property(root, "position:y", 0.0, 0.2)
	tween.tween_property(node["active_lbl"], "modulate", active_color, 0.2)