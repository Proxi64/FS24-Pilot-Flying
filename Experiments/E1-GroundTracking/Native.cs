using System.Runtime.InteropServices;

namespace PilotFlying.Experiments.GroundTracking;

/// <summary>
/// Native declarations from SimConnect.h (MSFS 2024 SDK, #pragma pack(1)).
/// The SDK's managed DLL targets .NET Framework 4.6.1, so the native SimConnect.dll (x64) is called directly.
/// </summary>
internal static class Native
{
    private const string Dll = "SimConnect.dll";

    public const uint RECV_ID_EXCEPTION = 1;
    public const uint RECV_ID_OPEN = 2;
    public const uint RECV_ID_QUIT = 3;
    public const uint RECV_ID_SIMOBJECT_DATA = 8;

    public const uint OBJECT_ID_USER = 0;
    public const uint UNUSED = uint.MaxValue;
    public const int DATATYPE_FLOAT64 = 4;
    public const int PERIOD_SIM_FRAME = 3;

    /// <summary>SIMCONNECT_GROUP_PRIORITY_HIGHEST, used with SIMCONNECT_EVENT_FLAG_GROUPID_IS_PRIORITY.</summary>
    public const uint GROUP_PRIORITY_HIGHEST = 1, EVENT_FLAG_GROUPID_IS_PRIORITY = 0x10;

    [DllImport(Dll, CharSet = CharSet.Ansi)]
    public static extern int SimConnect_Open(out IntPtr phSimConnect, string szName, IntPtr hWnd, uint userEventWin32, IntPtr hEventHandle, uint configIndex);

    [DllImport(Dll)]
    public static extern int SimConnect_Close(IntPtr hSimConnect);

    [DllImport(Dll)]
    public static extern int SimConnect_GetNextDispatch(IntPtr hSimConnect, out IntPtr ppData, out uint pcbData);

    [DllImport(Dll)]
    public static extern int SimConnect_GetLastSentPacketID(IntPtr hSimConnect, out uint pdwError);

    [DllImport(Dll, CharSet = CharSet.Ansi)]
    public static extern int SimConnect_AddToDataDefinition(IntPtr hSimConnect, uint defineId, string datumName, string? unitsName,
        int datumType, float epsilon, uint datumId);

    [DllImport(Dll)]
    public static extern int SimConnect_RequestDataOnSimObject(IntPtr hSimConnect, uint requestId, uint defineId, uint objectId,
        int period, uint flags, uint origin, uint interval, uint limit);

    [DllImport(Dll, CharSet = CharSet.Ansi)]
    public static extern int SimConnect_MapClientEventToSimEvent(IntPtr hSimConnect, uint eventId, string eventName);

    [DllImport(Dll)]
    public static extern int SimConnect_TransmitClientEvent(IntPtr hSimConnect, uint objectId, uint eventId, uint data, uint groupId, uint flags);

    /// <summary>Windows timer resolution (winmm), so that Thread.Sleep(1) really sleeps about 1 ms.</summary>
    [DllImport("winmm.dll")]
    public static extern uint timeBeginPeriod(uint period);

    [DllImport("winmm.dll")]
    public static extern uint timeEndPeriod(uint period);

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct Recv
    {
        public uint dwSize, dwVersion, dwID;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct RecvException
    {
        public Recv Header;
        public uint dwException, dwSendID, dwIndex;
    }

    /// <summary>SIMCONNECT_RECV_SIMOBJECT_DATA (without the dwData field, which follows immediately).</summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct RecvSimObjectData
    {
        public Recv Header;
        public uint dwRequestID, dwObjectID, dwDefineID, dwFlags, dwentrynumber, dwoutof, dwDefineCount;
    }
}
