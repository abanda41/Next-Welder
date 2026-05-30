using UnityEngine;
using UnityEngine.SceneManagement;

public static class TableHeightManager
{
    private const string MainSceneName = "Main VR Scene";
    private const string PrimaryTableName = "welding table";
    private const float TableVisualHeightScale = 1.1f;

    private static readonly string[] FallbackTableNames =
    {
        "Welding Table",
        "Table"
    };

    private static int cachedSceneHandle = -1;
    private static Transform cachedTableTransform;
    private static BoxCollider cachedTableCollider;
    private static Vector3 cachedBaseTableScale;
    private static float cachedBaseTableWorldX;
    private static float cachedBaseTableWorldZ;
    private static Vector3 cachedJointAnchorPosition;
    private static bool hasCachedJointAnchor;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterSceneLoadHook()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ResetSceneCache(scene.handle);
    }

    public static void RegisterJointAnchor(Vector3 jointAnchorPosition)
    {
        cachedJointAnchorPosition = jointAnchorPosition;
        hasCachedJointAnchor = true;
    }

    public static void ApplyCurrentTableHeight()
    {
        ApplyCurrentTableHeight(true);
    }

    public static void ApplyCurrentTableHeight(bool alignJoint)
    {
        ApplyCurrentTableHeight(null, alignJoint);
    }

    public static void ApplyCurrentTableHeight(GameObject activeJoint)
    {
        ApplyCurrentTableHeight(activeJoint, true);
    }

    private static void ApplyCurrentTableHeight(GameObject activeJoint, bool alignJoint)
    {
        Scene activeScene = SceneManager.GetActiveScene();
        if (activeScene.name != MainSceneName)
            return;

        if (!TryResolveTable(activeScene))
            return;

        ApplyTableTransform();

        if (alignJoint)
            AlignJointToTable(activeScene, activeJoint);
    }

    private static bool TryResolveTable(Scene scene)
    {
        if (cachedSceneHandle == scene.handle &&
            cachedTableTransform != null &&
            cachedTableCollider != null)
        {
            return true;
        }

        ResetSceneCache(scene.handle);

        GameObject tableObject = GameObject.Find(PrimaryTableName);
        if (tableObject == null)
        {
            for (int i = 0; i < FallbackTableNames.Length; i++)
            {
                tableObject = GameObject.Find(FallbackTableNames[i]);
                if (tableObject != null)
                    break;
            }
        }

        if (tableObject == null)
            return false;

        cachedTableTransform = tableObject.transform;
        cachedTableCollider = tableObject.GetComponent<BoxCollider>();
        if (cachedTableCollider == null)
            cachedTableCollider = tableObject.GetComponentInChildren<BoxCollider>();

        if (cachedTableCollider == null)
            return false;

        cachedBaseTableScale = cachedTableTransform.localScale;
        cachedBaseTableWorldX = cachedTableTransform.position.x;
        cachedBaseTableWorldZ = cachedTableTransform.position.z;
        return true;
    }

    private static void ApplyTableTransform()
    {
        cachedTableTransform.localScale = new Vector3(
            cachedBaseTableScale.x,
            cachedBaseTableScale.y * TableVisualHeightScale,
            cachedBaseTableScale.z);

        float targetSurfaceY = SimulatorSettings.GetTableHeight();
        float tableTopOffsetY = GetTableTopOffsetY(cachedTableCollider, cachedTableTransform);

        cachedTableTransform.position = new Vector3(
            cachedBaseTableWorldX,
            targetSurfaceY - tableTopOffsetY,
            cachedBaseTableWorldZ);
    }

    private static float GetTableTopOffsetY(BoxCollider tableCollider, Transform tableTransform)
    {
        Vector3 scale = tableTransform.lossyScale;
        float scaledCenterY = tableCollider.center.y * scale.y;
        float scaledHalfHeight = tableCollider.size.y * Mathf.Abs(scale.y) * 0.5f;
        return scaledCenterY + scaledHalfHeight;
    }

    private static void AlignJointToTable(Scene scene, GameObject activeJoint)
    {
        if (!scene.IsValid() || !scene.isLoaded)
            return;

        GameObject joint = activeJoint != null ? activeJoint : FindActiveJoint();
        if (joint == null)
            return;

        if (!TryGetJointBounds(joint, out Bounds jointBounds))
            return;

        Vector3 currentPosition = joint.transform.position;
        float jointBottomOffsetY = jointBounds.min.y - currentPosition.y;
        float targetSurfaceY = SimulatorSettings.GetTableHeight();

        joint.transform.position = new Vector3(
            hasCachedJointAnchor ? cachedJointAnchorPosition.x : currentPosition.x,
            targetSurfaceY - jointBottomOffsetY,
            hasCachedJointAnchor ? cachedJointAnchorPosition.z : currentPosition.z);
    }

    private static GameObject FindActiveJoint()
    {
        WeldJointType selectedType = WeldJointSelectionState.GetSelectedOrDefault();
        GameObject selectedJoint = GameObject.Find(WeldJointCatalog.GetSceneObjectName(selectedType));
        if (selectedJoint != null)
            return selectedJoint;

        for (int i = 0; i < WeldJointCatalog.OrderedTypes.Length; i++)
        {
            string jointName = WeldJointCatalog.GetSceneObjectName(WeldJointCatalog.OrderedTypes[i]);
            GameObject joint = GameObject.Find(jointName);
            if (joint != null)
                return joint;
        }

        return null;
    }

    private static void ResetSceneCache(int sceneHandle)
    {
        cachedSceneHandle = sceneHandle;
        cachedTableTransform = null;
        cachedTableCollider = null;
        cachedBaseTableScale = Vector3.one;
        cachedBaseTableWorldX = 0f;
        cachedBaseTableWorldZ = 0f;
        hasCachedJointAnchor = false;
    }

    private static bool TryGetJointBounds(GameObject joint, out Bounds bounds)
    {
        Renderer[] renderers = joint.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length > 0)
        {
            bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);

            return true;
        }

        Collider[] colliders = joint.GetComponentsInChildren<Collider>(true);
        if (colliders.Length > 0)
        {
            bounds = colliders[0].bounds;
            for (int i = 1; i < colliders.Length; i++)
                bounds.Encapsulate(colliders[i].bounds);

            return true;
        }

        bounds = new Bounds();
        return false;
    }
}
