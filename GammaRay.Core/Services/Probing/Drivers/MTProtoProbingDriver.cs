using GammaRay.Core.Network.Flow;
using GammaRay.Core.Utils;
using System.Buffers;
using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace GammaRay.Core.Services.Probing.Drivers;

[RecommendedDriverName("MTProto")]
public sealed class MTProtoProbingDriver : IProbeDriver
{
	private readonly ArrayPool<byte> _pool = ArrayPool<byte>.Create();

	
	public async Task<ProbeResult> ProbeAsync(ProbingArgs args)
	{
		var (flow, time, options) = (args.TargetOutcomingFlow, args.TimeProvider, args.Options);

		if (flow is not IStreamDataFlow streamDataFlow)
			throw new ArgumentException("Only stream based data flows supported", nameof(args));

		var start = time.GetTimestamp();
		var resultHelper = new ResultHelper(time, start);

		var readingOptions = new DataFlowReadingOptions() { Timeout = options.RTTTimeout };
		var writingOptions = new DataFlowWritingOptions() { Timeout = options.RTTTimeout };

		var messageBuffer = _pool.Rent(1024);

		try
		{
			var request = new RequestMessage(CreateMessageId(time));
			var nonceSpan = MemoryMarshal.AsBytes(MemoryMarshal.CreateSpan(ref request.Nonce, 1));
			RandomNumberGenerator.Fill(nonceSpan);

			MemoryMarshal.AsBytes(MemoryMarshal.CreateSpan(ref request, 1)).CopyTo(messageBuffer);

			await streamDataFlow.WriteAsync(messageBuffer.AsMemory(..Marshal.SizeOf<RequestMessage>()), writingOptions);

			var read = await streamDataFlow.ReadAsync(messageBuffer, readingOptions);
			if (read <= 0)
				return resultHelper.L6Failure(ProbeResult.CommunicationStatus.FlowFailure);

			var responseBin = messageBuffer.AsSpan(..read);
			var success = ContainsResPQ(responseBin);

			if (success)
				return resultHelper.Success();
			else
				return resultHelper.L6Failure(ProbeResult.CommunicationStatus.UnexceptedData, "ResPQ constructor not found in response");
		}
		catch (TimeoutException) { return resultHelper.L6Failure(ProbeResult.CommunicationStatus.Timeout); }
		catch (Exception ex) { return resultHelper.L6Failure(ProbeResult.CommunicationStatus.FlowFailure, ex.ToString()); }
		finally
		{
			_pool.Return(messageBuffer);
		}
	}

	private static bool ContainsResPQ(ReadOnlySpan<byte> buffer)
	{
		var reqPQConstructor = ToLE(0x05162463);
		for (int i = 0; i <= buffer.Length - 4; i++)
		{
			ref readonly uint data = ref MemoryMarshal.AsRef<uint>(buffer[i..(i + 4)]);
			if (data == reqPQConstructor)
				return true;
		}
		return false;
	}

	private static uint ToLE(uint value)
	{
		if (BitConverter.IsLittleEndian)
			return value;
		return BinaryPrimitives.ReverseEndianness(value);
	}

	private static ulong CreateMessageId(TimeProvider time)
	{
		long timestamp = time.GetTimestamp();
		return (ulong)(timestamp << 32);
	}

	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	private struct RequestMessage(ulong messageId)
	{
		public byte TransportType = 0xEF;

		// Message header
		public byte PacketLength = 10;
		public ulong AuthKeyId = 0;
		public ulong MessageId = messageId;
		public uint BodyLength = ToLE(20);

		// Body
		public uint ReqPQMulti = ToLE(0xbe7e8ef1);
		public UInt128 Nonce;
	}

	private readonly struct ResultHelper(TimeProvider _time, long _startTime)
	{
		public ProbeResult Success()
		{
			var result = new ProbeResult(ProbeResult.CommunicationStatus.Skipped, ProbeResult.CommunicationStatus.Success, _time.GetElapsedTime(_startTime));
			return result;
		}

		public ProbeResult L6Failure(ProbeResult.CommunicationStatus status, string? comment = null)
		{
			return new ProbeResult(ProbeResult.CommunicationStatus.Skipped, status, _time.GetElapsedTime(_startTime))
				{ FailureComment = comment };
		}
	}
}
