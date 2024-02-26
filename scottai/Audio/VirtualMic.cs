using Azure.Communication.Calling.WindowsClient;
using Windows.Foundation;
using WinRT;

namespace VoiceChat;

internal class VirtualMic
{
	private readonly Thread _thread;
	private byte[]? _next = null;
	private readonly TaskCompletionSource _startedTaskCompletionSource = new();
	private readonly CancellationTokenSource _cancellationTokenSource = new();
	private RawOutgoingAudioStream? _stream;

	public event EventHandler? StartSpeaking;
	public event EventHandler? StopSpeaking;
	public event EventHandler<float>? SpeechAmplitude;

	public VirtualMic()
	{
		_thread = new Thread(Run);
	}
	public Task StartAsync(RawOutgoingAudioStream stream)
	{
		if (!_startedTaskCompletionSource.Task.IsCompleted && !_thread.IsAlive)
		{
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
	public void SpeakNow(byte[] data)
	{
		_next = data;
	}
	public void MaybeSpeak(byte[] data)
	{
		_next ??= data;
	}
	public void Interrupt()
	{
		_next = Array.Empty<byte>();
	}

	private unsafe void Run()
	{
		if (_stream == null)
		{
			_startedTaskCompletionSource.SetException(new ArgumentNullException("RawOutgoingAudioStream was null"));
			return;
		}
		var stride = (uint)_stream.ExpectedBufferSizeInBytes;

		using var memoryBuffer = new MemoryBuffer(stride);
		using var memoryBufferReference = memoryBuffer.CreateReference();
		var memoryBufferByteAccess = memoryBufferReference.As<IMemoryBufferByteAccess>() ?? throw new Exception("Unable to get IMemoryBufferByteAccess");
		memoryBufferByteAccess.GetBuffer(out byte* arrayBuffer, out uint arrayBufferCapacity);

		_startedTaskCompletionSource.SetResult();
		var start = DateTime.Now;
		byte[]? current = null;
		uint index = 0;

		while (!_cancellationTokenSource.IsCancellationRequested)
		{
			if (_next != null)
			{
				current = _next;
				_next = null;
				index = 0;

				if (current.Length <= 0)
				{
					current = null;
					StopSpeaking?.Invoke(this, EventArgs.Empty);
				}
				else
				{
					StartSpeaking?.Invoke(this, EventArgs.Empty);
				}
			}
			if (current != null)
			{
				// Audio ampl, may remove depending on performance
				short maxV = 0;
				for (int i = (int)index; i < index + stride && (i + 2) < current.Length; i += 2)
				{
					short v = BitConverter.ToInt16(current, i);
					if (v > maxV) maxV = v;
				}
				SpeechAmplitude?.Invoke(this, ((float)maxV) / short.MaxValue);

				for (int i = 0; i < stride; i++)
					arrayBuffer[i] = index + i < current.Length ? current[index + i] : (byte)0;

				var audioBuffer = new RawAudioBuffer()
				{
					Buffer = memoryBuffer
				};
				_stream.SendRawAudioBufferAsync(audioBuffer).AsTask().Wait();

				index += stride;
				if (index >= current?.Length)
				{
					current = null;
					StopSpeaking?.Invoke(this, EventArgs.Empty);
				}
			}
			int nextIn = (int)(20 - (DateTime.Now - start).TotalMilliseconds);
			if (nextIn > 0) Thread.Sleep(nextIn);
			start = DateTime.Now;
		}
	}
}
