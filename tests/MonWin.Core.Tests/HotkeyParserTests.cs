using SystemMonitor.Services;
using Xunit;

namespace SystemMonitor.Tests;

public class HotkeyParserTests
{
    [Fact]
    public void TryParse_ModifiersAndLetter()
    {
        Assert.True(HotkeyParser.TryParse("Ctrl+Alt+M", out var mods, out var vk));
        Assert.Equal(HotkeyParser.ModControl | HotkeyParser.ModAlt, mods);
        Assert.Equal((uint)'M', vk);
    }

    [Theory]
    [InlineData("ctrl + shift + f5", HotkeyParser.ModControl | HotkeyParser.ModShift, 0x74u)]
    [InlineData("Win+Alt+9", HotkeyParser.ModWin | HotkeyParser.ModAlt, (uint)'9')]
    [InlineData("Control+Home", HotkeyParser.ModControl, 0x24u)]
    [InlineData("Alt+F24", HotkeyParser.ModAlt, 0x87u)]
    public void TryParse_AcceptsVariants(string text, uint expectedMods, uint expectedVk)
    {
        Assert.True(HotkeyParser.TryParse(text, out var mods, out var vk));
        Assert.Equal(expectedMods, mods);
        Assert.Equal(expectedVk, vk);
    }

    [Theory]
    [InlineData("")]
    [InlineData("M")]             // no modifier — would hijack normal typing
    [InlineData("Ctrl+Alt")]      // no key
    [InlineData("Ctrl+M+N")]      // two keys
    [InlineData("Ctrl+F25")]      // out of range
    [InlineData("Ctrl+Banana")]   // unknown key
    public void TryParse_RejectsInvalid(string text)
    {
        Assert.False(HotkeyParser.TryParse(text, out _, out _));
    }
}
