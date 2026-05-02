using UnityEngine;
using UnityEngine.SceneManagement;

public class GalacticSlimefallAudio : MonoBehaviour
{
    static GalacticSlimefallAudio _instance;

    AudioSource _musicSource;
    AudioSource _uiSource;

    AudioClip _musicLoop;
    AudioClip _uiClick;
    AudioClip _uiHover;
    AudioClip _jump;
    AudioClip _slimeHitSoft;
    AudioClip _slimeHitHeavy;
    AudioClip _cannonLaunch;
    AudioClip _clusterLaunch;
    AudioClip _nukeLaunch;
    AudioClip _rollerLaunch;
    AudioClip _cannonImpact;
    AudioClip _clusterImpact;
    AudioClip _nukeImpact;
    AudioClip _rollerImpact;
    AudioClip _shrapnelImpact;

    float _lastHoverTime;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Bootstrap()
    {
        EnsureInstance();
    }

    static GalacticSlimefallAudio EnsureInstance()
    {
        if (_instance != null)
        {
            return _instance;
        }

        GameObject root = new GameObject("GalacticSlimefallAudio");
        _instance = root.AddComponent<GalacticSlimefallAudio>();
        DontDestroyOnLoad(root);
        return _instance;
    }

    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += OnSceneLoaded;
        LoadClips();
        EnsureSources();
        EnsureMusic();
    }

    void OnDestroy()
    {
        if (_instance == this)
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            _instance = null;
        }
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        EnsureMusic();
    }

    void LoadClips()
    {
        _musicLoop = Resources.Load<AudioClip>("Audio/Music/bgm_space_battle");
        _uiClick = Resources.Load<AudioClip>("Audio/SFX/UI/ui_click_confirm");
        _uiHover = Resources.Load<AudioClip>("Audio/SFX/UI/ui_hover_rollover");
        _jump = Resources.Load<AudioClip>("Audio/SFX/Movement/slime_jump");
        _slimeHitSoft = Resources.Load<AudioClip>("Audio/SFX/Combat/slime_hit_soft");
        _slimeHitHeavy = Resources.Load<AudioClip>("Audio/SFX/Combat/slime_hit_heavy");
        _cannonLaunch = Resources.Load<AudioClip>("Audio/SFX/Combat/cannon_launch");
        _clusterLaunch = Resources.Load<AudioClip>("Audio/SFX/Combat/cluster_launch");
        _nukeLaunch = Resources.Load<AudioClip>("Audio/SFX/Combat/nuke_launch");
        _rollerLaunch = Resources.Load<AudioClip>("Audio/SFX/Combat/roller_launch");
        _cannonImpact = Resources.Load<AudioClip>("Audio/SFX/Combat/cannon_impact");
        _clusterImpact = Resources.Load<AudioClip>("Audio/SFX/Combat/cluster_impact");
        _nukeImpact = Resources.Load<AudioClip>("Audio/SFX/Combat/nuke_impact");
        _rollerImpact = Resources.Load<AudioClip>("Audio/SFX/Combat/roller_impact");
        _shrapnelImpact = Resources.Load<AudioClip>("Audio/SFX/Combat/shrapnel_impact");
    }

    void EnsureSources()
    {
        if (_musicSource == null)
        {
            _musicSource = gameObject.AddComponent<AudioSource>();
            _musicSource.playOnAwake = false;
            _musicSource.loop = true;
            _musicSource.volume = 0.3f;
            _musicSource.spatialBlend = 0f;
        }

        if (_uiSource == null)
        {
            _uiSource = gameObject.AddComponent<AudioSource>();
            _uiSource.playOnAwake = false;
            _uiSource.loop = false;
            _uiSource.volume = 0.9f;
            _uiSource.spatialBlend = 0f;
        }
    }

    void EnsureMusic()
    {
        if (_musicLoop == null)
        {
            return;
        }

        if (_musicSource.clip != _musicLoop)
        {
            _musicSource.clip = _musicLoop;
        }

        if (!_musicSource.isPlaying)
        {
            _musicSource.Play();
        }
    }

    public static void PlayUiClick()
    {
        GalacticSlimefallAudio audio = EnsureInstance();
        if (audio._uiClick == null)
        {
            return;
        }

        audio._uiSource.PlayOneShot(audio._uiClick, 0.85f);
    }

    public static void PlayUiHover()
    {
        GalacticSlimefallAudio audio = EnsureInstance();
        if (audio._uiHover == null)
        {
            return;
        }

        if (Time.unscaledTime - audio._lastHoverTime < 0.07f)
        {
            return;
        }

        audio._lastHoverTime = Time.unscaledTime;
        audio._uiSource.PlayOneShot(audio._uiHover, 0.42f);
    }

    public static void PlayJump(Vector3 position, bool isExtraJump)
    {
        GalacticSlimefallAudio audio = EnsureInstance();
        float volume = isExtraJump ? 0.2125f : 0.425f;
        audio.PlayWorld(audio._jump, position, volume);
    }
    public static void PlaySlimeHit(Vector3 position, int damage)
    {
        GalacticSlimefallAudio audio = EnsureInstance();
        AudioClip clip = damage >= 18 ? audio._slimeHitHeavy : audio._slimeHitSoft;
        audio.PlayWorld(clip, position, 0.92f);
    }

    public static void PlayProjectileLaunch(ProjectileController projectile, Vector3 position)
    {
        GalacticSlimefallAudio audio = EnsureInstance();
        audio.PlayWorld(audio.GetLaunchClip(projectile), position, 0.9f);
    }

    public static void PlayProjectileImpact(ProjectileController projectile, Vector3 position)
    {
        GalacticSlimefallAudio audio = EnsureInstance();
        audio.PlayWorld(audio.GetImpactClip(projectile), position, 1f);
    }

    void PlayWorld(AudioClip clip, Vector3 position, float volume)
    {
        if (clip == null)
        {
            return;
        }

        GameObject oneShotObject = new GameObject("AudioOneShot");
        oneShotObject.transform.position = position;
        AudioSource source = oneShotObject.AddComponent<AudioSource>();
        source.clip = clip;
        source.volume = volume;
        source.spatialBlend = 0f;
        source.rolloffMode = AudioRolloffMode.Linear;
        source.Play();
        Destroy(oneShotObject, clip.length + 0.1f);
    }

    AudioClip GetLaunchClip(ProjectileController projectile)
    {
        switch (GetProjectileFamily(projectile))
        {
            case ProjectileFamily.Cluster:
                return _clusterLaunch;
            case ProjectileFamily.Nuke:
                return _nukeLaunch;
            case ProjectileFamily.Roller:
                return _rollerLaunch;
            default:
                return _cannonLaunch;
        }
    }

    AudioClip GetImpactClip(ProjectileController projectile)
    {
        switch (GetProjectileFamily(projectile))
        {
            case ProjectileFamily.ClusterShard:
                return _shrapnelImpact;
            case ProjectileFamily.Cluster:
                return _clusterImpact;
            case ProjectileFamily.Nuke:
                return _nukeImpact;
            case ProjectileFamily.Roller:
                return _rollerImpact;
            default:
                return _cannonImpact;
        }
    }

    ProjectileFamily GetProjectileFamily(ProjectileController projectile)
    {
        if (projectile == null)
        {
            return ProjectileFamily.Cannon;
        }

        string projectileName = projectile.name.ToLowerInvariant();

        if (projectileName.Contains("clustershard") || projectileName.Contains("shrapnel"))
        {
            return ProjectileFamily.ClusterShard;
        }

        if (projectile.behavior == ProjectileController.ProjectileBehavior.ClusterBomb || projectileName.Contains("cluster"))
        {
            return ProjectileFamily.Cluster;
        }

        if (projectile.behavior == ProjectileController.ProjectileBehavior.Roller || projectileName.Contains("roller"))
        {
            return ProjectileFamily.Roller;
        }

        if (projectileName.Contains("nuke") || projectile.impactShrapnelCount > 0 || projectile.directDamage >= 30)
        {
            return ProjectileFamily.Nuke;
        }

        return ProjectileFamily.Cannon;
    }

    enum ProjectileFamily
    {
        Cannon,
        Cluster,
        ClusterShard,
        Nuke,
        Roller
    }
}



