using UnityEngine;

public class WeldParameterMonitor : MonoBehaviour
{
    [Header("HUD")]
    [SerializeField] private WeldingHudPanel hudPanel;

    [Header("Smoothing")]
    [SerializeField] private float displayUpdateInterval = 0.5f;
    [SerializeField] private float valueSharpness = 12f;
    [SerializeField] private float minimumSpeedForAnglesMmPerSec = 5f;

    [Header("Speed Display")]
    [SerializeField] private float speedDisplayScale = 0.45f;
    [SerializeField] private float minMovementThreshold = 0.00005f;
    [SerializeField] private float speedSmoothingSeconds = 0.6f;

    [Header("HUD Near-Range Coaching")]
    [SerializeField] private float hudNearSpeedMargin = 0.8f;
    [SerializeField] private float hudNearWorkAngleMargin = 5f;
    [SerializeField] private float hudNearTravelAngleMargin = 3f;
    [SerializeField] private float hudNearCtwdMargin = 2f;

    [Header("Work Angle Debug")]
    [SerializeField] private bool logWorkAngleDebug;
    [SerializeField] private WeldJointType debugSelectedJointType;
    [SerializeField] private Vector3 debugSeamDirection;
    [SerializeField] private Vector3 debugHitNormal;
    [SerializeField] private Vector3 debugDerivedSecondNormal;
    [SerializeField] private float debugRawAngleA;
    [SerializeField] private float debugRawAngleB;

    public float TravelSpeedMmPerSec { get; private set; }
    public float StickoutDistanceMm { get; private set; }
    public float TravelAngleDeg { get; private set; }
    public float WorkAngleDeg { get; private set; }
    public float PathDistanceMm { get; private set; }
    public bool IsInsidePathTolerance { get; private set; }
    public bool HasPreviewMeasurements { get; private set; }
    public bool HasLiveMeasurements { get; private set; }

    private bool hasPreviousSample;
    private bool hasSmoothedValues;
    private float previousSampleTime;
    private float nextHudUpdateTime;
    private Vector3 previousBeadPoint;

    private void Awake()
    {
        SanitizeSettings();
        ResolveHudPanel();
    }

    private void OnValidate()
    {
        SanitizeSettings();
    }

    public void ResetMeasurements(bool updateHud = true)
    {
        bool hadMeasurements = hasPreviousSample || hasSmoothedValues;

        hasPreviousSample = false;
        hasSmoothedValues = false;

        TravelSpeedMmPerSec = 0f;
        StickoutDistanceMm = 0f;
        TravelAngleDeg = 0f;
        WorkAngleDeg = 0f;
        PathDistanceMm = 0f;
        IsInsidePathTolerance = false;
        HasPreviewMeasurements = false;
        HasLiveMeasurements = false;

        if (updateHud && hadMeasurements)
            PushToHud(true);
    }

