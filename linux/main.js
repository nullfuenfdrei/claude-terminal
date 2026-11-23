// Claude Terminal – Linux-App (Electron). Lädt die gemeinsame index.html.
const { app, BrowserWindow, session, shell, protocol, net } = require("electron");
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
