// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

'use strict';

function escapeAttribute(value) {
    return String(value).replace(/[&<>"']/g, character => ({
        '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;'
    })[character]);
}

/** The webview loads local assets only; document content arrives by message. */
function renderWebview({ scriptUri, styleUri, cspSource, nonce }) {
    const policy = `default-src 'none'; img-src ${cspSource} data:; style-src ${cspSource}; script-src 'nonce-${nonce}'; connect-src 'none'; base-uri 'none'; form-action 'none';`;
    return `<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <meta http-equiv="Content-Security-Policy" content="${escapeAttribute(policy)}">
    <link rel="stylesheet" href="${escapeAttribute(styleUri)}">
    <title>LiTheView Designer</title>
</head>
<body>
    <header class="toolbar">
        <div class="brand"><span class="brand-mark" aria-hidden="true">L</span><strong>LiTheView</strong><span class="designer-label">Designer</span></div>
        <div class="toolbar-actions">
            <label class="zoom-label" for="zoom">Zoom</label>
            <select id="zoom" aria-label="Canvas zoom">
                <option value="0.25">25%</option><option value="0.5">50%</option>
                <option value="0.75">75%</option><option value="1" selected>100%</option>
                <option value="1.5">150%</option><option value="2">200%</option>
                <option value="3">300%</option><option value="4">400%</option>
            </select>
            <button id="source" class="secondary" type="button">View source</button>
            <button id="setup" class="secondary" type="button" disabled>Configure</button>
            <button id="generate" type="button" disabled>Generate UI</button>
        </div>
    </header>
    <main class="workspace">
        <aside class="navigation" aria-label="Widgets and components">
            <section class="palette-section">
                <h2>Widgets</h2>
                <label class="visually-hidden" for="widget-filter">Filter widgets</label>
                <input id="widget-filter" type="search" placeholder="Find a widget…" autocomplete="off">
                <div id="palette" class="palette" aria-label="Add a widget"></div>
                <p class="hint">Choose a widget to add it to the form.</p>
            </section>
            <section class="hierarchy-section">
                <h2>Components <span id="component-count" class="count">0</span></h2>
                <ul id="hierarchy" class="hierarchy" aria-label="Components"></ul>
                <p class="hint">Ctrl- or Shift-click to select several.</p>
            </section>
            <section class="theme-section">
                <h2>Theme colors <span id="theme-count" class="count">0</span></h2>
                <ul id="theme-colors" class="theme-colors" aria-label="Theme color tokens"></ul>
                <form id="theme-add">
                    <fieldset id="theme-add-fields" class="theme-add" disabled>
                        <legend class="visually-hidden">Add a theme color</legend>
                        <label class="visually-hidden" for="theme-add-id">New token ID</label>
                        <input id="theme-add-id" type="text" placeholder="theme.name" autocomplete="off" spellcheck="false">
                        <input id="theme-add-color" type="color" value="#2d6cdf" aria-label="New token color" title="New token color">
                        <button class="secondary" type="submit">Add</button>
                    </fieldset>
                </form>
            </section>
        </aside>
        <section class="canvas-section" aria-label="Display layout">
            <div class="canvas-caption"><span id="surface-description">Loading form…</span><span id="render-status" role="status">Connecting</span></div>
            <div id="canvas-viewport" class="canvas-viewport">
                <p id="canvas-empty" class="canvas-empty">Preparing the native display preview…</p>
                <div id="canvas-stage" class="canvas-stage" tabindex="0" role="group" aria-label="Layout canvas. Select a component; use arrow keys to move, Shift for ten pixels, or Delete to remove." hidden>
                    <canvas id="native-canvas" aria-label="Native LiTheView preview"></canvas>
                    <div id="selection" class="selection" hidden>
                        <span id="selection-label" class="selection-label"></span>
                        <button id="resize-handle" class="resize-handle" type="button" aria-label="Resize selected component. Drag, or use arrow keys; hold Shift for ten pixels." title="Drag to resize; arrow keys also resize"></button>
                    </div>
                </div>
            </div>
            <div class="canvas-footer"><span id="selection-status">No selection</span><span>Native renderer</span></div>
        </section>
        <aside class="inspector" aria-label="Component properties">
            <h2>Properties</h2>
            <p id="inspector-empty" class="hint">Select a component on the canvas or in the list.</p>
            <div id="inspector-content" hidden>
                <strong id="component-name" class="component-name"></strong>
                <p id="component-kind" class="hint"></p>
                <fieldset id="selection-fields" class="selection-fields" hidden disabled>
                    <legend>Selection <span id="selection-count" class="count"></span></legend>
                    <p class="hint">Aligns to the last selected component.</p>
                    <div id="arrange-buttons" class="arrange-buttons"></div>
                    <div class="button-row">
                        <button id="duplicate-selection" class="secondary" type="button">Duplicate</button>
                        <button id="delete-selection" class="secondary delete-button" type="button">Delete</button>
                    </div>
                </fieldset>
                <fieldset id="component-fields" disabled>
                    <legend>Component</legend>
                    <label class="field-label" for="component-name-input">Name</label>
                    <input id="component-name-input" type="text" autocomplete="off" spellcheck="false">
                    <div id="parent-row">
                        <label class="field-label" for="component-parent">Parent</label>
                        <select id="component-parent"></select>
                    </div>
                    <div id="order-row" class="order-buttons" role="group" aria-label="Stacking order">
                        <button class="secondary" type="button" data-order="sendToBack" title="Send to back">To back</button>
                        <button class="secondary" type="button" data-order="sendBackward" title="Send backward">Back</button>
                        <button class="secondary" type="button" data-order="bringForward" title="Bring forward">Forward</button>
                        <button class="secondary" type="button" data-order="bringToFront" title="Bring to front">To front</button>
                    </div>
                    <button id="duplicate-component" class="secondary full-width" type="button">Duplicate</button>
                </fieldset>
                <div id="action-groups"></div>
                <form id="frame-form">
                    <fieldset id="frame-fields" disabled>
                        <legend>Frame <span class="unit">px</span></legend>
                        <div class="frame-grid">
                            <label>X<input id="frame-x" type="number" step="1" required></label>
                            <label>Y<input id="frame-y" type="number" step="1" required></label>
                            <label>Width<input id="frame-width" type="number" step="1" min="1" required></label>
                            <label>Height<input id="frame-height" type="number" step="1" min="1" required></label>
                        </div>
                        <button class="secondary full-width" type="submit">Apply frame</button>
                    </fieldset>
                </form>
                <form id="text-form" hidden>
                    <fieldset id="text-fields" disabled>
                        <legend>Text</legend>
                        <label class="visually-hidden" for="component-text">Component text</label>
                        <textarea id="component-text" rows="3"></textarea>
                        <button class="secondary full-width" type="submit">Apply text</button>
                    </fieldset>
                </form>
                <div id="property-groups" class="property-groups" aria-label="Widget properties"></div>
                <datalist id="theme-tokens"></datalist>
                <button id="delete-component" class="secondary full-width delete-button" type="button" disabled>Delete component</button>
                <p id="root-hint" class="hint" hidden>The form root cannot be deleted.</p>
            </div>
        </aside>
    </main>
    <section id="diagnostics-section" class="diagnostics-section" aria-label="Validation messages" hidden>
        <h2>Validation <span id="diagnostic-count" class="count">0</span></h2>
        <ul id="diagnostics"></ul>
    </section>
    <div id="announcement" class="visually-hidden" role="status" aria-live="polite"></div>
    <script nonce="${escapeAttribute(nonce)}" src="${escapeAttribute(scriptUri)}"></script>
</body>
</html>`;
}

module.exports = { renderWebview };