    public void UpdateMeasurements(
        Vector3 beadPoint,
        Vector3 surfacePoint,
        Vector3 surfaceNormal,
        Vector3 torchTipWorldPos,
        Vector3 torchTipForward,
        WeldPath weldPath,
        WeldableJoint activeJoint)
    {
        ResolveHudPanel();

        float now = Time.unscaledTime;
        float deltaTime = hasPreviousSample
            ? Mathf.Max(0.0001f, now - previousSampleTime)
            : 0f;

        float rawSpeedMmPerSec = 0f;
        Vector3 travelDirection = Vector3.zero;
        bool shouldUpdatePreviousSample = !hasPreviousSample;
        if (hasPreviousSample)
        {
            Vector3 travelDelta = beadPoint - previousBeadPoint;
            float travelDistance = travelDelta.magnitude;
            if (travelDistance >= minMovementThreshold)
            {
                rawSpeedMmPerSec = travelDistance / deltaTime * 1000f;
                travelDirection = travelDelta.normalized;
                shouldUpdatePreviousSample = true;
            }
        }

        Vector3 torchForward = torchTipForward.sqrMagnitude > 1e-8f
            ? torchTipForward.normalized
            : Vector3.forward;

        float rawStickoutMm = Mathf.Max(
            0f,
            Vector3.Dot(surfacePoint - torchTipWorldPos, torchForward) * 1000f);

        float rawTravelAngle = CalculateTravelAngle(
            torchForward,
            surfaceNormal,
            travelDirection,
            rawSpeedMmPerSec);

        float rawWorkAngle = CalculateApproximateWorkAngle(
            torchForward,
            surfaceNormal,
            surfacePoint,
            weldPath,
            activeJoint);

        float rawPathDistanceMm = 0f;
        bool rawInsidePathTolerance = false;
        if (weldPath != null)
        {
            rawPathDistanceMm = weldPath.DistanceToPath(surfacePoint) * 1000f;
            rawInsidePathTolerance = weldPath.IsNearPath(surfacePoint);
        }

        float t = hasSmoothedValues
            ? GetSharpnessT(valueSharpness, Time.unscaledDeltaTime)
            : 1f;
        float speedT = hasSmoothedValues
            ? GetSmoothingSecondsT(speedSmoothingSeconds, Time.unscaledDeltaTime)
            : 1f;

        float displayedSpeedMmPerSec = rawSpeedMmPerSec * speedDisplayScale;

        TravelSpeedMmPerSec = Mathf.Lerp(TravelSpeedMmPerSec, displayedSpeedMmPerSec, speedT);
        StickoutDistanceMm = Mathf.Lerp(StickoutDistanceMm, rawStickoutMm, t);
        TravelAngleDeg = Mathf.Lerp(TravelAngleDeg, rawTravelAngle, t);
        WorkAngleDeg = Mathf.Lerp(WorkAngleDeg, rawWorkAngle, t);
        PathDistanceMm = Mathf.Lerp(PathDistanceMm, rawPathDistanceMm, t);
        IsInsidePathTolerance = rawInsidePathTolerance;

        if (shouldUpdatePreviousSample)
        {
            previousBeadPoint = beadPoint;
            previousSampleTime = now;
            hasPreviousSample = true;
        }

        hasSmoothedValues = true;
        HasPreviewMeasurements = false;
        HasLiveMeasurements = true;

        PushToHud(false);
    }

    public void UpdatePreviewMeasurements(
        Vector3 surfacePoint,
        Vector3 surfaceNormal,
        Vector3 torchTipWorldPos,
        Vector3 torchTipForward,
        WeldPath weldPath,
        WeldableJoint activeJoint)
    {
        ResolveHudPanel();

        Vector3 torchForward = torchTipForward.sqrMagnitude > 1e-8f
            ? torchTipForward.normalized
            : Vector3.forward;

        float rawStickoutMm = Mathf.Max(
            0f,
            Vector3.Dot(surfacePoint - torchTipWorldPos, torchForward) * 1000f);

        bool hasTravelAnglePreview = TryGetNearestSeamDirection(
            weldPath,
            surfacePoint,
            out Vector3 seamDirection);

        float rawTravelAngle = hasTravelAnglePreview
            ? CalculatePreviewTravelAngle(torchForward, surfaceNormal, seamDirection)
            : 0f;

        float rawWorkAngle = CalculateApproximateWorkAngle(
            torchForward,
            surfaceNormal,
            surfacePoint,
            weldPath,
            activeJoint);

        float rawPathDistanceMm = 0f;
        bool rawInsidePathTolerance = false;
        if (weldPath != null)
        {
            rawPathDistanceMm = weldPath.DistanceToPath(surfacePoint) * 1000f;
            rawInsidePathTolerance = weldPath.IsNearPath(surfacePoint);
        }

        float t = hasSmoothedValues
            ? GetSharpnessT(valueSharpness, Time.unscaledDeltaTime)
            : 1f;

        TravelSpeedMmPerSec = 0f;
        StickoutDistanceMm = Mathf.Lerp(StickoutDistanceMm, rawStickoutMm, t);
        TravelAngleDeg = Mathf.Lerp(TravelAngleDeg, rawTravelAngle, t);
        WorkAngleDeg = Mathf.Lerp(WorkAngleDeg, rawWorkAngle, t);
        PathDistanceMm = Mathf.Lerp(PathDistanceMm, rawPathDistanceMm, t);
        IsInsidePathTolerance = rawInsidePathTolerance;
        hasSmoothedValues = true;
        HasPreviewMeasurements = true;

        PushPreviewToHud(hasTravelAnglePreview, false);
    }

