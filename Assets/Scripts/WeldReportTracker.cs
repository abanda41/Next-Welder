using System.Text;
using System.Collections.Generic;
using UnityEngine;

public class WeldReportTracker : MonoBehaviour
{
    public struct ReportIssueSummary
    {
        public string Label;
        public string Reason;
        public float Percent;
    }

    private const int DefectCount = 5;

    private readonly float[] defectDurations = new float[DefectCount];

    private bool hasSamples;
    private WeldJointType trackedJointType;
    private WeldQualityProfile trackedProfile;
    private float totalWeldTime;
    private float speedWeightedSum;
    private float workAngleWeightedSum;
    private float travelAngleWeightedSum;
    private float ctwdWeightedSum;
    private float speedBelowRangeTime;
    private float speedAboveRangeTime;
    private float workAngleBelowRangeTime;
    private float workAngleAboveRangeTime;
    private float travelAngleBelowRangeTime;
    private float travelAngleAboveRangeTime;
    private float ctwdBelowRangeTime;
    private float ctwdAboveRangeTime;

    public bool HasSamples => hasSamples;
    public WeldJointType TrackedJointType => trackedJointType;
    public WeldQualityProfile TrackedProfile => trackedProfile;
    public float TotalWeldTime => totalWeldTime;
    public float AverageSpeedMmPerSec => GetAverageSpeed();
    public float AverageWorkAngleDeg => GetAverageWorkAngle();
    public float AverageTravelAngleDeg => GetAverageTravelAngle();
    public float AverageCtwdMm => GetAverageCtwd();
    public TubeMeshBuilder.WeldDefect DominantDefect => GetDominantDefect();

    public void RecordSample(WeldQualityEvaluation evaluation, float deltaTime)
    {
        float dt = Mathf.Max(0f, deltaTime);
        if (dt <= 0f)
            return;

        if (!hasSamples)
        {
            trackedJointType = evaluation.JointType;
            trackedProfile = evaluation.Profile;
            hasSamples = true;
        }

        totalWeldTime += dt;
        speedWeightedSum += evaluation.TravelSpeedMmPerSec * dt;
        workAngleWeightedSum += evaluation.WorkAngleDeg * dt;
        travelAngleWeightedSum += evaluation.TravelAngleDeg * dt;
        ctwdWeightedSum += evaluation.CtwdMm * dt;

        AccumulateRangeViolation(
            evaluation.TravelSpeedMmPerSec,
            trackedProfile.NormalSpeedMin,
            trackedProfile.NormalSpeedMax,
            dt,
            ref speedBelowRangeTime,
            ref speedAboveRangeTime);

        AccumulateRangeViolation(
            evaluation.WorkAngleDeg,
            trackedProfile.NormalWorkAngleMin,
            trackedProfile.NormalWorkAngleMax,
            dt,
            ref workAngleBelowRangeTime,
            ref workAngleAboveRangeTime);

        AccumulateRangeViolation(
            evaluation.TravelAngleDeg,
            trackedProfile.NormalTravelAngleMin,
            trackedProfile.NormalTravelAngleMax,
            dt,
            ref travelAngleBelowRangeTime,
            ref travelAngleAboveRangeTime);

        AccumulateRangeViolation(
            evaluation.CtwdMm,
            trackedProfile.NormalCtwdMin,
            trackedProfile.NormalCtwdMax,
            dt,
            ref ctwdBelowRangeTime,
            ref ctwdAboveRangeTime);

        int defectIndex = Mathf.Clamp((int)evaluation.Defect, 0, DefectCount - 1);
        defectDurations[defectIndex] += dt;
    }

    public void ResetReport()
    {
        hasSamples = false;
        totalWeldTime = 0f;
        speedWeightedSum = 0f;
        workAngleWeightedSum = 0f;
        travelAngleWeightedSum = 0f;
        ctwdWeightedSum = 0f;
        speedBelowRangeTime = 0f;
        speedAboveRangeTime = 0f;
        workAngleBelowRangeTime = 0f;
        workAngleAboveRangeTime = 0f;
        travelAngleBelowRangeTime = 0f;
        travelAngleAboveRangeTime = 0f;
        ctwdBelowRangeTime = 0f;
        ctwdAboveRangeTime = 0f;

        for (int i = 0; i < defectDurations.Length; i++)
            defectDurations[i] = 0f;
    }

