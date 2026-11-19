// A copy of AstroShowcase.Web/web/serve.mjs, with comments for this page.
// Serves this folder on 127.0.0.1 and prints its URL; with no port, or port 0, on a free port.
// Usage: node serve.mjs [port]
import fs from "node:fs";
import http from "node:http";
import path from "node:path";
import { fileURLToPath } from "node:url";

const root = path.dirname(fileURLToPath(import.meta.url));
const types = { ".html": "text/html", ".js": "text/javascript" };

const server = http.createServer((request, response) => {
  // The page posts here each line it reports, its errors among them; they are appended to probe.log beside this
  // file, which run.mjs reads.
  if (request.method === "POST" && request.url === "/log") {
    let body = "";
    request.on("data", (chunk) => (body += chunk));
    request.on("end", () => {
      fs.appendFileSync(path.join(root, "probe.log"), body + "\n");
      response.writeHead(204).end();
    });
    return;
  }
  // The page reads its query itself (?script=<name>), so the query plays no part in finding the file.
  const name = decodeURIComponent(request.url.split("?")[0]).replace(/^\/+/, "") || "index.html";
  const file = path.join(root, name);
  if (!file.startsWith(root) || !fs.existsSync(file) || !fs.statSync(file).isFile()) {
    response.writeHead(404).end("Not found");
    return;
  }
  response.writeHead(200, { "content-type": types[path.extname(file)] ?? "application/octet-stream" });
  fs.createReadStream(file).pipe(response);
});

server.listen(Number(process.argv[2] ?? 0), "127.0.0.1", () => console.log(`http://127.0.0.1:${server.address().port}/`));
