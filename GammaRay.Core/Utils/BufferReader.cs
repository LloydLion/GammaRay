using System.Runtime.InteropServices;
using System.Text;

namespace GammaRay.Core.Utils;

public ref struct BufferReader(ReadOnlySpan<byte> buffer, int initialReadLength = 0)
{
	public readonly ReadOnlySpan<byte> Buffer { get; } = buffer;

	public int ReadLength { readonly get; private set; } = initialReadLength;

	public readonly int RemainingLength => Buffer.Length - ReadLength;

	public readonly ReadOnlySpan<byte> ReadBufferPart => Buffer[..ReadLength];

	public readonly ReadOnlySpan<byte> UnreadBufferPart => Buffer[ReadLength..];


	public int ReadInt()
	{
		var value = BitConverter.ToInt32(UnreadBufferPart);
		Advance(sizeof(int));
		return value;
	}

	public ushort ReadUShort()
	{
		var value = BitConverter.ToUInt16(UnreadBufferPart);
		Advance(sizeof(ushort));
		return value;
	}

	public byte ReadByte()
	{
		var value = UnreadBufferPart[0];
		Advance(sizeof(byte));
		return value;
	}

	public Guid ReadGuid()
	{
		var value = MemoryMarshal.AsRef<Guid>(UnreadBufferPart[..16]);
		Advance(16);
		return value;
	}

	public DateTime ReadDateTime()
	{
		var ticks = BitConverter.ToInt64(UnreadBufferPart);
		Advance(sizeof(long));
		return new DateTime(ticks);
	}

	public string ReadStringWithLength(Encoding encoding)
	{
		var length = ReadInt();
		return ReadString(encoding, length);
	}

	public string ReadString(Encoding encoding, int length)
	{
		var value = encoding.GetString(UnreadBufferPart[..length]);
		Advance(length);
		return value;
	}

	public string ReadStringToEnd(Encoding encoding)
	{
		var value = encoding.GetString(UnreadBufferPart);
		ReadLength = Buffer.Length;
		return value;
	}

	public void Advance(int advance)
	{
		ReadLength += advance;
	}
}
