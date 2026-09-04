using System;
using System.ComponentModel;
using System.Globalization;
using Xunit;

namespace FriendlyEnvars.Tests;

public class DefaultEnvarPropertyBinderTests
{
    private readonly DefaultEnvarPropertyBinder _binder = new();
    private readonly CultureInfo _invariantCulture = CultureInfo.InvariantCulture;

    [Fact]
    public void Convert_StringType_ReturnsValueUnchanged()
    {
        var result = _binder.Convert("test value", typeof(string), _invariantCulture);

        Assert.Equal("test value", result);
    }

    [Theory]
    [InlineData("true", true)]
    [InlineData("false", false)]
    [InlineData("True", true)]
    [InlineData("False", false)]
    [InlineData("TRUE", true)]
    [InlineData("FALSE", false)]
    public void Convert_BoolType_ParsesCorrectly(string value, bool expected)
    {
        var result = _binder.Convert(value, typeof(bool), _invariantCulture);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("0", (byte)0)]
    [InlineData("255", (byte)255)]
    [InlineData("42", (byte)42)]
    public void Convert_ByteType_ParsesCorrectly(string value, byte expected)
    {
        var result = _binder.Convert(value, typeof(byte), _invariantCulture);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("-128", (sbyte)-128)]
    [InlineData("127", (sbyte)127)]
    [InlineData("0", (sbyte)0)]
    [InlineData("42", (sbyte)42)]
    public void Convert_SByteType_ParsesCorrectly(string value, sbyte expected)
    {
        var result = _binder.Convert(value, typeof(sbyte), _invariantCulture);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("a", 'a')]
    [InlineData("Z", 'Z')]
    [InlineData("5", '5')]
    [InlineData(" ", ' ')]
    public void Convert_CharType_ParsesCorrectly(string value, char expected)
    {
        var result = _binder.Convert(value, typeof(char), _invariantCulture);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("")]
    [InlineData("ab")]
    [InlineData("abc")]
    public void Convert_CharType_ThrowsForInvalidLength(string value)
    {
        var exception = Assert.Throws<FormatException>(() => _binder.Convert(value, typeof(char), _invariantCulture));

        Assert.Contains("exactly one character", exception.Message);
    }

