using System.ComponentModel;

namespace FriendlyEnvars.Benchmarks;

// Mechanical option types for each property-count and value-kind pair.

public enum BenchLevel
{
    Level0,
    Level1,
    Level2,
    Level3,
    Level4,
    Level5,
    Level6,
    Level7,
    Level8,
    Level9
}

[TypeConverter(typeof(EndpointConverter))]
public sealed class Endpoint
{
    public string Host { get; init; } = string.Empty;

    public int Port { get; init; }
}

public sealed class EndpointConverter : TypeConverter
{
    public override bool CanConvertFrom(ITypeDescriptorContext? context, System.Type sourceType)
    {
        return sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);
    }

    public override object? ConvertFrom(ITypeDescriptorContext? context, System.Globalization.CultureInfo? culture, object value)
    {
        if (value is string text)
        {
            int separator = text.IndexOf(':', System.StringComparison.Ordinal);

            return new Endpoint
            {
                Host = text[..separator],
                Port = int.Parse(text[(separator + 1)..], System.Globalization.CultureInfo.InvariantCulture)
            };
        }

        return base.ConvertFrom(context, culture, value);
    }
}

public sealed class EmptyProps
{
    public string? Untouched { get; set; }
}

public sealed class StringProps1
{
    [Envar("BENCH_STRING_1_0")]
    public string P0 { get; set; } = string.Empty;
}

public sealed class StringProps10
{
    [Envar("BENCH_STRING_10_0")]
    public string P0 { get; set; } = string.Empty;

    [Envar("BENCH_STRING_10_1")]
    public string P1 { get; set; } = string.Empty;

    [Envar("BENCH_STRING_10_2")]
    public string P2 { get; set; } = string.Empty;

    [Envar("BENCH_STRING_10_3")]
    public string P3 { get; set; } = string.Empty;

    [Envar("BENCH_STRING_10_4")]
    public string P4 { get; set; } = string.Empty;

    [Envar("BENCH_STRING_10_5")]
    public string P5 { get; set; } = string.Empty;

    [Envar("BENCH_STRING_10_6")]
    public string P6 { get; set; } = string.Empty;

    [Envar("BENCH_STRING_10_7")]
    public string P7 { get; set; } = string.Empty;

    [Envar("BENCH_STRING_10_8")]
    public string P8 { get; set; } = string.Empty;

    [Envar("BENCH_STRING_10_9")]
    public string P9 { get; set; } = string.Empty;
}

public sealed class StringProps100
{
    [Envar("BENCH_STRING_100_0")]
    public string P0 { get; set; } = string.Empty;

    [Envar("BENCH_STRING_100_1")]
    public string P1 { get; set; } = string.Empty;

    [Envar("BENCH_STRING_100_2")]
    public string P2 { get; set; } = string.Empty;

    [Envar("BENCH_STRING_100_3")]
    public string P3 { get; set; } = string.Empty;

    [Envar("BENCH_STRING_100_4")]
    public string P4 { get; set; } = string.Empty;

    [Envar("BENCH_STRING_100_5")]
    public string P5 { get; set; } = string.Empty;

    [Envar("BENCH_STRING_100_6")]
    public string P6 { get; set; } = string.Empty;

    [Envar("BENCH_STRING_100_7")]
    public string P7 { get; set; } = string.Empty;

    [Envar("BENCH_STRING_100_8")]
    public string P8 { get; set; } = string.Empty;

    [Envar("BENCH_STRING_100_9")]
    public string P9 { get; set; } = string.Empty;

    [Envar("BENCH_STRING_100_10")]
    public string P10 { get; set; } = string.Empty;

    [Envar("BENCH_STRING_100_11")]
    public string P11 { get; set; } = string.Empty;

    [Envar("BENCH_STRING_100_12")]
    public string P12 { get; set; } = string.Empty;

    [Envar("BENCH_STRING_100_13")]
    public string P13 { get; set; } = string.Empty;

    [Envar("BENCH_STRING_100_14")]
    public string P14 { get; set; } = string.Empty;

    [Envar("BENCH_STRING_100_15")]
    public string P15 { get; set; } = string.Empty;

    [Envar("BENCH_STRING_100_16")]
    public string P16 { get; set; } = string.Empty;

    [Envar("BENCH_STRING_100_17")]
    public string P17 { get; set; } = string.Empty;

    [Envar("BENCH_STRING_100_18")]
    public string P18 { get; set; } = string.Empty;

    [Envar("BENCH_STRING_100_19")]
    public string P19 { get; set; } = string.Empty;

    [Envar("BENCH_STRING_100_20")]
    public string P20 { get; set; } = string.Empty;

    [Envar("BENCH_STRING_100_21")]
    public string P21 { get; set; } = string.Empty;

    [Envar("BENCH_STRING_100_22")]
    public string P22 { get; set; } = string.Empty;

    [Envar("BENCH_STRING_100_23")]
    public string P23 { get; set; } = string.Empty;

    [Envar("BENCH_STRING_100_24")]
    public string P24 { get; set; } = string.Empty;

    [Envar("BENCH_STRING_100_25")]
    public string P25 { get; set; } = string.Empty;

    [Envar("BENCH_STRING_100_26")]
    public string P26 { get; set; } = string.Empty;

    [Envar("BENCH_STRING_100_27")]
    public string P27 { get; set; } = string.Empty;

    [Envar("BENCH_STRING_100_28")]
    public string P28 { get; set; } = string.Empty;

    [Envar("BENCH_STRING_100_29")]
    public string P29 { get; set; } = string.Empty;

    [Envar("BENCH_STRING_100_30")]
    public string P30 { get; set; } = string.Empty;

    [Envar("BENCH_STRING_100_31")]
    public string P31 { get; set; } = string.Empty;

    [Envar("BENCH_STRING_100_32")]
    public string P32 { get; set; } = string.Empty;

    [Envar("BENCH_STRING_100_33")]
    public string P33 { get; set; } = string.Empty;

    [Envar("BENCH_STRING_100_34")]
    public string P34 { get; set; } = string.Empty;

    [Envar("BENCH_STRING_100_35")]
    public string P35 { get; set; } = string.Empty;

    [Envar("BENCH_STRING_100_36")]
    public string P36 { get; set; } = string.Empty;

    [Envar("BENCH_STRING_100_37")]
    public string P37 { get; set; } = string.Empty;

    [Envar("BENCH_STRING_100_38")]
    public string P38 { get; set; } = string.Empty;

    [Envar("BENCH_STRING_100_39")]
    public string P39 { get; set; } = string.Empty;

    [Envar("BENCH_STRING_100_40")]
    public string P40 { get; set; } = string.Empty;

    [Envar("BENCH_STRING_100_41")]
    public string P41 { get; set; } = string.Empty;

    [Envar("BENCH_STRING_100_42")]
    public string P42 { get; set; } = string.Empty;

    [Envar("BENCH_STRING_100_43")]
    public string P43 { get; set; } = string.Empty;

    [Envar("BENCH_STRING_100_44")]
    public string P44 { get; set; } = string.Empty;

    [Envar("BENCH_STRING_100_45")]
    public string P45 { get; set; } = string.Empty;

    [Envar("BENCH_STRING_100_46")]
    public string P46 { get; set; } = string.Empty;

    [Envar("BENCH_STRING_100_47")]
    public string P47 { get; set; } = string.Empty;

    [Envar("BENCH_STRING_100_48")]
    public string P48 { get; set; } = string.Empty;

    [Envar("BENCH_STRING_100_49")]
    public string P49 { get; set; } = string.Empty;

