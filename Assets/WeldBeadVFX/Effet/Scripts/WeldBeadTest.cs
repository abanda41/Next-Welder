using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class WeldBeadTest : MonoBehaviour
{

    [SerializeField] private float sizeRandomness = 0.02f;

    [Header("Sphere Trail")]
    [SerializeField] private GameObject spherePrefab;
    [SerializeField] private float sphereSize = 0.08f;
    [SerializeField] private float spacing = 0.06f;
    [SerializeField] private float surfaceOffset = 0.005f;

    [SerializeField] private float sideJitter = 0.01f;

    [SerializeField] private Vector3 rotationOffsetEuler;


    private Vector3 lastSpawnPosition;
    private bool hasSpawned;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if(Mouse.current.leftButton.isPressed)
        {
            shootRayAndSpawn();
        }
    }

    private void shootRayAndSpawn()
    {
        if (Camera.main == null) return;

        Ray ray = Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue());

        if(Physics.Raycast(ray, out RaycastHit hit))
        {
            if(hit.collider.CompareTag("weldable"))
            {
                if(!hasSpawned || Vector3.Distance(lastSpawnPosition, hit.point) >= spacing)
                {
                    SpawnSphereAtHit(hit, ray.direction);
                }
            }
        }
    }

    private void SpawnSphereAtHit(RaycastHit hit, Vector3 rayDirection)
    {
        Vector3 spawnPosition = hit.point + hit.normal * surfaceOffset;
        Vector3 sideDirection = Vector3.Cross(hit.normal, rayDirection).normalized;
        if (sideDirection != Vector3.zero)
        {
            spawnPosition += sideDirection * Random.Range(-sideJitter, sideJitter);
        }
        Quaternion baseRotation = Quaternion.LookRotation(hit.normal);
        Quaternion offset = Quaternion.Euler(rotationOffsetEuler);

        Quaternion finalRotation = baseRotation * offset;
        
        GameObject sphere = Instantiate(spherePrefab, spawnPosition, finalRotation);

        Vector3 originalLocalScale = sphere.transform.localScale;
        float randomSize = sphereSize + Random.Range(-sizeRandomness, sizeRandomness);
        sphere.transform.localScale = new Vector3 (randomSize * originalLocalScale.x, randomSize * originalLocalScale.y, randomSize * originalLocalScale.z);

        lastSpawnPosition = hit.point;
        hasSpawned = true;
    }
}
