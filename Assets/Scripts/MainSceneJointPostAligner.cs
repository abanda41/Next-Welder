using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MainSceneJointPostAligner : MonoBehaviour
{
    private const string MainSceneName = "Main VR Scene";

    private static MainSceneJointPostAligner instance;
    private Coroutine pendingRoutine;
    private GameObject targetJoint;

    public static void Schedule(GameObject joint)
    {
        if (SceneManager.GetActiveScene().name != MainSceneName)
            return;

        if (instance == null)
        {
            GameObject host = new GameObject("Main Scene Joint Post Aligner");
            instance = host.AddComponent<MainSceneJointPostAligner>();
        }

        instance.Begin(joint);
    }

    private void Begin(GameObject joint)
    {
        targetJoint = joint;

        if (pendingRoutine != null)
            StopCoroutine(pendingRoutine);

        pendingRoutine = StartCoroutine(RealignAfterSpawn());
    }

    private IEnumerator RealignAfterSpawn()
    {
        yield return null;
        yield return null;
        yield return new WaitForEndOfFrame();

        if (SceneManager.GetActiveScene().name == MainSceneName)
            TableHeightManager.ApplyCurrentTableHeight(targetJoint);

        // ── Auto-assign WeldPath to WeldBead ──────────────────────────
        // The joint is now fully positioned. Find the WeldPath on it and
        // give it to every WeldBead in the scene so snap assist works
        // without any manual inspector wiring.
        AutoAssignWeldPath(targetJoint);

        pendingRoutine = null;
        targetJoint = null;
        Destroy(gameObject);
    }

    private static void AutoAssignWeldPath(GameObject jointInstance)
    {
        if (jointInstance == null) return;

        // Find the WeldPath on the spawned joint
        WeldPath weldPath = jointInstance.GetComponentInChildren<WeldPath>(true);
        if (weldPath == null)
        {
            Debug.LogWarning("[PostAligner] No WeldPath found on joint — snap assist will be inactive.");
            return;
        }

        // Find every WeldBead in the scene and assign the path
        WeldBead[] allWeldBeads = Object.FindObjectsByType<WeldBead>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);

        if (allWeldBeads.Length == 0)
        {
            Debug.LogWarning("[PostAligner] No WeldBead found in scene — cannot assign WeldPath.");
            return;
        }

        foreach (WeldBead wb in allWeldBeads)
        {
            wb.weldPath = weldPath;
            Debug.Log($"[PostAligner] WeldPath auto-assigned to '{wb.gameObject.name}' " +
                      $"from joint '{jointInstance.name}'.");
        }
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }
}
