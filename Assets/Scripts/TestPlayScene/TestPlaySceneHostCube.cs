using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class TestPlaySceneHostCube : MonoBehaviour
{
    public static TestPlaySceneHostCube Instance { get; private set; }

    [SerializeField] private float moveSpeed = 4f;
    [SerializeField] private Renderer targetRenderer;

    private void Awake()
    {
        Instance = this;

        if (targetRenderer == null)
        {
            targetRenderer = GetComponentInChildren<Renderer>();
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void Start()
    {
        var isHost = NetworkManager.Singleton != null && NetworkManager.Singleton.IsHost;
        SetCubeVisible(isHost);

        if (isHost)
        {
            transform.position = new Vector3(0f, 0.5f, 0f);
            ApplyRandomColor();
        }
    }

    private void Update()
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsHost)
        {
            return;
        }

        var keyboard = Keyboard.current;
        if (keyboard == null)
        {
            return;
        }

        var horizontal = 0f;
        if (keyboard.aKey.isPressed)
        {
            horizontal -= 1f;
        }
        if (keyboard.dKey.isPressed)
        {
            horizontal += 1f;
        }

        var vertical = 0f;
        if (keyboard.sKey.isPressed)
        {
            vertical -= 1f;
        }
        if (keyboard.wKey.isPressed)
        {
            vertical += 1f;
        }

        var input = new Vector3(horizontal, 0f, vertical);
        if (input.sqrMagnitude > 1f)
        {
            input.Normalize();
        }

        if (input.sqrMagnitude <= 0f)
        {
            return;
        }

        transform.position += input * moveSpeed * Time.deltaTime;
        transform.position = new Vector3(transform.position.x, 0.5f, transform.position.z);
    }

    public void ApplyRandomColor()
    {
        if (targetRenderer == null)
        {
            return;
        }

        targetRenderer.material.color = Random.ColorHSV(0f, 1f, 0.65f, 1f, 0.65f, 1f);
    }

    private void SetCubeVisible(bool visible)
    {
        if (targetRenderer != null)
        {
            targetRenderer.enabled = visible;
        }
    }
}
