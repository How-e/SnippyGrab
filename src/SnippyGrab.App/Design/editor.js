"use strict";
(() => {
    const $ = (s) => document.querySelector(s),
        state = {
            doc: null,
            changes: [],
            selected: [],
            zoom: 1,
            autoFit: true,
            original: false,
            excluded: [],
            collapsed: new Set(),
            review: null,
            pending: false,
        };
    const send = (command, data = {}) => {
        if (
            !state.doc &&
            !["ready", "import", "open", "bridge"].includes(command)
        )
            return;
        if (state.pending) {
            status(
                "Finishing the current operation. Please retry in a moment.",
            );
            return;
        }
        if (command !== "ready") state.pending = true;
        window.chrome.webview.postMessage({
            command,
            revision: state.doc?.revision || 0,
            ids: state.selected,
            excluded: state.excluded,
            ...data,
        });
    };
    const value = (v) => (typeof v === "string" ? v : JSON.stringify(v));
    const current = () =>
        state.doc?.layers.find((n) => n.id === state.selected[0]);
    const selectedRoots = () => {
        const map = new Map(state.doc.layers.map((n) => [n.id, n]));
        return state.selected.filter((id) => {
            let parent = map.get(id)?.parent;
            while (parent) {
                if (state.selected.includes(parent)) return false;
                parent = map.get(parent)?.parent;
            }
            return true;
        });
    };
    function tree() {
        if (!state.doc) return;
        const children = new Map();
        for (const n of state.doc.layers) {
            const key = n.parent || "";
            if (!children.has(key)) children.set(key, []);
            children.get(key).push(n);
        }
        const visible = [];
        const visit = (parent, depth) => {
            for (const n of children.get(parent) || []) {
                visible.push({ n, depth, children: children.has(n.id) });
                if (!state.collapsed.has(n.id)) visit(n.id, depth + 1);
            }
        };
        visit("", 0);
        const rowHeight = 32 * (state.textScale || 1);
        const spacer = $("#tree-space");
        spacer.style.height = visible.length * rowHeight + "px";
        spacer.replaceChildren();
        const start = Math.max(
                0,
                Math.floor($("#tree").scrollTop / rowHeight) - 5,
            ),
            end = Math.min(
                visible.length,
                start + Math.ceil($("#tree").clientHeight / rowHeight) + 15,
            );
        for (let i = start; i < end; i++) {
            const { n, depth, children: hasChildren } = visible[i],
                row = document.createElement("button");
            row.type = "button";
            row.className =
                "tree-row" +
                (state.changes.some((c) => c.layerId === n.id)
                    ? " changed"
                    : "");
            row.style.top = i * rowHeight + "px";
            row.style.paddingLeft = Math.min(depth * 12, 80) + 5 + "px";
            row.setAttribute("role", "option");
            row.setAttribute(
                "aria-selected",
                String(state.selected.includes(n.id)),
            );
            row.setAttribute(
                "aria-label",
                n.name +
                    (n.locked ? ", locked" : "") +
                    (!n.visible ? ", hidden" : ""),
            );
            const fold = document.createElement("span");
            fold.className = "fold";
            fold.textContent = hasChildren
                ? state.collapsed.has(n.id)
                    ? "▸"
                    : "▾"
                : n.kind === "text"
                  ? "T"
                  : "□";
            const label = document.createElement("span");
            label.className = "name";
            label.textContent = n.name;
            row.append(fold, label);
            row.onclick = (e) => {
                if (e.target === fold && hasChildren) {
                    if (state.collapsed.has(n.id)) state.collapsed.delete(n.id);
                    else state.collapsed.add(n.id);
                    tree();
                    return;
                }
                if (e.ctrlKey || e.metaKey) {
                    state.selected = state.selected.includes(n.id)
                        ? state.selected.filter((id) => id !== n.id)
                        : [...state.selected, n.id];
                } else state.selected = [n.id];
                properties();
                tree();
                selection();
            };
            spacer.append(row);
        }
    }
    function properties() {
        const n = current();
        $("#selected-name").textContent = n
            ? state.selected.length > 1
                ? state.selected.length + " layers"
                : n.name
            : "Select a layer";
        for (const field of document.querySelectorAll("[data-property]")) {
            const key = field.dataset.property;
            field.disabled =
                !n || state.original || (n.locked && key !== "locked");
            if (field.type === "checkbox") field.checked = n?.[key] ?? false;
            else field.value = n?.[key] ?? "";
        }
        const parent = $("#parent");
        parent.replaceChildren();
        const rootOption = document.createElement("option");
        rootOption.value = "";
        rootOption.textContent = "Artboard";
        parent.append(rootOption);
        for (const candidate of state.doc?.layers || []) {
            if (
                candidate.id === n?.id ||
                !["frame", "group"].includes(candidate.kind)
            )
                continue;
            const option = document.createElement("option");
            option.value = candidate.id;
            option.textContent = candidate.name;
            parent.append(option);
        }
        parent.value = n?.parent || "";
        parent.disabled = !n || state.original || n.locked;
        $("#text-field").hidden = n?.kind !== "text";
        $("#coordinates").textContent = n
            ? `Left ${Math.round(n.x)} · Top ${Math.round(n.y)}`
            : "";
        $("#evidence").textContent = n
            ? `${n.source.status} source\n${n.source.selector}\n${n.source.file || "Project file not mapped"}${n.fallback ? "\nImage region: " + n.fallback : ""}`
            : "";
    }
    function changes(container, review = false) {
        container.replaceChildren();
        const groups = new Map();
        for (const c of state.changes) {
            if (!groups.has(c.layerId)) groups.set(c.layerId, []);
            groups.get(c.layerId).push(c);
        }
        if (!groups.size) {
            container.textContent =
                "No requested changes. Original and edited layers match.";
            return;
        }
        for (const [id, list] of groups) {
            const item = document.createElement("div");
            item.className = "change";
            const title = document.createElement("strong");
            title.textContent = list[0].name;
            if (review) {
                const label = document.createElement("label"),
                    check = document.createElement("input");
                check.type = "checkbox";
                check.checked = !state.excluded.includes(id);
                check.onchange = () => {
                    state.excluded = check.checked
                        ? state.excluded.filter((x) => x !== id)
                        : [...state.excluded, id];
                    send("preview");
                };
                label.append(check, title);
                item.append(label);
            } else item.append(title);
            for (const c of list) {
                const p = document.createElement("p"),
                    before = document.createElement("del"),
                    after = document.createElement("span");
                before.textContent = value(c.before).slice(0, 200);
                after.textContent = " → " + value(c.after).slice(0, 200);
                p.append(c.property + ": ", before, after);
                item.append(p);
            }
            if (!review) {
                const select = document.createElement("button");
                select.textContent = "Locate";
                select.onclick = () => {
                    state.selected = [id];
                    properties();
                    tree();
                    selection();
                };
                const revert = document.createElement("button");
                revert.textContent = "Revert";
                revert.onclick = () => send("revert", { ids: [id] });
                item.append(select, revert);
            }
            container.append(item);
        }
    }
    function renderCanvas() {
        if (!state.doc) return;
        const art = $("#artboard");
        art.replaceChildren();
        if (state.original && state.doc.assets[state.doc.original]) {
            const image = document.createElement("img");
            image.src =
                "data:image/png;base64," +
                state.doc.assets[state.doc.original].data;
            image.style.width = state.doc.width + "px";
            image.style.height = state.doc.height + "px";
            image.draggable = false;
            art.append(image);
        } else art.append(SGScene.svg(state.doc));
        art.style.width = state.doc.width + "px";
        art.style.height = state.doc.height + "px";
        art.style.transform = `scale(${state.zoom})`;
        $("#canvas-space").style.width = state.doc.width * state.zoom + "px";
        $("#canvas-space").style.height = state.doc.height * state.zoom + "px";
        $("#dimensions").textContent = `${Math.round(state.doc.width)} × ${Math.round(state.doc.height)} · ${Math.round(state.zoom * 100)}%`;
        selection();
    }
    function selection() {
        const box = $("#selection"),
            handle = $("#resize");
        box.style.display = "none";
        handle.style.display = "none";
        if (state.original || state.selected.length !== 1) return;
        const element = [
            ...$("#artboard").querySelectorAll("[data-layer]"),
        ].find((e) => e.dataset.layer === state.selected[0]);
        if (!element) return;
        const r = element.getBoundingClientRect(),
            p = $("#viewport").getBoundingClientRect();
        Object.assign(box.style, {
            display: "block",
            left: r.left - p.left + $("#viewport").scrollLeft + "px",
            top: r.top - p.top + $("#viewport").scrollTop + "px",
            width: r.width + "px",
            height: r.height + "px",
        });
        if (!current()?.locked)
            Object.assign(handle.style, {
                display: "block",
                left: r.right - p.left + $("#viewport").scrollLeft - 5 + "px",
                top: r.bottom - p.top + $("#viewport").scrollTop - 5 + "px",
            });
    }
    function status(text) {
        $("#status").textContent = text;
    }
    function fit() {
        if (!state.doc) return;
        state.autoFit = true;
        state.zoom = Math.min(
            1,
            Math.max(
                0.001,
                Math.min(
                    ($("#viewport").clientWidth - 48) / state.doc.width,
                    ($("#viewport").clientHeight - 48) / state.doc.height,
                ),
            ),
        );
        renderCanvas();
    }
    function render(message) {
        const d = state.doc;
        $("#name").textContent = d
            ? d.source.title
            : "Import a project page or open a design";
        $("#source").textContent = d?.source.url || "";
        $("#count").textContent = state.changes.length;
        $("#layer-count").textContent = d?.layers.length || 0;
        $("#dimensions").textContent = d
            ? `${Math.round(d.width)} × ${Math.round(d.height)} · ${Math.round(state.zoom * 100)}%`
            : "";
        $("#revision").textContent = d ? "Revision " + d.revision : "";
        $("#limitations").textContent = d?.limitations.join(" ") || "";
        $("#review").disabled = !state.changes.length;
        $("[data-command=undo]").disabled = !message.canUndo;
        $("[data-command=redo]").disabled = !message.canRedo;
        properties();
        tree();
        changes($("#changes"));
        renderCanvas();
        status(message.status || "Ready");
    }
    function reviewVisual(original = false) {
        $("#brief").hidden = true;
        $("#review-visual").hidden = false;
        const d = state.review;
        if (!d) return;
        $("#review-visual").replaceChildren();
        if (original && d.assets[d.original]) {
            const img = document.createElement("img");
            img.src = "data:image/png;base64," + d.assets[d.original].data;
            $("#review-visual").append(img);
        } else $("#review-visual").append(SGScene.svg(d));
    }
    window.chrome.webview.addEventListener("message", (event) => {
        const m = event.data;
        if (m.kind === "idle") {
            state.pending = false;
            return;
        }
        if (m.kind === "state") {
            const first = state.doc?.id !== m.document.id;
            if (first) {
                state.selected = [];
                state.excluded = [];
                state.collapsed.clear();
                state.review = null;
                state.original = false;
                state.autoFit = true;
                $("#compare").textContent = "Original";
                $("#handoff").hidden = true;
                $("#viewport").scrollTo(0, 0);
            }
            state.textScale = m.textScale || 1;
            const assets = m.document.assets || (first ? {} : state.doc?.assets) || {};
            state.doc = { ...m.document, assets };
            state.changes = m.changes;
            state.selected = state.selected.filter((id) =>
                state.doc.layers.some((n) => n.id === id),
            );
            if (!state.selected.length && state.doc.layers.length)
                state.selected = [state.doc.layers[0].id];
            if (m.theme) document.documentElement.style.colorScheme = m.theme;
            document.documentElement.style.setProperty(
                "--ui-scale",
                String(m.textScale || 1),
            );
            render(m);
            if (first) requestAnimationFrame(fit);
        } else if (m.kind === "review") {
            state.review = m.document;
            $("#brief").value = m.brief;
            $("#handoff").hidden = false;
            changes($("#review-changes"), true);
            reviewVisual();
            $("#back").focus();
        } else if (m.kind === "status") {
            status(m.text);
            if (m.error && !$("#handoff").hidden) {
                $("#brief").hidden = false;
                $("#review-visual").hidden = true;
                $("#brief").value = m.text + "\n\n" + $("#brief").value;
            }
        }
    });
    document.addEventListener("click", (event) => {
        const button = event.target.closest("[data-command]");
        if (button) send(button.dataset.command);
    });
    for (const field of document.querySelectorAll("[data-property]"))
        field.addEventListener("change", () => {
            const n = current();
            if (!n) return;
            let v =
                field.type === "checkbox"
                    ? field.checked
                    : field.type === "number"
                      ? Number(field.value)
                      : field.value;
            if (
                !field.checkValidity() ||
                (typeof v === "number" && !Number.isFinite(v))
            ) {
                properties();
                status("Enter a valid property value.");
                return;
            }
            send("edit", {
                edits: [
                    { id: n.id, property: field.dataset.property, value: v },
                ],
            });
        });
    $("#tree").addEventListener("scroll", tree);
    $("#viewport").addEventListener("scroll", selection);
    $("#parent").onchange = () =>
        send("reparent", { parent: $("#parent").value });
    $("#compare").onclick = () => {
        state.original = !state.original;
        $("#compare").textContent = state.original
            ? "Back to edited"
            : "Original";
        renderCanvas();
        properties();
    };
    $("#review").onclick = () => {
        state.excluded = [];
        send("preview");
    };
    $("#back").onclick = () => {
        $("#handoff").hidden = true;
        $("#review").focus();
    };
    $("#review-original").onclick = () => reviewVisual(true);
    $("#review-edited").onclick = () => reviewVisual();
    $("#review-brief").onclick = () => {
        $("#brief").hidden = false;
        $("#review-visual").hidden = true;
    };
    $("#zoom-fit").onclick = fit;
    new ResizeObserver(() => {
        if (state.autoFit) fit();
        else selection();
        tree();
    }).observe($("#viewport"));
    $("#zoom-in").onclick = () => {
        state.autoFit = false;
        state.zoom = Math.min(8, state.zoom * 1.2);
        renderCanvas();
    };
    $("#zoom-out").onclick = () => {
        state.autoFit = false;
        state.zoom = Math.max(0.03, state.zoom / 1.2);
        renderCanvas();
    };
    $("#properties-tab").onclick = () => {
        $("#properties").hidden = false;
        $("#changes").hidden = true;
        $("#properties-tab").setAttribute("aria-pressed", "true");
        $("#changes-tab").setAttribute("aria-pressed", "false");
    };
    $("#changes-tab").onclick = () => {
        $("#properties").hidden = true;
        $("#changes").hidden = false;
        $("#properties-tab").setAttribute("aria-pressed", "false");
        $("#changes-tab").setAttribute("aria-pressed", "true");
    };
    let gesture = null;
    $("#viewport").addEventListener("pointerdown", (event) => {
        if (state.original || !state.doc || state.pending || event.button !== 0)
            return;
        const resize = event.target.id === "resize",
            target = event.target.closest("[data-layer]");
        if (!resize && !target) return;
        const id = resize ? state.selected[0] : target.dataset.layer;
        if (event.ctrlKey || event.metaKey) {
            state.selected = state.selected.includes(id)
                ? state.selected.filter((x) => x !== id)
                : [...state.selected, id];
            properties();
            tree();
            selection();
            return;
        }
        if (!state.selected.includes(id)) state.selected = [id];
        properties();
        tree();
        selection();
        if (
            state.selected.some(
                (x) => state.doc.layers.find((n) => n.id === x)?.locked,
            )
        )
            return;
        gesture = {
            x: event.clientX,
            y: event.clientY,
            resize,
            before: structuredClone(state.doc.layers),
            ids: selectedRoots(),
            changed: false,
        };
        $("#viewport").setPointerCapture(event.pointerId);
        event.preventDefault();
    });
    $("#viewport").addEventListener("pointermove", (event) => {
        if (!gesture) return;
        const dx = (event.clientX - gesture.x) / state.zoom,
            dy = (event.clientY - gesture.y) / state.zoom;
        gesture.changed = Math.abs(dx) > 1 || Math.abs(dy) > 1;
        for (const id of gesture.ids) {
            const n = state.doc.layers.find((n) => n.id === id),
                b = gesture.before.find((n) => n.id === id);
            if (gesture.resize) {
                n.width = Math.max(1, b.width + dx);
                n.height = Math.max(1, b.height + dy);
            } else {
                n.x = b.x + dx;
                n.y = b.y + dy;
            }
        }
        renderCanvas();
    });
    function finish(cancel) {
        if (!gesture) return;
        const g = gesture;
        gesture = null;
        const edits = [];
        if (g.changed && !cancel)
            for (const id of g.ids) {
                const n = state.doc.layers.find((n) => n.id === id);
                for (const p of g.resize ? ["width", "height"] : ["x", "y"])
                    edits.push({
                        id,
                        property: p,
                        value: Math.round(n[p] * 1000) / 1000,
                    });
            }
        state.doc.layers = g.before;
        renderCanvas();
        if (edits.length) send("edit", { edits });
    }
    $("#viewport").addEventListener("pointerup", () => finish(false));
    $("#viewport").addEventListener("pointercancel", () => finish(true));
    $("#artboard").addEventListener("dblclick", (event) => {
        const id = event.target.closest("[data-layer]")?.dataset.layer;
        if (!id || state.original) return;
        state.selected = [id];
        properties();
        if (current()?.kind === "text") {
            $("[data-property=text]").focus();
            $("[data-property=text]").select();
        }
    });
    document.addEventListener("keydown", (event) => {
        if (event.target.matches("input,textarea,select")) return;
        if (event.key === "Escape") {
            finish(true);
            $("#handoff").hidden = true;
            return;
        }
        if (event.ctrlKey || event.metaKey) {
            const key = event.key.toLowerCase();
            if (["z", "y", "s", "d"].includes(key)) {
                event.preventDefault();
                send(
                    key === "z"
                        ? event.shiftKey
                            ? "redo"
                            : "undo"
                        : key === "y"
                          ? "redo"
                          : key === "s"
                            ? "save"
                            : "duplicate",
                );
                return;
            }
        }
        if (event.key === "Delete") {
            event.preventDefault();
            send("delete");
        }
        if (event.key === "Enter" && current()?.kind === "text")
            $("[data-property=text]").focus();
        if (
            ["ArrowLeft", "ArrowRight", "ArrowUp", "ArrowDown"].includes(
                event.key,
            ) &&
            state.selected.length
        ) {
            event.preventDefault();
            const amount = event.shiftKey ? 10 : 1,
                p =
                    event.key === "ArrowLeft" || event.key === "ArrowRight"
                        ? "x"
                        : "y",
                delta =
                    event.key === "ArrowLeft" || event.key === "ArrowUp"
                        ? -amount
                        : amount;
            send("edit", {
                edits: selectedRoots().map((id) => ({
                    id,
                    property: p,
                    value: state.doc.layers.find((n) => n.id === id)[p] + delta,
                })),
            });
        }
    });
    new ResizeObserver(() => selection()).observe($("#viewport"));
    send("ready");
})();
