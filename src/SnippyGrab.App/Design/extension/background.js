"use strict";
let importing = false;
chrome.runtime.onMessage.addListener((message, sender, respond) => {
    if (sender.id !== chrome.runtime.id || sender.tab || !["full", "visible", "pick", "element"].includes(message.command)) return;
    (async () => {
        if (importing) throw Error("An import is already running. Wait for it to finish.");
        importing = true;
        let target, attached = false, metricsSet = false;
        try {
            const [tab] = await chrome.tabs.query({ active: true, currentWindow: true });
            if (!tab?.id || !/^https?:/.test(tab.url || "")) throw Error("Choose an HTTP(S) page. Browser settings and protected pages cannot be imported.");
            target = { tabId: tab.id };
            await chrome.scripting.executeScript({ target, files: ["capture.js"] });
            if (message.command === "pick") {
                await chrome.scripting.executeScript({ target, func: () => snippyPick() });
                return { ok: true };
            }
            const [{ result: viewport }] = await chrome.scripting.executeScript({ target, func: () => ({ width: innerWidth, height: innerHeight, deviceScaleFactor: devicePixelRatio }) });
            try { await chrome.debugger.attach(target, "1.3"); attached = true; }
            catch { throw Error("Could not start page capture. Reload the extension to accept its page-capture permission, close DevTools or another debugger on this tab, and retry."); }
            // The debugger notification bar can shrink the layout viewport. Capture the size
            // the user selected before attaching, including its original device scale.
            await chrome.debugger.sendCommand(target, "Emulation.setDeviceMetricsOverride", { ...viewport, mobile: false, dontSetVisibleSize: true });
            metricsSet = true;
            const [extracted] = await chrome.scripting.executeScript({
                target,
                func: (mode) => { globalThis.snippyPickCleanup?.(); return snippyExtract({ mode }); },
                args: [message.command],
            });
            const capture = extracted?.result;
            if (!capture?.region) throw Error("Page extraction failed. Reload the page and retry.");
            const { data } = await chrome.debugger.sendCommand(target, "Page.captureScreenshot", {
                format: "png", captureBeyondViewport: true, fromSurface: true,
                clip: { ...capture.region, scale: 1 / viewport.deviceScaleFactor },
            });
            const [current] = await chrome.scripting.executeScript({ target, func: () => true });
            if (current.documentId !== extracted.documentId) throw Error("The page navigated during capture. Wait for it to load and retry.");
            const payload = { command: "capture", capture, screenshot: "data:image/png;base64," + data };
            if (new TextEncoder().encode(JSON.stringify(payload)).length > 64 * 1024 * 1024) throw Error("Browser import exceeds 64 MiB. Choose a smaller element or visible area.");
            try { return await chrome.runtime.sendNativeMessage("com.snippygrab.design", payload); }
            catch { throw Error("Could not reach SnippyGrab. Register this extension in UI Design → Browser extension and retry."); }
        } finally {
            if (target) {
                try { await chrome.scripting.executeScript({ target, func: () => globalThis.snippyFreezeCleanup?.() }); } catch {}
                if (metricsSet) { try { await chrome.debugger.sendCommand(target, "Emulation.clearDeviceMetricsOverride"); } catch {} }
                if (attached) { try { await chrome.debugger.detach(target); } catch {} }
            }
            importing = false;
        }
    })().then(respond, (error) => respond({ ok: false, error: error.message }));
    return true;
});
