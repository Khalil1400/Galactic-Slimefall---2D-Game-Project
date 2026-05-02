using UnityEngine;
[RequireComponent(typeof(Collider2D))]
public class JumpPad : MonoBehaviour
{
    [SerializeField] float launchForce = 12f;
    [SerializeField] float horizontalBoost = 0f;

    public float LaunchForce
    {
        get => launchForce;
        set => launchForce = value;
    }

    public float HorizontalBoost
    {
        get => horizontalBoost;
        set => horizontalBoost = value;
    }

    void Reset()
    {
        if (TryGetComponent<Collider2D>(out Collider2D collider))
        {
            collider.isTrigger = true;
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        Rigidbody2D body = other.attachedRigidbody;
        Unit unit = other.GetComponent<Unit>() ?? other.GetComponentInParent<Unit>() ?? body?.GetComponent<Unit>();
        if (unit == null || unit.IsDead || body == null)
        {
            return;
        }

        Vector2 velocity = body.linearVelocity;
        velocity.y = Mathf.Max(velocity.y, launchForce);

        if (!Mathf.Approximately(horizontalBoost, 0f))
        {
            float direction = Mathf.Sign(unit.transform.position.x - transform.position.x);
            if (Mathf.Approximately(direction, 0f))
            {
                direction = 1f;
            }

            velocity.x += direction * horizontalBoost;
        }

        body.linearVelocity = velocity;
    }
}

