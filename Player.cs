namespace Main;

using CornerCorrection;
using Godot;

public partial class Player : CharacterBody2D
{
    /// <summary>
    /// Player speed, pixels per second
    /// </summary>
    [Export] public float Speed { get; set; } = 200.0f;

    /// <summary>
    /// The corner correction to use when moving
    /// </summary>
    public CornerCorrection CornerCorrectionNode { get; set; } = default!;

    /// <summary>
    /// Constant movement that player will go through
    /// This does not respect `Speed` property, so (1, 1) will move VERY slowly to bottom right
    /// </summary>
    public Vector2 ConstantMovement { get; set; } = Vector2.Zero;

    public override void _Ready()
    {
        CornerCorrectionNode = GetNode<CornerCorrection>("CornerCorrection");
    }

    public override void _PhysicsProcess(double delta)
    {
        Velocity = Input.GetVector("ui_left", "ui_right", "ui_up", "ui_down") * Speed;
        Velocity += ConstantMovement;
        CornerCorrectionNode.MoveAndSlideCorner((float)delta);
    }
}