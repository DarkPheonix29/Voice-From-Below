using System;

public static class DynamiteTracker
{
    // Initialize SessionCount to 0 explicitly.
    // NOTE: C# guarantees this, but explicit initialization prevents potential confusion
    // and ensures consistency across all environments.
    public static int SessionCount { get; private set; } = 0; 

    public static event Action<int> OnChanged;

    public static void Add(int amount = 1)
    {
        SessionCount += amount;
        if (SessionCount < 0) SessionCount = 0;
        OnChanged?.Invoke(SessionCount);
        // Added log for debugging the director
        UnityEngine.Debug.Log($"[DynamiteTracker] Added {amount}. Total: {SessionCount}"); 
    }

    public static void Reset()
    {
        SessionCount = 0;
        OnChanged?.Invoke(SessionCount);
    }
}