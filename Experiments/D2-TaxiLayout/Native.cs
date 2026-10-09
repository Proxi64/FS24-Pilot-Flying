using System.Runtime.InteropServices;
using System.Text;

namespace PilotFlying.Experiments.TaxiLayout;

/// <summary>
/// Native declarations from SimConnect.h (MSFS 2024 SDK, #pragma pack(1)).
/// The SDK's managed DLL targets .NET Framework 4.6.1, so the native SimConnect.dll (x64) is called directly.
/// </summary>
internal static unsafe class Native
{
    private const string Dll = "SimConnect.dll";

    public const uint RECV_ID_EXCEPTION = 1;
    public const uint RECV_ID_OPEN = 2;
    public const uint RECV_ID_QUIT = 3;
    public const uint RECV_ID_FACILITY_DATA = 28;
    public const uint RECV_ID_FACILITY_DATA_END = 29;

    // SIMCONNECT_FACILITY_DATA_TYPE (14, 15, 16 already verified in practice; 2 and 17 to be confirmed by this experiment)
    public const uint DATA_AIRPORT = 0;
    public const uint DATA_RUNWAY = 1;
    public const uint DATA_START = 2;
    public const uint DATA_TAXI_POINT = 14;
    public const uint DATA_TAXI_PARKING = 15;
    public const uint DATA_TAXI_PATH = 16;
    public const uint DATA_TAXI_NAME = 17;

    [DllImport(Dll, CharSet = CharSet.Ansi)]
    public static extern int SimConnect_Open(out IntPtr phSimConnect, string szName, IntPtr hWnd, uint userEventWin32, IntPtr hEventHandle, uint configIndex);

    [DllImport(Dll)]
    public static extern int SimConnect_Close(IntPtr hSimConnect);

    [DllImport(Dll)]
    public static extern int SimConnect_GetNextDispatch(IntPtr hSimConnect, out IntPtr ppData, out uint pcbData);

    [DllImport(Dll)]
    public static extern int SimConnect_GetLastSentPacketID(IntPtr hSimConnect, out uint pdwError);

    [DllImport(Dll, CharSet = CharSet.Ansi)]
    public static extern int SimConnect_AddToFacilityDefinition(IntPtr hSimConnect, uint defineId, string fieldName);

    [DllImport(Dll, CharSet = CharSet.Ansi)]
    public static extern int SimConnect_RequestFacilityData(IntPtr hSimConnect, uint defineId, uint requestId, string icao, string region);

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

    /// <summary>SIMCONNECT_RECV_FACILITY_DATA (without the Data field, which follows immediately).</summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct RecvFacilityData
    {
        public Recv Header;
        public uint UserRequestId, UniqueRequestId, ParentUniqueRequestId, Type, IsListItem, ItemIndex, ListSize;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct RecvFacilityDataEnd
    {
        public Recv Header;
        public uint RequestId;
    }
}

/// <summary>Sequential reading of the received fields, in definition order (packed data, no alignment).</summary>
internal unsafe struct FieldReader(byte* start)
{
    private byte* _p = start;

    public double Double() { var v = *(double*)_p; _p += 8; return v; }
    public float Float() { var v = *(float*)_p; _p += 4; return v; }
    public int Int() { var v = *(int*)_p; _p += 4; return v; }
    public uint UInt() { var v = *(uint*)_p; _p += 4; return v; }

    public string Text(int size)
    {
        var length = 0;
        while (length < size && _p[length] != 0) length++;
        var bytes = new ReadOnlySpan<byte>(_p, length);
        string text;
        try { text = new UTF8Encoding(false, true).GetString(bytes); }
        catch (DecoderFallbackException) { text = Encoding.Latin1.GetString(bytes); }
        _p += size;
        return text.Trim();
    }
}
