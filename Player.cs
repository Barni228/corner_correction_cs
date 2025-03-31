using System;
using System.Diagnostics; // Debug.Assert
using Godot;

// TIP: GetRect will return rectangle representing this shape, without including Transform
// TODO: support rotation, and skew
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
    public override void _PhysicsProcess(double delta)
    {
        Velocity = Input.GetVector("ui_left", "ui_right", "ui_up", "ui_down") * Speed;
        MoveAndSlideCorner((float)delta);
    }

    private void MoveAndSlideCorner(float delta)
    {
        // of we store global transform, then we would need to update it every frame
        var collisionShape = GetNode<CollisionShape2D>("CollisionShape2D");
        // we expect the shape to be rectangle shape
        var shape = (RectangleShape2D)collisionShape.Shape;

        // move
        var collision = MoveAndCollide(Velocity * delta);

        // if we collided with something
        if (collision is not null)
        {
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
                collision.GetRemainder(),
                collider.Shape,
                collider.GlobalTransform,
                Vector2.Zero
            );
            // We have to have a collision
            Debug.Assert(points.Length > 0);
            bool isCorner = true;
            // side of collision, 1 for right or bottom, and -1 for left or top
            int? side = null;
            // check if every point is at max `CornerCorrectionAmount` away from the corner
            foreach (var point in points)
            {
                // if it is collision on y axis (we are at the top or bottom)
                if (collision.GetNormal().Y != 0)
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
                var hisSize = (collider.Shape as RectangleShape2D).Size * collider.GlobalScale;
                var ourSize = shape.Size * collisionShape.GlobalScale;
                var moveSamePos = collider.GlobalPosition - collisionShape.GlobalPosition;
                // For Y collision (we are at the top or bottom)
                // we will move ourselves to the same x as the collider is
                //     ||||||||
                //       ||||
                // then by half of collider width
                //     ||||||||
                //   ||||
                // then by half of our width
                //     ||||||||
                // ||||
                // and then by one pixel so that we cont collide with it again (same x is collision)
                var snapMovement = moveSamePos;
                snapMovement -= side.Value * (hisSize / 2 + ourSize / 2 + Vector2.One);
                if (collision.GetNormal().Y != 0)
                    snapMovement.Y = 0;
                else
                    snapMovement.X = 0;
                // if moving there results in no collision, move there
                if (MoveAndCollide(snapMovement, true) is null)
                {
                    Position += snapMovement;
                }
            }
            else
            {
                // Get the velocity that we still need to move, without delta
                Velocity = collision.GetRemainder() / (float)delta;
                MoveAndSlide();
                // or manually do MoveAndSlide()
                // this might not use movement correctly (GetRemainder)
                // Velocity = Velocity.Slide(collision.GetNormal());
                // MoveAndSlide();
                // const int maxSlides = 4;
                // int slideCount = 0;
                // while (movement.LengthSquared() > 0.001f && slideCount < maxSlides)
                // {
                //     var _collision = MoveAndCollide(movement);
                //     if (_collision is null)
                //     {
                //         // No collision, we're done
                //         break;
                //     }
                //     // Slide along the collision normal
                //     var normal = _collision.GetNormal();
                //     movement = movement.Slide(normal);
                //     // Prevent infinite loops
                //     slideCount++;
                // }
            }
        }
    }
}