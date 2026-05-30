using UnityEngine;

public enum WeldJointType
{
    CornerJoint,
    TeeJoint,
    ButtJoint,
    EdgeJoint
}

public static class WeldJointCatalog
{
    public static readonly WeldJointType[] OrderedTypes =
    {
        WeldJointType.CornerJoint,
        WeldJointType.TeeJoint,
        WeldJointType.ButtJoint,
        WeldJointType.EdgeJoint
    };

    public static string GetLabel(WeldJointType jointType)
    {
        switch (jointType)
        {
            case WeldJointType.CornerJoint:
                return "Corner Joint";
            case WeldJointType.TeeJoint:
                return "Tee Joint";
            case WeldJointType.ButtJoint:
                return "Butt Joint";
            case WeldJointType.EdgeJoint:
                return "Edge Joint";
            default:
                return jointType.ToString();
        }
    }

    public static string GetSceneObjectName(WeldJointType jointType)
    {
        switch (jointType)
        {
            case WeldJointType.CornerJoint:
                return "Corner joint";
            case WeldJointType.TeeJoint:
                return "Tee joint";
            case WeldJointType.ButtJoint:
                return "Buttjoint";
            case WeldJointType.EdgeJoint:
                return "Edgejoint";
            default:
                return GetLabel(jointType);
        }
    }

    public static string GetResourcePath(WeldJointType jointType)
    {
        return "Joints/" + GetSceneObjectName(jointType);
    }

    public static Quaternion GetRotationOffset(WeldJointType jointType)
    {
        switch (jointType)
        {
            case WeldJointType.CornerJoint:
                return Quaternion.Euler(0f, 180f, 0f);
            case WeldJointType.TeeJoint:
                return Quaternion.Euler(0f, 180f, 0f);
            case WeldJointType.ButtJoint:
                return Quaternion.Euler(0f, 90f, 0f);
            case WeldJointType.EdgeJoint:
                return Quaternion.Euler(0f, -90f, 0f);
            default:
                return Quaternion.identity;
        }
    }
}

public static class WeldJointSelectionState
{
    public static bool HasSelection { get; private set; }

    public static WeldJointType SelectedType { get; private set; } = WeldJointType.TeeJoint;

    public static void SetSelection(WeldJointType jointType)
    {
        SelectedType = jointType;
        HasSelection = true;
    }

    public static WeldJointType GetSelectedOrDefault()
    {
        return SelectedType;
    }
}
