using System.Collections;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.SceneManagement;

public class XRSceneSpawnAligner : MonoBehaviour
{
    private const string StartSceneName = "Start Scene";
    private const string MainSceneName = "Main VR Scene";
    private const string VrPlayerName = "VR Player";

    // These are the authored headset spawn positions already visible in the scenes.
    private static readonly Vector2 StartSceneHeadXZ = new Vector2(0.105f, 0.945f);
    private static readonly Vector2 MainSceneHeadXZ = new Vector2(0f, 3.172f);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterSceneHook()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != StartSceneName && scene.name != MainSceneName)
            return;

        GameObject alignerObject = new GameObject("XR Scene Spawn Aligner");
        XRSceneSpawnAligner aligner = alignerObject.AddComponent<XRSceneSpawnAligner>();
        aligner.sceneName = scene.name;
    }

    private string sceneName;

    private IEnumerator Start()
    {
        GameObject player = GameObject.Find(VrPlayerName);
        if (player == null)
        {
            Destroy(gameObject);
            yield break;
        }

        XROrigin origin = player.GetComponent<XROrigin>();
        Camera xrCamera = origin != null ? origin.Camera : Camera.main;
        if (origin == null || xrCamera == null)
        {
            Destroy(gameObject);
            yield break;
        }

        // Give the device a brief moment to publish its tracked pose, then settle twice.
        yield return null;
        yield return new WaitForSecondsRealtime(0.1f);
        AlignHeadsetToSceneSpawn(origin, xrCamera);
        yield return new WaitForSecondsRealtime(0.1f);
        AlignHeadsetToSceneSpawn(origin, xrCamera);

        Destroy(gameObject);
    }

    private void AlignHeadsetToSceneSpawn(XROrigin origin, Camera xrCamera)
    {
        Vector2 targetXZ = sceneName == MainSceneName ? MainSceneHeadXZ : StartSceneHeadXZ;
        Vector3 currentHeadPosition = xrCamera.transform.position;
        Vector3 desiredHeadPosition = new Vector3(targetXZ.x, currentHeadPosition.y, targetXZ.y);
        origin.MoveCameraToWorldLocation(desiredHeadPosition);

        if (sceneName == MainSceneName)
            origin.MatchOriginUpCameraForward(Vector3.up, Vector3.back);
    }
}