    public string BuildReportText()
    {
        if (!hasSamples || totalWeldTime <= 0f)
            return "No weld data has been recorded yet.";

        float averageSpeed = speedWeightedSum / totalWeldTime;
        float averageWorkAngle = workAngleWeightedSum / totalWeldTime;
        float averageTravelAngle = travelAngleWeightedSum / totalWeldTime;
        float averageCtwd = ctwdWeightedSum / totalWeldTime;
        TubeMeshBuilder.WeldDefect dominantDefect = GetDominantDefect();

        StringBuilder report = new StringBuilder(768);
        report.AppendLine("Joint: " + WeldJointCatalog.GetLabel(trackedJointType));
        report.AppendLine("Dominant defect: " + GetDefectLabel(dominantDefect));
        report.AppendLine("Tracked weld time: " + totalWeldTime.ToString("0.0") + " s");
        report.AppendLine();
        report.AppendLine("Average values");
        report.AppendLine(
            "- Travel speed: " + averageSpeed.ToString("0.0") + " mm/s  | target " +
            FormatRange(trackedProfile.NormalSpeedMin, trackedProfile.NormalSpeedMax) + " mm/s");
        report.AppendLine(
            "- Work angle: " + averageWorkAngle.ToString("0.0") + " deg  | target " +
            FormatRange(trackedProfile.NormalWorkAngleMin, trackedProfile.NormalWorkAngleMax) + " deg");
        report.AppendLine(
            "- Travel angle: " + averageTravelAngle.ToString("0.0") + " deg  | target " +
            FormatRange(trackedProfile.NormalTravelAngleMin, trackedProfile.NormalTravelAngleMax) + " deg");
        report.AppendLine(
            "- CTWD: " + averageCtwd.ToString("0.0") + " mm  | target " +
            FormatRange(trackedProfile.NormalCtwdMin, trackedProfile.NormalCtwdMax) + " mm");
        report.AppendLine();
        report.AppendLine("What went wrong");

        int issueCount = 0;
        issueCount += AppendSpeedIssue(report);
        issueCount += AppendAngleIssue(
            report,
            "Work angle",
            trackedProfile.NormalWorkAngleMin,
            trackedProfile.NormalWorkAngleMax,
            workAngleBelowRangeTime,
            workAngleAboveRangeTime,
            "Incorrect work angle changes how the arc is aimed into the joint.");
        issueCount += AppendAngleIssue(
            report,
            "Travel angle",
            trackedProfile.NormalTravelAngleMin,
            trackedProfile.NormalTravelAngleMax,
            travelAngleBelowRangeTime,
            travelAngleAboveRangeTime,
            "Incorrect travel angle changes how the puddle is pushed along the seam.");
        issueCount += AppendCtwdIssue(report);

        if (issueCount == 0)
            report.AppendLine("- All tracked parameters stayed inside the validated ranges.");

        report.AppendLine();
        report.AppendLine("Detected defect time");
        AppendDefectDuration(report, TubeMeshBuilder.WeldDefect.Overlap);
        AppendDefectDuration(report, TubeMeshBuilder.WeldDefect.LackOfFusion);
        AppendDefectDuration(report, TubeMeshBuilder.WeldDefect.Porosity);
        AppendDefectDuration(report, TubeMeshBuilder.WeldDefect.Spatter);

        return report.ToString();
    }

    public string BuildHeadlineText()
    {
        if (!hasSamples || totalWeldTime <= 0f)
            return "No weld data recorded";

        TubeMeshBuilder.WeldDefect dominantDefect = GetDominantDefect();
        if (dominantDefect == TubeMeshBuilder.WeldDefect.Normal)
            return "Weld quality within tracked limits";

        return GetDefectLabel(dominantDefect) + " detected";
    }

    public string BuildSummaryText()
    {
        if (!hasSamples || totalWeldTime <= 0f)
            return "Record a weld before generating a report.";

        TubeMeshBuilder.WeldDefect dominantDefect = GetDominantDefect();
        if (dominantDefect == TubeMeshBuilder.WeldDefect.Normal)
        {
            return WeldJointCatalog.GetLabel(trackedJointType) +
                   " weld completed with all tracked parameters inside the validated range.";
        }

        return WeldJointCatalog.GetLabel(trackedJointType) +
               " weld completed with " + GetDefectLabel(dominantDefect).ToLowerInvariant() +
               " as the main detected issue.";
    }

    public string BuildKeyStatsText()
    {
        if (!hasSamples || totalWeldTime <= 0f)
            return "Joint: --\nTime: --\nMain defect: --\nNormal weld: --";

        return "Joint: " + WeldJointCatalog.GetLabel(trackedJointType) +
               "\nTime: " + totalWeldTime.ToString("0.0") + " s" +
               "\nMain defect: " + GetDefectLabel(GetDominantDefect()) +
               "\nNormal weld: " + GetDefectPercent(TubeMeshBuilder.WeldDefect.Normal).ToString("0") + "%";
    }

