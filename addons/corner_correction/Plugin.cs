#if TOOLS
using Godot;

[Tool]
public partial class CornerCorrectionPlugin : EditorPlugin
{
	public override void _EnterTree()
	{
		// for some reason, the icon does not work for me ):
		var script = GD.Load<Script>("CornerCorrection.cs");
		// Initialization of the plugin goes here.
		AddCustomType("CornerCorrection", "Node", script, null);
	}

	public override void _ExitTree()
	{
		// Clean-up of the plugin goes here.
		RemoveCustomType("CornerCorrection");
	}
}
#endif
