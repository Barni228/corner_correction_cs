extends TestParent

func before_all():
	setup(preload("res://test/scenes/test_snap_platform.tscn"))

func test_correct_ignore_sides():
	assert_eq(player.IgnoreSides, PackedVector2Array([Vector2.DOWN]))

func test_snap_platform():
	watch_signals(area)

	move_player(0.6, 0.4)
	await wait_for_signal(area.body_entered, 1)
	assert_signal_emitted(area, "body_entered")

func test_no_snap_platform():
	watch_signals(area)
	area_should_be_reached(false)

	move_player(0.4, 0.6)
	await wait_for_signal(area.body_entered, 1)
	assert_signal_not_emitted(area, "body_entered")

func test_ignore_is_special():
	watch_signals(area)
	player.IgnoreIsSpecial = true
	move_player(0.4, 0.6)
	await wait_for_signal(area.body_entered, 1)
	assert_signal_emitted(area, "body_entered")