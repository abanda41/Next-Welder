using UnityEngine;

public class WeldDefectEvaluator : MonoBehaviour
{
    [SerializeField] private WeldParameterMonitor parameterMonitor;

    private void Awake()
    {
        ResolveParameterMonitor();
    }

    public WeldQualityEvaluation EvaluateCurrent()
    {
        ResolveParameterMonitor();

        WeldJointType jointType = WeldJointSelectionState.GetSelectedOrDefault();
        WeldQualityProfile profile = WeldQualityTable.GetProfile(jointType);

        float speed = parameterMonitor != null ? parameterMonitor.TravelSpeedMmPerSec : 0f;
        float workAngle = parameterMonitor != null ? parameterMonitor.WorkAngleDeg : 0f;
        float travelAngle = parameterMonitor != null ? parameterMonitor.TravelAngleDeg : 0f;
        float ctwd = parameterMonitor != null ? parameterMonitor.StickoutDistanceMm : 0f;

        bool speedInRange = profile.IsSpeedNormal(speed);
        bool workAngleInRange = profile.IsWorkAngleNormal(workAngle);
        bool travelAngleInRange = profile.IsTravelAngleNormal(travelAngle);
        bool ctwdInRange = profile.IsCtwdNormal(ctwd);

        return new WeldQualityEvaluation
        {
            JointType = jointType,
            Profile = profile,
            Defect = EvaluateDefect(
                profile,
                speed,
                workAngle,
                travelAngle,
                ctwd,
                speedInRange,
                workAngleInRange,
                travelAngleInRange,
                ctwdInRange),
            TravelSpeedMmPerSec = speed,
            WorkAngleDeg = workAngle,
            TravelAngleDeg = travelAngle,
            CtwdMm = ctwd,
            SpeedInRange = speedInRange,
            WorkAngleInRange = workAngleInRange,
            TravelAngleInRange = travelAngleInRange,
            CtwdInRange = ctwdInRange
        };
    }

    private static TubeMeshBuilder.WeldDefect EvaluateDefect(
        WeldQualityProfile profile,
        float speed,
        float workAngle,
        float travelAngle,
        float ctwd,
        bool speedInRange,
        bool workAngleInRange,
        bool travelAngleInRange,
        bool ctwdInRange)
    {
        if (speedInRange && workAngleInRange && travelAngleInRange && ctwdInRange)
            return TubeMeshBuilder.WeldDefect.Normal;

        // Travel speed is the clean separator between overlap and lack of fusion
        // in the validated table, so it decides between those two visual defects.
        if (speed < profile.OverlapSpeedMax)
            return TubeMeshBuilder.WeldDefect.Overlap;

        if (speed > profile.LackOfFusionSpeedMin)
            return TubeMeshBuilder.WeldDefect.LackOfFusion;

        // Arc length is intentionally ignored because the current simulator does not
        // measure it. The remaining porosity conditions still come directly from
        // the validated table.
        if (speedInRange &&
            workAngleInRange &&
            travelAngle > profile.PorosityTravelAngleMin &&
            ctwd > profile.PorosityCtwdMin)
        {
            return TubeMeshBuilder.WeldDefect.Porosity;
        }

        if (travelAngle > profile.SpatterTravelAngleMin ||
            ctwd < profile.SpatterCtwdMin ||
            ctwd > profile.SpatterCtwdMax)
        {
            return TubeMeshBuilder.WeldDefect.Spatter;
        }

        return TubeMeshBuilder.WeldDefect.Normal;
    }

    private void ResolveParameterMonitor()
    {
        if (parameterMonitor == null)
            parameterMonitor = GetComponent<WeldParameterMonitor>();
    }
}
