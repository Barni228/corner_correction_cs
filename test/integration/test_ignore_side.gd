extends TestTemplate

func before_all() -> void:
	setup(preload("res://test/scenes/test_ignore_side.tscn"))

func test_can_do_without_ignore() -> void:
	watch_signals(area)
	var init_pos := player.position
	player.IgnoreSides = []
	var call_count := 0

	for key: int in [KEY_UP, KEY_RIGHT, KEY_DOWN, KEY_LEFT]:
		call_count += 1
		player.position = init_pos
		input_sender.key_down(key)
		await wait_for_signal(area.body_entered, 1)
		assert_signal_emit_count(area, "body_entered", call_count)
		input_sender.release_all()
		await wait_frames(5)

func test_ignore_single_side() -> void:
	watch_signals(area)
	area_should_be_reached(false)
	var init_pos := player.position

	for i: Array in [[Vector2.UP, KEY_UP], [Vector2.RIGHT, KEY_RIGHT], [Vector2.DOWN, KEY_DOWN], [Vector2.LEFT, KEY_LEFT]]:
		player.position = init_pos
		var side: Vector2 = i[0]
		var key: int = i[1]
		player.IgnoreSides = [side]
		input_sender.key_down(key)
		await wait_for_signal(area.body_entered, 1)
		assert_signal_not_emitted(area, "body_entered")
		input_sender.release_all()