using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.VFX;
using UnityEngine.XR;

public class WeldingTorchController : MonoBehaviour
{
    private const string DefaultWeldLoopResourcePath = "Audio/MigWeldingLoop";
    private const float DefaultTorchTipForwardOffset = 0.1f;
    private const float WeldHapticAmplitude = 1f;
    private const float WeldHapticPulseDuration = 0.12f;
    private const float WeldHapticPulseInterval = 0.08f;

    [Header("Input")]
    public InputActionReference weldTrigger;

    [Header("Optional - exact weld origin")]
    public Transform torchTip;

    [Header("Effects")]
    public ParticleSystem sparkParticles;
    public Light arcLight;
    public AudioSource weldSound;

    [Header("Free-Fire Visuals")]
    [Tooltip("Shows a visual-only arc effect when the trigger is held away from a weldable plate. "
           + "This never creates bead geometry or report samples.")]
    [SerializeField] private bool enableFreeFireVisuals = true;
    [SerializeField] private float freeFireForwardOffset = 0.015f;
    [SerializeField] private float freeFireArcSize = 0.0035f;
    [SerializeField] private float freeFireCoreSize = 0.0012f;
    [SerializeField] private float freeFireArcIntensity = 0.35f;
    [SerializeField] private float freeFireCoreIntensity = 1.2f;
    [SerializeField] private Color freeFireArcColor = new Color(0f, 0.25f, 0.65f, 1f);

    [Header("Dynamic Weld Audio")]
    [SerializeField] private bool enableDynamicWeldAudio = true;
    [SerializeField] private float audioMinDistance = 0.35f;
    [SerializeField] private float audioMaxDistance = 2.5f;
    [SerializeField] private float baseWeldVolume = 1f;
    [SerializeField] private float unstableVolumeBoost = 0.35f;
    [SerializeField] private float ctwdPitchRange = 0.35f;
    [SerializeField] private float anglePitchRange = 0.25f;
    [SerializeField] private float freeFirePitch = 1.18f;
    [SerializeField] private float audioResponseSharpness = 10f;

    [Header("Weld Logic")]
    [Tooltip("The old segment-based bead system. Leave assigned but it is automatically "
           + "disabled at runtime when WeldBead is present — WeldBead replaces it in VR.")]
    public BeadGenerator beadGenerator;

    [Tooltip("The high-quality VFX tube-mesh bead system. Drag the TJointPrefab (or "
           + "whatever GameObject has the WeldBead component) here.")]
    public WeldBead weldBead;

    private bool isWelding;
    private bool hasActiveWeldContact;
    private bool hasActiveWeldAudio;
    private float nextHapticPulseTime;
    private GameObject freeFireVfxRoot;
    private VisualEffect freeFireParticleEffect;
    private bool freeFireVisualsActive;
    private MainScenePauseMenu pauseMenu;
    private TutorialDirector tutorialDirector;
    private WeldParameterMonitor parameterMonitor;

    void Awake()
    {
        ResolveTorchTip();
        EnsureWeldSoundSetup();
        EnsureFreeFireVisuals();

        // ── Disable BeadGenerator when WeldBead is present ───────────
        // BeadGenerator spawns the plain black sphere segments — these are
        // replaced entirely by WeldBead's tube-mesh system in VR.
        // Mouse mode: WeldBead handles itself via Update(), BeadGenerator
        //             is not needed there either with WeldBead present.
        if (weldBead != null && beadGenerator != null)
            beadGenerator.enabled = false;
    }

    void OnEnable()
    {
        if (weldTrigger != null) weldTrigger.action.Enable();
    }

    void OnDisable()
    {
        if (weldTrigger != null) weldTrigger.action.Disable();
        StopWeldHaptics();
        SetFreeFireVisualsActive(false);
    }

    void Update()
    {
        if (weldTrigger == null) return;

        if (IsInteractionUiOpen())
        {
            if (isWelding)
                StopWelding();

            return;
        }

        float triggerValue = weldTrigger.action.ReadValue<float>();

        if (triggerValue > 0.8f && !isWelding) StartWelding();
        else if (triggerValue < 0.2f && isWelding) StopWelding();
    }

    void StartWelding()
    {
        isWelding = true;
        UpdateWeldEffects(false);

        // Only call BeadGenerator if WeldBead is not present (fallback)
        if (weldBead == null && beadGenerator != null)
            beadGenerator.BeginBead();

        weldBead?.BeginVRBead();
    }

    void StopWelding()
    {
        isWelding = false;
        SetFreeFireVisualsActive(false);
        UpdateWeldEffects(false);

        if (weldBead == null && beadGenerator != null)
            beadGenerator.EndBead();

        weldBead?.EndVRBead();
    }

