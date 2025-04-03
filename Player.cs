namespace CornerCorrection; // tell that this entire file is corner correction namespace
using Godot;

// TIP: GetRect will return rectangle representing this shape, without including Transform
// TODO: add tests
public partial class Player : CharacterBody2D
{
    /// <summary>
    /// maximum normal angle at which we will still corner correct
    /// so if it is `0.1f` and we hit something with normal `(0.1, 0.9)`, we will still corner correct
    /// </summary>
    const float normalAngleMax = 0.1f;

    /// <summary>
    /// Player speed, pixels per second
    /// </summary>
    [Export] public float Speed { get; set; } = 200.0f;

    /// <summary>
    /// Amount of corner correction, inclusive (if 5, then player can be moved by 5 or less)
    /// </summary>
    [Export] public int CornerCorrectionAmount { get; set; } = 8;

    public override void _PhysicsProcess(double delta)
    {
        Velocity = Input.GetVector("ui_left", "ui_right", "ui_up", "ui_down") * Speed;
        MoveAndSlideCorner((float)delta);
    }

    /// <summary>
    /// This method works almost exactly the same as <see cref="CharacterBody2d.MoveAndCollide"/>.
    /// </summary>
    /// <param name="motion">Characters motion, for frame independence use `delta`</param>
    /// <param name="testOnly">If true, don't actually move the player</param>
    /// <param name="keepMove">
    /// If true, object continues moving after corner correction
    /// So if object needed to move by 10px, and it moves by 2px and then it hits an obstacle
    /// But successfully corner corrects away, if `keepMove` is true it will still move by 8px
    /// </param>
    /// <returns>collision object</returns>
    public KinematicCollision2D? MoveAndCollideCorner(Vector2 motion, bool testOnly = false, bool keepMove = true)
    {
        var collision = MoveAndCollide(motion, testOnly);
        if (collision is null)
            return null;

        var normal = collision.GetNormal();
        var smallest = Mathf.Min(Mathf.Abs(normal.X), Mathf.Abs(normal.Y));

        if (smallest > 0.1f)
            return collision;

        var correctionResult = Wiggle(CornerCorrectionAmount, normal, testOnly);
        if (correctionResult is null)
            return collision;

        if (!keepMove)
            return null;

        return MoveAndCollideCorner(collision.GetRemainder(), testOnly, keepMove);
    }

    /// <summary>
    /// This method will move back and forth the player on `axis` axis by no more that `range`
    /// </summary>
    /// <param name="range">maximum movement that could be performed</param>
    /// <param name="normal">collision normal (`KinematicCollision2D.GetNormal()`)</param>
    /// <param name="testOnly">if `true`, do not actually move the player</param>
    /// <returns>
    /// number of pixels moved if player was moved successfully 
    /// (or how many pixels player could be moved if `testOnly` is `true`)
    /// `null` if player could not be moved to valid position
    /// </returns>
    private int? Wiggle(int range, Vector2 normal, bool testOnly)
    {
        // Axis on which we will be moving
        var v = ApproximatelyEqual(normal.X, 0) ? Vector2.Right : Vector2.Down;

        for (int i = 1; i <= range; i++)
            foreach (Vector2 move in new Vector2[] { v * i, v * i * -1 })
            {
                if (CheckIfWorks(move, normal * -1, testOnly))
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
    /// <param name="delta">The delta time, for frame independence</param>
    public void MoveAndSlideCorner(float delta)
    {
        // move regularly
        var collision = MoveAndCollideCorner(Velocity * delta);
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

    private static bool ApproximatelyEqual(float x, float y, float precision = normalAngleMax) =>
        Mathf.Abs(x - y) <= precision;
}