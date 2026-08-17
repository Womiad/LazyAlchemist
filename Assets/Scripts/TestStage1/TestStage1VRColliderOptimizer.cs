using System;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Disables non-essential colliders in TestStage1_VR at runtime.
/// The scene asset is left untouched, so designers can still select and edit props normally.
/// </summary>
[DefaultExecutionOrder(-900)]
public sealed class TestStage1VRColliderOptimizer : MonoBehaviour
{
    private static readonly string[] InteractiveKeywords =
    {
        "crystal", "firefly", "slime", "craftingtable", "slimebait",
        "staff", "pickaxe", "magic", "book", "door", "lever", "altar",
        "chest", "cauldron", "button", "trigger", "spawn", "interact",
        "collect", "player", "host3d", "vbasiccontroller"
    };

    private static readonly string[] StructuralKeywords =
    {
        "ground", "floor", "terrain", "wall", "rockwall", "cave", "cliff",
        "path", "road", "bridge", "platform", "plane", "boundary"
    };

    private static readonly string[] DecorativeKeywords =
    {
        "flower", "grass", "plant", "mushroom", "fern", "bush", "tree",
        "stair", "ladder", "crate", "barrel", "bucket", "pot", "vase",
        "chair", "bench", "cart", "tent", "candle", "decoration", "decor", "prop"
    };

    private bool optimized;

    private void Awake()
    {
        Optimize();
    }

    [ContextMenu("Optimize VR Scene Colliders")]
    public void Optimize()
    {
        if (optimized)
        {
            return;
        }

        optimized = true;
        Scene activeScene = SceneManager.GetActiveScene();
        Collider[] colliders = FindObjectsByType<Collider>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        int disabledCount = 0;
        int keptCount = 0;
        int alreadyDisabledCount = 0;

        foreach (Collider sceneCollider in colliders)
        {
            if (sceneCollider == null || sceneCollider.gameObject.scene != activeScene)
            {
                continue;
            }

            if (!sceneCollider.enabled)
            {
                alreadyDisabledCount++;
                continue;
            }

            if (ShouldKeep(sceneCollider))
            {
                keptCount++;
                continue;
            }

            sceneCollider.enabled = false;
            disabledCount++;
        }

        Debug.Log(
            $"[TestStage1 VR] Collider optimization complete: disabled {disabledCount}, " +
            $"kept {keptCount}, already disabled {alreadyDisabledCount}.",
            this);
    }

    private static bool ShouldKeep(Collider sceneCollider)
    {
        if (sceneCollider is TerrainCollider || sceneCollider.isTrigger)
        {
            return true;
        }

        if (sceneCollider.GetComponentInParent<TestStage1KeepCollider>(true) != null ||
            sceneCollider.GetComponentInParent<TestStage1SlimeSmallController>(true) != null)
        {
            return true;
        }

        if (HierarchyMatches(sceneCollider.transform, InteractiveKeywords) || IsPlayerHierarchy(sceneCollider.transform))
        {
            return true;
        }

        // Explicit decoration names take priority over broad structural names such as "cave".
        if (HierarchyMatches(sceneCollider.transform, DecorativeKeywords))
        {
            return false;
        }

        if (HierarchyMatches(sceneCollider.transform, StructuralKeywords))
        {
            return true;
        }

        // Keep unnamed, large colliders because they are commonly terrain chunks or invisible boundaries.
        Bounds bounds = sceneCollider.bounds;
        float largestDimension = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
        return largestDimension >= 8f;
    }

    private static bool IsPlayerHierarchy(Transform start)
    {
        for (Transform current = start; current != null; current = current.parent)
        {
            if (current.CompareTag("Player"))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HierarchyMatches(Transform start, string[] keywords)
    {
        for (Transform current = start; current != null; current = current.parent)
        {
            string normalizedName = Normalize(current.name);
            foreach (string keyword in keywords)
            {
                if (normalizedName.Contains(keyword, StringComparison.Ordinal))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static string Normalize(string value)
    {
        return value.Replace(" ", string.Empty)
            .Replace("_", string.Empty)
            .Replace("-", string.Empty)
            .ToLowerInvariant();
    }
}
