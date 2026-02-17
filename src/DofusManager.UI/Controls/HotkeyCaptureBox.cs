using System.Text;
using System.Windows;
using System.Windows.Input;
using System.Windows.Controls;

namespace DofusManager.UI.Controls;

/// <summary>
/// TextBox custom qui capture un raccourci clavier (modifiers + touche).
/// En lecture seule : l'utilisateur clique dessus, appuie sur une combinaison, et le raccourci est capturé.
/// </summary>
public class HotkeyCaptureBox : TextBox
{
    public static readonly DependencyProperty HotkeyModifiersProperty =
        DependencyProperty.Register(nameof(HotkeyModifiers), typeof(uint), typeof(HotkeyCaptureBox),
            new FrameworkPropertyMetadata(0u, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnHotkeyChanged));

    public static readonly DependencyProperty VirtualKeyCodeProperty =
        DependencyProperty.Register(nameof(VirtualKeyCode), typeof(uint), typeof(HotkeyCaptureBox),
            new FrameworkPropertyMetadata(0u, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnHotkeyChanged));

    public static readonly DependencyProperty HotkeyDisplayProperty =
        DependencyProperty.Register(nameof(HotkeyDisplay), typeof(string), typeof(HotkeyCaptureBox),
            new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    /// <summary>
    /// En mode SingleKey, les touches modificatrices (Alt, Ctrl, Shift) sont capturées
    /// comme touche principale (sans préfixe modifier). Utile pour la touche broadcast.
    /// </summary>
    public static readonly DependencyProperty SingleKeyModeProperty =
        DependencyProperty.Register(nameof(SingleKeyMode), typeof(bool), typeof(HotkeyCaptureBox),
            new PropertyMetadata(false));

    public uint HotkeyModifiers
    {
        get => (uint)GetValue(HotkeyModifiersProperty);
        set => SetValue(HotkeyModifiersProperty, value);
    }

    public uint VirtualKeyCode
    {
        get => (uint)GetValue(VirtualKeyCodeProperty);
        set => SetValue(VirtualKeyCodeProperty, value);
    }

    public string HotkeyDisplay
    {
        get => (string)GetValue(HotkeyDisplayProperty);
        set => SetValue(HotkeyDisplayProperty, value);
    }

    public bool SingleKeyMode
    {
        get => (bool)GetValue(SingleKeyModeProperty);
        set => SetValue(SingleKeyModeProperty, value);
    }

    public HotkeyCaptureBox()
    {
        IsReadOnly = true;
        IsReadOnlyCaretVisible = false;
        Focusable = true;
        Cursor = Cursors.Hand;
    }

    private static void OnHotkeyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is HotkeyCaptureBox box)
        {
            box.UpdateDisplayFromProperties();
        }
    }

    private void UpdateDisplayFromProperties()
    {
        if (VirtualKeyCode == 0)
        {
            Text = string.Empty;
            HotkeyDisplay = string.Empty;
            return;
        }

        var display = FormatHotkey((Core.Models.HotkeyModifiers)HotkeyModifiers, VirtualKeyCode);
        Text = display;
        HotkeyDisplay = display;
    }

    protected override void OnGotKeyboardFocus(KeyboardFocusChangedEventArgs e)
    {
        base.OnGotKeyboardFocus(e);
        Text = "Appuyez sur une touche...";
    }

    protected override void OnLostKeyboardFocus(KeyboardFocusChangedEventArgs e)
    {
        base.OnLostKeyboardFocus(e);
        UpdateDisplayFromProperties();
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        e.Handled = true;

        var key = e.Key == Key.System ? e.SystemKey : e.Key;

        // Escape annule la capture
        if (key == Key.Escape)
        {
            UpdateDisplayFromProperties();
            Keyboard.ClearFocus();
            return;
        }

        var isModifierKey = key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt
            or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin;

        if (SingleKeyMode)
        {
            // En mode single-key : capturer n'importe quelle touche (y compris modifiers)
            // sans préfixe modifier. Mapper L/R vers les VK génériques.
            var vk = NormalizeVirtualKey(key);

            HotkeyModifiers = 0;
            VirtualKeyCode = vk;

            var display = GetKeyName(vk);
            HotkeyDisplay = display;
            Text = display;

            Keyboard.ClearFocus();
            return;
        }

        // Mode normal : ignorer les modifiers seuls
        if (isModifierKey)
            return;

        // Calculer les modifiers
        var modifiers = Core.Models.HotkeyModifiers.None;
        if ((Keyboard.Modifiers & ModifierKeys.Control) != 0)
            modifiers |= Core.Models.HotkeyModifiers.Control;
        if ((Keyboard.Modifiers & ModifierKeys.Shift) != 0)
            modifiers |= Core.Models.HotkeyModifiers.Shift;
        if ((Keyboard.Modifiers & ModifierKeys.Alt) != 0)
            modifiers |= Core.Models.HotkeyModifiers.Alt;
        if ((Keyboard.Modifiers & ModifierKeys.Windows) != 0)
            modifiers |= Core.Models.HotkeyModifiers.Win;

        // Convertir WPF Key → Win32 Virtual Key Code
        var vkCode = (uint)KeyInterop.VirtualKeyFromKey(key);

        HotkeyModifiers = (uint)modifiers;
        VirtualKeyCode = vkCode;

        var display2 = FormatHotkey(modifiers, vkCode);
        HotkeyDisplay = display2;
        Text = display2;

        Keyboard.ClearFocus();
    }

    /// <summary>
    /// Normalise les VK spécifiques (Left/Right) vers les VK génériques.
    /// Ex: VK_LMENU (0xA4) → VK_MENU (0x12)
    /// </summary>
    private static uint NormalizeVirtualKey(Key key) => key switch
    {
        Key.LeftCtrl or Key.RightCtrl => 0x11,   // VK_CONTROL
        Key.LeftAlt or Key.RightAlt => 0x12,     // VK_MENU
        Key.LeftShift or Key.RightShift => 0x10, // VK_SHIFT
        Key.LWin or Key.RWin => 0x5B,            // VK_LWIN
        _ => (uint)KeyInterop.VirtualKeyFromKey(key)
    };

    internal static string FormatHotkey(Core.Models.HotkeyModifiers modifiers, uint vk)
    {
        var sb = new StringBuilder();

        if (modifiers.HasFlag(Core.Models.HotkeyModifiers.Control))
            sb.Append("Ctrl+");
        if (modifiers.HasFlag(Core.Models.HotkeyModifiers.Alt))
            sb.Append("Alt+");
        if (modifiers.HasFlag(Core.Models.HotkeyModifiers.Shift))
            sb.Append("Shift+");
        if (modifiers.HasFlag(Core.Models.HotkeyModifiers.Win))
            sb.Append("Win+");

        sb.Append(GetKeyName(vk));
        return sb.ToString();
    }

    internal static string GetKeyName(uint vk) => vk switch
    {
        0x10 => "Shift",
        0x11 => "Ctrl",
        0x12 => "Alt",
        0x5B => "Win",
        >= 0x70 and <= 0x87 => $"F{vk - 0x70 + 1}",  // F1-F24
        >= 0x30 and <= 0x39 => ((char)vk).ToString(),   // 0-9
        >= 0x41 and <= 0x5A => ((char)vk).ToString(),   // A-Z
        0x09 => "Tab",
        0x0D => "Enter",
        0x20 => "Espace",
        0x08 => "Backspace",
        0x2E => "Delete",
        0x24 => "Home",
        0x23 => "End",
        0x21 => "PageUp",
        0x22 => "PageDown",
        0x25 => "Left",
        0x26 => "Up",
        0x27 => "Right",
        0x28 => "Down",
        0x2D => "Insert",
        0x14 => "CapsLock",
        0x90 => "NumLock",
        0x91 => "ScrollLock",
        0x13 => "Pause",
        0x2C => "PrintScreen",
        0xC0 => "`",
        0xBD => "-",
        0xBB => "=",
        0xDB => "[",
        0xDD => "]",
        0xDC => "\\",
        0xBA => ";",
        0xDE => "'",
        0xBC => ",",
        0xBE => ".",
        0xBF => "/",
        0x60 => "Num0",
        0x61 => "Num1",
        0x62 => "Num2",
        0x63 => "Num3",
        0x64 => "Num4",
        0x65 => "Num5",
        0x66 => "Num6",
        0x67 => "Num7",
        0x68 => "Num8",
        0x69 => "Num9",
        0x6A => "Num*",
        0x6B => "Num+",
        0x6D => "Num-",
        0x6E => "Num.",
        0x6F => "Num/",
        _ => $"VK_{vk:X2}"
    };
}