    public string BuildParameterTableText()
    {
        if (!hasSamples || totalWeldTime <= 0f)
            return "Parameter                 Result        Target\n--                        --            --";

        return
            "Parameter                 Result        Target\n" +
            "Travel speed              " + FormatValue(GetAverageSpeed(), "mm/s") + "    " +
            FormatRange(trackedProfile.NormalSpeedMin, trackedProfile.NormalSpeedMax) + " mm/s\n" +
            "Work angle                " + FormatValue(GetAverageWorkAngle(), "deg") + "     " +
            FormatRange(trackedProfile.NormalWorkAngleMin, trackedProfile.NormalWorkAngleMax) + " deg\n" +
            "Travel angle              " + FormatValue(GetAverageTravelAngle(), "deg") + "     " +
            FormatRange(trackedProfile.NormalTravelAngleMin, trackedProfile.NormalTravelAngleMax) + " deg\n" +
            "CTWD                      " + FormatValue(GetAverageCtwd(), "mm") + "      " +
            FormatRange(trackedProfile.NormalCtwdMin, trackedProfile.NormalCtwdMax) + " mm";
    }

    public string BuildIssueSummaryText()
    {
        if (!hasSamples || totalWeldTime <= 0f)
            return "No issues recorded.";

        StringBuilder report = new StringBuilder(320);
        int issueCount = 0;

        issueCount += AppendCompactSpeedIssue(report);
        issueCount += AppendCompactAngleIssue(
            report,
            "Work angle",
            trackedProfile.NormalWorkAngleMin,
            trackedProfile.NormalWorkAngleMax,
            workAngleBelowRangeTime,
            workAngleAboveRangeTime);
        issueCount += AppendCompactAngleIssue(
            report,
            "Travel angle",
            trackedProfile.NormalTravelAngleMin,
            trackedProfile.NormalTravelAngleMax,
            travelAngleBelowRangeTime,
            travelAngleAboveRangeTime);
        issueCount += AppendCompactCtwdIssue(report);

        if (issueCount == 0)
            report.Append("All tracked parameters stayed inside the validated range.");

        return report.ToString().TrimEnd();
    }

    public string BuildPriorityIssuesText(int maxIssues = 3)
    {
        if (!hasSamples || totalWeldTime <= 0f)
            return "No issues recorded.";

        List<ReportIssue> issues = BuildReportIssues();
        if (issues.Count == 0)
            return "All tracked parameters stayed inside the validated range.";

        issues.Sort((a, b) => b.Percent.CompareTo(a.Percent));
        StringBuilder report = new StringBuilder(180);
        int count = Mathf.Min(Mathf.Max(1, maxIssues), issues.Count);

        for (int i = 0; i < count; i++)
        {
            if (i > 0)
                report.AppendLine();

            report.Append(i + 1);
            report.Append(". ");
            report.Append(issues[i].ShortLabel);
            report.Append(" - ");
            report.Append(issues[i].Percent.ToString("0"));
            report.Append("%");

            if (!string.IsNullOrEmpty(issues[i].ShortReason))
            {
                report.Append(" (");
                report.Append(issues[i].ShortReason);
                report.Append(")");
            }
        }

        return report.ToString();
    }

    public string BuildDefectBreakdownText()
    {
        if (!hasSamples || totalWeldTime <= 0f)
            return "No defect data recorded.";

        return
            "Normal " + GetDefectPercent(TubeMeshBuilder.WeldDefect.Normal).ToString("0") + "%\n" +
            "Overlap " + GetDefectPercent(TubeMeshBuilder.WeldDefect.Overlap).ToString("0") + "%\n" +
            "Lack of Fusion " + GetDefectPercent(TubeMeshBuilder.WeldDefect.LackOfFusion).ToString("0") + "%\n" +
            "Porosity " + GetDefectPercent(TubeMeshBuilder.WeldDefect.Porosity).ToString("0") + "%\n" +
            "Spatter " + GetDefectPercent(TubeMeshBuilder.WeldDefect.Spatter).ToString("0") + "%";
    }

    public float GetDefectPercent(TubeMeshBuilder.WeldDefect defect)
    {
        return GetPercent(defectDurations[(int)defect]);
    }

    public string GetDominantDefectLabel()
    {
        return GetDefectLabel(GetDominantDefect());
    }

