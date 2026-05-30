void hsv2rgb_float(float3 c, out float3 color)
{
	float3 rgb = clamp( abs(fmod(c.x*6.0+float3(0.0,4.0,2.0),6.0)-3.0)-1.0, 0.0, 1.0 );
	color = c.z * lerp( float3(1.0,1.0,1.0), rgb, c.y);
}