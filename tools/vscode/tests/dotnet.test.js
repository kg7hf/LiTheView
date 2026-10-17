// SPDX-FileCopyrightText: 2026 Paul R. Decker
// SPDX-License-Identifier: GPL-3.0-only

'use strict';

const test = require('node:test');
const assert = require('node:assert/strict');
const { requiredFramework, parseRuntimes, candidates, resolveDotnet } = require('../lib/dotnet');
const { describeExit } = require('../lib/service-client');

const runtimeConfig = JSON.stringify({ runtimeOptions: { tfm: 'net10.0', framework: { name: 'Microsoft.NETCore.App', version: '10.0.0' } } });
const readFile = async () => runtimeConfig;
const listing = versions => versions.map(version =>
    `Microsoft.NETCore.App ${version} [C:\\Program Files\\dotnet\\shared\\Microsoft.NETCore.App]`).join('\r\n');
const env = { USERPROFILE: 'C:\\Users\\dev', ProgramFiles: 'C:\\Program Files' };
const servicePath = 'C:\\lib\\build\\vscode-designer\\service\\LitheView.Designer.Service.dll';
const userDotnet = 'C:\\Users\\dev\\.dotnet\\dotnet.exe';

test('the required framework comes from the service runtimeconfig.json', async () => {
    let read;
    const framework = await requiredFramework(servicePath, async file => { read = file; return runtimeConfig; });
    assert.equal(read, 'C:\\lib\\build\\vscode-designer\\service\\LitheView.Designer.Service.runtimeconfig.json');
    assert.deepEqual(framework, { name: 'Microsoft.NETCore.App', version: '10.0.0', major: 10, minor: 0 });
    assert.equal(await requiredFramework(servicePath, async () => { throw new Error('missing'); }), null);
});

test('runtime listings parse and candidates follow the documented order', () => {
    assert.deepEqual(parseRuntimes('Microsoft.AspNetCore.App 10.0.2 [x]\nMicrosoft.NETCore.App 9.0.11 [y]\n\njunk'), [
        { name: 'Microsoft.AspNetCore.App', major: 10, minor: 0, version: '10.0.2' },
        { name: 'Microsoft.NETCore.App', major: 9, minor: 0, version: '9.0.11' }
    ]);
    assert.deepEqual(candidates({ ...env, DOTNET_ROOT: 'D:\\dotnet' }, 'win32'),
        ['D:\\dotnet\\dotnet.exe', userDotnet, 'dotnet', 'C:\\Program Files\\dotnet\\dotnet.exe']);
});

test('the default setting finds the per-user install when PATH has an older runtime', async () => {
    const asked = [];
    const result = await resolveDotnet({ configured: 'dotnet', servicePath, env, platform: 'win32' }, {
        readFile,
        listRuntimes: async candidate => {
            asked.push(candidate);
            return candidate === userDotnet ? parseRuntimes(listing(['10.0.12'])) : parseRuntimes(listing(['8.0.11']));
        }
    });
    assert.equal(result.dotnetPath, userDotnet);
    assert.deepEqual(asked, [userDotnet]);
});

test('PATH is used when it has the runtime and the per-user install is absent', async () => {
    const result = await resolveDotnet({ configured: '', servicePath, env, platform: 'win32' }, {
        readFile,
        listRuntimes: async candidate => candidate === 'dotnet' ? parseRuntimes(listing(['9.0.1', '10.1.0'])) : null
    });
    assert.equal(result.dotnetPath, 'dotnet');
});

test('a missing runtime is reported with what each candidate has', async () => {
    await assert.rejects(resolveDotnet({ configured: 'dotnet', servicePath, env, platform: 'win32' }, {
        readFile,
        listRuntimes: async candidate => candidate === 'dotnet' ? parseRuntimes(listing(['8.0.11', '11.0.0'])) : null
    }), error => {
        assert.match(error.message, /needs the \.NET 10 runtime/);
        assert.match(error.message, /dotnet \(has 8\.0\.11, 11\.0\.0\)/);
        assert.match(error.message, /C:\\Users\\dev\\\.dotnet\\dotnet\.exe \(not found or did not start\)/);
        assert.match(error.message, /set litheview\.dotnetPath/);
        return true;
    });
});

test('an explicit setting is checked but never replaced', async () => {
    const asked = [];
    await assert.rejects(resolveDotnet({ configured: 'E:\\sdk\\dotnet.exe', servicePath, env, platform: 'win32' }, {
        readFile,
        listRuntimes: async candidate => { asked.push(candidate); return parseRuntimes(listing(['9.0.0'])); }
    }), /litheview\.dotnetPath does not provide it/);
    assert.deepEqual(asked, ['E:\\sdk\\dotnet.exe']);
    const selfContained = await resolveDotnet({ configured: '', servicePath: 'C:\\svc\\Service.exe', env, platform: 'win32' });
    assert.equal(selfContained.framework, null);
});

test('service exits explain .NET host failures in both signed and unsigned form', () => {
    const missing = describeExit(2147516566, null, 'dotnet', 'You must install or update .NET to run this application.\r\n');
    assert.match(missing, /"dotnet" does not have the \.NET runtime/);
    assert.match(missing, /Last message: You must install or update \.NET/);
    assert.equal(describeExit(-2147450730, null, 'dotnet'), describeExit(2147516566, null, 'dotnet'));
    assert.match(describeExit(0x80008083, null, 'dotnet'), /\.NET host failed \(0x80008083\)/);
    assert.match(describeExit(3, null, 'dotnet', 'boom'), /exited \(3\)\. Reopen the form to restart it\. Last message: boom/);
    assert.match(describeExit(null, 'SIGTERM', 'dotnet'), /stopped \(SIGTERM\)/);
});