    public List<ReportIssueSummary> GetPriorityIssues(int maxIssues = 3)
    {
        List<ReportIssue> issues = BuildReportIssues();
        issues.Sort((a, b) => b.Percent.CompareTo(a.Percent));

        int count = Mathf.Min(Mathf.Max(0, maxIssues), issues.Count);
        List<ReportIssueSummary> summaries = new List<ReportIssueSummary>(count);
        for (int i = 0; i < count; i++)
        {
            summaries.Add(new ReportIssueSummary
            {
                Label = issues[i].ShortLabel,
                Reason = issues[i].ShortReason,
                Percent = issues[i].Percent
            });
        }

        return summaries;
    }

    private int AppendSpeedIssue(StringBuilder report)
    {
        int issueCount = 0;
        float lowPercent = GetPercent(speedBelowRangeTime);
        float highPercent = GetPercent(speedAboveRangeTime);

        if (lowPercent > 0f)
        {
            report.AppendLine(
                "- Travel speed was below " + trackedProfile.NormalSpeedMin.ToString("0.0") +
                " mm/s for " + lowPercent.ToString("0") +
                "% of the weld. The validated overlap trigger begins below " +
                trackedProfile.OverlapSpeedMax.ToString("0.0") + " mm/s.");
            issueCount++;
        }

        if (highPercent > 0f)
        {
            report.AppendLine(
                "- Travel speed was above " + trackedProfile.NormalSpeedMax.ToString("0.0") +
                " mm/s for " + highPercent.ToString("0") +
                "% of the weld. The validated lack-of-fusion trigger begins above " +
                trackedProfile.LackOfFusionSpeedMin.ToString("0.0") + " mm/s.");
            issueCount++;
        }

        return issueCount;
    }

    private int AppendCompactSpeedIssue(StringBuilder report)
    {
        int issueCount = 0;
        float lowPercent = GetPercent(speedBelowRangeTime);
        float highPercent = GetPercent(speedAboveRangeTime);

        if (lowPercent > 0f)
        {
            AppendCompactIssue(
                report,
                "Speed too low",
                lowPercent,
                "risk: overlap");
            issueCount++;
        }

        if (highPercent > 0f)
        {
            AppendCompactIssue(
                report,
                "Speed too high",
                highPercent,
                "risk: lack of fusion");
            issueCount++;
        }

        return issueCount;
    }

    private int AppendAngleIssue(
        StringBuilder report,
        string displayName,
        float normalMin,
        float normalMax,
        float belowTime,
        float aboveTime,
        string explanation)
    {
        float totalIssuePercent = GetPercent(belowTime + aboveTime);
        if (totalIssuePercent <= 0f)
            return 0;

        report.AppendLine(
            "- " + displayName + " was outside " +
            FormatRange(normalMin, normalMax) + " deg for " +
            totalIssuePercent.ToString("0") + "% of the weld. " + explanation);
        return 1;
    }

    private int AppendCompactAngleIssue(
        StringBuilder report,
        string displayName,
        float normalMin,
        float normalMax,
        float belowTime,
        float aboveTime)
    {
        float totalIssuePercent = GetPercent(belowTime + aboveTime);
        if (totalIssuePercent <= 0f)
            return 0;

        AppendCompactIssue(
            report,
            displayName + " outside " + FormatRange(normalMin, normalMax) + " deg",
            totalIssuePercent,
            "outside target");
        return 1;
    }

    private int AppendCtwdIssue(StringBuilder report)
    {
        float lowPercent = GetPercent(ctwdBelowRangeTime);
        float highPercent = GetPercent(ctwdAboveRangeTime);
        int issueCount = 0;

        if (lowPercent > 0f)
        {
            report.AppendLine(
                "- CTWD was below " + trackedProfile.NormalCtwdMin.ToString("0.0") +
                " mm for " + lowPercent.ToString("0") +
                "% of the weld. Very short CTWD is one of the table triggers for spatter.");
            issueCount++;
        }

        if (highPercent > 0f)
        {
            report.AppendLine(
                "- CTWD was above " + trackedProfile.NormalCtwdMax.ToString("0.0") +
                " mm for " + highPercent.ToString("0") +
                "% of the weld. Long CTWD appears in the table for unstable weld quality and defect conditions.");
            issueCount++;
        }

        return issueCount;
    }

    private int AppendCompactCtwdIssue(StringBuilder report)
    {
        int issueCount = 0;
        float lowPercent = GetPercent(ctwdBelowRangeTime);
        float highPercent = GetPercent(ctwdAboveRangeTime);

        if (lowPercent > 0f)
        {
            AppendCompactIssue(
                report,
                "CTWD too short",
                lowPercent,
                "risk: spatter");
            issueCount++;
        }

        if (highPercent > 0f)
        {
            AppendCompactIssue(
                report,
                "CTWD too long",
                highPercent,
                "unstable arc / porosity risk");
            issueCount++;
        }

        return issueCount;
    }

