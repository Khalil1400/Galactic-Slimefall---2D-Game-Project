using System.Collections.Generic;
using UnityEngine;
[RequireComponent(typeof(Collider2D))]
public class HazardDamage : MonoBehaviour
{
    [SerializeField] int damage = 1;
    [SerializeField] float cooldown = 0.6f;
    [SerializeField] Vector2 knockbackDirection = new(0f, 1f);

    public int Damage
    {
        get => damage;
        set => damage = value;
    }

    public Vector2 KnockbackDirection
    {
        get => knockbackDirection;
        set => knockbackDirection = value;
    }

    public float Cooldown
    {
        get => cooldown;
        set => cooldown = value;
    }

    readonly Dictionary<Unit, float> _lastHitAt = new();

    void Reset()
    {
        if (TryGetComponent<Collider2D>(out Collider2D collider))
        {
            collider.isTrigger = true;
        }
    }

    void OnTriggerStay2D(Collider2D other)
    {
        Unit unit = other.GetComponent<Unit>() ?? other.GetComponentInParent<Unit>() ?? other.attachedRigidbody?.GetComponent<Unit>();
        if (unit == null || unit.IsDead)
        {
            return;
        }

        if (_lastHitAt.TryGetValue(unit, out float lastHitTime) && Time.time - lastHitTime < cooldown)
        {
            return;
        }

        _lastHitAt[unit] = Time.time;
        Vector2 direction = knockbackDirection == Vector2.zero ? Vector2.up : knockbackDirection.normalized;
        unit.TakeDamage(damage, direction);
    }
}

