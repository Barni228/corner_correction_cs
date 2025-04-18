namespace CornerCorrection;

using System;
using System.Diagnostics; // Debug.Assert
using Godot;
using static Godot.Mathf;

// this makes it visible in `Add child` in godot editor
/// <summary>
/// Corner Corrected character body 2D
/// </summary>
[GlobalClass]
public partial class CornerCharacter2D : CharacterBody2D
{
    /// <summary>
    /// this contains a KinematicCollision2D, bool of whether it corner corrected or no, and new player position
    /// </summary>
    /// <param name="Collision">Collision that could not be corner corrected (null if collision was corrected)</param>
    /// <param name="Corrected">`true` if corner correction happened</param>
    /// <param name="NewPos">New player position</param>
    public record CornerCorrectionResult(
        bool Corrected,
        KinematicCollision2D? Collision,
        Vector2 NewPos
    );

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
    /// The default value is `true`
    /// </summary>
    public bool IgnoreIsSpecial { get; set; } = true;

    /// <summary>
    /// Amount of corner correction, inclusive
    /// </summary>
    [Export] public int CornerCorrectionAmount { get; set; } = 8;

    /// <summary>
    /// Sides that will not have any corner correction
    /// For platformer games, you might want to ignore bottom (IgnoreSides = [Side.Bottom])
    /// so player does not fall down when he lands on an edge of a platform
    /// </summary>
    [Export] public Godot.Collections.Array<Side> IgnoreSides { get; set; } = [];

    /// <summary>
    /// This method behaves almost the same as `MoveAndSlide` but with corner correction
    /// It will use `Velocity` to calculate movement
    /// </summary>
    /// <param name="delta">The delta time, for frame rate independence</param>
    public void MoveAndSlideCorner(float delta)
    {
        // if we can corner correct, do it
        var CornerCorrectResult = MoveAndCorrect(Velocity * delta, true);
        if (CornerCorrectResult.Corrected)
            GlobalPosition = CornerCorrectResult.NewPos;

        // otherwise use move and slide
        else
            MoveAndSlide();
    }

    /// <inheritdoc/>
    public void MoveAndSlideCorner(double delta) => MoveAndSlideCorner((float)delta);


    /// <summary>
    /// This method works almost exactly the same as <see cref="CharacterBody2d.MoveAndCollide"/>.
    /// It modifies `Position`, and uses `MoveAndCollide`
    /// </summary>
    /// <param name="motion">Characters motion, for frame independence multiply by `delta`</param>
    /// <param name="testOnly">If true, don't actually move the player</param>
    /// <returns>
    /// CornerCorrectionResult with
    /// - Corrected: `true` if this function corner corrected
    /// - Collision: collision that could not be corner corrected (if corrected is true this is null)
    /// - NewPos: if `testOnly` is true, this is APPROXIMATELY where the player would be if it was false
    /// </returns>
    public CornerCorrectionResult MoveAndCorrect(Vector2 motion, bool testOnly = false)
    {
        var from = GlobalTransform;

        var collision = MoveAndCollide(motion, testOnly);

        if (collision is null)
            return new CornerCorrectionResult(false, null, from.Origin);

        from.Origin += collision.GetTravel();

        var normal = collision.GetNormal();

        // if we hit something diagonally, dont corner correct
        if (IsDiagonal(normal))
            return new CornerCorrectionResult(false, collision, from.Origin);

        // the direction of the collision
        var direction = -normal.Round();
        // we are sure that direction is either (±1, 0) or (0, ±1)
        Debug.Assert(direction.LengthSquared() == 1);

        // direction that player was going, integer only
        var logicalMotion = NormalizeSum1(motion).Round();

        foreach (var ignore in IgnoreSides)
        {
            var ignoreVec = SideToVec(ignore);
            if (ignoreVec == direction)
                return new CornerCorrectionResult(false, collision, from.Origin);

            // this is the only place where we use `IgnoreIsSpecial`
            if (IgnoreIsSpecial)
            {
                // basically, if we ignore our current logical movement, but we also were moving slightly diagonally
                // then we say the other way we were moving will be the main way we were moving
                // technically, we dont need to do the checks with 0, because if we lie about logical movement
                // then next check (comparing it to direction of collision) will result in `return collision;`
                // but why would I make the code intentionally lie to me
                if (logicalMotion.X == ignoreVec.X)
                    logicalMotion = motion.Y == 0 ? Vector2.Zero : motion.Y > 0 ? Vector2.Down : Vector2.Up;
                if (logicalMotion.Y == ignoreVec.Y)
                    logicalMotion = motion.X == 0 ? Vector2.Zero : motion.X > 0 ? Vector2.Right : Vector2.Left;
            }
        }


        // if we hit something, but we were not going that direction, dont corner correct
        // so if we were moving top, and very slightly left, we only want corner correct to the top
        if (direction != logicalMotion)
            return new CornerCorrectionResult(false, collision, from.Origin);

        // logical motion is (±1, 0) or (0, ±1)
        Debug.Assert(logicalMotion.LengthSquared() == 1);

        #region actually corner correcting
        var correctedPos = Wiggle(from, CornerCorrectionAmount, normal);
        if (correctedPos is null)
            return new CornerCorrectionResult(false, collision, from.Origin);

        from.Origin = correctedPos.Value;

        if (!testOnly)
        {
            GlobalPosition = correctedPos.Value;
            MoveAndCorrect(collision.GetRemainder());
        }

        return new CornerCorrectionResult(true, null, from.Origin);
        #endregion
    }