    private float CalculateTravelAngle(
        Vector3 torchForward,
        Vector3 surfaceNormal,
        Vector3 travelDirection,
        float rawSpeedMmPerSec)
    {
        if (rawSpeedMmPerSec < minimumSpeedForAnglesMmPerSec)
            return 0f;

        if (surfaceNormal.sqrMagnitude < 1e-8f || travelDirection.sqrMagnitude < 1e-8f)
            return 0f;

        Vector3 normalAwayFromSurface = surfaceNormal.normalized;
        Vector3 torchTowardSurface = torchForward.normalized;
        Vector3 idealTorchDirection = -normalAwayFromSurface;
        Vector3 travelPlaneNormal = Vector3.Cross(travelDirection, normalAwayFromSurface);

        if (travelPlaneNormal.sqrMagnitude < 1e-8f)
            return 0f;

        Vector3 torchInTravelPlane = Vector3.ProjectOnPlane(torchTowardSurface, travelPlaneNormal);
        if (torchInTravelPlane.sqrMagnitude < 1e-8f)
            return 0f;

        return Mathf.Clamp(Vector3.Angle(torchInTravelPlane.normalized, idealTorchDirection), 0f, 90f);
    }

    private float CalculatePreviewTravelAngle(
        Vector3 torchForward,
        Vector3 surfaceNormal,
        Vector3 seamDirection)
    {
        if (seamDirection.sqrMagnitude < 1e-8f)
            return 0f;

        return CalculateTravelAngle(
            torchForward,
            surfaceNormal,
            seamDirection,
            minimumSpeedForAnglesMmPerSec);
    }

    private float CalculateApproximateWorkAngle(
        Vector3 torchForward,
        Vector3 surfaceNormal,
        Vector3 surfacePoint,
        WeldPath weldPath,
        WeldableJoint activeJoint)
    {
        if (surfaceNormal.sqrMagnitude < 1e-8f)
            return 0f;

        if (!TryGetNearestSeamDirection(weldPath, surfacePoint, out Vector3 seamDirection))
            return 0f;

        WeldJointType selectedJointType = WeldJointSelectionState.GetSelectedOrDefault();
        debugSelectedJointType = selectedJointType;
        debugSeamDirection = seamDirection;
        debugHitNormal = surfaceNormal;

        if (activeJoint != null)
        {
            activeJoint.ResolveWorkAngleReferences();

            if (activeJoint.workAnglePlateA != null)
            {
                Vector3 primaryNormal = activeJoint.workAnglePlateA.forward;
                Vector3 secondaryNormal = activeJoint.workAnglePlateB != null
                    ? activeJoint.workAnglePlateB.forward
                    : Vector3.zero;

                return CalculateReferenceWorkAngle(
                    torchForward,
                    primaryNormal,
                    secondaryNormal,
                    seamDirection);
            }
        }

        return CalculateSingleSurfaceWorkAngle(torchForward, surfaceNormal, seamDirection);
    }

    private float CalculateReferenceWorkAngle(
        Vector3 torchForward,
        Vector3 primaryPlateNormal,
        Vector3 secondaryPlateNormal,
        Vector3 seamDirection)
    {
        if (torchForward.sqrMagnitude < 1e-8f ||
            primaryPlateNormal.sqrMagnitude < 1e-8f ||
            seamDirection.sqrMagnitude < 1e-8f)
        {
            return 0f;
        }

        Vector3 seam = seamDirection.normalized;
        Vector3 torchTowardWork = Vector3.ProjectOnPlane(torchForward.normalized, seam);
        Vector3 inwardA = Vector3.ProjectOnPlane(-primaryPlateNormal.normalized, seam);
        if (torchTowardWork.sqrMagnitude < 1e-8f || inwardA.sqrMagnitude < 1e-8f)
            return 0f;

        float angleA = MeasureWorkAngleAgainstPlate(torchTowardWork, inwardA);
        float angleB = 0f;
        debugDerivedSecondNormal = Vector3.zero;

        if (secondaryPlateNormal.sqrMagnitude > 1e-8f)
        {
            Vector3 inwardB = Vector3.ProjectOnPlane(-secondaryPlateNormal.normalized, seam);
            if (inwardB.sqrMagnitude > 1e-8f)
            {
                angleB = MeasureWorkAngleAgainstPlate(torchTowardWork, inwardB);
                debugDerivedSecondNormal = secondaryPlateNormal.normalized;
            }
        }

        debugRawAngleA = angleA;
        debugRawAngleB = angleB;

        if (logWorkAngleDebug)
        {
            Debug.Log($"[WeldParameterMonitor] {debugSelectedJointType} work angle: " +
                      $"seam={debugSeamDirection} hitNormal={debugHitNormal} " +
                      $"normalB={debugDerivedSecondNormal} angleA={debugRawAngleA:F1} " +
                      $"angleB={debugRawAngleB:F1} displayed={angleA:F1}");
        }

        return Mathf.Clamp(angleA, 0f, 90f);
    }

