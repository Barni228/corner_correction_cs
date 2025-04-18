#if TOOLS
using Godot;

[Tool]
public partial class CornerCorrectionPlugin : EditorPlugin
{
	public override void _EnterTree()
	{
		var script = GD.Load<Script>("uid://brqg0lw1rsj8u");

		AddCustomType("CornerCharacter2D", "CharacterBody2D", script, null);
	}

	public override void _ExitTree()
	{
		RemoveCustomType("CornerCharacter2D");
	}
}
#endif
