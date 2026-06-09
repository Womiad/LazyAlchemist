using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

public class TestStage1StaffMagicAttackController : MonoBehaviour
{
    private const string AttackMessageName = "TestStage1.StaffMagicAttack";
    private const string MagicRootName = "magic";
    private const string PlayerTag = "Player";
    private const float LoopPlaySeconds = 1f;
    private const float DeactivateSafetyDelaySeconds = 3f;
    private const float AdvantageDamage = 35f;
    private const float NeutralDamage = 1f;
    private const float SameElementDamage = 0f;

    [Header("Prefab Effects")]
    [SerializeField] private Transform nonMagicEffectPrefab;
    [SerializeField] private Transform fireEffectPrefab;
    [SerializeField] private Transform landEffectPrefab;
    [SerializeField] private Transform windEffectPrefab;
    [SerializeField] private Transform waterEffectPrefab;
    [SerializeField] private Transform effectSpawnRoot;

    [Header("Scene Fallback")]
    [SerializeField] private bool useMagicRootChildrenFallback = true;

    [Header("Host Audio")]
    [SerializeField] private AudioClip nonMagicAudioClip;
    [SerializeField] private AudioClip fireMagicAudioClip;
    [SerializeField] private AudioClip landMagicAudioClip;
    [SerializeField] private AudioClip windMagicAudioClip;
    [SerializeField] private AudioClip waterMagicAudioClip;
    [SerializeField, Range(0f, 1f)] private float magicAudioVolume = 0.7f;

    [Header("Damage")]
    [SerializeField] private float attackRadius = 5f;
    [SerializeField] private LayerMask damageMask = ~0;
    [SerializeField] private bool showDebugAttackRange = true;
    [SerializeField] private float debugAttackRangeSeconds = 1f;
    [SerializeField] private Color debugAttackRangeColor = new Color(1f, 0.25f, 0.1f, 0.85f);

    private readonly Dictionary<TestStage1ElementPower, Transform> effectsByElement =
        new Dictionary<TestStage1ElementPower, Transform>();

    private readonly Dictionary<Transform, Coroutine> runningEffects = new Dictionary<Transform, Coroutine>();
    private Transform magicRoot;
    private Transform runtimeEffectSpawnRoot;
    private AudioSource magicAudioSource;
    private bool initialized;
    private bool clientHandlerRegistered;

    private void Update()
    {
        RegisterClientHandlerIfNeeded();

        if (!initialized)
        {
            TryInitialize();
        }

        if (CanHostAttack() && Input.GetMouseButtonDown(0))
        {
            PlayAttack(TestStage1HandState.ElementPower, true);
        }
    }

    private void OnDestroy()
    {
        UnregisterClientHandler();
    }

    private void TryInitialize()
    {
        magicRoot = FindObjectInActiveScene(MagicRootName)?.transform;
        runtimeEffectSpawnRoot = effectSpawnRoot != null
            ? effectSpawnRoot
            : ResolveDefaultEffectSpawnRoot();

        CachePrefabEffects();

        if (!useMagicRootChildrenFallback || magicRoot == null)
        {
            initialized = true;
            return;
        }

        for (var i = 0; i < magicRoot.childCount; i++)
        {
            var child = magicRoot.GetChild(i);
            var element = ElementFromEffectName(child.name);
            if (effectsByElement.ContainsKey(element))
            {
                continue;
            }

            effectsByElement.Add(element, child);
            child.gameObject.SetActive(false);
        }

        initialized = true;
    }

    private bool CanHostAttack()
    {
        if (NetworkManager.Singleton != null && !NetworkManager.Singleton.IsHost)
        {
            return false;
        }

        return TestStage1HandState.RightHandTool == TestStage1RightHandTool.Staff
            && !IsPointerOverUi();
    }

    private void PlayAttack(TestStage1ElementPower element, bool broadcast)
    {
        if (!initialized)
        {
            TryInitialize();
        }

        if (!effectsByElement.TryGetValue(element, out var effect) || effect == null)
        {
            Debug.LogWarning($"TestStage1 magic effect is missing for {element} under {MagicRootName}.");
            return;
        }

        PlayEffect(CreateRuntimeEffect(effect));
        if (broadcast)
        {
            PlayHostAttackAudio(element);
            ApplyAreaDamage(element);
            BroadcastAttack(element);
        }
    }

