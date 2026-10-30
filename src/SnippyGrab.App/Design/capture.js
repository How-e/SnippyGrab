/* Runs in the source browser with no native host bridge. Output is data only. */
globalThis.snippyExtract = async function (options = {}) {
    globalThis.snippyFreezeCleanup?.();
    const animations = document
        .getAnimations()
        .map((animation) => ({
            animation,
            running: animation.playState === "running",
        }));
    for (const { animation, running } of animations)
        if (running)
            try {
                animation.pause();
            } catch {}
    globalThis.snippyFreezeCleanup = () => {
        for (const { animation, running } of animations)
            if (running)
                try {
                    animation.play();
                } catch {}
    };
    const mode = options.mode || "visible";
    const root =
        mode === "element" ? globalThis.snippyTarget : document.documentElement;
    if (!root || !root.isConnected)
        throw Error("Select an element on the current page first.");
    await Promise.race([
        document.fonts.ready,
        new Promise((resolve) => setTimeout(resolve, 3000)),
    ]);
    const rootBox = root.getBoundingClientRect();
    const region =
        mode === "element"
            ? {
                  x: rootBox.left + scrollX,
                  y: rootBox.top + scrollY,
                  width: rootBox.width,
                  height: rootBox.height,
              }
            : mode === "full"
              ? {
                    x: 0,
                    y: 0,
                    width: document.documentElement.scrollWidth,
                    height: document.documentElement.scrollHeight,
                }
              : {
                    x: scrollX,
                    y: scrollY,
                    width: innerWidth,
                    height: innerHeight,
                };
    if (
        region.width < 1 ||
        region.height < 1 ||
        region.width > 16384 ||
        region.height > 16384 ||
        region.width * region.height > 64000000
    )
        throw Error(
            "Choose a smaller capture (64 MP and 16384 pixels per axis).",
        );
    const layers = [],
        limitations = new Set(),
        fonts = [],
        started = performance.now();
    const color = (value) => {
        const match = String(value).match(/^rgba?\(([^)]+)\)/);
        if (!match)
            return /^#[0-9a-f]{6,8}$/i.test(value)
                ? value.toLowerCase()
                : "#00000000";
        const parts = match[1]
            .split(/[, /]+/)
            .filter(Boolean)
            .map(Number);
        return (
            "#" +
            parts
                .slice(0, 3)
                .map((v) =>
                    Math.max(0, Math.min(255, Math.round(v)))
                        .toString(16)
                        .padStart(2, "0"),
                )
                .join("") +
            (parts.length > 3
                ? Math.round(parts[3] * 255)
                      .toString(16)
                      .padStart(2, "0")
                : "")
        );
    };
    const selector = (element) => {
        if (element.dataset.testid)
            return (
                "[data-testid=" + JSON.stringify(element.dataset.testid) + "]"
            );
        if (element.id) return "#" + CSS.escape(element.id);
        const pieces = [];
        let e = element;
        for (let i = 0; e && i < 5; i++, e = e.parentElement) {
            let s = e.localName;
            const siblings = e.parentElement
                ? [...e.parentElement.children].filter((n) => n.localName === s)
                : [];
            if (siblings.length > 1)
                s += ":nth-of-type(" + (siblings.indexOf(e) + 1) + ")";
            pieces.unshift(s);
        }
        return pieces.join(" > ");
    };
    const source = (e) => {
        let file = e.getAttribute("data-snippy-file") || "",
            component = e.getAttribute("data-snippy-component") || "";
        if (
            file.startsWith("/") ||
            file.includes(":") ||
            file.split(/[\\/]/).includes("..")
        )
            file = "";
        return {
            key: e.dataset.snippyId || selector(e),
            selector: selector(e),
            role: e.getAttribute("role") || e.localName,
            name: (
                e.getAttribute("aria-label") ||
                e.getAttribute("alt") ||
                ""
            ).slice(0, 1000),
            status: "observed",
            file,
            component,
        };
    };
    const bounds = (e) => {
        const b = e.getBoundingClientRect();
        return {
            x: b.left + scrollX - region.x,
            y: b.top + scrollY - region.y,
            width: b.width,
            height: b.height,
        };
    };
    const outside = (b) =>
        b.x + b.width <= 0 ||
        b.y + b.height <= 0 ||
        b.x >= region.width ||
        b.y >= region.height;
    const add = (n) => {
        if (layers.length >= 10000 || performance.now() - started > 30000)
            throw Error(
                "Capture exceeded 10000 layers or 30 seconds. Select a smaller element.",
            );
        n.id = "layer-" + layers.length;
        layers.push(n);
        return n;
    };
    async function walk(e, parent, origin, depth) {
        if (depth > 100) {
            limitations.add("Deep content was omitted at the hierarchy limit.");
            return;
        }
        if (
            /^(script|style|link|meta|head|noscript|template)$/.test(
                e.localName,
            ) ||
            e.matches("input[type=password],input[type=hidden]")
        )
            return;
        const c = getComputedStyle(e),
            b = bounds(e);
        if (
            c.display === "none" ||
            c.visibility === "hidden" ||
            Number(c.opacity) === 0 ||
            b.width < 0.1 ||
            b.height < 0.1 ||
            outside(b)
        )
            return;
        if (b.width > 32768 || b.height > 32768) {
            const right = Math.min(region.width, b.x + b.width),
                bottom = Math.min(region.height, b.y + b.height);
            b.x = Math.max(0, b.x);
            b.y = Math.max(0, b.y);
            b.width = right - b.x;
            b.height = bottom - b.y;
            limitations.add(
                "Oversized source containers were clipped to the captured area.",
            );
        }
        let fallback = "";
        const matrix =
            c.transform === "none" ? null : new DOMMatrixReadOnly(c.transform);
        if (
            matrix &&
            (Math.abs(matrix.b) > 0.0001 ||
                Math.abs(matrix.c) > 0.0001 ||
                Math.abs(matrix.a - 1) > 0.0001 ||
                Math.abs(matrix.d - 1) > 0.0001)
        )
            fallback = "Transformed content captured as an image";
        if (mode === "full" && ["fixed", "sticky"].includes(c.position))
            limitations.add(
                "Fixed/sticky regions use their observed capture position. Compare them with the original.",
            );
        if (
            c.filter !== "none" ||
            (c.backdropFilter && c.backdropFilter !== "none") ||
            c.mixBlendMode !== "normal" ||
            (c.maskImage && c.maskImage !== "none")
        )
            fallback = "CSS effect captured as an image";
        let gradientStops = [],
            gradientAngle = 180,
            shadowColor = "#00000000",
            shadowX = 0,
            shadowY = 0,
            shadowBlur = 0;
        if (c.backgroundImage !== "none") {
            const gradient = c.backgroundImage.match(
                /^linear-gradient\((.*)\)$/,
            );
            if (gradient) {
                const parts = gradient[1]
                    .split(/,(?![^()]*\))/)
                    .map((p) => p.trim());
                if (parts[0].includes("deg"))
                    gradientAngle = parseFloat(parts.shift());
                else if (parts[0].startsWith("to ")) {
                    const direction = parts.shift();
                    gradientAngle =
                        direction === "to right"
                            ? 90
                            : direction === "to left"
                              ? 270
                              : direction === "to top"
                                ? 0
                                : direction === "to bottom"
                                  ? 180
                                  : NaN;
                }
                gradientStops = parts.map((part, i) => {
                    const match = part.match(
                        /^(rgba?\([^)]+\)|#[0-9a-f]+)(?:\s+([\d.]+)%)?$/i,
                    );
                    return match
                        ? {
                              color: color(match[1]),
                              offset:
                                  match[2] === undefined
                                      ? i / Math.max(1, parts.length - 1)
                                      : Number(match[2]) / 100,
                          }
                        : null;
                });
                if (
                    !Number.isFinite(gradientAngle) ||
                    gradientStops.some((s) => !s) ||
                    gradientStops.length > 20
                ) {
                    gradientStops = [];
                    fallback = "Complex gradient captured as an image";
                }
            } else fallback = "Background image captured as an image";
        }
        if (c.boxShadow !== "none") {
            const shadow = c.boxShadow.match(
                /^(rgba?\([^)]+\))\s+(-?[\d.]+)px\s+(-?[\d.]+)px\s+([\d.]+)px(?:\s+(-?[\d.]+)px)?$/,
            );
            if (shadow && (!shadow[5] || Number(shadow[5]) === 0)) {
                shadowColor = color(shadow[1]);
                shadowX = Number(shadow[2]);
                shadowY = Number(shadow[3]);
                shadowBlur = Number(shadow[4]);
            } else fallback = "Complex shadow captured as an image";
        }
        for (const pseudo of ["::before", "::after"]) {
            const content = getComputedStyle(e, pseudo).content;
            if (
                content &&
                content !== "none" &&
                content !== "normal" &&
                content !== '""'
            )
                fallback = "Generated content captured as an image";
        }
        if (
            /^(img|canvas|video|iframe|object|embed|input|textarea|select)$/.test(
                e.localName,
            )
        )
            fallback =
                e.localName === "img"
                    ? "Captured image"
                    : "Browser control or embedded content captured as an image";
        if (e.shadowRoot) fallback = "Shadow-root content captured as an image";
        if (e.localName === "svg") {
            const paths = [...e.children];
            if (
                paths.length &&
                paths.every(
                    (p) =>
                        p.localName === "path" && !p.getAttribute("transform"),
                )
            ) {
                const vb = e.viewBox.baseVal;
                const group = add({
                    parent,
                    kind: "group",
                    name: e.getAttribute("aria-label") || "SVG icon",
                    ...b,
                    x: b.x - origin.x,
                    y: b.y - origin.y,
                    source: source(e),
                });
                for (const p of paths) {
                    const s = getComputedStyle(p);
                    add({
                        parent: group.id,
                        kind: "path",
                        name: "Vector path",
                        x: 0,
                        y: 0,
                        width: b.width,
                        height: b.height,
                        path: p.getAttribute("d") || "",
                        viewBox: [
                            vb.x,
                            vb.y,
                            vb.width || b.width,
                            vb.height || b.height,
                        ].join(" "),
                        fill: color(s.fill),
                        stroke: color(s.stroke),
                        strokeWidth: parseFloat(s.strokeWidth) || 0,
                        source: source(e),
                    });
                }
                return;
            }
            fallback = "Complex SVG captured as an image";
        }
        const node = add({
            parent,
            kind: fallback ? "image" : "frame",
            name: (
                e.getAttribute("aria-label") ||
                e.dataset.testid ||
                e.localName
            ).slice(0, 500),
            x: b.x - origin.x,
            y: b.y - origin.y,
            width: b.width,
            height: b.height,
            fill: color(c.backgroundColor),
            stroke: color(c.borderTopColor),
            strokeWidth: parseFloat(c.borderTopWidth) || 0,
            radius: parseFloat(c.borderTopLeftRadius) || 0,
            opacity: fallback ? 1 : Number(c.opacity),
            clip: c.overflow !== "visible",
            layout:
                c.display === "flex" && c.flexDirection === "column"
                    ? "vertical"
                    : c.display === "flex" && c.flexDirection === "row"
                      ? "horizontal"
                      : "free",
            gap: parseFloat(c.gap) || 0,
            shadowColor,
            shadowX,
            shadowY,
            shadowBlur,
            gradientStops,
            gradientAngle,
            fallback,
            source: source(e),
        });
        if (fallback) {
            limitations.add(
                fallback +
                    ". Internal elements in these image layers are not editable.",
            );
            return;
        }
        let textIndex = 0;
        const ordered = [...e.childNodes].sort(
            (a, b) =>
                (a.nodeType === 1
                    ? parseInt(getComputedStyle(a).zIndex) || 0
                    : 0) -
                (b.nodeType === 1
                    ? parseInt(getComputedStyle(b).zIndex) || 0
                    : 0),
        );
        for (const child of ordered) {
            if (child.nodeType === Node.ELEMENT_NODE) {
                await walk(child, node.id, b, depth + 1);
                continue;
            }
            if (child.nodeType !== Node.TEXT_NODE || !child.textContent.trim())
                continue;
            const raw = child.textContent;
            const range = document.createRange();
            range.selectNodeContents(child);
            const rects = [...range.getClientRects()];
            if (!rects.length) continue;
            const lines = [];
            if (rects.length === 1)
                lines.push({
                    text: raw.replace(/\s+/g, " ").trim(),
                    box: rects[0],
                });
            else {
                if (raw.length > 20000) {
                    limitations.add(
                        "A long text run was shortened to the import budget.",
                    );
                }
                for (let i = 0; i < Math.min(raw.length, 20000); i++) {
                    range.setStart(child, i);
                    range.setEnd(child, i + 1);
                    const r = range.getBoundingClientRect();
                    if (r.width === 0) continue;
                    let line = lines.find(
                        (l) => Math.abs(l.box.top - r.top) < 1,
                    );
                    if (!line) {
                        line = {
                            text: "",
                            box: {
                                left: r.left,
                                top: r.top,
                                width: r.width,
                                height: r.height,
                            },
                        };
                        lines.push(line);
                    }
                    line.text += raw[i];
                    line.box.width = Math.max(
                        line.box.width,
                        r.right - line.box.left,
                    );
                }
            }
            for (const line of lines) {
                if (!line.text.trim()) continue;
                const r = line.box,
                    blockText = e.children.length === 0 && lines.length === 1;
                const padding = parseFloat(c.paddingLeft) || 0;
                add({
                    parent: node.id,
                    kind: "text",
                    name: line.text.trim().slice(0, 60),
                    x: blockText ? padding : r.left + scrollX - region.x - b.x,
                    y: r.top + scrollY - region.y - b.y,
                    width: blockText
                        ? Math.max(
                              1,
                              e.clientWidth -
                                  padding -
                                  (parseFloat(c.paddingRight) || 0),
                          )
                        : Math.max(1, r.width + 0.5),
                    height: Math.max(1, r.height),
                    align:
                        blockText && ["center", "right"].includes(c.textAlign)
                            ? c.textAlign
                            : "left",
                    text: line.text.replace(/\s+/g, " ").trim(),
                    fontFamily: c.fontFamily,
                    fontSize: parseFloat(c.fontSize) || 14,
                    fontWeight: parseInt(c.fontWeight) || 400,
                    lineHeight:
                        parseFloat(c.lineHeight) ||
                        parseFloat(c.fontSize) * 1.2,
                    fill: color(c.color),
                    source: {
                        ...source(e),
                        key: source(e).key + "-text-" + textIndex++,
                    },
                });
            }
        }
        if (layers.length % 100 === 0)
            await new Promise((resolve) => setTimeout(resolve, 0));
    }
    await walk(root, null, { x: 0, y: 0 }, 0);
    for (const sheet of document.styleSheets) {
        try {
            for (const rule of sheet.cssRules) {
                if (rule.type !== CSSRule.FONT_FACE_RULE) continue;
                const family = rule.style
                    .getPropertyValue("font-family")
                    .replace(/^['"]|['"]$/g, "");
                for (const match of rule.style
                    .getPropertyValue("src")
                    .matchAll(/url\(['"]?([^)'"\s]+)['"]?\)/g)) {
                    const url = new URL(match[1], sheet.href || location.href)
                        .href;
                    if (/^https?:/.test(url) && fonts.length < 40)
                        fonts.push({ url, family });
                }
            }
        } catch {
            limitations.add(
                "Some cross-origin font definitions could not be inspected.",
            );
        }
    }
    const url = new URL(location.href);
    url.username = "";
    url.password = "";
    url.search = "";
    url.hash = "";
    return {
        schemaVersion: 1,
        width: region.width,
        height: region.height,
        devicePixelRatio: devicePixelRatio,
        source: {
            url: /^https?:$/.test(url.protocol) ? url.href : "",
            title: (document.title || "Imported UI").slice(0, 500),
            locale: document.documentElement.lang,
            theme: getComputedStyle(
                document.documentElement,
            ).colorScheme.includes("dark")
                ? "dark"
                : "light",
        },
        layers,
        limitations: [...limitations],
        region,
        fonts,
    };
};
globalThis.snippyPick = function () {
    globalThis.snippyPickCleanup?.();
    let target = null;
    const overlay = document.createElement("div");
    overlay.setAttribute("data-snippy-picker", "");
    Object.assign(overlay.style, {
        position: "fixed",
        pointerEvents: "none",
        outline: "2px solid #3973db",
        background: "#3973db12",
        zIndex: "2147483647",
    });
    document.documentElement.append(overlay);
    const move = (event) => {
        target = event.target;
        const b = target.getBoundingClientRect();
        Object.assign(overlay.style, {
            left: b.left + "px",
            top: b.top + "px",
            width: b.width + "px",
            height: b.height + "px",
        });
    };
    const cleanup = () => {
        overlay.remove();
        document.removeEventListener("pointermove", move, true);
        document.removeEventListener("click", pick, true);
        document.removeEventListener("keydown", key, true);
    };
    const pick = (event) => {
        event.preventDefault();
        event.stopImmediatePropagation();
        globalThis.snippyTarget = target || event.target;
        cleanup();
    };
    const key = (event) => {
        if (event.key === "Escape") {
            event.preventDefault();
            cleanup();
        }
    };
    document.addEventListener("pointermove", move, true);
    document.addEventListener("click", pick, true);
    document.addEventListener("keydown", key, true);
    globalThis.snippyPickCleanup = cleanup;
};
