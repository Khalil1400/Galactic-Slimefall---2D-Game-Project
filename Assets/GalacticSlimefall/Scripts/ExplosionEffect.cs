using System.Collections;
using UnityEngine;
public class ExplosionEffect : MonoBehaviour
{
    [Header("Animation")]
    public float duration = 0.35f;
    public float maxScale = 2.4f;
    public AnimationCurve scaleCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    public AnimationCurve alphaCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);

    [Header("Optional Renderer")]
    public SpriteRenderer ring;

    void Start()
    {
        StartCoroutine(Animate());
    }

    IEnumerator Animate()
    {
        Vector3 baseScale = transform.localScale;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            transform.localScale = baseScale * Mathf.Max(0.01f, scaleCurve.Evaluate(progress) * maxScale);

            if (ring != null)
            {
                Color color = ring.color;
                color.a = alphaCurve.Evaluate(progress);
                ring.color = color;
            }

            yield return null;
        }

        Destroy(gameObject);
    }
}

