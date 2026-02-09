using System;

namespace RendererCore.Integrators;

public static class StepPolicy
{
    public static float ComputeDt(float kmax, float epsPos, float dtMin, float dtMax)
    {
        if (kmax <= 0f)
        {
            return dtMax;
        }

        var dt = MathF.Sqrt(2f * epsPos / MathF.Max(1e-8f, kmax));
        return Math.Clamp(dt, dtMin, dtMax);
    }
}
