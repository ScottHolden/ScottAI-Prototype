using Microsoft.Extensions.Logging;
using SkiaSharp;

namespace ScottAIPrototype;

public class FixedImageVideoRenderer : IVideoRenderer
{
    private readonly ILogger _logger;
    public RenderSize RenderSize { get; }
    public SKBitmap _fixedBitmap;
    public FixedImageVideoRenderer(RenderSize renderSize, string imagePath, ILogger logger)
    {
        RenderSize = renderSize;
        _logger = logger;

        // Todo: shift to init
        var baseImage = SKBitmap.Decode(File.ReadAllBytes(imagePath));
        _fixedBitmap = new SKBitmap((int)renderSize.Width, (int)renderSize.Height);
        using SKCanvas canvas = new(_fixedBitmap);
        SKRect sourceRect = new(0, 0, baseImage.Width, baseImage.Height);
        SKRect destRect = new(0, 0, _fixedBitmap.Width, _fixedBitmap.Height);
        canvas.DrawBitmap(baseImage, sourceRect, destRect);
    }

    public void Dispose()
    {
    }

    public void Init()
    {
    }

    public unsafe void Render(byte* arrayBuffer)
    {
        _fixedBitmap.GetPixelSpan().CopyTo(new Span<byte>(arrayBuffer, 1));
    }

    public void FadeIn()
    {
    }

    public void FadeOut()
    {
    }

    public void SetAs(bool active)
    {
    }

    public void SetAmp(float value)
    {
    }
}
