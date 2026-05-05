using System.Numerics;
using static SDL3.SDL;

namespace Foster.Framework;

internal sealed class InputProviderSDL(App app) : InputProvider
{
	public readonly App App = app;
	private Vector2 lastMouse;

	private readonly List<(uint ID, nint Ptr)> openJoysticks = [];
	private readonly List<(uint ID, nint Ptr)> openGamepads = [];

	public override string GetClipboard()
	{
		return SDL_GetClipboardText();
	}

	public override void SetClipboard(string text)
	{
		SDL_SetClipboardText(text);
	}

	public override void Rumble(ControllerID id, float lowIntensity, float highIntensity, float duration)
	{
		var highFrequency = (ushort)(Calc.Clamp(highIntensity, 0, 1) * 0xFFFF);
		var lowFrequency = (ushort)(Calc.Clamp(lowIntensity, 0, 1) * 0xFFFF);
		var durationms = (uint)TimeSpan.FromSeconds(duration).TotalMilliseconds;

		if (Input.GetController(id)?.IsGamepad ?? false)
		{
			var ptr = SDL_GetGamepadFromID(id.Value);
			if (ptr != nint.Zero)
				SDL_RumbleGamepad(ptr, lowFrequency, highFrequency, durationms);

		}
		else
		{
			var ptr = SDL_GetJoystickFromID(id.Value);
			if (ptr != nint.Zero)
				SDL_RumbleJoystick(ptr, lowFrequency, highFrequency, durationms);
		}
	}

	public override void Update(in Time time)
	{
		// get window properties
		var windowSize = new Point2(App.Window.Width, App.Window.Height);
		var windowSizeInPx = new Point2(App.Window.WidthInPixels, App.Window.HeightInPixels);
		var windowPos = new Point2();
		SDL_GetWindowPosition(App.Window.Handle, out windowPos.X, out windowPos.Y);

		// use global mouse position so we can get it as it moves outside the window
		var mouse = new Vector2();
		SDL_GetGlobalMouseState(out mouse.X, out mouse.Y);
		mouse -= windowPos;

		// scale it to the pixel coords
		mouse = mouse / windowSize * windowSizeInPx;
		var delta = mouse - lastMouse;

		// get mouse delta if we're in relative mouse mode
		if (SDL_GetWindowRelativeMouseMode(App.Window.Handle))
		{
			SDL_GetRelativeMouseState(out float dx, out float dy);
			delta = new Vector2(dx, dy) / windowSize * windowSizeInPx;
		}

		// add new event if moved
		if (lastMouse.X != mouse.X || lastMouse.Y != mouse.Y || delta.X != 0 || delta.Y != 0)
		{
			lastMouse = mouse;
			MouseMove(mouse, delta, time.Elapsed);
		}

		base.Update(time);
	}

