using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Defines the expected weld seam path for a joint.
/// Add this component (and its child waypoint Transforms) to each joint prefab.
///
/// Usage in prefab hierarchy:
///   JointRoot  (WeldableJoint, WeldPath)
///     ├─ ...mesh children...
///     └─ WeldPath
///          ├─ Waypoint_0   ← first point of seam (local space)
///          ├─ Waypoint_1
///          └─ Waypoint_N   ← last point
///
/// Waypoints are placed at the joint's weld seam in LOCAL space so they
/// move correctly when the joint is rotated/scaled by the bootstrap.
///
/// For Tee joint:   two waypoints at either end of the vertical-plate base.
/// For Butt joint:  two waypoints at either end of the gap between plates.
/// For Corner joint: two waypoints following the corner seam.
/// For Edge joint:  two waypoints at either end of the edge.
/// </summary>
public class WeldPath : MonoBehaviour
{
    [Tooltip("Ordered list of waypoint Transforms that define the weld seam. "
           + "Assign child Transforms here (or let Awake auto-populate from children).")]
    public List<Transform> waypoints = new();

    [Tooltip("Half-width of the valid weld zone around the path (in metres). "
           + "Useful for HUD 'on path' feedback.")]
    public float pathTolerance = 0.06f;

    // ────────────────────────────────────────────────────────────────
    private void Awake()
    {
        // Auto-populate from child transforms if none assigned
        if (waypoints == null || waypoints.Count == 0)
        {
            waypoints = new List<Transform>();
            foreach (Transform child in transform)
                waypoints.Add(child);
        }
    }

    // ────────────────────────────────────────────────────────────────
    /// <summary>
    /// Returns the closest point (world space) on the piecewise-linear path
    /// to the given world position.
    /// </summary>
    public Vector3 GetClosestPointOnPath(Vector3 worldPos)
    {
        if (waypoints == null || waypoints.Count == 0)
            return transform.position;

        if (waypoints.Count == 1)
            return waypoints[0].position;

        float   bestSqDist = float.PositiveInfinity;
        Vector3 bestPoint  = waypoints[0].position;

        for (int i = 0; i < waypoints.Count - 1; i++)
        {
            if (waypoints[i] == null || waypoints[i + 1] == null) continue;

            Vector3 closest = ClosestPointOnSegment(
                waypoints[i].position,
                waypoints[i + 1].position,
                worldPos);

            float sqDist = (closest - worldPos).sqrMagnitude;
            if (sqDist < bestSqDist)
            {
                bestSqDist = sqDist;
                bestPoint  = closest;
            }
        }

        return bestPoint;
    }

    // ────────────────────────────────────────────────────────────────
    /// <summary>Returns true if worldPos is within pathTolerance of the seam.</summary>
    public bool IsNearPath(Vector3 worldPos)
    {
        Vector3 closest = GetClosestPointOnPath(worldPos);
        return (closest - worldPos).sqrMagnitude <= pathTolerance * pathTolerance;
    }

    // ────────────────────────────────────────────────────────────────
    /// <summary>Distance from worldPos to the closest point on the seam.</summary>
    public float DistanceToPath(Vector3 worldPos)
    {
        return Vector3.Distance(worldPos, GetClosestPointOnPath(worldPos));
    }

    // ────────────────────────────────────────────────────────────────
    private static Vector3 ClosestPointOnSegment(Vector3 a, Vector3 b, Vector3 p)
    {
        Vector3 ab = b - a;
        float   t  = Vector3.Dot(p - a, ab);
        float   d  = ab.sqrMagnitude;
        if (d < 1e-8f) return a;
        t = Mathf.Clamp01(t / d);
        return a + ab * t;
    }

    // ────────────────────────────────────────────────────────────────
#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (waypoints == null || waypoints.Count < 2) return;

        Gizmos.color = Color.red;
        for (int i = 0; i < waypoints.Count - 1; i++)
        {
            if (waypoints[i] == null || waypoints[i + 1] == null) continue;
            Gizmos.DrawLine(waypoints[i].position, waypoints[i + 1].position);
        }

        Gizmos.color = new Color(1f, 0.3f, 0.3f, 0.8f);
        foreach (var wp in waypoints)
        {
            if (wp != null)
                Gizmos.DrawWireSphere(wp.position, pathTolerance * 0.5f);
        }
    }
#endif
}
