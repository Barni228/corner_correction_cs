#if TOOLS
using Godot;

[Tool]
public partial class CornerCorrectionPlugin : EditorPlugin
{
	public override void _EnterTree()
	{
		var script = GD.Load<Script>("CornerCorrection.cs");

		// for some reason i cannot add a custom icon
		AddCustomType("CornerCorrection", "Node", script, null);
	}

	public override void _ExitTree()
	{
		RemoveCustomType("CornerCorrection");
	}
}
#endif