    [Envar("BENCH_STRING_100_50")]
    public string P50 { get; set; } = string.Empty;

    [Envar("BENCH_STRING_100_51")]
    public string P51 { get; set; } = string.Empty;

    [Envar("BENCH_STRING_100_52")]
    public string P52 { get; set; } = string.Empty;

    [Envar("BENCH_STRING_100_53")]
    public string P53 { get; set; } = string.Empty;

    [Envar("BENCH_STRING_100_54")]
    public string P54 { get; set; } = string.Empty;

    [Envar("BENCH_STRING_100_55")]
    public string P55 { get; set; } = string.Empty;

    [Envar("BENCH_STRING_100_56")]
    public string P56 { get; set; } = string.Empty;

    [Envar("BENCH_STRING_100_57")]
    public string P57 { get; set; } = string.Empty;

    [Envar("BENCH_STRING_100_58")]
    public string P58 { get; set; } = string.Empty;

    [Envar("BENCH_STRING_100_59")]
    public string P59 { get; set; } = string.Empty;

    [Envar("BENCH_STRING_100_60")]
    public string P60 { get; set; } = string.Empty;

    [Envar("BENCH_STRING_100_61")]
    public string P61 { get; set; } = string.Empty;

    [Envar("BENCH_STRING_100_62")]
    public string P62 { get; set; } = string.Empty;

    [Envar("BENCH_STRING_100_63")]
    public string P63 { get; set; } = string.Empty;

    [Envar("BENCH_STRING_100_64")]
    public string P64 { get; set; } = string.Empty;

    [Envar("BENCH_STRING_100_65")]
    public string P65 { get; set; } = string.Empty;

    [Envar("BENCH_STRING_100_66")]
    public string P66 { get; set; } = string.Empty;

    [Envar("BENCH_STRING_100_67")]
    public string P67 { get; set; } = string.Empty;

    [Envar("BENCH_STRING_100_68")]
    public string P68 { get; set; } = string.Empty;

    [Envar("BENCH_STRING_100_69")]
    public string P69 { get; set; } = string.Empty;

    [Envar("BENCH_STRING_100_70")]
    public string P70 { get; set; } = string.Empty;

    [Envar("BENCH_STRING_100_71")]
    public string P71 { get; set; } = string.Empty;

    [Envar("BENCH_STRING_100_72")]
    public string P72 { get; set; } = string.Empty;

    [Envar("BENCH_STRING_100_73")]
    public string P73 { get; set; } = string.Empty;

    [Envar("BENCH_STRING_100_74")]
    public string P74 { get; set; } = string.Empty;

    [Envar("BENCH_STRING_100_75")]
    public string P75 { get; set; } = string.Empty;

    [Envar("BENCH_STRING_100_76")]
    public string P76 { get; set; } = string.Empty;

    [Envar("BENCH_STRING_100_77")]
    public string P77 { get; set; } = string.Empty;

    [Envar("BENCH_STRING_100_78")]
    public string P78 { get; set; } = string.Empty;

    [Envar("BENCH_STRING_100_79")]
    public string P79 { get; set; } = string.Empty;

    [Envar("BENCH_STRING_100_80")]
    public string P80 { get; set; } = string.Empty;

    [Envar("BENCH_STRING_100_81")]
    public string P81 { get; set; } = string.Empty;

    [Envar("BENCH_STRING_100_82")]
    public string P82 { get; set; } = string.Empty;

    [Envar("BENCH_STRING_100_83")]
    public string P83 { get; set; } = string.Empty;

    [Envar("BENCH_STRING_100_84")]
    public string P84 { get; set; } = string.Empty;

    [Envar("BENCH_STRING_100_85")]
    public string P85 { get; set; } = string.Empty;

    [Envar("BENCH_STRING_100_86")]
    public string P86 { get; set; } = string.Empty;

    [Envar("BENCH_STRING_100_87")]
    public string P87 { get; set; } = string.Empty;

    [Envar("BENCH_STRING_100_88")]
    public string P88 { get; set; } = string.Empty;

    [Envar("BENCH_STRING_100_89")]
    public string P89 { get; set; } = string.Empty;

    [Envar("BENCH_STRING_100_90")]
    public string P90 { get; set; } = string.Empty;

    [Envar("BENCH_STRING_100_91")]
    public string P91 { get; set; } = string.Empty;

    [Envar("BENCH_STRING_100_92")]
    public string P92 { get; set; } = string.Empty;

    [Envar("BENCH_STRING_100_93")]
    public string P93 { get; set; } = string.Empty;

    [Envar("BENCH_STRING_100_94")]
    public string P94 { get; set; } = string.Empty;

    [Envar("BENCH_STRING_100_95")]
    public string P95 { get; set; } = string.Empty;

    [Envar("BENCH_STRING_100_96")]
    public string P96 { get; set; } = string.Empty;

    [Envar("BENCH_STRING_100_97")]
    public string P97 { get; set; } = string.Empty;

    [Envar("BENCH_STRING_100_98")]
    public string P98 { get; set; } = string.Empty;

    [Envar("BENCH_STRING_100_99")]
    public string P99 { get; set; } = string.Empty;
}

public sealed class NumericProps1
{
    [Envar("BENCH_NUMERIC_1_0")]
    public int P0 { get; set; }
}

public sealed class NumericProps10
{
    [Envar("BENCH_NUMERIC_10_0")]
    public int P0 { get; set; }

    [Envar("BENCH_NUMERIC_10_1")]
    public int P1 { get; set; }

    [Envar("BENCH_NUMERIC_10_2")]
    public int P2 { get; set; }

    [Envar("BENCH_NUMERIC_10_3")]
    public int P3 { get; set; }

    [Envar("BENCH_NUMERIC_10_4")]
    public int P4 { get; set; }

    [Envar("BENCH_NUMERIC_10_5")]
    public int P5 { get; set; }

    [Envar("BENCH_NUMERIC_10_6")]
    public int P6 { get; set; }

    [Envar("BENCH_NUMERIC_10_7")]
    public int P7 { get; set; }

    [Envar("BENCH_NUMERIC_10_8")]
    public int P8 { get; set; }

    [Envar("BENCH_NUMERIC_10_9")]
    public int P9 { get; set; }
}

public sealed class NumericProps100
{
    [Envar("BENCH_NUMERIC_100_0")]
    public int P0 { get; set; }

    [Envar("BENCH_NUMERIC_100_1")]
    public int P1 { get; set; }

    [Envar("BENCH_NUMERIC_100_2")]
    public int P2 { get; set; }

    [Envar("BENCH_NUMERIC_100_3")]
    public int P3 { get; set; }

    [Envar("BENCH_NUMERIC_100_4")]
    public int P4 { get; set; }

    [Envar("BENCH_NUMERIC_100_5")]
    public int P5 { get; set; }

    [Envar("BENCH_NUMERIC_100_6")]
    public int P6 { get; set; }

    [Envar("BENCH_NUMERIC_100_7")]
    public int P7 { get; set; }

    [Envar("BENCH_NUMERIC_100_8")]
    public int P8 { get; set; }

    [Envar("BENCH_NUMERIC_100_9")]
    public int P9 { get; set; }

    [Envar("BENCH_NUMERIC_100_10")]
    public int P10 { get; set; }

    [Envar("BENCH_NUMERIC_100_11")]
    public int P11 { get; set; }

    [Envar("BENCH_NUMERIC_100_12")]
    public int P12 { get; set; }

    [Envar("BENCH_NUMERIC_100_13")]
    public int P13 { get; set; }