    void LateUpdate()
    {
        if (weldBead == null && beadGenerator == null) return;

        Transform t = GetActiveTorchTip();

        if (!isWelding)
        {
            weldBead?.PreviewVRMeasurements(t.position, t.forward);
            SetFreeFireVisualsActive(false);
            return;
        }

        bool hasContact = false;

        if (weldBead != null)
        {
            // WeldBead: pass torch position AND forward direction.
            // It raycasts along forward just like mouse mode uses the camera ray.
            hasContact = weldBead.UpdateVRBead(t.position, t.forward);
        }
        else if (beadGenerator != null)
        {
            // Fallback: old segment system if no WeldBead assigned
            hasContact = beadGenerator.UpdateBeadAtPosition(t.position, t.forward);
        }

        UpdateFreeFireVisuals(!hasContact, t);
        UpdateWeldEffects(hasContact);
        UpdateWeldHaptics();
    }

    private Transform GetActiveTorchTip()
    {
        if (torchTip == null)
            ResolveTorchTip();

        return torchTip != null ? torchTip : transform;
    }

    private void ResolveTorchTip()
    {
        if (torchTip != null)
            return;

        torchTip = FindChildByName(transform, "TorchTip");
        if (torchTip == null)
            torchTip = FindChildByName(transform, "Tip Tracker");

        if (torchTip != null)
            return;

        Transform gunRoot = FindChildByName(transform, "full_");
        if (gunRoot == null)
            gunRoot = FindChildByName(transform, "full");

        if (gunRoot != null)
        {
            Transform tipTracker = FindChildByName(gunRoot, "Tip Tracker");
            if (tipTracker != null)
            {
                torchTip = tipTracker;
                return;
            }

            torchTip = CreateTorchTipAtGunNozzle(gunRoot);
            return;
        }

        torchTip = CreateFallbackTorchTip(transform, DefaultTorchTipForwardOffset);
    }

    private void EnsureWeldSoundSetup()
    {
        if (weldSound == null)
            weldSound = GetComponent<AudioSource>();

        if (weldSound == null)
            weldSound = gameObject.AddComponent<AudioSource>();

        if (weldSound == null)
            return;

        if (weldSound.clip == null)
            weldSound.clip = Resources.Load<AudioClip>(DefaultWeldLoopResourcePath);

        weldSound.loop = true;
        weldSound.playOnAwake = false;
        weldSound.spatialBlend = 0.95f;
        weldSound.rolloffMode = AudioRolloffMode.Logarithmic;
        weldSound.minDistance = Mathf.Max(0.01f, audioMinDistance);
        weldSound.maxDistance = Mathf.Max(weldSound.minDistance + 0.01f, audioMaxDistance);
        weldSound.volume = baseWeldVolume;
        weldSound.pitch = 1f;
    }

    private void EnsureFreeFireVisuals()
    {
        if (!enableFreeFireVisuals || weldBead == null || weldBead.ContactParticleEffect == null)
            return;

        if (freeFireParticleEffect != null)
            return;

        Transform parent = GetActiveTorchTip();
        freeFireVfxRoot = Instantiate(weldBead.ContactParticleEffect.gameObject, parent);
        freeFireVfxRoot.name = "Free Fire Particle Effect";
        freeFireVfxRoot.transform.localPosition = Vector3.zero;
        freeFireVfxRoot.transform.localRotation = Quaternion.identity;
        freeFireParticleEffect = freeFireVfxRoot.GetComponent<VisualEffect>();
        ConfigureFreeFireVisuals();
        freeFireVfxRoot.SetActive(false);
        freeFireVisualsActive = false;
    }

    private void UpdateFreeFireVisuals(bool shouldShow, Transform activeTorchTip)
    {
        if (!enableFreeFireVisuals)
            return;

        if (freeFireParticleEffect == null)
            EnsureFreeFireVisuals();

        if (freeFireParticleEffect == null || activeTorchTip == null)
            return;

        SetFreeFireVisualsActive(shouldShow);
        if (!shouldShow)
            return;

        Vector3 forward = activeTorchTip.forward.sqrMagnitude > 1e-8f
            ? activeTorchTip.forward.normalized
            : transform.forward;
        Vector3 origin = activeTorchTip.position + forward * freeFireForwardOffset;

        freeFireParticleEffect.SetVector3("OriginPosition", origin);
        freeFireParticleEffect.SetVector3("NormalDirection", forward);
        freeFireParticleEffect.SetFloat("ArcSize", freeFireArcSize);
        freeFireParticleEffect.SetFloat("Coresize", freeFireCoreSize);
        freeFireParticleEffect.SetFloat("Arcintensity", freeFireArcIntensity);
        freeFireParticleEffect.SetFloat("Coreintensity", freeFireCoreIntensity);
        freeFireParticleEffect.SetVector4("ArcColor", freeFireArcColor);
        freeFireParticleEffect.SendEvent("Trigger");
    }

