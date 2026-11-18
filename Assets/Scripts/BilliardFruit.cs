using UnityEngine;

[RequireComponent(typeof(Rigidbody2D), typeof(CircleCollider2D))]
public sealed class BilliardFruit : MonoBehaviour
{
    private const float IdleSpinSpeed = 55f;

    private FruitBilliards table;
    private Vector2 incomingVelocity;
    private float startRotation;
    private float spinDirection = -1f;

    public Rigidbody2D Body { get; private set; }
    public CircleCollider2D Circle { get; private set; }
    public Vector2 StartPosition { get; private set; }
    public bool Skewered { get; private set; }

    internal void Initialize(FruitBilliards owner)
    {
        table = owner;
        StartPosition = transform.position;
        startRotation = transform.eulerAngles.z;
        Body = GetComponent<Rigidbody2D>();
        Circle = GetComponent<CircleCollider2D>();
    }

    internal void Shoot(Vector2 velocity)
    {
        Body.linearVelocity = velocity;
        float direction = Mathf.Abs(velocity.x) >= Mathf.Abs(velocity.y) ? -Mathf.Sign(velocity.x) : Mathf.Sign(velocity.y);
        SetSpin(direction * Mathf.Min(360f, velocity.magnitude * 32f));
    }

    private void Update()
    {
        if (!Body.simulated && !Skewered)
            transform.Rotate(0f, 0f, spinDirection * IdleSpinSpeed * Time.deltaTime);
    }

    private void FixedUpdate()
    {
        if (!Body.simulated)
            return;

        if (Body.linearVelocity.sqrMagnitude < 0.01f)
            Body.linearVelocity = Vector2.zero;
        if (Mathf.Abs(Body.angularVelocity) < IdleSpinSpeed)
            Body.angularVelocity = spinDirection * IdleSpinSpeed;

        incomingVelocity = Body.linearVelocity;
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (Skewered || collision.contactCount == 0)
            return;

        ContactPoint2D contact = collision.GetContact(0);
        table.HandleFruitContact(this, collision.collider, contact, incomingVelocity);
        if (Skewered)
            return;

        float grazingSpeed = Vector2.Dot(incomingVelocity, Vector2.Perpendicular(contact.normal));
        SetSpin(Mathf.Clamp(Body.angularVelocity * 0.65f + grazingSpeed * 45f, -480f, 480f));
    }

    private void SetSpin(float angularSpeed)
    {
        if (Mathf.Abs(angularSpeed) > 0.01f)
            spinDirection = Mathf.Sign(angularSpeed);
        Body.angularVelocity = spinDirection * Mathf.Max(IdleSpinSpeed, Mathf.Abs(angularSpeed));
    }

    internal void StopAtPenTip(Vector2 position)
    {
        float visibleRotation = transform.eulerAngles.z;
        Skewered = true;
        Body.linearVelocity = Vector2.zero;
        Body.angularVelocity = 0f;
        Body.simulated = false;
        Body.position = position;
        Body.rotation = visibleRotation;
        transform.SetPositionAndRotation(position, Quaternion.Euler(0f, 0f, visibleRotation));
    }

    internal void ResetShot(Vector2 position)
    {
        StartPosition = position;
        Skewered = false;
        spinDirection = -1f;
        Body.position = StartPosition;
        Body.rotation = startRotation;
        transform.SetPositionAndRotation(StartPosition, Quaternion.Euler(0f, 0f, startRotation));
        Body.simulated = true;
        Body.linearVelocity = Vector2.zero;
        Body.angularVelocity = 0f;
        incomingVelocity = Vector2.zero;
    }

    internal void KeepInField(Rect bounds)
    {
        if (Skewered)
            return;

        float padding = Circle.radius + 0.05f;
        Vector2 current = Body.position;
        Vector2 clamped = new Vector2(
            Mathf.Clamp(current.x, bounds.xMin + padding, bounds.xMax - padding),
            Mathf.Clamp(current.y, bounds.yMin + padding, bounds.yMax - padding));
        if (current == clamped)
            return;

        Body.position = clamped;
        transform.position = clamped;
        Body.linearVelocity = Vector2.zero;
        Body.angularVelocity = 0f;
    }
}
