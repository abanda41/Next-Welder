using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Turning;

public class SetTurnTypeFromPlayerPref : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterSceneLoadHook()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode loadMode)
    {
        ApplyPlayerPrefToCurrentScene();
    }

    private void Start()
    {
        ApplyPlayerPrefToCurrentScene();
    }

    public void ApplyPlayerPref()
    {
        ApplyPlayerPrefToCurrentScene();
    }

    public static void ApplyPlayerPrefToCurrentScene()
    {
        bool useContinuousTurn = SimulatorSettings.GetTurnModeIndex() != SimulatorSettings.SnapTurnIndex;

        ApplyToProviders(!useContinuousTurn, useContinuousTurn);
    }

    private static void ApplyToProviders(bool snapEnabled, bool continuousEnabled)
    {
        foreach (var snapTurn in FindSceneComponents<SnapTurnProvider>())
            SetSnapTurnEnabled(snapTurn, snapEnabled);

        foreach (var continuousTurn in FindSceneComponents<ContinuousTurnProvider>())
            SetContinuousTurnEnabled(continuousTurn, continuousEnabled);
    }

    private static T[] FindSceneComponents<T>() where T : Component
    {
        T[] allComponents = Resources.FindObjectsOfTypeAll<T>();
        int validCount = 0;

        for (int i = 0; i < allComponents.Length; i++)
        {
            T component = allComponents[i];
            if (component == null)
                continue;

            if (!component.gameObject.scene.IsValid())
                continue;

            validCount++;
        }

        if (validCount == allComponents.Length)
            return allComponents;

        T[] validComponents = new T[validCount];
        int index = 0;

        for (int i = 0; i < allComponents.Length; i++)
        {
            T component = allComponents[i];
            if (component == null)
                continue;

            if (!component.gameObject.scene.IsValid())
                continue;

            validComponents[index] = component;
            index++;
        }

        return validComponents;
    }

    private static void SetSnapTurnEnabled(SnapTurnProvider snapTurn, bool isEnabled)
    {
        if (snapTurn == null)
            return;

        snapTurn.enabled = isEnabled;
    }

    private static void SetContinuousTurnEnabled(ContinuousTurnProvider continuousTurn, bool isEnabled)
    {
        if (continuousTurn == null)
            return;

        continuousTurn.enabled = isEnabled;
    }
}