    [Envar("BENCH_NUMERIC_100_14")]
    public int P14 { get; set; }

    [Envar("BENCH_NUMERIC_100_15")]
    public int P15 { get; set; }

    [Envar("BENCH_NUMERIC_100_16")]
    public int P16 { get; set; }

    [Envar("BENCH_NUMERIC_100_17")]
    public int P17 { get; set; }

    [Envar("BENCH_NUMERIC_100_18")]
    public int P18 { get; set; }

    [Envar("BENCH_NUMERIC_100_19")]
    public int P19 { get; set; }

    [Envar("BENCH_NUMERIC_100_20")]
    public int P20 { get; set; }

    [Envar("BENCH_NUMERIC_100_21")]
    public int P21 { get; set; }

    [Envar("BENCH_NUMERIC_100_22")]
    public int P22 { get; set; }

    [Envar("BENCH_NUMERIC_100_23")]
    public int P23 { get; set; }

    [Envar("BENCH_NUMERIC_100_24")]
    public int P24 { get; set; }

    [Envar("BENCH_NUMERIC_100_25")]
    public int P25 { get; set; }

    [Envar("BENCH_NUMERIC_100_26")]
    public int P26 { get; set; }

    [Envar("BENCH_NUMERIC_100_27")]
    public int P27 { get; set; }

    [Envar("BENCH_NUMERIC_100_28")]
    public int P28 { get; set; }

    [Envar("BENCH_NUMERIC_100_29")]
    public int P29 { get; set; }

    [Envar("BENCH_NUMERIC_100_30")]
    public int P30 { get; set; }

    [Envar("BENCH_NUMERIC_100_31")]
    public int P31 { get; set; }

    [Envar("BENCH_NUMERIC_100_32")]
    public int P32 { get; set; }

    [Envar("BENCH_NUMERIC_100_33")]
    public int P33 { get; set; }

    [Envar("BENCH_NUMERIC_100_34")]
    public int P34 { get; set; }

    [Envar("BENCH_NUMERIC_100_35")]
    public int P35 { get; set; }

    [Envar("BENCH_NUMERIC_100_36")]
    public int P36 { get; set; }

    [Envar("BENCH_NUMERIC_100_37")]
    public int P37 { get; set; }

    [Envar("BENCH_NUMERIC_100_38")]
    public int P38 { get; set; }

    [Envar("BENCH_NUMERIC_100_39")]
    public int P39 { get; set; }

    [Envar("BENCH_NUMERIC_100_40")]
    public int P40 { get; set; }

    [Envar("BENCH_NUMERIC_100_41")]
    public int P41 { get; set; }

    [Envar("BENCH_NUMERIC_100_42")]
    public int P42 { get; set; }

    [Envar("BENCH_NUMERIC_100_43")]
    public int P43 { get; set; }

    [Envar("BENCH_NUMERIC_100_44")]
    public int P44 { get; set; }

    [Envar("BENCH_NUMERIC_100_45")]
    public int P45 { get; set; }

    [Envar("BENCH_NUMERIC_100_46")]
    public int P46 { get; set; }

    [Envar("BENCH_NUMERIC_100_47")]
    public int P47 { get; set; }

    [Envar("BENCH_NUMERIC_100_48")]
    public int P48 { get; set; }

    [Envar("BENCH_NUMERIC_100_49")]
    public int P49 { get; set; }

    [Envar("BENCH_NUMERIC_100_50")]
    public int P50 { get; set; }

    [Envar("BENCH_NUMERIC_100_51")]
    public int P51 { get; set; }

    [Envar("BENCH_NUMERIC_100_52")]
    public int P52 { get; set; }

    [Envar("BENCH_NUMERIC_100_53")]
    public int P53 { get; set; }

    [Envar("BENCH_NUMERIC_100_54")]
    public int P54 { get; set; }

    [Envar("BENCH_NUMERIC_100_55")]
    public int P55 { get; set; }

    [Envar("BENCH_NUMERIC_100_56")]
    public int P56 { get; set; }

    [Envar("BENCH_NUMERIC_100_57")]
    public int P57 { get; set; }

    [Envar("BENCH_NUMERIC_100_58")]
    public int P58 { get; set; }

    [Envar("BENCH_NUMERIC_100_59")]
    public int P59 { get; set; }

    [Envar("BENCH_NUMERIC_100_60")]
    public int P60 { get; set; }

    [Envar("BENCH_NUMERIC_100_61")]
    public int P61 { get; set; }

    [Envar("BENCH_NUMERIC_100_62")]
    public int P62 { get; set; }

    [Envar("BENCH_NUMERIC_100_63")]
    public int P63 { get; set; }

    [Envar("BENCH_NUMERIC_100_64")]
    public int P64 { get; set; }

    [Envar("BENCH_NUMERIC_100_65")]
    public int P65 { get; set; }

    [Envar("BENCH_NUMERIC_100_66")]
    public int P66 { get; set; }

    [Envar("BENCH_NUMERIC_100_67")]
    public int P67 { get; set; }

    [Envar("BENCH_NUMERIC_100_68")]
    public int P68 { get; set; }

    [Envar("BENCH_NUMERIC_100_69")]
    public int P69 { get; set; }

    [Envar("BENCH_NUMERIC_100_70")]
    public int P70 { get; set; }

    [Envar("BENCH_NUMERIC_100_71")]
    public int P71 { get; set; }

    [Envar("BENCH_NUMERIC_100_72")]
    public int P72 { get; set; }

    [Envar("BENCH_NUMERIC_100_73")]
    public int P73 { get; set; }

    [Envar("BENCH_NUMERIC_100_74")]
    public int P74 { get; set; }

    [Envar("BENCH_NUMERIC_100_75")]
    public int P75 { get; set; }

    [Envar("BENCH_NUMERIC_100_76")]
    public int P76 { get; set; }

    [Envar("BENCH_NUMERIC_100_77")]
    public int P77 { get; set; }

    [Envar("BENCH_NUMERIC_100_78")]
    public int P78 { get; set; }

    [Envar("BENCH_NUMERIC_100_79")]
    public int P79 { get; set; }

    [Envar("BENCH_NUMERIC_100_80")]
    public int P80 { get; set; }

    [Envar("BENCH_NUMERIC_100_81")]
    public int P81 { get; set; }

    [Envar("BENCH_NUMERIC_100_82")]
    public int P82 { get; set; }

    [Envar("BENCH_NUMERIC_100_83")]
    public int P83 { get; set; }

    [Envar("BENCH_NUMERIC_100_84")]
    public int P84 { get; set; }

    [Envar("BENCH_NUMERIC_100_85")]
    public int P85 { get; set; }

    [Envar("BENCH_NUMERIC_100_86")]
    public int P86 { get; set; }

    [Envar("BENCH_NUMERIC_100_87")]
    public int P87 { get; set; }

    [Envar("BENCH_NUMERIC_100_88")]
    public int P88 { get; set; }

    [Envar("BENCH_NUMERIC_100_89")]
    public int P89 { get; set; }

    [Envar("BENCH_NUMERIC_100_90")]
    public int P90 { get; set; }

    [Envar("BENCH_NUMERIC_100_91")]
    public int P91 { get; set; }

    [Envar("BENCH_NUMERIC_100_92")]
    public int P92 { get; set; }

    [Envar("BENCH_NUMERIC_100_93")]
    public int P93 { get; set; }

    [Envar("BENCH_NUMERIC_100_94")]
    public int P94 { get; set; }

