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
        KeyDown += (_, e) => { if (e.KeyCode == Keys.F11) ToggleFullscreen(); };
        // Start direkt im Vollbild (F11 schaltet um)
        Load += async (_, _) => { ToggleFullscreen(); await InitAsync(); };
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        int on = 1; // dunkle Titelleiste
        DwmSetWindowAttribute(Handle, 20, ref on, sizeof(int));
    }

    FormWindowState prevState;
    long lastToggle;
    void ToggleFullscreen()
    {
        if (Environment.TickCount64 - lastToggle < 300) return; // F11 kann doppelt ankommen (Form + WebView)
        lastToggle = Environment.TickCount64;
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
        // F11 auch dann, wenn die Seite den Fokus hat
        cw.Settings.AreBrowserAcceleratorKeysEnabled = true;
        web.KeyDown += (_, e) => { if (e.KeyCode == Keys.F11) ToggleFullscreen(); };
        // Abo-Modus: Nachrichten der Seite an Claude Code weiterreichen
        cw.WebMessageReceived += (_, e) =>
        {
            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(e.WebMessageAsJson);
                var r = doc.RootElement.Clone();
                var t = r.TryGetProperty("t", out var tv) ? tv.GetString() : null;
                if (t == "cc") ClaudeCodeBridge.Run(r, json => BeginInvoke(() => cw.PostWebMessageAsJson(json)));
                else if (t == "cclogin") ClaudeCodeBridge.OpenLogin();
            }
            catch { }
        };
        // Große Bildschirme: alles größer und besser lesbar
        var sw = Screen.FromControl(this).Bounds.Width;
        web.ZoomFactor = sw >= 3200 ? 1.6 : sw >= 2400 ? 1.3 : sw >= 1900 ? 1.1 : 1.0;
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
