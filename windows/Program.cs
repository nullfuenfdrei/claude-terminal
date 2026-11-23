using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }
}

class MainForm : Form
{
    [DllImport("dwmapi.dll")]
    static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    readonly WebView2 web = new() { Dock = DockStyle.Fill, DefaultBackgroundColor = Color.Black };

    public MainForm()
    {
        Text = "Claude Terminal";
        BackColor = Color.Black;
        Width = 1400; Height = 900;
        StartPosition = FormStartPosition.CenterScreen;
        try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }
        Controls.Add(web);
        KeyPreview = true;
        KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.F11) ToggleFullscreen();
            if (e.KeyCode == Keys.Escape && FormBorderStyle == FormBorderStyle.None) ToggleFullscreen();
        };
        Load += async (_, _) => await InitAsync();
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        int on = 1; // dunkle Titelleiste
        DwmSetWindowAttribute(Handle, 20, ref on, sizeof(int));
    }

    FormWindowState prevState;
    void ToggleFullscreen()
    {
        if (FormBorderStyle == FormBorderStyle.None)
        { FormBorderStyle = FormBorderStyle.Sizable; WindowState = prevState; }
        else
        { prevState = WindowState; FormBorderStyle = FormBorderStyle.None; WindowState = FormWindowState.Normal; WindowState = FormWindowState.Maximized; }
    }

    async Task InitAsync()
    {
        var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ClaudeTerminal");
        var webDir = Path.Combine(root, "web");
        Directory.CreateDirectory(webDir);
        using (var s = Assembly.GetExecutingAssembly().GetManifestResourceStream("index.html")!)
        using (var f = File.Create(Path.Combine(webDir, "index.html")))
            s.CopyTo(f);

        var env = await CoreWebView2Environment.CreateAsync(null, Path.Combine(root, "profile"));
        await web.EnsureCoreWebView2Async(env);
        var cw = web.CoreWebView2;
        cw.Settings.AreDefaultContextMenusEnabled = false;
        cw.Settings.IsStatusBarEnabled = false;
        cw.Settings.AreDevToolsEnabled = false;
        cw.SetVirtualHostNameToFolderMapping("claude-terminal.app", webDir, CoreWebView2HostResourceAccessKind.Allow);
        // Links (z. B. "console.anthropic.com") im normalen Browser öffnen statt in einem Popup
        cw.NewWindowRequested += (_, e) =>
        {
            e.Handled = true;
            if (Uri.TryCreate(e.Uri, UriKind.Absolute, out var u) && (u.Scheme == "https" || u.Scheme == "http"))
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(u.ToString()) { UseShellExecute = true });
        };
        cw.Navigate("https://claude-terminal.app/index.html");

        // PC-Sound abgreifen und ~60x pro Sekunde an die Grafik schicken
        try
        {
            audio.Start();
            var timer = new System.Windows.Forms.Timer { Interval = 15 };
            timer.Tick += (_, _) => { var j = audio.TakeJson(); if (j != null) cw.PostWebMessageAsJson(j); };
            timer.Start();
        }
        catch { /* kein Ausgabegerät – Visualizer bleibt aus */ }
    }

    readonly AudioTap audio = new();

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        audio.Dispose();
        base.OnFormClosed(e);
    }
}
