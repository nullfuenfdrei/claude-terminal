// Claude Terminal – Linux-App (Electron). Lädt die gemeinsame index.html.
const { app, BrowserWindow, session, shell, protocol, net, ipcMain } = require("electron");
const { spawn, spawnSync } = require("child_process");
const os = require("os");
const fs = require("fs");
const path = require("path");
const { pathToFileURL } = require("url");

// Eigene Origin app://claude-terminal, damit localStorage (Key, Einstellungen) dauerhaft bleibt
protocol.registerSchemesAsPrivileged([
  { scheme: "app", privileges: { standard: true, secure: true, supportFetchAPI: true, corsEnabled: true } },
]);

app.setName("claude-terminal");

function createWindow() {
  const win = new BrowserWindow({
    width: 1400,
    height: 900,
    backgroundColor: "#000000",
    title: "Claude Terminal",
    icon: path.join(__dirname, "icon.png"),
    autoHideMenuBar: true,
    webPreferences: { preload: path.join(__dirname, "preload.js") },
  });
  win.setMenu(null);

  // F11 = Vollbild
  win.webContents.on("before-input-event", (e, input) => {
    if (input.type === "keyDown" && input.key === "F11") { win.setFullScreen(!win.isFullScreen()); e.preventDefault(); }
  });
  // Links (z. B. console.anthropic.com) im normalen Browser öffnen
  win.webContents.setWindowOpenHandler(({ url }) => {
    if (/^https?:\/\//.test(url)) shell.openExternal(url);
    return { action: "deny" };
  });
  win.loadURL("app://claude-terminal/index.html");
}

app.whenReady().then(() => {
  protocol.handle("app", (req) => {
    let p = decodeURIComponent(new URL(req.url).pathname);
    if (!p || p === "/") p = "/index.html";
    const file = path.resolve(__dirname, "." + p);
    if (!file.startsWith(__dirname + path.sep)) return new Response("Not found", { status: 404 }); // nur Dateien aus dem App-Ordner
    return net.fetch(pathToFileURL(file).toString());
  });
  // Mikrofon/Audio-Monitor für den Visualizer erlauben (nur diese App)
  session.defaultSession.setPermissionRequestHandler((_wc, perm, cb) => cb(perm === "media"));
  session.defaultSession.setPermissionCheckHandler((_wc, perm) => perm === "media");
  createWindow();
  app.on("activate", () => { if (BrowserWindow.getAllWindows().length === 0) createWindow(); });
});

app.on("window-all-closed", () => app.quit());

// ===== Abo-Modus: Chat über die lokal installierte Claude-Code-CLI ("claude -p") =====
// Läuft über das Claude-Abo des Nutzers – kein API-Key, kein Guthaben nötig.
const HOME = os.homedir();
process.env.PATH = [process.env.PATH, HOME + "/.local/bin", HOME + "/.claude/local", "/usr/local/bin", "/usr/bin"].filter(Boolean).join(":");
const WORK = path.join(HOME, ".local", "share", "claude-terminal", "chat");

ipcMain.on("host", (e, msg) => {
  if (!msg || typeof msg !== "object") return;
  if (msg.t === "cclogin") return openLoginTerminal();
  if (msg.t !== "cc") return;
  const send = (o) => { if (!e.sender.isDestroyed()) e.sender.send("host", Object.assign({ t: "cc", id: msg.id }, o)); };
  fs.mkdirSync(WORK, { recursive: true });
  const args = ["-p", "--output-format", "stream-json", "--verbose", "--include-partial-messages", "--tools", "", "--strict-mcp-config"];
  if (msg.model) args.push("--model", String(msg.model));
  if (msg.system) args.push("--append-system-prompt", String(msg.system));
  if (msg.resume) args.push("--resume", String(msg.resume));
  let child;
  try { child = spawn("claude", args, { cwd: WORK, env: process.env }); }
  catch (err) { return send({ exit: -1, err: String(err.message || err) }); }
  let buf = "", err = "";
  child.on("error", (er) => send({ exit: -1, err: String(er.code || er.message) + ": Claude Code nicht gefunden" }));
  child.stdout.setEncoding("utf8");
  child.stdout.on("data", (d) => { buf += d; let i; while ((i = buf.indexOf(String.fromCharCode(10))) >= 0) { const line = buf.slice(0, i).trim(); buf = buf.slice(i + 1); if (line) send({ line }); } });
  child.stderr.on("data", (d) => { err = (err + d).slice(-400); });
  child.on("close", (code) => { if (buf.trim()) send({ line: buf.trim() }); send({ exit: code, err }); });
  child.stdin.end(String(msg.prompt || ""));
});

function openLoginTerminal() {
  const cands = [process.env.TERMINAL, "kitty", "alacritty", "foot", "wezterm", "konsole", "gnome-terminal", "xfce4-terminal", "xterm"].filter(Boolean);
  for (const t of cands) {
    if (spawnSync("sh", ["-c", "command -v " + JSON.stringify(t)]).status !== 0) continue;
    const args = t === "gnome-terminal" ? ["--", "claude"] : t === "wezterm" ? ["start", "--", "claude"] : ["-e", "claude"];
    spawn(t, args, { detached: true, stdio: "ignore", env: process.env }).unref();
    return;
  }
}
