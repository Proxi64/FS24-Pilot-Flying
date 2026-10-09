using System.Runtime.InteropServices;

namespace PilotFlying.Experiments.FenixControls;

/// <summary>
/// Native declarations from SimConnect.h (MSFS 2024 SDK, #pragma pack(1)); values checked in the SDK header.
/// The SDK's managed DLL targets .NET Framework 4.6.1, so the native SimConnect.dll (x64) is called directly.
/// </summary>
internal static class Native
{
    private const string Dll = "SimConnect.dll";

    public const uint RECV_ID_EXCEPTION = 1;
    public const uint RECV_ID_QUIT = 3;
    public const uint RECV_ID_SIMOBJECT_DATA = 8;
    public const uint RECV_ID_SYSTEM_STATE = 15;
    public const uint RECV_ID_CLIENT_DATA = 16;
    public const uint RECV_ID_ENUMERATE_INPUT_EVENTS = 34;
    public const uint RECV_ID_GET_INPUT_EVENT = 35;
    public const uint RECV_ID_SUBSCRIBE_INPUT_EVENT = 36;
    public const uint DATA_REQUEST_FLAG_CHANGED = 1;
    public const uint INPUT_EVENT_TYPE_DOUBLE = 0;

    public const uint OBJECT_ID_USER = 0;
    public const uint UNUSED = uint.MaxValue;
    public const int DATATYPE_FLOAT64 = 4;
    public const int PERIOD_SIM_FRAME = 3;
    public const int CLIENT_DATA_PERIOD_ON_SET = 3;
    public const uint CLIENT_DATA_REQUEST_FLAG_CHANGED = 1;

    [DllImport(Dll, CharSet = CharSet.Ansi)]
    public static extern int SimConnect_Open(out IntPtr phSimConnect, string szName, IntPtr hWnd, uint userEventWin32, IntPtr hEventHandle, uint configIndex);

    [DllImport(Dll)]
    public static extern int SimConnect_Close(IntPtr hSimConnect);

    [DllImport(Dll)]
    public static extern int SimConnect_GetNextDispatch(IntPtr hSimConnect, out IntPtr ppData, out uint pcbData);

    [DllImport(Dll, CharSet = CharSet.Ansi)]
    public static extern int SimConnect_AddToDataDefinition(IntPtr hSimConnect, uint defineId, string datumName, string? unitsName,
        int datumType, float epsilon, uint datumId);

    [DllImport(Dll)]
    public static extern int SimConnect_RequestDataOnSimObject(IntPtr hSimConnect, uint requestId, uint defineId, uint objectId,
        int period, uint flags, uint origin, uint interval, uint limit);

    [DllImport(Dll, CharSet = CharSet.Ansi)]
    public static extern int SimConnect_RequestSystemState(IntPtr hSimConnect, uint requestId, string state);

    [DllImport(Dll, CharSet = CharSet.Ansi)]
    public static extern int SimConnect_MapClientDataNameToID(IntPtr hSimConnect, string clientDataName, uint clientDataId);

    [DllImport(Dll)]
    public static extern int SimConnect_AddToClientDataDefinition(IntPtr hSimConnect, uint defineId, uint offset, uint sizeOrType,
        float epsilon, uint datumId);

    [DllImport(Dll)]
    public static extern int SimConnect_RequestClientData(IntPtr hSimConnect, uint clientDataId, uint requestId, uint defineId,
        int period, uint flags, uint origin, uint interval, uint limit);

    [DllImport(Dll)]
    public static extern int SimConnect_SetClientData(IntPtr hSimConnect, uint clientDataId, uint defineId, uint flags, uint reserved,
        uint unitSize, byte[] data);

    /// <summary>SIMCONNECT_GROUP_PRIORITY_HIGHEST, used with SIMCONNECT_EVENT_FLAG_GROUPID_IS_PRIORITY.</summary>
    public const uint GROUP_PRIORITY_HIGHEST = 1, EVENT_FLAG_GROUPID_IS_PRIORITY = 0x10;

    [DllImport(Dll, CharSet = CharSet.Ansi)]
    public static extern int SimConnect_MapClientEventToSimEvent(IntPtr hSimConnect, uint eventId, string eventName);

    [DllImport(Dll)]
    public static extern int SimConnect_TransmitClientEvent(IntPtr hSimConnect, uint objectId, uint eventId, uint data, uint groupId, uint flags);

    [DllImport(Dll)]
    public static extern int SimConnect_GetLastSentPacketID(IntPtr hSimConnect, out uint pdwError);

    [DllImport(Dll)]
    public static extern int SimConnect_SetDataOnSimObject(IntPtr hSimConnect, uint defineId, uint objectId, uint flags,
        uint arrayCount, uint unitSize, ref double data);

    // Input events (the "B:" variables of MSFS 2024 cockpits).
    [DllImport(Dll)]
    public static extern int SimConnect_EnumerateInputEvents(IntPtr hSimConnect, uint requestId);

    [DllImport(Dll)]
    public static extern int SimConnect_GetInputEvent(IntPtr hSimConnect, uint requestId, ulong hash);

    [DllImport(Dll)]
    public static extern int SimConnect_SetInputEvent(IntPtr hSimConnect, ulong hash, uint unitSize, ref double value);

    [DllImport(Dll)]
    public static extern int SimConnect_SubscribeInputEvent(IntPtr hSimConnect, ulong hash);

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

    /// <summary>SIMCONNECT_RECV_SIMOBJECT_DATA, also SIMCONNECT_RECV_CLIENT_DATA (data follows immediately).</summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct RecvData
    {
        public Recv Header;
        public uint dwRequestID, dwObjectID, dwDefineID, dwFlags, dwentrynumber, dwoutof, dwDefineCount;
    }

    /// <summary>SIMCONNECT_RECV_LIST_TEMPLATE header of SIMCONNECT_RECV_ENUMERATE_INPUT_EVENTS (descriptors follow).</summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct RecvList
    {
        public Recv Header;
        public uint dwRequestID, dwArraySize, dwEntryNumber, dwOutOf;
    }

    /// <summary>SIMCONNECT_INPUT_EVENT_DESCRIPTOR: Name char[64], Hash, eType (76 bytes).</summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public unsafe struct InputEventDescriptor
    {
        public fixed byte Name[64];
        public ulong Hash;
        public uint eType;
    }

    /// <summary>SIMCONNECT_RECV_GET_INPUT_EVENT (the value follows: double or string).</summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct RecvGetInputEvent
    {
        public Recv Header;
        public uint dwRequestID, eType;
    }

    /// <summary>SIMCONNECT_RECV_SUBSCRIBE_INPUT_EVENT (the value follows: double or string).</summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct RecvSubscribeInputEvent
    {
        public Recv Header;
        public ulong Hash;
        public uint eType;
    }

    /// <summary>SIMCONNECT_RECV_SYSTEM_STATE (szString follows: char[MAX_PATH]).</summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct RecvSystemState
    {
        public Recv Header;
        public uint dwRequestID, dwInteger;
        public float fFloat;
    }
}
