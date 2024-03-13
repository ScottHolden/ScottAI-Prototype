namespace ScottAIPrototype;

public interface IVideoRenderer
{
    void Init(RenderSize renderSize);
    unsafe void Render(byte* arrayBuffer);
    void Dispose();
    void FadeIn();
    void FadeOut();
    void SetAs(bool active);
    void SetAmp(float value);
}

public record RenderSize(uint Width, uint Height);