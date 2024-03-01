namespace ScottAIPrototype;

public class LerpStep(float step, float min, float max, float target, float actual)
{
    public float Value => actual;
    public void SetTarget(float value) => target = value;
    public void Step()
    {
        if (actual < target) actual = Math.Min(actual + step, max);
        else if (actual > target) actual = Math.Max(actual - step, min);
    }
}