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
        // Uwaga 5 z 10.11.2026: „0W” → 0x57 0x30.
        Assert.Equal(new byte[] { 0x57, 0x30 }, ParameterCodec.Encode(recv, "0W"));
        Assert.Equal("0W", ParameterCodec.Decode(recv, [0x57, 0x30]));
        Assert.Equal("W", ParameterCodec.Decode(recv, ParameterCodec.Encode(recv, "W")));
        Assert.NotNull(ParameterCodec.Validate(recv, "ABC"));
        Assert.NotNull(ParameterCodec.Validate(Catalog[29], "Zakład"));
        Assert.Null(ParameterCodec.Validate(Catalog[29], "Zaklad"));
        Assert.NotNull(ParameterCodec.Validate(Catalog[6], "12a4"));
    }

    [Theory]
    [InlineData("2,5", 25)]
    [InlineData("2.5", 25)]
    [InlineData("0", 0)]
    [InlineData("25,5", 255)]
    public void Binary_switching_level_is_entered_in_volts(string text, byte raw)
    {
        var def = Catalog[1006];
        Assert.Equal(EditKind.Decimal, def.EditKind);
        Assert.Equal("V", def.Unit);
        Assert.Null(ParameterCodec.Validate(def, text));
        Assert.Equal(new[] { raw }, ParameterCodec.Encode(def, text));
    }

    [Theory]
    [InlineData("2,55")]
    [InlineData("25,6")]
    [InlineData("abc")]
    [InlineData("-0,1")]
    public void Binary_switching_level_rejects_invalid_values(string text)
    {
        Assert.NotNull(ParameterCodec.Validate(Catalog[1006], text));
    }

    [Fact]
    public void Binary_switching_level_decodes_with_one_decimal()
    {
        Assert.Equal("2,5", ParameterCodec.Decode(Catalog[1006], [25]));
        Assert.Equal("0,0", ParameterCodec.Decode(Catalog[1006], [0]));
        Assert.Equal("2,5 V", ParameterCodec.Format(Catalog[1006], "2,5"));
    }

    [Fact]
    public void New_configuration_defaults_follow_test_remarks()
    {
        var config = Configuration.CreateDefault("000000");
        Assert.Equal("4", config[ParameterIds.CatM1Bands]); // tylko B20
        Assert.Equal("2", config[ParameterIds.NbBands]);    // tylko B8
        Assert.Equal("1", config[24]);                      // TCP
        Assert.Equal("1", config[25]);                      // Bin (V12)
        Assert.DoesNotContain(Catalog[24].Options, o => o.Value == 0);
        Assert.DoesNotContain(Catalog[25].Options, o => o.Value == 0);
        Assert.Equal("0,0", config[1006]);
    }

    [Fact]
    public void Required_fields_are_reported_when_empty()
    {
        var config = Configuration.CreateDefault("000000");
        Assert.Equal(new ushort[] { 3, 7, 16, 17 }, ConfigurationValidator.MissingRequired(config).Select(p => p.Id));
        config[3] = "0W";
        config[7] = "internet";
        config[16] = "10.0.0.1:5000";
        config[17] = "10.0.0.2:5000";
        Assert.Empty(ConfigurationValidator.MissingRequired(config));
    }

    [Fact]
    public void Counter_catalogs_are_loaded()
    {
        Assert.Equal(new ushort[] { 101, 201, 301, 401, 501, 601, 701, 801 }, ParameterCatalog.MeasureCounters.All.Select(p => p.Id));
        Assert.Equal(9, ParameterCatalog.DeviceCounters.All.Count);
        Assert.Equal("-5", ParameterCodec.Decode(ParameterCatalog.MeasureCounters[101], [0xFB, 0xFF, 0xFF, 0xFF]));
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
        config[2006] = "12,3";
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
