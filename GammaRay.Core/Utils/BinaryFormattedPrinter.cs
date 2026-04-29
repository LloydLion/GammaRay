using System.Text;

namespace GammaRay.Core.Utils;

public static class BinaryFormattedPrinter
{
	private static readonly char[] HexNumbers = ['0', '1', '2', '3', '4', '5', '6', '7', '8', '9', 'A', 'B', 'C', 'D', 'E', 'F'];


	public static void Print(ReadOnlySpan<byte> data, TextWriter output)
	{
		Span<char> charBuffer = stackalloc char[16];

		while (data.Length != 0)
		{
			var toPrint = Math.Min(16, data.Length);
			var buffer = data[..toPrint];
			data = data[toPrint..];

			for (int i = 0; i < toPrint; i++)
			{
				var byteToPrint = buffer[i];
				var lowBits = (byte)(byteToPrint & 0x0F);
				var hiBits = (byte)(byteToPrint >> 4);

				output.Write(HexNumbers[hiBits]);
				output.Write(HexNumbers[lowBits]);
				output.Write(' ');
			}

			Encoding.ASCII.GetChars(buffer, charBuffer);
			for (int i = 0; i < toPrint; i++)
				if (char.IsSymbol(charBuffer[i]) == false)
					charBuffer[i] = '.';
			output.Write(charBuffer);

			output.WriteLine();
		}
	}
}