    [Theory]
    [InlineData("-32768", (short)-32768)]
    [InlineData("32767", (short)32767)]
    [InlineData("0", (short)0)]
    [InlineData("42", (short)42)]
    public void Convert_ShortType_ParsesCorrectly(string value, short expected)
    {
        var result = _binder.Convert(value, typeof(short), _invariantCulture);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("0", (ushort)0)]
    [InlineData("65535", (ushort)65535)]
    [InlineData("42", (ushort)42)]
    public void Convert_UShortType_ParsesCorrectly(string value, ushort expected)
    {
        var result = _binder.Convert(value, typeof(ushort), _invariantCulture);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("-2147483648", -2147483648)]
    [InlineData("2147483647", 2147483647)]
    [InlineData("0", 0)]
    [InlineData("42", 42)]
    public void Convert_IntType_ParsesCorrectly(string value, int expected)
    {
        var result = _binder.Convert(value, typeof(int), _invariantCulture);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("0", 0U)]
    [InlineData("4294967295", 4294967295U)]
    [InlineData("42", 42U)]
    public void Convert_UIntType_ParsesCorrectly(string value, uint expected)
    {
        var result = _binder.Convert(value, typeof(uint), _invariantCulture);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("-9223372036854775808", -9223372036854775808L)]
    [InlineData("9223372036854775807", 9223372036854775807L)]
    [InlineData("0", 0L)]
    [InlineData("42", 42L)]
    public void Convert_LongType_ParsesCorrectly(string value, long expected)
    {
        var result = _binder.Convert(value, typeof(long), _invariantCulture);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("0", 0UL)]
    [InlineData("18446744073709551615", 18446744073709551615UL)]
    [InlineData("42", 42UL)]
    public void Convert_ULongType_ParsesCorrectly(string value, ulong expected)
    {
        var result = _binder.Convert(value, typeof(ulong), _invariantCulture);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("3.14", 3.14f)]
    [InlineData("-3.14", -3.14f)]
    [InlineData("0", 0f)]
    [InlineData("42", 42f)]
    [InlineData("3.40282347E+38", float.MaxValue)]
    [InlineData("-3.40282347E+38", float.MinValue)]
    public void Convert_FloatType_ParsesCorrectly(string value, float expected)
    {
        var result = _binder.Convert(value, typeof(float), _invariantCulture);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("3.14", 3.14d)]
    [InlineData("-3.14", -3.14d)]
    [InlineData("0", 0d)]
    [InlineData("42", 42d)]
    [InlineData("1.7976931348623157E+308", double.MaxValue)]
    [InlineData("-1.7976931348623157E+308", double.MinValue)]
    public void Convert_DoubleType_ParsesCorrectly(string value, double expected)
    {
        var result = _binder.Convert(value, typeof(double), _invariantCulture);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("3.14", "3.14")]
    [InlineData("-3.14", "-3.14")]
    [InlineData("0", "0")]
    [InlineData("42", "42")]
    [InlineData("79228162514264337593543950335", "79228162514264337593543950335")]
    public void Convert_DecimalType_ParsesCorrectly(string value, string expectedStr)
    {
        var expected = decimal.Parse(expectedStr, _invariantCulture);
        var result = _binder.Convert(value, typeof(decimal), _invariantCulture);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    [InlineData("12345678-1234-5678-9012-123456789012")]
    [InlineData("FFFFFFFF-FFFF-FFFF-FFFF-FFFFFFFFFFFF")]
    public void Convert_GuidType_ParsesCorrectly(string value)
    {
        var expected = Guid.Parse(value);
        var result = _binder.Convert(value, typeof(Guid), _invariantCulture);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("https://example.com")]
    [InlineData("http://localhost:8080")]
    [InlineData("ftp://files.example.com")]
    [InlineData("file:///C:/temp/file.txt")]
    public void Convert_UriType_ParsesCorrectly(string value)
    {
        var expected = new Uri(value);
        var result = _binder.Convert(value, typeof(Uri), _invariantCulture);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("00:00:00", 0, 0, 0, 0)]
    [InlineData("01:30:45", 0, 1, 30, 45)]
    [InlineData("23:59:59", 0, 23, 59, 59)]
    [InlineData("1.02:03:04", 1, 2, 3, 4)]
    public void Convert_TimeSpanType_ParsesCorrectly(string value, int days, int hours, int minutes, int seconds)
    {
        var expected = new TimeSpan(days, hours, minutes, seconds);
        var result = _binder.Convert(value, typeof(TimeSpan), _invariantCulture);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("2023-01-01")]
    [InlineData("2023-12-31T23:59:59")]
    [InlineData("2023-06-15T12:30:45.123")]
    public void Convert_DateTimeType_ParsesCorrectly(string value)
    {
        var expected = DateTime.Parse(value, _invariantCulture);
        var result = _binder.Convert(value, typeof(DateTime), _invariantCulture);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("2023-01-01")]
    [InlineData("2023-12-31T23:59:59Z")]
    [InlineData("2023-06-15T12:30:45.123+02:00")]
    public void Convert_DateTimeOffsetType_ParsesCorrectly(string value)
    {
        var expected = DateTimeOffset.Parse(value, _invariantCulture);
        var result = _binder.Convert(value, typeof(DateTimeOffset), _invariantCulture);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("2023-01-01")]
    [InlineData("2023-12-31")]
    [InlineData("2023-06-15")]
    public void Convert_DateOnlyType_ParsesCorrectly(string value)
    {
        var expected = DateOnly.Parse(value, _invariantCulture);
        var result = _binder.Convert(value, typeof(DateOnly), _invariantCulture);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("00:00:00")]
    [InlineData("12:30:45")]
    [InlineData("23:59:59")]
    [InlineData("12:30:45.123")]
    public void Convert_TimeOnlyType_ParsesCorrectly(string value)
    {
        var expected = TimeOnly.Parse(value, _invariantCulture);
        var result = _binder.Convert(value, typeof(TimeOnly), _invariantCulture);

        Assert.Equal(expected, result);
    }

    public enum TestEnum
    {
        Value1,
        Value2,
        CamelCaseValue
    }

    [Theory]
    [InlineData("Value1", TestEnum.Value1)]
    [InlineData("Value2", TestEnum.Value2)]
    [InlineData("CamelCaseValue", TestEnum.CamelCaseValue)]
    [InlineData("value1", TestEnum.Value1)]
    [InlineData("VALUE1", TestEnum.Value1)]
    [InlineData("camelcasevalue", TestEnum.CamelCaseValue)]
    public void Convert_EnumType_ParsesCorrectly(string value, TestEnum expected)
    {
        var result = _binder.Convert(value, typeof(TestEnum), _invariantCulture);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void Convert_NullableTypes_ParsesCorrectly()
    {
        var intResult = _binder.Convert("42", typeof(int?), _invariantCulture);
        Assert.Equal(42, intResult);

        var boolResult = _binder.Convert("true", typeof(bool?), _invariantCulture);
        Assert.Equal(true, boolResult);

        var doubleResult = _binder.Convert("3.14", typeof(double?), _invariantCulture);
        Assert.Equal(3.14, doubleResult);
    }

    [Theory]
    [InlineData("3.14", "en-US")]
    [InlineData("3,14", "de-DE")]
    [InlineData("3.14", "")]
    public void Convert_CultureSpecificParsing_ParsesCorrectly(string value, string cultureName)
    {
        var culture = string.IsNullOrEmpty(cultureName) ? CultureInfo.InvariantCulture : new CultureInfo(cultureName);
        var result = _binder.Convert(value, typeof(double), culture);

        Assert.Equal(3.14, result);
    }

    [Theory]
    [InlineData("1234", "en-US")]
    [InlineData("1234", "de-DE")]
    public void Convert_CultureSpecificNumberFormatting_ParsesCorrectly(string value, string cultureName)
    {
        var culture = new CultureInfo(cultureName);
        var result = _binder.Convert(value, typeof(int), culture);

        Assert.Equal(1234, result);
    }

    [Theory]
    [InlineData("not a number", typeof(int))]
    [InlineData("invalid", typeof(bool))]
    [InlineData("not-a-guid", typeof(Guid))]
    [InlineData("invalid-uri", typeof(Uri))]
    [InlineData("invalid-date", typeof(DateTime))]
    [InlineData("invalid-timespan", typeof(TimeSpan))]
    [InlineData("256", typeof(byte))]
    [InlineData("-1", typeof(uint))]
    public void Convert_InvalidValues_ThrowsExceptions(string value, Type targetType)
    {
        Assert.ThrowsAny<Exception>(() => _binder.Convert(value, targetType, _invariantCulture));
    }

    [Theory]
    [InlineData("InvalidEnumValue")]
    [InlineData("999")]
    [InlineData("")]
    public void Convert_InvalidEnumValues_ThrowsException(string value)
    {
        Assert.ThrowsAny<Exception>(() => _binder.Convert(value, typeof(TestEnum), _invariantCulture));
    }

    [TypeConverter(typeof(CustomTypeConverter))]
    public class CustomType
    {
        public string Value { get; init; } = "";
    }

    public class CustomTypeConverter : TypeConverter
    {
        public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType)
        {
            return sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);
        }

        public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
        {
            if (value is string stringValue)
            {
                return new CustomType { Value = stringValue };
            }

            return base.ConvertFrom(context, culture, value);
        }
    }