    private float CalculateSingleSurfaceWorkAngle(
        Vector3 torchForward,
        Vector3 surfaceNormal,
        Vector3 seamDirection)
    {
        if (torchForward.sqrMagnitude < 1e-8f ||
            surfaceNormal.sqrMagnitude < 1e-8f ||
            seamDirection.sqrMagnitude < 1e-8f)
        {
            return 0f;
        }

        Vector3 seam = seamDirection.normalized;
        Vector3 torchTowardWork = Vector3.ProjectOnPlane(torchForward.normalized, seam);
        Vector3 surfaceReference = Vector3.ProjectOnPlane(-surfaceNormal.normalized, seam);
        if (torchTowardWork.sqrMagnitude < 1e-8f || surfaceReference.sqrMagnitude < 1e-8f)
            return 0f;

        float angle = 90f - Vector3.Angle(torchTowardWork.normalized, surfaceReference.normalized);
        debugDerivedSecondNormal = Vector3.zero;
        debugRawAngleA = angle;
        debugRawAngleB = 0f;

        return Mathf.Clamp(angle, 0f, 90f);
    }

    private static float MeasureWorkAngleAgainstPlate(Vector3 torchTowardWork, Vector3 inwardPlateNormal)
    {
        return 90f - Vector3.Angle(torchTowardWork.normalized, inwardPlateNormal.normalized);
    }

    private static bool TryGetNearestSeamDirection(
        WeldPath weldPath,
        Vector3 worldPos,
        out Vector3 seamDirection)
    {
        seamDirection = Vector3.zero;

        if (weldPath == null || weldPath.waypoints == null || weldPath.waypoints.Count < 2)
            return false;

        float bestSqDist = float.PositiveInfinity;
        for (int i = 0; i < weldPath.waypoints.Count - 1; i++)
        {
            Transform start = weldPath.waypoints[i];
            Transform end = weldPath.waypoints[i + 1];
            if (start == null || end == null)
                continue;

            Vector3 segment = end.position - start.position;
            float segmentSq = segment.sqrMagnitude;
            if (segmentSq < 1e-8f)
                continue;

            Vector3 closest = ClosestPointOnSegment(start.position, end.position, worldPos);
            float sqDist = (closest - worldPos).sqrMagnitude;
            if (sqDist < bestSqDist)
            {
                bestSqDist = sqDist;
                seamDirection = segment.normalized;
            }
        }

        return seamDirection.sqrMagnitude > 1e-8f;
    }

