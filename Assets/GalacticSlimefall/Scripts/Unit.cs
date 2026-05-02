using System.Collections;
using UnityEngine;
[RequireComponent(typeof(Collider2D))]
public class Unit : MonoBehaviour
{
    [Header("Stats")]
    public int maxHP = 100;
    public bool isPlayerTeam = true;

    [Header("Visuals")]
    public SpriteRenderer bodyRenderer;
    public GameObject deathVFXPrefab;
    public Transform aimAnchor;

    [Header("Knockback")]
    public float knockbackForce = 4f;

    public int CurrentHP { get; private set; }
    public bool IsDead => CurrentHP <= 0;
    public float HealthPercent => maxHP > 0 ? (float)CurrentHP / maxHP : 0f;
    public Vector2 AimTarget => aimAnchor != null
        ? (Vector2)aimAnchor.position
        : (Vector2)transform.position + Vector2.up * 0.45f;

    public System.Action<int, int> OnHealthChanged;
    public System.Action OnDeath;

    Rigidbody2D _rigidbody;
    Color _baseColor = Color.white;

    void Awake()
    {
        _rigidbody = GetComponent<Rigidbody2D>();
        CurrentHP = Mathf.Max(1, maxHP);

        if (bodyRenderer != null)
        {
            _baseColor = bodyRenderer.color;
        }
    }

    void Start()
    {
        GameManager.Instance?.RegisterUnit(this);
        UIManager.Instance?.RegisterUnit(this);
    }

    public void TakeDamage(int amount, Vector2 hitDirection = default, bool showPopup = true)
    {
        if (IsDead || amount <= 0)
        {
            return;
        }

        CurrentHP = Mathf.Max(0, CurrentHP - amount);
        OnHealthChanged?.Invoke(CurrentHP, maxHP);
        UIManager.Instance?.UpdateUnitHealth(this);
        UIManager.Instance?.ShowHpValue(this);
        if (showPopup)
        {
            UIManager.Instance?.ShowDamagePopup(this, amount);
        }

        if (hitDirection != Vector2.zero && _rigidbody != null && _rigidbody.bodyType == RigidbodyType2D.Dynamic)
        {
            _rigidbody.AddForce(hitDirection.normalized * knockbackForce, ForceMode2D.Impulse);
        }

        GalacticSlimefallAudio.PlaySlimeHit(transform.position, amount);
        StartCoroutine(HitFlash());

        if (IsDead)
        {
            Die();
        }
    }

    public void Heal(int amount)
    {
        if (IsDead || amount <= 0)
        {
            return;
        }

        CurrentHP = Mathf.Min(maxHP, CurrentHP + amount);
        OnHealthChanged?.Invoke(CurrentHP, maxHP);
        UIManager.Instance?.UpdateUnitHealth(this);
    }

    void Die()
    {
        OnDeath?.Invoke();
        GameManager.Instance?.NotifyUnitDied(this);

        if (deathVFXPrefab != null)
        {
            Instantiate(deathVFXPrefab, transform.position, Quaternion.identity);
        }

        StartCoroutine(DeathSequence());
    }

    IEnumerator DeathSequence()
    {
        if (TryGetComponent<Collider2D>(out var col))
        {
            col.enabled = false;
        }

        if (_rigidbody != null)
        {
            _rigidbody.linearVelocity = Vector2.zero;
            _rigidbody.bodyType = RigidbodyType2D.Static;
        }

        yield return new WaitForSeconds(1.35f);
        Destroy(gameObject);
    }

    IEnumerator HitFlash()
    {
        if (bodyRenderer == null)
        {
            yield break;
        }

        Color restoreColor = bodyRenderer.color;
        bodyRenderer.color = Color.white;
        yield return new WaitForSeconds(0.08f);

        if (bodyRenderer != null)
        {
            bodyRenderer.color = restoreColor;
        }
    }
}


