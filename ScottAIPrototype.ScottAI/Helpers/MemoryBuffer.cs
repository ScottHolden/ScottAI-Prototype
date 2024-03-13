using System.Runtime.InteropServices;
using Windows.Foundation;
using WinRT;

namespace VoiceChat;

internal class MemoryBufferHelpers
{
	public static unsafe byte[] GetArrayBuffer(IMemoryBuffer memoryBuffer)
	{
		using IMemoryBufferReference memoryBufferReference = memoryBuffer.CreateReference();
		if (memoryBufferReference.Capacity <= 0) throw new Exception("Buffer is empty");

		var memoryBufferByteAccess = memoryBufferReference.As<IMemoryBufferByteAccess>() ?? throw new Exception("Unable to get IMemoryBufferByteAccess");
		memoryBufferByteAccess.GetBuffer(out byte* arrayBuffer, out uint arrayBufferCapacity);

		byte[] bytes = new byte[arrayBufferCapacity];

		for (int i = 0; i < arrayBufferCapacity; i++)
			bytes[i] = arrayBuffer[i];

		return bytes;
	}

	public static unsafe MemoryBuffer BuildBuffer(byte[] data, uint offset, uint count)
	{
		var memoryBuffer = new MemoryBuffer(count);
		var memoryBufferReference = memoryBuffer.CreateReference();
		var memoryBufferByteAccess = memoryBufferReference.As<IMemoryBufferByteAccess>() ?? throw new Exception("Unable to get IMemoryBufferByteAccess");
		memoryBufferByteAccess.GetBuffer(out byte* arrayBuffer, out uint arrayBufferCapacity);
		for (int i = 0; i < count; i++)
			arrayBuffer[i] = offset + i < data.Length ? data[offset + i] : (byte)0;
		return memoryBuffer;
	}
}

[ComImport]
[Guid("5B0D3235-4DBA-4D44-865E-8F1D0E4FD04D")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
unsafe interface IMemoryBufferByteAccess
{
	void GetBuffer(out byte* buffer, out uint capacity);
}

[ComImport]
[Guid("905A0FEF-BC53-11DF-8C49-001E4FC686DA")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
unsafe interface IBufferByteAccess
{
	void Buffer(out byte* buffer);
}