	public unsafe void OnEvent(SDL_Event ev)
	{
		switch ((SDL_EventType)ev.type)
		{
		// mouse
		case SDL_EventType.SDL_EVENT_MOUSE_BUTTON_DOWN:
			MouseButton((int)GetMouseFromSDL(ev.button.button), true, App.Time.Elapsed);
			break;
		case SDL_EventType.SDL_EVENT_MOUSE_BUTTON_UP:
			MouseButton((int)GetMouseFromSDL(ev.button.button), false, App.Time.Elapsed);
			break;
		case SDL_EventType.SDL_EVENT_MOUSE_WHEEL:
			MouseWheel(new(ev.wheel.x, ev.wheel.y));
			break;

		// keyboard
		case SDL_EventType.SDL_EVENT_KEY_DOWN:
			if (!ev.key.repeat)
				Key((int)GetKeyFromSDL((SDL_Keycode)ev.key.key), true, App.Time.Elapsed);
			break;
		case SDL_EventType.SDL_EVENT_KEY_UP:
			if (!ev.key.repeat)
				Key((int)GetKeyFromSDL((SDL_Keycode)ev.key.key), false, App.Time.Elapsed);
			break;

		case SDL_EventType.SDL_EVENT_TEXT_INPUT:
			Text(new nint(ev.text.text), App.Window);
			break;

		// joystick
		case SDL_EventType.SDL_EVENT_JOYSTICK_ADDED:
			{
				var id = ev.jdevice.which;
				if (SDL_IsGamepad(id))
					break;

				var ptr = SDL_OpenJoystick(id);
				openJoysticks.Add((id, ptr));

				ConnectController(
					id: new(id),
					name: SDL_GetJoystickName(ptr),
					buttonCount: SDL_GetNumJoystickButtons(ptr),
					axisCount: SDL_GetNumJoystickAxes(ptr),
					isGamepad: false,
					type: GamepadTypes.Unknown,
					vendor: SDL_GetJoystickVendor(ptr),
					product: SDL_GetJoystickProduct(ptr),
					version: SDL_GetJoystickProductVersion(ptr)
				);
				break;
			}
		case SDL_EventType.SDL_EVENT_JOYSTICK_REMOVED:
			{
				var id = ev.jdevice.which;
				if (SDL_IsGamepad(id))
					break;

				for (int i = 0; i < openJoysticks.Count; i ++)
					if (openJoysticks[i].ID == id)
					{
						SDL_CloseJoystick(openJoysticks[i].Ptr);
						openJoysticks.RemoveAt(i);
					}

				DisconnectController(new(id));
				break;
			}
		case SDL_EventType.SDL_EVENT_JOYSTICK_BUTTON_DOWN:
		case SDL_EventType.SDL_EVENT_JOYSTICK_BUTTON_UP:
			{
				var id = ev.jbutton.which;
				if (SDL_IsGamepad(id))
					break;

				ControllerButton(
					id: new(id),
					button: ev.jbutton.button,
					pressed: ev.type == (uint)SDL_EventType.SDL_EVENT_JOYSTICK_BUTTON_DOWN,
					time: App.Time.Elapsed);

				break;
			}
		case SDL_EventType.SDL_EVENT_JOYSTICK_AXIS_MOTION:
			{
				var id = ev.jaxis.which;
				if (SDL_IsGamepad(id))
					break;

				float value = ev.jaxis.value >= 0
					? ev.jaxis.value / 32767.0f
					: ev.jaxis.value / 32768.0f;

				ControllerAxis(
					id: new(id),
					axis: ev.jaxis.axis,
					value: value,
					time: App.Time.Elapsed);

				break;
			}

		// gamepad
		case SDL_EventType.SDL_EVENT_GAMEPAD_ADDED:
			{
				var id = ev.gdevice.which;
				var ptr = SDL_OpenGamepad(id);
				openGamepads.Add((id, ptr));

				ConnectController(
					id: new(id),
					name: SDL_GetGamepadName(ptr),
					buttonCount: 15,
					axisCount: 6,
					isGamepad: true,
					type: (GamepadTypes)SDL_GetGamepadType(ptr),
					vendor: SDL_GetGamepadVendor(ptr),
					product: SDL_GetGamepadProduct(ptr),
					version: SDL_GetGamepadProductVersion(ptr)
				);
				break;
			}
		case SDL_EventType.SDL_EVENT_GAMEPAD_REMOVED:
			{
				var id = ev.gdevice.which;
				for (int i = 0; i < openGamepads.Count; i ++)
					if (openGamepads[i].ID == id)
					{
						SDL_CloseGamepad(openGamepads[i].Ptr);
						openGamepads.RemoveAt(i);
					}

				DisconnectController(new(id));
				break;
			}
		case SDL_EventType.SDL_EVENT_GAMEPAD_BUTTON_DOWN:
		case SDL_EventType.SDL_EVENT_GAMEPAD_BUTTON_UP:
			{
				var id = ev.gbutton.which;
				ControllerButton(
					id: new(id),
					button: (int)GetButtonFromSDL((SDL_GamepadButton)ev.gbutton.button),
					pressed: ev.type == (uint)SDL_EventType.SDL_EVENT_GAMEPAD_BUTTON_DOWN,
					time: App.Time.Elapsed);

				break;
			}
		case SDL_EventType.SDL_EVENT_GAMEPAD_AXIS_MOTION:
			{
				var id = ev.gbutton.which;
				float value = ev.gaxis.value >= 0
					? ev.gaxis.value / 32767.0f
					: ev.gaxis.value / 32768.0f;

				ControllerAxis(
					id: new(id),
					axis: (int)GetAxisFromSDL((SDL_GamepadAxis)ev.gaxis.axis),
					value: value,
					time: App.Time.Elapsed);

				break;
			}
		}
	}

