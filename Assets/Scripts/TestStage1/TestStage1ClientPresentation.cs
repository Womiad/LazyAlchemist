using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public class TestStage1ClientPresentation : MonoBehaviour
{
    private const string ClientUiName = "ClientUI";
    private const string HostModelName = "Host3D";

    private readonly Dictionary<Camera, CameraRenderState> cameraStates = new Dictionary<Camera, CameraRenderState>();

    private GameObject clientUi;
    private GameObject hostModel;
    private bool? clientOnlyApplied;
    private bool? hostModelVisibleApplied;

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
                return;
            }
        }
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
