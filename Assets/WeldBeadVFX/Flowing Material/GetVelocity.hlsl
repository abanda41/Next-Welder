void GetVelocity_float(float2 uv, out float2 velocity)
{
    float2 diff0 = uv - float2(0.2, 0.5);
    float2 diff1 = uv - float2(0.8, 0.5);

    float charge0 = -0.0005;
    float charge1 = -0.0005;

    float eps = 0.01;
    velocity = 
        normalize(diff0) * charge0 / (dot(diff0, diff0) + eps) +
        normalize(diff1) * charge1 / (dot(diff1, diff1) + eps);
}