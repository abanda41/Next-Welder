using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class WeldingHudPanel : MonoBehaviour
{
    public enum ValueRangeState
    {
        OutOfRange,
        NearRange,
        InRange
    }

    private const string TravelAngleObjectName = "TravelAngleValue";
    private const string WorkAngleObjectName = "WorkAngleValue";
    private const string TravelSpeedObjectName = "TravelSpeedValue";
    private const string StickoutDistanceObjectName = "StickoutDistanceValue";
    private const string PathDistanceObjectName = "PathDistanceValue";
    private const string PathStatusObjectName = "PathStatusValue";

    [Header("XR HUD Overlay")]
    [SerializeField] private bool configureAsXrOverlay = true;
    [SerializeField] private bool useStabilizedWorldAnchor = true;
    [SerializeField] private bool hardLockToHead = true;
    [SerializeField] private Camera xrRenderCamera;
    [SerializeField] private float planeDistance = 1.25f;
    [SerializeField] private int sortingOrder = 500;
    [SerializeField] private Vector2 referenceResolution = new Vector2(1920f, 1080f);
    [SerializeField, Range(0.25f, 1f)] private float verticalViewCoverage = 1f;
    [SerializeField] private float positionSharpness = 80f;
    [SerializeField] private float rotationSharpness = 65f;
    [SerializeField] private float snapDistance = 0.25f;
    [SerializeField] private float snapAngle = 18f;
    [SerializeField] private Material noDepthUiMaterial;

    [Header("Recording Layout")]
    [SerializeField] private bool useRecordingLayout;
    [SerializeField] private RectTransform panelRoot;
    [SerializeField] private Vector2 normalPanelAnchor = new Vector2(0f, 1f);
    [SerializeField] private Vector2 normalPanelPivot = new Vector2(0f, 1f);
    [SerializeField] private Vector2 normalPanelPosition = new Vector2(287f, -63f);
    [SerializeField] private Vector2 recordingPanelAnchor = new Vector2(0.5f, 1f);
    [SerializeField] private Vector2 recordingPanelPivot = new Vector2(0.5f, 1f);
    [SerializeField] private Vector2 recordingPanelPosition = new Vector2(0f, -120f);

    [Header("Value Text References")]
    [SerializeField] private TextMeshProUGUI travelAngleValueText;
    [SerializeField] private TextMeshProUGUI workAngleValueText;
    [SerializeField] private TextMeshProUGUI travelSpeedValueText;
    [SerializeField] private TextMeshProUGUI stickoutDistanceValueText;
    [SerializeField] private TextMeshProUGUI pathDistanceValueText;
    [SerializeField] private TextMeshProUGUI pathStatusValueText;

    [Header("Parameter Colors")]
    [SerializeField] private Color inRangeValueColor = new Color(0.34f, 1f, 0.45f, 1f);
    [SerializeField] private Color nearRangeValueColor = new Color(1f, 0.67f, 0.32f, 1f);
    [SerializeField] private Color outOfRangeValueColor = new Color(1f, 0.28f, 0.24f, 1f);
    [SerializeField] private Color idleValueColor = Color.white;

    private bool noDepthMaterialsApplied;
    private bool hasStablePose;
    private Vector3 stableCameraPosition;
    private Quaternion stableCameraRotation;

    public TextMeshProUGUI TravelAngleValueText => travelAngleValueText;
    public TextMeshProUGUI WorkAngleValueText => workAngleValueText;
    public TextMeshProUGUI TravelSpeedValueText => travelSpeedValueText;
    public TextMeshProUGUI StickoutDistanceValueText => stickoutDistanceValueText;
    public TextMeshProUGUI PathDistanceValueText => pathDistanceValueText;
    public TextMeshProUGUI PathStatusValueText => pathStatusValueText;

    private void Awake()
    {
        AutoAssignPanelRoot();
        ConfigureXrOverlay();
    }

    private void OnEnable()
    {
        Application.onBeforeRender += HandleBeforeRender;
        ConfigureXrOverlay();
    }

    private void OnDisable()
    {
        Application.onBeforeRender -= HandleBeforeRender;
    }

    private void LateUpdate()
    {
        UpdateHudPose(Time.unscaledDeltaTime);
    }

    private void Reset()
    {
        AutoAssignMissingReferences();
        AutoAssignPanelRoot();
    }

    private void OnValidate()
    {
        AutoAssignMissingReferences();
        AutoAssignPanelRoot();
        ApplyPanelLayout();
    }

    [ContextMenu("Auto Assign Value References")]
    public void AutoAssignMissingReferences()
    {
        if (travelAngleValueText == null)
            travelAngleValueText = FindValueText(TravelAngleObjectName);

        if (workAngleValueText == null)
            workAngleValueText = FindValueText(WorkAngleObjectName);

        if (travelSpeedValueText == null)
            travelSpeedValueText = FindValueText(TravelSpeedObjectName);

        if (stickoutDistanceValueText == null)
            stickoutDistanceValueText = FindValueText(StickoutDistanceObjectName);

        if (pathDistanceValueText == null)
            pathDistanceValueText = FindValueText(PathDistanceObjectName);

        if (pathStatusValueText == null)
            pathStatusValueText = FindValueText(PathStatusObjectName);
    }

    public void SetTravelAngle(float value)
    {
        SetText(travelAngleValueText, value);
    }

    public void SetWorkAngle(float value)
    {
        SetText(workAngleValueText, value);
    }

    public void SetTravelSpeed(float value)
    {
        SetText(travelSpeedValueText, value);
    }

    public void SetStickoutDistance(float value)
    {
        SetText(stickoutDistanceValueText, value);
    }

    public void SetValues(float travelAngle, float workAngle, float travelSpeed, float stickoutDistance)
    {
        SetTravelAngle(travelAngle);
        SetWorkAngle(workAngle);
        SetTravelSpeed(travelSpeed);
        SetStickoutDistance(stickoutDistance);
    }

    public void SetValueRangeColors(
        bool hasLiveMeasurements,
        ValueRangeState travelAngleState,
        ValueRangeState workAngleState,
        ValueRangeState travelSpeedState,
        ValueRangeState stickoutDistanceState)
    {
        SetValueColor(travelAngleValueText, hasLiveMeasurements, travelAngleState);
        SetValueColor(workAngleValueText, hasLiveMeasurements, workAngleState);
        SetValueColor(travelSpeedValueText, hasLiveMeasurements, travelSpeedState);
        SetValueColor(stickoutDistanceValueText, hasLiveMeasurements, stickoutDistanceState);
    }

    public void SetPreviewValueRangeColors(
        bool hasTravelAnglePreview,
        ValueRangeState travelAngleState,
        ValueRangeState workAngleState,
        ValueRangeState stickoutDistanceState)
    {
        SetValueColor(travelAngleValueText, hasTravelAnglePreview, travelAngleState);
        SetValueColor(workAngleValueText, true, workAngleState);
        SetValueColor(travelSpeedValueText, false, ValueRangeState.OutOfRange);
        SetValueColor(stickoutDistanceValueText, true, stickoutDistanceState);
    }

    public void SetPathValues(float pathDistance, bool isInsideTolerance)
    {
        SetText(pathDistanceValueText, pathDistance);
        SetText(pathStatusValueText, isInsideTolerance ? "IN" : "OUT");
    }

    [ContextMenu("Configure XR HUD Overlay")]
    public void ConfigureXrOverlay()
    {
        if (!configureAsXrOverlay)
            return;

        Canvas canvas = GetComponent<Canvas>();
        if (canvas == null)
            return;

        if (xrRenderCamera == null)
            xrRenderCamera = FindHudCamera();

        bool useWorldAnchor = useStabilizedWorldAnchor || hardLockToHead;
        canvas.renderMode = useWorldAnchor ? RenderMode.WorldSpace : RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = xrRenderCamera;
        canvas.planeDistance = planeDistance;
        canvas.overrideSorting = true;
        canvas.sortingOrder = sortingOrder;

        CanvasScaler canvasScaler = GetComponent<CanvasScaler>();
        if (canvasScaler != null)
        {
            if (useWorldAnchor)
            {
                canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
                canvasScaler.scaleFactor = 1f;
            }
            else
            {
                canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                canvasScaler.referenceResolution = referenceResolution;
                canvasScaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                canvasScaler.matchWidthOrHeight = 0.5f;
            }
        }

        CanvasGroup canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup != null)
        {
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }

        DisableRaycastTargets();

        if (Application.isPlaying && !noDepthMaterialsApplied)
        {
            ApplyNoDepthMaterials();
            noDepthMaterialsApplied = true;
        }

        if (hardLockToHead)
            UpdateHardLockedHudPose();
        else if (useStabilizedWorldAnchor)
            UpdateStabilizedHudPose(0f, true);

        ApplyPanelLayout();
    }

    public void AssignValueReferences(
        TextMeshProUGUI travelAngle,
        TextMeshProUGUI workAngle,
        TextMeshProUGUI travelSpeed,
        TextMeshProUGUI stickoutDistance)
    {
        travelAngleValueText = travelAngle;
        workAngleValueText = workAngle;
        travelSpeedValueText = travelSpeed;
        stickoutDistanceValueText = stickoutDistance;
    }

    public void AssignPathReferences(TextMeshProUGUI pathDistance, TextMeshProUGUI pathStatus)
    {
        pathDistanceValueText = pathDistance;
        pathStatusValueText = pathStatus;
    }

    [ContextMenu("Use Normal HUD Layout")]
    public void UseNormalLayout()
    {
        useRecordingLayout = false;
        ApplyPanelLayout();
    }

    [ContextMenu("Use Recording HUD Layout")]
    public void UseRecordingLayout()
    {
        useRecordingLayout = true;
        ApplyPanelLayout();
    }

    private TextMeshProUGUI FindValueText(string objectName)
    {
        TextMeshProUGUI[] valueTexts = GetComponentsInChildren<TextMeshProUGUI>(true);
        for (int i = 0; i < valueTexts.Length; i++)
        {
            if (valueTexts[i].name == objectName)
                return valueTexts[i];
        }

        return null;
    }

    private void AutoAssignPanelRoot()
    {
        if (panelRoot != null)
            return;

        Transform existingPanel = transform.Find("Welding Parameters Panel");
        if (existingPanel != null)
            panelRoot = existingPanel as RectTransform;
    }

    private void ApplyPanelLayout()
    {
        if (panelRoot == null)
            return;

        panelRoot.anchorMin = useRecordingLayout ? recordingPanelAnchor : normalPanelAnchor;
        panelRoot.anchorMax = useRecordingLayout ? recordingPanelAnchor : normalPanelAnchor;
        panelRoot.pivot = useRecordingLayout ? recordingPanelPivot : normalPanelPivot;
        panelRoot.anchoredPosition = useRecordingLayout ? recordingPanelPosition : normalPanelPosition;
    }

    private static Camera FindHudCamera()
    {
        if (Camera.main != null)
            return Camera.main;

#if UNITY_2023_1_OR_NEWER
        return FindFirstObjectByType<Camera>();
#else
        return FindObjectOfType<Camera>();
#endif
    }

    private void DisableRaycastTargets()
    {
        Graphic[] graphics = GetComponentsInChildren<Graphic>(true);
        for (int i = 0; i < graphics.Length; i++)
            graphics[i].raycastTarget = false;
    }

    private void HandleBeforeRender()
    {
        UpdateHudPose(Time.unscaledDeltaTime);
    }

    private void UpdateHudPose(float deltaTime)
    {
        if (hardLockToHead)
            UpdateHardLockedHudPose();
        else
            UpdateStabilizedHudPose(deltaTime);
    }

    private void UpdateHardLockedHudPose()
    {
        if (!configureAsXrOverlay || !hardLockToHead)
            return;

        if (xrRenderCamera == null)
            xrRenderCamera = FindHudCamera();

        if (xrRenderCamera == null)
            return;

        RectTransform rectTransform = transform as RectTransform;
        if (rectTransform == null)
            return;

        Transform cameraTransform = xrRenderCamera.transform;
        if (rectTransform.parent != cameraTransform)
            rectTransform.SetParent(cameraTransform, false);

        ConfigureHudRectAndScale(rectTransform);
        rectTransform.localPosition = Vector3.forward * planeDistance;
        rectTransform.localRotation = Quaternion.identity;
    }

    private void UpdateStabilizedHudPose(float deltaTime, bool force = false)
    {
        if (!configureAsXrOverlay || !useStabilizedWorldAnchor)
            return;

        if (xrRenderCamera == null)
            xrRenderCamera = FindHudCamera();

        if (xrRenderCamera == null)
            return;

        RectTransform rectTransform = transform as RectTransform;
        if (rectTransform == null)
            return;

        Transform cameraTransform = xrRenderCamera.transform;
        Vector3 targetPosition = cameraTransform.position;
        Quaternion targetRotation = cameraTransform.rotation;

        if (!hasStablePose || force)
        {
            stableCameraPosition = targetPosition;
            stableCameraRotation = targetRotation;
            hasStablePose = true;
        }
        else
        {
            float positionDelta = Vector3.Distance(stableCameraPosition, targetPosition);
            float rotationDelta = Quaternion.Angle(stableCameraRotation, targetRotation);

            if (positionDelta > snapDistance || rotationDelta > snapAngle)
            {
                stableCameraPosition = targetPosition;
                stableCameraRotation = targetRotation;
            }
            else
            {
                float positionT = GetSharpnessT(positionSharpness, deltaTime);
                float rotationT = GetSharpnessT(rotationSharpness, deltaTime);
                stableCameraPosition = Vector3.Lerp(stableCameraPosition, targetPosition, positionT);
                stableCameraRotation = Quaternion.Slerp(stableCameraRotation, targetRotation, rotationT);
            }
        }

        ConfigureHudRectAndScale(rectTransform);

        Vector3 forward = stableCameraRotation * Vector3.forward;
        rectTransform.SetPositionAndRotation(stableCameraPosition + forward * planeDistance, stableCameraRotation);
    }

    private void ConfigureHudRectAndScale(RectTransform rectTransform)
    {
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.zero;
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.sizeDelta = referenceResolution;
        rectTransform.anchoredPosition = referenceResolution * 0.5f;

        float verticalFov = Mathf.Max(1f, xrRenderCamera.fieldOfView) * Mathf.Deg2Rad;
        float worldHeight = 2f * planeDistance * Mathf.Tan(verticalFov * 0.5f) * verticalViewCoverage;
        float worldScale = worldHeight / Mathf.Max(1f, referenceResolution.y);
        rectTransform.localScale = new Vector3(worldScale, worldScale, worldScale);
    }

    private static float GetSharpnessT(float sharpness, float deltaTime)
    {
        if (sharpness <= 0f)
            return 1f;

        return 1f - Mathf.Exp(-sharpness * Mathf.Max(0f, deltaTime));
    }

    private void ApplyNoDepthMaterials()
    {
        Graphic[] graphics = GetComponentsInChildren<Graphic>(true);
        for (int i = 0; i < graphics.Length; i++)
        {
            Graphic graphic = graphics[i];
            TextMeshProUGUI text = graphic as TextMeshProUGUI;
            if (text != null)
            {
                ApplyNoDepthTextMaterial(text);
                continue;
            }

            Material material = GetNoDepthUiMaterial();
            if (material != null)
                graphic.material = material;
        }
    }

    private Material GetNoDepthUiMaterial()
    {
        if (noDepthUiMaterial != null)
            return noDepthUiMaterial;

        Shader noDepthShader = Shader.Find("UI/NoZTest");
        if (noDepthShader == null)
            return null;

        noDepthUiMaterial = new Material(noDepthShader)
        {
            name = "Runtime UI No Depth"
        };

        return noDepthUiMaterial;
    }

    private static void ApplyNoDepthTextMaterial(TextMeshProUGUI text)
    {
        Material sourceMaterial = text.fontSharedMaterial;
        if (sourceMaterial == null)
            return;

        Shader overlayShader = Shader.Find("TextMeshPro/Distance Field Overlay");
        if (overlayShader == null)
            overlayShader = Shader.Find("TextMeshPro/Mobile/Distance Field Overlay");

        if (overlayShader == null)
            return;

        Material overlayMaterial = new Material(sourceMaterial)
        {
            shader = overlayShader,
            name = sourceMaterial.name + " No Depth"
        };

        text.fontSharedMaterial = overlayMaterial;
    }

    private static void SetText(TextMeshProUGUI target, float value)
    {
        if (target == null)
            return;

        target.text = value.ToString("0.0");
    }

    private static void SetText(TextMeshProUGUI target, string value)
    {
        if (target == null)
            return;

        target.text = value;
    }

    private void SetValueColor(TextMeshProUGUI target, bool hasLiveMeasurements, ValueRangeState state)
    {
        if (target == null)
            return;

        if (!hasLiveMeasurements)
        {
            target.color = idleValueColor;
            return;
        }

        switch (state)
        {
            case ValueRangeState.InRange:
                target.color = inRangeValueColor;
                break;
            case ValueRangeState.NearRange:
                target.color = nearRangeValueColor;
                break;
            default:
                target.color = outOfRangeValueColor;
                break;
        }
    }
}
