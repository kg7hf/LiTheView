// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

/* Native pixels and editing adornments only. VS Code owns the document. */
(function () {
    'use strict';

    function validFrame(frame) {
        return frame && ['x', 'y', 'width', 'height'].every(key => Number.isSafeInteger(frame[key]))
            && frame.width > 0 && frame.height > 0;
    }

    function absoluteFrame(component, components) {
        if (!component || !validFrame(component.frame)) return null;
        const frame = { ...component.frame };
        const visited = new Set([component.id]);
        let parentId = component.parentId;
        while (parentId) {
            if (visited.has(parentId)) return null;
            visited.add(parentId);
            const parent = components.find(candidate => candidate.id === parentId);
            if (!parent || !validFrame(parent.frame)) return null;
            frame.x += parent.frame.x;
            frame.y += parent.frame.y;
            parentId = parent.parentId;
        }
        return frame;
    }

    function hierarchyEntries(components) {
        const result = [];
        const visited = new Set();
        function visit(component, depth) {
            if (visited.has(component.id)) return;
            visited.add(component.id);
            result.push({ component, depth });
            for (const child of components.filter(candidate => candidate.parentId === component.id)) {
                visit(child, depth + 1);
            }
        }
        for (const root of components.filter(component => !component.parentId)) visit(root, 0);
        // Malformed or orphaned nodes remain reachable for inspection and repair.
        for (const component of components) visit(component, 0);
        return result;
    }

    // Pages of a tab view other than the selected one are hidden by the runtime, so the canvas
    // neither hit-tests them nor anything inside them.
    function hitTest(components, x, y) {
        const ordered = hierarchyEntries(components);
        for (let index = ordered.length - 1; index >= 0; --index) {
            const component = ordered[index].component;
            if (component.previewHidden) continue;
            const frame = absoluteFrame(component, components);
            if (frame && x >= frame.x && y >= frame.y && x < frame.x + frame.width && y < frame.y + frame.height) {
                return component;
            }
        }
        return null;
    }

    function gestureFrame(frame, kind, deltaX, deltaY) {
        const dx = Math.round(deltaX);
        const dy = Math.round(deltaY);
        return kind === 'resize'
            ? { ...frame, width: Math.max(1, frame.width + dx), height: Math.max(1, frame.height + dy) }
            : { ...frame, x: frame.x + dx, y: frame.y + dy };
    }

    function sameFrame(left, right) {
        return ['x', 'y', 'width', 'height'].every(key => left[key] === right[key]);
    }

    function editMessage(documentVersion, expectedVersion, operation) {
        if (!Number.isInteger(documentVersion) || documentVersion !== expectedVersion) return null;
        if (operation.kind === 'frame' && !validFrame(operation.frame)) return null;
        if ((operation.kind === 'property' || operation.kind === 'reset') && typeof operation.propertyId !== 'string') return null;
        if (operation.kind === 'property' && (operation.value === undefined || operation.value === null)) return null;
        if (operation.kind === 'frames' && (!operation.frames || typeof operation.frames !== 'object' ||
            Object.keys(operation.frames).length === 0 || !Object.values(operation.frames).every(validFrame))) return null;
        if (operation.kind === 'rename' && (typeof operation.name !== 'string' || operation.name.trim() === '')) return null;
        return { type: 'edit', documentVersion, operation };
    }

    // Inspector groups follow the schema's property categories in a stable order.
    const categoryOrder = ['Behavior', 'Content', 'Appearance', 'Layout', 'Resources', 'Actions and bindings'];
    const choiceKinds = { color: 'themeColor', bitmap: 'bitmap', font: 'font', actionId: 'action', bindingId: 'binding' };
    const integerKinds = new Set(['signedInteger', 'unsignedInteger', 'duration']);

    // Legacy XML colors keep the historical byte order: low byte red, then green, blue, and an
    // inverted alpha in the high byte. "#00bbggrr" is therefore opaque.
    function legacyColorParts(text) {
        const match = /^(?:#|0x)?([0-9a-f]{8})$/i.exec(String(text ?? '').trim());
        if (!match) return null;
        const value = Number.parseInt(match[1], 16);
        return { digits: match[1].toLowerCase(), red: value & 0xff, green: (value >>> 8) & 0xff,
            blue: (value >>> 16) & 0xff, alpha: 255 - ((value >>> 24) & 0xff) };
    }

    function legacyColorToHex(text) {
        const color = legacyColorParts(text);
        return color ? '#' + [color.red, color.green, color.blue].map(part => part.toString(16).padStart(2, '0')).join('') : null;
    }

    function hexToLegacyColor(hex) {
        const match = /^#([0-9a-f]{6})$/i.exec(String(hex ?? ''));
        if (!match) return null;
        const rgb = Number.parseInt(match[1], 16);
        const bytes = [rgb & 0xff, (rgb >>> 8) & 0xff, (rgb >>> 16) & 0xff];
        return '#00' + bytes.map(part => part.toString(16).padStart(2, '0')).join('');
    }

    /** Converts an editor control value to the JSON value of a "property" edit, or null. */
    function propertyValue(descriptor, raw) {
        const kind = descriptor?.kind;
        if (kind === 'boolean') return typeof raw === 'boolean' ? raw : null;
        if (integerKinds.has(kind)) {
            if (typeof raw === 'string' && raw.trim() === '') return null;
            const value = Number(raw);
            if (!Number.isSafeInteger(value) || (kind !== 'signedInteger' && value < 0)) return null;
            if (Number.isFinite(descriptor.minimum) && value < descriptor.minimum) return null;
            if (Number.isFinite(descriptor.maximum) && value > descriptor.maximum) return null;
            return value;
        }
        if (kind === 'enum') return Array.isArray(descriptor.enumValues) && descriptor.enumValues.includes(raw) ? raw : null;
        if (kind === 'color') {
            const text = String(raw ?? '').trim();
            if (/^theme\.[a-z0-9]+(?:[.-][a-z0-9]+)*$/.test(text)) return text;
            const color = legacyColorParts(text);
            return color ? '#' + color.digits : null;
        }
        if (kind === 'text' || kind === 'boundedText') return typeof raw === 'string' ? raw : null;
        if (choiceKinds[kind]) return typeof raw === 'string' && raw !== '' ? raw : null;
        return null;
    }

    /** One row per schema property of the component's widget; frame and text have their own forms. */
    function inspectorRows(state, component) {
        const widget = (Array.isArray(state?.widgets) ? state.widgets : []).find(item => item.id === component?.widgetId);
        if (!widget || !Array.isArray(widget.properties)) return [];
        const descriptors = new Map((Array.isArray(state.properties) ? state.properties : []).map(item => [item.id, item]));
        const values = new Map((Array.isArray(component.properties) ? component.properties : []).map(item => [item.id, item.value]));
        const rank = descriptor => {
            const index = categoryOrder.indexOf(descriptor.category);
            return index < 0 ? categoryOrder.length : index;
        };
        return widget.properties
            .filter(id => id !== 'frame' && id !== 'text' && descriptors.has(id))
            .map((id, order) => ({ descriptor: descriptors.get(id), explicit: values.has(id),
                value: values.has(id) ? values.get(id) : descriptors.get(id).defaultValue, order }))
            .sort((left, right) => rank(left.descriptor) - rank(right.descriptor) || left.order - right.order);
    }

    function choicesFor(state, descriptor) {
        const kind = choiceKinds[descriptor?.kind];
        return kind ? (Array.isArray(state?.choices) ? state.choices : []).filter(choice => choice.kind === kind) : [];
    }

    // The palette adds into the selected container when the widget may live there (a page into a
    // tab view, a control into a panel), otherwise into the form root. The initial frame is left to
    // the shared editing session: the schema's default size centered in the parent, or a tab view's
    // content area for a page.
    function paletteOperation(components, widgets, selectedId, widget) {
        const root = components.find(component => !component.parentId) || null;
        if (!root || !widget || !validFrame(root.frame)) return null;
        const allowed = Array.isArray(widget.allowedParentIds) ? widget.allowedParentIds : [];
        const target = components.find(component => component.id === selectedId) || null;
        const targetWidget = target && (Array.isArray(widgets) ? widgets : []).find(candidate => candidate.id === target.widgetId);
        const nested = target && target !== root && !target.previewHidden && targetWidget?.container &&
            allowed.includes(target.widgetId);
        return { kind: 'add', parentId: nested ? target.id : root.id, widgetId: widget.id };
    }

    function isDescendant(components, componentId, ancestorId) {
        const visited = new Set();
        for (let current = components.find(item => item.id === componentId); current && !visited.has(current.id);
            current = components.find(item => item.id === current.parentId)) {
            visited.add(current.id);
            if (current.id === ancestorId) return true;
        }
        return false;
    }

    /** Containers the component may move into, in hierarchy order; its current parent is included. */
    function reparentTargets(components, widgets, componentId) {
        const component = components.find(item => item.id === componentId);
        const widget = (Array.isArray(widgets) ? widgets : []).find(item => item.id === component?.widgetId);
        if (!component || !component.parentId || !widget) return [];
        const allowed = Array.isArray(widget.allowedParentIds) ? widget.allowedParentIds : [];
        const containers = new Set((Array.isArray(widgets) ? widgets : []).filter(item => item.container).map(item => item.id));
        return hierarchyEntries(components).map(entry => entry.component).filter(candidate =>
            containers.has(candidate.widgetId) && allowed.includes(candidate.widgetId) &&
            !isDescendant(components, candidate.id, componentId));
    }

    /** Mirrors the designer's C++ symbol rule: "start pump" and "startPump" become "StartPump". */
    function cppIdentifier(value) {
        let result = '';
        let capitalize = true;
        for (const character of String(value ?? '')) {
            const letter = /[A-Za-z]/.test(character);
            if (!letter && !/[0-9]/.test(character)) { capitalize = true; continue; }
            const output = capitalize && letter ? character.toUpperCase() : character;
            if (result === '' && /[0-9]/.test(output)) result = 'N';
            result += output;
            capitalize = false;
        }
        return result || 'Unnamed';
    }

    /** "StartPump" becomes "action.start-pump"; null when the symbol has no letters or digits. */
    function actionIdFromSymbol(symbol) {
        const words = String(symbol ?? '').replace(/([a-z0-9])([A-Z])/g, '$1 $2').toLowerCase().match(/[a-z0-9]+/g);
        if (!words) return null;
        const id = 'action.' + words.join('-');
        return /^action\.[a-z]/.test(id) ? id : null;
    }

    /** The smallest runtime ID above every declared action and component runtime ID, or null when full. */
    function nextActionRuntimeId(state) {
        let highest = 0;
        for (const choice of Array.isArray(state?.choices) ? state.choices : []) {
            if (choice.kind === 'action' && Number.isInteger(choice.runtimeId)) highest = Math.max(highest, choice.runtimeId);
        }
        for (const component of Array.isArray(state?.components) ? state.components : []) {
            const runtime = (Array.isArray(component.properties) ? component.properties : []).find(item => item.id === 'runtime-id');
            if (runtime && Number.isInteger(runtime.value)) highest = Math.max(highest, runtime.value);
        }
        return highest < 65535 ? highest + 1 : null;
    }

    /** Adds or removes one component from a multi-selection; the most recent addition is primary (last). */
    function toggleSelection(selectedIds, componentId) {
        const ids = Array.isArray(selectedIds) ? selectedIds.filter(id => id !== componentId) : [];
        return ids.length === (Array.isArray(selectedIds) ? selectedIds.length : 0) ? [...ids, componentId] : ids;
    }

    const alignmentModes = ['left', 'center', 'right', 'top', 'middle', 'bottom',
        'distributeHorizontal', 'distributeVertical', 'sameWidth', 'sameHeight', 'sameSize'];

    /**
     * Parent-relative frames for aligning siblings to the anchor (the primary selection), distributing
     * them evenly between the outermost two, or matching the anchor's size. Only changed frames are
     * returned; null when the selection is not two or more siblings (three for distribution).
     */
    function arrangeFrames(components, selectedIds, anchorId, mode) {
        const chosen = (Array.isArray(selectedIds) ? selectedIds : [])
            .map(id => components.find(item => item.id === id)).filter(Boolean);
        const anchor = chosen.find(item => item.id === anchorId);
        if (!anchor || chosen.length < 2 || !alignmentModes.includes(mode) ||
            chosen.some(item => !item.parentId || item.parentId !== anchor.parentId || !validFrame(item.frame))) return null;
        const distribute = mode === 'distributeHorizontal' || mode === 'distributeVertical';
        if (distribute && chosen.length < 3) return null;
        const a = anchor.frame;
        const result = {};
        const place = (item, frame) => { if (!sameFrame(item.frame, frame)) result[item.id] = frame; };
        if (distribute) {
            const horizontal = mode === 'distributeHorizontal';
            const [position, extent] = horizontal ? ['x', 'width'] : ['y', 'height'];
            const ordered = [...chosen].sort((left, right) => left.frame[position] - right.frame[position] || (left.id < right.id ? -1 : 1));
            const first = ordered[0].frame[position];
            const last = ordered[ordered.length - 1];
            const span = last.frame[position] + last.frame[extent] - first;
            const occupied = ordered.reduce((total, item) => total + item.frame[extent], 0);
            const gap = (span - occupied) / (ordered.length - 1);
            let cursor = first;
            for (const item of ordered) {
                place(item, { ...item.frame, [position]: Math.round(cursor) });
                cursor += item.frame[extent] + gap;
            }
            return result;
        }
        for (const item of chosen) {
            const f = item.frame;
            const frame = {
                left: { ...f, x: a.x },
                center: { ...f, x: a.x + Math.round((a.width - f.width) / 2) },
                right: { ...f, x: a.x + a.width - f.width },
                top: { ...f, y: a.y },
                middle: { ...f, y: a.y + Math.round((a.height - f.height) / 2) },
                bottom: { ...f, y: a.y + a.height - f.height },
                sameWidth: { ...f, width: a.width },
                sameHeight: { ...f, height: a.height },
                sameSize: { ...f, width: a.width, height: a.height }
            }[mode];
            place(item, frame);
        }
        return result;
    }

    /** Parent-relative frames that move every selected non-root component by the same offset. */
    function movedFrames(components, selectedIds, dx, dy) {
        const result = {};
        for (const id of Array.isArray(selectedIds) ? selectedIds : []) {
            const component = components.find(item => item.id === id);
            if (component && component.parentId && validFrame(component.frame)) {
                result[id] = { ...component.frame, x: component.frame.x + Math.round(dx), y: component.frame.y + Math.round(dy) };
            }
        }
        return result;
    }

    const helpers = { validFrame, absoluteFrame, hierarchyEntries, hitTest, gestureFrame, sameFrame, editMessage,
        legacyColorParts, legacyColorToHex, hexToLegacyColor, propertyValue, inspectorRows, choicesFor,
        paletteOperation, reparentTargets, cppIdentifier, actionIdFromSymbol, nextActionRuntimeId, toggleSelection,
        alignmentModes, arrangeFrames, movedFrames };
    if (typeof module !== 'undefined' && module.exports) module.exports = helpers;
    if (typeof document === 'undefined' || typeof acquireVsCodeApi !== 'function') return;

    const vscode = acquireVsCodeApi();
    const saved = vscode.getState() || {};
    const byId = id => document.getElementById(id);
    const stage = byId('canvas-stage');
    const canvas = byId('native-canvas');
    const selection = byId('selection');
    const context = canvas.getContext('2d', { alpha: true });
    const fieldIds = { x: 'frame-x', y: 'frame-y', width: 'frame-width', height: 'frame-height' };
    const zoomValues = [0.25, 0.5, 0.75, 1, 1.5, 2, 3, 4];
    let zoom = zoomValues.includes(saved.zoom) ? saved.zoom : 1;
    let state = null;
    let selectedId = typeof saved.selectedId === 'string' ? saved.selectedId : null;
    // Every selected component; the primary selection (selectedId) is the last entry.
    let selectedIds = selectedId ? [selectedId] : [];
    let newActionFor = null;
    let frameVersion = null;
    let busy = false;
    let pendingEdit = false;
    let gesture = null;
    let frameDiagnostics = [];
    let localError = null;
    byId('zoom').value = String(zoom);

    const components = () => state && Array.isArray(state.components) ? state.components : [];
    const selected = () => components().find(component => component.id === selectedId) || null;
    const editable = () => state && Number.isInteger(state.documentVersion) && !busy && !pendingEdit;
    const canvasEditable = () => editable() && frameVersion === state.documentVersion;
    const isRoot = component => component && !component.parentId;
    function remember() { vscode.setState({ selectedId, zoom }); }
    function announce(message) { byId('announcement').textContent = message; }

    function setStatus(message, error = false) {
        byId('render-status').textContent = message;
        byId('render-status').classList.toggle('error', error);
    }

    function reportError(message) {
        localError = { severity: 'error', code: '', message: String(message) };
        setStatus(String(message), true);
        renderDiagnostics();
        announce(String(message));
    }

    function renderDiagnostics() {
        const list = byId('diagnostics');
        list.replaceChildren();
        const diagnostics = [...(Array.isArray(state?.diagnostics) ? state.diagnostics : []), ...frameDiagnostics];
        if (localError) diagnostics.push(localError);
        const seen = new Set();
        for (const diagnostic of diagnostics) {
            const key = JSON.stringify([diagnostic.code, diagnostic.message, diagnostic.path]);
            if (seen.has(key)) continue;
            seen.add(key);
            const item = document.createElement('li');
            const severity = String(diagnostic.severity || '').toLowerCase();
            if (severity.includes('error')) item.className = 'diagnostic-error';
            else if (severity.includes('warn')) item.className = 'diagnostic-warning';
            const code = document.createElement('span');
            code.className = 'diagnostic-code';
            code.textContent = String(diagnostic.code || severity);
            item.append(code, document.createTextNode(String(diagnostic.message || '')));
            if (diagnostic.path) item.title = String(diagnostic.path);
            list.append(item);
        }
        byId('diagnostic-count').textContent = String(seen.size);
        byId('diagnostics-section').hidden = seen.size === 0;
    }

    function rootComponent() { return components().find(component => !component.parentId) || null; }

    function renderPalette() {
        const palette = byId('palette');
        const filter = byId('widget-filter').value.trim().toLocaleLowerCase();
        palette.replaceChildren();
        for (const widget of Array.isArray(state?.widgets) ? state.widgets : []) {
            const name = String(widget.name || widget.id);
            if (!name.toLocaleLowerCase().includes(filter)) continue;
            const button = document.createElement('button');
            button.className = 'secondary';
            button.type = 'button';
            button.textContent = name;
            button.title = `Add ${name} to the selected container, or to the form`;
            button.setAttribute('aria-label', `Add ${name} to the selected container or the form`);
            button.disabled = !editable() || !rootComponent();
            button.addEventListener('click', () => {
                const operation = paletteOperation(components(), state?.widgets, selectedId, widget);
                if (operation && editable()) commit(operation);
            });
            palette.append(button);
        }
    }

    function renderHierarchy() {
        const list = byId('hierarchy');
        list.replaceChildren();
        for (const { component, depth } of hierarchyEntries(components())) {
            const item = document.createElement('li');
            const button = document.createElement('button');
            button.type = 'button';
            button.style.paddingLeft = `${8 + depth * 12}px`;
            button.setAttribute('aria-current', String(component.id === selectedId));
            button.append(document.createTextNode(String(component.name || component.id)));
            const kind = document.createElement('span');
            kind.className = 'widget-kind';
            kind.textContent = String(component.widgetId || 'Form') + (component.previewHidden ? ' · hidden page' : '');
            button.append(kind);
            button.classList.toggle('preview-hidden', Boolean(component.previewHidden));
            button.classList.toggle('multi-selected', selectedIds.includes(component.id));
            button.title = String(component.id);
            button.addEventListener('click', event => {
                if (event.ctrlKey || event.metaKey || event.shiftKey) toggleSelect(component.id);
                else select(component.id);
            });
            item.append(button);
            list.append(item);
        }
        byId('component-count').textContent = String(components().length);
    }

    // The on-canvas frame of a component, including the preview of a gesture in progress.
    function overlayFrame(component) {
        let frame = absoluteFrame(component, components());
        if (!frame || !gesture) return frame;
        if (gesture.kind === 'group' && gesture.ids.includes(component.id)) {
            return { ...frame, x: frame.x + gesture.dx, y: frame.y + gesture.dy };
        }
        if (gesture.kind !== 'group' && gesture.componentId === component.id) {
            frame = { ...frame,
                x: frame.x + gesture.preview.x - component.frame.x,
                y: frame.y + gesture.preview.y - component.frame.y,
                width: gesture.preview.width, height: gesture.preview.height };
        }
        return frame;
    }

    function renderSecondarySelections() {
        for (const outline of stage.querySelectorAll('.selection.secondary')) outline.remove();
        for (const id of selectedIds) {
            if (id === selectedId) continue;
            const component = components().find(item => item.id === id);
            const frame = component && overlayFrame(component);
            if (!frame) continue;
            const outline = document.createElement('div');
            outline.className = 'selection secondary';
            outline.style.left = `${frame.x * zoom}px`;
            outline.style.top = `${frame.y * zoom}px`;
            outline.style.width = `${frame.width * zoom}px`;
            outline.style.height = `${frame.height * zoom}px`;
            stage.append(outline);
        }
    }

    function renderSelection() {
        const component = selected();
        const frame = component ? overlayFrame(component) : null;
        renderSecondarySelections();
        selection.hidden = !frame;
        if (!frame) {
            byId('selection-status').textContent = 'No selection';
            return;
        }
        selection.classList.toggle('preview-hidden', Boolean(component.previewHidden));
        selection.style.left = `${frame.x * zoom}px`;
        selection.style.top = `${frame.y * zoom}px`;
        selection.style.width = `${frame.width * zoom}px`;
        selection.style.height = `${frame.height * zoom}px`;
        byId('selection-label').textContent = String(component.name || component.id);
        byId('selection-status').textContent = (selectedIds.length > 1 ? `${selectedIds.length} selected · ` : '') +
            `${component.name || component.id} · ${frame.width} × ${frame.height} px` +
            (component.previewHidden ? ' · on a hidden page; change the tab view\'s Selected Index to show it' : '');
        byId('resize-handle').disabled = !canvasEditable();
    }

    function renderInspector() {
        const component = selected();
        byId('inspector-empty').hidden = Boolean(component);
        byId('inspector-content').hidden = !component;
        if (!component) return;
        byId('component-name').textContent = String(component.name || component.id);
        byId('component-kind').textContent = String(component.widgetId || 'Form');
        for (const [key, id] of Object.entries(fieldIds)) byId(id).value = String(component.frame[key]);
        const hasText = typeof component.text === 'string';
        byId('text-form').hidden = !hasText;
        byId('component-text').value = hasText ? component.text : '';
        byId('root-hint').hidden = !isRoot(component);
        renderComponentFields(component);
        renderSelectionFields();
        renderActions(component);
        renderProperties(component);
        refreshControls();
    }

    const widgetFor = component => (Array.isArray(state?.widgets) ? state.widgets : []).find(item => item.id === component?.widgetId);

    function renderComponentFields(component) {
        const root = isRoot(component);
        byId('component-name-input').value = String(component.name || '');
        byId('parent-row').hidden = root;
        byId('order-row').hidden = root;
        byId('duplicate-component').hidden = root;
        const parent = byId('component-parent');
        parent.replaceChildren();
        if (root) return;
        const targets = reparentTargets(components(), state?.widgets, component.id);
        if (!targets.some(item => item.id === component.parentId)) {
            const current = components().find(item => item.id === component.parentId);
            if (current) targets.unshift(current);
        }
        for (const target of targets) {
            const option = document.createElement('option');
            option.value = target.id;
            option.textContent = `${target.name || target.id}${target.previewHidden ? ' (hidden page)' : ''}`;
            option.selected = target.id === component.parentId;
            parent.append(option);
        }
    }

    const arrangeLabels = { left: 'Left', center: 'Center', right: 'Right', top: 'Top', middle: 'Middle', bottom: 'Bottom',
        distributeHorizontal: 'Space H', distributeVertical: 'Space V', sameWidth: 'Same W', sameHeight: 'Same H', sameSize: 'Same size' };
    const arrangeTitles = { distributeHorizontal: 'Distribute horizontally (three or more)',
        distributeVertical: 'Distribute vertically (three or more)', sameWidth: 'Same width as the last selected',
        sameHeight: 'Same height as the last selected', sameSize: 'Same size as the last selected' };

    function renderSelectionFields() {
        const many = selectedIds.length > 1;
        byId('selection-fields').hidden = !many;
        if (!many) return;
        byId('selection-count').textContent = String(selectedIds.length);
        const buttons = byId('arrange-buttons');
        buttons.replaceChildren();
        for (const mode of alignmentModes) {
            const button = document.createElement('button');
            button.type = 'button';
            button.className = 'secondary';
            button.textContent = arrangeLabels[mode];
            button.title = arrangeTitles[mode] || `Align ${arrangeLabels[mode].toLowerCase()} edges to the last selected`;
            const frames = arrangeFrames(components(), selectedIds, selectedId, mode);
            button.dataset.available = String(Boolean(frames && Object.keys(frames).length > 0));
            button.addEventListener('click', () => {
                const next = arrangeFrames(components(), selectedIds, selectedId, mode);
                if (next && Object.keys(next).length > 0) commit({ kind: 'frames', frames: next });
            });
            buttons.append(button);
        }
    }

    function renderActions(component) {
        const container = byId('action-groups');
        container.replaceChildren();
        const events = widgetFor(component)?.events || [];
        if (events.length > 0) {
            const group = document.createElement('fieldset');
            group.className = 'action-group';
            const legend = document.createElement('legend');
            legend.textContent = 'Events';
            group.append(legend);
            for (const event of events) {
                const handler = document.createElement('button');
                handler.type = 'button';
                handler.className = 'secondary';
                handler.textContent = `Create/Open ${event.name} handler`;
                handler.addEventListener('click', () => vscode.postMessage({
                    type: 'handler', componentId: component.id, eventId: event.id,
                    documentVersion: state.documentVersion
                }));
                group.append(handler);
            }
            container.append(group);
        }
        const policy = widgetFor(component)?.eventPolicy;
        if (policy) {
            const help = document.createElement('p');
            help.className = 'muted';
            help.textContent = policy;
            container.append(help);
        }
        const widgetActions = Array.isArray(widgetFor(component)?.actions) ? widgetFor(component).actions : [];
        if (widgetActions.length === 0) return;
        const group = document.createElement('fieldset');
        group.className = 'action-group';
        const legend = document.createElement('legend');
        legend.textContent = 'Actions';
        group.append(legend);
        const assignments = new Map((Array.isArray(component.actions) ? component.actions : [])
            .map(item => [item.widgetActionId, item.actionId || '']));
        const declared = choicesFor(state, { kind: 'actionId' });
        for (const widgetAction of widgetActions) {
            const row = document.createElement('div');
            row.className = 'property-row explicit';
            const controlId = `action-${widgetAction.id}`;
            const label = document.createElement('label');
            label.htmlFor = controlId;
            label.textContent = String(widgetAction.name || widgetAction.id);
            const select = document.createElement('select');
            select.id = controlId;
            const options = [['', '(unassigned)'], ...declared.map(choice => [choice.id, choice.name]), ['__new__', 'New action…']];
            for (const [value, text] of options) {
                const option = document.createElement('option');
                option.value = value;
                option.textContent = String(text);
                select.append(option);
            }
            select.value = newActionFor === widgetAction.id ? '__new__' : assignments.get(widgetAction.id) || '';
            select.addEventListener('change', () => {
                if (select.value === '__new__') { newActionFor = widgetAction.id; renderInspector(); return; }
                newActionFor = null;
                commit({ kind: 'action', componentId: component.id, widgetActionId: widgetAction.id, actionId: select.value || null });
            });
            const control = document.createElement('div');
            control.className = 'property-control';
            control.append(select);
            row.append(label, control);
            if (newActionFor === widgetAction.id) row.append(newActionForm(component, widgetAction));
            group.append(row);
        }
        container.append(group);
    }

    function newActionForm(component, widgetAction) {
        const form = document.createElement('div');
        form.className = 'new-action';
        const symbol = document.createElement('input');
        symbol.type = 'text';
        symbol.placeholder = 'Symbol, e.g. StartPump';
        symbol.setAttribute('aria-label', 'New action C++ symbol');
        symbol.spellcheck = false;
        const runtime = document.createElement('input');
        runtime.type = 'number';
        runtime.min = '1';
        runtime.max = '65535';
        runtime.step = '1';
        runtime.value = String(nextActionRuntimeId(state) ?? '');
        runtime.title = 'Runtime ID reported to onAction()';
        runtime.setAttribute('aria-label', 'New action runtime ID');
        const create = document.createElement('button');
        create.type = 'button';
        create.className = 'secondary';
        create.textContent = 'Create and assign';
        create.addEventListener('click', () => {
            const name = cppIdentifier(symbol.value);
            const id = actionIdFromSymbol(name);
            const runtimeId = runtime.valueAsNumber;
            if (!symbol.value.trim() || !id || !Number.isInteger(runtimeId) || runtimeId < 1 || runtimeId > 65535) {
                reportError('A new action needs a symbol and a runtime ID from 1 through 65535.');
                return;
            }
            newActionFor = null;
            commit({ kind: 'action', componentId: component.id, widgetActionId: widgetAction.id,
                newAction: { id, runtimeId, symbol: name } });
        });
        form.append(symbol, runtime, create);
        return form;
    }

    function renderThemeColors() {
        const list = byId('theme-colors');
        list.replaceChildren();
        const tokens = choicesFor(state, { kind: 'color' });
        byId('theme-count').textContent = `${tokens.length}/32`;
        for (const token of tokens) {
            const item = document.createElement('li');
            const swatch = document.createElement('input');
            swatch.type = 'color';
            swatch.value = legacyColorToHex(token.color) || '#000000';
            swatch.title = `${token.id} ${token.color || ''}`;
            swatch.setAttribute('aria-label', `Color of ${token.id}`);
            swatch.addEventListener('change', () =>
                commit({ kind: 'themeColor', tokenId: token.id, color: hexToLegacyColor(swatch.value) }));
            const name = document.createElement('input');
            name.type = 'text';
            name.value = token.id;
            name.title = token.id;
            name.spellcheck = false;
            name.setAttribute('aria-label', `Rename ${token.id}`);
            name.addEventListener('change', () => {
                const next = name.value.trim();
                if (next && next !== token.id) commit({ kind: 'renameThemeColor', tokenId: token.id, newTokenId: next });
                else name.value = token.id;
            });
            const remove = document.createElement('button');
            remove.type = 'button';
            remove.className = 'secondary';
            remove.textContent = '×';
            remove.title = `Delete ${token.id} (only when unused)`;
            remove.setAttribute('aria-label', `Delete ${token.id}`);
            remove.addEventListener('click', () => commit({ kind: 'deleteThemeColor', tokenId: token.id }));
            item.append(swatch, name, remove);
            list.append(item);
        }
    }

    function renderProperties(component) {
        const container = byId('property-groups');
        container.replaceChildren();
        byId('theme-tokens').replaceChildren(...choicesFor(state, { kind: 'color' }).map(choice => {
            const option = document.createElement('option');
            option.value = choice.id;
            return option;
        }));
        let category = null;
        let group = null;
        for (const row of inspectorRows(state, component)) {
            if (row.descriptor.category !== category) {
                category = row.descriptor.category;
                group = document.createElement('fieldset');
                group.className = 'property-group';
                const legend = document.createElement('legend');
                legend.textContent = String(category);
                group.append(legend);
                container.append(group);
            }
            group.append(propertyRow(component, row));
        }
    }

    function commitProperty(component, descriptor, raw) {
        const value = propertyValue(descriptor, raw);
        if (value === null) {
            reportError(`${descriptor.name} needs a valid ${descriptor.kind} value.`);
            renderInspector();
            return;
        }
        commit({ kind: 'property', componentId: component.id, propertyId: descriptor.id, value });
    }

    function resetProperty(component, descriptor) {
        commit({ kind: 'reset', componentId: component.id, propertyId: descriptor.id });
    }

    function propertyRow(component, row) {
        const { descriptor } = row;
        const controlId = `property-${descriptor.id}`;
        const wrapper = document.createElement('div');
        wrapper.className = row.explicit ? 'property-row explicit' : 'property-row';
        const label = document.createElement('label');
        label.htmlFor = controlId;
        label.textContent = descriptor.units ? `${descriptor.name} (${descriptor.units})` : String(descriptor.name);
        label.title = String(descriptor.documentation || '');
        const control = document.createElement('div');
        control.className = 'property-control';
        control.append(...propertyInputs(component, row, controlId));
        wrapper.append(label, control);
        if (row.explicit && !descriptor.required && descriptor.editable) {
            const reset = document.createElement('button');
            reset.type = 'button';
            reset.className = 'secondary reset-property';
            reset.textContent = 'Reset';
            reset.title = `Use the default ${descriptor.name}`;
            reset.setAttribute('aria-label', `Reset ${descriptor.name} to its default`);
            reset.addEventListener('click', () => resetProperty(component, descriptor));
            control.append(reset);
        }
        return wrapper;
    }

    function propertyInputs(component, row, controlId) {
        const { descriptor } = row;
        const shown = row.value === null || row.value === undefined ? '' : row.value;
        if (!descriptor.editable) {
            const text = document.createElement('span');
            text.id = controlId;
            text.className = 'hint';
            text.textContent = Array.isArray(shown) ? shown.join(', ') : String(shown) || 'Edit in source';
            return [text];
        }
        if (descriptor.kind === 'boolean') {
            const input = document.createElement('input');
            input.id = controlId;
            input.type = 'checkbox';
            input.checked = shown === true;
            input.addEventListener('change', () => commitProperty(component, descriptor, input.checked));
            return [input];
        }
        if (descriptor.kind === 'enum' || choiceKinds[descriptor.kind] && descriptor.kind !== 'color') {
            const select = document.createElement('select');
            select.id = controlId;
            const options = descriptor.kind === 'enum'
                ? descriptor.enumValues.map(value => [value, value])
                : [['', descriptor.required ? 'Choose…' : '(none)'], ...choicesFor(state, descriptor).map(choice => [choice.id, choice.name])];
            for (const [value, text] of options) {
                const option = document.createElement('option');
                option.value = value;
                option.textContent = text;
                select.append(option);
            }
            select.value = String(shown);
            select.addEventListener('change', () => {
                if (select.value === '' && descriptor.kind !== 'enum') resetProperty(component, descriptor);
                else commitProperty(component, descriptor, select.value);
            });
            return [select];
        }
        const input = document.createElement('input');
        input.id = controlId;
        if (integerKinds.has(descriptor.kind)) {
            input.type = 'number';
            input.step = '1';
            if (Number.isFinite(descriptor.minimum)) input.min = String(descriptor.minimum);
            if (Number.isFinite(descriptor.maximum)) input.max = String(descriptor.maximum);
        } else input.type = 'text';
        // Defaults are shown as placeholders so an untouched property stays absent from the XML.
        input.value = row.explicit ? String(shown) : '';
        input.placeholder = row.explicit || shown === '' ? 'Default' : String(shown);
        if (descriptor.kind === 'color') {
            input.setAttribute('list', 'theme-tokens');
            input.placeholder = 'Default';
        }
        input.addEventListener('change', () => {
            if (input.value === '' && row.explicit && !descriptor.required && descriptor.kind !== 'text' && descriptor.kind !== 'boundedText') {
                resetProperty(component, descriptor);
            } else commitProperty(component, descriptor, input.value);
        });
        if (descriptor.kind !== 'color') return [input];
        const picker = document.createElement('input');
        picker.type = 'color';
        picker.title = `Pick an opaque ${descriptor.name}`;
        picker.setAttribute('aria-label', `Pick an opaque ${descriptor.name}`);
        const token = choicesFor(state, descriptor).find(choice => choice.id === shown);
        picker.value = legacyColorToHex(token ? token.color : row.explicit ? shown : '') || '#000000';
        // An unset color inherits a widget- or state-dependent default; a dimmed swatch avoids implying black.
        if (!row.explicit) picker.classList.add('implicit');
        picker.addEventListener('change', () => commitProperty(component, descriptor, hexToLegacyColor(picker.value)));
        return [picker, input];
    }

    function refreshControls() {
        const canEdit = Boolean(editable());
        byId('generate').disabled = !canEdit;
        byId('setup').disabled = !canEdit;
        byId('frame-fields').disabled = !canEdit || !selected();
        byId('text-fields').disabled = !canEdit || !selected();
        for (const group of byId('property-groups').querySelectorAll('fieldset')) group.disabled = !canEdit || !selected();
        byId('delete-component').disabled = !canEdit || !selected() || isRoot(selected());
        byId('component-fields').disabled = !canEdit || !selected();
        byId('selection-fields').disabled = !canEdit;
        for (const button of byId('arrange-buttons').querySelectorAll('button')) {
            button.disabled = !canEdit || button.dataset.available !== 'true';
        }
        for (const group of byId('action-groups').querySelectorAll('fieldset')) group.disabled = !canEdit;
        byId('theme-add-fields').disabled = !canEdit;
        for (const control of byId('theme-colors').querySelectorAll('input, button')) control.disabled = !canEdit;
        for (const button of byId('palette').querySelectorAll('button')) button.disabled = !canEdit || !rootComponent();
        byId('resize-handle').disabled = !canvasEditable();
        stage.classList.toggle('stale', Boolean(state) && frameVersion !== state.documentVersion);
        stage.setAttribute('aria-busy', String(Boolean(busy || pendingEdit || (state && frameVersion !== state.documentVersion))));
    }

    function select(id) {
        cancelGesture();
        selectedId = components().some(component => component.id === id) ? id : null;
        selectedIds = selectedId ? [selectedId] : [];
        newActionFor = null;
        remember();
        renderHierarchy();
        renderInspector();
        renderSelection();
    }

    function toggleSelect(id) {
        cancelGesture();
        if (!components().some(component => component.id === id)) return;
        selectedIds = toggleSelection(selectedIds, id);
        selectedId = selectedIds.length ? selectedIds[selectedIds.length - 1] : null;
        newActionFor = null;
        remember();
        renderHierarchy();
        renderInspector();
        renderSelection();
    }

    // The selected components that can be moved, duplicated, or deleted (never the root).
    const editableSelection = () => selectedIds.filter(id => !isRoot(components().find(item => item.id === id)));

    function resizeStage() {
        if (!state?.surface) return;
        stage.style.width = `${state.surface.width * zoom}px`;
        stage.style.height = `${state.surface.height * zoom}px`;
        renderSelection();
    }

    function commit(operation, expectedVersion = state?.documentVersion) {
        if (!editable()) return;
        if (operation.kind === 'delete' && isRoot(components().find(component => component.id === operation.componentId))) return;
        const message = editMessage(state.documentVersion, expectedVersion, operation);
        if (!message) return;
        pendingEdit = true;
        localError = null;
        refreshControls();
        setStatus('Applying edit…');
        vscode.postMessage(message);
    }

    function releaseCapture(pointerId) {
        if (stage.hasPointerCapture(pointerId)) stage.releasePointerCapture(pointerId);
    }

    function cancelGesture() {
        if (!gesture) return;
        const pointerId = gesture.pointerId;
        gesture = null;
        releaseCapture(pointerId);
        renderSelection();
    }

    function pointerPosition(event) {
        const bounds = stage.getBoundingClientRect();
        return { x: (event.clientX - bounds.left) / zoom, y: (event.clientY - bounds.top) / zoom };
    }

    stage.addEventListener('pointerdown', event => {
        if (event.button !== 0 || !canvasEditable()) return;
        const position = pointerPosition(event);
        const resize = event.target === byId('resize-handle');
        const component = resize ? selected() : hitTest(components(), position.x, position.y);
        if (!resize && component && (event.ctrlKey || event.metaKey || event.shiftKey)) {
            event.preventDefault();
            toggleSelect(component.id);
            return;
        }
        if (!resize && component && selectedIds.length > 1 && selectedIds.includes(component.id)) {
            // Dragging any member of a multi-selection moves the whole selection.
            const ids = editableSelection();
            if (ids.length === 0) return;
            event.preventDefault();
            stage.focus({ preventScroll: true });
            gesture = { pointerId: event.pointerId, kind: 'group', ids, version: state.documentVersion,
                start: position, dx: 0, dy: 0 };
            stage.setPointerCapture(event.pointerId);
            return;
        }
        select(component?.id || null);
        if (!component || (!resize && isRoot(component))) return;
        event.preventDefault();
        stage.focus({ preventScroll: true });
        gesture = { pointerId: event.pointerId, kind: resize ? 'resize' : 'move',
            componentId: component.id, version: state.documentVersion, start: position,
            original: { ...component.frame }, preview: { ...component.frame } };
        stage.setPointerCapture(event.pointerId);
    });

    stage.addEventListener('pointermove', event => {
        if (!gesture || event.pointerId !== gesture.pointerId) return;
        if (gesture.version !== state?.documentVersion) { cancelGesture(); return; }
        const position = pointerPosition(event);
        if (gesture.kind === 'group') {
            gesture.dx = Math.round(position.x - gesture.start.x);
            gesture.dy = Math.round(position.y - gesture.start.y);
        } else {
            gesture.preview = gestureFrame(gesture.original, gesture.kind, position.x - gesture.start.x, position.y - gesture.start.y);
        }
        renderSelection();
    });

    stage.addEventListener('pointerup', event => {
        if (!gesture || event.pointerId !== gesture.pointerId) return;
        const finished = gesture;
        const position = pointerPosition(event);
        gesture = null;
        releaseCapture(event.pointerId);
        if (finished.kind === 'group') {
            const dx = Math.round(position.x - finished.start.x);
            const dy = Math.round(position.y - finished.start.y);
            if (dx !== 0 || dy !== 0) {
                commit({ kind: 'frames', frames: movedFrames(components(), finished.ids, dx, dy) }, finished.version);
            }
            renderSelection();
            return;
        }
        finished.preview = gestureFrame(finished.original, finished.kind, position.x - finished.start.x, position.y - finished.start.y);
        if (!sameFrame(finished.original, finished.preview)) {
            commit({ kind: 'frame', componentId: finished.componentId, frame: finished.preview }, finished.version);
        }
        renderSelection();
    });
    stage.addEventListener('pointercancel', cancelGesture);
    stage.addEventListener('lostpointercapture', cancelGesture);
    window.addEventListener('blur', cancelGesture);

    stage.addEventListener('keydown', event => {
        if (event.key === 'Escape') { cancelGesture(); return; }
        const component = selected();
        if (!component || !canvasEditable() || event.ctrlKey || event.metaKey || event.altKey) return;
        if (event.key === 'Delete' || event.key === 'Backspace') {
            const ids = editableSelection();
            if (ids.length > 0) { event.preventDefault(); commit({ kind: 'delete', componentIds: ids }); }
            return;
        }
        const directions = { ArrowLeft: [-1, 0], ArrowRight: [1, 0], ArrowUp: [0, -1], ArrowDown: [0, 1] };
        if (!directions[event.key]) return;
        const resize = event.target === byId('resize-handle');
        if (!resize && selectedIds.length > 1) {
            const amount = event.shiftKey ? 10 : 1;
            const [dx, dy] = directions[event.key];
            const frames = movedFrames(components(), editableSelection(), dx * amount, dy * amount);
            event.preventDefault();
            if (Object.keys(frames).length > 0) commit({ kind: 'frames', frames });
            return;
        }
        if (!resize && isRoot(component)) return;
        event.preventDefault();
        const amount = event.shiftKey ? 10 : 1;
        const [dx, dy] = directions[event.key];
        commit({ kind: 'frame', componentId: component.id,
            frame: gestureFrame(component.frame, resize ? 'resize' : 'move', dx * amount, dy * amount) });
    });

    byId('frame-form').addEventListener('submit', event => {
        event.preventDefault();
        const component = selected();
        if (!component) return;
        const frame = Object.fromEntries(Object.entries(fieldIds).map(([key, id]) => [key, byId(id).valueAsNumber]));
        if (!validFrame(frame)) { reportError('Frame values must be whole pixels, with positive width and height.'); return; }
        if (!sameFrame(frame, component.frame)) commit({ kind: 'frame', componentId: component.id, frame });
    });
    byId('text-form').addEventListener('submit', event => {
        event.preventDefault();
        const component = selected();
        const text = byId('component-text').value;
        if (component && text !== component.text) commit({ kind: 'text', componentId: component.id, text });
    });
    byId('delete-component').addEventListener('click', () => {
        const component = selected();
        if (component && !isRoot(component)) commit({ kind: 'delete', componentId: component.id });
    });
    byId('component-name-input').addEventListener('change', () => {
        const component = selected();
        const name = byId('component-name-input').value.trim();
        if (!component) return;
        if (!name) { byId('component-name-input').value = String(component.name || ''); reportError('A component needs a name.'); return; }
        if (name !== component.name) commit({ kind: 'rename', componentId: component.id, name });
    });
    byId('component-parent').addEventListener('change', () => {
        const component = selected();
        const parentId = byId('component-parent').value;
        if (component && parentId && parentId !== component.parentId) commit({ kind: 'reparent', componentId: component.id, parentId });
    });
    for (const button of byId('order-row').querySelectorAll('button[data-order]')) {
        button.addEventListener('click', () => {
            const component = selected();
            if (component && !isRoot(component)) commit({ kind: 'order', componentId: component.id, order: button.dataset.order });
        });
    }
    byId('duplicate-component').addEventListener('click', () => {
        const component = selected();
        if (component && !isRoot(component)) commit({ kind: 'duplicate', componentId: component.id });
    });
    byId('duplicate-selection').addEventListener('click', () => {
        const ids = editableSelection();
        if (ids.length > 0) commit({ kind: 'duplicate', componentIds: ids });
    });
    byId('delete-selection').addEventListener('click', () => {
        const ids = editableSelection();
        if (ids.length > 0) commit({ kind: 'delete', componentIds: ids });
    });
    byId('theme-add').addEventListener('submit', event => {
        event.preventDefault();
        const input = byId('theme-add-id');
        const raw = input.value.trim();
        if (!raw) { reportError('Name the new theme color, for example theme.brand.'); return; }
        const tokenId = raw.startsWith('theme.') ? raw : `theme.${raw}`;
        input.value = '';
        commit({ kind: 'themeColor', tokenId, color: hexToLegacyColor(byId('theme-add-color').value) });
    });
    byId('widget-filter').addEventListener('input', renderPalette);
    byId('zoom').addEventListener('change', () => {
        cancelGesture();
        zoom = Number(byId('zoom').value);
        remember();
        resizeStage();
    });
    byId('generate').addEventListener('click', () => { if (editable()) vscode.postMessage({ type: 'generate' }); });
    byId('add-screen').addEventListener('click', () => vscode.postMessage({ type: 'addScreen' }));
    byId('refresh-screens').addEventListener('click', () => vscode.postMessage({ type: 'refreshScreens' }));
    byId('source').addEventListener('click', () => vscode.postMessage({ type: 'source' }));
    byId('setup').addEventListener('click', () => { if (editable()) vscode.postMessage({ type: 'setup' }); });

    window.addEventListener('message', event => {
        const message = event.data;
        if (!message || message.type !== 'screens') return;
        const list = byId('screens');
        list.replaceChildren();
        const screens = Array.isArray(message.screens) ? message.screens : [];
        byId('screen-count').textContent = String(screens.length);
        byId('screens-status').textContent = message.warning || (screens.length ? 'Open a screen to edit its form.' : 'No screens found.');
        for (const screen of screens) {
            const item = document.createElement('li');
            const button = document.createElement('button');
            button.type = 'button';
            button.className = 'screen-button secondary';
            button.textContent = screen.label;
            button.title = screen.detail;
            if (screen.active) button.setAttribute('aria-current', 'page');
            button.addEventListener('click', () => vscode.postMessage({ type: 'openScreen', id: screen.id }));
            item.append(button);
            list.append(item);
        }
    });

    function acceptState(message) {
        if (!Number.isInteger(message.documentVersion)) { reportError('The designer received a document without a version.'); return; }
        if (state && message.documentVersion < state.documentVersion) return;
        cancelGesture();
        state = message;
        pendingEdit = false;
        localError = null;
        frameDiagnostics = [];
        const exists = id => components().some(component => component.id === id);
        if (message.selectedComponentId && exists(message.selectedComponentId)) {
            selectedId = message.selectedComponentId;
            selectedIds = [selectedId];
        } else {
            selectedIds = selectedIds.filter(exists);
            if (!exists(selectedId)) selectedId = selectedIds[selectedIds.length - 1] || rootComponent()?.id || null;
            if (selectedId && !selectedIds.includes(selectedId)) selectedIds = [...selectedIds, selectedId];
        }
        remember();
        const surface = message.surface;
        if (surface && Number.isInteger(surface.width) && Number.isInteger(surface.height) && surface.width > 0 && surface.height > 0) {
            stage.hidden = false;
            byId('canvas-empty').hidden = true;
            byId('surface-description').textContent = `${surface.width} × ${surface.height} · ${surface.format || ''}${typeof surface.shape === 'string' ? ` · ${surface.shape}` : ''}`;
            resizeStage();
        }
        renderPalette(); renderHierarchy(); renderThemeColors(); renderInspector(); renderSelection(); renderDiagnostics(); refreshControls();
        const invalid = Array.isArray(state.diagnostics) && state.diagnostics.some(diagnostic => String(diagnostic.severity).toLowerCase() === 'error');
        setStatus(invalid ? 'Fix validation errors' : frameVersion === state.documentVersion ? 'Preview ready' : 'Rendering…', invalid);
    }

    function acceptFrame(message) {
        if (!state || message.documentVersion !== state.documentVersion) return;
        frameDiagnostics = Array.isArray(message.diagnostics) ? message.diagnostics : [];
        renderDiagnostics();
        const { width, height, rgbaBase64 } = message;
        if (!Number.isInteger(width) || !Number.isInteger(height) || width < 1 || height < 1 || width * height > 16777216 || typeof rgbaBase64 !== 'string') {
            reportError(typeof message.status === 'string' ? `Preview unavailable: ${message.status}` : 'Native preview is unavailable. See validation messages.');
            return;
        }
        try {
            const decoded = atob(rgbaBase64);
            if (decoded.length !== width * height * 4) throw new Error('Native preview contains an incomplete pixel buffer.');
            const pixels = new Uint8ClampedArray(decoded.length);
            for (let index = 0; index < decoded.length; ++index) pixels[index] = decoded.charCodeAt(index);
            canvas.width = width;
            canvas.height = height;
            context.putImageData(new ImageData(pixels, width, height), 0, 0);
            frameVersion = message.documentVersion;
            localError = null;
            setStatus('Preview ready');
            renderDiagnostics(); refreshControls(); renderSelection();
        } catch (error) { reportError(error.message || 'Unable to display the native preview.'); }
    }

    window.addEventListener('message', event => {
        const message = event.data;
        if (!message || typeof message !== 'object') return;
        if (message.type === 'state') acceptState(message);
        else if (message.type === 'frame') acceptFrame(message);
        else if (message.type === 'busy') {
            busy = Boolean(message.busy);
            if (busy) cancelGesture();
            refreshControls();
        } else if (message.type === 'error') {
            pendingEdit = false;
            cancelGesture();
            reportError(message.message || 'The designer operation failed.');
            refreshControls();
        }
    });
    vscode.postMessage({ type: 'ready' });
}());
