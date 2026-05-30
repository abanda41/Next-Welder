
const float pi = 3.141592f;

float3 hash33(float3 p)
{
	p = float3(dot(p,float3(127.1,311.7, 74.7)),
			  dot(p,float3(269.5,183.3,246.1)),
			  dot(p,float3(113.5,271.9,124.6)));
	float3 randomPos = frac(sin(p)*43758.5453123);
	return randomPos;
}

float2 GetTextureUVs(float2 pos, float2 freq, float seed)
{
	float3 hash = hash33(float3(seed, 0.0, 0.0));
	float ang = hash.x * 2.0 * pi;
    float2x2 rotation = float2x2(cos(ang), sin(ang), -sin(ang), cos(ang));
    
    float2 uv = mul(rotation, pos * freq.xy) + hash.yz;
	return uv;
}

void AccumulateHarmonics_float(
    float2 uv,
    int harmonicsCount,
	Texture2D tex,
	SamplerState _sampleState,
    float2 texFreq,
    float2 seeds, 
    float2 weights,
    float2 phases,
    float2 velocity,
	float4 colorIn,
    out float4 colorOut
)
{
    float moment2 = 0.0;

    for (int i = 0; i < harmonicsCount; i++)
    {
        float weight = weights[i];
        moment2 += weight * weight;
 
        float2 sampleUV = uv + velocity * (phases[i] - 0.5) * 2.0;
		float2 lastUV = GetTextureUVs(sampleUV, texFreq, seeds[i]);
        colorIn += SAMPLE_TEXTURE2D(tex, _sampleState, lastUV) * weight;
		colorOut = colorIn;
    }
}