    private void AppendDefectDuration(StringBuilder report, TubeMeshBuilder.WeldDefect defect)
    {
        float percent = GetPercent(defectDurations[(int)defect]);
        report.AppendLine("- " + GetDefectLabel(defect) + ": " + percent.ToString("0") + "%");
    }

    private TubeMeshBuilder.WeldDefect GetDominantDefect()
    {
        TubeMeshBuilder.WeldDefect dominant = TubeMeshBuilder.WeldDefect.Normal;
        float longestDuration = 0f;

        for (int i = 1; i < defectDurations.Length; i++)
        {
            if (defectDurations[i] <= longestDuration)
                continue;

            longestDuration = defectDurations[i];
            dominant = (TubeMeshBuilder.WeldDefect)i;
        }

        return longestDuration > 0f ? dominant : TubeMeshBuilder.WeldDefect.Normal;
    }

    private float GetPercent(float duration)
    {
        if (totalWeldTime <= 0f)
            return 0f;

        return duration / totalWeldTime * 100f;
    }

    private static void AccumulateRangeViolation(
        float value,
        float min,
        float max,
        float deltaTime,
        ref float belowTime,
        ref float aboveTime)
    {
        if (value < min)
            belowTime += deltaTime;
        else if (value > max)
            aboveTime += deltaTime;
    }

    private static string FormatRange(float min, float max)
    {
        return min.ToString("0.0") + "-" + max.ToString("0.0");
    }

    private float GetAverageSpeed()
    {
        return totalWeldTime > 0f ? speedWeightedSum / totalWeldTime : 0f;
    }

    private float GetAverageWorkAngle()
    {
        return totalWeldTime > 0f ? workAngleWeightedSum / totalWeldTime : 0f;
    }

    private float GetAverageTravelAngle()
    {
        return totalWeldTime > 0f ? travelAngleWeightedSum / totalWeldTime : 0f;
    }

    private float GetAverageCtwd()
    {
        return totalWeldTime > 0f ? ctwdWeightedSum / totalWeldTime : 0f;
    }

    private static string FormatValue(float value, string unit)
    {
        return value.ToString("0.0").PadLeft(5) + " " + unit;
    }

    private static void AppendCompactIssue(StringBuilder report, string label, float percent, string detail)
    {
        if (report.Length > 0)
            report.AppendLine();

        report.Append("- ");
        report.Append(label);
        report.Append(" for ");
        report.Append(percent.ToString("0"));
        report.Append("% of weld (");
        report.Append(detail);
        report.Append(")");
    }

    private List<ReportIssue> BuildReportIssues()
    {
        List<ReportIssue> issues = new List<ReportIssue>();
        AddIssueIfPresent(
            issues,
            "Speed too low",
            "overlap risk",
            GetPercent(speedBelowRangeTime));
        AddIssueIfPresent(
            issues,
            "Speed too high",
            "lack of fusion risk",
            GetPercent(speedAboveRangeTime));
        AddIssueIfPresent(
            issues,
            "Work angle out of range",
            string.Empty,
            GetPercent(workAngleBelowRangeTime + workAngleAboveRangeTime));
        AddIssueIfPresent(
            issues,
            "Travel angle out of range",
            string.Empty,
            GetPercent(travelAngleBelowRangeTime + travelAngleAboveRangeTime));
        AddIssueIfPresent(
            issues,
            "CTWD too short",
            "spatter risk",
            GetPercent(ctwdBelowRangeTime));
        AddIssueIfPresent(
            issues,
            "CTWD too long",
            "unstable arc",
            GetPercent(ctwdAboveRangeTime));
        return issues;
    }

    private static void AddIssueIfPresent(
        List<ReportIssue> issues,
        string shortLabel,
        string shortReason,
        float percent)
    {
        if (percent <= 0f)
            return;

        issues.Add(new ReportIssue
        {
            ShortLabel = shortLabel,
            ShortReason = shortReason,
            Percent = percent
        });
    }

    private struct ReportIssue
    {
        public string ShortLabel;
        public string ShortReason;
        public float Percent;
    }

    private static string GetDefectLabel(TubeMeshBuilder.WeldDefect defect)
    {
        switch (defect)
        {
            case TubeMeshBuilder.WeldDefect.Porosity:
                return "Porosity";
            case TubeMeshBuilder.WeldDefect.Overlap:
                return "Overlap";
            case TubeMeshBuilder.WeldDefect.LackOfFusion:
                return "Lack of Fusion";
            case TubeMeshBuilder.WeldDefect.Spatter:
                return "Excess Spatter";
            default:
                return "Normal";
        }
    }
}
