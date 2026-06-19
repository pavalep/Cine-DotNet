using System;
using Cine.Avalonia.Services;
using Shouldly;
using Xunit;

namespace Cine.Tests.ViewModels;

public class TimeParsingUtilityTests
{
    [Theory]
    [InlineData("90",    "00:01:30")]
    [InlineData("5:30",  "00:05:30")]
    [InlineData("1:23:45","01:23:45")]
    [InlineData("0",     "00:00:00")]
    [InlineData("0:00",  "00:00:00")]
    [InlineData("0:00:00","00:00:00")]
    [InlineData("125",   "00:02:05")]  // bare seconds > 60
    [InlineData("59:59", "00:59:59")]  // max MM:SS
    public void TryParseTime_Valid_ReturnsExpected(string input, string expected)
    {
        var result = TimeParsingUtility.TryParseTime(input);
        result.ShouldNotBeNull();
        result.Value.ToString("hh\\:mm\\:ss").ShouldBe(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("abc")]
    [InlineData("1:2:3:4")]
    [InlineData("-1:00")]
    [InlineData("-90")]
    [InlineData("1:60")]    // seconds >= 60
    [InlineData("1:99")]    // seconds >= 60
    [InlineData("1:80:00")] // minutes >= 60
    public void TryParseTime_Invalid_ReturnsNull(string? input)
    {
        TimeParsingUtility.TryParseTime(input).ShouldBeNull();
    }

    [Fact]
    public void TryParseTime_TrimmedInput_Works()
    {
        var result = TimeParsingUtility.TryParseTime("  1:30  ");
        result.ShouldNotBeNull();
        result.Value.ShouldBe(new TimeSpan(0, 1, 30));
    }

    [Fact]
    public void TryParseTime_ExtremeValue_StillParses()
    {
        var result = TimeParsingUtility.TryParseTime("9999999:00:00");
        result.ShouldNotBeNull();
        result.Value.TotalDays.ShouldBeGreaterThan(400_000);
    }

    [Fact]
    public void TryParseTime_PartialSeconds_NotSupported()
    {
        TimeParsingUtility.TryParseTime("1.5").ShouldBeNull();
    }
}
