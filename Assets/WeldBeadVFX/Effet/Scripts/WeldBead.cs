using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Splines;
using UnityEngine.VFX;

[RequireComponent(typeof(SplineContainer))]
public class WeldBead : MonoBehaviour
{
    private const float MinimumStableKnotSpacing = 0.005f;

    [Header("Recording Settings")]
    [Tooltip("Minimum distance the torch tip must travel before a new bead ring is placed.\n"
           + "Too small (< 0.003) = rings stack on top of each other = bead inflates into a blob.\n"
           + "Too large (> 0.02) = bead looks segmented and chunky.\n"
           + "Recommended: 0.004 - 0.008m")]
    [SerializeField] [Min(MinimumStableKnotSpacing)] private float minDistanceBetweenKnots = MinimumStableKnotSpacing;
    [SerializeField] private int   maxKnots               = 512;

    [Header("Bead Spawning")]
    [SerializeField] private GameObject tubeMeshBuilderPrefab;
    [SerializeField] private Transform  beadParent;

    [Header("References")]
    [SerializeField] private Transform   torchTip;
    [SerializeField] private WeldSpatter spatter;
    [SerializeField] private VisualEffect particleEffect;
    [SerializeField] private WeldArcFlicker arcFlicker;
    [SerializeField] private WeldSparkSystem sparkSystem;
    [SerializeField] private WeldSmokeSystem smokeSystem;
    [SerializeField] private WeldParameterMonitor parameterMonitor;

    [Header("Automatic Defect Detection")]
    [SerializeField] private bool useAutomaticDefectDetection = true;
    [SerializeField] private WeldDefectEvaluator defectEvaluator;
    [SerializeField] private WeldReportTracker reportTracker;

    [Header("VR Surface Detection")]
    [Tooltip("How far the torch-tip raycast reaches to find a Weldable surface. Keep this close to the intended CTWD so VR welding cannot continue while the torch is far from the joint.")]
    [SerializeField] private float vrRaycastMaxDistance = 0.025f;
    [Tooltip("How far the idle preview raycast can reach before welding starts. This only updates the HUD and never creates bead geometry.")]
    [SerializeField] private float vrPreviewRaycastMaxDistance = 0.05f;
    [Tooltip("Small grace window for brief VR tracking/raycast misses. Prevents tiny hand tremors from splitting one weld into separate beads.")]
    [SerializeField] [Min(0f)] private float lostContactGraceSeconds = 0.08f;
    [Tooltip("Layer mask that matches the Weldable layer used on joint colliders.")]
    [SerializeField] private LayerMask weldableMask;

    [Header("VR Air-Weld Prevention")]
    [Tooltip("Once a weld stroke starts on a collider, the bead ONLY continues on that exact "
           + "collider. Moving to a different surface stops the stroke immediately.")]
    [SerializeField] private bool lockToStartCollider = true;

    [Header("VR Smoothing & Snap")]
    [Tooltip("Auto-assigned at runtime from the spawned joint's WeldPath component.")]
    [SerializeField] public WeldPath weldPath;

    [Tooltip("Smoothing factor for VR hand position (Exponential Moving Average).\n"
           + "0 = no smoothing, instant response (may show jitter).\n"
           + "1 = fully frozen, never moves.\n"
           + "Recommended: 0.6 - 0.75. This smooths tremor with ZERO fixed lag\n"
           + "because EMA weights recent frames more heavily than old ones.\n"
           + "Unlike the old ring-buffer average, fast movements still register immediately.")]
    [SerializeField] [Range(0f, 0.95f)] private float positionSmoothingEMA = 0.65f;

    [Tooltip("Maximum strength used when the torch is almost exactly on the seam.\n"
           + "0 = no seam snap. 1 = fully locked to seam (only along-seam progress remains).\n"
           + "The pull fades out toward Seam Snap Radius so off-line welds are not forced onto the guide.\n"
           + "Drag in Play mode to tune.")]
    [SerializeField] [Range(0f, 1f)] private float seamSnapStrength = 0.85f;

