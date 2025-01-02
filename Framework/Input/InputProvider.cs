using System.Numerics;

namespace Foster.Framework;

/// <summary>
/// The Input Provider sends data to an Input Module.
/// Every frame you should call <see cref="Update"/> to step the Input State.
/// </summary>
public abstract class InputProvider
{
	/// <summary>
	/// Our Input Module
	/// </summary>
	public readonly Input Input;

	public InputProvider()
	{
		Input = new(this);
	}

	/// <summary>
	/// What to do when the user tries to set the clipboard
	/// </summary>
	public abstract void SetClipboard(string text);

	/// <summary>
	/// What to do when the user tries to get the clipboard
	/// </summary>
	public abstract string GetClipboard();

	/// <summary>
	/// Rumbles a specific controller
	/// </summary>
	public abstract void Rumble(ControllerID id, float lowIntensity, float highIntensity, float duration);

	/// <summary>
	/// Run at the beginning of a frame to increment the input state.
	/// </summary>
	public virtual void Update(in Time time)
		=> Input.Step(time);

	public void Text(ReadOnlySpan<char> text)
		=> Input.OnText(text);

	internal unsafe void Text(nint cstr)
	{
		byte* ptr = (byte*)cstr;
		if (ptr == null || ptr[0] == 0)
			return;

		// get cstr length
		int len = 0;
		while (ptr[len] != 0)
			len++;

		// convert to chars
		char* chars = stackalloc char[64];
		int written = System.Text.Encoding.UTF8.GetChars(ptr, len, chars, 64);

		// append chars
		Text(new ReadOnlySpan<char>(chars, written));
	}

	public void Key(int key, bool pressed, in TimeSpan time)
		=> Input.NextState.Keyboard.OnKey(key, pressed, time);

	public void MouseButton(int button, bool pressed, in TimeSpan time)
		=> Input.NextState.Mouse.OnButton(button, pressed, time);

	public void MouseMove(Vector2 position, Vector2 delta)
	{
		Input.NextState.Mouse.Position = position;
		Input.NextState.Mouse.Delta = delta;
	}

	public void MouseWheel(Vector2 wheel)
		=> Input.NextState.Mouse.OnWheel(wheel);

	public void ConnectController(
		ControllerID id,
		string name,
		int buttonCount,
		int axisCount,
		bool isGamepad,
		GamepadTypes type,
		ushort vendor,
		ushort product,
		ushort version)
		=> Input.ConnectController(id, name, buttonCount, axisCount, isGamepad, type, vendor, product, version);

	public void DisconnectController(ControllerID id)
		=> Input.DisconnectController(id);

	public void ControllerButton(ControllerID id, int button, bool pressed, in TimeSpan time)
		=> Input.NextState.GetController(id)?.OnButton(button, pressed, time);

	public void ControllerAxis(ControllerID id, int axis, float value, in TimeSpan time)
		=> Input.NextState.GetController(id)?.OnAxis(axis, value, time);
}