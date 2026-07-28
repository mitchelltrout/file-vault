using System.Drawing;
using System.Windows.Forms;

namespace FileVault.UI.Services;

public class SystemTrayService : IDisposable
{
    private readonly NotifyIcon _trayIcon;

    public event Action? ShowWindowRequested;
    public event Action? ExitRequested;

    public SystemTrayService()
    {
        _trayIcon = new NotifyIcon
        {
            Text = "FileVault",
            Icon = CreateTextIcon("FV"),
            Visible = false,
        };

        _trayIcon.DoubleClick += (_, _) => ShowWindowRequested?.Invoke();

        var menu = new ContextMenuStrip();
        var showItem = new ToolStripMenuItem("Open FileVault");
        showItem.Click += (_, _) => ShowWindowRequested?.Invoke();
        var exitItem = new ToolStripMenuItem("Exit");
        exitItem.Click += (_, _) => ExitRequested?.Invoke();

        menu.Items.Add(showItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(exitItem);

        _trayIcon.ContextMenuStrip = menu;
    }

    private static Icon CreateTextIcon(string text)
    {
        using var bmp = new Bitmap(32, 32);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        g.Clear(Color.Black);
        using var font = new Font("Segoe UI", 11, FontStyle.Bold, GraphicsUnit.Pixel);
        using var brush = new SolidBrush(Color.White);
        var size = g.MeasureString(text, font);
        g.DrawString(text, font, brush, (32 - size.Width) / 2, (32 - size.Height) / 2);
        return Icon.FromHandle(bmp.GetHicon());
    }

    public void Show() => _trayIcon.Visible = true;

    public void Dispose()
    {
        _trayIcon.Visible = false;
        _trayIcon.Dispose();
    }
}
