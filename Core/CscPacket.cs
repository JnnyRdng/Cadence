using System.Buffers.Binary;

namespace Core;

public static class CscPacket
{
    // Flags byte values
    private const byte FlagWheelRevPresent = 0x01;
    private const byte FlagCrankRevPresent = 0x02;
    private const byte FlagBothValuesPresent = 0x03;
    
    public static byte[] EncodeCrankRevolutions(
        ushort cumulativeCrankRevolutions,
        ushort lastCrankEventTime1024)
    {
        var buffer = new byte[5];
        buffer[0] = FlagCrankRevPresent;
        BinaryPrimitives.WriteUInt16LittleEndian(buffer.AsSpan(1, 2), cumulativeCrankRevolutions);
        BinaryPrimitives.WriteUInt16LittleEndian(buffer.AsSpan(3, 2), lastCrankEventTime1024);
        return buffer;
    }
}