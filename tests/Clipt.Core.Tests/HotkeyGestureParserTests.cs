using Clipt.Core.Services;
using FluentAssertions;
using Xunit;

namespace Clipt.Core.Tests;

public sealed class HotkeyGestureParserTests
{
    [Fact]
    public void Parse_CtrlShiftV_ReturnsParsed()
    {
        var result = HotkeyGestureParser.Parse("Ctrl+Shift+V");

        result.Should().NotBeNull();
        result!.Key.Should().Be("V");
        result.Modifiers.Should().BeEquivalentTo(["Ctrl", "Shift"]);
    }

    [Fact]
    public void Parse_ControlSynonyms_ReturnsNormalized()
    {
        var result = HotkeyGestureParser.Parse("Control+Shift+V");

        result.Should().NotBeNull();
        result!.Key.Should().Be("V");
        result.Modifiers.Should().BeEquivalentTo(["Ctrl", "Shift"]);
    }

    [Fact]
    public void Parse_WinSynonyms_ReturnsNormalized()
    {
        var result = HotkeyGestureParser.Parse("Windows+L");

        result.Should().NotBeNull();
        result!.Key.Should().Be("L");
        result.Modifiers.Should().BeEquivalentTo(["Win"]);
    }

    [Fact]
    public void Parse_AltF4_ReturnsParsed()
    {
        var result = HotkeyGestureParser.Parse("Alt+F4");

        result.Should().NotBeNull();
        result!.Key.Should().Be("F4");
        result.Modifiers.Should().BeEquivalentTo(["Alt"]);
    }

    [Fact]
    public void Parse_CtrlAltDelete_ReturnsParsed()
    {
        var result = HotkeyGestureParser.Parse("Ctrl+Alt+Delete");

        result.Should().NotBeNull();
        result!.Key.Should().Be("Delete");
        result.Modifiers.Should().BeEquivalentTo(["Ctrl", "Alt"]);
    }

    [Fact]
    public void Parse_OnlyModifier_ReturnsNull()
    {
        var result = HotkeyGestureParser.Parse("Ctrl+Shift");

        result.Should().BeNull();
    }

    [Fact]
    public void Parse_OnlyKey_ReturnsNull()
    {
        var result = HotkeyGestureParser.Parse("V");

        result.Should().BeNull();
    }

    [Fact]
    public void Parse_EmptyString_ReturnsNull()
    {
        var result = HotkeyGestureParser.Parse(string.Empty);

        result.Should().BeNull();
    }

    [Fact]
    public void Parse_WhitespaceString_ReturnsNull()
    {
        var result = HotkeyGestureParser.Parse("   ");

        result.Should().BeNull();
    }

    [Fact]
    public void Parse_NullString_ReturnsNull()
    {
        var result = HotkeyGestureParser.Parse(null);

        result.Should().BeNull();
    }

    [Fact]
    public void Parse_MultipleKeys_ReturnsNull()
    {
        var result = HotkeyGestureParser.Parse("Ctrl+Shift+V+X");

        result.Should().BeNull();
    }

    [Fact]
    public void Parse_CaseInsensitiveModifiers_StillWorks()
    {
        var result = HotkeyGestureParser.Parse("ctrl+SHIFT+v");

        result.Should().NotBeNull();
        result!.Key.Should().Be("v");
        result.Modifiers.Should().BeEquivalentTo(["Ctrl", "Shift"]);
    }

    [Fact]
    public void Parse_ModifiersInAnyOrder_ReturnsAllModifiers()
    {
        var result = HotkeyGestureParser.Parse("Shift+Ctrl+V");

        result.Should().NotBeNull();
        result!.Key.Should().Be("V");
        result.Modifiers.Should().BeEquivalentTo(["Ctrl", "Shift"]);
    }
}