    [Envar("BENCH_NUMERIC_100_95")]
    public int P95 { get; set; }

    [Envar("BENCH_NUMERIC_100_96")]
    public int P96 { get; set; }

    [Envar("BENCH_NUMERIC_100_97")]
    public int P97 { get; set; }

    [Envar("BENCH_NUMERIC_100_98")]
    public int P98 { get; set; }

    [Envar("BENCH_NUMERIC_100_99")]
    public int P99 { get; set; }
}

public sealed class EnumProps1
{
    [Envar("BENCH_ENUM_1_0")]
    public BenchLevel P0 { get; set; }
}

public sealed class EnumProps10
{
    [Envar("BENCH_ENUM_10_0")]
    public BenchLevel P0 { get; set; }

    [Envar("BENCH_ENUM_10_1")]
    public BenchLevel P1 { get; set; }

    [Envar("BENCH_ENUM_10_2")]
    public BenchLevel P2 { get; set; }

    [Envar("BENCH_ENUM_10_3")]
    public BenchLevel P3 { get; set; }

    [Envar("BENCH_ENUM_10_4")]
    public BenchLevel P4 { get; set; }

    [Envar("BENCH_ENUM_10_5")]
    public BenchLevel P5 { get; set; }

    [Envar("BENCH_ENUM_10_6")]
    public BenchLevel P6 { get; set; }

    [Envar("BENCH_ENUM_10_7")]
    public BenchLevel P7 { get; set; }

    [Envar("BENCH_ENUM_10_8")]
    public BenchLevel P8 { get; set; }

    [Envar("BENCH_ENUM_10_9")]
    public BenchLevel P9 { get; set; }
}

public sealed class EnumProps100
{
    [Envar("BENCH_ENUM_100_0")]
    public BenchLevel P0 { get; set; }

    [Envar("BENCH_ENUM_100_1")]
    public BenchLevel P1 { get; set; }

    [Envar("BENCH_ENUM_100_2")]
    public BenchLevel P2 { get; set; }

    [Envar("BENCH_ENUM_100_3")]
    public BenchLevel P3 { get; set; }

    [Envar("BENCH_ENUM_100_4")]
    public BenchLevel P4 { get; set; }

    [Envar("BENCH_ENUM_100_5")]
    public BenchLevel P5 { get; set; }

    [Envar("BENCH_ENUM_100_6")]
    public BenchLevel P6 { get; set; }

    [Envar("BENCH_ENUM_100_7")]
    public BenchLevel P7 { get; set; }

    [Envar("BENCH_ENUM_100_8")]
    public BenchLevel P8 { get; set; }

    [Envar("BENCH_ENUM_100_9")]
    public BenchLevel P9 { get; set; }

    [Envar("BENCH_ENUM_100_10")]
    public BenchLevel P10 { get; set; }

    [Envar("BENCH_ENUM_100_11")]
    public BenchLevel P11 { get; set; }

    [Envar("BENCH_ENUM_100_12")]
    public BenchLevel P12 { get; set; }

    [Envar("BENCH_ENUM_100_13")]
    public BenchLevel P13 { get; set; }

    [Envar("BENCH_ENUM_100_14")]
    public BenchLevel P14 { get; set; }

    [Envar("BENCH_ENUM_100_15")]
    public BenchLevel P15 { get; set; }

    [Envar("BENCH_ENUM_100_16")]
    public BenchLevel P16 { get; set; }

    [Envar("BENCH_ENUM_100_17")]
    public BenchLevel P17 { get; set; }

    [Envar("BENCH_ENUM_100_18")]
    public BenchLevel P18 { get; set; }

    [Envar("BENCH_ENUM_100_19")]
    public BenchLevel P19 { get; set; }

    [Envar("BENCH_ENUM_100_20")]
    public BenchLevel P20 { get; set; }

    [Envar("BENCH_ENUM_100_21")]
    public BenchLevel P21 { get; set; }

    [Envar("BENCH_ENUM_100_22")]
    public BenchLevel P22 { get; set; }

    [Envar("BENCH_ENUM_100_23")]
    public BenchLevel P23 { get; set; }

    [Envar("BENCH_ENUM_100_24")]
    public BenchLevel P24 { get; set; }

    [Envar("BENCH_ENUM_100_25")]
    public BenchLevel P25 { get; set; }

    [Envar("BENCH_ENUM_100_26")]
    public BenchLevel P26 { get; set; }

    [Envar("BENCH_ENUM_100_27")]
    public BenchLevel P27 { get; set; }

    [Envar("BENCH_ENUM_100_28")]
    public BenchLevel P28 { get; set; }

    [Envar("BENCH_ENUM_100_29")]
    public BenchLevel P29 { get; set; }

    [Envar("BENCH_ENUM_100_30")]
    public BenchLevel P30 { get; set; }

    [Envar("BENCH_ENUM_100_31")]
    public BenchLevel P31 { get; set; }

    [Envar("BENCH_ENUM_100_32")]
    public BenchLevel P32 { get; set; }

    [Envar("BENCH_ENUM_100_33")]
    public BenchLevel P33 { get; set; }

    [Envar("BENCH_ENUM_100_34")]
    public BenchLevel P34 { get; set; }

    [Envar("BENCH_ENUM_100_35")]
    public BenchLevel P35 { get; set; }

    [Envar("BENCH_ENUM_100_36")]
    public BenchLevel P36 { get; set; }

    [Envar("BENCH_ENUM_100_37")]
    public BenchLevel P37 { get; set; }

    [Envar("BENCH_ENUM_100_38")]
    public BenchLevel P38 { get; set; }

    [Envar("BENCH_ENUM_100_39")]
    public BenchLevel P39 { get; set; }

    [Envar("BENCH_ENUM_100_40")]
    public BenchLevel P40 { get; set; }

    [Envar("BENCH_ENUM_100_41")]
    public BenchLevel P41 { get; set; }

    [Envar("BENCH_ENUM_100_42")]
    public BenchLevel P42 { get; set; }

    [Envar("BENCH_ENUM_100_43")]
    public BenchLevel P43 { get; set; }

    [Envar("BENCH_ENUM_100_44")]
    public BenchLevel P44 { get; set; }

    [Envar("BENCH_ENUM_100_45")]
    public BenchLevel P45 { get; set; }

    [Envar("BENCH_ENUM_100_46")]
    public BenchLevel P46 { get; set; }

    [Envar("BENCH_ENUM_100_47")]
    public BenchLevel P47 { get; set; }

    [Envar("BENCH_ENUM_100_48")]
    public BenchLevel P48 { get; set; }

    [Envar("BENCH_ENUM_100_49")]
    public BenchLevel P49 { get; set; }

    [Envar("BENCH_ENUM_100_50")]
    public BenchLevel P50 { get; set; }

    [Envar("BENCH_ENUM_100_51")]
    public BenchLevel P51 { get; set; }

    [Envar("BENCH_ENUM_100_52")]
    public BenchLevel P52 { get; set; }

    [Envar("BENCH_ENUM_100_53")]
    public BenchLevel P53 { get; set; }

    [Envar("BENCH_ENUM_100_54")]
    public BenchLevel P54 { get; set; }

    [Envar("BENCH_ENUM_100_55")]
    public BenchLevel P55 { get; set; }

    [Envar("BENCH_ENUM_100_56")]
    public BenchLevel P56 { get; set; }

    [Envar("BENCH_ENUM_100_57")]
    public BenchLevel P57 { get; set; }

    [Envar("BENCH_ENUM_100_58")]
    public BenchLevel P58 { get; set; }

