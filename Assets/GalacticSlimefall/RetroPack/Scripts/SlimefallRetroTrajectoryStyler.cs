using System.Collections;
using UnityEngine;

public class SlimefallRetroTrajectoryStyler : MonoBehaviour
{
    Texture2D lineTexture;
    Sprite impactSprite;
    readonly Color slimeMint = new(0.23f, 1f, 0.84f, 1f);
    readonly Color hotGold = new(1f, 0.84f, 0.38f, 1f);

    IEnumerator Start()
    {
        yield return null;
        yield return null;
        EnsureAssets();
        StartCoroutine(StyleLoop());
    }

    IEnumerator StyleLoop()
    {
        while (true)
        {
            StyleAimingSystem();
            AnimateImpactMarker();
            yield return new WaitForSeconds(0.15f);
        }
    }

    void EnsureAssets()
    {
        if (lineTexture == null)
        {
            lineTexture = new Texture2D(24, 24, TextureFormat.RGBA32, false);
            lineTexture.filterMode = FilterMode.Point;
            for (int y = 0; y < 24; y++)
            {
                for (int x = 0; x < 24; x++)
                {
                    float dx = x - 11.5f;
                    float dy = y - 11.5f;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    Color c = dist < 7f ? hotGold : (dist < 10f ? slimeMint : new Color(0,0,0,0));
                    lineTexture.SetPixel(x, y, c);
                }
            }
            lineTexture.Apply();
        }

        if (impactSprite == null)
        {
            Texture2D tex = new Texture2D(64, 64, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;
            for (int y = 0; y < 64; y++)
            {
                for (int x = 0; x < 64; x++)
                {
                    float dx = x - 31.5f;
                    float dy = y - 31.5f;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    bool outer = dist > 24 && dist < 28;
                    bool inner = dist > 14 && dist < 16;
                    bool cross = (Mathf.Abs(dx) < 2 && dist < 30) || (Mathf.Abs(dy) < 2 && dist < 30);
                    Color c = (outer || inner) ? hotGold : (cross ? slimeMint : new Color(0,0,0,0));
                    tex.SetPixel(x, y, c);
                }
            }
            tex.Apply();
            impactSprite = Sprite.Create(tex, new Rect(0,0,64,64), new Vector2(0.5f,0.5f), 64f);
        }
    }

    void StyleAimingSystem()
    {
        AimingSystem aim = AimingSystem.Instance;
        if (aim == null) return;

        if (aim.trajectoryLine == null)
        {
            GameObject go = new GameObject("GB_TrajectoryLine");
            LineRenderer lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            aim.trajectoryLine = lr;
        }

        LineRenderer line = aim.trajectoryLine;
        line.textureMode = LineTextureMode.Tile;
        line.alignment = LineAlignment.View;
        line.numCapVertices = 4;
        line.numCornerVertices = 2;
        line.widthMultiplier = 0.12f;
        line.startWidth = 0.13f;
        line.endWidth = 0.05f;
        line.material = new Material(Shader.Find("Sprites/Default"));
        line.material.mainTexture = lineTexture;
        line.material.color = Color.white;
        line.startColor = slimeMint;
        line.endColor = hotGold;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;
        line.sortingOrder = 30;

        if (aim.impactMarker != null)
        {
            aim.impactMarker.sprite = impactSprite;
            aim.impactMarker.color = Color.white;
            aim.impactMarker.transform.localScale = Vector3.one * 0.55f;
            aim.impactMarker.sortingOrder = 35;
        }
    }

    void AnimateImpactMarker()
    {
        AimingSystem aim = AimingSystem.Instance;
        if (aim == null || aim.impactMarker == null || !aim.impactMarker.enabled) return;
        float pulse = 1f + Mathf.Sin(Time.time * 10f) * 0.08f;
        aim.impactMarker.transform.localScale = Vector3.one * (0.52f * pulse);
    }
}

