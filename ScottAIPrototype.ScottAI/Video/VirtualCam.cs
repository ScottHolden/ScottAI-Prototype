﻿using Azure.Communication.Calling.WindowsClient;
using Microsoft.Extensions.Logging;
using ScottAIPrototype;
using Windows.Foundation;
using WinRT;

namespace VoiceChat;

internal class VirtualCam
{
    private readonly Thread _thread;
    private readonly TaskCompletionSource _startedTaskCompletionSource = new();
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private VirtualOutgoingVideoStream? _stream;
    private RenderSize? _renderSize;
    private readonly IVideoRenderer? _renderer;
    private readonly ILogger _logger;
    public VirtualCam(IVideoRenderer? renderer, ILogger logger)
    {
        _renderer = renderer;
        _logger = logger;
        _thread = new Thread(Run);
    }
    public Task StartAsync(VirtualOutgoingVideoStream stream, RenderSize renderSize)
    {
        if (!_startedTaskCompletionSource.Task.IsCompleted && !_thread.IsAlive)
        {
            _renderSize = renderSize;
            _stream = stream;
            _thread.Start();
        }
        return _startedTaskCompletionSource.Task;
    }
    public Task StopAsync()
    {
        _cancellationTokenSource.Cancel();
        return Task.Run(_thread.Join);
    }
    private unsafe void Run()
    {
        if (_renderer == null || _renderSize == null) return;
        try
        {
            _logger.LogInformation("VC Starting...");
            if (_stream == null)
            {
                _startedTaskCompletionSource.SetException(new ArgumentNullException("VirtualOutgoingVideoStream was null"));
                return;
            }

            using var memoryBuffer = new MemoryBuffer(4 * _renderSize.Width * _renderSize.Height);
            using var memoryBufferReference = memoryBuffer.CreateReference();
            var memoryBufferByteAccess = memoryBufferReference.As<IMemoryBufferByteAccess>() ?? throw new Exception("Unable to get IMemoryBufferByteAccess");

            _renderer.Init(_renderSize);

            _logger.LogInformation("VC Started");
            _startedTaskCompletionSource.SetResult();


            var ts = DateTime.Now;
            while (!_cancellationTokenSource.IsCancellationRequested)
            {
                var start = DateTime.Now;
                if (_stream.State != VideoStreamState.Started)
                {
                    Thread.Sleep(33);
                    continue;
                }

                // Prepare memory
                memoryBufferByteAccess.GetBuffer(out byte* arrayBuffer, out uint _);

                // Draw
                _renderer.Render(arrayBuffer);

                // Transmit
                var rawVideoFrame = new RawVideoFrameBuffer()
                {
                    StreamFormat = _stream.Format,
                    Buffers = new MemoryBuffer[] { memoryBuffer }
                };
                // Recheck for race conditions
                if (_stream.State == VideoStreamState.Started)
                    _stream.SendRawVideoFrameAsync(rawVideoFrame).AsTask().Wait();

                // Sleep
                int nextIn = (int)(33 - (DateTime.Now - start).TotalMilliseconds);
                if (nextIn > 0) Thread.Sleep(nextIn);
            }
            _logger.LogInformation("VC Stopping...");
        }
        catch (Exception ex)
        {
            if (!_startedTaskCompletionSource.Task.IsCompleted)
                _startedTaskCompletionSource.SetException(ex);
        }
        finally
        {
            _renderer.Dispose();
            _logger.LogInformation("VC Stopped");
        }
    }
}