    [Theory]
    [InlineData("test value")]
    [InlineData("another test")]
    public void Convert_TypeConverterFallback_UsesTypeConverter(string value)
    {
        var result = _binder.Convert(value, typeof(CustomType), _invariantCulture);

        Assert.IsType<CustomType>(result);
        Assert.Equal(value, ((CustomType)result).Value);
    }

    public class UnsupportedType
    {
        public string Value { get; set; } = string.Empty;
    }

    [Fact]
    public void Convert_UnsupportedType_ThrowsEnvarsException()
    {
        Assert.Throws<NotSupportedException>(() => _binder.Convert("test", typeof(UnsupportedType), _invariantCulture));
    }

    [Theory]
    [InlineData("maybe", typeof(bool))]
    [InlineData("notanumber", typeof(int))]
    [InlineData("invalid-guid", typeof(Guid))]
    [InlineData("xyz", typeof(TestEnum))]
    public void Convert_InvalidFormats_ThrowsExpectedExceptions(string value, Type targetType)
    {
        Assert.ThrowsAny<Exception>(() => _binder.Convert(value, targetType, _invariantCulture));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t")]
    [InlineData("\n")]
    public void Convert_WhitespaceValues_HandledCorrectly(string value)
    {
        var stringResult = _binder.Convert(value, typeof(string), _invariantCulture);
        Assert.Equal(value, stringResult);

        if (value.Trim().Length == 0)
        {
            Assert.ThrowsAny<Exception>(() => _binder.Convert(value, typeof(int), _invariantCulture));
        }
    }

    [Theory]
    [InlineData("0", TestEnum.Value1)]
    [InlineData("1", TestEnum.Value2)]
    [InlineData("2", TestEnum.CamelCaseValue)]
    public void Convert_EnumFromNumericString_ParsesCorrectly(string value, TestEnum expected)
    {
        var result = _binder.Convert(value, typeof(TestEnum), _invariantCulture);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(typeof(bool?), "true", true)]
    [InlineData(typeof(int?), "42", 42)]
    [InlineData(typeof(double?), "3.14", 3.14)]
    [InlineData(typeof(TestEnum?), "Value1", TestEnum.Value1)]
    [InlineData(typeof(Guid?), "00000000-0000-0000-0000-000000000000", "00000000-0000-0000-0000-000000000000")]
    public void Convert_NullableTypesExtensive_ParsesCorrectly(Type targetType, string value, object expectedRaw)
    {
        var result = _binder.Convert(value, targetType, _invariantCulture);

        if (expectedRaw is string guidStr)
        {
            Assert.Equal(Guid.Parse(guidStr), result);
        }
        else
        {
            Assert.Equal(expectedRaw, result);
        }
    }

    [Theory]
    [InlineData("Infinity", double.PositiveInfinity)]
    [InlineData("-Infinity", double.NegativeInfinity)]
    [InlineData("NaN", double.NaN)]
    public void Convert_SpecialDoubleValues_ParsesCorrectly(string value, double expected)
    {
        var result = _binder.Convert(value, typeof(double), _invariantCulture);

        if (double.IsNaN(expected))
        {
            Assert.True(double.IsNaN((double)result!));
        }
        else
        {
            Assert.Equal(expected, result);
        }
    }

    [Theory]
    [InlineData("Infinity", float.PositiveInfinity)]
    [InlineData("-Infinity", float.NegativeInfinity)]
    [InlineData("NaN", float.NaN)]
    public void Convert_SpecialFloatValues_ParsesCorrectly(string value, float expected)
    {
        var result = _binder.Convert(value, typeof(float), _invariantCulture);

        if (float.IsNaN(expected))
        {
            Assert.True(float.IsNaN((float)result!));
        }
        else
        {
            Assert.Equal(expected, result);
        }
    }

    // Enum grammar across all underlying types, flags modes, and case collisions.

    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    // All flags fixtures use the same members and 0b111 allowed mask.
    [Flags] public enum FlagsByte : byte { None = 0, Read = 1, Write = 2, ReadWrite = 3, Execute = 4 }
    [Flags] public enum FlagsSByte : sbyte { None = 0, Read = 1, Write = 2, ReadWrite = 3, Execute = 4 }
    [Flags] public enum FlagsInt16 : short { None = 0, Read = 1, Write = 2, ReadWrite = 3, Execute = 4 }
    [Flags] public enum FlagsUInt16 : ushort { None = 0, Read = 1, Write = 2, ReadWrite = 3, Execute = 4 }
    [Flags] public enum FlagsInt32 : int { None = 0, Read = 1, Write = 2, ReadWrite = 3, Execute = 4 }
    [Flags] public enum FlagsUInt32 : uint { None = 0, Read = 1, Write = 2, ReadWrite = 3, Execute = 4 }
    [Flags] public enum FlagsInt64 : long { None = 0, Read = 1, Write = 2, ReadWrite = 3, Execute = 4 }
    [Flags] public enum FlagsUInt64 : ulong { None = 0, Read = 1, Write = 2, ReadWrite = 3, Execute = 4 }

    public enum PlainByte : byte { Zero = 0, One = 1, Two = 2 }
    public enum PlainSByte : sbyte { Zero = 0, One = 1, Two = 2 }
    public enum PlainInt16 : short { Zero = 0, One = 1, Two = 2 }
    public enum PlainUInt16 : ushort { Zero = 0, One = 1, Two = 2 }
    public enum PlainInt32 : int { Zero = 0, One = 1, Two = 2 }
    public enum PlainUInt32 : uint { Zero = 0, One = 1, Two = 2 }
    public enum PlainInt64 : long { Zero = 0, One = 1, Two = 2 }
    public enum PlainUInt64 : ulong { Zero = 0, One = 1, Two = 2 }

    [Flags] public enum FlagsWithNegative { None = 0, Read = 1, Write = 2, All = -1 }

    public enum PlainWithNegative { All = -1, Zero = 0, One = 1 }

    [Flags] public enum FlagsCollision { Read = 1, READ = 2, Same = 4, SAME = 4 }

    public enum PlainCollision { Read = 1, READ = 2, Same = 4, SAME = 4 }

    public static TheoryData<Type> FlagsTypes() =>
    [
        typeof(FlagsByte), typeof(FlagsSByte), typeof(FlagsInt16), typeof(FlagsUInt16),
        typeof(FlagsInt32), typeof(FlagsUInt32), typeof(FlagsInt64), typeof(FlagsUInt64)
    ];

    public static TheoryData<Type> PlainTypes() =>
    [
        typeof(PlainByte), typeof(PlainSByte), typeof(PlainInt16), typeof(PlainUInt16),
        typeof(PlainInt32), typeof(PlainUInt32), typeof(PlainInt64), typeof(PlainUInt64)
    ];

    private static string OverflowText(Type enumType)
    {
        return Type.GetTypeCode(Enum.GetUnderlyingType(enumType)) switch
        {
            TypeCode.SByte or TypeCode.Byte => "256",
            TypeCode.Int16 or TypeCode.UInt16 => "65536",
            TypeCode.Int32 or TypeCode.UInt32 => "4294967296",
            _ => "18446744073709551616"
        };
    }

    private object Convert(string text, Type enumType) => _binder.Convert(text, enumType, Invariant)!;

    private void AssertAccepts(Type enumType, string text, long expected)
    {
        object result = Convert(text, enumType);

        Assert.IsType(enumType, result);
        Assert.Equal(expected, System.Convert.ToInt64(result, Invariant));
    }

    private void AssertRejects(Type enumType, string text)
    {
        var exception = Assert.ThrowsAny<Exception>(() => Convert(text, enumType));

        // One-character inputs can occur in prose, so check the quoted form here.
        Assert.DoesNotContain($"'{text.Trim()}'", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(FlagsTypes))]
    public void Flags_AcceptsDeclaredNamesCompositesAndListsOnEveryUnderlyingType(Type enumType)
    {
        AssertAccepts(enumType, "Read", 1);
        AssertAccepts(enumType, "Write", 2);

        // A declared composite member.
        AssertAccepts(enumType, "ReadWrite", 3);

        // A comma-separated list of declared names.
        AssertAccepts(enumType, "Read,Write", 3);
        AssertAccepts(enumType, " Read , Execute ", 5);

        // Case-insensitive when no exact-case match exists.
        AssertAccepts(enumType, "read", 1);
        AssertAccepts(enumType, "READ", 1);

        AssertAccepts(enumType, "None", 0);
        AssertAccepts(enumType, "0", 0);

        // Numeric combinations inside the allowed mask.
        AssertAccepts(enumType, "3", 3);
        AssertAccepts(enumType, "5", 5);
        AssertAccepts(enumType, "7", 7);

        // The whole input is trimmed before parsing.
        AssertAccepts(enumType, " 3 ", 3);
    }

    [Theory]
    [MemberData(nameof(FlagsTypes))]
    public void Flags_RejectsEverythingOutsideTheGrammarOnEveryUnderlyingType(Type enumType)
    {
        AssertRejects(enumType, "Unknown");
        AssertRejects(enumType, "Read,Unknown");

        // Bit 8 is outside the allowed mask.
        AssertRejects(enumType, "8");
        AssertRejects(enumType, "9");

        AssertRejects(enumType, "-1");
        AssertRejects(enumType, "+3");
        AssertRejects(enumType, "0x3");
        AssertRejects(enumType, "3.0");
        AssertRejects(enumType, "1_0");

        // Numeric tokens are forbidden inside a list.
        AssertRejects(enumType, "Read,2");
        AssertRejects(enumType, "1,2");

        // Empty list elements and comma-only input.
        AssertRejects(enumType, "Read,");
        AssertRejects(enumType, ",Read");
        AssertRejects(enumType, "Read,,Write");
        AssertRejects(enumType, ",");

        AssertRejects(enumType, string.Empty);
        AssertRejects(enumType, "   ");
        AssertRejects(enumType, "\t\n");

        AssertRejects(enumType, OverflowText(enumType));

        // Non-ASCII digits are not decimal digits for this grammar.
        AssertRejects(enumType, "٣");
    }

    [Fact]
    public void Flags_AcceptsADeclaredNegativeMemberByNameButRejectsItWrittenAsANegativeNumber()
    {
        AssertAccepts(typeof(FlagsWithNegative), "All", -1);
        AssertAccepts(typeof(FlagsWithNegative), "all", -1);

        AssertRejects(typeof(FlagsWithNegative), "-1");

        // All = -1 contributes every bit to the allowed mask.
        AssertAccepts(typeof(FlagsWithNegative), "4294967295", -1);
    }

    [Theory]
    [MemberData(nameof(PlainTypes))]
    public void NonFlags_AcceptsOneDeclaredNameOrOneUnsignedDecimalDefinedValue(Type enumType)
    {
        AssertAccepts(enumType, "One", 1);
        AssertAccepts(enumType, "one", 1);
        AssertAccepts(enumType, "ONE", 1);
        AssertAccepts(enumType, " Two ", 2);

        AssertAccepts(enumType, "0", 0);
        AssertAccepts(enumType, "1", 1);
        AssertAccepts(enumType, "2", 2);
    }

    [Theory]
    [MemberData(nameof(PlainTypes))]
    public void NonFlags_RejectsListsSignsHexOverflowAndUndefinedValues(Type enumType)
    {
        // Non-flags enums never accept lists.
        AssertRejects(enumType, "One,Two");
        AssertRejects(enumType, "One, Two");
        AssertRejects(enumType, ",");

        AssertRejects(enumType, "+1");
        AssertRejects(enumType, "-1");
        AssertRejects(enumType, "0x1");
        AssertRejects(enumType, "1.0");

        // 3 converts cleanly but is not a declared member.
        AssertRejects(enumType, "3");
        AssertRejects(enumType, "Unknown");

        AssertRejects(enumType, string.Empty);
        AssertRejects(enumType, "   ");

        AssertRejects(enumType, OverflowText(enumType));
    }

    [Fact]
    public void NonFlags_AcceptsADeclaredNegativeMemberByNameButRejectsItWrittenNumericallyWithASign()
    {
        AssertAccepts(typeof(PlainWithNegative), "All", -1);
        AssertAccepts(typeof(PlainWithNegative), "all", -1);

        AssertRejects(typeof(PlainWithNegative), "-1");

        // The unsigned bit pattern is a defined value.
        AssertAccepts(typeof(PlainWithNegative), "4294967295", -1);
    }

    [Fact]
    public void Flags_ExactCaseNamesSelectTheirOwnValue()
    {
        AssertAccepts(typeof(FlagsCollision), "Read", 1);
        AssertAccepts(typeof(FlagsCollision), "READ", 2);
        AssertAccepts(typeof(FlagsCollision), "Same", 4);
        AssertAccepts(typeof(FlagsCollision), "SAME", 4);
    }

    [Fact]
    public void Flags_MixedCaseIsRejectedWhenCandidatesDisagreeAndAcceptedWhenTheyAgree()
    {
        // "read" matches names with different values.
        AssertRejects(typeof(FlagsCollision), "read");
        AssertRejects(typeof(FlagsCollision), "rEaD");

        // "same" matches names with the same value.
        AssertAccepts(typeof(FlagsCollision), "same", 4);
        AssertAccepts(typeof(FlagsCollision), "sAmE", 4);
    }

    [Fact]
    public void FlagsListTokens_FollowTheSameCollisionRule()
    {
        AssertAccepts(typeof(FlagsCollision), "Read,Same", 5);
        AssertAccepts(typeof(FlagsCollision), "READ,same", 6);
        AssertAccepts(typeof(FlagsCollision), "Read,READ,SAME", 7);

        AssertRejects(typeof(FlagsCollision), "read,Same");
        AssertRejects(typeof(FlagsCollision), "Same,read");
    }

    [Fact]
    public void NonFlagsNames_FollowTheSameCollisionRule()
    {
        AssertAccepts(typeof(PlainCollision), "Read", 1);
        AssertAccepts(typeof(PlainCollision), "READ", 2);
        AssertAccepts(typeof(PlainCollision), "Same", 4);
        AssertAccepts(typeof(PlainCollision), "SAME", 4);

        AssertAccepts(typeof(PlainCollision), "same", 4);
        AssertRejects(typeof(PlainCollision), "read");
    }

    [Fact]
    public void RejectionMessagesDoNotDiscloseTheValue()
    {
        // A distinctive value makes disclosure easy to detect.
        const string Sentinel = "QZXJKVWYPLMB-NOT-A-MEMBER-0000";

        var exception = Assert.ThrowsAny<Exception>(() => Convert(Sentinel, typeof(FlagsInt32)));

        Assert.DoesNotContain(Sentinel, exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(Sentinel, exception.ToString(), StringComparison.Ordinal);
        Assert.Contains(typeof(FlagsInt32).FullName!, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void NullableEnumsUseTheSameGrammar()
    {
        // Boxing a Nullable<T> yields the underlying value, so the result is a FlagsInt32.
        object result = Convert("Read,Write", typeof(FlagsInt32?));

        Assert.IsType<FlagsInt32>(result);
        Assert.Equal(FlagsInt32.ReadWrite, (FlagsInt32)result);

        AssertRejects(typeof(FlagsInt32?), "Read,2");
    }
}
