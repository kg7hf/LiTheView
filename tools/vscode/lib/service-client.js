// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

'use strict';

const { spawn } = require('node:child_process');
const MAX_LINE = 32 * 1024 * 1024;
const PROTOCOL_VERSION = 1;
const MAX_STDERR_TAIL = 4096;
// .NET host status codes; Windows reports them unsigned and other platforms signed.
const HOST_FRAMEWORK_MISSING = 0x80008096;
const HOST_STATUS_MIN = 0x80008081;
const HOST_STATUS_MAX = 0x800080ff;

/** Turns a service exit into a message a developer can act on. */
function describeExit(code, signal, dotnetPath, stderrTail = '') {
  const detail = String(stderrTail).split(/\r?\n/).map(line => line.trim()).filter(Boolean).slice(-1)[0];
  const suffix = detail ? ` Last message: ${detail.slice(0, 300)}` : '';
  if (signal) return `Designer service stopped (${signal}). Reopen the form to restart it.${suffix}`;
  const status = Number.isInteger(code) ? code >>> 0 : null;
  if (status === HOST_FRAMEWORK_MISSING) {
    return `Designer service could not start: "${dotnetPath}" does not have the .NET runtime the service needs. ` +
      'Install that runtime, or set litheview.dotnetPath to a dotnet executable that has it, then reopen the form.' + suffix;
  }
  if (status !== null && status >= HOST_STATUS_MIN && status <= HOST_STATUS_MAX) {
    return `Designer service could not start: the .NET host failed (0x${status.toString(16)}) using "${dotnetPath}". ` +
      'Check litheview.dotnetPath and the LiTheView output channel, then reopen the form.' + suffix;
  }
  return `Designer service exited (${code}). Reopen the form to restart it.${suffix}`;
}

class ServiceClient {
  constructor(options, dependencies = {}) {
    this.options = options;
    this.spawn = dependencies.spawn || spawn;
    this.platform = dependencies.platform || process.platform;
    this.nextId = 1;
    this.pending = new Map();
    this.buffer = '';
    this.disposed = false;
    this.ready = undefined;
    this.process = undefined;
    this.stderrTail = '';
  }

  async start() {
    if (this.ready) return this.ready;
    if (this.disposed) throw new Error('Designer service is closed. Reopen the form to restart it.');
    const isDll = this.options.servicePath.toLowerCase().endsWith('.dll');
    const command = isDll ? this.options.dotnetPath : this.options.servicePath;
    const args = [...(isDll ? [this.options.servicePath] : []), '--library-root', this.options.libraryRoot, '--preview-worker', this.options.previewWorkerPath];
    this.process = this.spawn(command, args, { cwd: this.options.applicationRoot, windowsHide: true, shell: false, stdio: ['pipe', 'pipe', 'pipe'] });
    this.process.stdout.setEncoding('utf8');
    this.process.stderr.setEncoding('utf8');
    this.process.stdout.on('data', data => this.receive(data));
    this.process.stderr.on('data', data => {
      this.stderrTail = (this.stderrTail + data).slice(-MAX_STDERR_TAIL);
      this.options.onLog?.(data);
    });
    this.process.stdin.on('error', error => { this.fail(error); this.terminate(); });
    this.process.on('error', error => { this.fail(error); this.terminate(); });
    this.process.on('exit', (code, signal) => this.fail(new Error(describeExit(code, signal, command, this.stderrTail))));
    this.ready = this.requestRaw('hello', { protocolVersion: PROTOCOL_VERSION }).then(result => {
      if (result?.protocolVersion !== PROTOCOL_VERSION) throw new Error('Designer service protocol version does not match this extension.');
      return result;
    }).catch(error => { this.fail(error); this.terminate(); throw error; });
    return this.ready;
  }

  async request(method, params) {
    await this.start();
    return this.requestRaw(method, params);
  }

  requestRaw(method, params) {
    if (this.disposed || !this.process) return Promise.reject(new Error('Designer service is closed. Reopen the form to restart it.'));
    const id = this.nextId++;
    return new Promise((resolve, reject) => {
      const timer = setTimeout(() => {
        this.fail(new Error(`Designer service ${method} timed out. Reopen the form to restart it.`));
        this.terminate();
      }, this.options.timeoutMs || 30000);
      this.pending.set(id, { resolve, reject, timer });
      const request = JSON.stringify({ protocolVersion: PROTOCOL_VERSION, id, method, params });
      try {
        this.process.stdin.write(`${request}\n`, error => { if (error) { this.fail(error); this.terminate(); } });
      } catch (error) { this.fail(error); this.terminate(); }
    });
  }

  receive(data) {
    this.buffer += data;
    let end;
    while ((end = this.buffer.indexOf('\n')) >= 0) {
      if (end > MAX_LINE) { this.fail(new Error('Designer response exceeds the size limit.')); this.terminate(); return; }
      const line = this.buffer.slice(0, end).trim();
      this.buffer = this.buffer.slice(end + 1);
      if (!line) continue;
      let response;
      try { response = JSON.parse(line); } catch { this.fail(new Error('Designer service returned an invalid JSON response.')); this.terminate(); return; }
      if (response.protocolVersion !== PROTOCOL_VERSION) { this.fail(new Error('Designer service protocol version mismatch.')); this.terminate(); return; }
      const pending = this.pending.get(response.id);
      if (!pending) continue;
      this.pending.delete(response.id);
      clearTimeout(pending.timer);
      if (response.ok) pending.resolve(response.result);
      else pending.reject(Object.assign(new Error(response.error?.message || 'Designer request failed.'), { code: response.error?.code }));
    }
    if (this.buffer.length > MAX_LINE) { this.fail(new Error('Designer response exceeds the size limit.')); this.terminate(); }
  }

  fail(error) {
    this.disposed = true;
    for (const pending of this.pending.values()) { clearTimeout(pending.timer); pending.reject(error); }
    this.pending.clear();
  }

  terminate() {
    if (!this.process || this.process.exitCode !== null) return;
    if (this.platform === 'win32' && this.process.pid) {
      // Select only the process we created; terminate its preview child too.
      const killer = this.spawn('taskkill.exe', ['/PID', String(this.process.pid), '/T', '/F'], { windowsHide: true, shell: false, stdio: 'ignore' });
      killer.on('error', () => this.process.kill());
    } else this.process.kill();
  }

  dispose() {
    if (!this.process) { this.disposed = true; return; }
    this.fail(new Error('Designer editor closed.'));
    this.process.stdin.end();
    const timer = setTimeout(() => this.terminate(), 1000);
    timer.unref?.();
    this.process.once('exit', () => clearTimeout(timer));
  }
}

module.exports = { PROTOCOL_VERSION, ServiceClient, describeExit };
