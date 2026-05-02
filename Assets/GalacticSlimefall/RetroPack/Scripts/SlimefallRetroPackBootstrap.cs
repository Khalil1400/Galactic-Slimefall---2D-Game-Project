using UnityEngine;

public static class SlimefallRetroPackBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (Object.FindFirstObjectByType<SlimefallRetroRuntimeRoot>() != null)
        {
            return;
        }

        GameObject root = new GameObject("SlimefallRetroRuntimeRoot");
        Object.DontDestroyOnLoad(root);
        root.AddComponent<SlimefallRetroRuntimeRoot>();
        root.AddComponent<SlimefallRetroHudStyler>();
        root.AddComponent<SlimefallRetroTrajectoryStyler>();
        root.AddComponent<SlimefallRetroSlimeManager>();
    }
}

public class SlimefallRetroRuntimeRoot : MonoBehaviour { }