    private void PlayHostAttackAudio(TestStage1ElementPower element)
    {
        if (NetworkManager.Singleton != null && !NetworkManager.Singleton.IsHost)
        {
            return;
        }

        var clip = GetAttackAudioClip(element);
        if (clip == null)
        {
            return;
        }

        EnsureMagicAudioSource();
        magicAudioSource.PlayOneShot(clip, magicAudioVolume);
    }

    private AudioClip GetAttackAudioClip(TestStage1ElementPower element)
    {
        return element switch
        {
            TestStage1ElementPower.Fire => fireMagicAudioClip,
            TestStage1ElementPower.Land => landMagicAudioClip,
            TestStage1ElementPower.Wind => windMagicAudioClip,
            TestStage1ElementPower.Water => waterMagicAudioClip,
            _ => nonMagicAudioClip
        };
    }

    private void EnsureMagicAudioSource()
    {
        if (magicAudioSource != null)
        {
            return;
        }

        magicAudioSource = gameObject.AddComponent<AudioSource>();
        magicAudioSource.playOnAwake = false;
        magicAudioSource.loop = false;
        magicAudioSource.spatialBlend = 0f;
    }

    private void CachePrefabEffects()
    {
        effectsByElement.Clear();
        AddPrefabEffect(TestStage1ElementPower.None, nonMagicEffectPrefab);
        AddPrefabEffect(TestStage1ElementPower.Fire, fireEffectPrefab);
        AddPrefabEffect(TestStage1ElementPower.Land, landEffectPrefab);
        AddPrefabEffect(TestStage1ElementPower.Wind, windEffectPrefab);
        AddPrefabEffect(TestStage1ElementPower.Water, waterEffectPrefab);
    }

    private void AddPrefabEffect(TestStage1ElementPower element, Transform prefab)
    {
        if (prefab != null)
        {
            effectsByElement[element] = prefab;
        }
    }

    private Transform CreateRuntimeEffect(Transform effect)
    {
        var parent = runtimeEffectSpawnRoot != null ? runtimeEffectSpawnRoot : transform;
        var instance = Instantiate(effect, parent, false);
        instance.name = effect.name;
        instance.localPosition = Vector3.zero;
        instance.localRotation = Quaternion.identity;
        return instance;
    }

    private void PlayEffect(Transform effect)
    {
        if (runningEffects.TryGetValue(effect, out var runningEffect) && runningEffect != null)
        {
            StopCoroutine(runningEffect);
        }

        runningEffects[effect] = StartCoroutine(PlayEffectRoutine(effect));
    }