    private void SetFreeFireVisualsActive(bool shouldBeActive)
    {
        if (freeFireVisualsActive == shouldBeActive)
            return;

        freeFireVisualsActive = shouldBeActive;

        if (freeFireVfxRoot != null)
            freeFireVfxRoot.SetActive(shouldBeActive);
    }

    private void ConfigureFreeFireVisuals()
    {
        if (freeFireVfxRoot == null)
            return;

        WeldArcFlicker[] flickers = freeFireVfxRoot.GetComponentsInChildren<WeldArcFlicker>(true);
        for (int i = 0; i < flickers.Length; i++)
            flickers[i].enabled = false;

        Light[] lights = freeFireVfxRoot.GetComponentsInChildren<Light>(true);
        for (int i = 0; i < lights.Length; i++)
            lights[i].enabled = false;
    }

    private bool IsInteractionUiOpen()
    {
        if (pauseMenu == null)
            pauseMenu = FindFirstObjectByType<MainScenePauseMenu>();

        if (tutorialDirector == null)
            tutorialDirector = FindFirstObjectByType<TutorialDirector>(FindObjectsInactive.Include);

        return (pauseMenu != null && pauseMenu.IsMenuOpen)
            || (tutorialDirector != null && tutorialDirector.IsTutorialVisible);
    }

    private Transform FindChildByName(Transform root, string targetName)
    {
        if (root == null)
            return null;

        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            if (child != null && child.name == targetName)
                return child;
        }

