using System.Diagnostics;
using System.Runtime.InteropServices;

namespace GammaRay.Core.Utils;

using DWORD = Int32;
using ULONG = UInt32;

public static partial class TcpProcessLookup
{
	private static DWORD _socketTableBufferSize = 0;
	private static IntPtr _socketTableBuffer = IntPtr.Zero;


	public static int GetProcessIdByLocalPort(int port)
	{
		try
		{
			const DWORD ERROR_INSUFFICIENT_BUFFER = 122;
			const DWORD ERROR_INVALID_PARAMETER = 87;
			const DWORD AF_INET = 2;

		retry:
			var result = GetExtendedTcpTable(_socketTableBuffer, ref _socketTableBufferSize, true, AF_INET, TCP_TABLE_CLASS.TCP_TABLE_OWNER_PID_CONNECTIONS, 0);

			if (result == ERROR_INSUFFICIENT_BUFFER)
			{
				// Works fine with IntPtr.Zero
				_socketTableBuffer = Marshal.ReAllocHGlobal(_socketTableBuffer, _socketTableBufferSize);
				goto retry;
			}

			if (result == ERROR_INVALID_PARAMETER)
				return -2;

			int count = Marshal.ReadInt32(_socketTableBuffer);
			IntPtr rowPtr = _socketTableBuffer + sizeof(DWORD);
			int rowSize = Marshal.SizeOf<MIB_TCPROW_OWNER_PID>();

			for (int i = 0; i < count; i++)
			{
				var row = Marshal.PtrToStructure<MIB_TCPROW_OWNER_PID>(rowPtr + rowSize * i);

				var le = row.LocalPort;
				var be = (ushort)(le & 0xFF00) >> 8 | (ushort)(le & 0xFF) << 8;

				if (le == port || be == port)
					return (int)row.OwningPid;
			}

			return -1;
		}
		catch (Exception ex)
		{
			Debugger.BreakForUserUnhandledException(ex);
			return -3;
		}
	}

	[LibraryImport("iphlpapi.dll")]
	private static partial uint GetExtendedTcpTable(
		IntPtr pTcpTable,
		ref DWORD dwOutBufLen,
		[MarshalAs(UnmanagedType.Bool)] bool sort,
		ULONG ipVersion,
		TCP_TABLE_CLASS tableClass,
		ULONG reserved
	);

	private enum TCP_TABLE_CLASS : DWORD
	{
		TCP_TABLE_OWNER_PID_CONNECTIONS = 4
	}

	[StructLayout(LayoutKind.Sequential)]
	private struct MIB_TCPROW_OWNER_PID
	{
		public uint State;
		public uint LocalAddress;
		public uint LocalPort;
		public uint RemoteAddress;
		public uint RemotePort;
		public uint OwningPid;
	}
}
