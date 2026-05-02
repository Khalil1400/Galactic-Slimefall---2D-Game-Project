using System;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif
[ExecuteAlways]
public class ImageLevelCollisionAuthoring : MonoBehaviour
{
    static Material _runtimeUnlitMaterial;
    const string DefaultLevelSpritePath = "Assets/GalacticSlimefall/Art/ImageLevels/Level base 1.png";

    [Serializable]
    public class ColliderDefinition
    {
        public string name = "Platform";
        public Vector2 normalizedCenter = new(0.5f, 0.5f);
        public Vector2 normalizedSize = new(0.1f, 0.03f);
        public bool oneWayPlatform = true;
        public bool isTrigger = false;
        public int hazardDamage = 0;
        public Vector2 hazardKnockback = Vector2.up;
        public float jumpPadForce = 0f;
        public float jumpPadHorizontalBoost = 0f;
    }

    [Serializable]
    public class BlockerDefinition
    {
        public string name = "Blocker";
        public Vector2 normalizedCenter = new(0.5f, 0.5f);
        public Vector2 normalizedSize = new(0.08f, 0.12f);
    }

    [Header("Visual")]
    public SpriteRenderer visualSprite;
    public Transform generatedRoot;

    [Header("Generated Colliders")]
    public List<ColliderDefinition> colliders = new();
    public bool disableChildRenderers = true;
    public bool hideLegacyWorldRenderers = true;

    [Header("Map Frame")]
    public bool createRuntimeMapFrame = true;
    public float mapFrameThickness = 0.55f;
    public float mapFrameInset = 0.02f;

    [Header("Balance Blockers")]
    public bool createRuntimeBlockers = false;
    public List<BlockerDefinition> blockers = new();

    void OnEnable()
    {
        EnsureVisualRendering();
        if (Application.isPlaying)
        {
            RemoveRuntimeBlockers();
            EnsureRuntimeMapFrame();
            HideLegacyHelperVisuals();
        }
    }

    void Start()
    {
        EnsureVisualRendering();
        if (Application.isPlaying)
        {
            RemoveRuntimeBlockers();
            EnsureRuntimeMapFrame();
            HideLegacyHelperVisuals();
        }
    }

    void Update()
    {
        if (Application.isPlaying)
        {
            return;
        }

        EnsureVisualRendering();
        HideLegacyHelperVisuals();
    }

    [ContextMenu("Load Uploaded Space Preset")]
    public void LoadUploadedSpacePreset()
    {
        colliders = new List<ColliderDefinition>
        {
            new() { name = "LeftTopBridge", normalizedCenter = new Vector2(0.071f, 0.407f), normalizedSize = new Vector2(0.142f, 0.026f), oneWayPlatform = false },
            new() { name = "LeftLowerBlock", normalizedCenter = new Vector2(0.119f, 0.272f), normalizedSize = new Vector2(0.117f, 0.026f), oneWayPlatform = false },
            new() { name = "LeftSmallFloat", normalizedCenter = new Vector2(0.203f, 0.437f), normalizedSize = new Vector2(0.081f, 0.024f), oneWayPlatform = true },
            new() { name = "MidLeftFloat", normalizedCenter = new Vector2(0.331f, 0.349f), normalizedSize = new Vector2(0.099f, 0.024f), oneWayPlatform = true },
            new() { name = "TopCenterFloat", normalizedCenter = new Vector2(0.485f, 0.629f), normalizedSize = new Vector2(0.121f, 0.026f), oneWayPlatform = true },
            new() { name = "CenterLowerMoundTop", normalizedCenter = new Vector2(0.502f, 0.345f), normalizedSize = new Vector2(0.086f, 0.024f), oneWayPlatform = false },
            new() { name = "CenterUpperMoundTop", normalizedCenter = new Vector2(0.593f, 0.453f), normalizedSize = new Vector2(0.082f, 0.024f), oneWayPlatform = false },
            new() { name = "GroundCenterRun", normalizedCenter = new Vector2(0.501f, 0.198f), normalizedSize = new Vector2(0.288f, 0.028f), oneWayPlatform = false },
            new() { name = "HazardFloatTop", normalizedCenter = new Vector2(0.686f, 0.323f), normalizedSize = new Vector2(0.112f, 0.026f), oneWayPlatform = true },
            new() { name = "HazardFloatSpikes", normalizedCenter = new Vector2(0.686f, 0.289f), normalizedSize = new Vector2(0.112f, 0.048f), oneWayPlatform = false, isTrigger = true, hazardDamage = 1, hazardKnockback = new Vector2(0f, 1f) },
            new() { name = "RightUpperFloat", normalizedCenter = new Vector2(0.784f, 0.437f), normalizedSize = new Vector2(0.089f, 0.024f), oneWayPlatform = true },
            new() { name = "RightLowerBlock", normalizedCenter = new Vector2(0.836f, 0.272f), normalizedSize = new Vector2(0.113f, 0.026f), oneWayPlatform = false },
            new() { name = "RightTopBridge", normalizedCenter = new Vector2(0.929f, 0.407f), normalizedSize = new Vector2(0.166f, 0.026f), oneWayPlatform = false },
            new() { name = "RightSmallStep", normalizedCenter = new Vector2(0.910f, 0.210f), normalizedSize = new Vector2(0.079f, 0.022f), oneWayPlatform = false }
        };
    }

