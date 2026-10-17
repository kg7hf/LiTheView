// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

'use strict';

const test = require('node:test');
const assert = require('node:assert/strict');
const { reparentTargets, cppIdentifier, actionIdFromSymbol, nextActionRuntimeId, toggleSelection,
    arrangeFrames, movedFrames, editMessage } = require('../media/designer');

const widgets = [
    { id: 'lithe.widget.panel', container: true, allowedParentIds: ['lithe.widget.panel', 'lithe.widget.tab-view'] },
    { id: 'lithe.widget.tab-view', container: true, allowedParentIds: ['lithe.widget.panel'] },
    { id: 'lithe.widget.button', container: false, allowedParentIds: ['lithe.widget.panel'] }
];
const frame = (x, y, width, height) => ({ x, y, width, height });
const root = { id: 'root', parentId: null, widgetId: 'lithe.widget.panel', frame: frame(0, 0, 480, 272) };
const group = { id: 'group', parentId: 'root', widgetId: 'lithe.widget.panel', frame: frame(10, 10, 200, 120) };
const inner = { id: 'inner', parentId: 'group', widgetId: 'lithe.widget.panel', frame: frame(5, 5, 50, 50) };
const tabs = { id: 'tabs', parentId: 'root', widgetId: 'lithe.widget.tab-view', frame: frame(220, 10, 200, 200) };
const a = { id: 'a', parentId: 'root', widgetId: 'lithe.widget.button', frame: frame(10, 150, 60, 20) };
const b = { id: 'b', parentId: 'root', widgetId: 'lithe.widget.button', frame: frame(100, 160, 80, 30) };
const c = { id: 'c', parentId: 'root', widgetId: 'lithe.widget.button', frame: frame(300, 140, 40, 24) };
const all = [root, group, inner, tabs, a, b, c];

test('reparent targets are allowed containers that are not the component or its descendants', () => {
    assert.deepEqual(reparentTargets(all, widgets, 'a').map(item => item.id), ['root', 'group', 'inner']);
    assert.deepEqual(reparentTargets(all, widgets, 'group').map(item => item.id), ['root', 'tabs']);
    assert.deepEqual(reparentTargets(all, widgets, 'root'), []);
});

test('new action symbols, IDs, and runtime IDs follow the designer rules', () => {
    assert.equal(cppIdentifier('start pump'), 'StartPump');
    assert.equal(cppIdentifier('startPump'), 'StartPump');
    assert.equal(cppIdentifier('2nd stage'), 'N2ndStage');
    assert.equal(cppIdentifier('  '), 'Unnamed');
    assert.equal(actionIdFromSymbol('StartPump'), 'action.start-pump');
    assert.equal(actionIdFromSymbol('N2ndStage'), 'action.n2nd-stage');
    assert.equal(actionIdFromSymbol('!!'), null);
    const state = {
        choices: [{ id: 'action.go', kind: 'action', runtimeId: 7 }, { id: 'theme.x', kind: 'themeColor' }],
        components: [{ id: 'x', properties: [{ id: 'runtime-id', value: 12 }] }]
    };
    assert.equal(nextActionRuntimeId(state), 13);
    assert.equal(nextActionRuntimeId({ choices: [{ kind: 'action', runtimeId: 65535 }], components: [] }), null);
});

test('multi-selection toggles membership and keeps the newest as primary', () => {
    assert.deepEqual(toggleSelection(['a'], 'b'), ['a', 'b']);
    assert.deepEqual(toggleSelection(['a', 'b'], 'a'), ['b']);
    assert.deepEqual(toggleSelection([], 'c'), ['c']);
});

test('alignment, distribution, and sizing use the anchor and change only what moves', () => {
    assert.deepEqual(arrangeFrames(all, ['a', 'b'], 'b', 'left'), { a: frame(100, 150, 60, 20) });
    assert.deepEqual(arrangeFrames(all, ['a', 'b'], 'a', 'right'), { b: frame(-10, 160, 80, 30) });
    assert.deepEqual(arrangeFrames(all, ['a', 'b'], 'b', 'middle'), { a: frame(10, 165, 60, 20) });
    assert.deepEqual(arrangeFrames(all, ['a', 'b'], 'a', 'sameSize'), { b: frame(100, 160, 60, 20) });
    // The outermost stay put; 150 free pixels between x=10 and x=340 become two 75-pixel gaps.
    assert.deepEqual(arrangeFrames(all, ['c', 'a', 'b'], 'a', 'distributeHorizontal'), { b: frame(145, 160, 80, 30) });
    assert.equal(arrangeFrames(all, ['a', 'b'], 'a', 'distributeVertical'), null, 'distribution needs three');
    assert.equal(arrangeFrames(all, ['a', 'inner'], 'a', 'left'), null, 'only siblings align');
    assert.equal(arrangeFrames(all, ['a'], 'a', 'left'), null);
    assert.equal(arrangeFrames(all, ['a', 'b'], 'a', 'diagonal'), null);
});

test('group moves skip the root and produce a valid frames edit', () => {
    const frames = movedFrames(all, ['root', 'a', 'b'], 5, -3);
    assert.deepEqual(frames, { a: frame(15, 147, 60, 20), b: frame(105, 157, 80, 30) });
    assert.ok(editMessage(4, 4, { kind: 'frames', frames }));
    assert.equal(editMessage(4, 4, { kind: 'frames', frames: {} }), null);
    assert.equal(editMessage(4, 4, { kind: 'frames', frames: { a: frame(0, 0, 0, 5) } }), null);
    assert.equal(editMessage(4, 4, { kind: 'rename', componentId: 'a', name: '  ' }), null);
});
