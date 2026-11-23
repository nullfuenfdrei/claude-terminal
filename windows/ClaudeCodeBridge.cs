using System.Diagnostics;
using System.Text.Json;

/// <summary>
/// Abo-Modus: schickt Chat-Nachrichten an die lokal installierte Claude-Code-CLI ("claude -p").
/// Die läuft über das Claude-Abo des Nutzers – kein API-Key, kein Guthaben nötig.
/// Jede Ausgabezeile (stream-json) wird 1:1 an die Seite weitergereicht; geparst wird in index.html.
/// </summary>
static class ClaudeCodeBridge
{
    static string? FindClaude()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var local = Path.Combine(home, ".local", "bin", "claude.exe");
        if (File.Exists(local)) return local;
        foreach (var dir in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator))
        {
            try { var p = Path.Combine(dir.Trim(), "claude.exe"); if (File.Exists(p)) return p; } catch { }
        }
        return null;
    }

    /// <param name="msg">{ t:"cc", id, prompt, model, system, resume }</param>
    /// <param name="post">schickt JSON an die Seite (wird auf dem UI-Thread ausgeführt)</param>
    public static void Run(JsonElement msg, Action<string> post)
    {
        string id = msg.GetProperty("id").GetString() ?? "";
        string Str(string k) => msg.TryGetProperty(k, out var v) ? v.GetString() ?? "" : "";
        void Send(object o) => post(JsonSerializer.Serialize(o));

        var exe = FindClaude();
        if (exe == null) { Send(new { t = "cc", id, exit = -1, err = "ENOENT: Claude Code nicht gefunden" }); return; }

        var work = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ClaudeTerminal", "chat");
        Directory.CreateDirectory(work);

        var psi = new ProcessStartInfo(exe)
        {
            WorkingDirectory = work,
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = System.Text.Encoding.UTF8,
            StandardErrorEncoding = System.Text.Encoding.UTF8,
        };
        foreach (var a in new[] { "-p", "--output-format", "stream-json", "--verbose", "--include-partial-messages",
                                   "--tools", "", "--strict-mcp-config" }) psi.ArgumentList.Add(a);
        if (Str("model") != "") { psi.ArgumentList.Add("--model"); psi.ArgumentList.Add(Str("model")); }
        if (Str("system") != "") { psi.ArgumentList.Add("--append-system-prompt"); psi.ArgumentList.Add(Str("system")); }
        if (Str("resume") != "") { psi.ArgumentList.Add("--resume"); psi.ArgumentList.Add(Str("resume")); }

        Task.Run(async () =>
        {
            try
            {
                using var p = Process.Start(psi)!;
                var errTask = p.StandardError.ReadToEndAsync();
                using (var stdin = new StreamWriter(p.StandardInput.BaseStream, new System.Text.UTF8Encoding(false)))
                    await stdin.WriteAsync(Str("prompt"));
                string? line;
                while ((line = await p.StandardOutput.ReadLineAsync()) != null)
                    if (line.Length > 0) Send(new { t = "cc", id, line });
                await p.WaitForExitAsync();
                var err = await errTask;
                Send(new { t = "cc", id, exit = p.ExitCode, err = err.Length > 400 ? err[^400..] : err });
            }
            catch (Exception ex) { Send(new { t = "cc", id, exit = -1, err = ex.Message }); }
        });
    }

    /// <summary>Öffnet ein Terminal mit "claude", damit sich der Nutzer einmal anmelden kann.</summary>
    public static void OpenLogin()
    {
        var exe = FindClaude();
        var cmd = exe != null ? $"\"{exe}\" auth login --claudeai" : "echo Claude Code ist nicht installiert. Installieren mit: winget install Anthropic.ClaudeCode";
        Process.Start(new ProcessStartInfo("cmd.exe", $"/k title Claude Code Anmeldung & echo Melde dich an (Abo-Account) und schliesse danach dieses Fenster. & {cmd}") { UseShellExecute = true });
    }
}
