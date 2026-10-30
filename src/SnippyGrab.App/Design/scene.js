"use strict";
globalThis.SGScene = (() => {
    const ns = "http://www.w3.org/2000/svg";
    const node = (tag, attrs = {}, text) => {
        const e = document.createElementNS(ns, tag);
        for (const [k, v] of Object.entries(attrs))
            e.setAttribute(k, String(v));
        if (text !== undefined) e.textContent = text;
        return e;
    };
    const measure = document.createElement("canvas").getContext("2d");
    function svg(doc, layers = doc.layers) {
        const root = node("svg", {
            xmlns: ns,
            width: Math.ceil(doc.width),
            height: Math.ceil(doc.height),
            viewBox: `0 0 ${doc.width} ${doc.height}`,
        });
        const defs = node("defs"),
            style = node("style");
        style.textContent = Object.values(doc.assets || {})
            .filter((a) => a.mediaType.startsWith("font/"))
            .map(
                (a) =>
                    "@font-face{font-family:" +
                    JSON.stringify(a.family) +
                    ";font-weight:" + (a.weight || "normal") + ";font-style:" + (a.style || "normal") + ";src:url(data:" +
                    a.mediaType +
                    ";base64," +
                    a.data +
                    ")}",
            )
            .join("\n");
        defs.append(style);
        root.append(defs);
        root.append(
            node("rect", {
                width: doc.width,
                height: doc.height,
                fill: "#ffffff",
            }),
        );
        const children = new Map();
        for (const n of layers) {
            const key = n.parent || "";
            if (!children.has(key)) children.set(key, []);
            children.get(key).push(n);
        }
        function draw(n) {
            if (!n.visible) return null;
            const g = node("g", {
                "data-layer": n.id,
                transform: `translate(${n.x} ${n.y}) rotate(${n.rotation} ${n.width / 2} ${n.height / 2})`,
                opacity: n.opacity,
            });
            const geometry = {
                x: 0,
                y: 0,
                width: n.width,
                height: n.height,
                fill: n.fill,
                stroke: n.stroke,
                "stroke-width": n.strokeWidth,
            };
            if (n.clip) {
                const id = "clip-" + n.id;
                const clip = node("clipPath", { id });
                clip.append(
                    node("rect", {
                        width: n.width,
                        height: n.height,
                        rx: n.radius,
                    }),
                );
                defs.append(clip);
                g.setAttribute("clip-path", `url(#${id})`);
            }
            if (n.gradientStops?.length) {
                const id = "gradient-" + n.id,
                    angle = (n.gradientAngle * Math.PI) / 180,
                    gradient = node("linearGradient", {
                        id,
                        x1: 0.5 - Math.sin(angle) / 2,
                        y1: 0.5 + Math.cos(angle) / 2,
                        x2: 0.5 + Math.sin(angle) / 2,
                        y2: 0.5 - Math.cos(angle) / 2,
                    });
                for (const stop of n.gradientStops)
                    gradient.append(
                        node("stop", {
                            offset: stop.offset,
                            "stop-color": stop.color,
                        }),
                    );
                defs.append(gradient);
                geometry.fill = `url(#${id})`;
            }
            if (n.shadowColor && n.shadowColor !== "#00000000") {
                const id = "shadow-" + n.id,
                    filter = node("filter", {
                        id,
                        x: "-100%",
                        y: "-100%",
                        width: "300%",
                        height: "300%",
                    });
                filter.append(
                    node("feDropShadow", {
                        dx: n.shadowX,
                        dy: n.shadowY,
                        stdDeviation: n.shadowBlur / 2,
                        "flood-color": n.shadowColor,
                    }),
                );
                defs.append(filter);
                geometry.filter = `url(#${id})`;
            }
            if (n.kind === "frame" || n.kind === "group")
                g.append(node("rect", { ...geometry, rx: n.radius }));
            if (n.kind === "ellipse")
                g.append(
                    node("ellipse", {
                        cx: n.width / 2,
                        cy: n.height / 2,
                        rx: n.width / 2,
                        ry: n.height / 2,
                        fill: n.fill,
                        stroke: n.stroke,
                        "stroke-width": n.strokeWidth,
                    }),
                );
            if (n.kind === "path") {
                const inner = node("svg", {
                    width: n.width,
                    height: n.height,
                    viewBox: n.viewBox,
                    preserveAspectRatio: "none",
                });
                inner.append(
                    node("path", {
                        d: n.path,
                        fill: n.fill,
                        stroke: n.stroke,
                        "stroke-width": n.strokeWidth,
                    }),
                );
                g.append(inner);
            }
            if (n.kind === "image" && doc.assets?.[n.asset]) {
                const a = doc.assets[n.asset],
                    image = node("image", {
                        width: n.width,
                        height: n.height,
                        href: "data:" + a.mediaType + ";base64," + a.data,
                        preserveAspectRatio: "none",
                    });
                if (n.radius > 0) {
                    const id = "image-clip-" + n.id,
                        clip = node("clipPath", { id });
                    clip.append(
                        node("rect", {
                            width: n.width,
                            height: n.height,
                            rx: n.radius,
                        }),
                    );
                    defs.append(clip);
                    image.setAttribute("clip-path", `url(#${id})`);
                }
                g.append(image);
                g.append(
                    node("rect", { ...geometry, fill: "none", rx: n.radius }),
                );
            }
            if (n.kind === "text") {
                measure.font = `${n.fontWeight} ${n.fontSize}px ${n.fontFamily}`;
                const lines = [];
                for (const paragraph of n.text.split("\n")) {
                    if (n.noWrap) { lines.push(paragraph); continue; }
                    let line = "";
                    for (const word of paragraph.split(/(\s+)/)) {
                        const next = line + word;
                        if (
                            line &&
                            measure.measureText(next).width > n.width + 0.5
                        ) {
                            lines.push(line.trimEnd());
                            line = word.trimStart();
                        } else line = next;
                    }
                    lines.push(line);
                }
                const text = node("text", {
                    fill: n.fill,
                    stroke: n.stroke,
                    "stroke-width": n.strokeWidth,
                    "font-family": n.fontFamily,
                    "font-size": n.fontSize,
                    "font-weight": n.fontWeight,
                    "font-style": n.fontStyle || "normal",
                    "letter-spacing": n.letterSpacing || 0,
                    "text-decoration": n.textDecoration || "none",
                    "xml:space": "preserve",
                    "dominant-baseline": "text-before-edge",
                    "text-anchor":
                        n.align === "center"
                            ? "middle"
                            : n.align === "right"
                              ? "end"
                              : "start",
                });
                lines.forEach((line, i) =>
                    text.append(
                        node(
                            "tspan",
                            {
                                x:
                                    n.align === "center"
                                        ? n.width / 2
                                        : n.align === "right"
                                          ? n.width
                                          : 0,
                                y: i * n.lineHeight,
                            },
                            line,
                        ),
                    ),
                );
                g.append(text);
            }
            for (const child of children.get(n.id) || []) {
                const rendered = draw(child);
                if (rendered) g.append(rendered);
            }
            return g;
        }
        for (const n of children.get("") || []) {
            const g = draw(n);
            if (g) root.append(g);
        }
        return root;
    }
    async function png(doc, layers = doc.layers) {
        if (doc.width * doc.height > 64000000)
            throw Error("Render exceeds 64 MP.");
        const element = svg(doc, layers),
            xml = new XMLSerializer().serializeToString(element),
            url = URL.createObjectURL(
                new Blob([xml], { type: "image/svg+xml" }),
            );
        try {
            const image = new Image();
            await new Promise((resolve, reject) => {
                image.onload = resolve;
                image.onerror = () =>
                    reject(Error("Could not render design image."));
                image.src = url;
            });
            const canvas = document.createElement("canvas");
            canvas.width = Math.ceil(doc.width);
            canvas.height = Math.ceil(doc.height);
            canvas.getContext("2d").drawImage(image, 0, 0);
            return canvas.toDataURL("image/png").split(",")[1];
        } finally {
            URL.revokeObjectURL(url);
        }
    }
    return { svg, png };
})();
