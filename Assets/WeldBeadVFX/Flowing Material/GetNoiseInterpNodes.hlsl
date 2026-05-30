void GetNoiseInterpNodes_float(float smoothNoise, 
                                    out float2 seeds,
                                    out float2 weights,
                                    out float2 phases)
{
    float2 globalPhases = (smoothNoise * 0.5f).xx + float2(0.5f, 0.0f);
    phases       = frac(globalPhases);
    seeds        = floor(globalPhases) * 2.0f + float2(0.0f, 1.0f);
    weights      = min(phases, 1.0f - phases) * 2.0f;
}