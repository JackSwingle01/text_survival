// Static server for a published wwwroot. Serves the precompressed .br sibling when the browser
// accepts it, as a CDN would, so transfer sizes are the real compressed download.
// node serve.mjs <wwwroot> <port>
import http from 'node:http';
import fs from 'node:fs';
import path from 'node:path';

const root = path.resolve(process.argv[2]);
const port = +(process.argv[3] ?? 8080);
const types = { '.html': 'text/html', '.js': 'text/javascript', '.mjs': 'text/javascript', '.wasm': 'application/wasm',
  '.json': 'application/json', '.png': 'image/png', '.dat': 'application/octet-stream', '.dll': 'application/octet-stream',
  '.webcil': 'application/octet-stream', '.pdb': 'application/octet-stream', '.css': 'text/css' };

http.createServer((req, res) => {
  const url = decodeURIComponent(new URL(req.url, 'http://x').pathname);
  let file = path.join(root, url === '/' ? 'index.html' : url);
  if (!file.startsWith(root) || !fs.existsSync(file) || fs.statSync(file).isDirectory()) { res.writeHead(404); return res.end(); }
  const headers = { 'Content-Type': types[path.extname(file)] ?? 'application/octet-stream', 'Cache-Control': 'no-store' };
  if ((req.headers['accept-encoding'] ?? '').includes('br') && fs.existsSync(file + '.br')) {
    headers['Content-Encoding'] = 'br';
    file += '.br';
  }
  res.writeHead(200, headers);
  fs.createReadStream(file).pipe(res);
}).listen(port, () => console.log(`serving ${root} on http://localhost:${port}`));
