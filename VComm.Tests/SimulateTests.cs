using VComm.Core.Functions;
using WindowsInput.Native;
using Xunit;

namespace VComm.Tests;

public sealed class SimulateTests
{
    [Theory]
    [InlineData("(hOLD) {MMB}", "Middle", true)]
    [InlineData("{mouse3}", "Middle", false)]
    [InlineData("{LBUTTON}", "Left", false)]
    [InlineData("{MOUSE4}", "XButton1", false)]
    [InlineData("wheelDown", "WheelDown", false)]
    public void ParsesMouseInputs(string token, string expected, bool hold)
    {
        bool parsed = Simulate.TryParseInput(token, out Simulate.ParsedInput input);

        Assert.True(parsed);
        Assert.Equal(Enum.Parse<Simulate.MouseInput>(expected), input.Mouse);
        Assert.Null(input.Key);
        Assert.Equal(hold, input.Hold);
    }

    [Theory]
    [InlineData("{X}", VirtualKeyCode.VK_X, false)]
    [InlineData("(HOLD){CTRL}", VirtualKeyCode.CONTROL, true)]
    [InlineData("{CAPSLOCK}", VirtualKeyCode.CAPITAL, false)]
    [InlineData("(HOLD){LCTRL}", VirtualKeyCode.LCONTROL, true)]
    [InlineData("{RIGHTCTRL}", VirtualKeyCode.RCONTROL, false)]
    [InlineData("{/}", VirtualKeyCode.OEM_2, false)]
    public void ParsesKeyboardInputs(string token, VirtualKeyCode expected, bool hold)
    {
        bool parsed = Simulate.TryParseInput(token, out Simulate.ParsedInput input);

        Assert.True(parsed);
        Assert.Equal(expected, input.Key);
        Assert.Null(input.Mouse);
        Assert.Equal(hold, input.Hold);
    }

    [Theory]
    [InlineData("")]
    [InlineData("{definitely-not-a-key}")]
    [InlineData("(HOLD){WHEELUP}")]
    public void RejectsInvalidInputs(string token)
    {
        Assert.False(Simulate.TryParseInput(token, out _));
    }

    [Fact]
    public void ParsesHoldMiddleMouseThenXChord()
    {
        Assert.True(Simulate.TryParseInput("(HOLD){MMB}", out Simulate.ParsedInput middleMouse));
        Assert.True(Simulate.TryParseInput("{X}", out Simulate.ParsedInput x));

        Assert.True(middleMouse.Hold);
        Assert.Equal(Simulate.MouseInput.Middle, middleMouse.Mouse);
        Assert.False(x.Hold);
        Assert.Equal(VirtualKeyCode.VK_X, x.Key);
    }

    [Fact]
    public void ParsesPersistentControlAndArrowSequence()
    {
        string[] sequence = { "(HOLD){LCTRL}", "{UP}", "{LEFT}", "{RIGHT}", "{DOWN}" };

        Simulate.ParsedInput[] inputs = sequence.Select(token =>
        {
            Assert.True(Simulate.TryParseInput(token, out Simulate.ParsedInput input));
            return input;
        }).ToArray();

        Assert.True(inputs[0].Hold);
        Assert.Equal(VirtualKeyCode.LCONTROL, inputs[0].Key);
        Assert.All(inputs.Skip(1), input => Assert.False(input.Hold));
    }
}
