class_name TestParent extends GutTest

## A test parent that will give you `level`, `player`, `area`, and 'input_sender` variables,
## when you call `setup` function, with scene that ideally inherit from `test_scene`

# we cannot use type created in C# (Player) in GdScript statically
var _level_scene: PackedScene
var level: Node2D
var player: CharacterBody2D
var area: Area2D
var input_sender = GutInputSender.new(Input)

## expects scene, which ideally inherits from `test_scene`
func setup(level_scene: PackedScene):
	_level_scene = level_scene

func area_should_be_reached(b: bool):
	area.should_be_reached = b

## A constant motion that player should have
## both x and y will get multiplied by player speed
func move_player(x: float, y: float):
	var direction = Vector2(x, y)
	player.ConstantMovement = direction * player.Speed

func before_all():
	gut.p("parent before all")

func before_each():
	if _level_scene == null:
		push_error("you forgot to provide a scene, do it with `setup`, " + \
		"example: ```before_each(): setup(preload(...))```")

	level = add_child_autofree(_level_scene.instantiate())
	player = level.get_node("Player")
	area = level.get_node("Area2D")

func after_each():
	input_sender.release_all()
	input_sender.clear()
