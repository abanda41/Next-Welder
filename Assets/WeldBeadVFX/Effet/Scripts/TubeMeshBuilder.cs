using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class TubeMeshBuilder : MonoBehaviour
{
    private const float DefaultMinimumRingSpacing = 0.004f;
    private const float MaxConformRayLength = 0.08f;
    private const float MaxRayDirectionOffset = 0.02f;
    private static readonly int BeadAgePropertyId = Shader.PropertyToID("_BeadAge");

    // ── Defect types ─────────────────────────────────────────────────
    public enum WeldDefect { Normal = 0, Porosity = 1, Overlap = 2, LackOfFusion = 3, Spatter = 4 }
    private const int DEFECT_COUNT = 5;

    // ── Per-defect settings ──────────────────────────────────────────
    [System.Serializable]
    public class DefectSettings
    {
        public float rayDirectionOffset = 0.001f;
        public float snapMultiplier     = 1f;
        public float conformRayLength   = 0.02f;
        public float snapSharpness      = 1f;
        public float crownAmount        = 0.3f;
        public float crackThreshold     = 0.3f;
        public float crackWidth         = 0f;
        public float crackLift          = 0f;
    }

    [Header("Tube Shape")]
    [SerializeField] private int   sidesPerRing = 8;
    [SerializeField] private float tubeRadius   = 0.004f;
    [SerializeField] private float beadWidth    = 0.5f;
    [SerializeField] private float beadHeight   = 0.5f;
    [SerializeField] [Min(0f)] private float minimumRingSpacing = DefaultMinimumRingSpacing;

    [Header("Conform Settings (shared)")]
    [SerializeField] private LayerMask conformMask;

    [Header("Per-Defect Settings")]
    [SerializeField] private DefectSettings settingsNormal       = new();
    [SerializeField] private DefectSettings settingsPorosity     = new();
    [SerializeField] private DefectSettings settingsOverlap      = new();
    [SerializeField] private DefectSettings settingsLackOfFusion = new()
    {
        rayDirectionOffset = 0.008f,
        snapMultiplier     = 0f,
        conformRayLength   = 0.02f,
        snapSharpness      = 1f,
        crownAmount        = 0.3f,
        crackThreshold     = 0.3f,
        crackWidth         = 0.003f,
        crackLift          = 0.002f
    };
    [SerializeField] private DefectSettings settingsSpatter      = new();

    [Header("Materials")]
    [SerializeField] private Material materialNormal;
    [SerializeField] private Material materialPorosity;
    [SerializeField] private Material materialOverlap;
    [SerializeField] private Material materialLackOfFusion;
    [SerializeField] private Material materialSpatter;
    [Header("Defect State")]
    [SerializeField] private WeldDefect currentDefect = WeldDefect.Normal;

    [Header("Solidifying Trail")]
    [SerializeField] [Range(0.05f, 1f)] private float solidifyingTrailSpeed = 0.2f;

    // ── Mesh data ────────────────────────────────────────────────────
    private Mesh          _mesh;
    private List<Vector3> _vertices  = new();
    private List<Vector2> _uvs       = new();
    private List<Vector3> _normals   = new();
    private List<Color>   _colors    = new();
    private List<int>[]   _triangles = new List<int>[DEFECT_COUNT];

    // ── State ────────────────────────────────────────────────────────
    private int     _ringCount   = 0;
    private float   _totalLength = 0f;
    private Vector3 _prevRingPos;
    private Vector3 _prevNormal  = Vector3.up;
    private Vector3 _prevTangent = Vector3.forward;
    private float   _beadAge     = 0f;
    private bool    _isRecording = false;

    private int VertsPerRing => sidesPerRing + 1;

    private MeshCollider _meshCollider;
    private MeshRenderer _meshRenderer;
    private MaterialPropertyBlock _beadPropertyBlock;
    private Material[] _defaultMaterials;
    private bool _isDefectFilterActive;
    private WeldDefect _visibleDefectFilter;

    private void OnValidate()
    {
        sidesPerRing = Mathf.Max(3, sidesPerRing);
        tubeRadius = Mathf.Max(0.0001f, tubeRadius);
        beadWidth = Mathf.Max(0.01f, beadWidth);
        beadHeight = Mathf.Max(0.01f, beadHeight);
        minimumRingSpacing = Mathf.Max(0f, minimumRingSpacing);
        solidifyingTrailSpeed = Mathf.Clamp(solidifyingTrailSpeed, 0.05f, 1f);
        SanitizeDefectSettings(settingsNormal);
        SanitizeDefectSettings(settingsPorosity);
        SanitizeDefectSettings(settingsOverlap);
        SanitizeDefectSettings(settingsLackOfFusion);
        SanitizeDefectSettings(settingsSpatter);
    }


    // ────────────────────────────────────────────────────────────────
    private DefectSettings GetCurrentSettings()
    {
        return currentDefect switch
        {
            WeldDefect.Porosity     => settingsPorosity,
            WeldDefect.Overlap      => settingsOverlap,
            WeldDefect.LackOfFusion => settingsLackOfFusion,
            WeldDefect.Spatter      => settingsSpatter,
            _                       => settingsNormal
        };
    }

    private static void SanitizeDefectSettings(DefectSettings settings)
    {
        if (settings == null) return;

        settings.conformRayLength = Mathf.Clamp(settings.conformRayLength, 0.005f, MaxConformRayLength);
        settings.rayDirectionOffset = Mathf.Clamp(settings.rayDirectionOffset, 0f, MaxRayDirectionOffset);
        settings.snapMultiplier = Mathf.Clamp01(settings.snapMultiplier);
        settings.snapSharpness = Mathf.Max(0.01f, settings.snapSharpness);
    }

    // ────────────────────────────────────────────────────────────────
    void Awake()
    {
        OnValidate();

        
    _mesh = new Mesh { name = "WeldBead" };
    _mesh.MarkDynamic();
    GetComponent<MeshFilter>().mesh = _mesh;
    gameObject.layer = LayerMask.NameToLayer("Bead"); // ← add this back

    _meshCollider = gameObject.AddComponent<MeshCollider>();
    // Don't assign _mesh yet to avoid "no vertices" error
    _meshCollider.cookingOptions = MeshColliderCookingOptions.None;
    _meshRenderer = GetComponent<MeshRenderer>();
    _beadPropertyBlock = new MaterialPropertyBlock();

        for (int i = 0; i < DEFECT_COUNT; i++)
            _triangles[i] = new List<int>();

        _defaultMaterials = new Material[]
        {
            materialNormal,
            materialPorosity,
            materialOverlap,
            materialLackOfFusion,
            materialSpatter
        };

        _meshRenderer.materials = _defaultMaterials;
    }

    // ────────────────────────────────────────────────────────────────
    void Update()
    {
        _beadAge += Time.deltaTime * solidifyingTrailSpeed;

        if (_meshRenderer == null) return;
        if (_beadPropertyBlock == null)
            _beadPropertyBlock = new MaterialPropertyBlock();

        _meshRenderer.GetPropertyBlock(_beadPropertyBlock);
        _beadPropertyBlock.SetFloat(BeadAgePropertyId, _beadAge);
        _meshRenderer.SetPropertyBlock(_beadPropertyBlock);
    }

    // ────────────────────────────────────────────────────────────────
    public void StartAgeing()
    {
        _beadAge     = 0f;
        _isRecording = true;
    }

    public void StopAgeing()
    {
        _isRecording = false;
    }

    public void SetDefect(WeldDefect defect)
    {
        currentDefect = defect;
    }

    public void ShowOnlyDefect(WeldDefect defect)
    {
        _isDefectFilterActive = true;
        _visibleDefectFilter = defect;
        ApplyDefectVisibility();
    }

    public void ShowAllDefects()
    {
        _isDefectFilterActive = false;
        ApplyDefectVisibility();
    }

    public bool TryGetDefectBounds(WeldDefect defect, out Bounds bounds)
    {
        List<int> triangles = _triangles[(int)defect];
        if (triangles == null || triangles.Count == 0 || _vertices.Count == 0)
        {
            bounds = default;
            return false;
        }

        bool hasBounds = false;
        bounds = default;
        for (int i = 0; i < triangles.Count; i++)
        {
            int vertexIndex = triangles[i];
            if (vertexIndex < 0 || vertexIndex >= _vertices.Count)
                continue;

            Vector3 vertex = _vertices[vertexIndex];
            if (!hasBounds)
            {
                bounds = new Bounds(vertex, Vector3.zero);
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(vertex);
            }
        }

        return hasBounds;
    }

    // ────────────────────────────────────────────────────────────────
    public void OnKnotAdded(Vector3 worldPos, Vector3 worldTangent, Vector3 rayDir)
    {
        if (_ringCount > 0 && minimumRingSpacing > 0f)
        {
            float ringSpacingSq = (worldPos - _prevRingPos).sqrMagnitude;
            if (ringSpacingSq < minimumRingSpacing * minimumRingSpacing)
                return;
        }

        Vector3 tangent = worldTangent.sqrMagnitude > 1e-8f
            ? worldTangent.normalized
            : _prevTangent;
        if (tangent.sqrMagnitude < 1e-8f)
            tangent = Vector3.forward;

        Vector3 rayNorm = rayDir.sqrMagnitude > 1e-8f
            ? rayDir.normalized
            : -_prevNormal;
        if (rayNorm.sqrMagnitude < 1e-8f)
            rayNorm = Vector3.down;

        Vector3 rayUp = -rayNorm;

        Vector3 normalCandidate = Vector3.ProjectOnPlane(rayUp, tangent);
        Vector3 normal = normalCandidate.sqrMagnitude > 1e-8f
            ? normalCandidate.normalized
            : _prevNormal;
        if (normal.sqrMagnitude < 1e-8f)
            normal = Vector3.up;

        Vector3 binormal = Vector3.Cross(tangent, normal).normalized;
        if (binormal.sqrMagnitude < 1e-8f)
        {
            Vector3 fallbackAxis = Mathf.Abs(Vector3.Dot(tangent, Vector3.up)) < 0.9f
                ? Vector3.up
                : Vector3.right;
            normal = Vector3.ProjectOnPlane(fallbackAxis, tangent).normalized;
            binormal = Vector3.Cross(tangent, normal).normalized;
        }

        normal = Vector3.Cross(binormal, tangent).normalized;
        if (_ringCount > 0 && Vector3.Dot(normal, _prevNormal) < 0f)
        {
            normal = -normal;
            binormal = -binormal;
        }

        LastBinormal = binormal; 


        if (_ringCount > 0)
            _totalLength += Vector3.Distance(worldPos, _prevRingPos);

        int ringStart = _vertices.Count;

        DefectSettings s = GetCurrentSettings();
        GetVisualScale(currentDefect, out float widthScale, out float heightScale);
        GetCrackVisuals(currentDefect, s, out float crackThreshold, out float crackWidth, out float crackLift);

        // ── Push new ring of vertices ────────────────────────────────
        for (int i = 0; i <= sidesPerRing; i++)
        {
            int iWrapped = i % sidesPerRing;

            float angle      = (float)iWrapped / sidesPerRing * Mathf.PI * 2f;
            float cos        = Mathf.Cos(angle);
            float sin        = Mathf.Sin(angle);
            float crown      = 1f + s.crownAmount * cos;
            float horizontal = sin * beadWidth * widthScale;
            float vertical   = cos * beadHeight * heightScale * crown;

            Vector3 offset  = (binormal * horizontal + normal * vertical) * tubeRadius;
            Vector3 vertPos = worldPos + offset;

            // ── Conform along ray direction ───────────────────────────
            Vector3 rayOrigin  = vertPos + (-rayNorm) * s.conformRayLength;
            Ray     conformRay = new Ray(rayOrigin, rayNorm);

            if (Physics.Raycast(conformRay, out RaycastHit conformHit, s.conformRayLength * 2f, conformMask))
            {
                // Dot product between vertex offset and ray — camera independent
                Vector3 offsetDir  = offset.normalized;
                float   alignment  = Vector3.Dot(offsetDir, rayNorm);
                float   snapWeight = Mathf.Clamp01((alignment + 1f) * 0.5f);
                snapWeight = Mathf.Clamp01(Mathf.Pow(snapWeight, s.snapSharpness) * s.snapMultiplier);

                Vector3 snappedPos = conformHit.point + (-rayNorm) * s.rayDirectionOffset;
                vertPos = Vector3.Lerp(vertPos, snappedPos, snapWeight);
            }

            // ── Crack deformation ─────────────────────────────────────
            if (crackWidth > 0f || crackLift > 0f)
            {
                float bottomness  = Mathf.Clamp01((-cos - crackThreshold) / (1f - crackThreshold));
                float crackFactor = Mathf.SmoothStep(0f, 1f, bottomness);

                if (crackFactor > 0f)
                {
                    Vector3 toCenter    = (worldPos - vertPos).normalized;
                    Vector3 crackInward = toCenter   * (crackWidth * crackFactor);
                    Vector3 crackUp     = (-rayNorm) * (crackLift  * crackFactor);

                    vertPos += crackInward + crackUp;
                }
            }

            _vertices.Add(vertPos);
            _colors  .Add(new Color(_beadAge, 0f, 0f, 0f));
            _uvs     .Add(new Vector2(_totalLength, (float)i / sidesPerRing));
        }

        // ── Compute normals from actual final vertex positions ────────
        for (int i = 0; i <= sidesPerRing; i++)
        {
            int iWrapped = i % sidesPerRing;
            int prev     = (iWrapped - 1 + sidesPerRing) % sidesPerRing;
            int next     = (iWrapped + 1) % sidesPerRing;

            Vector3 edgePrev    = _vertices[ringStart + iWrapped] - _vertices[ringStart + prev];
            Vector3 edgeNext    = _vertices[ringStart + next]     - _vertices[ringStart + iWrapped];
            Vector3 ringTangent = (edgePrev + edgeNext).normalized;
            Vector3 vertNormal  = Vector3.Cross(ringTangent, tangent).normalized;

            _normals.Add(vertNormal);
        }

        // ── Connect to previous ring → into current defect submesh ───
        if (_ringCount > 0)
        {
            int prevRingStart = ringStart - VertsPerRing;
            List<int> tris    = _triangles[(int)currentDefect];

            for (int i = 0; i < sidesPerRing; i++)
            {
                int a = prevRingStart + i,     b = prevRingStart + i + 1;
                int c = ringStart     + i,     d = ringStart     + i + 1;

                tris.Add(a); tris.Add(c); tris.Add(b);
                tris.Add(b); tris.Add(c); tris.Add(d);
            }
        }

        UploadMesh();

        _prevNormal  = normal;
        _prevTangent = tangent;
        _prevRingPos = worldPos;
        _ringCount++;

        if (_ringCount == 1)
            CapWithFan(true);
    }

    private void GetVisualScale(WeldDefect defect, out float widthScale, out float heightScale)
    {
        switch (defect)
        {
            case WeldDefect.Overlap:
                widthScale = 1.9f;
                heightScale = 0.62f;
                return;

            case WeldDefect.LackOfFusion:
                widthScale = 0.72f;
                heightScale = 0.58f;
                return;

            default:
                widthScale = 1f;
                heightScale = 1f;
                return;
        }
    }

    private void GetCrackVisuals(
        WeldDefect defect,
        DefectSettings settings,
        out float crackThreshold,
        out float crackWidth,
        out float crackLift)
    {
        crackThreshold = settings.crackThreshold;
        crackWidth = settings.crackWidth;
        crackLift = settings.crackLift;

        if (defect != WeldDefect.LackOfFusion)
            return;

        crackThreshold = Mathf.Min(crackThreshold, 0.1f);
        crackWidth = Mathf.Max(crackWidth, tubeRadius * 0.35f);
        crackLift = Mathf.Max(crackLift, tubeRadius * 0.16f);
    }

    // ────────────────────────────────────────────────────────────────
    private void UploadMesh()
    {
        _mesh.SetVertices(_vertices);
        _mesh.SetNormals (_normals);
        _mesh.SetColors  (_colors);
        _mesh.SetUVs     (0, _uvs);

        _mesh.subMeshCount = DEFECT_COUNT;
        ApplyDefectVisibility();

        _mesh.RecalculateBounds();
        _mesh.RecalculateNormals();

        // ── Force physics collider update ────────────────────────────
        if (_meshCollider != null)
        {
            bool hasTriangles = false;
            for (int i = 0; i < DEFECT_COUNT; i++)
            {
                if (_triangles[i].Count >= 3)
                {
                    hasTriangles = true;
                    break;
                }
            }

            if (hasTriangles)
            {
                _meshCollider.sharedMesh = null;
                _meshCollider.sharedMesh = _mesh; // reassign forces immediate cook
            }
        }
        }

    private void ApplyDefectVisibility()
    {
        if (_mesh == null)
            return;

        _mesh.subMeshCount = DEFECT_COUNT;
        for (int i = 0; i < DEFECT_COUNT; i++)
        {
            bool shouldShow = !_isDefectFilterActive || i == (int)_visibleDefectFilter;
            if (shouldShow)
                _mesh.SetTriangles(_triangles[i], i);
            else
                _mesh.SetTriangles(System.Array.Empty<int>(), i);
        }

        _mesh.RecalculateBounds();
    }

    // ────────────────────────────────────────────────────────────────
public void ResetMesh(Vector3 rayDir)
{
    _vertices.Clear();
    _uvs     .Clear();
    _normals .Clear();
    _colors  .Clear();

    for (int i = 0; i < DEFECT_COUNT; i++)
        _triangles[i].Clear();

    _mesh.Clear();

    _ringCount      = 0;
    _totalLength    = 0f;
    _prevTangent    = Vector3.forward;
    Vector3 rayNorm = rayDir.sqrMagnitude > 1e-8f ? rayDir.normalized : Vector3.down;
    _prevNormal     = -rayNorm;
    _startCapAdded  = false; // ← reset
}


    // ────────────────────────────────────────────────────────────────
// Add this field
private bool _startCapAdded = false;

public void CapWithFan(bool isStart)
{
    if (_ringCount < 1) return;

    // ── Correct ring start accounting for start cap vertex ────────
    int ringStart;
    if (isStart)
    {
        ringStart = 0;
    }
    else
    {
        // If start cap was added, it inserted one extra vertex before the rings
        int extraVerts = _startCapAdded ? 1 : 0;
        ringStart = _vertices.Count - VertsPerRing - extraVerts;
    }

    // ── Center of the ring ────────────────────────────────────────
    Vector3 center = Vector3.zero;
    for (int i = 0; i < sidesPerRing; i++)
        center += _vertices[ringStart + i];
    center /= sidesPerRing;

    // ── Cap normal — use actual tangent at this end ───────────────
    // For start cap: _prevTangent was set by first OnKnotAdded call
    // For end cap:   _prevTangent is the last tangent
    Vector3 capNormal = isStart ? -_prevTangent : _prevTangent;

    int centerIndex = _vertices.Count;
    _vertices.Add(center);
    _normals .Add(capNormal);
    _colors  .Add(new Color(_beadAge, 0f, 0f, 0f));
    _uvs     .Add(new Vector2(_totalLength, 0.5f));

    List<int> tris = _triangles[(int)currentDefect];

    for (int i = 0; i < sidesPerRing; i++)
    {
        int next = i + 1;

        int idxA = centerIndex;
        int idxB = ringStart + i;
        int idxC = ringStart + next;

        // Verify winding against cap normal
        Vector3 faceNormal = Vector3.Cross(
            _vertices[idxB] - _vertices[idxA],
            _vertices[idxC] - _vertices[idxA]
        );

        if (Vector3.Dot(faceNormal, capNormal) >= 0f)
        {
            tris.Add(idxA); tris.Add(idxB); tris.Add(idxC);
        }
        else
        {
            tris.Add(idxA); tris.Add(idxC); tris.Add(idxB);
        }
    }

    if (isStart) _startCapAdded = true;

    UploadMesh();
}
    public Vector3 LastBinormal { get; private set; }

}
