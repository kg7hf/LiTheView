// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

'use strict';

const endMarker = '    // </litheview-handlers>';
const bindingPattern = /^[ \t]*\/\/ litheview-handler ([^\r\n]*)$/gm;
const eventId = handler => handler.eventId || 'clicked';
const parameterType = handler => handler.parameterType || '';

function validatePlan(plan) {
  if ((plan?.className && !/^[A-Za-z][A-Za-z0-9]*$/.test(plan.className)) || (plan?.baseType && !/^lithe::(?:generated|simulator)::[A-Za-z][A-Za-z0-9]*$/.test(plan.baseType))) throw new Error('Invalid handler type.');
  const seen = new Set();
  if (!plan || !/^[A-Za-z][\w.-]{0,127}$/.test(plan.formId) || typeof plan.include !== 'string' ||
      !plan.include || /["\r\n]/.test(plan.include) || !Array.isArray(plan.handlers) || plan.handlers.length > 1536 ||
      plan.handlers.some(item => {
        const key = `${item.componentId}:${eventId(item)}`;
        const duplicate = seen.has(key);
        seen.add(key);
        return duplicate || !/^[A-Za-z][\w.-]{0,127}$/.test(item.componentId) ||
          !/^on[A-Za-z0-9]+$/.test(item.method) || !/^[a-z][A-Za-z0-9]*$/.test(eventId(item)) ||
          (parameterType(item) !== '' && !/^lithe::[A-Z][A-Za-z0-9]*$/.test(parameterType(item)));
      })) {
    throw new Error('Invalid typed-handler contract from the designer service.');
  }
}

function signatureMatch(text, handler) {
  const parameter = parameterType(handler) ?
    String.raw`(?:\[\[maybe_unused\]\]\s*)?(?:const\s+)?${parameterType(handler)}\s+[A-Za-z_]\w*` : '';
  return new RegExp(String.raw`\bvoid\s+${handler.method}\s*\(\s*${parameter}\s*\)\s*noexcept\s+override\b`).exec(text);
}

function validateBindings(text, plan) {
  validatePlan(plan);
  if (text.length > 1024 * 1024) throw new Error('The handwritten form exceeds the 1 MiB editing limit.');
  if (!text.includes(`// LiTheView handwritten form: ${plan.formId}\n`) &&
      !text.includes(`// LiTheView handwritten form: ${plan.formId}\r\n`)) {
    throw new Error('The existing form_handlers.hpp belongs to a different form or has no ownership marker. It has been preserved.');
  }
  if ((plan?.className && !/^[A-Za-z][A-Za-z0-9]*$/.test(plan.className)) || (plan?.baseType && !/^lithe::(?:generated|simulator)::[A-Za-z][A-Za-z0-9]*$/.test(plan.baseType))) throw new Error('Invalid handler type.');
  const seen = new Set();
  for (const match of text.matchAll(bindingPattern)) {
    const fields = match[1].trim().split(/\s+/);
    // Legacy comments identify only the click handler. Keep them and their bodies intact.
    const [componentId, event, method] = fields.length === 2 ? [fields[0], 'clicked', fields[1]] : fields;
    const key = `${componentId}:${event}`;
    const current = plan.handlers.find(item => item.componentId === componentId && eventId(item) === event);
    if (![2, 3].includes(fields.length) || seen.has(key) || !current || current.method !== method) {
      throw new Error(`Handler ${method} has a renamed, deleted, or duplicate control/event binding. Update the handwritten override and its litheview-handler comment; the body has been preserved.`);
    }
    if (!signatureMatch(text, current)) {
      throw new Error(`Handler ${method} has an incompatible signature. Update the handwritten override to match ${parameterType(current) || 'no parameters'}; the body has been preserved.`);
    }
    seen.add(key);
  }
}

function planHandlerEdit(existing, plan, componentId, requestedEvent = 'clicked') {
  validatePlan(plan);
  const handler = plan.handlers.find(item => item.componentId === componentId && eventId(item) === requestedEvent);
  if (!handler) throw new Error('Select an available control event to create or open its handler.');
  let text = existing;
  if (text === null) {
    text = `// LiTheView handwritten form: ${plan.formId}\n` +
      '// Application-owned. Generate UI never rewrites this file.\n#pragma once\n\n' +
      `#include "${plan.include}"\n\nnamespace application::ui\n{\n` +
      `class ${plan.className || 'Form'} : public ${plan.baseType || 'lithe::simulator::Form'}::Instance<>\n{\nprotected:\n` +
      '    // <litheview-handlers>\n' + endMarker + '\n};\n} // namespace application::ui\n';
  }
  validateBindings(text, plan);
  const parameters = parameterType(handler) ? `[[maybe_unused]] ${parameterType(handler)} event` : '';
  const signature = `void ${handler.method}(${parameters}) noexcept override`;
  const existingOffset = signatureMatch(text, handler)?.index ?? -1;
  if (existingOffset >= 0) return { text, offset: existingOffset, changed: existing === null };
  if (text.indexOf(endMarker) < 0 || text.indexOf(endMarker) !== text.lastIndexOf(endMarker)) {
    throw new Error('Expected one handwritten handler insertion marker. Restore it or add the override manually.');
  }
  if (text.includes(handler.method)) throw new Error(`An existing ${handler.method} declaration needs review; it has been preserved.`);
  const eol = text.includes('\r\n') ? '\r\n' : '\n';
  const stub = [
    `    // litheview-handler ${handler.componentId} ${eventId(handler)} ${handler.method}`,
    `    ${signature}`, '    {', '        // Add application behavior here.', '    }', ''
  ].join(eol) + eol;
  text = text.replace(endMarker, stub + endMarker);
  return { text, offset: text.indexOf(signature), changed: true };
}

module.exports = { planHandlerEdit, validateBindings };
