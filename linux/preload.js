// Sagt der Seite, dass sie in der Desktop-App läuft (für PC-Sound über PipeWire/PulseAudio)
const { contextBridge } = require("electron");
contextBridge.exposeInMainWorld("claudeHost", { app: "electron", platform: process.platform });
