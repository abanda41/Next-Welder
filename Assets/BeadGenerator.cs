using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Spawns weld bead segments while the torch is near any object on the Weldable layer.
/// Activated by WeldingTorchController via BeginBead / EndBead / UpdateBeadAtPosition.
/// </summary>
public class BeadGenerator : MonoBehaviour
{
    [Header("Bead Settings")]
    public GameObject beadSegmentPrefab;
    public float segmentSpacing = 0.005f;

    [Header("Bead Material")]
    public Material beadMat;

    [Header("Welding Gate")]
    public LayerMask weldableLayers;
    public float weldRadius = 0.03f;
    public float surfaceOffset = 0.001f;

    [Header("Physics Safety")]
    public string beadLayerName = "Bead";
    public bool removeSpawnedColliders = true;

    private Vector3 lastWeldPoint;
    private bool hasLastPoint;
    private bool weldingActive;

    private readonly List<GameObject> placedSegments = new();
    private int beadLayer;
    private readonly Collider[] hits = new Collider[16];

    private void Awake()
    {
        beadLayer = LayerMask.NameToLayer(beadLayerName);
        if (beadLayer == -1)
            Debug.LogWarning($"BeadGenerator: Layer '{beadLayerName}' not found. Create it in Tags & Layers.");

        if (weldableLayers.value == 0)
            Debug.LogWarning("BeadGenerator: Weldable Layers is set to Nothing. Assign your Weldable layer in the Inspector.");
    }

    public void BeginBead()
    {
        weldingActive = true;
        hasLastPoint = false;
        placedSegments.Clear();
    }

    public void EndBead()
    {
        weldingActive = false;
    }

    /// <summary>
    /// Updates the active bead and returns true while the torch is close enough
    /// to a weldable surface to place beads.
    /// </summary>
    public bool UpdateBeadAtPosition(Vector3 torchPos, Vector3 torchForward)
    {
        if (!weldingActive || beadSegmentPrefab == null)
            return false;

        int count = Physics.OverlapSphereNonAlloc(
            torchPos,
            weldRadius,
            hits,
            weldableLayers,
            QueryTriggerInteraction.Ignore);

        if (count == 0)
            return false;

        Vector3 closestPoint = default;
        float bestDist = float.PositiveInfinity;

        for (int i = 0; i < count; i++)
        {
            Collider hit = hits[i];
            if (hit == null)
                continue;

            Vector3 point = hit.ClosestPoint(torchPos);
            float distance = (point - torchPos).sqrMagnitude;
            if (distance < bestDist)
            {
                bestDist = distance;
                closestPoint = point;
            }
        }

        Vector3 approxNormal = torchPos - closestPoint;
        if (approxNormal.sqrMagnitude < 1e-8f)
            approxNormal = Vector3.up;

        approxNormal.Normalize();
        Vector3 weldPoint = closestPoint + approxNormal * surfaceOffset;

        if (!hasLastPoint)
        {
            lastWeldPoint = weldPoint;
            hasLastPoint = true;
            return true;
        }

        if (Vector3.Distance(weldPoint, lastWeldPoint) < segmentSpacing)
            return true;

        GameObject segment = Instantiate(beadSegmentPrefab, weldPoint, Quaternion.identity);

        if (beadLayer != -1)
        {
            foreach (Transform t in segment.GetComponentsInChildren<Transform>(true))
                t.gameObject.layer = beadLayer;
        }

        if (removeSpawnedColliders)
        {
            foreach (Collider col in segment.GetComponentsInChildren<Collider>(true))
                Destroy(col);

            Rigidbody rb = segment.GetComponent<Rigidbody>();
            if (rb)
                Destroy(rb);
        }

        Renderer rend = segment.GetComponentInChildren<Renderer>();
        if (rend != null && beadMat != null)
            rend.sharedMaterial = beadMat;

        placedSegments.Add(segment);
        lastWeldPoint = weldPoint;
        return true;
    }
}
