using System.Runtime.InteropServices;
using System.Text;

namespace GammaRay.Core.Utils;

public ref struct BufferWriter(Span<byte> buffer, int initialUsedLength = 0)
{
	public Span<byte> Buffer { get; } = buffer;

	public int UsedLength { get; private set; } = initialUsedLength;

	public readonly Span<byte> UsedBufferPart => Buffer[..UsedLength];

	public readonly Span<byte> UnusedBufferPart => Buffer[UsedLength..];


	public void WriteInt(int value)
	{
		if (BitConverter.TryWriteBytes(UnusedBufferPart, value) == false)
			throw new InternalBufferOverflowException();
		UsedLength += sizeof(int);
	}

	public void WriteUShort(ushort value)
	{
		if (BitConverter.TryWriteBytes(UnusedBufferPart, value) == false)
			throw new InternalBufferOverflowException();
		UsedLength += sizeof(ushort);
	}

	public void WriteByte(byte value)
	{
		UnusedBufferPart[0] = value;
		UsedLength += sizeof(byte);
	}

	public void WriteGuid(Guid id)
	{
		var bytes = MemoryMarshal.AsBytes(MemoryMarshal.CreateSpan(ref id, 1));
		bytes.CopyTo(UnusedBufferPart);
		UsedLength += 16;
	}

	public void WriteDateTime(DateTime dateTime)
	{
		var ticks = dateTime.Ticks;
		if (BitConverter.TryWriteBytes(UnusedBufferPart, ticks) == false)
			throw new InternalBufferOverflowException();
		UsedLength += sizeof(long);
	}

	public void WriteStringWithLength(ReadOnlySpan<char> str, Encoding encoding)
	{
		var buffer = UnusedBufferPart;
		var wrote = encoding.GetBytes(str, buffer[4..]);
		WriteInt(wrote);
		UsedLength += wrote;
	}

	public void WriteString(ReadOnlySpan<char> str, Encoding encoding)
	{
		var wrote = encoding.GetBytes(str, UnusedBufferPart);
		UsedLength += wrote;
	}

	public void WriteBoolean(bool value) => WriteByte((byte)(value ? 1 : 0));

	public void Advance(int advance)
	{
		UsedLength += advance;
	}
}
