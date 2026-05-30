using UnityEngine;

public class WeldSmokeSystem : MonoBehaviour
{
    [SerializeField] private ParticleSystem smokeParticles;
    
    void Awake()
    {
        if (smokeParticles == null)
            smokeParticles = GetComponent<ParticleSystem>();
            
        if (smokeParticles == null)
        {
            CreateDefaultSmokeSystem();
        }
    }

    private void CreateDefaultSmokeSystem()
    {
        GameObject psGo = new GameObject("SmokeParticles");
        psGo.transform.SetParent(transform, false);
        smokeParticles = psGo.AddComponent<ParticleSystem>();
        
        var main = smokeParticles.main;
        main.startSize = new ParticleSystem.MinMaxCurve(0.01f, 0.04f); // Slightly smaller
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.2f);
        main.startLifetime = new ParticleSystem.MinMaxCurve(1.5f, 3f); // Longer
        main.gravityModifier = -0.02f; 
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startRotation = new ParticleSystem.MinMaxCurve(0, 360f); // Random rotation
        
        var emission = smokeParticles.emission;
        emission.rateOverTime = 0; 
        
        var shape = smokeParticles.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.005f;

        var colorOverLifetime = smokeParticles.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[] { new GradientColorKey(new Color(0.6f, 0.6f, 0.6f), 0f), new GradientColorKey(new Color(0.4f, 0.4f, 0.4f), 1f) },
            new GradientAlphaKey[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.15f, 0.1f), new GradientAlphaKey(0f, 1f) }
        );
        colorOverLifetime.color = new ParticleSystem.MinMaxGradient(grad);

        var sizeOverLifetime = smokeParticles.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, 4f); // Grow more
        
        var renderer = psGo.GetComponent<ParticleSystemRenderer>();
        renderer.material = new Material(Shader.Find("Sprites/Default")); 
        Shader softShader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (softShader != null) renderer.material = new Material(softShader);
    }

    public void EmitSmoke(Vector3 position, int count = 1)
    {
        if (smokeParticles == null) return;
        var emitParams = new ParticleSystem.EmitParams();
        emitParams.position = position;
        smokeParticles.Emit(emitParams, count);
    }
}