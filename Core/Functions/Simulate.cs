namespace VComm.Core.Functions
{
    internal static class Simulate
    {
        private const uint MouseLeftDown = 0x0002;
        private const uint MouseLeftUp = 0x0004;
        private const uint MouseRightDown = 0x0008;
        private const uint MouseRightUp = 0x0010;
        private const uint MouseMiddleDown = 0x0020;
        private const uint MouseMiddleUp = 0x0040;
        private const uint MouseXDown = 0x0080;
        private const uint MouseXUp = 0x0100;
        private const uint MouseWheel = 0x0800;
        private const uint MouseHorizontalWheel = 0x1000;
        private const uint XButton1 = 0x0001;
        private const uint XButton2 = 0x0002;
        private const int WheelDelta = 120;

        private static readonly InputSimulator Simulator = new InputSimulator();
        private static readonly SemaphoreSlim MacroLock = new SemaphoreSlim(1, 1);

        private static readonly IReadOnlyDictionary<string, string> KeyAliases =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["ALT"] = "MENU",
                ["BACKSPACE"] = "BACK",
                ["BKSP"] = "BACK",
                ["BREAK"] = "CANCEL",
                ["BS"] = "BACK",
                ["CAPSLOCK"] = "CAPITAL",
                ["CTRL"] = "CONTROL",
                ["LCTRL"] = "LCONTROL",
                ["LEFTCTRL"] = "LCONTROL",
                ["RCTRL"] = "RCONTROL",
                ["RIGHTCTRL"] = "RCONTROL",
                ["LEFTSHIFT"] = "LSHIFT",
                ["RIGHTSHIFT"] = "RSHIFT",
                ["LALT"] = "LMENU",
                ["LEFTALT"] = "LMENU",
                ["RALT"] = "RMENU",
                ["RIGHTALT"] = "RMENU",
                ["DEL"] = "DELETE",
                ["ENTER"] = "RETURN",
                ["ESC"] = "ESCAPE",
                ["INS"] = "INSERT",
                ["PAGEDOWN"] = "NEXT",
                ["PAGEUP"] = "PRIOR",
                ["PGDN"] = "NEXT",
                ["PGUP"] = "PRIOR",
                ["PRINTSCREEN"] = "SNAPSHOT",
                ["PRTSC"] = "SNAPSHOT",
                ["SCROLLLOCK"] = "SCROLL",
                ["SPACEBAR"] = "SPACE",
                ["WIN"] = "LWIN",
                ["WINDOWS"] = "LWIN",
                ["+"] = "OEM_PLUS",
                [","] = "OEM_COMMA",
                ["-"] = "OEM_MINUS",
                ["."] = "OEM_PERIOD",
                ["/"] = "OEM_2",
                [";"] = "OEM_1",
                ["["] = "OEM_4",
                ["\\"] = "OEM_5",
                ["]"] = "OEM_6",
                ["'"] = "OEM_7"
            };

        private static readonly IReadOnlyDictionary<string, MouseInput> MouseAliases =
            new Dictionary<string, MouseInput>(StringComparer.OrdinalIgnoreCase)
            {
                ["LMB"] = MouseInput.Left,
                ["LBUTTON"] = MouseInput.Left,
                ["MOUSE1"] = MouseInput.Left,
                ["LEFTBUTTON"] = MouseInput.Left,
                ["LEFTMOUSE"] = MouseInput.Left,
                ["LEFTMOUSEBUTTON"] = MouseInput.Left,
                ["RMB"] = MouseInput.Right,
                ["RBUTTON"] = MouseInput.Right,
                ["MOUSE2"] = MouseInput.Right,
                ["RIGHTBUTTON"] = MouseInput.Right,
                ["RIGHTMOUSE"] = MouseInput.Right,
                ["RIGHTMOUSEBUTTON"] = MouseInput.Right,
                ["MMB"] = MouseInput.Middle,
                ["MBUTTON"] = MouseInput.Middle,
                ["MOUSE3"] = MouseInput.Middle,
                ["MIDDLEBUTTON"] = MouseInput.Middle,
                ["MIDDLEMOUSE"] = MouseInput.Middle,
                ["MIDDLEMOUSEBUTTON"] = MouseInput.Middle,
                ["MB4"] = MouseInput.XButton1,
                ["MOUSE4"] = MouseInput.XButton1,
                ["XBUTTON1"] = MouseInput.XButton1,
                ["X1"] = MouseInput.XButton1,
                ["MB5"] = MouseInput.XButton2,
                ["MOUSE5"] = MouseInput.XButton2,
                ["XBUTTON2"] = MouseInput.XButton2,
                ["X2"] = MouseInput.XButton2,
                ["WHEELUP"] = MouseInput.WheelUp,
                ["WHEELDOWN"] = MouseInput.WheelDown,
                ["WHEELLEFT"] = MouseInput.WheelLeft,
                ["WHEELRIGHT"] = MouseInput.WheelRight
            };

        /// <summary>
        /// Executes a macro serially. Inputs prefixed with (HOLD) remain down while later
        /// inputs run and are always released, in reverse order, when the macro ends.
        /// </summary>
        public static async Task<bool> Press(Macro macro)
        {
            if (macro is null || macro.msToWait < 0 || macro.keycodes is null || macro.keycodes.Count == 0)
                return false;

            List<ParsedInput> inputs = new List<ParsedInput>(macro.keycodes.Count);
            foreach (string keycode in macro.keycodes)
            {
                if (!TryParseInput(keycode, out ParsedInput input))
                {
                    await typeof(Simulate).Log($"Unsupported input code: '{keycode}'.", error: true);
                    return false;
                }

                inputs.Add(input);
            }

            await MacroLock.WaitAsync();
            List<ParsedInput> heldInputs = new List<ParsedInput>();
            try
            {
                foreach (ParsedInput input in inputs)
                {
                    if (input.Mouse is MouseInput.WheelUp or MouseInput.WheelDown or MouseInput.WheelLeft or MouseInput.WheelRight)
                    {
                        SendMouseWheel(input.Mouse.Value);
                    }
                    else if (input.Hold)
                    {
                        SendDown(input);
                        heldInputs.Add(input);
                    }
                    else
                    {
                        SendDown(input);
                        await Task.Delay(Variables.KeyPressDurationMs);
                        SendUp(input);
                    }

                    if (macro.msToWait > 0)
                        await Task.Delay(macro.msToWait);
                }

                return true;
            }
            catch (Exception ex)
            {
                await typeof(Simulate).Log($"Failed to simulate macro: {ex.Message}", error: true);
                return false;
            }
            finally
            {
                for (int index = heldInputs.Count - 1; index >= 0; index--)
                {
                    try
                    {
                        SendUp(heldInputs[index]);
                    }
                    catch (Exception ex)
                    {
                        await typeof(Simulate).Log($"Failed to release held input '{heldInputs[index].DisplayName}': {ex.Message}", error: true);
                    }
                }

                MacroLock.Release();
            }
        }

        internal static bool TryParseInput(string? keycode, out ParsedInput input)
        {
            input = default;
            if (string.IsNullOrWhiteSpace(keycode))
                return false;

            string token = keycode.Trim();
            bool hold = false;
            const string holdPrefix = "(HOLD)";
            if (token.StartsWith(holdPrefix, StringComparison.OrdinalIgnoreCase))
            {
                hold = true;
                token = token[holdPrefix.Length..].Trim();
            }

            if (token.Length >= 2 && token[0] == '{' && token[^1] == '}')
                token = token[1..^1].Trim();

            string compactMouseToken = token.Replace(" ", string.Empty).Replace("_", string.Empty);
            if (MouseAliases.TryGetValue(compactMouseToken, out MouseInput mouse))
            {
                if (hold && mouse is MouseInput.WheelUp or MouseInput.WheelDown or MouseInput.WheelLeft or MouseInput.WheelRight)
                    return false;

                input = new ParsedInput(null, mouse, hold, token.ToUpperInvariant());
                return true;
            }

            if (KeyAliases.TryGetValue(token, out string? alias))
                token = alias;
            else if (token.Length == 1 && char.IsLetterOrDigit(token[0]))
                token = "VK_" + token.ToUpperInvariant();

            if (!Enum.TryParse(token, ignoreCase: true, out VirtualKeyCode virtualKey)
                || !Enum.IsDefined(typeof(VirtualKeyCode), virtualKey))
                return false;

            input = new ParsedInput(virtualKey, null, hold, token.ToUpperInvariant());
            return true;
        }

        private static void SendDown(ParsedInput input)
        {
            if (input.Key is VirtualKeyCode key)
                Simulator.Keyboard.KeyDown(key);
            else if (input.Mouse is MouseInput mouse)
                SendMouseButton(mouse, down: true);
        }

        private static void SendUp(ParsedInput input)
        {
            if (input.Key is VirtualKeyCode key)
                Simulator.Keyboard.KeyUp(key);
            else if (input.Mouse is MouseInput mouse)
                SendMouseButton(mouse, down: false);
        }

        private static void SendMouseButton(MouseInput mouse, bool down)
        {
            (uint flags, uint data) = mouse switch
            {
                MouseInput.Left => (down ? MouseLeftDown : MouseLeftUp, 0u),
                MouseInput.Right => (down ? MouseRightDown : MouseRightUp, 0u),
                MouseInput.Middle => (down ? MouseMiddleDown : MouseMiddleUp, 0u),
                MouseInput.XButton1 => (down ? MouseXDown : MouseXUp, XButton1),
                MouseInput.XButton2 => (down ? MouseXDown : MouseXUp, XButton2),
                _ => throw new ArgumentOutOfRangeException(nameof(mouse), mouse, "This mouse input is not a button.")
            };

            mouse_event(flags, 0, 0, data, UIntPtr.Zero);
        }

        private static void SendMouseWheel(MouseInput mouse)
        {
            (uint flags, int delta) = mouse switch
            {
                MouseInput.WheelUp => (MouseWheel, WheelDelta),
                MouseInput.WheelDown => (MouseWheel, -WheelDelta),
                MouseInput.WheelLeft => (MouseHorizontalWheel, -WheelDelta),
                MouseInput.WheelRight => (MouseHorizontalWheel, WheelDelta),
                _ => throw new ArgumentOutOfRangeException(nameof(mouse), mouse, "This mouse input is not a wheel action.")
            };

            mouse_event(flags, 0, 0, unchecked((uint)delta), UIntPtr.Zero);
        }

        [DllImport("user32.dll")]
        private static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, UIntPtr dwExtraInfo);

        internal readonly record struct ParsedInput(
            VirtualKeyCode? Key,
            MouseInput? Mouse,
            bool Hold,
            string DisplayName);

        internal enum MouseInput
        {
            Left, Right, Middle, XButton1, XButton2,
            WheelUp, WheelDown, WheelLeft, WheelRight
        }
    }
}
