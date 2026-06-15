using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class TestStage1ClientPresentation : MonoBehaviour
{
    private const string ClientUiName = "ClientUI";
    private const string HostModelName = "Host3D";
    private static readonly Vector2 ClientUiPortraitReferenceResolution = new Vector2(1080f, 1920f);
    private static readonly Vector2 ClientUiLandscapeReferenceResolution = new Vector2(1920f, 1080f);
    private const float ClientUiMaxLandscapeScale = 1.55f;

    private readonly Dictionary<Camera, CameraRenderState> cameraStates = new Dictionary<Camera, CameraRenderState>();

    private GameObject clientUi;
    private CanvasScaler clientUiScaler;
    private GameObject hostModel;
    private bool? clientOnlyApplied;
    private bool? hostModelVisibleApplied;
    private int appliedClientUiScreenWidth;
    private int appliedClientUiScreenHeight;

    private void Update()
    {
        if (NetworkManager.Singleton == null)
        {
            ApplyClientOnly(false);
            ApplyHostModelVisible(false);
            return;
        }

        ApplyClientOnly(NetworkManager.Singleton.IsClient && !NetworkManager.Singleton.IsHost);
        ApplyHostModelVisible(NetworkManager.Singleton.IsHost);
    }

    private void OnDestroy()
    {
        ApplyClientOnly(false);
        ApplyHostModelVisible(false);
    }

    private void ApplyClientOnly(bool isClientOnly)
    {
        EnsureClientUiReference();
        ApplyClientUiResponsiveScale();

        if (clientUi != null && clientUi.activeSelf != isClientOnly)
        {
            clientUi.SetActive(isClientOnly);
        }

        if (clientOnlyApplied == isClientOnly && cameraStates.Count > 0)
        {
            return;
        }

        clientOnlyApplied = isClientOnly;

        foreach (var sceneCamera in FindObjectsByType<Camera>(FindObjectsSortMode.None))
        {
            if (sceneCamera == null || IsUnderClientUi(sceneCamera.transform))
            {
                continue;
            }

            if (!cameraStates.ContainsKey(sceneCamera))
            {
                cameraStates.Add(sceneCamera, new CameraRenderState(sceneCamera));
            }

            if (isClientOnly)
            {
                sceneCamera.cullingMask = 0;
                sceneCamera.clearFlags = CameraClearFlags.SolidColor;
                sceneCamera.backgroundColor = Color.black;
            }
            else
            {
                cameraStates[sceneCamera].Restore(sceneCamera);
            }
        }
    }

    private void EnsureClientUiReference()
    {
        if (clientUi != null)
        {
            return;
        }

        foreach (var rootObject in SceneManager.GetActiveScene().GetRootGameObjects())
        {
            clientUi = FindChildByName(rootObject.transform, ClientUiName);
            if (clientUi != null)
            {
                clientUiScaler = null;
                return;
            }
        }
    }

    private void ApplyClientUiResponsiveScale()
    {
        if (clientUi == null || (appliedClientUiScreenWidth == Screen.width && appliedClientUiScreenHeight == Screen.height))
        {
            return;
        }

        if (clientUiScaler == null)
        {
            clientUiScaler = clientUi.GetComponentInChildren<CanvasScaler>(true);
        }

        if (clientUiScaler == null)
        {
            return;
        }

        var isLandscape = Screen.width >= Screen.height;

        if (isLandscape)
        {
            clientUiScaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            clientUiScaler.scaleFactor = Mathf.Min(
                ClientUiMaxLandscapeScale,
                Mathf.Min(
                    Screen.width / ClientUiLandscapeReferenceResolution.x,
                    Screen.height / ClientUiLandscapeReferenceResolution.y
                )
            );
        }
        else
        {
            clientUiScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            clientUiScaler.referenceResolution = ClientUiPortraitReferenceResolution;
            clientUiScaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            clientUiScaler.matchWidthOrHeight = 0.5f;
        }

        appliedClientUiScreenWidth = Screen.width;
        appliedClientUiScreenHeight = Screen.height;
    }

    private void ApplyHostModelVisible(bool isVisible)
    {
        EnsureHostModelReference();

        if (hostModelVisibleApplied == isVisible)
        {
            return;
        }

        hostModelVisibleApplied = isVisible;

        if (hostModel != null && hostModel.activeSelf != isVisible)
        {
            hostModel.SetActive(isVisible);
        }
    }

    private void EnsureHostModelReference()
    {
        if (hostModel != null)
        {
            return;
        }

        foreach (var rootObject in SceneManager.GetActiveScene().GetRootGameObjects())
        {
            hostModel = FindChildByName(rootObject.transform, HostModelName);
            if (hostModel != null)
            {
                return;
            }
        }
    }

    private bool IsUnderClientUi(Transform target)
    {
        return clientUi != null && target != null && target.IsChildOf(clientUi.transform);
    }

    private static GameObject FindChildByName(Transform parent, string objectName)
    {
        if (parent.name == objectName)
        {
            return parent.gameObject;
        }

        for (var i = 0; i < parent.childCount; i++)
        {
            var match = FindChildByName(parent.GetChild(i), objectName);
            if (match != null)
            {
                return match;
            }
        }

        return null;
    }

    private readonly struct CameraRenderState
    {
        private readonly int cullingMask;
        private readonly CameraClearFlags clearFlags;
        private readonly Color backgroundColor;

        public CameraRenderState(Camera camera)
        {
            cullingMask = camera.cullingMask;
            clearFlags = camera.clearFlags;
            backgroundColor = camera.backgroundColor;
        }

        public void Restore(Camera camera)
        {
            camera.cullingMask = cullingMask;
            camera.clearFlags = clearFlags;
            camera.backgroundColor = backgroundColor;
        }
    }
}
