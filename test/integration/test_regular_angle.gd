extends TestTemplate

func before_all() -> void:
	setup(preload("res://test/scenes/test_regular_angle.tscn"))

func test_regular_angle_no_correct() -> void:
	watch_signals(area)
	move_player(0.4, -0.6)
	await wait_for_signal(area.body_entered, 2)

	assert_signal_emitted(area, "body_entered")

func test_regular_angle_correct() -> void:
	watch_signals(area)
	# move player slightly right, so he needs to be corner corrected
	player.position.x += 32
	move_player(0.4, -0.6)
	await wait_for_signal(area.body_entered, 2)

	assert_signal_emitted(area, "body_entered")