    [Tooltip("Maximum distance from the seam line where snap may activate (metres).\n"
           + "Keep this tight so snap only helps when the torch is already very close to the groove.\n"
           + "Recommended: 0.01 - 0.02m.")]
    [SerializeField] [Min(0f)] private float seamSnapRadius = 0.02f;

    // Internal state
    private SplineContainer _splineContainer;
    private Spline          _spline;
    private bool            _isRecording;
    private Vector3         _lastRecordedPosition;
    private Vector3         _lastRecordedForDistance;
    private Vector3         _lastDirection = Vector3.forward;
    private Vector3         _lastRayDir    = Vector3.down;

    private TubeMeshBuilder            _currentBuilder;
    private List<TubeMeshBuilder>      _allBeads  = new();
    private TubeMeshBuilder.WeldDefect _currentDefect = TubeMeshBuilder.WeldDefect.Normal;

    // VR state
    private bool     _vrWeldActive;
    private Collider _strokeCollider;
    private WeldableJoint _strokeJoint;
    private float    _lostContactSeconds;

    // EMA smoothed position — single Vector3, no lag, no buffer
    private Vector3 _emaPosition;
    private bool    _emaInitialized;

    // Public read-only
    public Spline ActiveSpline => _spline;
    public int    KnotCount    => _spline?.Count ?? 0;
    public bool   IsRecording  => _isRecording;
    public VisualEffect ContactParticleEffect => particleEffect;
    public IReadOnlyList<TubeMeshBuilder> AllBeads => _allBeads;

    private float EffectiveMinDistanceBetweenKnots => Mathf.Max(minDistanceBetweenKnots, MinimumStableKnotSpacing);

    private void OnValidate()
    {
        minDistanceBetweenKnots = Mathf.Max(minDistanceBetweenKnots, MinimumStableKnotSpacing);
        seamSnapRadius = Mathf.Max(0f, seamSnapRadius);
    }

    private void Awake()
    {
        minDistanceBetweenKnots = Mathf.Max(minDistanceBetweenKnots, MinimumStableKnotSpacing);

        _splineContainer = GetComponent<SplineContainer>();
        _splineContainer.Spline.Clear();
        _spline = _splineContainer.Spline;

        if (torchTip == null)
            torchTip = transform;

        if (weldableMask.value == 0)
            weldableMask = LayerMask.GetMask("Weldable");

        if (parameterMonitor == null)
            parameterMonitor = GetComponent<WeldParameterMonitor>();

        if (parameterMonitor == null)
            parameterMonitor = gameObject.AddComponent<WeldParameterMonitor>();

        if (defectEvaluator == null)
            defectEvaluator = GetComponent<WeldDefectEvaluator>();

        if (defectEvaluator == null)
            defectEvaluator = gameObject.AddComponent<WeldDefectEvaluator>();

        if (reportTracker == null)
            reportTracker = GetComponent<WeldReportTracker>();

        if (reportTracker == null)
            reportTracker = gameObject.AddComponent<WeldReportTracker>();

    }
    private void ResetSmoothBuffer()
    {
        _emaInitialized = false;
    }

    // ════════════════════════════════════════════════════════════════════
    //  MOUSE MODE - unchanged from original
    // ════════════════════════════════════════════════════════════════════
    [Header("VFX Polishing")]
    [SerializeField] private float arcJitterStrength = 0.006f; // Increased from 0.002
    [SerializeField] private float arcJitterSpeed    = 50f;   // Increased from 30

    private float _jitterTime;

    private Vector3 GetJitteredPosition(Vector3 basePos)
    {
        _jitterTime += Time.deltaTime * arcJitterSpeed;
        Vector3 jitter = new Vector3(
            Mathf.PerlinNoise(_jitterTime, 0f) - 0.5f,
            Mathf.PerlinNoise(0f, _jitterTime) - 0.5f,
            Mathf.PerlinNoise(_jitterTime, _jitterTime) - 0.5f
        ) * arcJitterStrength;
        return basePos + jitter;
    }

