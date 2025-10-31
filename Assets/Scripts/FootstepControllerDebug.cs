using UnityEngine;

public static class FootstepControllerDebug
{
    public delegate void Step(AudioClip clip, AudioSource src, float pitch, float volume);
    public static event Step OnStep;

    public static void Emit(AudioClip clip, AudioSource src, float pitch, float volume)
    {
        OnStep?.Invoke(clip, src, pitch, volume);
    }
}
