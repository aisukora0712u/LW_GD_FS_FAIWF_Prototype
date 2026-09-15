import http from 'node:http';
import { readFile } from 'node:fs/promises';
import { fileURLToPath } from 'node:url';
import path from 'node:path';
import '../Docs/Workflow-Site/dist/report-data.js';

// Zero-dependency loopback preview. Only named report assets and cited sources are served.
const root = fileURLToPath(new URL('../', import.meta.url));
const site = path.join(root, 'Docs', 'Workflow-Site', 'dist');
const args = process.argv.slice(2);
if (args.length && (args.length !== 2 || args[0] !== '--port' || !/^\d+$/.test(args[1]))) {
  console.error('Usage: node Tools/Preview-Workflow.mjs [--port 4173]');
  process.exit(1);
}
const port = args.length ? Number(args[1]) : 4173;
if (!Number.isInteger(port) || port < 1024 || port > 65535) {
  console.error('Port must be between 1024 and 65535.');
  process.exit(1);
}
const assets = new Map([
  ['/', ['index.html', 'text/html; charset=utf-8']],
  ['/index.html', ['index.html', 'text/html; charset=utf-8']],
  ['/styles.css', ['styles.css', 'text/css; charset=utf-8']],
  ['/app.js', ['app.js', 'text/javascript; charset=utf-8']],
  ['/report-data.js', ['report-data.js', 'text/javascript; charset=utf-8']]
]);
const sources = new Set(Object.values(globalThis.WORKFLOW_REPORT.sources).map(source => source.path));
const server = http.createServer(async (request, response) => {
  response.setHeader('X-Content-Type-Options', 'nosniff');
  response.setHeader('Cache-Control', 'no-store');
  if (!['GET', 'HEAD'].includes(request.method)) {
    response.writeHead(405, { Allow: 'GET, HEAD' });
    response.end('Method not allowed');
    return;
  }
  let pathname;
  try { pathname = decodeURIComponent(new URL(request.url, `http://127.0.0.1:${port}`).pathname); }
  catch { response.writeHead(400); response.end('Bad request'); return; }
  const asset = assets.get(pathname);
  const source = pathname.startsWith('/source/') ? pathname.slice(8) : '';
  const file = asset ? path.join(site, asset[0]) : sources.has(source) ? path.join(root, source) : null;
  if (!file) { response.writeHead(404); response.end('Not found'); return; }
  try {
    const body = await readFile(file);
    response.writeHead(200, { 'Content-Type': asset ? asset[1] : 'text/plain; charset=utf-8', 'Content-Length': body.length });
    response.end(request.method === 'HEAD' ? undefined : body);
  } catch (error) {
    response.writeHead(error.code === 'ENOENT' ? 404 : 500);
    response.end(error.code === 'ENOENT' ? 'Not found' : 'Unable to read file');
  }
});
server.on('error', error => {
  console.error(error.code === 'EADDRINUSE' ? `Port ${port} is in use. Select another with --port.` : error.message);
  process.exitCode = 1;
});
server.listen(port, '127.0.0.1', () => console.log(`WorkFlow report: http://127.0.0.1:${port}/`));
