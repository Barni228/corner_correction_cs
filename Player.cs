namespace CornerCorrection; // tell that this entire file is corner correction namespace

using System;
using System.Diagnostics;
using Godot;
using static Godot.Mathf;

// TIP: GetRect will return rectangle representing this shape, without including Transform
public partial class Player : CharacterBody2D
{
    /// <summary>
    /// maximum normal angle at which we will still corner correct
    /// so if it is `0.1f` and we hit something with normal `(0.1, 0.9)`, we will still corner correct
    /// </summary>
    const float NormalAngleMax = 0.1f;

    /// <summary>
    /// if player ignores bottom, and moves 80px down and 20px right (falls)
    /// and he hits corner of something to hit right (platform), should he:
    /// - hit it and just fall down, because he was moving mostly down (false)
    /// - corner correct and keep moving right, because he ignores bottom (true)
    /// by default or if there was no ignoring he would just hit it and fall down
    /// </summary>
    public bool IgnoreIsSpecial { get; set; } = false;

    /// <summary>
    /// Player speed, pixels per second
    /// </summary>
    [Export] public float Speed { get; set; } = 200.0f;

    /// <summary>
    /// Amount of corner correction, inclusive (if 5, then player can be moved by 5 or less)
    /// </summary>
    [Export] public int CornerCorrectionAmount { get; set; } = 8;

    [Export] public Vector2[] IgnoreSides { get; set; } = [];

    /// <summary>
    /// Constant movement that player will go through
    /// This does not respect `Speed` property, so (1, 1) will move very slowly to bottom right
    /// </summary>
    public Vector2 ConstantMovement { get; set; } = Vector2.Zero;

    public override void _PhysicsProcess(double delta)
    {
        Velocity = Input.GetVector("ui_left", "ui_right", "ui_up", "ui_down") * Speed;
        Velocity += ConstantMovement;
        MoveAndSlideCorner((float)delta, IgnoreSides);
    }

    /// <summary>
    /// This method works almost exactly the same as <see cref="CharacterBody2d.MoveAndCollide"/>.
    /// It modifies `Position`, and uses `MoveAndCollide`
    /// </summary>
    /// <param name="motion">Characters motion, for frame independence use `delta`</param>
    /// <param name="testOnly">If true, don't actually move the player</param>
    /// <param name="ignoreSides">The sides that should not have corner correction (e.g. Vector2.DOWN)</param>
    /// <param name="keepMove">
    /// If true, object continues moving after corner correction
    /// So if object needed to move by 10px, and it moves by 2px and then it hits an obstacle
    /// But successfully corner corrects away, if `keepMove` is true it will still move by 8px
    /// </param>
    /// <returns>collision object</returns>
    public KinematicCollision2D? MoveAndCollideCorner(
        Vector2 motion,
        bool testOnly = false,
        ReadOnlySpan<Vector2> ignoreSides = default,
        bool keepMove = true)
    {
        var collision = MoveAndCollide(motion, testOnly);
        if (collision is null)
            return null;

        var normal = collision.GetNormal();

        // if we hit something diagonally, dont corner correct
        if (IsDiagonal(normal))
            return collision;

        // the direction of the collision
        var direction = -normal.Round();
        // we are sure that direction is either (±1, 0) or (0, ±1)
        Debug.Assert(direction.LengthSquared() == 1);

        // direction that player was going, integer only
        // we make sure that motion x + y == 1, and then round both numbers
        var logicalMotion = NormalizeSum1(motion).Round();

        foreach (var ignore in ignoreSides)
        {
            if (ApproximatelyEqual(ignore, direction))
                return collision;

            // this is the only place where we use `IgnoreIsSpecial`
            if (IgnoreIsSpecial)
            {
                // basically, if we ignore our current logical movement, but we also were moving slightly diagonally
                // then we say the other way we were moving will be the main way we were moving
                if (logicalMotion.X == ignore.X)
                    // logicalMotion = motion.Y == 0 ? Vector2.Zero : motion.Y > 0 ? Vector2.Down : Vector2.Up;
                    logicalMotion = motion.Y > 0 ? Vector2.Down : Vector2.Up;
                if (logicalMotion.Y == ignore.Y)
                    // logicalMotion = motion.X == 0 ? Vector2.Zero : motion.X > 0 ? Vector2.Right : Vector2.Left;
                    logicalMotion = motion.X > 0 ? Vector2.Right : Vector2.Left;
            }
            else
            {
                if (logicalMotion.X == ignore.X)
                    logicalMotion.X = 0;
                if (logicalMotion.Y == ignore.Y)
                    logicalMotion.Y = 0;
            }
        }

        // if logical motion is not (±1, 0) or (0, ±1)
        // this is redundant, because we already make this check for `direction`
        // so if logical motion is not one of them then we return collision anyway
        if (logicalMotion.LengthSquared() != 1)
            return collision;

        // if we hit something, but we were not going that direction, dont corner correct
        // so if we were moving top, and very slightly left, we only want corner correct to the top
        if (direction != logicalMotion)
            return collision;

        var correctionResult = Wiggle(CornerCorrectionAmount, normal, testOnly);
        if (correctionResult is null)
            return collision;

        if (!keepMove)
            return null;

        return MoveAndCollideCorner(collision.GetRemainder(), testOnly, ignoreSides, keepMove);
    }

