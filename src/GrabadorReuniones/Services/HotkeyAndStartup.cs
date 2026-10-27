using System.Text.RegularExpressions;

namespace GrabadorReuniones;

internal readonly record struct HotkeySpec(bool Control, bool Shift, bool Alt, Keys Key)
{
    public static HotkeySpec Default { get; } = new(true, true, false, Keys.R);

    public uint Modifiers
    {
        get
        {
            uint m = 0x4000; // MOD_NOREPEAT
            if (Alt) m |= 0x0001;
            if (Control) m |= 0x0002;
            if (Shift) m |= 0x0004;
            return m;
        }
    }

    public uint VirtualKey => (uint)(Key & Keys.KeyCode);

    public override string ToString()
    {
        var parts = new List<string>();
        if (Control) parts.Add("Ctrl");
        if (Shift) parts.Add("Shift");
        if (Alt) parts.Add("Alt");
        parts.Add(FormatKey(Key));
        return string.Join("+", parts);
    }

    public static bool TryParse(string? text, out HotkeySpec spec)
    {
        spec = Default;
        if (string.IsNullOrWhiteSpace(text))
            return false;

        var tokens = text.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length == 0)
            return false;

        var control = false;
        var shift = false;
        var alt = false;
        Keys? key = null;
        foreach (var raw in tokens)
        {
            var t = raw.Trim();
            if (t.Equals("Ctrl", StringComparison.OrdinalIgnoreCase) ||
                t.Equals("Control", StringComparison.OrdinalIgnoreCase))
            {
                control = true;
                continue;
            }

            if (t.Equals("Shift", StringComparison.OrdinalIgnoreCase))
            {
                shift = true;
                continue;
            }

            if (t.Equals("Alt", StringComparison.OrdinalIgnoreCase))
            {
                alt = true;
                continue;
            }

            if (t.Equals("Win", StringComparison.OrdinalIgnoreCase) ||
                t.Equals("Windows", StringComparison.OrdinalIgnoreCase))
                continue;

            if (TryParseKey(t, out var parsed))
                key = parsed;
        }

        if (key is null)
            return false;

        spec = new HotkeySpec(control, shift, alt, key.Value);
        return control || shift || alt;
    }

    public static IReadOnlyList<Keys> SelectableKeys()
    {
        var keys = new List<Keys>();
        for (var c = 'A'; c <= 'Z'; c++)
            keys.Add((Keys)c);
        for (var d = 0; d <= 9; d++)
            keys.Add(Keys.D0 + d);
        for (var f = 1; f <= 12; f++)
            keys.Add(Keys.F1 + (f - 1));
        return keys;
    }

    public static string FormatKey(Keys key)
    {
        if (key is >= Keys.D0 and <= Keys.D9)
            return ((char)('0' + (key - Keys.D0))).ToString();
        if (key is >= Keys.F1 and <= Keys.F24)
            return "F" + (key - Keys.F1 + 1);
        return key.ToString();
    }

    private static bool TryParseKey(string token, out Keys key)
    {
        key = Keys.None;
        if (token.Length == 1)
        {
            var c = char.ToUpperInvariant(token[0]);
            if (c is >= 'A' and <= 'Z')
            {
                key = (Keys)c;
                return true;
            }

            if (c is >= '0' and <= '9')
            {
                key = Keys.D0 + (c - '0');
                return true;
            }
        }

        if (Regex.IsMatch(token, @"^F([1-9]|1[0-2])$", RegexOptions.IgnoreCase) &&
            int.TryParse(token[1..], out var n))
        {
            key = Keys.F1 + (n - 1);
            return true;
        }

        return Enum.TryParse(token, ignoreCase: true, out key) && key != Keys.None;
    }
}

internal sealed class HotkeyService : IDisposable
{
    private const int HotkeyId = 0xA1C0;
    private readonly IntPtr _hwnd;
    private bool _registered;

    public HotkeyService(IntPtr hwnd) => _hwnd = hwnd;

    public bool TryRegister(HotkeySpec spec)
    {
        Unregister();
        if (_hwnd == IntPtr.Zero)
            return false;

        _registered = NativeMethods.RegisterHotKey(_hwnd, HotkeyId, spec.Modifiers, spec.VirtualKey);
        return _registered;
    }

    public void Unregister()
    {
        if (!_registered)
            return;
        NativeMethods.UnregisterHotKey(_hwnd, HotkeyId);
        _registered = false;
    }

    public void Dispose() => Unregister();

    private static class NativeMethods
    {
        [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
        public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
        public static extern bool UnregisterHotKey(IntPtr hWnd, int id);
    }
}

internal static class StartupService
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "GrabadorReunionesIA";

    public static void Apply(bool enabled)
    {
        using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(RunKey, writable: true)
                        ?? Microsoft.Win32.Registry.CurrentUser.CreateSubKey(RunKey);
        if (key is null)
            return;

        if (enabled)
        {
            var exe = Application.ExecutablePath;
            key.SetValue(ValueName, $"\"{exe}\"");
        }
        else if (key.GetValue(ValueName) is not null)
        {
            key.DeleteValue(ValueName, throwOnMissingValue: false);
        }
    }
}
