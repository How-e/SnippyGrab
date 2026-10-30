"use strict";
chrome.runtime.onMessage.addListener((message, sender, respond) => {
    if (
        sender.id !== chrome.runtime.id ||
        !["visible", "pick", "element"].includes(message.command)
    )
        return;
    (async () => {
        const [tab] = await chrome.tabs.query({
            active: true,
            currentWindow: true,
        });
        if (!tab?.id || !/^https?:/.test(tab.url || ""))
            throw Error(
                "Choose an HTTP(S) page. Browser settings and protected pages cannot be imported.",
            );
        await chrome.scripting.executeScript({
            target: { tabId: tab.id },
            files: ["capture.js"],
        });
        if (message.command === "pick") {
            await chrome.scripting.executeScript({
                target: { tabId: tab.id },
                func: () => snippyPick(),
            });
            return { ok: true };
        }
        try {
            const [{ result: capture }] = await chrome.scripting.executeScript({
                target: { tabId: tab.id },
                func: (mode) => {
                    globalThis.snippyPickCleanup?.();
                    return snippyExtract({ mode });
                },
                args: [message.command === "element" ? "element" : "visible"],
            });
            const full = await chrome.tabs.captureVisibleTab(tab.windowId, {
                format: "png",
            });
            let screenshot = full;
            if (message.command === "element") {
                const blob = await (await fetch(full)).blob(),
                    image = await createImageBitmap(blob),
                    r = capture.region,
                    ratio = capture.devicePixelRatio;
                const [{ result: scroll }] =
                    await chrome.scripting.executeScript({
                        target: { tabId: tab.id },
                        func: () => ({ x: scrollX, y: scrollY }),
                    });
                const x = (r.x - scroll.x) * ratio,
                    y = (r.y - scroll.y) * ratio,
                    w = r.width * ratio,
                    h = r.height * ratio;
                if (
                    x < 0 ||
                    y < 0 ||
                    x + w > image.width + 1 ||
                    y + h > image.height + 1
                )
                    throw Error(
                        "The selected element must be fully visible. Scroll it into view or import the visible area.",
                    );
                const canvas = new OffscreenCanvas(Math.ceil(w), Math.ceil(h));
                canvas
                    .getContext("2d")
                    .drawImage(image, x, y, w, h, 0, 0, w, h);
                const bytes = new Uint8Array(
                    await (
                        await canvas.convertToBlob({ type: "image/png" })
                    ).arrayBuffer(),
                );
                let binary = "";
                for (let i = 0; i < bytes.length; i += 32768)
                    binary += String.fromCharCode(
                        ...bytes.subarray(i, i + 32768),
                    );
                screenshot = "data:image/png;base64," + btoa(binary);
            }
            if (capture.fonts?.length)
                capture.limitations.push(
                    "The existing-tab adapter does not bundle custom font files. Compare text wrapping or use URL import to capture available font resources.",
                );
            delete capture.fonts;
            const response = await chrome.runtime.sendNativeMessage(
                "com.snippygrab.design",
                { command: "capture", capture, screenshot },
            );
            return response;
        } finally {
            try {
                await chrome.scripting.executeScript({
                    target: { tabId: tab.id },
                    func: () => globalThis.snippyFreezeCleanup?.(),
                });
            } catch {}
        }
    })().then(respond, (error) =>
        respond({
            ok: false,
            error:
                error.message +
                " If the native host is unavailable, register this extension in SnippyGrab.",
        }),
    );
    return true;
});