    /// <summary>
    /// This method will move back and forth the player by no more that `range`
    /// </summary>
    /// <param name="range">maximum movement that could be performed</param>
    /// <param name="normal">normal of the collision (`KinematicCollision2D.GetNormal()`)</param>
    /// <param name="testOnly">if `true`, do not actually move the player</param>
    /// <returns>
    /// number of pixels moved if player was moved successfully 
    /// (or how many pixels player could be moved if `testOnly` is `true`)
    /// `null` if player could not be moved to valid position
    /// </returns>
    private int? Wiggle(int range, Vector2 normal, bool testOnly)
    {
        // Axis on which we will be moving
        // var v = ApproximatelyEqual(direction.X, 0) ? Vector2.Right : Vector2.Down;
        var v = normal.Rotated(DegToRad(90));

        // we dont start at 0, because moving by 0 means not moving
        for (int i = 1; i <= range; i++)
            foreach (Vector2 move in new Vector2[] { v * i, v * i * -1 })
            {
                if (CheckIfWorks(move, -normal, testOnly))
                    return i;
            }
        return null;
    }

    private bool CheckIfWorks(Vector2 initMovement, Vector2 checkMovement, bool testOnly = false)
    {
        // if we cannot move to the initial position, then this does not work
        if (MoveAndCollide(initMovement, true) is not null)
            return false;

        // move to the initial position
        var prevPos = Position;
        Position += initMovement;

        // if we cannot move by the check movement, then this does not work
        if (MoveAndCollide(checkMovement, testOnly: testOnly) is not null)
        {
            // if this movement does not work, we don't want to move player
            Position = prevPos;
            return false;
        }
        // at this point, the movement works
        // if testOnly is false, then MoveAndCollide already moved above
        if (testOnly)
            Position = prevPos;

        return true;
    }

    /// <summary>
    /// This method behaves almost the same as `MoveAndSlide` but with corner correction
    /// It will use `Velocity` to calculate movement
    /// </summary>
    /// <param name="delta">The delta time, for frame rate independence</param>
    // public void MoveAndSlideCorner(float delta, ReadOnlySpan<Vector2> ignoreSides = default)
    public void MoveAndSlideCorner(float delta, ReadOnlySpan<Vector2> ignoreSides = default)
    {
        // move regularly
        var collision = MoveAndCollideCorner(Velocity * delta, ignoreSides: ignoreSides);
        if (collision is not null)
        {
            // Get the velocity that we still need to move, with delta applied
            Velocity = collision.GetRemainder();

            // for some reason move and slide does not work properly if i move character properly
            // Velocity /= delta; MoveAndSlide(); return;
            // you need the loop in case of slopes
            const int maxSlides = 4;
            int slideCount = 0;
            while (Velocity.LengthSquared() > 0.001f && slideCount < maxSlides)
            {
                var _collision = MoveAndCollide(Velocity);
                if (_collision is null)
                    // No collision, we're done
                    break;

                // Slide along the collision normal
                var normal = _collision.GetNormal();
                Velocity = Velocity.Slide(normal);
                // Prevent infinite loops
                slideCount++;
            }
        }
    }

    public static bool IsDiagonal(Vector2 v) => Min(Abs(v.X), Abs(v.Y)) > NormalAngleMax;
    public static bool ApproximatelyEqual(float a, float b, float precision = NormalAngleMax) =>
        Abs(a - b) <= precision;

    public static bool ApproximatelyEqual(Vector2 a, Vector2 b, float precision = NormalAngleMax) =>
        ApproximatelyEqual(a.X, b.X, precision) && ApproximatelyEqual(a.Y, b.Y, precision);

    public static Vector2 NormalizeSum1(Vector2 vec)
    {
        if (vec == Vector2.Zero)
            return Vector2.Zero;

        var sum = Abs(vec.X) + Abs(vec.Y);
        return vec / sum;
    }

    public static Vector2 NormalizeSum1(float x, float y) => NormalizeSum1(new Vector2(x, y));
}