    [ContextMenu("Rebuild Colliders")]
    public void RebuildColliders()
    {
        if (visualSprite == null || visualSprite.sprite == null)
        {
            Debug.LogWarning("ImageLevelCollisionAuthoring: assign a SpriteRenderer with the visual level image first.", this);
            return;
        }

        EnsureGeneratedRoot();
        ClearGenerated();

        Vector2 spriteSize = visualSprite.sprite.bounds.size;

        foreach (ColliderDefinition definition in colliders)
        {
            if (definition == null)
            {
                continue;
            }

            GameObject child = new GameObject(definition.name);
            child.transform.SetParent(generatedRoot, false);
            child.transform.localPosition = new Vector3(
                (definition.normalizedCenter.x - 0.5f) * spriteSize.x,
                (definition.normalizedCenter.y - 0.5f) * spriteSize.y,
                0f);

            BoxCollider2D collider = child.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(
                definition.normalizedSize.x * spriteSize.x,
                definition.normalizedSize.y * spriteSize.y);

            bool shouldBeTrigger = definition.isTrigger || definition.hazardDamage > 0 || definition.jumpPadForce > 0f;
            collider.isTrigger = shouldBeTrigger;

            if (definition.oneWayPlatform && !shouldBeTrigger)
            {
                collider.usedByEffector = true;
                PlatformEffector2D effector = child.AddComponent<PlatformEffector2D>();
                effector.surfaceArc = 170f;
                effector.useOneWay = true;
                effector.useOneWayGrouping = true;
                effector.useSideFriction = false;
                effector.useSideBounce = false;
            }

            if (definition.hazardDamage > 0)
            {
                HazardDamage hazard = child.AddComponent<HazardDamage>();
                hazard.Damage = definition.hazardDamage;
                hazard.KnockbackDirection = definition.hazardKnockback;
            }

            if (definition.jumpPadForce > 0f)
            {
                JumpPad jumpPad = child.AddComponent<JumpPad>();
                jumpPad.LaunchForce = definition.jumpPadForce;
                jumpPad.HorizontalBoost = definition.jumpPadHorizontalBoost;
            }
        }

        RemoveRuntimeBlockers();
        RemoveRuntimeMapFrame();
    }

    [ContextMenu("Clear Generated Colliders")]
    public void ClearGenerated()
    {
        if (generatedRoot == null)
        {
            return;
        }

        for (int i = generatedRoot.childCount - 1; i >= 0; i--)
        {
            Transform child = generatedRoot.GetChild(i);
            if (Application.isPlaying)
            {
                Destroy(child.gameObject);
            }
            else
            {
                DestroyImmediate(child.gameObject);
            }
        }
    }

    void EnsureGeneratedRoot()
    {
        if (generatedRoot != null)
        {
            return;
        }

        Transform existing = transform.Find("GeneratedColliders");
        if (existing != null)
        {
            generatedRoot = existing;
            return;
        }

        GameObject root = new GameObject("GeneratedColliders");
        root.transform.SetParent(transform, false);
        generatedRoot = root.transform;
    }

    void NormalizeGeneratedColliderTransforms()
    {
        if (generatedRoot == null)
        {
            return;
        }

        foreach (Transform child in generatedRoot)
        {
            if (child == null)
            {
                continue;
            }

            if (child.TryGetComponent<Collider2D>(out _) || child.GetComponentInChildren<Collider2D>(true) != null)
            {
                child.localScale = Vector3.one;
                child.localRotation = Quaternion.identity;
            }
        }
    }

    void RemoveRuntimeBlockers()
    {
        if (generatedRoot == null)
        {
            return;
        }

        Transform blockerRoot = generatedRoot.Find("RuntimeBlockers");
        if (blockerRoot == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(blockerRoot.gameObject);
        }
        else
        {
            DestroyImmediate(blockerRoot.gameObject);
        }
    }