    private IEnumerator PlayEffectRoutine(Transform effect)
    {
        effect.gameObject.SetActive(true);
        RestartParticleSystems(effect);

        var hasLoop = HasLoopingParticleSystem(effect);
        if (hasLoop)
        {
            yield return new WaitForSeconds(LoopPlaySeconds);
            StopParticleSystems(effect, ParticleSystemStopBehavior.StopEmitting);
            yield return new WaitForSeconds(GetMaxStartLifetime(effect));
        }
        else
        {
            yield return new WaitForSeconds(GetOneShotDuration(effect));
            StopParticleSystems(effect, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        if (effect != null)
        {
            Destroy(effect.gameObject);
            runningEffects.Remove(effect);
        }
    }

    private static void RestartParticleSystems(Transform effect)
    {
        foreach (var particleSystem in effect.GetComponentsInChildren<ParticleSystem>(true))
        {
            particleSystem.Clear(true);
            particleSystem.Play(true);
        }

        foreach (var behaviour in effect.GetComponentsInChildren<Behaviour>(true))
        {
            if (string.Equals(behaviour.GetType().Name, "VisualEffect", StringComparison.Ordinal))
            {
                behaviour.enabled = false;
                behaviour.enabled = true;
            }
        }
    }

    private static void StopParticleSystems(Transform effect, ParticleSystemStopBehavior stopBehavior)
    {
        foreach (var particleSystem in effect.GetComponentsInChildren<ParticleSystem>(true))
        {
            particleSystem.Stop(true, stopBehavior);
        }
    }

    private static bool HasLoopingParticleSystem(Transform effect)
    {
        foreach (var particleSystem in effect.GetComponentsInChildren<ParticleSystem>(true))
        {
            if (particleSystem.main.loop)
            {
                return true;
            }
        }

        return false;
    }

    private static float GetOneShotDuration(Transform effect)
    {
        var duration = 0.1f;
        foreach (var particleSystem in effect.GetComponentsInChildren<ParticleSystem>(true))
        {
            var main = particleSystem.main;
            duration = Mathf.Max(duration, main.duration + GetMaxLifetime(main.startLifetime));
        }

        return Mathf.Min(duration, DeactivateSafetyDelaySeconds);
    }

    private static float GetMaxStartLifetime(Transform effect)
    {
        var lifetime = 0.25f;
        foreach (var particleSystem in effect.GetComponentsInChildren<ParticleSystem>(true))
        {
            lifetime = Mathf.Max(lifetime, GetMaxLifetime(particleSystem.main.startLifetime));
        }

        return Mathf.Min(lifetime, DeactivateSafetyDelaySeconds);
    }

    private void ApplyAreaDamage(TestStage1ElementPower attackElement)
    {
        var center = ResolveAttackCenter();
        DrawDebugAttackRange(center);

        var hits = Physics.OverlapSphere(center, attackRadius, damageMask, QueryTriggerInteraction.Ignore);
        var damagedSlimes = new HashSet<TestStage1SlimeSmallController>();

        foreach (var hit in hits)
        {
            var slime = FindActiveSlimeController(hit);
            if (slime == null || !slime.isActiveAndEnabled || !damagedSlimes.Add(slime))
            {
                continue;
            }

            slime.TakeDamage(GetDamage(attackElement, slime.ElementPower));
        }
    }

    private static TestStage1SlimeSmallController FindActiveSlimeController(Collider hit)
    {
        foreach (var slime in hit.GetComponentsInParent<TestStage1SlimeSmallController>(true))
        {
            if (slime != null && slime.isActiveAndEnabled)
            {
                return slime;
            }
        }

        return null;
    }

    private void DrawDebugAttackRange(Vector3 center)
    {
        if (!showDebugAttackRange)
        {
            return;
        }

        var rangeObject = new GameObject("DebugAttackRange");
        rangeObject.transform.position = center;

        var lineRenderer = rangeObject.AddComponent<LineRenderer>();
        lineRenderer.useWorldSpace = false;
        lineRenderer.loop = true;
        lineRenderer.positionCount = 96;
        lineRenderer.startWidth = 0.06f;
        lineRenderer.endWidth = 0.06f;
        lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
        lineRenderer.startColor = debugAttackRangeColor;
        lineRenderer.endColor = debugAttackRangeColor;

        for (var i = 0; i < lineRenderer.positionCount; i++)
        {
            var angle = (float)i / lineRenderer.positionCount * Mathf.PI * 2f;
            lineRenderer.SetPosition(i, new Vector3(
                Mathf.Cos(angle) * attackRadius,
                0.05f,
                Mathf.Sin(angle) * attackRadius
            ));
        }

        Destroy(rangeObject, debugAttackRangeSeconds);
    }

    private Vector3 ResolveAttackCenter()
    {
        if (runtimeEffectSpawnRoot != null)
        {
            return runtimeEffectSpawnRoot.position;
        }

        var player = GameObject.FindGameObjectWithTag(PlayerTag);
        return player != null ? player.transform.position : transform.position;
    }

    private static float GetDamage(TestStage1ElementPower attackElement, TestStage1ElementPower slimeElement)
    {
        if (attackElement == slimeElement)
        {
            return SameElementDamage;
        }

        return IsElementAdvantage(attackElement, slimeElement) ? AdvantageDamage : NeutralDamage;
    }

    private static bool IsElementAdvantage(TestStage1ElementPower attackElement, TestStage1ElementPower slimeElement)
    {
        return (attackElement == TestStage1ElementPower.Fire && slimeElement == TestStage1ElementPower.Land)
            || (attackElement == TestStage1ElementPower.Land && slimeElement == TestStage1ElementPower.Wind)
            || (attackElement == TestStage1ElementPower.Wind && slimeElement == TestStage1ElementPower.Water)
            || (attackElement == TestStage1ElementPower.Water && slimeElement == TestStage1ElementPower.Fire);
    }

    private static float GetMaxLifetime(ParticleSystem.MinMaxCurve curve)
    {
        return curve.mode switch
        {
            ParticleSystemCurveMode.Constant => curve.constant,
            ParticleSystemCurveMode.TwoConstants => curve.constantMax,
            ParticleSystemCurveMode.Curve => curve.curve.length > 0 ? curve.curve.keys[^1].time : curve.constant,
            ParticleSystemCurveMode.TwoCurves => curve.curveMax.length > 0 ? curve.curveMax.keys[^1].time : curve.constantMax,
            _ => curve.constant
        };
    }

    private void RegisterClientHandlerIfNeeded()
    {
        if (clientHandlerRegistered
            || NetworkManager.Singleton == null
            || !NetworkManager.Singleton.IsClient
            || NetworkManager.Singleton.IsHost)
        {
            return;
        }

        NetworkManager.Singleton.CustomMessagingManager.RegisterNamedMessageHandler(
            AttackMessageName,
            OnAttackMessageReceived
        );
        clientHandlerRegistered = true;
    }

    private void UnregisterClientHandler()
    {
        if (!clientHandlerRegistered || NetworkManager.Singleton == null)
        {
            return;
        }

        NetworkManager.Singleton.CustomMessagingManager.UnregisterNamedMessageHandler(AttackMessageName);
        clientHandlerRegistered = false;
    }

    private static void BroadcastAttack(TestStage1ElementPower element)
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer)
        {
            return;
        }

        foreach (var clientId in NetworkManager.Singleton.ConnectedClientsIds)
        {
            if (clientId == NetworkManager.ServerClientId)
            {
                continue;
            }

            using var writer = new FastBufferWriter(1, Allocator.Temp);
            writer.WriteValueSafe((byte)element);
            NetworkManager.Singleton.CustomMessagingManager.SendNamedMessage(
                AttackMessageName,
                clientId,
                writer
            );
        }
    }