	public void CloseDevices()
    {
		foreach (var it in openJoysticks)
			SDL_CloseJoystick(it.Ptr);
		foreach (var it in openGamepads)
			SDL_CloseGamepad(it.Ptr);
		openJoysticks.Clear();
		openGamepads.Clear();
	}

	private static Buttons GetButtonFromSDL(SDL_GamepadButton button) => button switch
	{
		SDL_GamepadButton.SDL_GAMEPAD_BUTTON_INVALID => Buttons.None,
		SDL_GamepadButton.SDL_GAMEPAD_BUTTON_SOUTH => Buttons.South,
		SDL_GamepadButton.SDL_GAMEPAD_BUTTON_EAST => Buttons.East,
		SDL_GamepadButton.SDL_GAMEPAD_BUTTON_WEST => Buttons.West,
		SDL_GamepadButton.SDL_GAMEPAD_BUTTON_NORTH => Buttons.North,
		SDL_GamepadButton.SDL_GAMEPAD_BUTTON_BACK => Buttons.Back,
		SDL_GamepadButton.SDL_GAMEPAD_BUTTON_GUIDE => Buttons.Guide,
		SDL_GamepadButton.SDL_GAMEPAD_BUTTON_START => Buttons.Start,
		SDL_GamepadButton.SDL_GAMEPAD_BUTTON_LEFT_STICK => Buttons.LeftStick,
		SDL_GamepadButton.SDL_GAMEPAD_BUTTON_RIGHT_STICK => Buttons.RightStick,
		SDL_GamepadButton.SDL_GAMEPAD_BUTTON_LEFT_SHOULDER => Buttons.LeftShoulder,
		SDL_GamepadButton.SDL_GAMEPAD_BUTTON_RIGHT_SHOULDER => Buttons.RightShoulder,
		SDL_GamepadButton.SDL_GAMEPAD_BUTTON_DPAD_UP => Buttons.Up,
		SDL_GamepadButton.SDL_GAMEPAD_BUTTON_DPAD_DOWN => Buttons.Down,
		SDL_GamepadButton.SDL_GAMEPAD_BUTTON_DPAD_LEFT => Buttons.Left,
		SDL_GamepadButton.SDL_GAMEPAD_BUTTON_DPAD_RIGHT => Buttons.Right,
		_ => Buttons.None,
	};

	private static MouseButtons GetMouseFromSDL(int button) => button switch
	{
		1 => MouseButtons.Left,
		2 => MouseButtons.Middle,
		3 => MouseButtons.Right,
		_ => MouseButtons.None,
	};

	private static Axes GetAxisFromSDL(SDL_GamepadAxis axis) => axis switch
	{
		SDL_GamepadAxis.SDL_GAMEPAD_AXIS_INVALID => Axes.None,
		SDL_GamepadAxis.SDL_GAMEPAD_AXIS_LEFTX => Axes.LeftX,
		SDL_GamepadAxis.SDL_GAMEPAD_AXIS_LEFTY => Axes.LeftY,
		SDL_GamepadAxis.SDL_GAMEPAD_AXIS_RIGHTX => Axes.RightX,
		SDL_GamepadAxis.SDL_GAMEPAD_AXIS_RIGHTY => Axes.RightY,
		SDL_GamepadAxis.SDL_GAMEPAD_AXIS_LEFT_TRIGGER => Axes.LeftTrigger,
		SDL_GamepadAxis.SDL_GAMEPAD_AXIS_RIGHT_TRIGGER => Axes.RightTrigger,
		_ => Axes.None,
	};

