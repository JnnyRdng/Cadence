namespace Core.Test;

public class CscPacketTests
{
    [Fact]
    public void EncodesFlagsByteAsCrankOnly()
    {
        var bytes = CscPacket.EncodeCrankRevolutions(0, 0);
        bytes[0].ShouldBe((byte)0x02);
    }

    [Fact]
    public void EncodesLengthOfFiveBytes()
    {
        var bytes = CscPacket.EncodeCrankRevolutions(0, 0);
        bytes.Length.ShouldBe(5);
    }

    [Fact]
    public void EncodesValuesLittleEndian()
    {
        var bytes = CscPacket.EncodeCrankRevolutions(0x1234, 0xABCD);
        bytes.ShouldBe(new byte[] { 0x02, 0x34, 0x12, 0xCD, 0xAB });
    }

    [Fact]
    public void EncodesMaxValuesWithoutOverflow()
    {
        var bytes = CscPacket.EncodeCrankRevolutions(ushort.MaxValue, ushort.MaxValue);
        bytes.ShouldBe(new byte[] { 0x02, 0xFF, 0xFF, 0xFF, 0xFF });
    }

    [Fact]
    public void EncodesZeroValuesCleanly()
    {
        var bytes = CscPacket.EncodeCrankRevolutions(0, 0);
        bytes.ShouldBe(new byte[] { 0x02, 0x00, 0x00, 0x00, 0x00 });
    }
}