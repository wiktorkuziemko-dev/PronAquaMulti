using System.Text;
using MultiRejestrator.Protocol;

namespace MultiRejestrator.Tests;

public class ProtocolTests
{
    [Fact]
    public void Crc_matches_ccitt_false_check_value()
    {
        Assert.Equal(0x29B1, Crc16Ccitt.Compute(Encoding.ASCII.GetBytes("123456789")));
    }

    [Fact]
    public void Request_frame_has_expected_layout()
    {
        var frame = new Frame(0x0001, 7, AsciiText.Encode("000000", 6), []);
        var bytes = frame.Encode();

        Assert.Equal(16, bytes.Length);
        Assert.Equal("PMU"u8.ToArray(), bytes[..3]);
        Assert.Equal(11, bytes[3] | (bytes[4] << 8)); // type 2 + id 1 + CRC 2 + hasło 6
        Assert.Equal(0x01, bytes[5]);
        Assert.Equal(0x00, bytes[6]);
        Assert.Equal(7, bytes[7]);
        Assert.Equal("000000"u8.ToArray(), bytes[10..16]);
    }

    [Fact]
    public void Response_frame_size_is_smaller_by_password_length()
    {
        var bytes = new Frame(0x8004, 3, null, [1, 0]).Encode();
        Assert.Equal(7, bytes[3] | (bytes[4] << 8)); // 5 + 2 B danych, bez hasła (odpowiedź 2.1)
        Assert.Equal(12, bytes.Length);
    }

    [Fact]
    public void Parser_skips_garbage_and_handles_split_chunks()
    {
        var frame = new Frame(0x8011, 9, null, [1, 0, 4, 0, 0x78, 0x56, 0x34, 0x12]);
        var bytes = frame.Encode();
        var parser = new FrameParser(framesHavePassword: false);

        Assert.Empty(parser.Feed([0x00, 0x50, 0x41]));
        Assert.Empty(parser.Feed(bytes[..5]));
        var result = parser.Feed(bytes[5..]);

        var parsed = Assert.Single(result);
        Assert.Equal(frame.Type, parsed.Type);
        Assert.Equal(frame.Id, parsed.Id);
        Assert.Equal(frame.Data, parsed.Data);
    }

    [Fact]
    public void Parser_rejects_corrupted_crc_and_resynchronises()
    {
        var good = new Frame(0x8001, 1, null, []).Encode();
        var bad = good.ToArray();
        bad[7] ^= 0xFF;
        var parser = new FrameParser(framesHavePassword: false);
        var discarded = new List<string>();
        parser.Discarded += (reason, _) => discarded.Add(reason);

        var frames = parser.Feed([.. bad, .. good]);

        Assert.Single(frames);
        Assert.Contains("błąd CRC", discarded);
    }

    [Fact]
    public void Parser_on_device_side_reads_password()
    {
        var bytes = new Frame(0x0011, 2, AsciiText.Encode("abc123", 6), [1, 0]).Encode();
        var frame = Assert.Single(new FrameParser(framesHavePassword: true).Feed(bytes));
        Assert.Equal("abc123", AsciiText.Read(frame.Password));
        Assert.Equal(new byte[] { 1, 0 }, frame.Data);
    }

    [Fact]
    public void ConfBlocks_round_trip()
    {
        var blocks = new[] { new ConfBlock(1, [0x78, 0x56, 0x34, 0x12]), new ConfBlock(1001, [5]) };
        var decoded = ConfBlock.DecodeMany(ConfBlock.EncodeMany(blocks));
        Assert.Equal(2, decoded.Count);
        Assert.Equal(1001, decoded[1].Id);
        Assert.Equal(new byte[] { 5 }, decoded[1].Value);
    }

    [Fact]
    public void PackBlocks_respects_ten_blocks_and_1024_bytes()
    {
        var small = Enumerable.Range(1, 25).Select(i => new ConfBlock((ushort)i, [1]));
        Assert.Equal([10, 10, 5], ProtocolClient.PackBlocks(small).Select(f => f.Count));

        var big = Enumerable.Range(1, 30).Select(i => new ConfBlock((ushort)i, new byte[200]));
        Assert.All(ProtocolClient.PackBlocks(big), f => Assert.True(ConfBlock.EncodeMany(f).Length <= 1024));
    }

    [Fact]
    public void Device_error_describes_combined_flags()
    {
        var ex = new DeviceErrorException(DeviceErrorFlags.Crc | DeviceErrorFlags.WrongPassword);
        Assert.Contains("błąd CRC", ex.Message);
        Assert.Contains("błędne hasło", ex.Message);
    }

    [Fact]
    public void Measure_report_section_round_trips()
    {
        var section = new MeasureReportSection(3, 1440, DateTimeOffset.FromUnixTimeSeconds(1_770_000_000), InputType.Analog4To20mA, 3,
            [new AnalogRecord(15, 1, 120, -5, 300, 150), new AnalogRecord(30, 0, 99, 1, 2, 3)]);
        var decoded = Assert.Single(MeasureReportSection.DecodeResponse(section.Encode()));
        Assert.Equal(3u, decoded.DayIndex);
        Assert.Equal(InputType.Analog4To20mA, decoded.InputType);
        Assert.Equal(section.Records, decoded.Records);
    }
}