    /// <summary>
    /// This method works almost exactly the same as <see cref="CharacterBody2d.MoveAndCollide"/>.
    /// See `MoveAndCorrect` (this method just uses that but only returns the collision)
    /// It modifies `GlobalPosition`, and uses `MoveAndCollide`
    /// </summary>
    /// <param name="motion">Characters motion, for frame independence multiply by `delta`</param>
    /// <param name="testOnly">If true, don't actually move the player</param>
    /// <returns>Collision Object</returns>
    public KinematicCollision2D? MoveAndCollideCorner(
        Vector2 motion,
        bool testOnly = false
    ) => MoveAndCorrect(motion, testOnly).Collision;


    /// <summary>
    /// This method will move back and forth the player by no more than `range`
    /// It has no side effects
    /// </summary>
    /// <param name="from">The player `GlobalTransform`</param>
    /// <param name="range">maximum movement that could be performed</param>
    /// <param name="normal">normal of the collision (`KinematicCollision2D.GetNormal()`)</param>
    /// <returns>
    /// The correct player position, or `null` if it was not found
    /// </returns>
    private Vector2? Wiggle(Transform2D from, int range, Vector2 normal)
    {
        // Axis on which we will be moving
        var v = normal.Rotated(DegToRad(90));

        // we dont start at 0, because moving by 0 means not moving
        for (int i = 1; i <= range; i++)
            foreach (Vector2 move in new Vector2[] { v * i, v * i * -1 })
            {
                if (CheckIfWorks(from, move, -normal))
                    return from.Origin + move;
            }
        return null;
    }

    /// <summary>
    /// Checks if the `initMovement` movement results in player being able
    /// to move with `checkMovement` without any collisions
    /// This method has no side effects (does not modify anything)
    /// </summary>
    /// <param name="from">The player `GlobalTransform`</param>
    /// <param name="initMovement">The initial movement to perform</param>
    /// <param name="checkMovement">The movement that player should be able to do from the `initMovement`</param>
    /// <returns>
    /// `true` if player can do `checkMovement` without collision after applying the `initMovement`
    /// or `false` if player collides when trying to do `checkMovement` after `initMovement`
    /// </returns>
    private bool CheckIfWorks(Transform2D from, Vector2 initMovement, Vector2 checkMovement)
    {
        // if we cannot move to the initial position, then this does not work
        if (TestMove(from, initMovement))
            return false;

        // move to the initial position
        // origin is basically Position
        from.Origin += initMovement;

        // return whether there was no collision
        return !TestMove(from, checkMovement);
    }

    /// <summary>
    /// convert `Side` to `Vector2`, so `Side.Top` is `Vector2.Up`
    /// </summary>
    /// <param name="side">Side to convert</param>
    /// <returns>Converted Vector2</returns>
    public static Vector2 SideToVec(Side side)
    {
        return side switch
        {
            Side.Top => Vector2.Up,
            Side.Right => Vector2.Right,
            Side.Bottom => Vector2.Down,
            Side.Left => Vector2.Left,
            _ => throw new ArgumentOutOfRangeException(nameof(side), side, "Invalid side value")
        };
    }

    public static bool IsDiagonal(Vector2 v) => Min(Abs(v.X), Abs(v.Y)) > NormalAngleMax;

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
