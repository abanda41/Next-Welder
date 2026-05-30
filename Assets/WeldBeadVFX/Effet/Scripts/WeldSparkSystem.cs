using UnityEngine;

public class WeldSparkSystem : MonoBehaviour
{
    [SerializeField] private ParticleSystem sparkParticles;
    [SerializeField] private int burstCount = 5;
    
    void Awake()
    {
        if (sparkParticles == null)
            sparkParticles = GetComponent<ParticleSystem>();
            
        if (sparkParticles == null)
        {
            CreateDefaultSparkSystem();
        }
    }

    private void CreateDefaultSparkSystem()
    {
        GameObject psGo = new GameObject("SparkParticles");
        psGo.transform.SetParent(transform, false);
        sparkParticles = psGo.AddComponent<ParticleSystem>();
        
        var main = sparkParticles.main;
        main.startSize = new ParticleSystem.MinMaxCurve(0.002f, 0.005f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 1.5f);
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.1f, 0.3f);
        main.gravityModifier = 0.5f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        
        var emission = sparkParticles.emission;
        emission.enabled = false; 
        
        var shape = sparkParticles.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.001f;
        
        var renderer = psGo.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        
        // Use URP Particle shader if possible, fallback to Sprites/Default
        Shader particleShader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (particleShader == null) particleShader = Shader.Find("Sprites/Default");
        
        renderer.material = new Material(particleShader);
        renderer.material.color = new Color(1f, 0.6f, 0f) * 2f; // Reduced intensity
    }

    public void TriggerSparks(Vector3 position, Vector3 normal)
    {
        if (sparkParticles == null) return;
        
        transform.position = position;
        transform.forward = normal;
        sparkParticles.Emit(burstCount);
    }
}