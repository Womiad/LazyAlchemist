using Invector.vCharacterController;
using Unity.Netcode;
using UnityEngine;

[DisallowMultipleComponent]
public class TestStage1HostFootstepAudio : MonoBehaviour
{
    [SerializeField] private AudioClip footstepClip;
    [SerializeField, Range(0f, 1f)] private float volume = 0.45f;
    [SerializeField] private float minHorizontalSpeed = 0.1f;
    [SerializeField] private float inputThreshold = 0.1f;

    private AudioSource audioSource;
    private Rigidbody hostRigidbody;
    private vThirdPersonInput hostInput;

    private void Awake()
    {
        hostRigidbody = GetComponent<Rigidbody>();
        hostInput = GetComponent<vThirdPersonInput>();
        EnsureAudioSource();
    }

    private void Update()
    {
        if (!IsHost())
        {
            StopFootsteps();
            return;
        }

        if (ShouldPlayFootsteps())
        {
            PlayFootsteps();
        }
        else
        {
            StopFootsteps();
        }
    }

    private void OnDisable()
    {
        StopFootsteps();
    }

    private bool ShouldPlayFootsteps()
    {
        if (footstepClip == null)
        {
            return false;
        }

        return HasMovementInput() && GetHorizontalSpeed() > minHorizontalSpeed;
    }

    private bool HasMovementInput()
    {
        if (GetComponent<TestStage1VRHostController>() != null)
        {
            return TestStage1VRHostController.MoveAxis.sqrMagnitude > inputThreshold * inputThreshold;
        }

        if (hostInput == null)
        {
            return Input.GetAxisRaw("Horizontal") * Input.GetAxisRaw("Horizontal")
                + Input.GetAxisRaw("Vertical") * Input.GetAxisRaw("Vertical") > inputThreshold * inputThreshold;
        }

        var horizontal = Input.GetAxisRaw(hostInput.horizontalInput);
        var vertical = Input.GetAxisRaw(hostInput.verticallInput);
        return horizontal * horizontal + vertical * vertical > inputThreshold * inputThreshold;
    }

    private float GetHorizontalSpeed()
    {
        if (hostRigidbody == null)
        {
            return 0f;
        }

        var velocity = hostRigidbody.linearVelocity;
        velocity.y = 0f;
        return velocity.magnitude;
    }

    private void PlayFootsteps()
    {
        EnsureAudioSource();
        if (audioSource == null || audioSource.isPlaying)
        {
            return;
        }

        audioSource.clip = footstepClip;
        audioSource.volume = volume;
        audioSource.loop = true;
        audioSource.Play();
    }

    private void StopFootsteps()
    {
        if (audioSource != null && audioSource.isPlaying)
        {
            audioSource.Stop();
        }
    }

    private void EnsureAudioSource()
    {
        if (audioSource != null)
        {
            return;
        }

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }

        audioSource.playOnAwake = false;
        audioSource.loop = true;
        audioSource.spatialBlend = 0f;
    }

    private static bool IsHost()
    {
        return TestStage1DebugMode.IsHost;
    }
}
