/* Usage: node scripts/test-design-extension.cjs <playwright module> <new report directory>
 * Playwright is a test-only prerequisite, not an application dependency. Uses an isolated
 * Chromium profile and loopback fixtures; only the native-message receiver is substituted.
 */
"use strict";
const assert = require("node:assert/strict");
const fs = require("node:fs/promises");
const path = require("node:path");
const os = require("node:os");
const http = require("node:http");
const { chromium } = require(path.resolve(process.argv[2]));
const output = path.resolve(process.argv[3]);
const extension = path.resolve(__dirname, "../src/SnippyGrab.App/Design");
const fontPath = process.argv[4] ? path.resolve(process.argv[4]) : null;
const fixture = `<!doctype html><html><meta charset="utf-8"><style>
${fontPath ? '@font-face{font-family:FixtureUI;src:url(/font.woff);font-weight:100 900;font-style:normal}body{font-family:FixtureUI!important}' : ''}
html,body{margin:0;background:#101214;color:#eee;font-family:Arial,sans-serif}main{padding:24px;height:6800px;box-sizing:border-box}
header{position:fixed;top:0;left:0;width:100%;height:24px;background:#304050}h1{margin-top:24px}
#stat{font-size:30px;font-weight:700}#bottom{position:absolute;top:6500px;left:0;width:100%;height:200px;background:#14283c}
#panel{height:50px;overflow:auto}#panel>div{height:150px}#pseudo:before{content:"";display:inline-block;width:14px;height:14px;background:#678}
#deferred{position:absolute;top:1700px;width:240px;height:120px;content-visibility:auto}
</style><header>Fixed header</header><main><h1>Signed-in fixture</h1><span id="stat">1 / 5</span> capture slots
<p id="pseudo">Generated icon</p><div id="panel"><div>Panel top</div><p>Panel bottom</p></div>
<div id="boxless" style="display:contents">Boxless direct text <b>Boxless child</b></div>
<div id="deferred"><p>Deferred source content</p></div><img id="lazy-picture" loading="lazy" src="/lazy-image.svg" width="80" height="40" style="position:absolute;top:6520px;left:20px">
<div id="bottom">Bottom of full signed-in page</div></main></html>`;
const server = http.createServer(async (request, response) => {
    if (request.headers.authorization !== "Basic " + Buffer.from("fixture:synthetic-password").toString("base64")) {
        response.writeHead(401, { "WWW-Authenticate": 'Basic realm="Extension fixture"' }); response.end("Sign in"); return;
    }
    if (request.url === "/font.woff" && fontPath) { response.writeHead(200, { "Content-Type": "font/woff" }); response.end(await fs.readFile(fontPath)); return; }
    if (request.url === "/lazy-image.svg") { response.writeHead(200, { "Content-Type": "image/svg+xml" }); response.end('<svg xmlns="http://www.w3.org/2000/svg" width="80" height="40"><rect width="80" height="40" fill="#ff00aa"/></svg>'); return; }
    response.writeHead(200, { "Content-Type": "text/html", "Set-Cookie": "fixture-session=yes; HttpOnly; SameSite=Lax; Path=/" });
    response.end(fixture);
});
async function waitFor(condition) {
    const deadline = Date.now() + 20000;
    while (!await condition()) { if (Date.now() > deadline) throw Error("Extension fixture timed out"); await new Promise(resolve => setTimeout(resolve, 20)); }
}
async function main() {
    await fs.mkdir(output, { recursive: true });
    const profile = await fs.mkdtemp(path.join(os.tmpdir(), "SnippyGrab-extension-check-"));
    await new Promise(resolve => server.listen(0, "127.0.0.1", resolve));
    const url = `http://127.0.0.1:${server.address().port}/private?token=synthetic-secret`;
    const context = await chromium.launchPersistentContext(profile, {
        channel: "chromium", headless: true, viewport: { width: 800, height: 500 }, deviceScaleFactor: 2,
        httpCredentials: { username: "fixture", password: "synthetic-password" },
        args: [`--disable-extensions-except=${extension}`, `--load-extension=${extension}`],
    });
    try {
        const page = context.pages()[0]; await page.goto(url);
        const worker = context.serviceWorkers()[0] || await context.waitForEvent("serviceworker");
        const browserCdp = await context.browser().newBrowserCDPSession();
        // Trigger the real extension action to grant activeTab, without broad host permissions.
        const { targetInfos } = await browserCdp.send("Target.getTargets", { filter: [{ type: "tab", exclude: false }] });
        await browserCdp.send("Extensions.triggerAction", { id: worker.url().split("/")[2], targetId: targetInfos.find(t => t.url.startsWith("http:")).targetId });
        const code = await fs.readFile(path.join(extension, "extension/background.js"), "utf8");
        await worker.evaluate(code => {
            globalThis.fixtureCaptures = []; globalThis.fixtureFailure = "";
            const api = {
                tabs: chrome.tabs, scripting: chrome.scripting,
                debugger: {
                    attach: (...args) => { if (globalThis.fixtureFailure === "attach") throw Error("fixture attach failure"); return chrome.debugger.attach(...args); },
                    sendCommand: async (...args) => {
                        if (globalThis.fixtureFailure === "screenshot" && args[1] === "Page.captureScreenshot") throw Error("fixture screenshot failure");
                        try { const result = await chrome.debugger.sendCommand(...args); if (args[1] === "Page.getResourceContent") globalThis.fixtureResource = { encoded: result.base64Encoded, length: result.content?.length, prefix: result.content?.slice(0, 8) }; return result; }
                        catch (error) { globalThis.fixtureResourceError = error.message; throw error; }
                    },
                    detach: (...args) => chrome.debugger.detach(...args),
                },
                runtime: {
                    id: chrome.runtime.id,
                    onMessage: { addListener: listener => { globalThis.fixtureListener = listener; } },
                    sendNativeMessage: async (host, payload) => {
                        if (globalThis.fixtureFailure === "native") throw Error("fixture missing native host");
                        if (globalThis.fixtureFailure === "rejected") return { ok: false, error: "fixture inbox full" };
                        if (globalThis.fixtureFailure === "pending") await new Promise(resolve => { globalThis.fixtureRelease = resolve; });
                        globalThis.fixtureCaptures.push(payload); return { ok: true };
                    },
                },
            };
            new Function("chrome", code)(api);
            globalThis.fixtureImport = command => new Promise(resolve => globalThis.fixtureListener({ command }, { id: chrome.runtime.id }, resolve));
        }, code);
        async function capture(command) {
            const result = await worker.evaluate(command => globalThis.fixtureImport(command), command);
            assert.equal(result.ok, true, result.error);
            return worker.evaluate(() => globalThis.fixtureCaptures.at(-1));
        }
        await page.evaluate(() => { scrollTo(0, 400); document.querySelector("#panel").scrollTop = 65; });
        assert.equal(await page.locator("#lazy-picture").evaluate(image => image.complete), false, "Lazy-image fixture must start unloaded");
        const full = await capture("full");
        assert(full.capture.height >= 2300); assert.equal(full.capture.devicePixelRatio, 2);
        assert(full.capture.layers.some(n => n.kind === "text" && n.text === "Bottom of full signed-in page"));
        assert(full.capture.layers.some(n => n.kind === "text" && n.text === "1 / 5" && n.width > 50 && n.noWrap));
        assert(full.capture.layers.some(n => n.kind === "text" && n.text === "Panel bottom"));
        assert(full.capture.layers.some(n => n.kind === "text" && n.text.trim() === "Boxless direct text"));
        assert(full.capture.layers.some(n => n.kind === "text" && n.text === "Boxless child"));
        assert(full.capture.layers.some(n => n.kind === "text" && n.text === "Deferred source content"));
        assert(await page.locator("#lazy-picture").evaluate(image => image.complete && image.naturalWidth === 80));
        assert.equal(await page.locator("#lazy-picture").getAttribute("loading"), "lazy");
        assert.equal(await page.locator("#deferred").getAttribute("style"), null);
        assert(!JSON.stringify(full.capture).includes("synthetic-password")); assert(!full.capture.source.url.includes("token="));
        if (fontPath) { assert(full.capture.fontAssets.some(font => font.family === "FixtureUI" && font.weight === "100 900" && font.mediaType === "font/woff"), JSON.stringify({ fonts: full.capture.fontAssets.map(({ data, ...font }) => font), limitations: full.capture.limitations, resource: await worker.evaluate(() => ({ result: globalThis.fixtureResource, error: globalThis.fixtureResourceError })) })); }
        const png = Buffer.from(full.screenshot.split(",")[1], "base64");
        assert.equal(png.readUInt32BE(16), Math.ceil(full.capture.width)); assert.equal(png.readUInt32BE(20), Math.ceil(full.capture.height));
        await fs.writeFile(path.join(output, "full-reference.png"), png);
        await fs.writeFile(path.join(output, "native-input.json"), JSON.stringify(full));
        const restored = await page.evaluate(() => ({ y: scrollY, style: document.querySelector("#panel").getAttribute("style") }));
        assert.equal(restored.y, 400); assert.equal(restored.style, null);
        assert.equal(await page.locator("#panel").evaluate(panel => panel.scrollTop), 65);
        const visibleHeight = await page.evaluate(() => innerHeight);
        const visible = await capture("visible"); assert.equal(visible.capture.height, visibleHeight);
        assert(!visible.capture.layers.some(n => n.text === "Bottom of full signed-in page"));
        await worker.evaluate(async () => { const [tab] = await chrome.tabs.query({ active: true, currentWindow: true }); await chrome.scripting.executeScript({ target: { tabId: tab.id }, func: () => { globalThis.snippyTarget = document.querySelector("#bottom"); } }); });
        const element = await capture("element"); assert.equal(element.capture.height, 200); assert(element.capture.layers.some(n => n.text === "Bottom of full signed-in page"));
        for (const failure of ["attach", "screenshot", "native", "rejected"]) {
            await worker.evaluate(failure => { globalThis.fixtureFailure = failure; }, failure);
            const result = await worker.evaluate(() => globalThis.fixtureImport("full")); assert.equal(result.ok, false);
            assert.equal(await page.evaluate(() => scrollY), 400); assert.equal(await page.locator("#panel").getAttribute("style"), null);
            await worker.evaluate(() => { globalThis.fixtureFailure = ""; }); await capture("visible");
        }
        await worker.evaluate(() => { globalThis.fixtureFailure = "pending"; globalThis.fixtureFirst = globalThis.fixtureImport("full"); });
        await waitFor(() => worker.evaluate(() => typeof globalThis.fixtureRelease === "function"));
        const concurrent = await worker.evaluate(() => globalThis.fixtureImport("full")); assert.equal(concurrent.ok, false); assert.match(concurrent.error, /already running/);
        await worker.evaluate(() => { globalThis.fixtureFailure = ""; globalThis.fixtureRelease(); });
        assert.equal((await worker.evaluate(() => globalThis.fixtureFirst)).ok, true);
        const originRejected = await worker.evaluate(() => globalThis.fixtureListener({ command: "full" }, { id: "different-extension" }, () => {})); assert.equal(originRejected, undefined);
        const tabRejected = await worker.evaluate(() => globalThis.fixtureListener({ command: "full" }, { id: chrome.runtime.id, tab: { id: 1 } }, () => {})); assert.equal(tabRejected, undefined);
        const report = { Result: "PASS", Browser: context.browser().version(), FullPage: { Width: full.capture.width, Height: full.capture.height, DevicePixelRatio: 2, ReferenceWidth: png.readUInt32BE(16), ReferenceHeight: png.readUInt32BE(20) }, FontBundle: fontPath ? "PASS" : "not exercised", Checks: ["real manifest/service worker and activeTab grant", "signed-in HTTP Basic page and cookie session", "full-page bottom content", "high-DPI reference dimensions", "inline text", "nested loaded scroller", "visible area", "offscreen picked element", "scroll/style restoration", "attach/screenshot/native/inbox errors and retry", "concurrent import rejection", "sender restrictions", "credential/query exclusion"], Scope: "Production background code and real Chrome scripting/debugger/screenshot APIs in an isolated Chromium profile. Native receiver substituted to inspect payload; native admission is checked separately. Does not establish owner Chrome/Edge profile, policy or extension-upgrade acceptance." };
        await fs.writeFile(path.join(output, "summary.json"), JSON.stringify(report, null, 2)); console.log(JSON.stringify(report));
    } finally {
        await context.close(); server.close();
        const resolvedProfile = path.resolve(profile), tempRoot = path.resolve(os.tmpdir());
        if (!resolvedProfile.startsWith(tempRoot + path.sep) || !path.basename(resolvedProfile).startsWith("SnippyGrab-extension-check-")) throw Error("Unexpected test profile path");
        await fs.rm(resolvedProfile, { recursive: true, force: true, maxRetries: 5, retryDelay: 200 });
    }
}
main().catch(error => { console.error(error); server.close(); process.exitCode = 1; });
