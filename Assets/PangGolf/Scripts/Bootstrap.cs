using UnityEngine;

namespace PangGolf
{
    /// <summary>Starts the game automatically when Play is pressed in any scene.</summary>
    public static class Bootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init()
        {
            if (Object.FindFirstObjectByType<GolfGame>() == null)
                new GameObject("PangGolf").AddComponent<GolfGame>();
        }
    }
}
