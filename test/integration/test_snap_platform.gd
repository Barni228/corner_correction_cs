extends TestParent

func before_all():
	setup(preload("res://test/scenes/test_snap_platform.tscn"))

func test_correct_ignore_sides():
	assert_eq(player.IgnoreSides, PackedVector2Array([Vector2.DOWN]))

func test_snap_platform():
	watch_signals(area)

	input_sender.key_down(KEY_DOWN)
	input_sender.key_down(KEY_RIGHT)
	await wait_for_signal(area.body_entered, 2)
	assert_signal_emitted(area, "body_entered")