	private static Keys GetKeyFromSDL(SDL_Keycode keycode) => keycode switch
	{
		SDL_Keycode.SDLK_UNKNOWN => Keys.Unknown,
		SDL_Keycode.SDLK_A => Keys.A,
		SDL_Keycode.SDLK_B => Keys.B,
		SDL_Keycode.SDLK_C => Keys.C,
		SDL_Keycode.SDLK_D => Keys.D,
		SDL_Keycode.SDLK_E => Keys.E,
		SDL_Keycode.SDLK_F => Keys.F,
		SDL_Keycode.SDLK_G => Keys.G,
		SDL_Keycode.SDLK_H => Keys.H,
		SDL_Keycode.SDLK_I => Keys.I,
		SDL_Keycode.SDLK_J => Keys.J,
		SDL_Keycode.SDLK_K => Keys.K,
		SDL_Keycode.SDLK_L => Keys.L,
		SDL_Keycode.SDLK_M => Keys.M,
		SDL_Keycode.SDLK_N => Keys.N,
		SDL_Keycode.SDLK_O => Keys.O,
		SDL_Keycode.SDLK_P => Keys.P,
		SDL_Keycode.SDLK_Q => Keys.Q,
		SDL_Keycode.SDLK_R => Keys.R,
		SDL_Keycode.SDLK_S => Keys.S,
		SDL_Keycode.SDLK_T => Keys.T,
		SDL_Keycode.SDLK_U => Keys.U,
		SDL_Keycode.SDLK_V => Keys.V,
		SDL_Keycode.SDLK_W => Keys.W,
		SDL_Keycode.SDLK_X => Keys.X,
		SDL_Keycode.SDLK_Y => Keys.Y,
		SDL_Keycode.SDLK_Z => Keys.Z,
		SDL_Keycode.SDLK_1 => Keys.D1,
		SDL_Keycode.SDLK_2 => Keys.D2,
		SDL_Keycode.SDLK_3 => Keys.D3,
		SDL_Keycode.SDLK_4 => Keys.D4,
		SDL_Keycode.SDLK_5 => Keys.D5,
		SDL_Keycode.SDLK_6 => Keys.D6,
		SDL_Keycode.SDLK_7 => Keys.D7,
		SDL_Keycode.SDLK_8 => Keys.D8,
		SDL_Keycode.SDLK_9 => Keys.D9,
		SDL_Keycode.SDLK_0 => Keys.D0,
		SDL_Keycode.SDLK_RETURN => Keys.Enter,
		SDL_Keycode.SDLK_ESCAPE => Keys.Escape,
		SDL_Keycode.SDLK_BACKSPACE => Keys.Backspace,
		SDL_Keycode.SDLK_TAB => Keys.Tab,
		SDL_Keycode.SDLK_SPACE => Keys.Space,
		SDL_Keycode.SDLK_MINUS => Keys.Minus,
		SDL_Keycode.SDLK_EQUALS => Keys.Equals,
		SDL_Keycode.SDLK_LEFTBRACKET => Keys.LeftBracket,
		SDL_Keycode.SDLK_RIGHTBRACKET => Keys.RightBracket,
		SDL_Keycode.SDLK_BACKSLASH => Keys.Backslash,
		SDL_Keycode.SDLK_SEMICOLON => Keys.Semicolon,
		SDL_Keycode.SDLK_APOSTROPHE => Keys.Apostrophe,
		SDL_Keycode.SDLK_GRAVE => Keys.Tilde,
		SDL_Keycode.SDLK_COMMA => Keys.Comma,
		SDL_Keycode.SDLK_PERIOD => Keys.Period,
		SDL_Keycode.SDLK_SLASH => Keys.Slash,
		SDL_Keycode.SDLK_CAPSLOCK => Keys.Capslock,
		SDL_Keycode.SDLK_F1 => Keys.F1,
		SDL_Keycode.SDLK_F2 => Keys.F2,
		SDL_Keycode.SDLK_F3 => Keys.F3,
		SDL_Keycode.SDLK_F4 => Keys.F4,
		SDL_Keycode.SDLK_F5 => Keys.F5,
		SDL_Keycode.SDLK_F6 => Keys.F6,
		SDL_Keycode.SDLK_F7 => Keys.F7,
		SDL_Keycode.SDLK_F8 => Keys.F8,
		SDL_Keycode.SDLK_F9 => Keys.F9,
		SDL_Keycode.SDLK_F10 => Keys.F10,
		SDL_Keycode.SDLK_F11 => Keys.F11,
		SDL_Keycode.SDLK_F12 => Keys.F12,
		SDL_Keycode.SDLK_PRINTSCREEN => Keys.PrintScreen,
		SDL_Keycode.SDLK_SCROLLLOCK => Keys.ScrollLock,
		SDL_Keycode.SDLK_PAUSE => Keys.Pause,
		SDL_Keycode.SDLK_INSERT => Keys.Insert,
		SDL_Keycode.SDLK_HOME => Keys.Home,
		SDL_Keycode.SDLK_PAGEUP => Keys.PageUp,
		SDL_Keycode.SDLK_DELETE => Keys.Delete,
		SDL_Keycode.SDLK_END => Keys.End,
		SDL_Keycode.SDLK_PAGEDOWN => Keys.PageDown,
		SDL_Keycode.SDLK_RIGHT => Keys.Right,
		SDL_Keycode.SDLK_LEFT => Keys.Left,
		SDL_Keycode.SDLK_DOWN => Keys.Down,
		SDL_Keycode.SDLK_UP => Keys.Up,
		SDL_Keycode.SDLK_KP_DIVIDE => Keys.KeypadDivide,
		SDL_Keycode.SDLK_KP_MULTIPLY => Keys.KeypadMultiply,
		SDL_Keycode.SDLK_KP_MINUS => Keys.KeypadMinus,
		SDL_Keycode.SDLK_KP_PLUS => Keys.KeypadPlus,
		SDL_Keycode.SDLK_KP_ENTER => Keys.KeypadEnter,
		SDL_Keycode.SDLK_KP_1 => Keys.Keypad1,
		SDL_Keycode.SDLK_KP_2 => Keys.Keypad2,
		SDL_Keycode.SDLK_KP_3 => Keys.Keypad3,
		SDL_Keycode.SDLK_KP_4 => Keys.Keypad4,
		SDL_Keycode.SDLK_KP_5 => Keys.Keypad5,
		SDL_Keycode.SDLK_KP_6 => Keys.Keypad6,
		SDL_Keycode.SDLK_KP_7 => Keys.Keypad7,
		SDL_Keycode.SDLK_KP_8 => Keys.Keypad8,
		SDL_Keycode.SDLK_KP_9 => Keys.Keypad9,
		SDL_Keycode.SDLK_KP_0 => Keys.Keypad0,
		SDL_Keycode.SDLK_APPLICATION => Keys.Application,
		SDL_Keycode.SDLK_KP_EQUALS => Keys.KeypadEquals,
		SDL_Keycode.SDLK_F13 => Keys.F13,
		SDL_Keycode.SDLK_F14 => Keys.F14,
		SDL_Keycode.SDLK_F15 => Keys.F15,
		SDL_Keycode.SDLK_F16 => Keys.F16,
		SDL_Keycode.SDLK_F17 => Keys.F17,
		SDL_Keycode.SDLK_F18 => Keys.F18,
		SDL_Keycode.SDLK_F19 => Keys.F19,
		SDL_Keycode.SDLK_F20 => Keys.F20,
		SDL_Keycode.SDLK_F21 => Keys.F21,
		SDL_Keycode.SDLK_F22 => Keys.F22,
		SDL_Keycode.SDLK_F23 => Keys.F23,
		SDL_Keycode.SDLK_F24 => Keys.F24,
		SDL_Keycode.SDLK_EXECUTE => Keys.Execute,
		SDL_Keycode.SDLK_HELP => Keys.Help,
		SDL_Keycode.SDLK_MENU => Keys.Menu,
		SDL_Keycode.SDLK_SELECT => Keys.Select,
		SDL_Keycode.SDLK_STOP => Keys.Stop,
		SDL_Keycode.SDLK_UNDO => Keys.Undo,
		SDL_Keycode.SDLK_CUT => Keys.Cut,
		SDL_Keycode.SDLK_COPY => Keys.Copy,
		SDL_Keycode.SDLK_PASTE => Keys.Paste,
		SDL_Keycode.SDLK_FIND => Keys.Find,
		SDL_Keycode.SDLK_MUTE => Keys.Mute,
		SDL_Keycode.SDLK_VOLUMEUP => Keys.VolumeUp,
		SDL_Keycode.SDLK_VOLUMEDOWN => Keys.VolumeDown,
		SDL_Keycode.SDLK_KP_COMMA => Keys.KeypadComma,
		SDL_Keycode.SDLK_ALTERASE => Keys.AltErase,
		SDL_Keycode.SDLK_SYSREQ => Keys.SysReq,
		SDL_Keycode.SDLK_CANCEL => Keys.Cancel,
		SDL_Keycode.SDLK_CLEAR => Keys.Clear,
		SDL_Keycode.SDLK_PRIOR => Keys.Prior,
		SDL_Keycode.SDLK_RETURN2 => Keys.Enter2,
		SDL_Keycode.SDLK_SEPARATOR => Keys.Separator,
		SDL_Keycode.SDLK_OUT => Keys.Out,
		SDL_Keycode.SDLK_OPER => Keys.Oper,
		SDL_Keycode.SDLK_CLEARAGAIN => Keys.ClearAgain,
		SDL_Keycode.SDLK_KP_00 => Keys.Keypad00,
		SDL_Keycode.SDLK_KP_000 => Keys.Keypad000,
		SDL_Keycode.SDLK_KP_LEFTPAREN => Keys.KeypadLeftParen,
		SDL_Keycode.SDLK_KP_RIGHTPAREN => Keys.KeypadRightParen,
		SDL_Keycode.SDLK_KP_LEFTBRACE => Keys.KeypadLeftBrace,
		SDL_Keycode.SDLK_KP_RIGHTBRACE => Keys.KeypadRightBrace,
		SDL_Keycode.SDLK_KP_TAB => Keys.KeypadTab,
		SDL_Keycode.SDLK_KP_BACKSPACE => Keys.KeypadBackspace,
		SDL_Keycode.SDLK_KP_A => Keys.KeypadA,
		SDL_Keycode.SDLK_KP_B => Keys.KeypadB,
		SDL_Keycode.SDLK_KP_C => Keys.KeypadC,
		SDL_Keycode.SDLK_KP_D => Keys.KeypadD,
		SDL_Keycode.SDLK_KP_E => Keys.KeypadE,
		SDL_Keycode.SDLK_KP_F => Keys.KeypadF,
		SDL_Keycode.SDLK_KP_XOR => Keys.KeypadXor,
		SDL_Keycode.SDLK_KP_POWER => Keys.KeypadPower,
		SDL_Keycode.SDLK_KP_PERCENT => Keys.KeypadPercent,
		SDL_Keycode.SDLK_KP_LESS => Keys.KeypadLess,
		SDL_Keycode.SDLK_KP_GREATER => Keys.KeypadGreater,
		SDL_Keycode.SDLK_KP_AMPERSAND => Keys.KeypadAmpersand,
		SDL_Keycode.SDLK_KP_COLON => Keys.KeypadColon,
		SDL_Keycode.SDLK_KP_HASH => Keys.KeypadHash,
		SDL_Keycode.SDLK_KP_SPACE => Keys.KeypadSpace,
		SDL_Keycode.SDLK_KP_CLEAR => Keys.KeypadClear,
		SDL_Keycode.SDLK_LCTRL => Keys.LeftControl,
		SDL_Keycode.SDLK_LSHIFT => Keys.LeftShift,
		SDL_Keycode.SDLK_LALT => Keys.LeftAlt,
		SDL_Keycode.SDLK_LGUI => Keys.LeftOS,
		SDL_Keycode.SDLK_RCTRL => Keys.RightControl,
		SDL_Keycode.SDLK_RSHIFT => Keys.RightShift,
		SDL_Keycode.SDLK_RALT => Keys.RightAlt,
		SDL_Keycode.SDLK_RGUI => Keys.RightOS,
		_ => Keys.Unknown,
	};
}
