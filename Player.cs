using System;
using System.Diagnostics; // Debug.Assert
using Godot;

// TIP: GetRect will return rectangle representing this shape, without including Transform
// TODO: support rotation, and skew
// TODO: add tests
public partial class Player : CharacterBody2D
{
    /// <summary>
    /// Player speed, pixels per second
    /// </summary>
    [Export] public float Speed = 200.0f;

    /// <summary>
    /// Amount of corner correction
    /// </summary>
    [Export] public int CornerCorrectionAmount = 32;

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
    private static CollisionShape2D collisionShape;
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.

    public override void _Ready()
    {
        // if we store global transform, then we would need to update it every frame
        collisionShape = GetNode<CollisionShape2D>("CollisionShape2D");
    }
    public override void _PhysicsProcess(double delta)
    {
        Velocity = Input.GetVector("ui_left", "ui_right", "ui_up", "ui_down") * Speed;
        MoveAndSlideCorner((float)delta);
    }

    /// <summary>
    /// This method works almost exactly the same as <see cref="CharacterBody2d.MoveAndCollide"/>.
    /// </summary>
    /// <param name="motion">Characters motion, for frame independence use `delta`</param>
    /// <returns>collision object</returns>
    public KinematicCollision2D? MoveAndCollideCorner(Vector2 motion, bool testOnly = false)
    {
        // we expect the shape to be rectangle shape
        var shape = (RectangleShape2D)collisionShape.Shape;

        // move
        var collision = MoveAndCollide(motion, testOnly);

        // if we collided with something
        if (collision is not null)
        {
            // we expect get normal to be combination of (0, 1) or (0, -1)
            if (!(collision.GetNormal().X == 0 || collision.GetNormal().Y == 0))
                return collision;

            var remainingMotion = testOnly ? motion : collision.GetRemainder();
            // get the shape of the thing we collided with
            var collider = (CollisionShape2D)collision.GetColliderShape();
            // this will imagine moving `shape` with `shapeTransform` by
            // `collision.GetRemainder()` (remaining movement),
            // and return Contacts with the `collider.Shape` with `collider.GlobalTransform` that would move
            // by `Vector2.ZERO`
            // Contacts are points that would define polygon of interception
            // so for two rects it will be 4 Vector2 (top left, top right, bottom right, bottom left)
            var points = shape.CollideWithMotionAndGetContacts(
                // Global Transform is the real transform of a Node,
                // so if parent has scale 0.5 and we have scale 2.0, global scale is 1.0
                collisionShape.GlobalTransform,
                remainingMotion,
                collider.Shape,
                collider.GlobalTransform,
                Vector2.Zero
            );
            // when we have sub-pixel collision, MoveAndCollide will see it while shape will not
            // so we will just move one pixel away and try again
            if (points.Length <= 0)
            {
                // if we handled the collision, return null
                if (Wiggle(1, collision.GetNormal(), testOnly) is not null)
                    return null;
                else
                    return collision;
            }
            bool isCorner = true;
            // side of collision, 1 for right or bottom, and -1 for left or top
            int? side = null;
            // check if every point is at max `CornerCorrectionAmount` away from the corner
            foreach (var point in points)
            {
                if (collision.GetNormal().X == 0)
                {
                    // our local right, if we multiply this by -1 it will be left side
                    var rightSide = shape.Size.X * collisionShape.GlobalTransform.Scale.X / 2;
                    // make sure side is not null
                    side ??= Mathf.Abs(rightSide - ToLocal(point).X) > CornerCorrectionAmount ? -1 : 1;
                    // this is the same
                    // if (side is null)
                    //     if (Mathf.Abs(leftSide * 1 - ToLocal(point).X) > CornerCorrectionAmount)
                    //         side = -1;
                    //     else
                    //         side = 1;
                    // if point is more then `CornerCorrectionAmount` from the Corner
                    // .Value returns the non null value of the variable, or error otherwise
                    if (Mathf.Abs(rightSide * side.Value - ToLocal(point).X) > CornerCorrectionAmount)
                    {
                        isCorner = false;
                        break;
                    }
                }
                else
                {
                    var bottomSide = shape.Size.Y * collisionShape.GlobalTransform.Scale.Y / 2;
                    side ??= Mathf.Abs(bottomSide - ToLocal(point).Y) > CornerCorrectionAmount ? -1 : 1;
                    if (Mathf.Abs(bottomSide * side.Value - ToLocal(point).Y) > CornerCorrectionAmount)
                    {
                        isCorner = false;
                        break;
                    }
                }
            }
            if (isCorner)
            {
                // if we handled collision, return null
                if (Wiggle(CornerCorrectionAmount, collision.GetNormal(), testOnly) is not null)
                    return null;
            }
        }
        return collision;
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
        var v = normal.X == 0 ? Vector2I.Right : Vector2I.Down;
        for (int i = 1; i < range; i++)
            foreach (Vector2I move in new Vector2I[] { v * i, v * i * -1 })
            {
                if (TestMove(collisionShape.GlobalTransform, move))
                {
                    continue;
                }
                Position += move;
                // if we are at the bottom, try to move to the top
                if (MoveAndCollide(normal * -1, testOnly: testOnly) is null)
                {
                    if (testOnly)
                        Position -= move;
                    return i;
                }
                Position -= move;
            }
        return null;
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

            // Velocity /= delta; MoveAndSlide(); return;
            // you need the loop in case of slopes
            const int maxSlides = 4;
            int slideCount = 0;
            while (Velocity.LengthSquared() > 0.001f && slideCount < maxSlides)
            {
                var _collision = MoveAndCollide(Velocity);
                if (_collision is null)
                {
                    // No collision, we're done
                    break;
                }
                // Slide along the collision normal
                var normal = _collision.GetNormal();
                Velocity = Velocity.Slide(normal);
                // Prevent infinite loops
                slideCount++;
            }
        }
    }
}