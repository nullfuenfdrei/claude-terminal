// Brücke zwischen Seite und Desktop-App (PC-Sound-Erkennung + Abo-Modus über Claude Code)
const { contextBridge, ipcRenderer } = require("electron");
contextBridge.exposeInMainWorld("claudeHost", {
  app: "electron",
  platform: process.platform,
  send: (msg) => ipcRenderer.send("host", msg),
  onMessage: (cb) => ipcRenderer.on("host", (_e, msg) => cb(msg)),
});