    [Envar("BENCH_ENUM_100_59")]
    public BenchLevel P59 { get; set; }

    [Envar("BENCH_ENUM_100_60")]
    public BenchLevel P60 { get; set; }

    [Envar("BENCH_ENUM_100_61")]
    public BenchLevel P61 { get; set; }

    [Envar("BENCH_ENUM_100_62")]
    public BenchLevel P62 { get; set; }

    [Envar("BENCH_ENUM_100_63")]
    public BenchLevel P63 { get; set; }

    [Envar("BENCH_ENUM_100_64")]
    public BenchLevel P64 { get; set; }

    [Envar("BENCH_ENUM_100_65")]
    public BenchLevel P65 { get; set; }

    [Envar("BENCH_ENUM_100_66")]
    public BenchLevel P66 { get; set; }

    [Envar("BENCH_ENUM_100_67")]
    public BenchLevel P67 { get; set; }

    [Envar("BENCH_ENUM_100_68")]
    public BenchLevel P68 { get; set; }

    [Envar("BENCH_ENUM_100_69")]
    public BenchLevel P69 { get; set; }

    [Envar("BENCH_ENUM_100_70")]
    public BenchLevel P70 { get; set; }

    [Envar("BENCH_ENUM_100_71")]
    public BenchLevel P71 { get; set; }

    [Envar("BENCH_ENUM_100_72")]
    public BenchLevel P72 { get; set; }

    [Envar("BENCH_ENUM_100_73")]
    public BenchLevel P73 { get; set; }

    [Envar("BENCH_ENUM_100_74")]
    public BenchLevel P74 { get; set; }

    [Envar("BENCH_ENUM_100_75")]
    public BenchLevel P75 { get; set; }

    [Envar("BENCH_ENUM_100_76")]
    public BenchLevel P76 { get; set; }

    [Envar("BENCH_ENUM_100_77")]
    public BenchLevel P77 { get; set; }

    [Envar("BENCH_ENUM_100_78")]
    public BenchLevel P78 { get; set; }

    [Envar("BENCH_ENUM_100_79")]
    public BenchLevel P79 { get; set; }

    [Envar("BENCH_ENUM_100_80")]
    public BenchLevel P80 { get; set; }

    [Envar("BENCH_ENUM_100_81")]
    public BenchLevel P81 { get; set; }

    [Envar("BENCH_ENUM_100_82")]
    public BenchLevel P82 { get; set; }

    [Envar("BENCH_ENUM_100_83")]
    public BenchLevel P83 { get; set; }

    [Envar("BENCH_ENUM_100_84")]
    public BenchLevel P84 { get; set; }

    [Envar("BENCH_ENUM_100_85")]
    public BenchLevel P85 { get; set; }

    [Envar("BENCH_ENUM_100_86")]
    public BenchLevel P86 { get; set; }

    [Envar("BENCH_ENUM_100_87")]
    public BenchLevel P87 { get; set; }

    [Envar("BENCH_ENUM_100_88")]
    public BenchLevel P88 { get; set; }

    [Envar("BENCH_ENUM_100_89")]
    public BenchLevel P89 { get; set; }

    [Envar("BENCH_ENUM_100_90")]
    public BenchLevel P90 { get; set; }

    [Envar("BENCH_ENUM_100_91")]
    public BenchLevel P91 { get; set; }

    [Envar("BENCH_ENUM_100_92")]
    public BenchLevel P92 { get; set; }

    [Envar("BENCH_ENUM_100_93")]
    public BenchLevel P93 { get; set; }

    [Envar("BENCH_ENUM_100_94")]
    public BenchLevel P94 { get; set; }

    [Envar("BENCH_ENUM_100_95")]
    public BenchLevel P95 { get; set; }

    [Envar("BENCH_ENUM_100_96")]
    public BenchLevel P96 { get; set; }

    [Envar("BENCH_ENUM_100_97")]
    public BenchLevel P97 { get; set; }

    [Envar("BENCH_ENUM_100_98")]
    public BenchLevel P98 { get; set; }

    [Envar("BENCH_ENUM_100_99")]
    public BenchLevel P99 { get; set; }
}

public sealed class CustomConverterProps1
{
    [Envar("BENCH_CUSTOMCONVERTER_1_0")]
    public Endpoint P0 { get; set; } = new();
}

public sealed class CustomConverterProps10
{
    [Envar("BENCH_CUSTOMCONVERTER_10_0")]
    public Endpoint P0 { get; set; } = new();

    [Envar("BENCH_CUSTOMCONVERTER_10_1")]
    public Endpoint P1 { get; set; } = new();

    [Envar("BENCH_CUSTOMCONVERTER_10_2")]
    public Endpoint P2 { get; set; } = new();

    [Envar("BENCH_CUSTOMCONVERTER_10_3")]
    public Endpoint P3 { get; set; } = new();

    [Envar("BENCH_CUSTOMCONVERTER_10_4")]
    public Endpoint P4 { get; set; } = new();

    [Envar("BENCH_CUSTOMCONVERTER_10_5")]
    public Endpoint P5 { get; set; } = new();

    [Envar("BENCH_CUSTOMCONVERTER_10_6")]
    public Endpoint P6 { get; set; } = new();

    [Envar("BENCH_CUSTOMCONVERTER_10_7")]
    public Endpoint P7 { get; set; } = new();

    [Envar("BENCH_CUSTOMCONVERTER_10_8")]
    public Endpoint P8 { get; set; } = new();

    [Envar("BENCH_CUSTOMCONVERTER_10_9")]
    public Endpoint P9 { get; set; } = new();
}

public sealed class CustomConverterProps100
{
    [Envar("BENCH_CUSTOMCONVERTER_100_0")]
    public Endpoint P0 { get; set; } = new();

    [Envar("BENCH_CUSTOMCONVERTER_100_1")]
    public Endpoint P1 { get; set; } = new();

    [Envar("BENCH_CUSTOMCONVERTER_100_2")]
    public Endpoint P2 { get; set; } = new();

    [Envar("BENCH_CUSTOMCONVERTER_100_3")]
    public Endpoint P3 { get; set; } = new();

    [Envar("BENCH_CUSTOMCONVERTER_100_4")]
    public Endpoint P4 { get; set; } = new();

    [Envar("BENCH_CUSTOMCONVERTER_100_5")]
    public Endpoint P5 { get; set; } = new();

    [Envar("BENCH_CUSTOMCONVERTER_100_6")]
    public Endpoint P6 { get; set; } = new();

    [Envar("BENCH_CUSTOMCONVERTER_100_7")]
    public Endpoint P7 { get; set; } = new();

    [Envar("BENCH_CUSTOMCONVERTER_100_8")]
    public Endpoint P8 { get; set; } = new();

    [Envar("BENCH_CUSTOMCONVERTER_100_9")]
    public Endpoint P9 { get; set; } = new();

    [Envar("BENCH_CUSTOMCONVERTER_100_10")]
    public Endpoint P10 { get; set; } = new();

    [Envar("BENCH_CUSTOMCONVERTER_100_11")]
    public Endpoint P11 { get; set; } = new();

    [Envar("BENCH_CUSTOMCONVERTER_100_12")]
    public Endpoint P12 { get; set; } = new();

    [Envar("BENCH_CUSTOMCONVERTER_100_13")]
    public Endpoint P13 { get; set; } = new();

    [Envar("BENCH_CUSTOMCONVERTER_100_14")]
    public Endpoint P14 { get; set; } = new();

