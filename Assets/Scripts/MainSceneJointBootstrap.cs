using UnityEngine;
using UnityEngine.SceneManagement;

public static class MainSceneJointBootstrap
{
    private const string MainSceneName = "Main VR Scene";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterSceneLoadHandler()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode loadMode)
    {
        if (scene.name != MainSceneName)
            return;

        ApplyJointSelection(scene);
    }

    private static void ApplyJointSelection(Scene scene)
    {
        GameObject teeJoint = GameObject.Find(WeldJointCatalog.GetSceneObjectName(WeldJointType.TeeJoint));
        if (teeJoint == null)
        {
            Debug.LogWarning("MainSceneJointBootstrap: Could not find the Tee joint anchor in Main VR Scene.");
            return;
        }

        Transform parent = teeJoint.transform.parent;
        int siblingIndex = teeJoint.transform.GetSiblingIndex();
        Vector3 targetPosition = teeJoint.transform.position;
        Quaternion targetRotation = teeJoint.transform.rotation;
        Vector3 targetScale = teeJoint.transform.localScale;
        Bounds teeBounds = GetRendererBoundsOrFallback(teeJoint, targetPosition);
        Material jointMaterial = GetPrimarySharedMaterial(teeJoint);
        TableHeightManager.RegisterJointAnchor(targetPosition);

        WeldJointType selectedType = WeldJointSelectionState.GetSelectedOrDefault();
        GameObject selectedPrefab = Resources.Load<GameObject>(WeldJointCatalog.GetResourcePath(selectedType));
        Quaternion finalRotation = targetRotation * WeldJointCatalog.GetRotationOffset(selectedType);

        if (selectedPrefab == null)
        {
            Debug.LogError($"MainSceneJointBootstrap: Missing resource '{WeldJointCatalog.GetResourcePath(selectedType)}'.");
            return;
        }

        RemoveKnownJointObjects(scene);

        GameObject jointInstance;
        if (parent != null)
        {
            jointInstance = Object.Instantiate(selectedPrefab, parent);
            jointInstance.transform.SetSiblingIndex(siblingIndex);
            jointInstance.transform.SetPositionAndRotation(targetPosition, finalRotation);
        }
        else
        {
            jointInstance = Object.Instantiate(selectedPrefab, targetPosition, finalRotation);
        }

        jointInstance.name = WeldJointCatalog.GetSceneObjectName(selectedType);
        jointInstance.transform.localScale = targetScale;
        AlignJointOnTargetSurface(jointInstance, teeBounds);

        ApplySharedMaterial(jointInstance, jointMaterial);
        EnsureJointIsWeldable(jointInstance);
        EnsureJointHasWeldableJointMarker(jointInstance);
        TableHeightManager.ApplyCurrentTableHeight(jointInstance);
        MainSceneJointPostAligner.Schedule(jointInstance);
    }

    private static void RemoveKnownJointObjects(Scene scene)
    {
        GameObject[] rootObjects = scene.GetRootGameObjects();
        foreach (WeldJointType jointType in WeldJointCatalog.OrderedTypes)
        {
            string objectName = WeldJointCatalog.GetSceneObjectName(jointType);
            foreach (GameObject rootObject in rootObjects)
            {
                Transform matchingTransform = FindChildRecursive(rootObject.transform, objectName);
                if (matchingTransform != null)
                {
                    matchingTransform.gameObject.name = matchingTransform.gameObject.name + " (Pending Destroy)";
                    matchingTransform.gameObject.SetActive(false);
                    Object.Destroy(matchingTransform.gameObject);
                }
            }
        }
    }

    private static Transform FindChildRecursive(Transform root, string targetName)
    {
        if (root.name == targetName)
            return root;

        foreach (Transform child in root)
        {
            Transform match = FindChildRecursive(child, targetName);
            if (match != null)
                return match;
        }

        return null;
    }

    private static void AlignJointOnTargetSurface(GameObject jointInstance, Bounds targetBounds)
    {
        Bounds currentBounds = GetRendererBoundsOrFallback(jointInstance, jointInstance.transform.position);
        Vector3 currentCenter = currentBounds.center;
        Vector3 targetCenter = targetBounds.center;

        jointInstance.transform.position += new Vector3(
            targetCenter.x - currentCenter.x,
            targetBounds.min.y - currentBounds.min.y,
            targetCenter.z - currentCenter.z);
    }

    private static Bounds GetRendererBoundsOrFallback(GameObject root, Vector3 fallbackCenter)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
            return new Bounds(fallbackCenter, Vector3.zero);

        Bounds combinedBounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            combinedBounds.Encapsulate(renderers[i].bounds);

        return combinedBounds;
    }

    private static void EnsureJointIsWeldable(GameObject root)
    {
        int weldableLayer = LayerMask.NameToLayer("Weldable");
        if (weldableLayer == -1)
        {
            Debug.LogWarning("MainSceneJointBootstrap: Weldable layer was not found.");
            return;
        }

        foreach (Transform item in root.GetComponentsInChildren<Transform>(true))
            item.gameObject.layer = weldableLayer;

        foreach (MeshFilter meshFilter in root.GetComponentsInChildren<MeshFilter>(true))
        {
            if (meshFilter.sharedMesh == null)
                continue;

            MeshCollider meshCollider = meshFilter.GetComponent<MeshCollider>();
            if (meshCollider == null)
                meshCollider = meshFilter.gameObject.AddComponent<MeshCollider>();

            meshCollider.sharedMesh = meshFilter.sharedMesh;
            meshCollider.convex = false;
            meshCollider.isTrigger = false;
        }
    }

    private static void EnsureJointHasWeldableJointMarker(GameObject root)
    {
        // Add WeldableJoint marker so WeldBead (VR mode) can confirm this
        // surface belongs to a joint and not the floor / environment.
        if (root.GetComponent<WeldableJoint>() == null)
        {
            WeldableJoint marker = root.AddComponent<WeldableJoint>();
            // Auto-wire WeldPath if one exists in the prefab hierarchy
            marker.weldPath = root.GetComponentInChildren<WeldPath>(true);
        }
    }

    private static Material GetPrimarySharedMaterial(GameObject root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || renderer.sharedMaterial == null)
                continue;

            return renderer.sharedMaterial;
        }

        return null;
    }

    private static void ApplySharedMaterial(GameObject root, Material material)
    {
        if (material == null)
            return;

        foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            if (renderer == null)
                continue;

            Material[] materials = renderer.sharedMaterials;
            if (materials == null || materials.Length == 0)
            {
                renderer.sharedMaterial = material;
                continue;
            }

            for (int i = 0; i < materials.Length; i++)
                materials[i] = material;

            renderer.sharedMaterials = materials;
        }
    }

}
