namespace ScottAIPrototype;

public interface IVideoRenderer
{
    RenderSize RenderSize { get; }
    void Init();
    unsafe void Render(byte* arrayBuffer);
    void Dispose();
}

public record RenderSize(uint Width, uint Height);