    [Envar("BENCH_CUSTOMCONVERTER_100_15")]
    public Endpoint P15 { get; set; } = new();

    [Envar("BENCH_CUSTOMCONVERTER_100_16")]
    public Endpoint P16 { get; set; } = new();

    [Envar("BENCH_CUSTOMCONVERTER_100_17")]
    public Endpoint P17 { get; set; } = new();

    [Envar("BENCH_CUSTOMCONVERTER_100_18")]
    public Endpoint P18 { get; set; } = new();

    [Envar("BENCH_CUSTOMCONVERTER_100_19")]
    public Endpoint P19 { get; set; } = new();

    [Envar("BENCH_CUSTOMCONVERTER_100_20")]
    public Endpoint P20 { get; set; } = new();

    [Envar("BENCH_CUSTOMCONVERTER_100_21")]
    public Endpoint P21 { get; set; } = new();

    [Envar("BENCH_CUSTOMCONVERTER_100_22")]
    public Endpoint P22 { get; set; } = new();

    [Envar("BENCH_CUSTOMCONVERTER_100_23")]
    public Endpoint P23 { get; set; } = new();

    [Envar("BENCH_CUSTOMCONVERTER_100_24")]
    public Endpoint P24 { get; set; } = new();

    [Envar("BENCH_CUSTOMCONVERTER_100_25")]
    public Endpoint P25 { get; set; } = new();

    [Envar("BENCH_CUSTOMCONVERTER_100_26")]
    public Endpoint P26 { get; set; } = new();

    [Envar("BENCH_CUSTOMCONVERTER_100_27")]
    public Endpoint P27 { get; set; } = new();

    [Envar("BENCH_CUSTOMCONVERTER_100_28")]
    public Endpoint P28 { get; set; } = new();

    [Envar("BENCH_CUSTOMCONVERTER_100_29")]
    public Endpoint P29 { get; set; } = new();

    [Envar("BENCH_CUSTOMCONVERTER_100_30")]
    public Endpoint P30 { get; set; } = new();

    [Envar("BENCH_CUSTOMCONVERTER_100_31")]
    public Endpoint P31 { get; set; } = new();

    [Envar("BENCH_CUSTOMCONVERTER_100_32")]
    public Endpoint P32 { get; set; } = new();

    [Envar("BENCH_CUSTOMCONVERTER_100_33")]
    public Endpoint P33 { get; set; } = new();

    [Envar("BENCH_CUSTOMCONVERTER_100_34")]
    public Endpoint P34 { get; set; } = new();

    [Envar("BENCH_CUSTOMCONVERTER_100_35")]
    public Endpoint P35 { get; set; } = new();

    [Envar("BENCH_CUSTOMCONVERTER_100_36")]
    public Endpoint P36 { get; set; } = new();

    [Envar("BENCH_CUSTOMCONVERTER_100_37")]
    public Endpoint P37 { get; set; } = new();

    [Envar("BENCH_CUSTOMCONVERTER_100_38")]
    public Endpoint P38 { get; set; } = new();

    [Envar("BENCH_CUSTOMCONVERTER_100_39")]
    public Endpoint P39 { get; set; } = new();

    [Envar("BENCH_CUSTOMCONVERTER_100_40")]
    public Endpoint P40 { get; set; } = new();

    [Envar("BENCH_CUSTOMCONVERTER_100_41")]
    public Endpoint P41 { get; set; } = new();

    [Envar("BENCH_CUSTOMCONVERTER_100_42")]
    public Endpoint P42 { get; set; } = new();

    [Envar("BENCH_CUSTOMCONVERTER_100_43")]
    public Endpoint P43 { get; set; } = new();

    [Envar("BENCH_CUSTOMCONVERTER_100_44")]
    public Endpoint P44 { get; set; } = new();

    [Envar("BENCH_CUSTOMCONVERTER_100_45")]
    public Endpoint P45 { get; set; } = new();

    [Envar("BENCH_CUSTOMCONVERTER_100_46")]
    public Endpoint P46 { get; set; } = new();

    [Envar("BENCH_CUSTOMCONVERTER_100_47")]
    public Endpoint P47 { get; set; } = new();

    [Envar("BENCH_CUSTOMCONVERTER_100_48")]
    public Endpoint P48 { get; set; } = new();

    [Envar("BENCH_CUSTOMCONVERTER_100_49")]
    public Endpoint P49 { get; set; } = new();

    [Envar("BENCH_CUSTOMCONVERTER_100_50")]
    public Endpoint P50 { get; set; } = new();

    [Envar("BENCH_CUSTOMCONVERTER_100_51")]
    public Endpoint P51 { get; set; } = new();

    [Envar("BENCH_CUSTOMCONVERTER_100_52")]
    public Endpoint P52 { get; set; } = new();

    [Envar("BENCH_CUSTOMCONVERTER_100_53")]
    public Endpoint P53 { get; set; } = new();

    [Envar("BENCH_CUSTOMCONVERTER_100_54")]
    public Endpoint P54 { get; set; } = new();

    [Envar("BENCH_CUSTOMCONVERTER_100_55")]
    public Endpoint P55 { get; set; } = new();

    [Envar("BENCH_CUSTOMCONVERTER_100_56")]
    public Endpoint P56 { get; set; } = new();

    [Envar("BENCH_CUSTOMCONVERTER_100_57")]
    public Endpoint P57 { get; set; } = new();

    [Envar("BENCH_CUSTOMCONVERTER_100_58")]
    public Endpoint P58 { get; set; } = new();

    [Envar("BENCH_CUSTOMCONVERTER_100_59")]
    public Endpoint P59 { get; set; } = new();

    [Envar("BENCH_CUSTOMCONVERTER_100_60")]
    public Endpoint P60 { get; set; } = new();

    [Envar("BENCH_CUSTOMCONVERTER_100_61")]
    public Endpoint P61 { get; set; } = new();

    [Envar("BENCH_CUSTOMCONVERTER_100_62")]
    public Endpoint P62 { get; set; } = new();

    [Envar("BENCH_CUSTOMCONVERTER_100_63")]
    public Endpoint P63 { get; set; } = new();

    [Envar("BENCH_CUSTOMCONVERTER_100_64")]
    public Endpoint P64 { get; set; } = new();

    [Envar("BENCH_CUSTOMCONVERTER_100_65")]
    public Endpoint P65 { get; set; } = new();

    [Envar("BENCH_CUSTOMCONVERTER_100_66")]
    public Endpoint P66 { get; set; } = new();

    [Envar("BENCH_CUSTOMCONVERTER_100_67")]
    public Endpoint P67 { get; set; } = new();

    [Envar("BENCH_CUSTOMCONVERTER_100_68")]
    public Endpoint P68 { get; set; } = new();

    [Envar("BENCH_CUSTOMCONVERTER_100_69")]
    public Endpoint P69 { get; set; } = new();

    [Envar("BENCH_CUSTOMCONVERTER_100_70")]
    public Endpoint P70 { get; set; } = new();

    [Envar("BENCH_CUSTOMCONVERTER_100_71")]
    public Endpoint P71 { get; set; } = new();

    [Envar("BENCH_CUSTOMCONVERTER_100_72")]
    public Endpoint P72 { get; set; } = new();

    [Envar("BENCH_CUSTOMCONVERTER_100_73")]
    public Endpoint P73 { get; set; } = new();

    [Envar("BENCH_CUSTOMCONVERTER_100_74")]
    public Endpoint P74 { get; set; } = new();

    [Envar("BENCH_CUSTOMCONVERTER_100_75")]
    public Endpoint P75 { get; set; } = new();

