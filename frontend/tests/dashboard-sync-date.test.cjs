const assert = require('node:assert/strict');
const { test } = require('node:test');
const { readFileSync } = require('node:fs');
const ts = require('typescript');
const compiled = ts.transpileModule(readFileSync(require('node:path').join(__dirname, '../src/app/dashboard/sync-date.ts'), 'utf8'), {
  compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022 },
}).outputText;
const exported = {};
new Function('exports', compiled)(exported);
const { formatSyncDate } = exported;

test('dashboard displays UTC sync completion on the correct Vietnam day', () => {
  const result = formatSyncDate('2026-11-03T18:08:00Z', 'en');
  assert.equal(result, '4 Nov 2026, 01:08');
  assert.equal(formatSyncDate('2026-11-03T18:08:00', 'en'), result);
  assert.equal(formatSyncDate('2026-11-04T01:08:00+07:00', 'en'), result);
  assert.match(formatSyncDate('2026-11-03T18:08:00Z', 'vi'), /01:08/);
});

test('invalid timestamps cannot break dashboard rendering', () => {
  assert.equal(formatSyncDate('invalid', 'en'), '—');
});