    void Update()
    {
        if (Keyboard.current == null || Mouse.current == null) return;

        Ray ray = Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue());

        if (!useAutomaticDefectDetection)
        {
            if (Keyboard.current.digit1Key.wasPressedThisFrame)
            { _currentDefect = TubeMeshBuilder.WeldDefect.Normal;       _currentBuilder?.SetDefect(_currentDefect); }
            if (Keyboard.current.digit2Key.wasPressedThisFrame)
            { _currentDefect = TubeMeshBuilder.WeldDefect.Porosity;     _currentBuilder?.SetDefect(_currentDefect); }
            if (Keyboard.current.digit3Key.wasPressedThisFrame)
            { _currentDefect = TubeMeshBuilder.WeldDefect.Overlap;      _currentBuilder?.SetDefect(_currentDefect); }
            if (Keyboard.current.digit4Key.wasPressedThisFrame)
            { _currentDefect = TubeMeshBuilder.WeldDefect.LackOfFusion; _currentBuilder?.SetDefect(_currentDefect); }
            if (Keyboard.current.digit5Key.wasPressedThisFrame)
            { _currentDefect = TubeMeshBuilder.WeldDefect.Spatter;      _currentBuilder?.SetDefect(_currentDefect); }
        }

        if (Keyboard.current.cKey.wasPressedThisFrame)
            ClearAllBeads();

        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            if (Physics.Raycast(ray, out RaycastHit startHit, 100f, LayerMask.GetMask("Weldable")))
                StartRecording(startHit.point, ray.direction);
        }

        if (Mouse.current.leftButton.wasReleasedThisFrame && _isRecording)
            StopRecording();

        if (!_isRecording) return;
        if (!Mouse.current.leftButton.isPressed) return;

        if (!Physics.Raycast(ray, out RaycastHit hit, 100f, LayerMask.GetMask("Weldable"))) return;

        Vector3 currentPos = hit.point;
        float   distMoved  = Vector3.Distance(currentPos, _lastRecordedForDistance);

        if (particleEffect != null)
        {
            Vector3 jitteredPos = GetJitteredPosition(currentPos);
            particleEffect.SetVector3("OriginPosition", jitteredPos);
            particleEffect.SetVector3("NormalDirection", ray.direction);
            particleEffect.SendEvent("Trigger");
        }

        if (distMoved >= EffectiveMinDistanceBetweenKnots)
        {
            Vector3 direction = (currentPos - _lastRecordedPosition).normalized;
            _lastDirection    = direction;
            _lastRayDir       = ray.direction;
            AddKnot(currentPos, direction, ray.direction);
            _lastRecordedForDistance = currentPos;
            _lastRecordedPosition    = currentPos;
        }
    }

    // ════════════════════════════════════════════════════════════════════
    //  VR PUBLIC API
    // ════════════════════════════════════════════════════════════════════

    public void BeginVRBead()
    {
        _vrWeldActive   = true;
        _strokeCollider = null;
        _strokeJoint    = null;
        _lostContactSeconds = 0f;
        ResetSmoothBuffer();
        parameterMonitor?.ResetMeasurements();
    }

    public void EndVRBead()
    {
        _vrWeldActive   = false;
        _strokeCollider = null;
        _strokeJoint    = null;
        _lostContactSeconds = 0f;
        ResetSmoothBuffer();
        parameterMonitor?.ResetMeasurements();
        if (_isRecording)
            StopRecording();
    }

    public bool UpdateVRBead(Vector3 torchTipWorldPos, Vector3 torchTipForward)
    {
        if (!_vrWeldActive) return false;

        // Raycast from the torch tip to find the weld surface.
        //
        // TWO-RAY approach — handles both normal distance and close-contact:
        //
        // Ray A (forward): tip → forward, max = vrRaycastMaxDistance
        //   Handles the normal case: tip is a few cm from the surface.
        //
        // Ray B (backward): a small probe point slightly ahead of the tip
        //   fires BACKWARD toward the tip. This catches the close-contact
        //   case where the tip is already touching/past the surface so
        //   Ray A misses it. Distance is tiny (0.015m) so it can NEVER
        //   reach the controller body behind the tip.
        //
        // The old pullback approach moved Ray A's origin 0.15m back into
        // the controller body — the grip area became a second valid origin
        // and produced false hits on the table below it.
        const float probeAhead   = 0.015f;  // how far ahead to start Ray B
        const float backRayLen   = probeAhead + 0.005f; // slightly more than probeAhead

        RaycastHit hit;
        bool foundHit = false;

        // Ray A: normal forward ray from tip
        Ray rayA = new Ray(torchTipWorldPos, torchTipForward);
        if (Physics.Raycast(rayA, out RaycastHit hitA, vrRaycastMaxDistance,
                            weldableMask, QueryTriggerInteraction.Ignore))
        {
            hit      = hitA;
            foundHit = true;
        }
        else
        {
            // Ray B: short backward ray from just ahead of the tip
            // Catches the case where the nozzle is pressed against the surface
            Vector3 probeOrigin = torchTipWorldPos + torchTipForward.normalized * probeAhead;
            Ray rayB = new Ray(probeOrigin, -torchTipForward);
            if (Physics.Raycast(rayB, out RaycastHit hitB, backRayLen,
                                weldableMask, QueryTriggerInteraction.Ignore))
            {
                hit      = hitB;
                foundHit = true;
            }
            else
            {
                hit = default;
            }
        }

        if (!foundHit)
        {
            _lostContactSeconds += Time.unscaledDeltaTime;

            if (_isRecording && _lostContactSeconds <= lostContactGraceSeconds)
                return true;

            if (_isRecording) StopRecording();
            _strokeCollider = null;
            _strokeJoint = null;
            _lostContactSeconds = 0f;
            ResetSmoothBuffer();
            parameterMonitor?.ResetMeasurements();
            return false;
        }

        _lostContactSeconds = 0f;

        WeldableJoint activeJoint = hit.collider.GetComponentInParent<WeldableJoint>();

        // Lock to the collider the stroke started on
        if (lockToStartCollider)
        {
            if (_strokeCollider == null)
            {
                _strokeCollider = hit.collider;
                _strokeJoint = activeJoint;
            }
            else
            {
                bool switchedWithinSameJoint =
                    _strokeJoint != null &&
                    activeJoint != null &&
                    activeJoint == _strokeJoint;

                if (hit.collider != _strokeCollider && !switchedWithinSameJoint)
                {
                    if (_isRecording) StopRecording();
                    _strokeCollider = null;
                    _strokeJoint = null;
                    ResetSmoothBuffer();
                    parameterMonitor?.ResetMeasurements();
                    return false;
                }
            }
        }

        // STAGE 1: Exponential Moving Average smoothing (zero fixed lag)
        // EMA formula: smoothed = alpha * raw + (1 - alpha) * previous_smoothed
        // where alpha = (1 - positionSmoothingEMA).
        // On the first sample of a new stroke, snap immediately to the raw point
        // so the bead starts exactly where the torch is, not at world origin.
        if (!_emaInitialized)
        {
            _emaPosition    = hit.point;
            _emaInitialized = true;
        }
        else
        {
            float alpha  = 1f - positionSmoothingEMA;
            _emaPosition = Vector3.Lerp(_emaPosition, hit.point, alpha);
        }
        Vector3 smoothed = _emaPosition;

        // STAGE 2: Seam projection
        Vector3 finalPoint = ApplySeamProjection(smoothed);

        // TubeMeshBuilder uses rayDir both to orient bead rings and to conform
        // each ring onto the surface. In VR the controller forward vector can
        // become nearly parallel to the plate when the tip is very close, which
        // stretches rings into wide sheets. Use the actual surface normal so
        // conform rays always travel into the plate instead.
        Vector3 rayDir = hit.normal.sqrMagnitude > 0.01f
            ? -hit.normal
            : torchTipForward.normalized;

        parameterMonitor?.UpdateMeasurements(
            finalPoint,
            hit.point,
            hit.normal,
            torchTipWorldPos,
            torchTipForward,
            weldPath,
            activeJoint);

        if (defectEvaluator != null)
        {
            WeldQualityEvaluation evaluation = defectEvaluator.EvaluateCurrent();

            if (useAutomaticDefectDetection)
                SetDefect(evaluation.Defect);

            reportTracker?.RecordSample(evaluation, Time.unscaledDeltaTime);
        }

        if (!_isRecording)
            StartRecording(finalPoint, rayDir);

        if (particleEffect != null)
        {
            Vector3 jitteredPos = GetJitteredPosition(finalPoint);
            particleEffect.SetVector3("OriginPosition", jitteredPos);
            particleEffect.SetVector3("NormalDirection", rayDir);
            particleEffect.SendEvent("Trigger");
        }

        float distMoved = Vector3.Distance(finalPoint, _lastRecordedForDistance);
        if (distMoved >= EffectiveMinDistanceBetweenKnots)
        {
            Vector3 direction = (finalPoint - _lastRecordedPosition).normalized;
            if (direction == Vector3.zero) direction = _lastDirection;

            _lastDirection = direction;
            _lastRayDir    = rayDir;
            AddKnot(finalPoint, direction, rayDir);
            _lastRecordedForDistance = finalPoint;
            _lastRecordedPosition    = finalPoint;
        }

        return true;
    }

    public bool PreviewVRMeasurements(Vector3 torchTipWorldPos, Vector3 torchTipForward)
    {
        if (_vrWeldActive)
            return false;

        const float probeAhead = 0.015f;
        const float backRayLen = probeAhead + 0.005f;

        Vector3 forward = torchTipForward.sqrMagnitude > 1e-8f
            ? torchTipForward.normalized
            : Vector3.forward;

        RaycastHit hit;
        bool foundHit = false;

        Ray rayA = new Ray(torchTipWorldPos, forward);
        if (Physics.Raycast(rayA, out RaycastHit hitA, vrPreviewRaycastMaxDistance,
                            weldableMask, QueryTriggerInteraction.Ignore))
        {
            hit = hitA;
            foundHit = true;
        }
        else
        {
            Vector3 probeOrigin = torchTipWorldPos + forward * probeAhead;
            Ray rayB = new Ray(probeOrigin, -forward);
            if (Physics.Raycast(rayB, out RaycastHit hitB, backRayLen,
                                weldableMask, QueryTriggerInteraction.Ignore))
            {
                hit = hitB;
                foundHit = true;
            }
            else
            {
                hit = default;
            }
        }

        if (!foundHit)
        {
            parameterMonitor?.ResetMeasurements();
            return false;
        }

        WeldableJoint activeJoint = hit.collider.GetComponentInParent<WeldableJoint>();
        parameterMonitor?.UpdatePreviewMeasurements(
            hit.point,
            hit.normal,
            torchTipWorldPos,
            forward,
            weldPath,
            activeJoint);
        return true;
    }

    // ════════════════════════════════════════════════════════════════════
    //  SEAM PROJECTION
    // ════════════════════════════════════════════════════════════════════

    private Vector3 ApplySeamProjection(Vector3 smoothedPoint)
    {
        if (weldPath == null) return smoothedPoint;
        if (seamSnapStrength <= 0f) return smoothedPoint;
        if (weldPath.waypoints == null || weldPath.waypoints.Count < 2) return smoothedPoint;

        float   bestSqDist = float.PositiveInfinity;
        Vector3 bestA      = Vector3.zero;
        Vector3 bestB      = Vector3.zero;

        for (int i = 0; i < weldPath.waypoints.Count - 1; i++)
        {
            if (weldPath.waypoints[i] == null || weldPath.waypoints[i + 1] == null) continue;
            Vector3 a = weldPath.waypoints[i].position;
            Vector3 b = weldPath.waypoints[i + 1].position;
            Vector3 closest = ClosestPointOnSegment(a, b, smoothedPoint);
            float   sqDist  = (closest - smoothedPoint).sqrMagnitude;
            if (sqDist < bestSqDist)
            {
                bestSqDist = sqDist;
                bestA      = a;
                bestB      = b;
            }
        }

        if (seamSnapRadius <= 0f) return smoothedPoint;

        float snapRadiusSq = seamSnapRadius * seamSnapRadius;
        if (bestSqDist > snapRadiusSq) return smoothedPoint;

        Vector3 seamDir = (bestB - bestA).normalized;
        if (seamDir == Vector3.zero) return smoothedPoint;

        Vector3 fromA         = smoothedPoint - bestA;
        float   alongScalar   = Vector3.Dot(fromA, seamDir);
        Vector3 alongSeam     = seamDir * alongScalar;
        Vector3 perpendicular = fromA - alongSeam;

        float distanceToSeam = Mathf.Sqrt(bestSqDist);
        float distance01     = Mathf.Clamp01(distanceToSeam / seamSnapRadius);
        float snapFalloff    = 1f - Mathf.SmoothStep(0f, 1f, distance01);
        float snapWeight     = seamSnapStrength * snapFalloff;

        Vector3 correctedFromA = alongSeam + perpendicular * (1f - snapWeight);
        return bestA + correctedFromA;
    }

    private static Vector3 ClosestPointOnSegment(Vector3 a, Vector3 b, Vector3 p)
    {
        Vector3 ab = b - a;
        float   d  = ab.sqrMagnitude;
        if (d < 1e-8f) return a;
        float t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / d);
        return a + ab * t;
    }

    public void SetDefect(TubeMeshBuilder.WeldDefect defect)
    {
        _currentDefect = defect;
        _currentBuilder?.SetDefect(_currentDefect);
    }

    public void ShowOnlyDefect(TubeMeshBuilder.WeldDefect defect)
    {
        for (int i = 0; i < _allBeads.Count; i++)
        {
            if (_allBeads[i] != null)
                _allBeads[i].ShowOnlyDefect(defect);
        }

        spatter?.EnableSpatter(defect == TubeMeshBuilder.WeldDefect.Spatter);
    }

    public void ShowAllDefects()
    {
        for (int i = 0; i < _allBeads.Count; i++)
        {
            if (_allBeads[i] != null)
                _allBeads[i].ShowAllDefects();
        }

        spatter?.EnableSpatter(true);
    }

    public bool TryGetDefectBounds(TubeMeshBuilder.WeldDefect defect, out Bounds bounds)
    {
        bool hasBounds = false;
        bounds = default;

        for (int i = 0; i < _allBeads.Count; i++)
        {
            TubeMeshBuilder bead = _allBeads[i];
            if (bead == null || !bead.TryGetDefectBounds(defect, out Bounds beadBounds))
                continue;

            if (!hasBounds)
            {
                bounds = beadBounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(beadBounds);
            }
        }

        return hasBounds;
    }

    // ════════════════════════════════════════════════════════════════════
    //  SHARED RECORDING
    // ════════════════════════════════════════════════════════════════════

    public void StartRecording(Vector3 hitPosition, Vector3 rayDirection)
    {
        _spline.Clear();
        _lastRecordedPosition    = hitPosition;
        _lastRecordedForDistance = hitPosition;
        _lastRayDir              = rayDirection;
        _isRecording             = true;

        GameObject go = Instantiate(
            tubeMeshBuilderPrefab, Vector3.zero, Quaternion.identity, beadParent);

        _currentBuilder = go.GetComponent<TubeMeshBuilder>();
        _allBeads.Add(_currentBuilder);

        _currentBuilder.ResetMesh(rayDirection);
        _currentBuilder.StartAgeing();
        _currentBuilder.SetDefect(_currentDefect);

        Vector3 initialTangent = GetInitialBeadTangent(rayDirection);
        _lastDirection = initialTangent;
        AddKnot(hitPosition, initialTangent, rayDirection);

        Debug.Log($"[WeldBead] Recording started. Defect={_currentDefect}  TotalBeads={_allBeads.Count}");
    }

    public void StopRecording()
    {
        if (!_isRecording) return;
        _isRecording = false;
        _currentBuilder?.CapWithFan(false);
        _currentBuilder?.StopAgeing();
        Debug.Log($"[WeldBead] Recording stopped. {_spline.Count} knots.");
    }

    private void AddKnot(Vector3 worldPosition, Vector3 worldForward, Vector3 rayDir)
    {
        Vector3 actualTangent = _spline.Count == 0
            ? worldForward
            : (worldPosition - _lastRecordedPosition).normalized;

        if (sparkSystem != null)
            sparkSystem.TriggerSparks(worldPosition, -rayDir);

        if (smokeSystem != null)
            smokeSystem.EmitSmoke(worldPosition, 2);

        Vector3 localPos     = transform.InverseTransformPoint(worldPosition);
        Vector3 localForward = transform.InverseTransformDirection(worldForward);
        float   tangentLen   = EffectiveMinDistanceBetweenKnots * 0.33f;

        _spline.Add(
            new BezierKnot(localPos, -localForward * tangentLen, localForward * tangentLen,
                           Quaternion.identity),
            TangentMode.Broken);

        if (_spline.Count > maxKnots) _spline.RemoveAt(0);

        _currentBuilder?.OnKnotAdded(worldPosition, actualTangent, rayDir);

        if (_currentDefect == TubeMeshBuilder.WeldDefect.Spatter && _spline.Count > 0)
            StartCoroutine(SpawnSpatterNextFrame(worldPosition, actualTangent, rayDir,
                           Vector3.Cross(rayDir, actualTangent)));
    }

    private Vector3 GetInitialBeadTangent(Vector3 rayDirection)
    {
        Vector3 rayNorm = rayDirection.sqrMagnitude > 1e-8f
            ? rayDirection.normalized
            : Vector3.down;

        Vector3 tangent = Vector3.ProjectOnPlane(Vector3.up, rayNorm);
        if (tangent.sqrMagnitude < 1e-8f)
            tangent = Vector3.ProjectOnPlane(Vector3.right, rayNorm);
        if (tangent.sqrMagnitude < 1e-8f)
            tangent = Vector3.forward;

        return tangent.normalized;
    }

    private System.Collections.IEnumerator SpawnSpatterNextFrame(
        Vector3 pos, Vector3 tangent, Vector3 rayDir, Vector3 binormal)
    {
        yield return new WaitForSeconds(0.05f);
        spatter?.OnKnotAdded(pos, tangent, rayDir, binormal);
    }

    public void ClearAllBeads()
    {
        foreach (var bead in _allBeads)
            if (bead != null) Destroy(bead.gameObject);

        _allBeads.Clear();
        _currentBuilder = null;
        _spline.Clear();
        spatter?.ResetSpatter();
        Debug.Log("[WeldBead] All beads cleared.");
    }

    public (Vector3 position, Vector3 tangent) EvaluateAt(float t)
    {
        SplineUtility.Evaluate(_spline, t, out var pos, out var tan, out _);
        return (transform.TransformPoint(pos), transform.TransformDirection(tan).normalized);
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (weldPath != null && weldPath.waypoints != null && weldPath.waypoints.Count >= 2)
        {
            Gizmos.color = Color.red;
            for (int i = 0; i < weldPath.waypoints.Count - 1; i++)
            {
                if (weldPath.waypoints[i] == null || weldPath.waypoints[i + 1] == null) continue;
                Gizmos.DrawLine(weldPath.waypoints[i].position, weldPath.waypoints[i + 1].position);
            }

            Gizmos.color = new Color(1f, 0.2f, 0.2f, 1f);
            foreach (var wp in weldPath.waypoints)
                if (wp != null) Gizmos.DrawSphere(wp.position, 0.008f);

            Gizmos.color = new Color(1f, 0.55f, 0f, 0.20f);
            foreach (var wp in weldPath.waypoints)
                if (wp != null) Gizmos.DrawSphere(wp.position, seamSnapRadius);

            UnityEditor.Handles.color = Color.red;
            if (weldPath.waypoints[0] != null)
                UnityEditor.Handles.Label(
                    weldPath.waypoints[0].position + Vector3.up * 0.06f,
                    $"WeldPath [ASSIGNED]  snap={seamSnapStrength:F2}  r={seamSnapRadius:F3}m");
        }
        else
        {
            UnityEditor.Handles.color = Color.yellow;
            UnityEditor.Handles.Label(
                transform.position + Vector3.up * 0.12f,
                "WeldBead: WeldPath NOT assigned - seam snap inactive");
        }

        if (torchTip != null)
        {
            Gizmos.color = Color.white;
            Gizmos.DrawWireSphere(torchTip.position, 0.015f);

            Vector3 fwd = torchTip.forward;

            // Ray A: forward from tip (normal distance)
            Gizmos.color = _isRecording ? Color.red : Color.cyan;
            Gizmos.DrawLine(torchTip.position,
                            torchTip.position + fwd * vrRaycastMaxDistance);

            // Ray B: tiny backward probe (close-contact case)
            const float probeAhead = 0.015f;
            Vector3 probeOrigin = torchTip.position + fwd * probeAhead;
            Gizmos.color = new Color(1f, 0.5f, 0f); // orange
            Gizmos.DrawLine(probeOrigin, probeOrigin - fwd * (probeAhead + 0.005f));
            Gizmos.DrawWireSphere(probeOrigin, 0.003f);

            // Check which ray hits and show green dot at contact
            bool gizmoHit = false;
            RaycastHit gizmoHitInfo = default;

            Ray rayA = new Ray(torchTip.position, fwd);
            if (Physics.Raycast(rayA, out RaycastHit hA, vrRaycastMaxDistance,
                                LayerMask.GetMask("Weldable")))
            { gizmoHitInfo = hA; gizmoHit = true; }
            else
            {
                Ray rayB = new Ray(probeOrigin, -fwd);
                if (Physics.Raycast(rayB, out RaycastHit hB, probeAhead + 0.005f,
                                    LayerMask.GetMask("Weldable")))
                { gizmoHitInfo = hB; gizmoHit = true; }
            }

            if (gizmoHit)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawSphere(gizmoHitInfo.point, 0.008f);
                Gizmos.color = Color.blue;
                Gizmos.DrawLine(gizmoHitInfo.point,
                                gizmoHitInfo.point + gizmoHitInfo.normal * 0.05f);

                if (weldPath != null && seamSnapStrength > 0f)
                {
                    Vector3 projected = ApplySeamProjection(gizmoHitInfo.point);
                    if ((projected - gizmoHitInfo.point).sqrMagnitude > 1e-8f)
                    {
                        Gizmos.color = Color.yellow;
                        Gizmos.DrawLine(gizmoHitInfo.point, projected);
                        Gizmos.DrawSphere(projected, 0.005f);
                    }
                }
            }
        }

        if (_spline == null || _spline.Count < 2) return;

        Gizmos.color = Color.yellow;
        int steps = _spline.Count * 8;
        for (int i = 0; i < steps; i++)
        {
            SplineUtility.Evaluate(_spline, (float)i / steps,       out var p0, out _, out _);
            SplineUtility.Evaluate(_spline, (float)(i + 1) / steps, out var p1, out _, out _);
            Gizmos.DrawLine(transform.TransformPoint(p0), transform.TransformPoint(p1));
        }

        if (_spline.Count > 0)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawSphere(transform.TransformPoint(_spline[_spline.Count - 1].Position), 0.005f);
        }
    }
#endif
}