    [Envar("BENCH_CUSTOMCONVERTER_100_76")]
    public Endpoint P76 { get; set; } = new();

    [Envar("BENCH_CUSTOMCONVERTER_100_77")]
    public Endpoint P77 { get; set; } = new();

    [Envar("BENCH_CUSTOMCONVERTER_100_78")]
    public Endpoint P78 { get; set; } = new();

    [Envar("BENCH_CUSTOMCONVERTER_100_79")]
    public Endpoint P79 { get; set; } = new();

    [Envar("BENCH_CUSTOMCONVERTER_100_80")]
    public Endpoint P80 { get; set; } = new();

    [Envar("BENCH_CUSTOMCONVERTER_100_81")]
    public Endpoint P81 { get; set; } = new();

    [Envar("BENCH_CUSTOMCONVERTER_100_82")]
    public Endpoint P82 { get; set; } = new();

    [Envar("BENCH_CUSTOMCONVERTER_100_83")]
    public Endpoint P83 { get; set; } = new();

    [Envar("BENCH_CUSTOMCONVERTER_100_84")]
    public Endpoint P84 { get; set; } = new();

    [Envar("BENCH_CUSTOMCONVERTER_100_85")]
    public Endpoint P85 { get; set; } = new();

    [Envar("BENCH_CUSTOMCONVERTER_100_86")]
    public Endpoint P86 { get; set; } = new();

    [Envar("BENCH_CUSTOMCONVERTER_100_87")]
    public Endpoint P87 { get; set; } = new();

    [Envar("BENCH_CUSTOMCONVERTER_100_88")]
    public Endpoint P88 { get; set; } = new();

    [Envar("BENCH_CUSTOMCONVERTER_100_89")]
    public Endpoint P89 { get; set; } = new();

    [Envar("BENCH_CUSTOMCONVERTER_100_90")]
    public Endpoint P90 { get; set; } = new();

    [Envar("BENCH_CUSTOMCONVERTER_100_91")]
    public Endpoint P91 { get; set; } = new();

    [Envar("BENCH_CUSTOMCONVERTER_100_92")]
    public Endpoint P92 { get; set; } = new();

    [Envar("BENCH_CUSTOMCONVERTER_100_93")]
    public Endpoint P93 { get; set; } = new();

    [Envar("BENCH_CUSTOMCONVERTER_100_94")]
    public Endpoint P94 { get; set; } = new();

    [Envar("BENCH_CUSTOMCONVERTER_100_95")]
    public Endpoint P95 { get; set; } = new();

    [Envar("BENCH_CUSTOMCONVERTER_100_96")]
    public Endpoint P96 { get; set; } = new();

    [Envar("BENCH_CUSTOMCONVERTER_100_97")]
    public Endpoint P97 { get; set; } = new();

    [Envar("BENCH_CUSTOMCONVERTER_100_98")]
    public Endpoint P98 { get; set; } = new();

    [Envar("BENCH_CUSTOMCONVERTER_100_99")]
    public Endpoint P99 { get; set; } = new();
}

public sealed class AbsentProps1
{
    [Envar("BENCH_ABSENT_1_0")]
    public string P0 { get; set; } = string.Empty;
}

public sealed class AbsentProps10
{
    [Envar("BENCH_ABSENT_10_0")]
    public string P0 { get; set; } = string.Empty;

    [Envar("BENCH_ABSENT_10_1")]
    public string P1 { get; set; } = string.Empty;

    [Envar("BENCH_ABSENT_10_2")]
    public string P2 { get; set; } = string.Empty;

    [Envar("BENCH_ABSENT_10_3")]
    public string P3 { get; set; } = string.Empty;

    [Envar("BENCH_ABSENT_10_4")]
    public string P4 { get; set; } = string.Empty;

    [Envar("BENCH_ABSENT_10_5")]
    public string P5 { get; set; } = string.Empty;

    [Envar("BENCH_ABSENT_10_6")]
    public string P6 { get; set; } = string.Empty;

    [Envar("BENCH_ABSENT_10_7")]
    public string P7 { get; set; } = string.Empty;

    [Envar("BENCH_ABSENT_10_8")]
    public string P8 { get; set; } = string.Empty;

    [Envar("BENCH_ABSENT_10_9")]
    public string P9 { get; set; } = string.Empty;
}

public sealed class AbsentProps100
{
    [Envar("BENCH_ABSENT_100_0")]
    public string P0 { get; set; } = string.Empty;

    [Envar("BENCH_ABSENT_100_1")]
    public string P1 { get; set; } = string.Empty;

    [Envar("BENCH_ABSENT_100_2")]
    public string P2 { get; set; } = string.Empty;

    [Envar("BENCH_ABSENT_100_3")]
    public string P3 { get; set; } = string.Empty;

    [Envar("BENCH_ABSENT_100_4")]
    public string P4 { get; set; } = string.Empty;

    [Envar("BENCH_ABSENT_100_5")]
    public string P5 { get; set; } = string.Empty;

    [Envar("BENCH_ABSENT_100_6")]
    public string P6 { get; set; } = string.Empty;

    [Envar("BENCH_ABSENT_100_7")]
    public string P7 { get; set; } = string.Empty;

    [Envar("BENCH_ABSENT_100_8")]
    public string P8 { get; set; } = string.Empty;

    [Envar("BENCH_ABSENT_100_9")]
    public string P9 { get; set; } = string.Empty;

    [Envar("BENCH_ABSENT_100_10")]
    public string P10 { get; set; } = string.Empty;

    [Envar("BENCH_ABSENT_100_11")]
    public string P11 { get; set; } = string.Empty;

    [Envar("BENCH_ABSENT_100_12")]
    public string P12 { get; set; } = string.Empty;

    [Envar("BENCH_ABSENT_100_13")]
    public string P13 { get; set; } = string.Empty;

    [Envar("BENCH_ABSENT_100_14")]
    public string P14 { get; set; } = string.Empty;

    [Envar("BENCH_ABSENT_100_15")]
    public string P15 { get; set; } = string.Empty;

    [Envar("BENCH_ABSENT_100_16")]
    public string P16 { get; set; } = string.Empty;

    [Envar("BENCH_ABSENT_100_17")]
    public string P17 { get; set; } = string.Empty;

    [Envar("BENCH_ABSENT_100_18")]
    public string P18 { get; set; } = string.Empty;

    [Envar("BENCH_ABSENT_100_19")]
    public string P19 { get; set; } = string.Empty;

    [Envar("BENCH_ABSENT_100_20")]
    public string P20 { get; set; } = string.Empty;

    [Envar("BENCH_ABSENT_100_21")]
    public string P21 { get; set; } = string.Empty;

    [Envar("BENCH_ABSENT_100_22")]
    public string P22 { get; set; } = string.Empty;

    [Envar("BENCH_ABSENT_100_23")]
    public string P23 { get; set; } = string.Empty;

    [Envar("BENCH_ABSENT_100_24")]
    public string P24 { get; set; } = string.Empty;

    [Envar("BENCH_ABSENT_100_25")]
    public string P25 { get; set; } = string.Empty;

    [Envar("BENCH_ABSENT_100_26")]
    public string P26 { get; set; } = string.Empty;

    [Envar("BENCH_ABSENT_100_27")]
    public string P27 { get; set; } = string.Empty;

    [Envar("BENCH_ABSENT_100_28")]
    public string P28 { get; set; } = string.Empty;

    [Envar("BENCH_ABSENT_100_29")]
    public string P29 { get; set; } = string.Empty;