    private void OnAttackMessageReceived(ulong senderClientId, FastBufferReader reader)
    {
        reader.ReadValueSafe(out byte elementValue);
        PlayAttack(IsKnownElement((TestStage1ElementPower)elementValue)
            ? (TestStage1ElementPower)elementValue
            : TestStage1ElementPower.None, false);
    }

    private static bool IsKnownElement(TestStage1ElementPower element)
    {
        return element == TestStage1ElementPower.None
            || element == TestStage1ElementPower.Fire
            || element == TestStage1ElementPower.Land
            || element == TestStage1ElementPower.Wind
            || element == TestStage1ElementPower.Water;
    }

    private static TestStage1ElementPower ElementFromEffectName(string effectName)
    {
        if (effectName.IndexOf("non", StringComparison.OrdinalIgnoreCase) >= 0
            || effectName.IndexOf("none", StringComparison.OrdinalIgnoreCase) >= 0
            || effectName.IndexOf("neutral", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return TestStage1ElementPower.None;
        }

        if (effectName.IndexOf("fire", StringComparison.OrdinalIgnoreCase) >= 0
            || effectName.IndexOf("flame", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return TestStage1ElementPower.Fire;
        }

        if (effectName.IndexOf("land", StringComparison.OrdinalIgnoreCase) >= 0
            || effectName.IndexOf("earth", StringComparison.OrdinalIgnoreCase) >= 0
            || effectName.IndexOf("ground", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return TestStage1ElementPower.Land;
        }

        if (effectName.IndexOf("wind", StringComparison.OrdinalIgnoreCase) >= 0
            || effectName.IndexOf("air", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return TestStage1ElementPower.Wind;
        }

        if (effectName.IndexOf("water", StringComparison.OrdinalIgnoreCase) >= 0
            || effectName.IndexOf("ice", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return TestStage1ElementPower.Water;
        }

        return TestStage1ElementPower.None;
    }

    private static bool IsPointerOverUi()
    {
        return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
    }

    private Transform ResolveDefaultEffectSpawnRoot()
    {
        var player = GameObject.FindGameObjectWithTag(PlayerTag);
        if (player != null)
        {
            return player.transform;
        }

        return magicRoot != null ? magicRoot : transform;
    }

    private static GameObject FindObjectInActiveScene(string objectName)
    {
        var scene = SceneManager.GetActiveScene();
        foreach (var rootObject in scene.GetRootGameObjects())
        {
            var match = FindChildByName(rootObject.transform, objectName);
            if (match != null)
            {
                return match.gameObject;
            }
        }

        return null;
    }

    private static Transform FindChildByName(Transform parent, string objectName)
    {
        if (string.Equals(parent.name, objectName, StringComparison.Ordinal))
        {
            return parent;
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
}