    void EnsureRuntimeMapFrame()
    {
        if (!createRuntimeMapFrame || visualSprite == null || visualSprite.sprite == null)
        {
            RemoveRuntimeMapFrame();
            return;
        }

        EnsureGeneratedRoot();

        Transform frameRoot = generatedRoot.Find("RuntimeMapFrame");
        if (frameRoot == null)
        {
            GameObject frameObject = new GameObject("RuntimeMapFrame");
            frameObject.transform.SetParent(generatedRoot, false);
            frameRoot = frameObject.transform;
        }

        SyncFrameWall(frameRoot, "LeftWall",
            new Vector2(-visualSprite.sprite.bounds.size.x * 0.5f - mapFrameThickness * 0.5f + mapFrameInset, 0f),
            new Vector2(mapFrameThickness, visualSprite.sprite.bounds.size.y + mapFrameThickness * 2f));
        SyncFrameWall(frameRoot, "RightWall",
            new Vector2(visualSprite.sprite.bounds.size.x * 0.5f + mapFrameThickness * 0.5f - mapFrameInset, 0f),
            new Vector2(mapFrameThickness, visualSprite.sprite.bounds.size.y + mapFrameThickness * 2f));
        SyncFrameWall(frameRoot, "TopWall",
            new Vector2(0f, visualSprite.sprite.bounds.size.y * 0.5f + mapFrameThickness * 0.5f - mapFrameInset),
            new Vector2(visualSprite.sprite.bounds.size.x + mapFrameThickness * 2f, mapFrameThickness));
        SyncFrameWall(frameRoot, "BottomWall",
            new Vector2(0f, -visualSprite.sprite.bounds.size.y * 0.5f - mapFrameThickness * 0.5f + mapFrameInset),
            new Vector2(visualSprite.sprite.bounds.size.x + mapFrameThickness * 2f, mapFrameThickness));
    }

    void SyncFrameWall(Transform frameRoot, string wallName, Vector2 localPosition, Vector2 size)
    {
        Transform existing = frameRoot.Find(wallName);
        GameObject wallObject;
        if (existing == null)
        {
            wallObject = new GameObject(wallName);
            wallObject.transform.SetParent(frameRoot, false);
        }
        else
        {
            wallObject = existing.gameObject;
        }

        wallObject.transform.localPosition = new Vector3(localPosition.x, localPosition.y, 0f);
        wallObject.transform.localRotation = Quaternion.identity;
        wallObject.transform.localScale = Vector3.one;

        BoxCollider2D collider = wallObject.GetComponent<BoxCollider2D>();
        if (collider == null)
        {
            collider = wallObject.AddComponent<BoxCollider2D>();
        }

        collider.size = size;
        collider.isTrigger = false;

        if (wallObject.GetComponent<MapBoundaryFrame>() == null)
        {
            wallObject.AddComponent<MapBoundaryFrame>();
        }
    }

    void RemoveRuntimeMapFrame()
    {
        if (generatedRoot == null)
        {
            return;
        }

        Transform frameRoot = generatedRoot.Find("RuntimeMapFrame");
        if (frameRoot == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(frameRoot.gameObject);
        }
        else
        {
            DestroyImmediate(frameRoot.gameObject);
        }
    }

    void EnsureVisualRendering()
    {
        TryRestoreVisualSprite();

        if (visualSprite == null)
        {
            return;
        }

        if (_runtimeUnlitMaterial == null)
        {
            Shader shader = Shader.Find("Sprites/Default");
            if (shader != null)
            {
                _runtimeUnlitMaterial = new Material(shader)
                {
                    name = "RuntimeSpriteUnlitMaterial"
                };
            }
        }

        if (_runtimeUnlitMaterial != null)
        {
            visualSprite.sharedMaterial = _runtimeUnlitMaterial;
        }

        visualSprite.sortingOrder = -50;
        visualSprite.color = Color.white;
        visualSprite.enabled = true;
    }

    void TryRestoreVisualSprite()
    {
        if (visualSprite == null)
        {
            visualSprite = GetComponent<SpriteRenderer>();
        }

        if (visualSprite == null || visualSprite.sprite != null)
        {
            return;
        }

#if UNITY_EDITOR
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(DefaultLevelSpritePath);
        if (sprite == null)
        {
            string[] guids = AssetDatabase.FindAssets("t:Sprite", new[] { "Assets/GalacticSlimefall/Art/ImageLevels" });
            foreach (string guid in guids)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
                if (sprite != null)
                {
                    break;
                }
            }
        }

        if (sprite != null)
        {
            visualSprite.sprite = sprite;
            EditorUtility.SetDirty(gameObject);
        }
#endif
    }

    void HideLegacyHelperVisuals()
    {
        if (!hideLegacyWorldRenderers)
        {
            return;
        }

        GameObject world = GameObject.Find("World");
        if (world != null)
        {
            foreach (SpriteRenderer renderer in world.GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (renderer == null)
                {
                    continue;
                }

                if (visualSprite != null &&
                    (renderer == visualSprite || renderer.transform.IsChildOf(visualSprite.transform)))
                {
                    renderer.enabled = true;
                    continue;
                }

                renderer.enabled = false;
            }
        }

        if (!disableChildRenderers || generatedRoot == null)
        {
            return;
        }

        foreach (SpriteRenderer renderer in generatedRoot.GetComponentsInChildren<SpriteRenderer>(true))
        {
            if (renderer != null)
            {
                renderer.enabled = false;
            }
        }
    }

}

