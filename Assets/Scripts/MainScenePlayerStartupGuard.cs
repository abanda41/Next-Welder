using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit.Locomotion;

public class MainScenePlayerStartupGuard : MonoBehaviour
{
    private const string MainSceneName = "Main VR Scene";
    private const string VrPlayerName = "VR Player";
    private const float StartupHoldSeconds = 0.35f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterSceneHook()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != MainSceneName)
            return;

        GameObject guardObject = new GameObject("Main Scene Player Startup Guard");
        guardObject.AddComponent<MainScenePlayerStartupGuard>();
    }

    private IEnumerator Start()
    {
        GameObject player = GameObject.Find(VrPlayerName);
        if (player == null)
        {
            Destroy(gameObject);
            yield break;
        }

        CharacterController controller = player.GetComponent<CharacterController>();
        LocomotionProvider[] locomotionProviders =
            player.GetComponentsInChildren<LocomotionProvider>(true);

        bool controllerWasEnabled = controller != null && controller.enabled;
        bool[] providerStates = new bool[locomotionProviders.Length];

        if (controller != null)
            controller.enabled = false;

        for (int i = 0; i < locomotionProviders.Length; i++)
        {
            providerStates[i] = locomotionProviders[i] != null && locomotionProviders[i].enabled;
            if (locomotionProviders[i] != null)
                locomotionProviders[i].enabled = false;
        }

        yield return new WaitForSecondsRealtime(StartupHoldSeconds);

        if (controller != null)
            controller.enabled = controllerWasEnabled;

        for (int i = 0; i < locomotionProviders.Length; i++)
        {
            if (locomotionProviders[i] != null)
                locomotionProviders[i].enabled = providerStates[i];
        }

        Destroy(gameObject);
    }
}
