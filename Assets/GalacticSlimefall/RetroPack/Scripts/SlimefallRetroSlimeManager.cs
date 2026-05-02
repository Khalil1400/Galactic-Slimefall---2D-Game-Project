using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SlimefallRetroSlimeManager : MonoBehaviour
{
    readonly HashSet<Unit> seenUnits = new();
    readonly HashSet<int> seenProjectiles = new();

    IEnumerator Start()
    {
        while (true)
        {
            AttachToUnits();
            DetectShots();
            yield return new WaitForSeconds(0.08f);
        }
    }

    void AttachToUnits()
    {
        Unit[] units = Object.FindObjectsByType<Unit>(FindObjectsSortMode.None);
        foreach (Unit unit in units)
        {
            if (unit == null)
            {
                continue;
            }

            SlimefallRetroSlimeVisual vis = unit.GetComponent<SlimefallRetroSlimeVisual>();
            if (vis == null)
            {
                vis = unit.gameObject.AddComponent<SlimefallRetroSlimeVisual>();
            }

            vis.SetTeam(unit.isPlayerTeam);

            if (seenUnits.Contains(unit))
            {
                continue;
            }

            seenUnits.Add(unit);

            if (unit.bodyRenderer == null)
            {
                unit.bodyRenderer = unit.GetComponent<SpriteRenderer>();
            }

            if (unit.bodyRenderer == null)
            {
                unit.bodyRenderer = unit.gameObject.AddComponent<SpriteRenderer>();
            }

            unit.bodyRenderer.sortingOrder = 10;
        }
    }

    void DetectShots()
    {
        ProjectileController[] projectiles = Object.FindObjectsByType<ProjectileController>(FindObjectsSortMode.None);
        foreach (ProjectileController projectile in projectiles)
        {
            if (projectile == null) continue;
            int id = projectile.GetInstanceID();
            if (seenProjectiles.Contains(id)) continue;

            seenProjectiles.Add(id);

            Unit closest = null;
            float best = 2.2f;
            foreach (Unit unit in seenUnits)
            {
                if (unit == null) continue;
                float d = Vector2.Distance(unit.transform.position, projectile.transform.position);
                if (d < best)
                {
                    best = d;
                    closest = unit;
                }
            }

            if (closest != null)
            {
                SlimefallRetroSlimeVisual vis = closest.GetComponent<SlimefallRetroSlimeVisual>();
                if (vis != null) vis.TriggerShoot();
            }
        }
    }
}

