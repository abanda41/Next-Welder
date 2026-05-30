void hash33_float(float3 p, out float3 randomPos)
{
	p = float3(dot(p,float3(127.1,311.7, 74.7)),
			  dot(p,float3(269.5,183.3,246.1)),
			  dot(p,float3(113.5,271.9,124.6)));

	randomPos = frac(sin(p)*43758.5453123);
}