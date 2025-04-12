namespace CornerCorrection; // tell that this entire file is corner correction namespace

using System;
using System.Diagnostics;
using Godot;
using static Godot.Mathf;

public partial class Player : CharacterBody2D
{
    /// <summary>
    /// maximum normal angle at which we will still corner correct
    /// so if it is `0.1f` and we hit something with normal `(0.1, 0.9)`, we will still corner correct
    /// </summary>
    const float NormalAngleMax = 0.1f;

    /// <summary>
    /// if player ignores bottom, and moves 80px down and 20px right (fall)
    /// and he hits corner of something to hit right (platform), should he:
    /// - hit it and just fall down, because he was moving mostly down (false)
    /// - corner correct and keep moving right, because he ignores bottom (true)
    /// by default or if there was no ignoring he would just hit it and fall down (false)
    /// </summary>
    public bool IgnoreIsSpecial { get; set; }

    /// <summary>
    /// Player speed, pixels per second
    /// </summary>
    [Export] public float Speed { get; set; } = 200.0f;

    /// <summary>
    /// Amount of corner correction, inclusive
    /// </summary>
    [Export] public int CornerCorrectionAmount { get; set; } = 8;

    /// <summary>
    /// Sides that will not have any corner correction
    /// For platformer games, you might want to ignore bottom (IgnoreSides = [Vector2.Down])
    /// so player does not fall down when he lands on an edge of a platform
    /// </summary>
    [Export] public Vector2[] IgnoreSides { get; set; } = [];

    /// <summary>
    /// Constant movement that player will go through
    /// This does not respect `Speed` property, so (1, 1) will move VERY slowly to bottom right
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
        #region checking if we should corner correct
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
                // technically, we dont need to do the checks with 0, because if we lie about logical movement
                // then next check (comparing it to direction of collision) will result in `return collision;`
                // but why would I make the code intentionally lie to me
                if (logicalMotion.X == ignore.X)
                    logicalMotion = motion.Y == 0 ? Vector2.Zero : motion.Y > 0 ? Vector2.Down : Vector2.Up;
                if (logicalMotion.Y == ignore.Y)
                    logicalMotion = motion.X == 0 ? Vector2.Zero : motion.X > 0 ? Vector2.Right : Vector2.Left;
            }
        }


        // if we hit something, but we were not going that direction, dont corner correct
        // so if we were moving top, and very slightly left, we only want corner correct to the top
        if (direction != logicalMotion)
            return collision;

        // logical motion is (±1, 0) or (0, ±1)
        Debug.Assert(logicalMotion.LengthSquared() == 1);

        #endregion

        #region actually corner correcting
        var correctionResult = Wiggle(CornerCorrectionAmount, normal, testOnly);
        if (correctionResult is null)
            return collision;

        if (!keepMove)
            return null;

        return MoveAndCollideCorner(collision.GetRemainder(), testOnly, ignoreSides, keepMove);
        #endregion
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
        var v = normal.Rotated(DegToRad(90));

        // we dont start at 0, because moving by 0 means not moving
        for (int i = 1; i <= range; i++)
            foreach (Vector2 move in new Vector2[] { v * i, v * i * -1 })
            {
                if (CheckIfWorks(move, -normal))
                {
                    if (!testOnly)
                        Position += move;

                    return i;
                }
            }
        return null;
    }

    /// <summary>
    /// Checks if the `initMovement` movement results in player being able
    /// to move with `checkMovement` without any collisions
    /// This method has no side effects (does not modify anything)
    /// </summary>
    /// <param name="initMovement">The initial movement to perform</param>
    /// <param name="checkMovement">The movement that player should be able to do from the `initMovement`</param>
    /// <returns>
    /// `true` if player can do `checkMovement` without collision after applying the `initMovement`
    /// or `false` if player collides when trying to do `checkMovement` after `initMovement`
    /// </returns>
    private bool CheckIfWorks(Vector2 initMovement, Vector2 checkMovement)
    {
        var transform = GlobalTransform;
        // if we cannot move to the initial position, then this does not work
        if (TestMove(transform, initMovement))
            return false;

        // move to the initial position
        // origin is basically Position
        transform.Origin += initMovement;

        // return whether there was no collision
        return !TestMove(transform, checkMovement);
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

    /// <summary>
    /// Will normalize Vector so that its sum (x + y) will always will be 1,
    /// If the vector is Vector2.Zero, then it will return Vector2.Zero
    /// </summary>
    /// <param name="vec">the vector to normalize</param>
    /// <returns>Vector whose x + y == 1, or 0</returns>
    public static Vector2 NormalizeSum1(Vector2 vec)
    {
        if (vec == Vector2.Zero)
            return Vector2.Zero;

        var sum = Abs(vec.X) + Abs(vec.Y);
        return vec / sum;
    }


    /// <inheritdoc/>
    /// <param name="x">x side of vector</param>
    /// <param name="y">y side of vector</param>
    public static Vector2 NormalizeSum1(float x, float y) => NormalizeSum1(new Vector2(x, y));
}