using Unity.Netcode;
using UnityEngine;

[DisallowMultipleComponent]
public class TestStage1ItemChangeAudio : MonoBehaviour
{
    [SerializeField] private AudioClip changeItemClip;
    [SerializeField, Range(0f, 1f)] private float volume = 0.75f;

    private AudioSource audioSource;

    private void OnEnable()
    {
        TestStage1HandState.Changed += OnHandStateChanged;
    }

    private void OnDisable()
    {
        TestStage1HandState.Changed -= OnHandStateChanged;
    }

    private void OnHandStateChanged(TestStage1PlayerState state)
    {
        if (!ShouldPlayForLocalPlayer() || changeItemClip == null)
        {
            return;
        }

        EnsureAudioSource();
        audioSource.PlayOneShot(changeItemClip, volume);
    }

    private void EnsureAudioSource()
    {
        if (audioSource != null)
        {
            return;
        }

        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.loop = false;
        audioSource.spatialBlend = 0f;
    }

    private static bool ShouldPlayForLocalPlayer()
    {
        return TestStage1DebugMode.IsEnabled
            || NetworkManager.Singleton == null
            || NetworkManager.Singleton.IsHost
            || (NetworkManager.Singleton.IsClient && !NetworkManager.Singleton.IsServer);
    }
}
