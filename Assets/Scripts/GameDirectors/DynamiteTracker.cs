using System;

public static class DynamiteTracker
{
    // readable from anywhere, writable only inside this class
    public static int SessionCount { get; private set; }

    public static event Action<int> OnChanged;

    public static void Add(int amount = 1)
    {
        SessionCount += amount;
        if (SessionCount < 0) SessionCount = 0;
        OnChanged?.Invoke(SessionCount);
    }

    public static void Reset()
    {
        SessionCount = 0;
        OnChanged?.Invoke(SessionCount);
    }
}
