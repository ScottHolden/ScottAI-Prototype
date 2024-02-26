using Microsoft.Extensions.Logging;

namespace ScottAIPrototype;

public class FixedImageVideoRenderer : IVideoRenderer
{
    private readonly ILogger _logger;
    public RenderSize RenderSize { get; }
    public FixedImageVideoRenderer(RenderSize renderSize, string imagePath, ILogger logger)
    {
        RenderSize = renderSize;
        _logger = logger;
    }

    public void Dispose()
    {
    }

    public void Init()
    {
    }

    public unsafe void Render(byte* arrayBuffer)
    {
        throw new NotImplementedException();
    }
}
