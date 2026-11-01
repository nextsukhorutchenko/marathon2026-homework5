import { createServer } from 'node:http';
import { createReadStream, existsSync, statSync } from 'node:fs';
import { resolve, relative, extname, sep } from 'node:path';
import { pathToFileURL } from 'node:url';

export function createSpaServer(directory) {
  const root = resolve(directory);
  if (!existsSync(resolve(root, 'index.html'))) throw new Error('Build the production frontend first: index.html is missing.');
  const types = { '.html': 'text/html; charset=utf-8', '.js': 'text/javascript', '.mjs': 'text/javascript', '.css': 'text/css', '.json': 'application/json', '.svg': 'image/svg+xml', '.png': 'image/png', '.jpg': 'image/jpeg', '.woff2': 'font/woff2', '.ico': 'image/x-icon' };
  return createServer((request, response) => {
    let pathname;
    try { pathname = decodeURIComponent(new URL(request.url, 'http://localhost').pathname); }
    catch { response.writeHead(400).end(); return; }
    if (pathname === '/__aqa_health') { response.writeHead(200).end('ok'); return; }
    if (!['GET', 'HEAD'].includes(request.method)) { response.writeHead(405).end(); return; }
    let file = resolve(root, '.' + pathname);
    const child = relative(root, file);
    if (child === '..' || child.startsWith('..' + sep) || child.includes(':')) { response.writeHead(403).end(); return; }
    if (!existsSync(file) || !statSync(file).isFile()) {
      if (extname(pathname)) { response.writeHead(404).end(); return; }
      file = resolve(root, 'index.html');
    }
    response.writeHead(200, { 'Content-Type': types[extname(file)] || 'application/octet-stream', 'Cache-Control': 'no-store' });
    if (request.method === 'HEAD') response.end();
    else createReadStream(file).on('error', () => response.destroy()).pipe(response);
  });
}

if (process.argv[1] && import.meta.url === pathToFileURL(resolve(process.argv[1])).href) {
  const port = Number(process.env.AQA_UI_PORT || 4200);
  if (!Number.isInteger(port) || port < 1 || port > 65535) throw new Error('Invalid AQA_UI_PORT');
  const server = createSpaServer(process.argv[2] || '../../frontend/restaurant-frontend-app/dist/restaurant-frontend-app/browser');
  server.listen(port, '127.0.0.1', () => process.stdout.write(`Production UI ready on loopback port ${port}\n`));
  for (const signal of ['SIGINT', 'SIGTERM']) process.on(signal, () => server.close());
}
