using UnityEngine;
using System.Collections.Generic;

public class WeldSpatter : MonoBehaviour
{
    [Header("Spatter Mesh & Material")]
    [SerializeField] private Mesh     spatterMesh;
    [SerializeField] private Material spatterMaterial;

    [Header("Spawn Settings")]
    [SerializeField] private int   maxSpatter       = 512;
    [SerializeField] private int   maxPerKnot       = 3;
    [SerializeField] private int   minimumPerKnot   = 1;
    [SerializeField] private float spawnChance      = 0.3f;
    [SerializeField] private float spawnDistance    = 0.01f;
    [SerializeField] private float conformRayLength = 0.02f;
    [SerializeField] private float sinkAmount       = 0f;

    [Header("Size Settings")]
    [SerializeField] private float minRadius = 0.005f;
    [SerializeField] private float maxRadius = 0.02f;

    [Header("Layer Settings")]
    [SerializeField] private LayerMask spatterMask;

    [Header("Solidifying Trail")]
    [SerializeField] [Range(0.05f, 1f)] private float solidifyingTrailSpeed = 0.0714f;

    // ── Internal state ────────────────────────────────────────────────
    private List<Matrix4x4> _matrices  = new();
    private List<float>     _birthAges = new();
    private List<Matrix4x4> _chunk     = new(1023);
    private float[]         _ageBuffer = new float[1023];

    private float _beadAge = 0f;
    private MaterialPropertyBlock _block;
    private bool _isEnabled = true;

    private static readonly int BeadAgePropertyId    = Shader.PropertyToID("_BeadAge");
    private static readonly int BirthTimePropertyId = Shader.PropertyToID("_SpatterBirthTime");

    // ────────────────────────────────────────────────────────────────
    void Awake()
    {
        if (spatterMesh == null)
            spatterMesh = Resources.GetBuiltinResource<Mesh>("Sphere.fbx");
        
        _block = new MaterialPropertyBlock();
    }

    // ────────────────────────────────────────────────────────────────
public void OnKnotAdded(Vector3 worldPos, Vector3 worldTangent, Vector3 rayDir, Vector3 binormal)
{
    if (!_isEnabled || _matrices.Count >= maxSpatter) return;

    Vector3 rayNorm = rayDir.normalized;
    Vector3 tangent = worldTangent.normalized;

    // ── Step 1: Find ALL surfaces under bead ─────────────────────
    Vector3      origin  = worldPos + (-rayNorm) * conformRayLength;
    RaycastHit[] allHits = Physics.RaycastAll(origin, rayNorm, conformRayLength * 2f, spatterMask);

    if (allHits.Length == 0) return;

    // ── Step 2: Scatter blobs ─────────────────────────────────────
    int spawnedThisKnot = 0;
    for (int attempt = 0; attempt < maxPerKnot; attempt++)
    {
        if (_matrices.Count >= maxSpatter) break;
        if (Random.value     > spawnChance) continue;

        // Pick a random surface from all hits
        RaycastHit baseHit      = allHits[Random.Range(0, allHits.Length)];
        Vector3    surfaceNormal = baseHit.normal;
        Vector3    surfaceRight  = Vector3.Cross(surfaceNormal, tangent).normalized;
        Vector3    surfaceForward = Vector3.Cross(surfaceRight, surfaceNormal).normalized;

        // Random position in surface plane
        float   angle      = Random.Range(0f, Mathf.PI * 2f);
        float   dist       = Random.Range(0f, spawnDistance);
        Vector3 scatterPos = baseHit.point
                           + surfaceRight   * Mathf.Cos(angle) * dist
                           + surfaceForward * Mathf.Sin(angle) * dist;

        // ── Step 3: Confirm placement on same surface ─────────────
        Vector3 castOrigin = scatterPos + surfaceNormal * conformRayLength;

        if (!Physics.Raycast(castOrigin, -surfaceNormal, out RaycastHit placeHit, conformRayLength * 2f, spatterMask))
            continue;

        PlaceBlob(placeHit.point, placeHit.normal);
        spawnedThisKnot++;
    }

    int guaranteedCount = Mathf.Max(0, minimumPerKnot);
    while (spawnedThisKnot < guaranteedCount && _matrices.Count < maxSpatter)
    {
        RaycastHit baseHit = allHits[spawnedThisKnot % allHits.Length];
        PlaceBlob(baseHit.point, baseHit.normal);
        spawnedThisKnot++;
    }
}

    // ────────────────────────────────────────────────────────────────
    private void PlaceBlob(Vector3 point, Vector3 normal)
    {
        float      scale    = Random.Range(minRadius, maxRadius);
        float      offset   = scale * (0.5f - sinkAmount);
        Vector3    pos      = point + normal * offset;
        Quaternion rotation = Quaternion.FromToRotation(Vector3.up, normal)
                            * Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

        _matrices.Add(Matrix4x4.TRS(pos, rotation, Vector3.one * scale));
        _birthAges.Add(_beadAge);
    }

    // ────────────────────────────────────────────────────────────────
    void Update()
    {
        _beadAge += Time.deltaTime * solidifyingTrailSpeed;

        if (!_isEnabled) return;
        if (_matrices.Count == 0)    return;
        if (spatterMesh     == null) return;
        if (spatterMaterial == null) return;

        for (int i = 0; i < _matrices.Count; i += 1023)
        {
            int count = Mathf.Min(1023, _matrices.Count - i);
            _chunk.Clear();
            
            for (int j = 0; j < count; j++)
            {
                _chunk.Add(_matrices[i + j]);
                _ageBuffer[j] = _birthAges[i + j];
            }

            _block.Clear();
            _block.SetFloat(BeadAgePropertyId, _beadAge);
            
            float[] slice = new float[count];
            System.Array.Copy(_ageBuffer, 0, slice, 0, count);
            _block.SetFloatArray(BirthTimePropertyId, slice);

            Graphics.DrawMeshInstanced(spatterMesh, 0, spatterMaterial, _chunk, _block);
        }
    }

    // ────────────────────────────────────────────────────────────────
    public void ResetSpatter()
    {
        _matrices.Clear();
        _birthAges.Clear();
        _beadAge = 0f;
    }
public void EnableSpatter(bool enabled)
{
    _isEnabled = enabled;
}
}
