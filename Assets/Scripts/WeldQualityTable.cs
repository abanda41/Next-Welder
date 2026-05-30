using UnityEngine;

/// <summary>
/// Source-of-truth ranges copied from the validated weld-parameter table.
/// Arc length is intentionally omitted because the simulator does not track it yet.
/// </summary>
public struct WeldQualityProfile
{
    public WeldJointType JointType;
    public float NormalSpeedMin;
    public float NormalSpeedMax;
    public float NormalWorkAngleMin;
    public float NormalWorkAngleMax;
    public float NormalTravelAngleMin;
    public float NormalTravelAngleMax;
    public float NormalCtwdMin;
    public float NormalCtwdMax;
    public float OverlapSpeedMax;
    public float LackOfFusionSpeedMin;
    public float PorosityTravelAngleMin;
    public float PorosityCtwdMin;
    public float SpatterTravelAngleMin;
    public float SpatterCtwdMin;
    public float SpatterCtwdMax;

    public bool IsSpeedNormal(float value)
    {
        return IsInside(value, NormalSpeedMin, NormalSpeedMax);
    }

    public bool IsWorkAngleNormal(float value)
    {
        return IsInside(value, NormalWorkAngleMin, NormalWorkAngleMax);
    }

    public bool IsTravelAngleNormal(float value)
    {
        return IsInside(value, NormalTravelAngleMin, NormalTravelAngleMax);
    }

    public bool IsCtwdNormal(float value)
    {
        return IsInside(value, NormalCtwdMin, NormalCtwdMax);
    }

    private static bool IsInside(float value, float min, float max)
    {
        return value >= min && value <= max;
    }
}

public struct WeldQualityEvaluation
{
    public WeldJointType JointType;
    public WeldQualityProfile Profile;
    public TubeMeshBuilder.WeldDefect Defect;
    public float TravelSpeedMmPerSec;
    public float WorkAngleDeg;
    public float TravelAngleDeg;
    public float CtwdMm;
    public bool SpeedInRange;
    public bool WorkAngleInRange;
    public bool TravelAngleInRange;
    public bool CtwdInRange;
}

public static class WeldQualityTable
{
    public static WeldQualityProfile GetProfile(WeldJointType jointType)
    {
        switch (jointType)
        {
            case WeldJointType.TeeJoint:
            case WeldJointType.CornerJoint:
                return CreateProfile(
                    jointType,
                    normalSpeedMin: 5.2f,
                    normalSpeedMax: 6.4f,
                    normalWorkAngleMin: 40f,
                    normalWorkAngleMax: 50f,
                    overlapSpeedMax: 4.0f,
                    lackOfFusionSpeedMin: 7.5f);

            case WeldJointType.ButtJoint:
                return CreateProfile(
                    jointType,
                    normalSpeedMin: 4.2f,
                    normalSpeedMax: 6.8f,
                    normalWorkAngleMin: 85f,
                    normalWorkAngleMax: 95f,
                    overlapSpeedMax: 3.5f,
                    lackOfFusionSpeedMin: 8.0f);

            case WeldJointType.EdgeJoint:
                return CreateProfile(
                    jointType,
                    normalSpeedMin: 5.0f,
                    normalSpeedMax: 6.8f,
                    normalWorkAngleMin: 85f,
                    normalWorkAngleMax: 95f,
                    overlapSpeedMax: 3.5f,
                    lackOfFusionSpeedMin: 8.0f);

            default:
                return CreateProfile(
                    WeldJointType.TeeJoint,
                    normalSpeedMin: 5.2f,
                    normalSpeedMax: 6.4f,
                    normalWorkAngleMin: 40f,
                    normalWorkAngleMax: 50f,
                    overlapSpeedMax: 4.0f,
                    lackOfFusionSpeedMin: 7.5f);
        }
    }

    private static WeldQualityProfile CreateProfile(
        WeldJointType jointType,
        float normalSpeedMin,
        float normalSpeedMax,
        float normalWorkAngleMin,
        float normalWorkAngleMax,
        float overlapSpeedMax,
        float lackOfFusionSpeedMin)
    {
        return new WeldQualityProfile
        {
            JointType = jointType,
            NormalSpeedMin = normalSpeedMin,
            NormalSpeedMax = normalSpeedMax,
            NormalWorkAngleMin = normalWorkAngleMin,
            NormalWorkAngleMax = normalWorkAngleMax,
            NormalTravelAngleMin = 5f,
            NormalTravelAngleMax = 15f,
            NormalCtwdMin = 10f,
            NormalCtwdMax = 12f,
            OverlapSpeedMax = overlapSpeedMax,
            LackOfFusionSpeedMin = lackOfFusionSpeedMin,
            PorosityTravelAngleMin = 25f,
            PorosityCtwdMin = 16f,
            SpatterTravelAngleMin = 20f,
            SpatterCtwdMin = 8f,
            SpatterCtwdMax = 16f
        };
    }
}
