const assert = require('node:assert/strict');
const { test, beforeEach } = require('node:test');
const { readFileSync } = require('node:fs');
const { resolve } = require('node:path');
const Module = require('node:module');
const ts = require('typescript');
require('@angular/compiler');
const { createEnvironmentInjector, runInInjectionContext } = require('@angular/core');
const { HttpErrorResponse, HttpRequest, HttpResponse } = require('@angular/common/http');
const { defer, lastValueFrom, of, throwError } = require('rxjs');

// Exercise the actual Angular service/interceptor with Node's test runner. No DOM or
// browser is needed for HTTP error and persisted-session behavior.
function loadTypeScript(relativePath) {
  const filename = resolve(__dirname, '..', relativePath);
  const output = ts.transpileModule(readFileSync(filename, 'utf8'), {
    compilerOptions: { module: ts.ModuleKind.CommonJS, experimentalDecorators: true },
  }).outputText;
  const loaded = new Module(filename, module);
  loaded.filename = filename;
  loaded.paths = Module._nodeModulePaths(resolve(__dirname, '..'));
  loaded._compile(output, filename);
  return loaded.exports;
}
const { AuthenticationService } = loadTypeScript('src/app/core/auth/auth.service.ts');
// Resolve the interceptor's service import to this same constructor.
const servicePath = resolve(__dirname, '../src/app/core/auth/auth.service.ts');
require.cache[servicePath] = { exports: { AuthenticationService } };
require.extensions['.ts'] = (loaded, filename) => {
  loaded._compile(ts.transpileModule(readFileSync(filename, 'utf8'), {
    compilerOptions: { module: ts.ModuleKind.CommonJS, experimentalDecorators: true },
  }).outputText, filename);
};
const { authInterceptor } = loadTypeScript('src/app/core/auth/auth.interceptor.ts');

beforeEach(() => {
  const storage = new Map();
  Object.defineProperty(globalThis, 'localStorage', {
    configurable: true,
    value: {
      getItem: key => storage.get(key) ?? null,
      setItem: (key, value) => storage.set(key, value),
      removeItem: key => storage.delete(key),
    },
  });
  localStorage.setItem('olympia_student_360_token', 'saved-token');
});

for (const status of [0, 502, 503, 504]) {
  test(`HTTP ${status} preserves session and retries validation on next navigation`, async () => {
    let calls = 0;
    const service = new AuthenticationService({ apiAuthMeGet: () => {
      calls++;
      return throwError(() => new HttpErrorResponse({ status }));
    } }, {});
    assert.equal(await lastValueFrom(service.ensureCurrentUser()), true);
    assert.equal(service.getToken(), 'saved-token');
    await lastValueFrom(service.ensureCurrentUser());
    assert.equal(calls, 2);
  });
}

for (const status of [401, 403]) {
  test(`HTTP ${status} clears the rejected session`, async () => {
    const service = new AuthenticationService({
      apiAuthMeGet: () => throwError(() => new HttpErrorResponse({ status })),
    }, {});
    assert.equal(await lastValueFrom(service.ensureCurrentUser()), false);
    assert.equal(service.getToken(), null);
  });
}

for (const [method, status, expectedCalls] of [['GET', 502, 3], ['POST', 503, 1], ['GET', 401, 1]]) {
  test(`${method} HTTP ${status} makes ${expectedCalls} attempts`, async () => {
    let calls = 0;
    const injector = createEnvironmentInjector([
      { provide: AuthenticationService, useValue: { getToken: () => 'saved-token' } },
    ]);
    try {
      const response = runInInjectionContext(injector, () => authInterceptor(
        new HttpRequest(method, '/api/test', null), request => {
          assert.equal(request.headers.get('Authorization'), 'Bearer saved-token');
          assert.equal(request.withCredentials, true);
          return defer(() => {
            if (++calls < 3 || method === 'POST' || status === 401)
              return throwError(() => new HttpErrorResponse({ status }));
            return of(new HttpResponse({ status: 200 }));
          });
        }));
      if (expectedCalls === 3) assert.equal((await lastValueFrom(response)).status, 200);
      else await assert.rejects(lastValueFrom(response), error => error.status === status);
      assert.equal(calls, expectedCalls);
    } finally { injector.destroy(); }
  });
}
