using UnityEngine;

public static class SFXRouterDebug
{
    public delegate void Played(string id, AudioClip clip, AudioSource src, float pitch, float volume);
    public static event Played OnPlayed;

    public static void Emit(string id, AudioClip clip, AudioSource src, float pitch, float volume)
    {
        OnPlayed?.Invoke(id, clip, src, pitch, volume);
    }
}
