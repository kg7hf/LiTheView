// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only
(() => {
  'use strict';
  // Both setup screens receive a service-validated, bounded glass definition.
  window.litheviewGlassPreview = {
    render(glass) {
      document.getElementById('glass-summary').textContent = `${glass.id}: ${glass.viewWidth} × ${glass.viewHeight} layout; ${glass.segmentCount} logical segments; ${Math.ceil(glass.segmentCount / 8)} bytes of segment state.`;
      document.getElementById('glass-fields').textContent = [
        ...glass.texts.map(f => `${f.id}: text, ${f.cells.length} characters`),
        ...glass.numbers.map(f => `${f.id}: number, ${f.cells.length} digits`),
        ...glass.icons.map(f => `${f.id}: icon`), ...glass.groups.map(f => `${f.id}: group, 0–${f.maximum}`)
      ].join(' · ');
      const canvas = document.getElementById('glass-preview'); canvas.hidden = false;
      const ctx = canvas.getContext('2d'); ctx.resetTransform(); ctx.clearRect(0, 0, canvas.width, canvas.height);
      const scale = Math.min(canvas.width / glass.viewWidth, canvas.height / glass.viewHeight);
      ctx.scale(scale, scale); ctx.fillStyle = '#888888';
      for (const shape of glass.shapes) {
        ctx.beginPath(); shape.polygon.forEach((p, i) => i ? ctx.lineTo(p.x, p.y) : ctx.moveTo(p.x, p.y)); ctx.closePath(); ctx.fill();
      }
    }
  };
})();
