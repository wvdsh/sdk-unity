import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import test from 'node:test';
import vm from 'node:vm';

const source = readFileSync(
  new URL('../Plugins/WebGL/WavedashPlugin.jslib', import.meta.url),
  'utf8',
);

for (const [name, window, expected] of [
  ['no browser window', undefined, 'en'],
  ['no host SDK', {}, 'en'],
  ['older SDK without the getter', { WavedashJS: {} }, 'en'],
  ['unavailable getter', { WavedashJS: { getUserLanguage: null } }, 'en'],
  [
    'unset preference',
    { WavedashJS: { getUserLanguage: () => undefined } },
    'en',
  ],
  ['null preference', { WavedashJS: { getUserLanguage: () => null } }, 'en'],
  ['empty preference', { WavedashJS: { getUserLanguage: () => '' } }, 'en'],
  [
    'invalid preference type',
    { WavedashJS: { getUserLanguage: () => 42 } },
    'en',
  ],
  ['saved preference', { WavedashJS: { getUserLanguage: () => 'fr' } }, 'fr'],
  [
    'saved regional preference',
    { WavedashJS: { getUserLanguage: () => 'pt-BR' } },
    'pt-BR',
  ],
  [
    'getter receiver',
    {
      WavedashJS: {
        language: 'zh-TW',
        getUserLanguage() {
          return this.language;
        },
      },
    },
    'zh-TW',
  ],
]) {
  test(name, () => {
    const library = {};
    vm.runInNewContext(source, {
      window,
      LibraryManager: { library },
      mergeInto: Object.assign,
      AllocUTF8: (value) => value,
    });
    assert.equal(library.WavedashJS_GetUserLanguage(), expected);
  });
}
