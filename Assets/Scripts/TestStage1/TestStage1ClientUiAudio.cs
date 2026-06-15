using UnityEngine;

[DisallowMultipleComponent]
public class TestStage1ClientUiAudio : MonoBehaviour
{
    public static TestStage1ClientUiAudio Instance { get; private set; }

    [SerializeField] private AudioClip pageTurnClip;
    [SerializeField] private AudioClip constructClip;
    [SerializeField] private AudioClip changeItemClip;
    [SerializeField, Range(0f, 1f)] private float volume = 0.8f;

    private AudioSource audioSource;

    private void Awake()
    {
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public static void PlayPageTurn()
    {
        Instance?.Play(Instance.pageTurnClip);
    }

    public static void PlayConstruct()
    {
        Instance?.Play(Instance.constructClip);
    }

    public static void PlayItemChange()
    {
        Instance?.Play(Instance.changeItemClip);
    }

    private void Play(AudioClip clip)
    {
        if (clip == null)
        {
            return;
        }

        EnsureAudioSource();
        audioSource.PlayOneShot(clip, volume);
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
}
