using MultiRejestrator.Core;
using MultiRejestrator.Core.Files;
using MultiRejestrator.Core.Parameters;
using MultiRejestrator.Core.Settings;

namespace MultiRejestrator.Tests;

public class CoreTests
{
    private static ParameterCatalog Catalog => ParameterCatalog.Configuration;

    [Fact]
    public void Catalog_contains_all_parameters()
    {
        Assert.Equal(29 + (8 * 61), Catalog.All.Count);
        Assert.True(Catalog.Contains(8061));
        Assert.Equal("Typ wejścia", Catalog[3001].Name);
        Assert.Contains("kanału 3", Catalog[3001].FullName);
        Assert.Equal("mV", Catalog[1013].Unit);
        Assert.Equal(15, ParameterCatalog.DeviceState.All.Count);
    }

    [Fact]
    public void Serial_number_is_encoded_as_bcd_little_endian()
    {
        var def = Catalog[ParameterIds.SerialNumber];
        Assert.Equal(new byte[] { 0x78, 0x56, 0x34, 0x12 }, ParameterCodec.Encode(def, "12345678"));
        Assert.Equal("12345678", ParameterCodec.Decode(def, [0x78, 0x56, 0x34, 0x12]));
        Assert.NotNull(ParameterCodec.Validate(def, "1234567A"));
    }

    [Fact]
    public void Text_fields_are_padded_and_validated()
    {
        var recv = Catalog[3];
        Assert.Equal(new byte[] { (byte)'W', (byte)'A' }, ParameterCodec.Encode(recv, "WA"));
        Assert.NotNull(ParameterCodec.Validate(recv, "ABC"));
        Assert.NotNull(ParameterCodec.Validate(Catalog[29], "Zakład"));
        Assert.Null(ParameterCodec.Validate(Catalog[29], "Zaklad"));
        Assert.NotNull(ParameterCodec.Validate(Catalog[6], "12a4"));
    }

    [Fact]
    public void Aes_key_is_hex()
    {
        var def = Catalog[26];
        var key = new string('A', 64);
        Assert.Null(ParameterCodec.Validate(def, key));
        Assert.Equal(32, ParameterCodec.Encode(def, key).Length);
        Assert.NotNull(ParameterCodec.Validate(def, "00"));
    }

    [Fact]
    public void Signed_values_decode_by_actual_length()
    {
        var temp = ParameterCatalog.DeviceState[DeviceStateIds.Temperature];
        Assert.Equal("-5", ParameterCodec.Decode(temp, [0xFB]));
        var state = ParameterCatalog.DeviceState[DeviceStateIds.GsmState];
        Assert.Equal("3", ParameterCodec.Decode(state, [3, 0]));
    }

    [Fact]
    public void Default_configuration_is_valid()
    {
        var config = Configuration.CreateDefault("000000");
        Assert.Empty(ConfigurationValidator.Validate(config));
        Assert.Equal("1234", config[6]);
        Assert.Equal("0", config[1001]);
    }

    [Fact]
    public void Validator_requires_at_least_one_band()
    {
        var config = Configuration.CreateDefault("000000");
        config[ParameterIds.CatM1Bands] = "0";
        Assert.Contains(ConfigurationValidator.Validate(config), i => i.ParameterId == ParameterIds.CatM1Bands);
    }

    [Theory]
    [InlineData("0", "16", "14", true)]
    [InlineData("0", "14", "16", false)]
    [InlineData("1", "6", "8", true)]
    [InlineData("1", "8", "6", false)]
    public void Validator_checks_alarm_threshold_order(string direction, string on, string off, bool valid)
    {
        var config = Configuration.CreateDefault("000000");
        config[3001] = "5"; // kanał 3: 4–20 mA
        config[3054] = "3"; // alarm 1 aktywny
        config[3046] = on;
        config[3047] = off;
        config[3058] = direction;
        var issues = ConfigurationValidator.Validate(config);
        Assert.Equal(valid, issues.All(i => i.ParameterId != 3046));
    }

    [Fact]
    public void Alarm_rules_ignore_inactive_groups()
    {
        var config = Configuration.CreateDefault("000000");
        config[1001] = "3"; // 2,5 V – a progi 4–20 mA są błędne, ale nieaktywne
        config[1054] = "3";
        config[1046] = "1";
        config[1047] = "9";
        Assert.Empty(ConfigurationValidator.Validate(config));
    }

    [Fact]
    public void Coupled_pulse_mode_locks_second_channel_of_pair()
    {
        var config = Configuration.CreateDefault("000000");
        config[1001] = "1";
        config[1002] = "2";
        Assert.Equal(0, config.CoupledOwner(1));
        Assert.Null(config.CoupledOwner(2));
        config[1002] = "0";
        Assert.Null(config.CoupledOwner(1));
    }

    [Fact]
    public void Copy_channel_copies_all_parameters()
    {
        var config = Configuration.CreateDefault("000000");
        config[1001] = "5";
        config[1046] = "16";
        config.CopyChannel(0, 5);
        Assert.Equal("5", config[6001]);
        Assert.Equal("16", config[6046]);
    }

    [Fact]
    public void Configuration_file_round_trips()
    {
        var config = Configuration.CreateDefault("ab\"c#1");
        config[1] = "12345678";
        config[29] = "Stacja \"P3\" # test";
        config[16] = "host=1;x";
        config[4001] = "4";
        var info = new ConfigurationFileInfo { ProtocolVersion = 1, SerialNumber = "12345678", Firmware = "r3871", Created = new DateTime(2026, 11, 6, 10, 0, 0) };

        var text = ConfigurationFile.Serialize(config, info);
        var result = ConfigurationFile.Parse(text, "000000");

        Assert.Empty(result.Warnings);
        Assert.True(config.ContentEquals(result.Configuration));
        Assert.Equal(1, result.Info.ProtocolVersion);
        Assert.Equal(info.Created, result.Info.Created);
        Assert.Contains("[Kanał 4]", text);
    }

    [Fact]
    public void Configuration_file_reports_unknown_and_missing_parameters()
    {
        var result = ConfigurationFile.Parse("[Ogólne]\n1 = 12345678\n9999 = 5\n", "000000");
        Assert.Equal("12345678", result.Configuration[1]);
        Assert.Equal(2, result.Warnings.Count);
    }

    [Fact]
    public void Settings_round_trip()
    {
        var s = new AppSettings { LastPort = "COM7", DefaultAccessCode = "123456", SetTimeOnSend = true };
        var parsed = AppSettings.Parse(s.Serialize());
        Assert.Equal("COM7", parsed.LastPort);
        Assert.Equal("123456", parsed.DefaultAccessCode);
        Assert.True(parsed.SetTimeOnSend);
    }

    [Fact]
    public void Comparer_lists_differences()
    {
        var a = Configuration.CreateDefault("000000");
        var b = a.Clone();
        b[7] = "internet";
        var diff = ConfigurationComparer.Compare(a, b).Where(d => d.IsDifferent).ToList();
        Assert.Equal(7, Assert.Single(diff).Parameter.Id);
    }
}
