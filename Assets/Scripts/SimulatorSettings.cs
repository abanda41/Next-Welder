using UnityEngine;
using UnityEngine.SceneManagement;

public static class SimulatorSettings
{
    public const string TurnPrefKey = "turn";
    public const string VolumePrefKey = "masterVolume";
    public const string TableHeightPrefKey = "tableHeightMeters";
    public const string TutorialsEnabledPrefKey = "tutorialsEnabled";
    public const int ContinuousTurnIndex = 0;
    public const int SnapTurnIndex = 1;
    public const float DefaultVolume = 0.5f;
    public const float DefaultTableHeight = 1f;
    public const bool DefaultTutorialsEnabled = true;
    public const float MinTableHeight = 0.75f;
    public const float MaxTableHeight = 1.25f;
    public const float TableHeightStep = 0.05f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterSceneLoadHook()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        EnsureDefaults();
        ApplyVolume();
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ApplyVolume();
    }

    public static int GetTurnModeIndex()
    {
        int value = PlayerPrefs.GetInt(TurnPrefKey, ContinuousTurnIndex);
        return Mathf.Clamp(value, ContinuousTurnIndex, SnapTurnIndex);
    }

    public static void SetTurnModeIndex(int value)
    {
        PlayerPrefs.SetInt(TurnPrefKey, Mathf.Clamp(value, ContinuousTurnIndex, SnapTurnIndex));
        PlayerPrefs.Save();
    }

    public static float GetVolume()
    {
        return Mathf.Clamp01(PlayerPrefs.GetFloat(VolumePrefKey, DefaultVolume));
    }

    public static void SetVolume(float value)
    {
        float clampedValue = Mathf.Clamp01(value);
        PlayerPrefs.SetFloat(VolumePrefKey, clampedValue);
        PlayerPrefs.Save();
        AudioListener.volume = clampedValue;
    }

    public static void ApplyVolume()
    {
        AudioListener.volume = GetVolume();
    }

    public static float GetTableHeight()
    {
        float value = PlayerPrefs.GetFloat(TableHeightPrefKey, DefaultTableHeight);
        return ClampTableHeight(value);
    }

    public static void SetTableHeight(float value)
    {
        float clampedValue = ClampTableHeight(value);
        PlayerPrefs.SetFloat(TableHeightPrefKey, clampedValue);
        PlayerPrefs.Save();
    }

    public static bool GetTutorialsEnabled()
    {
        return PlayerPrefs.GetInt(TutorialsEnabledPrefKey, DefaultTutorialsEnabled ? 1 : 0) != 0;
    }

    public static void SetTutorialsEnabled(bool enabled)
    {
        PlayerPrefs.SetInt(TutorialsEnabledPrefKey, enabled ? 1 : 0);
        PlayerPrefs.Save();
    }

    public static float ClampTableHeight(float value)
    {
        float clampedValue = Mathf.Clamp(value, MinTableHeight, MaxTableHeight);
        return Mathf.Round(clampedValue / TableHeightStep) * TableHeightStep;
    }

    private static void EnsureDefaults()
    {
        if (!PlayerPrefs.HasKey(TurnPrefKey))
            PlayerPrefs.SetInt(TurnPrefKey, ContinuousTurnIndex);

        if (!PlayerPrefs.HasKey(VolumePrefKey))
            PlayerPrefs.SetFloat(VolumePrefKey, DefaultVolume);

        if (!PlayerPrefs.HasKey(TableHeightPrefKey))
            PlayerPrefs.SetFloat(TableHeightPrefKey, DefaultTableHeight);

        if (!PlayerPrefs.HasKey(TutorialsEnabledPrefKey))
            PlayerPrefs.SetInt(TutorialsEnabledPrefKey, DefaultTutorialsEnabled ? 1 : 0);

        PlayerPrefs.Save();
    }
}
