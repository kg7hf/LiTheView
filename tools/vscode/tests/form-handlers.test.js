// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

'use strict';
const assert = require('node:assert/strict');
const test = require('node:test');
const { planHandlerEdit, validateBindings } = require('../lib/form-handlers');
const plan = {
  formId: 'form.main', include: '../../ui generated/product_config.hpp',
  handlers: [{ componentId: 'button.first', method: 'onButton1Clicked' },
    { componentId: 'button.second', method: 'onButton2Clicked' }]
};
test('create/open preserves behavior and adds a second handler without replacing the first', () => {
  const first = planHandlerEdit(null, plan, 'button.first');
  const edited = first.text.replace('// Add application behavior here.', 'getLabel1()->setText("OFF");');
  const opened = planHandlerEdit(edited, plan, 'button.first');
  assert.equal(opened.changed, false);
  assert.equal(opened.text, edited);
  const second = planHandlerEdit(edited, plan, 'button.second');
  assert.ok(second.text.includes('getLabel1()->setText("OFF");'));
  assert.equal((second.text.match(/void onButton1Clicked/g) || []).length, 1);
  assert.ok(second.text.includes('#include "../../ui generated/product_config.hpp"'));
  validateBindings(second.text, plan);
});
test('renamed/deleted bindings fail without rewriting user source', () => {
  const original = planHandlerEdit(null, plan, 'button.first').text;
  assert.throws(() => validateBindings(original, { ...plan, handlers: [] }), /renamed, deleted/);
  assert.throws(() => validateBindings(original, { ...plan, handlers: [{ componentId: 'button.first', method: 'onRenamedClicked' }] }), /renamed, deleted/);
  assert.throws(() => validateBindings(original, { ...plan, formId: 'different.form' }), /different form/);
});
test('CRLF bodies remain exact; ambiguous insertion markers and injected plans are rejected', () => {
  const original = planHandlerEdit(null, plan, 'button.first').text.replace(/\n/g, '\r\n');
  assert.equal(planHandlerEdit(original, plan, 'button.first').text, original);
  const second = planHandlerEdit(original, plan, 'button.second').text;
  assert.ok(second.includes('onButton2Clicked() noexcept override\r\n'));
  assert.throws(() => planHandlerEdit(original + '    // </litheview-handlers>', plan, 'button.second'), /one handwritten/);
  assert.throws(() => planHandlerEdit(null, { ...plan, include: 'bad"\npath' }, 'button.first'), /Invalid typed-handler/);
});

test('multiple typed events bind independently and preserve legacy click bodies', () => {
  const typed = { ...plan, handlers: [...plan.handlers,
    { componentId: 'button.first', eventId: 'checkedChanged', method: 'onButton1CheckedChanged', parameterType: 'lithe::CheckedChange' },
    { componentId: 'wheel', eventId: 'selectionChanged', method: 'onWheelSelectionChanged', parameterType: 'lithe::SelectionChange' },
    { componentId: 'wheel', eventId: 'editCommitted', method: 'onWheelEditCommitted', parameterType: 'lithe::SelectionEdit' },
    { componentId: 'wheel', eventId: 'editCancelled', method: 'onWheelEditCancelled', parameterType: 'lithe::SelectionEdit' }
  ] };
  const legacy = planHandlerEdit(null, plan, 'button.first').text
    .replace('button.first clicked onButton1Clicked', 'button.first onButton1Clicked')
    .replace('// Add application behavior here.', 'getLabel1()->setText("KEEP");');
  let text = legacy;
  for (const item of typed.handlers.slice(2)) {
    text = planHandlerEdit(text, typed, item.componentId, item.eventId).text;
  }
  assert.ok(text.includes('getLabel1()->setText("KEEP");'));
  assert.ok(text.includes('void onWheelEditCommitted([[maybe_unused]] lithe::SelectionEdit event) noexcept override'));
  validateBindings(text, typed);
  for (const item of typed.handlers.slice(2)) {
    assert.equal(planHandlerEdit(text, typed, item.componentId, item.eventId).changed, false);
  }
  assert.throws(() => validateBindings(text, { ...typed, handlers: typed.handlers.slice(0, -1) }), /control\/event binding/);
  assert.throws(() => validateBindings(text.replace('lithe::SelectionEdit event', 'lithe::SelectionChange event'), typed), /incompatible signature/);
  assert.throws(() => planHandlerEdit(text, typed, 'wheel', 'clicked'), /available control event/);
  assert.throws(() => validateBindings(text, { ...typed, handlers: [...typed.handlers, typed.handlers[2]] }), /Invalid typed-handler/);
});

test('text events keep independent typed handlers and preserve application code', () => {
  const textPlan = { ...plan, handlers: [
    { componentId: 'setpoint', eventId: 'textChanged', method: 'onSetpointTextChanged', parameterType: 'lithe::TextChange' },
    { componentId: 'setpoint', eventId: 'editCommitted', method: 'onSetpointEditCommitted', parameterType: 'lithe::TextEdit' },
    { componentId: 'setpoint', eventId: 'editCancelled', method: 'onSetpointEditCancelled', parameterType: 'lithe::TextEdit' }
  ] };
  let text = planHandlerEdit(null, textPlan, 'setpoint', 'editCommitted').text
    .replace('// Add application behavior here.', 'applySetpoint(event.text);');
  for (const event of textPlan.handlers) {
    text = planHandlerEdit(text, textPlan, 'setpoint', event.eventId).text;
  }
  validateBindings(text, textPlan);
  assert.ok(text.includes('applySetpoint(event.text);'));
  assert.ok(text.includes('onSetpointTextChanged([[maybe_unused]] lithe::TextChange event)'));
  for (const event of textPlan.handlers) {
    assert.equal(planHandlerEdit(text, textPlan, 'setpoint', event.eventId).changed, false);
  }
  assert.throws(() => validateBindings(text.replace('lithe::TextEdit event', 'lithe::SelectionEdit event'), textPlan), /incompatible signature/);
});
