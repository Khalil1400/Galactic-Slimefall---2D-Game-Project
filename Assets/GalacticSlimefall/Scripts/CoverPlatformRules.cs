using System.Collections.Generic;
using UnityEngine;
public static class CoverPlatformRules
{
    static readonly HashSet<string> CoverPlatformNames = new(System.StringComparer.OrdinalIgnoreCase)
    {
        "LeftSmallFloat",
        "MidLeftFloat",
        "HazardFloatTop",
        "RightUpperFloat",
        "RightLowerBlock",
        "RightTopBridge",
        "LeftTopBridge"
    };

    public static bool ShouldActAsCover(Collider2D collider)
    {
        return IsNamedCoverPlatform(collider) && HasUnitShelteringUnderPlatform(collider);
    }

    public static bool IsNamedCoverPlatform(Collider2D collider)
    {
        if (collider == null)
        {
            return false;
        }

        string objectName = collider.gameObject.name;
        string parentName = collider.transform.parent != null ? collider.transform.parent.name : string.Empty;
        return CoverPlatformNames.Contains(objectName) || CoverPlatformNames.Contains(parentName);
    }

    public static bool HasUnitShelteringUnderPlatform(Collider2D collider)
    {
        if (collider == null)
        {
            return false;
        }

        Bounds bounds = collider.bounds;
        Vector2 probeCenter = new(bounds.center.x, bounds.min.y - 0.42f);
        Vector2 probeSize = new(Mathf.Max(0.3f, bounds.size.x * 0.94f), 1.15f);
        Collider2D[] hits = Physics2D.OverlapBoxAll(probeCenter, probeSize, 0f);
        foreach (Collider2D hit in hits)
        {
            Unit unit = hit != null
                ? hit.GetComponent<Unit>() ?? hit.GetComponentInParent<Unit>() ?? hit.attachedRigidbody?.GetComponent<Unit>()
                : null;

            if (unit != null && !unit.IsDead)
            {
                return true;
            }
        }

        return false;
    }
}

