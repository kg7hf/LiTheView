// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

'use strict';

const assert = require('node:assert/strict');
const test = require('node:test');
const { EventEmitter } = require('node:events');
const { ServiceClient } = require('../lib/service-client');

function fixture(options = {}) {
  const child = new EventEmitter();
  child.pid = 417;
  child.exitCode = null;
  child.stdout = new EventEmitter();
  child.stderr = new EventEmitter();
  child.stdout.setEncoding = child.stderr.setEncoding = () => {};
  child.stdin = new EventEmitter();
  const sent = [];
  child.stdin.write = (line, callback) => { sent.push(JSON.parse(line)); callback?.(); };
  child.stdin.end = () => { child.ended = true; child.exitCode = 0; child.emit('exit', 0); };
  child.kill = () => { child.killed = true; child.exitCode = 1; child.emit('exit', 1); };
  const spawned = [];
  const client = new ServiceClient({ servicePath: 'D:/library path/service.dll', dotnetPath: 'dotnet', libraryRoot: 'D:/library path', applicationRoot: 'D:/application', previewWorkerPath: 'D:/worker.exe', timeoutMs: 1000, ...options }, {
    platform: 'win32',
    spawn(command, args, settings) {
      spawned.push({ command, args, settings });
      return spawned.length === 1 ? child : new EventEmitter();
    }
  });
  const respond = (id, result, extra = {}) => child.stdout.emit('data', `${JSON.stringify({ protocolVersion: 1, id, ok: true, result, ...extra })}\n`);
  const start = async () => { const promise = client.start(); respond(1, { protocolVersion: 1 }); await promise; };
  return { child, client, sent, spawned, respond, start };
}

test('launch is shell-free and preserves paths containing spaces', async () => {
  const f = fixture();
  await f.start();
  assert.equal(f.spawned[0].command, 'dotnet');
  assert.deepEqual(f.spawned[0].args, ['D:/library path/service.dll', '--library-root', 'D:/library path', '--preview-worker', 'D:/worker.exe']);
  assert.equal(f.spawned[0].settings.shell, false);
  assert.equal(f.spawned[0].settings.windowsHide, true);
  assert.equal(f.spawned[0].settings.cwd, 'D:/application');
  f.client.dispose();
  assert.equal(f.child.ended, true);
});

test('NDJSON response chunks route by request ID, not response order', async () => {
  const f = fixture(); await f.start();
  const first = f.client.request('inspect', { xml: 'a' });
  const second = f.client.request('render', { xml: 'b' });
  await new Promise(setImmediate);
  const payload = JSON.stringify({ protocolVersion: 1, id: 3, ok: true, result: { marker: 'second' } });
  f.child.stdout.emit('data', payload.slice(0, 9));
  f.child.stdout.emit('data', `${payload.slice(9)}\n`);
  f.respond(2, { marker: 'first' });
  assert.deepEqual(await first, { marker: 'first' });
  assert.deepEqual(await second, { marker: 'second' });
  assert.equal(f.sent.length, 3);
  f.client.dispose();
});

test('service failure preserves service for subsequent editing', async () => {
  const f = fixture(); await f.start();
  const pending = f.client.request('render', {}); await new Promise(setImmediate);
  f.respond(2, undefined, { ok: false, error: { code: 'PreviewMissing', message: 'Worker missing' } });
  await assert.rejects(pending, { message: 'Worker missing', code: 'PreviewMissing' });
  const next = f.client.request('inspect', {}); await new Promise(setImmediate); f.respond(3, { valid: true });
  assert.deepEqual(await next, { valid: true });
  f.client.dispose();
});

test('handshake protocol mismatch rejects and terminates exact process tree', async () => {
  const f = fixture(); const pending = f.client.start();
  f.respond(1, { protocolVersion: 2 });
  await assert.rejects(pending, /protocol version/);
  assert.equal(f.spawned[1].command, 'taskkill.exe');
  assert.deepEqual(f.spawned[1].args, ['/PID', '417', '/T', '/F']);
});

test('malformed protocol rejects pending requests', async () => {
  const f = fixture(); await f.start();
  const pending = f.client.request('inspect', {}); await new Promise(setImmediate);
  f.child.stdout.emit('data', 'not-json\n');
  await assert.rejects(pending, /invalid JSON/);
  assert.equal(f.spawned.length, 2);
});

test('timeout rejects request and tears down worker tree', async () => {
  const f = fixture({ timeoutMs: 5 }); await f.start();
  await assert.rejects(f.client.request('render', {}), /timed out/);
  assert.equal(f.spawned[1].command, 'taskkill.exe');
});

test('closing rejects outstanding requests and sends EOF', async () => {
  const f = fixture(); await f.start();
  const pending = f.client.request('inspect', {}); await new Promise(setImmediate);
  f.client.dispose();
  await assert.rejects(pending, /closed/);
  assert.equal(f.child.ended, true);
});
