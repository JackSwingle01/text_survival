// Static server for a published wwwroot. Serves the precompressed .br sibling when the browser
// accepts it, as a CDN would. node serve.mjs <wwwroot> [port]
import http from 'node:http';
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const TYPES = { '.html': 'text/html', '.js': 'text/javascript', '.mjs': 'text/javascript', '.wasm': 'application/wasm',
  '.json': 'application/json', '.png': 'image/png', '.dat': 'application/octet-stream', '.css': 'text/css', '.ico': 'image/x-icon' };

export function serve(rootDir, port = 0) {
  const root = path.resolve(rootDir);
  const server = http.createServer((req, res) => {
    const url = decodeURIComponent(new URL(req.url, 'http://x').pathname);
    let file = path.join(root, url === '/' ? 'index.html' : url);
    if (!file.startsWith(root) || !fs.existsSync(file) || fs.statSync(file).isDirectory()) { res.writeHead(404); return res.end(); }
    const headers = { 'Content-Type': TYPES[path.extname(file)] ?? 'application/octet-stream', 'Cache-Control': 'no-store' };
    if ((req.headers['accept-encoding'] ?? '').includes('br') && fs.existsSync(file + '.br')) { headers['Content-Encoding'] = 'br'; file += '.br'; }
    res.writeHead(200, headers);
    fs.createReadStream(file).pipe(res);
  });
  return new Promise(resolve => server.listen(port, '127.0.0.1', () => resolve({ server, url: `http://127.0.0.1:${server.address().port}/` })));
}

if (process.argv[1] === fileURLToPath(import.meta.url)) {
  const { url } = await serve(process.argv[2] ?? '.', +(process.argv[3] ?? 8183));
  console.log(`serving ${process.argv[2]} on ${url}`);
}
