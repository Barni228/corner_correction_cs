extends TestParent

func before_all():
	setup(preload("res://test/scenes/test_tiny_angle.tscn"))

func test_angle_no_correct():
	watch_signals(area)
	move_player(0.08, -1)
	await wait_for_signal(area.body_entered, 2)
	assert_signal_emitted(area, "body_entered")

func test_angle_correct():
	watch_signals(area)
	player.position.y += 32
	move_player(0.08, -1)
	await wait_for_signal(area.body_entered, 2)
	assert_signal_emitted(area, "body_entered")