using UnityEngine;
using UnityEngine.UI;

public class SetOptionFromUI : MonoBehaviour
{
    public Scrollbar volumeSlider;
    public TMPro.TMP_Dropdown turnDropdown;
    public SetTurnTypeFromPlayerPref turnTypeFromPlayerPref;

    private void Start()
    {
        if (volumeSlider != null)
            volumeSlider.onValueChanged.AddListener(SetGlobalVolume);

        if (turnDropdown != null)
            turnDropdown.onValueChanged.AddListener(SetTurnPlayerPref);

        if (volumeSlider != null)
            volumeSlider.SetValueWithoutNotify(SimulatorSettings.GetVolume());

        if (turnDropdown != null)
            turnDropdown.SetValueWithoutNotify(SimulatorSettings.GetTurnModeIndex());

        SimulatorSettings.ApplyVolume();
        ApplyTurnSetting();
    }

    public void SetGlobalVolume(float value)
    {
        SimulatorSettings.SetVolume(value);
    }

    public void SetTurnPlayerPref(int value)
    {
        SimulatorSettings.SetTurnModeIndex(value);
        ApplyTurnSetting();
    }

    private void ApplyTurnSetting()
    {
        if (turnTypeFromPlayerPref != null)
        {
            turnTypeFromPlayerPref.ApplyPlayerPref();
            return;
        }

        SetTurnTypeFromPlayerPref.ApplyPlayerPrefToCurrentScene();
    }
}
