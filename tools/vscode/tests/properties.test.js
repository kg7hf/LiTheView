// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

'use strict';

const test = require('node:test');
const assert = require('node:assert/strict');
const { renderWebview } = require('../lib/webview');
const {
    editMessage, legacyColorParts, legacyColorToHex, hexToLegacyColor, propertyValue, inspectorRows, choicesFor
} = require('../media/designer');

const descriptors = [
    { id: 'frame', name: 'Frame', kind: 'rectangle', category: 'Layout', defaultValue: [0, 0, 1, 1], required: true, editable: false },
    { id: 'text', name: 'Text', kind: 'boundedText', category: 'Content', defaultValue: '', editable: true },
    { id: 'background-color', name: 'Background Color', kind: 'color', category: 'Appearance', defaultValue: '#00000000', editable: true },
    { id: 'border-width', name: 'Border Width', kind: 'unsignedInteger', category: 'Layout', defaultValue: 0, minimum: 0, maximum: 16, units: 'pixels', editable: true },
    { id: 'button-behavior', name: 'Behavior', kind: 'enum', category: 'Behavior', defaultValue: 'momentary', enumValues: ['momentary', 'toggle'], editable: true },
    { id: 'checked', name: 'Checked', kind: 'boolean', category: 'Behavior', defaultValue: false, editable: true },
    { id: 'icon', name: 'Icon', kind: 'bitmap', category: 'Resources', defaultValue: null, editable: true }
];
const state = {
    widgets: [{ id: 'lithe.widget.button', name: 'Button', properties: ['frame', 'background-color', 'border-width', 'text', 'button-behavior', 'checked', 'icon'] }],
    properties: descriptors,
    choices: [
        { id: 'theme.primary', name: 'theme.primary', kind: 'themeColor', color: '#00df6c2d' },
        { id: 'resource.bitmap.home', name: 'home-icon', kind: 'bitmap' },
        { id: 'resource.font.body', name: 'body', kind: 'font' }
    ]
};
const byId = id => descriptors.find(item => item.id === id);

test('legacy colors keep red in the low byte and an inverted alpha', () => {
    assert.deepEqual(legacyColorParts('#00fae4d6'), { digits: '00fae4d6', red: 0xd6, green: 0xe4, blue: 0xfa, alpha: 255 });
    assert.equal(legacyColorParts('0xFF000000').alpha, 0);
    assert.equal(legacyColorToHex('#00df6c2d'), '#2d6cdf');
    assert.equal(hexToLegacyColor('#2d6cdf'), '#00df6c2d');
    assert.equal(legacyColorToHex(hexToLegacyColor('#123456')), '#123456');
    assert.equal(legacyColorParts('#123'), null);
    assert.equal(hexToLegacyColor('red'), null);
});

test('control values become typed property values or are rejected', () => {
    assert.equal(propertyValue(byId('checked'), true), true);
    assert.equal(propertyValue(byId('checked'), 'true'), null);
    assert.equal(propertyValue(byId('border-width'), '4'), 4);
    assert.equal(propertyValue(byId('border-width'), '17'), null);
    assert.equal(propertyValue(byId('border-width'), '1.5'), null);
    assert.equal(propertyValue(byId('border-width'), ''), null);
    assert.equal(propertyValue(byId('button-behavior'), 'toggle'), 'toggle');
    assert.equal(propertyValue(byId('button-behavior'), 'latch'), null);
    assert.equal(propertyValue(byId('background-color'), ' theme.primary '), 'theme.primary');
    assert.equal(propertyValue(byId('background-color'), '0x00FAE4D6'), '#00fae4d6');
    assert.equal(propertyValue(byId('background-color'), 'theme.'), null);
    assert.equal(propertyValue(byId('icon'), 'resource.bitmap.home'), 'resource.bitmap.home');
    assert.equal(propertyValue(byId('icon'), ''), null);
    assert.equal(propertyValue(byId('frame'), '0,0,1,1'), null);
});

test('inspector rows follow the widget schema, skip frame and text, and keep defaults implicit', () => {
    const component = { id: 'b', widgetId: 'lithe.widget.button', properties: [{ id: 'button-behavior', value: 'toggle' }, { id: 'border-width', value: 2 }] };
    const rows = inspectorRows(state, component);
    assert.deepEqual(rows.map(row => row.descriptor.id), ['button-behavior', 'checked', 'background-color', 'border-width', 'icon']);
    assert.deepEqual(rows.map(row => row.explicit), [true, false, false, true, false]);
    assert.equal(rows[1].value, false);
    assert.equal(rows[4].value, null);
    assert.deepEqual(inspectorRows(state, { ...component, widgetId: 'unknown' }), []);
});

test('choices are limited to the property kind', () => {
    assert.deepEqual(choicesFor(state, byId('icon')).map(choice => choice.id), ['resource.bitmap.home']);
    assert.deepEqual(choicesFor(state, byId('background-color')).map(choice => choice.id), ['theme.primary']);
    assert.deepEqual(choicesFor(state, byId('checked')), []);
});

test('property and reset edits require a property and a value', () => {
    const property = { kind: 'property', componentId: 'b', propertyId: 'checked', value: true };
    assert.deepEqual(editMessage(3, 3, property), { type: 'edit', documentVersion: 3, operation: property });
    assert.equal(editMessage(3, 3, { ...property, value: null }), null);
    assert.equal(editMessage(3, 3, { kind: 'reset', componentId: 'b' }), null);
    assert.ok(editMessage(3, 3, { kind: 'reset', componentId: 'b', propertyId: 'checked' }));
    assert.equal(editMessage(4, 3, property), null);
});

test('HTML provides the property container and theme token list', () => {
    const html = renderWebview({ scriptUri: 'vscode-webview://test/designer.js', styleUri: 'vscode-webview://test/designer.css', cspSource: 'vscode-webview://test', nonce: 'n' });
    assert.match(html, /id="property-groups"/);
    assert.match(html, /<datalist id="theme-tokens"><\/datalist>/);
});
