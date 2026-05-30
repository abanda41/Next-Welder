using UnityEngine;
using UnityEngine.VFX;

public class WeldArcFlicker : MonoBehaviour
{
    [Header("VFX Properties")]
    [SerializeField] private VisualEffect vfx;
    [SerializeField] private string coreIntensityProperty = "Coreintensity";
    [SerializeField] private string arcIntensityProperty  = "Arcintensity";

    [Header("Light Settings")]
    [SerializeField] private Light arcLight;
    [SerializeField] private float baseLightIntensity = 10f;
    [SerializeField] private float lightFlickerStrength = 5f;

    [Header("Flicker Noise")]
    [SerializeField] private float flickerSpeed = 80f; // Faster
    [SerializeField] private Vector2 intensityRange = new Vector2(0.4f, 1.6f); // More range

    [Header("Glow (Halo) Settings")]
    [SerializeField] private Color glowColor = new Color(0f, 0.6f, 1f, 1f); // Cyan
    [SerializeField] private float baseGlowSize = 0.015f;
    [SerializeField] private float glowFlickerAmplitude = 0.01f;

    [Header("Core Settings")]
    [SerializeField] private float baseCoreSize = 0.004f;
    [SerializeField] private float coreFlickerAmplitude = 0.002f;

    [Header("Intensity Multipliers")]
    [SerializeField] private float coreIntensityMult = 4.0f; // Blinding core
    [SerializeField] private float arcIntensityMult  = 3.0f; 

    [Header("Light Flicker")]
    [SerializeField] private float baseLightRange = 2f;
    [SerializeField] private float lightRangeFlicker = 0.5f;

    private float _time;
    private float _baseCoreIntensity;
    private float _baseArcIntensity;
    private float _baseArcSize;
    private float _originalCoreSize;

    void Awake()
    {
        if (vfx == null) vfx = GetComponent<VisualEffect>();
        
        if (vfx != null)
        {
            _baseCoreIntensity = vfx.GetFloat(coreIntensityProperty);
            _baseArcIntensity  = vfx.GetFloat(arcIntensityProperty);
            _baseArcSize       = vfx.GetFloat("ArcSize");
            _originalCoreSize  = vfx.GetFloat("Coresize");
            
            // If they are 0, use reasonable defaults
            if (_baseCoreIntensity == 0) _baseCoreIntensity = 6.0f;
            if (_baseArcIntensity == 0)  _baseArcIntensity = 1.5f;
            if (_baseArcSize == 0)       _baseArcSize = 0.006f;
            if (_originalCoreSize == 0)  _originalCoreSize = 0.001f;
        }

        if (arcLight == null)
        {
            arcLight = GetComponentInChildren<Light>();
            if (arcLight == null)
            {
                GameObject lightGo = new GameObject("ArcLight");
                lightGo.transform.SetParent(transform, false);
                arcLight = lightGo.AddComponent<Light>();
                arcLight.type = LightType.Point;
                arcLight.color = glowColor;
                arcLight.range = baseLightRange;
                arcLight.intensity = baseLightIntensity;
                arcLight.shadows = LightShadows.Soft;
            }
        }
    }

    void Update()
    {
        if (vfx == null) return;

        _time += Time.deltaTime * flickerSpeed;
        
        // Use two octaves of noise for more "electric" feel
        float noise = (Mathf.PerlinNoise(_time, 0.5f) * 0.7f) + (Mathf.PerlinNoise(_time * 2.5f, 0.5f) * 0.3f);
        float flickerMult = Mathf.Lerp(intensityRange.x, intensityRange.y, noise);

        // Update VFX Core & Arc
        vfx.SetFloat(coreIntensityProperty, _baseCoreIntensity * flickerMult * coreIntensityMult);
        vfx.SetFloat(arcIntensityProperty,  _baseArcIntensity  * flickerMult * arcIntensityMult);
        
        // Pulse sizes independently
        float glowSize = baseGlowSize + (noise - 0.5f) * 2f * glowFlickerAmplitude;
        vfx.SetFloat("ArcSize", glowSize);
        
        float coreSize = baseCoreSize + (Mathf.PerlinNoise(_time * 1.5f, 0.1f) - 0.5f) * 2f * coreFlickerAmplitude;
        vfx.SetFloat("Coresize", coreSize);
        
        vfx.SetVector4("ArcColor", glowColor * flickerMult * 3f); 

        // Update Light
        if (arcLight != null)
        {
            arcLight.intensity = baseLightIntensity * flickerMult;
            arcLight.range = baseLightRange + (noise - 0.5f) * 2f * lightRangeFlicker;
        }
    }
}