    private static Vector3 ClosestPointOnSegment(Vector3 a, Vector3 b, Vector3 p)
    {
        Vector3 ab = b - a;
        float d = ab.sqrMagnitude;
        if (d < 1e-8f)
            return a;

        float t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / d);
        return a + ab * t;
    }

    private void PushToHud(bool force)
    {
        if (hudPanel == null)
            return;

        float now = Time.unscaledTime;
        float interval = Mathf.Max(0.01f, displayUpdateInterval);
        if (!force && now < nextHudUpdateTime)
            return;

        nextHudUpdateTime = now + interval;

        WeldQualityProfile profile =
            WeldQualityTable.GetProfile(WeldJointSelectionState.GetSelectedOrDefault());

        hudPanel.SetValues(TravelAngleDeg, WorkAngleDeg, TravelSpeedMmPerSec, StickoutDistanceMm);
        hudPanel.SetValueRangeColors(
            hasSmoothedValues,
            GetHudRangeState(
                TravelAngleDeg,
                profile.NormalTravelAngleMin,
                profile.NormalTravelAngleMax,
                hudNearTravelAngleMargin),
            GetHudRangeState(
                WorkAngleDeg,
                profile.NormalWorkAngleMin,
                profile.NormalWorkAngleMax,
                hudNearWorkAngleMargin),
            GetHudRangeState(
                TravelSpeedMmPerSec,
                profile.NormalSpeedMin,
                profile.NormalSpeedMax,
                hudNearSpeedMargin),
            GetHudRangeState(
                StickoutDistanceMm,
                profile.NormalCtwdMin,
                profile.NormalCtwdMax,
                hudNearCtwdMargin));
        hudPanel.SetPathValues(PathDistanceMm, IsInsidePathTolerance);
    }

    private void PushPreviewToHud(bool hasTravelAnglePreview, bool force)
    {
        if (hudPanel == null)
            return;

        float now = Time.unscaledTime;
        float interval = Mathf.Max(0.01f, displayUpdateInterval);
        if (!force && now < nextHudUpdateTime)
            return;

        nextHudUpdateTime = now + interval;

        WeldQualityProfile profile =
            WeldQualityTable.GetProfile(WeldJointSelectionState.GetSelectedOrDefault());

        hudPanel.SetValues(TravelAngleDeg, WorkAngleDeg, 0f, StickoutDistanceMm);
        hudPanel.SetPreviewValueRangeColors(
            hasTravelAnglePreview,
            GetHudRangeState(
                TravelAngleDeg,
                profile.NormalTravelAngleMin,
                profile.NormalTravelAngleMax,
                hudNearTravelAngleMargin),
            GetHudRangeState(
                WorkAngleDeg,
                profile.NormalWorkAngleMin,
                profile.NormalWorkAngleMax,
                hudNearWorkAngleMargin),
            GetHudRangeState(
                StickoutDistanceMm,
                profile.NormalCtwdMin,
                profile.NormalCtwdMax,
                hudNearCtwdMargin));
        hudPanel.SetPathValues(PathDistanceMm, IsInsidePathTolerance);
    }

    private void ResolveHudPanel()
    {
        if (hudPanel != null)
            return;

#if UNITY_2023_1_OR_NEWER
        hudPanel = FindFirstObjectByType<WeldingHudPanel>(FindObjectsInactive.Include);
#else
        hudPanel = FindObjectOfType<WeldingHudPanel>(true);
#endif
    }

    private void SanitizeSettings()
    {
        displayUpdateInterval = Mathf.Max(0.01f, displayUpdateInterval);
        valueSharpness = Mathf.Max(0f, valueSharpness);
        minimumSpeedForAnglesMmPerSec = Mathf.Max(0f, minimumSpeedForAnglesMmPerSec);
        speedDisplayScale = Mathf.Max(0f, speedDisplayScale);
        minMovementThreshold = Mathf.Max(0f, minMovementThreshold);
        speedSmoothingSeconds = Mathf.Max(0.01f, speedSmoothingSeconds);
        hudNearSpeedMargin = Mathf.Max(0f, hudNearSpeedMargin);
        hudNearWorkAngleMargin = Mathf.Max(0f, hudNearWorkAngleMargin);
        hudNearTravelAngleMargin = Mathf.Max(0f, hudNearTravelAngleMargin);
        hudNearCtwdMargin = Mathf.Max(0f, hudNearCtwdMargin);
    }

    private static WeldingHudPanel.ValueRangeState GetHudRangeState(
        float value,
        float strictMin,
        float strictMax,
        float nearMargin)
    {
        if (value >= strictMin && value <= strictMax)
            return WeldingHudPanel.ValueRangeState.InRange;

        if (value >= strictMin - nearMargin && value <= strictMax + nearMargin)
            return WeldingHudPanel.ValueRangeState.NearRange;

        return WeldingHudPanel.ValueRangeState.OutOfRange;
    }

    private static float GetSharpnessT(float sharpness, float deltaTime)
    {
        if (sharpness <= 0f)
            return 1f;

        return 1f - Mathf.Exp(-sharpness * Mathf.Max(0f, deltaTime));
    }

    private static float GetSmoothingSecondsT(float smoothingSeconds, float deltaTime)
    {
        if (smoothingSeconds <= 0f)
            return 1f;

        return 1f - Mathf.Exp(-Mathf.Max(0f, deltaTime) / smoothingSeconds);
    }
}
