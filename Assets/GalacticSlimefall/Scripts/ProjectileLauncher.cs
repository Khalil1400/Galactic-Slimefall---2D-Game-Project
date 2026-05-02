using UnityEngine;
[RequireComponent(typeof(AudioSource))]
public class ProjectileLauncher : MonoBehaviour
{
    [Header("Projectile")]
    public GameObject projectilePrefab;
    public bool isPlayerProjectile = true;
    public float spawnClearance = 0.35f;

    [Header("Audio")]
    public AudioClip launchSound;

    AudioSource _audioSource;

    void Awake()
    {
        _audioSource = GetComponent<AudioSource>();
        _audioSource.playOnAwake = false;
    }

    public ProjectileController Launch(Vector2 origin, Vector2 velocity, GameObject overrideProjectilePrefab = null)
    {
        GameObject activeProjectilePrefab = overrideProjectilePrefab != null ? overrideProjectilePrefab : projectilePrefab;
        if (activeProjectilePrefab == null)
        {
            Debug.LogWarning("ProjectileLauncher: projectile prefab/template is missing.");
            return null;
        }

        Vector2 direction = velocity.sqrMagnitude > 0.001f ? velocity.normalized : Vector2.right;
        GameObject instance = Instantiate(activeProjectilePrefab, origin + direction * spawnClearance, Quaternion.identity);
        instance.SetActive(true);

        var projectile = instance.GetComponent<ProjectileController>();
        if (projectile != null)
        {
            projectile.isPlayerProjectile = isPlayerProjectile;
            projectile.Launch(velocity);
        }

        Collider2D[] ownerColliders = GetComponentsInChildren<Collider2D>();
        Collider2D[] projectileColliders = instance.GetComponentsInChildren<Collider2D>();
        foreach (Collider2D ownerCollider in ownerColliders)
        {
            if (ownerCollider == null)
            {
                continue;
            }

            foreach (Collider2D projectileCollider in projectileColliders)
            {
                if (projectileCollider != null)
                {
                    Physics2D.IgnoreCollision(projectileCollider, ownerCollider, true);
                }
            }
        }

        Collider2D[] frameColliders = FindObjectsByType<Collider2D>(FindObjectsSortMode.None);
        foreach (Collider2D frameCollider in frameColliders)
        {
            if (frameCollider == null)
            {
                continue;
            }

            if (frameCollider.GetComponent<MapBoundaryFrame>() == null &&
                frameCollider.GetComponentInParent<MapBoundaryFrame>() == null)
            {
                continue;
            }

            foreach (Collider2D projectileCollider in projectileColliders)
            {
                if (projectileCollider != null)
                {
                    Physics2D.IgnoreCollision(projectileCollider, frameCollider, true);
                }
            }
        }

        if (launchSound != null)
        {
            _audioSource.PlayOneShot(launchSound);
        }
        else
        {
            GalacticSlimefallAudio.PlayProjectileLaunch(projectile, origin);
        }

        CameraController camera = CameraController.Instance;
        if (camera != null)
        {
            camera.FollowTarget(instance.transform, true);
        }
        return projectile;
    }
}