    [Envar("BENCH_ABSENT_100_30")]
    public string P30 { get; set; } = string.Empty;

    [Envar("BENCH_ABSENT_100_31")]
    public string P31 { get; set; } = string.Empty;

    [Envar("BENCH_ABSENT_100_32")]
    public string P32 { get; set; } = string.Empty;

    [Envar("BENCH_ABSENT_100_33")]
    public string P33 { get; set; } = string.Empty;

    [Envar("BENCH_ABSENT_100_34")]
    public string P34 { get; set; } = string.Empty;

    [Envar("BENCH_ABSENT_100_35")]
    public string P35 { get; set; } = string.Empty;

    [Envar("BENCH_ABSENT_100_36")]
    public string P36 { get; set; } = string.Empty;

    [Envar("BENCH_ABSENT_100_37")]
    public string P37 { get; set; } = string.Empty;

    [Envar("BENCH_ABSENT_100_38")]
    public string P38 { get; set; } = string.Empty;

    [Envar("BENCH_ABSENT_100_39")]
    public string P39 { get; set; } = string.Empty;

    [Envar("BENCH_ABSENT_100_40")]
    public string P40 { get; set; } = string.Empty;

    [Envar("BENCH_ABSENT_100_41")]
    public string P41 { get; set; } = string.Empty;

    [Envar("BENCH_ABSENT_100_42")]
    public string P42 { get; set; } = string.Empty;

    [Envar("BENCH_ABSENT_100_43")]
    public string P43 { get; set; } = string.Empty;

    [Envar("BENCH_ABSENT_100_44")]
    public string P44 { get; set; } = string.Empty;

    [Envar("BENCH_ABSENT_100_45")]
    public string P45 { get; set; } = string.Empty;

    [Envar("BENCH_ABSENT_100_46")]
    public string P46 { get; set; } = string.Empty;

    [Envar("BENCH_ABSENT_100_47")]
    public string P47 { get; set; } = string.Empty;

    [Envar("BENCH_ABSENT_100_48")]
    public string P48 { get; set; } = string.Empty;

    [Envar("BENCH_ABSENT_100_49")]
    public string P49 { get; set; } = string.Empty;

    [Envar("BENCH_ABSENT_100_50")]
    public string P50 { get; set; } = string.Empty;

    [Envar("BENCH_ABSENT_100_51")]
    public string P51 { get; set; } = string.Empty;

    [Envar("BENCH_ABSENT_100_52")]
    public string P52 { get; set; } = string.Empty;

    [Envar("BENCH_ABSENT_100_53")]
    public string P53 { get; set; } = string.Empty;

    [Envar("BENCH_ABSENT_100_54")]
    public string P54 { get; set; } = string.Empty;

    [Envar("BENCH_ABSENT_100_55")]
    public string P55 { get; set; } = string.Empty;

    [Envar("BENCH_ABSENT_100_56")]
    public string P56 { get; set; } = string.Empty;

    [Envar("BENCH_ABSENT_100_57")]
    public string P57 { get; set; } = string.Empty;

    [Envar("BENCH_ABSENT_100_58")]
    public string P58 { get; set; } = string.Empty;

    [Envar("BENCH_ABSENT_100_59")]
    public string P59 { get; set; } = string.Empty;

    [Envar("BENCH_ABSENT_100_60")]
    public string P60 { get; set; } = string.Empty;

    [Envar("BENCH_ABSENT_100_61")]
    public string P61 { get; set; } = string.Empty;

    [Envar("BENCH_ABSENT_100_62")]
    public string P62 { get; set; } = string.Empty;

    [Envar("BENCH_ABSENT_100_63")]
    public string P63 { get; set; } = string.Empty;

    [Envar("BENCH_ABSENT_100_64")]
    public string P64 { get; set; } = string.Empty;

    [Envar("BENCH_ABSENT_100_65")]
    public string P65 { get; set; } = string.Empty;

    [Envar("BENCH_ABSENT_100_66")]
    public string P66 { get; set; } = string.Empty;

    [Envar("BENCH_ABSENT_100_67")]
    public string P67 { get; set; } = string.Empty;

    [Envar("BENCH_ABSENT_100_68")]
    public string P68 { get; set; } = string.Empty;

    [Envar("BENCH_ABSENT_100_69")]
    public string P69 { get; set; } = string.Empty;

    [Envar("BENCH_ABSENT_100_70")]
    public string P70 { get; set; } = string.Empty;

    [Envar("BENCH_ABSENT_100_71")]
    public string P71 { get; set; } = string.Empty;

    [Envar("BENCH_ABSENT_100_72")]
    public string P72 { get; set; } = string.Empty;

    [Envar("BENCH_ABSENT_100_73")]
    public string P73 { get; set; } = string.Empty;

    [Envar("BENCH_ABSENT_100_74")]
    public string P74 { get; set; } = string.Empty;

    [Envar("BENCH_ABSENT_100_75")]
    public string P75 { get; set; } = string.Empty;

    [Envar("BENCH_ABSENT_100_76")]
    public string P76 { get; set; } = string.Empty;

    [Envar("BENCH_ABSENT_100_77")]
    public string P77 { get; set; } = string.Empty;

    [Envar("BENCH_ABSENT_100_78")]
    public string P78 { get; set; } = string.Empty;

    [Envar("BENCH_ABSENT_100_79")]
    public string P79 { get; set; } = string.Empty;

    [Envar("BENCH_ABSENT_100_80")]
    public string P80 { get; set; } = string.Empty;

    [Envar("BENCH_ABSENT_100_81")]
    public string P81 { get; set; } = string.Empty;

    [Envar("BENCH_ABSENT_100_82")]
    public string P82 { get; set; } = string.Empty;

    [Envar("BENCH_ABSENT_100_83")]
    public string P83 { get; set; } = string.Empty;

    [Envar("BENCH_ABSENT_100_84")]
    public string P84 { get; set; } = string.Empty;

    [Envar("BENCH_ABSENT_100_85")]
    public string P85 { get; set; } = string.Empty;

    [Envar("BENCH_ABSENT_100_86")]
    public string P86 { get; set; } = string.Empty;

    [Envar("BENCH_ABSENT_100_87")]
    public string P87 { get; set; } = string.Empty;

    [Envar("BENCH_ABSENT_100_88")]
    public string P88 { get; set; } = string.Empty;

    [Envar("BENCH_ABSENT_100_89")]
    public string P89 { get; set; } = string.Empty;

    [Envar("BENCH_ABSENT_100_90")]
    public string P90 { get; set; } = string.Empty;

    [Envar("BENCH_ABSENT_100_91")]
    public string P91 { get; set; } = string.Empty;

    [Envar("BENCH_ABSENT_100_92")]
    public string P92 { get; set; } = string.Empty;

    [Envar("BENCH_ABSENT_100_93")]
    public string P93 { get; set; } = string.Empty;

    [Envar("BENCH_ABSENT_100_94")]
    public string P94 { get; set; } = string.Empty;

    [Envar("BENCH_ABSENT_100_95")]
    public string P95 { get; set; } = string.Empty;

    [Envar("BENCH_ABSENT_100_96")]
    public string P96 { get; set; } = string.Empty;

    [Envar("BENCH_ABSENT_100_97")]
    public string P97 { get; set; } = string.Empty;

    [Envar("BENCH_ABSENT_100_98")]
    public string P98 { get; set; } = string.Empty;

    [Envar("BENCH_ABSENT_100_99")]
    public string P99 { get; set; } = string.Empty;
}