        return null;
    }

    private Transform CreateTorchTipAtGunNozzle(Transform gunRoot)
    {
        Transform existingTip = FindChildByName(gunRoot, "TorchTip");
        if (existingTip != null)
            return existingTip;

        GameObject tipObject = new GameObject("TorchTip");
        tipObject.transform.SetParent(gunRoot, false);

        Renderer[] renderers = gunRoot.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
        {
            tipObject.transform.localPosition = new Vector3(0f, 0f, DefaultTorchTipForwardOffset);
            tipObject.transform.localRotation = Quaternion.identity;
            return tipObject.transform;
        }

        Transform referenceTransform = gunRoot.parent != null ? gunRoot.parent : transform;
        Vector3 referencePosition = referenceTransform.position;
        Vector3 bestWorldPoint = gunRoot.position;
        float bestDistance = float.NegativeInfinity;

        for (int i = 0; i < renderers.Length; i++)
        {
            Bounds bounds = renderers[i].bounds;
            Vector3[] corners =
            {
                new Vector3(bounds.min.x, bounds.min.y, bounds.min.z),
                new Vector3(bounds.max.x, bounds.min.y, bounds.min.z),
                new Vector3(bounds.min.x, bounds.max.y, bounds.min.z),
                new Vector3(bounds.max.x, bounds.max.y, bounds.min.z),
                new Vector3(bounds.min.x, bounds.min.y, bounds.max.z),
                new Vector3(bounds.max.x, bounds.min.y, bounds.max.z),
                new Vector3(bounds.min.x, bounds.max.y, bounds.max.z),
                new Vector3(bounds.max.x, bounds.max.y, bounds.max.z)
            };

            for (int j = 0; j < corners.Length; j++)
            {
                float distance = (corners[j] - referencePosition).sqrMagnitude;
                if (distance > bestDistance)
                {
                    bestDistance = distance;
                    bestWorldPoint = corners[j];
                }
            }
        }

        tipObject.transform.position = bestWorldPoint;
        tipObject.transform.localRotation = Quaternion.identity;
        return tipObject.transform;
    }

    private Transform CreateFallbackTorchTip(Transform parent, float forwardOffset)
    {
        Transform existingTip = FindChildByName(parent, "TorchTip");
        if (existingTip != null)
            return existingTip;

        GameObject tipObject = new GameObject("TorchTip");
        tipObject.transform.SetParent(parent, false);
        tipObject.transform.localPosition = new Vector3(0f, 0f, forwardOffset);
        tipObject.transform.localRotation = Quaternion.identity;
        return tipObject.transform;
    }

    private void UpdateWeldEffects(bool hasContact)
    {
        if (hasActiveWeldContact != hasContact)
        {
            hasActiveWeldContact = hasContact;
            nextHapticPulseTime = 0f;

            if (sparkParticles != null)
            {
                if (hasContact)
                    sparkParticles.Play();
                else
                    sparkParticles.Stop();
            }

            if (arcLight != null)
                arcLight.enabled = hasContact;

            if (!hasContact && !isWelding)
                StopWeldHaptics();
        }

        bool shouldPlayWeldAudio = hasContact || freeFireVisualsActive;
        if (hasActiveWeldAudio != shouldPlayWeldAudio && weldSound != null)
        {
            hasActiveWeldAudio = shouldPlayWeldAudio;
            weldSound.Stop();
            weldSound.time = 0f;

            if (shouldPlayWeldAudio)
                weldSound.Play();
        }

        UpdateDynamicWeldAudio();
    }

    private void UpdateDynamicWeldAudio()
    {
        if (!enableDynamicWeldAudio || weldSound == null || !hasActiveWeldAudio)
            return;

        float targetVolume = baseWeldVolume;
        float targetPitch = freeFireVisualsActive && !hasActiveWeldContact
            ? freeFirePitch
            : 1f;

        if (hasActiveWeldContact)
        {
            if (parameterMonitor == null)
                parameterMonitor = FindFirstObjectByType<WeldParameterMonitor>();

            if (parameterMonitor != null && (parameterMonitor.HasLiveMeasurements || parameterMonitor.HasPreviewMeasurements))
            {
                WeldQualityProfile profile = WeldQualityTable.GetProfile(WeldJointSelectionState.GetSelectedOrDefault());

                float ctwdDeviation = GetNormalizedDeviation(
                    parameterMonitor.StickoutDistanceMm,
                    profile.NormalCtwdMin,
                    profile.NormalCtwdMax);
                float workAngleDeviation = GetNormalizedDeviation(
                    parameterMonitor.WorkAngleDeg,
                    profile.NormalWorkAngleMin,
                    profile.NormalWorkAngleMax);
                float travelAngleDeviation = GetNormalizedDeviation(
                    parameterMonitor.TravelAngleDeg,
                    profile.NormalTravelAngleMin,
                    profile.NormalTravelAngleMax);

                float angleDeviation = Mathf.Max(workAngleDeviation, travelAngleDeviation);
                targetPitch += Mathf.Clamp(ctwdDeviation, -1f, 1f) * ctwdPitchRange;
                targetPitch += Mathf.Clamp(angleDeviation, -1f, 1f) * anglePitchRange;
                targetVolume += Mathf.Clamp01(Mathf.Max(Mathf.Abs(ctwdDeviation), Mathf.Abs(angleDeviation))) * unstableVolumeBoost;
            }
        }

        float t = 1f - Mathf.Exp(-Mathf.Max(0.01f, audioResponseSharpness) * Time.unscaledDeltaTime);
        weldSound.volume = Mathf.Lerp(weldSound.volume, targetVolume, t);
        weldSound.pitch = Mathf.Lerp(weldSound.pitch, Mathf.Clamp(targetPitch, 0.75f, 1.35f), t);
    }

    private static float GetNormalizedDeviation(float value, float min, float max)
    {
        float center = (min + max) * 0.5f;
        float halfRange = Mathf.Max(0.001f, (max - min) * 0.5f);
        return (value - center) / halfRange;
    }

    private void UpdateWeldHaptics()
    {
        if (!isWelding)
            return;

        if (Time.unscaledTime < nextHapticPulseTime)
            return;

        UnityEngine.XR.InputDevice hapticDevice = GetRightHandHapticDevice();
        if (!hapticDevice.isValid)
            return;

        hapticDevice.SendHapticImpulse(0u, WeldHapticAmplitude, WeldHapticPulseDuration);
        nextHapticPulseTime = Time.unscaledTime + WeldHapticPulseInterval;
    }

    private void StopWeldHaptics()
    {
        UnityEngine.XR.InputDevice hapticDevice = GetRightHandHapticDevice();
        if (hapticDevice.isValid)
            hapticDevice.StopHaptics();
    }

    private UnityEngine.XR.InputDevice GetRightHandHapticDevice()
    {
        UnityEngine.XR.InputDevice device = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
        if (device.isValid)
            return device;

        List<UnityEngine.XR.InputDevice> rightHandDevices = new List<UnityEngine.XR.InputDevice>();
        InputDevices.GetDevicesWithCharacteristics(
            InputDeviceCharacteristics.Right | InputDeviceCharacteristics.Controller | InputDeviceCharacteristics.HeldInHand,
            rightHandDevices);

        for (int i = 0; i < rightHandDevices.Count; i++)
        {
            if (rightHandDevices[i].isValid)
                return rightHandDevices[i];
        }

        return default;
    }
}
