const assert = require('node:assert/strict');
const { test } = require('node:test');
const { readFileSync } = require('node:fs');
const ts = require('typescript');
require('@angular/compiler');
const { createEnvironmentInjector, runInInjectionContext } = require('@angular/core');
const { HttpClient } = require('@angular/common/http');
const { of } = require('rxjs');
require.extensions['.ts'] = (loaded, filename) => loaded._compile(ts.transpileModule(readFileSync(filename, 'utf8'), {
  compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022, experimentalDecorators: true },
}).outputText, filename);
const { UserAccessManagementComponent } = require('../src/app/admin/user-access-management.component.ts');
const { AccountManagementComponent } = require('../src/app/admin/account-management.component.ts');
const { UserAccessService } = require('../src/app/admin/user-access.service.ts');
const { BASE_PATH } = require('../src/app/api/variables.ts');

const scopes = ['All', 'Academic', 'Health', 'Bus', 'Class', 'SchoolLevel', 'Grade'];
const grades = Array.from({ length: 12 }, (_, index) => String(index + 1));
const teacher = { userId: 'teacher', id: 'teacher', username: 'teacher', fullName: 'Teacher', role: 'Teacher',
  scopes: ['Grade'], accessScopes: ['Grade'], homerooms: [], schoolLevels: [], gradeLevels: ['8'] };
const language = { t: key => key };

test('access editor loads grade assignments, requires a selection and saves combined scopes', () => {
  const calls = [];
  const service = {
    load: () => of({ users: [teacher], availableScopes: scopes, availableHomerooms: ['9A'],
      availableSchoolLevels: ['Primary', 'LowerSecondary', 'UpperSecondary'], availableGradeLevels: grades }),
    update: (userId, selected, homerooms, schoolLevels, gradeLevels) => {
      calls.push({ selected, homerooms, schoolLevels, gradeLevels });
      return of({ ...teacher, scopes: selected, homerooms, schoolLevels, gradeLevels });
    },
  };
  const component = new UserAccessManagementComponent(service, language);
  component.ngOnInit();
  assert.deepEqual(component.gradeLevels(), grades);
  assert.deepEqual([...component.selectedGradeLevels()], ['8']);
  component.toggleGradeLevel('8');
  component.save();
  assert.equal(component.error(), 'access.selectGrade');
  assert.equal(calls.length, 0);
  component.toggleGradeLevel('9');
  component.toggleScope('Class');
  component.toggleHomeroom('9A');
  component.save();
  assert.deepEqual(calls[0], { selected: ['Grade', 'Class'], homerooms: ['9A'], schoolLevels: [], gradeLevels: ['9'] });
  component.toggleScope('All');
  component.save();
  assert.deepEqual(calls[1], { selected: ['All'], homerooms: [], schoolLevels: [], gradeLevels: [] });
});

test('grade permissions are included in the actual access API request', () => {
  let sent;
  const injector = createEnvironmentInjector([
    { provide: HttpClient, useValue: { put: (url, body) => { sent = { url, body }; return of(teacher); } } },
    { provide: BASE_PATH, useValue: 'http://test' },
  ]);
  try {
    runInInjectionContext(injector, () => new UserAccessService().update('teacher', ['Grade'], [], [], ['8', '9']).subscribe());
    assert.deepEqual(sent, { url: 'http://test/api/user-access/teacher',
      body: { scopes: ['Grade'], homerooms: [], schoolLevels: [], gradeLevels: ['8', '9'] } });
  } finally { injector.destroy(); }
});

test('account editor validates grade selections and includes them in update requests', () => {
  const calls = [];
  const injector = createEnvironmentInjector([]);
  const originalWindow = globalThis.window;
  globalThis.window = { setTimeout: () => undefined };
  const service = {
    apiAdminUsersOptionsGet: () => of({ roles: ['Teacher'], accessScopes: scopes,
      homerooms: [], schoolLevels: ['Primary', 'LowerSecondary', 'UpperSecondary'], gradeLevels: grades }),
    apiAdminUsersIdPut: (id, body) => { calls.push(body); return of(teacher); },
    apiAdminUsersGet: () => of({ items: [], totalCount: 0, totalPages: 1 }),
  };
  try {
    const component = runInInjectionContext(injector, () => new AccountManagementComponent(service, language));
    component.loadOptions();
    component.edit(teacher);
    assert.deepEqual(component.form.gradeLevels, ['8']);
    component.toggleGradeLevel('8');
    component.save();
    assert.equal(calls.length, 0);
    component.toggleGradeLevel('9');
    component.save();
    assert.deepEqual(calls[0].gradeLevels, ['9']);
    assert.deepEqual(calls[0].accessScopes, ['Grade']);
  } finally { injector.destroy(); globalThis.window = originalWindow; }
});

test('grade controls wait for backend support during a rolling deployment', () => {
  const component = new UserAccessManagementComponent({ load: () => of({ users: [teacher],
    availableScopes: ['All', 'Class', 'SchoolLevel'], availableHomerooms: [], availableSchoolLevels: [] }) }, language);
  component.ngOnInit();
  assert.equal(component.scopes().includes('Grade'), false);
  assert.deepEqual(component.gradeLevels(), []);
});
