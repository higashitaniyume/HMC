using System.Runtime.InteropServices;

namespace HMC.Agent.Native;

/// <summary>
/// P/Invoke 声明，用于获取 TCP 连接表。
/// </summary>
internal static class Iphlpapi
{
    public const int AF_INET = 2;

    public const int TCP_TABLE_OWNER_PID_ALL = 5;

    [DllImport("iphlpapi.dll", SetLastError = true)]
    public static extern uint GetExtendedTcpTable(
        IntPtr pTcpTable,
        ref int dwOutBufLen,
        bool order,
        int ulAf,
        int tableClass,
        uint reserved = 0);

    [StructLayout(LayoutKind.Sequential)]
    public struct MibTcpRowOwnerPid
    {
        public TcpState state;
        public uint localAddr;
        public int localPort;
        public uint remoteAddr;
        public int remotePort;
        public int owningPid;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MibTcpTableOwnerPid
    {
        public uint dwNumEntries;
        // 后面跟 MibTcpRowOwnerPid 数组
        public MibTcpRowOwnerPid table;
    }

    public enum TcpState
    {
        Closed = 1,
        Listen = 2,
        SynSent = 3,
        SynRcvd = 4,
        Established = 5,
        FinWait1 = 6,
        FinWait2 = 7,
        CloseWait = 8,
        Closing = 9,
        LastAck = 10,
        TimeWait = 11,
        DeleteTcb = 12
    }
}
