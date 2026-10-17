// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

'use strict';

const test = require('node:test');
const assert = require('node:assert/strict');
const { renderWebview } = require('../lib/webview');
const { validFrame, absoluteFrame, hierarchyEntries, hitTest, gestureFrame, editMessage, paletteOperation } = require('../media/designer');

const root = { id: 'root', parentId: null, frame: { x: 0, y: 0, width: 390, height: 390 } };
const panel = { id: 'panel', parentId: 'root', frame: { x: 30, y: 40, width: 200, height: 160 } };
const button = { id: 'button', parentId: 'panel', frame: { x: 12, y: 15, width: 100, height: 32 } };
const components = [button, root, panel];

test('nested overlays add every parent offset without changing relative geometry', () => {
    assert.deepEqual(absoluteFrame(button, components), { x: 42, y: 55, width: 100, height: 32 });
    assert.deepEqual(button.frame, { x: 12, y: 15, width: 100, height: 32 });
});

test('hierarchy and hit testing find a nested child even when input is not preorder', () => {
    assert.deepEqual(hierarchyEntries(components).map(entry => [entry.component.id, entry.depth]), [['root', 0], ['panel', 1], ['button', 2]]);
    assert.equal(hitTest(components, 50, 60).id, 'button');
    assert.equal(hitTest(components, 142, 60).id, 'panel');
    assert.equal(hitTest(components, 390, 60), null);
});

test('hidden tab pages and their children are skipped by hit testing', () => {
    const tabs = { id: 'tabs', parentId: 'root', frame: { x: 0, y: 0, width: 300, height: 200 } };
    const shown = { id: 'shown', parentId: 'tabs', frame: { x: 0, y: 32, width: 300, height: 168 } };
    const hidden = { id: 'hidden', parentId: 'tabs', frame: { x: 0, y: 32, width: 300, height: 168 }, previewHidden: true };
    const hiddenButton = { id: 'hidden-button', parentId: 'hidden', frame: { x: 8, y: 8, width: 80, height: 30 }, previewHidden: true };
    const pages = [root, tabs, shown, hidden, hiddenButton];
    assert.equal(hitTest(pages, 20, 50).id, 'shown');
    assert.equal(hitTest(pages, 20, 10).id, 'tabs');
    assert.equal(hierarchyEntries(pages).length, 5);
});

test('the palette adds into a selected container that accepts the widget, otherwise into the root', () => {
    const tabs = { id: 'tabs', parentId: 'root', widgetId: 'lithe.widget.tab-view', frame: { x: 0, y: 0, width: 300, height: 200 } };
    const hiddenPage = { id: 'page', parentId: 'tabs', widgetId: 'lithe.widget.panel', frame: { x: 0, y: 32, width: 300, height: 168 }, previewHidden: true };
    const label = { id: 'label', parentId: 'root', widgetId: 'lithe.widget.label', frame: { x: 0, y: 0, width: 50, height: 20 } };
    const all = [{ ...root, widgetId: 'lithe.widget.panel' }, tabs, hiddenPage, label];
    const widgets = [
        { id: 'lithe.widget.panel', container: true, allowedParentIds: ['lithe.widget.panel', 'lithe.widget.tab-view'] },
        { id: 'lithe.widget.tab-view', container: true, allowedParentIds: ['lithe.widget.panel'] },
        { id: 'lithe.widget.label', container: false, allowedParentIds: ['lithe.widget.panel'] }
    ];
    const [panelWidget, , labelWidget] = widgets;
    assert.deepEqual(paletteOperation(all, widgets, 'tabs', panelWidget), { kind: 'add', parentId: 'tabs', widgetId: 'lithe.widget.panel' });
    assert.equal(paletteOperation(all, widgets, 'tabs', labelWidget).parentId, 'root');
    assert.deepEqual(paletteOperation(all, widgets, 'label', labelWidget), { kind: 'add', parentId: 'root', widgetId: 'lithe.widget.label' });
    assert.equal(paletteOperation(all, widgets, 'page', labelWidget).parentId, 'root');
    assert.equal(paletteOperation([], widgets, null, labelWidget), null);
});

test('orphaned and cyclic components remain inspectable but cannot place overlays', () => {
    const orphan = { ...button, parentId: 'missing' };
    const cyclic = [{ ...root, parentId: 'button' }, button, panel];
    assert.equal(absoluteFrame(orphan, [orphan]), null);
    assert.equal(absoluteFrame(button, cyclic), null);
    assert.equal(hierarchyEntries(cyclic).length, 3);
});

test('gestures move relative coordinates and resize to at least one pixel', () => {
    assert.deepEqual(gestureFrame(button.frame, 'move', 3.4, -7.2), { x: 15, y: 8, width: 100, height: 32 });
    assert.deepEqual(gestureFrame(button.frame, 'resize', -200, 8), { x: 12, y: 15, width: 1, height: 40 });
    assert.equal(validFrame({ x: 0.5, y: 0, width: 1, height: 1 }), false);
});

test('an edit completed against an older document version is discarded', () => {
    const operation = { kind: 'frame', componentId: 'button', frame: button.frame };
    assert.equal(editMessage(8, 7, operation), null);
    assert.deepEqual(editMessage(8, 8, operation), { type: 'edit', documentVersion: 8, operation });
    assert.equal(editMessage(8, 8, { ...operation, frame: { ...button.frame, width: 0 } }), null);
});

test('HTML uses only external nonce-authorized scripts and escapes URI attributes', () => {
    const html = renderWebview({ scriptUri: 'vscode-webview://test/designer.js?x="bad"', styleUri: 'vscode-webview://test/designer.css', cspSource: 'vscode-webview://test', nonce: 'nonce123' });
    assert.match(html, /default-src &#39;none&#39;/);
    assert.match(html, /script-src &#39;nonce-nonce123&#39;/);
    assert.match(html, /nonce="nonce123" src="vscode-webview:\/\/test\/designer\.js\?x=&quot;bad&quot;"/);
    assert.doesNotMatch(html, /\bon(?:click|load|error)=/);
    assert.doesNotMatch(html, /unsafe-inline|https?:\/\//);
    assert.match(html, /id="resize-handle".*aria-label=/);
});
