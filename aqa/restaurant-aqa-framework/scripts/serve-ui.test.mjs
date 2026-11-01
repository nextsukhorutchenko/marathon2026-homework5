import { test } from 'node:test';
import assert from 'node:assert/strict';
import { mkdtempSync, writeFileSync, rmSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { createSpaServer } from './serve-ui.mjs';

test('SPA routes use index.html while missing assets return 404 and traversal is refused', async () => {
  const directory = mkdtempSync(join(tmpdir(), 'aqa-spa-'));
  writeFileSync(join(directory, 'index.html'), '<h1>App</h1>');
  const server = createSpaServer(directory);
  await new Promise(resolve => server.listen(0, '127.0.0.1', resolve));
  const url = `http://127.0.0.1:${server.address().port}`;
  try {
    assert.equal(await (await fetch(url + '/reservations/fixture/group-order')).text(), '<h1>App</h1>');
    assert.equal((await fetch(url + '/missing.js')).status, 404);
    assert.equal((await fetch(url + '/..%2fsecret.txt')).status, 403);
    assert.equal(await (await fetch(url + '/__aqa_health')).text(), 'ok');
  } finally {
    await new Promise(resolve => server.close(resolve));
    rmSync(directory, { recursive: true });
  }
});
