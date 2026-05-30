const float pi = 3.141592f;

void GetTextureUVs_float(float2 pos, float freq, float3 hash, out float2 uv)
{
	float ang = hash.x * 2.0 * pi;
    float2x2 rotation = float2x2(cos(ang), sin(ang), -sin(ang), cos(ang));
    
    uv = mul(rotation, pos * freq) + hash.yz;
}