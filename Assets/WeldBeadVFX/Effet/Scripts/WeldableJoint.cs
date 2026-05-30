using UnityEngine;

/// <summary>
/// Marker component placed on the root of every joint prefab.
/// It carries the seam path and the plate-normal references used by the
/// measurement system when welding in VR.
/// </summary>
[DisallowMultipleComponent]
public class WeldableJoint : MonoBehaviour
{
    [Tooltip("Optional reference to this joint's defined weld path. Used for HUD path feedback.")]
    public WeldPath weldPath;

    [Header("Work Angle References")]
    [Tooltip("Primary plate reference. Its blue local Z axis should point away from that plate.")]
    public Transform workAnglePlateA;

    [Tooltip("Second plate reference for Tee/Corner joints. Leave empty for Butt/Edge joints.")]
    public Transform workAnglePlateB;

    private void Awake()
    {
        if (weldPath == null)
            weldPath = GetComponentInChildren<WeldPath>(true);

        ResolveWorkAngleReferences();
    }

    public void ResolveWorkAngleReferences()
    {
        if (workAnglePlateA == null)
            workAnglePlateA = FindChildByName("WorkAngle_PlateA");

        if (workAnglePlateB == null)
            workAnglePlateB = FindChildByName("WorkAngle_PlateB");
    }

    private Transform FindChildByName(string childName)
    {
        Transform[] children = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            Transform child = children[i];
            if (child != null && child.name == childName)
                return child;
        }

        return null;
    }
}
