using Microsoft.Extensions.Logging;
using SkiaSharp;
using System.Runtime.InteropServices;

namespace ScottAIPrototype;

public class FixedImageVideoRenderer : IVideoRenderer
{
    private readonly ILogger _logger;
    public RenderSize RenderSize { get; }
    private SKBitmap? _fixedBitmap;
    private byte[]? _rgbaSource;
    private readonly string _imagePath;
    public FixedImageVideoRenderer(RenderSize renderSize, string imagePath, ILogger logger)
    {
        RenderSize = renderSize;
        _logger = logger;
        _imagePath = imagePath;
    }

    public void Dispose()
    {
        _fixedBitmap?.Dispose();
    }

    public void Init()
    {
        try
        {
            _logger.LogInformation("Loading fixed image: {path}", _imagePath);
            using var baseImage = SKBitmap.Decode(File.ReadAllBytes(_imagePath));
            _fixedBitmap = new SKBitmap((int)RenderSize.Width, (int)RenderSize.Height, SKColorType.Rgba8888, SKAlphaType.Premul);
            using (SKCanvas canvas = new(_fixedBitmap))
            {
                SKRect sourceRect = new(0, 0, baseImage.Width, baseImage.Height);
                SKRect destRect = new(0, 0, _fixedBitmap.Width, _fixedBitmap.Height);
                _logger.LogInformation("Scaling from {source} to {dest}", sourceRect, destRect);
                canvas.DrawBitmap(baseImage, sourceRect, destRect);
            }
            _rgbaSource = _fixedBitmap.GetPixelSpan().ToArray();
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Unable to load and scale fixed image: {message}", e.Message);
            _fixedBitmap = null;
        }
    }

    public unsafe void Render(byte* arrayBuffer)
    {
        if (_rgbaSource != null && _rgbaSource.Length > 0)
        {
            Marshal.Copy(_rgbaSource, 0, (IntPtr)arrayBuffer, _rgbaSource.Length);
        }
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
