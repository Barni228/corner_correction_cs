extends TestParent

func before_all():
    setup(preload("res://test/scenes/test_corner.tscn"))

func test_corner_collision_works():
    watch_signals(area)

    # we can also press keys instead of actions
    input_sender.key_down(KEY_UP)
    input_sender.key_down(KEY_RIGHT)
    await wait_for_signal(area.body_entered, 3)

    assert_signal_emitted(area, "body_entered")