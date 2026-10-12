using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using Microsoft.Win32;
using SystemMonitor.Native;

namespace SystemMonitor.Services;

/// <summary>
/// Draws a number (the live CPU %) into a small-icon-sized bitmap for the tray. Each
/// render creates a native HICON; <see cref="Dispose"/> (and the next render) destroy the
/// previous one, so the tray never leaks GDI handles while updating every second.
/// </summary>
public sealed class TrayIconRenderer : IDisposable
{
    private nint _currentHandle;
    private Icon? _currentIcon;

    public Icon Render(int percent)
    {
        var size = System.Windows.Forms.SystemInformation.SmallIconSize;
        var text = Math.Clamp(percent, 0, 100).ToString(System.Globalization.CultureInfo.InvariantCulture);

        using var bitmap = new Bitmap(size.Width, size.Height);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            g.Clear(Color.Transparent);

            // "100" needs a smaller font than one or two digits to fit the square.
            var emSize = size.Height * (text.Length >= 3 ? 0.46f : 0.62f);
            using var font = new Font("Segoe UI", emSize, FontStyle.Bold, GraphicsUnit.Pixel);
            using var brush = new SolidBrush(IsTaskbarLight() ? Color.Black : Color.White);
            using var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            g.DrawString(text, font, brush, new RectangleF(0, 0, size.Width, size.Height), format);
        }

        var handle = bitmap.GetHicon();
        var icon = Icon.FromHandle(handle);
        ReleaseCurrent();
        _currentHandle = handle;
        _currentIcon = icon;
        return icon;
    }

    /// <summary>The taskbar follows "SystemUsesLightTheme" (not the apps setting), so text contrast must too.</summary>
    private static bool IsTaskbarLight()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("SystemUsesLightTheme") is int i && i != 0;
        }
        catch
        {
            return false;
        }
    }

    private void ReleaseCurrent()
    {
        _currentIcon?.Dispose();
        _currentIcon = null;
        if (_currentHandle != 0)
        {
            NativeMethods.DestroyIcon(_currentHandle);
            _currentHandle = 0;
        }
    }

    public void Dispose() => ReleaseCurrent();
}
