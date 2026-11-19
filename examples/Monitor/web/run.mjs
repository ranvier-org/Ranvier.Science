// Runs a script of the page in a headless Chrome and prints the lines the page reported.
// Usage: node run.mjs <name> [--picture <png>]
// It serves this folder with serve.mjs on a free port, starts its own Chrome with its own profile folder, and stops
// both by the processes it started. It exits with the code of the page's `script done <code>` line, or 1 without one.
import { execFileSync, spawn } from "node:child_process";
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const here = path.dirname(fileURLToPath(import.meta.url));
const log = path.join(here, "probe.log");
const profile = path.join(here, ".chrome-profile");
const chromePath = process.env.CHROME || "C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe";
const waitSeconds = 180;
const sleep = (ms) => new Promise((resolve) => setTimeout(resolve, ms));

const args = process.argv.slice(2);
const name = args[0];
const pictureAt = args.indexOf("--picture");
const picture = pictureAt >= 0 && args[pictureAt + 1] ? path.resolve(args[pictureAt + 1]) : null;

if (!name || name.startsWith("--") || (pictureAt >= 0 && !picture)) {
  console.error("usage: node run.mjs <name> [--picture <png>]");
  process.exit(2);
}

const readLog = () => (fs.existsSync(log) ? fs.readFileSync(log, "utf8") : "");
const doneLine = () => /^script done (-?\d+)$/m.exec(readLog());

//Resolves when the process has exited, or after `ms` milliseconds; true when it exited.
const exitWithin = (child, ms) =>
  child.exitCode !== null || child.signalCode !== null
    ? Promise.resolve(true)
    : Promise.race([new Promise((resolve) => child.once("exit", () => resolve(true))), sleep(ms).then(() => false)]);

//Ends a process and the processes it started, by its process id.
const stopTree = (child) => {
  if (child.exitCode !== null || child.signalCode !== null) return;
  try {
    execFileSync("taskkill", ["/PID", String(child.pid), "/T", "/F"], { stdio: "ignore" });
  } catch {
    child.kill();
  }
};

//Asks the Chrome started here to close through its DevTools endpoint, then ends it by its process id if it is
//still running after 10 s.
const stopChrome = async (chrome) => {
  try {
    const [port, target] = fs.readFileSync(path.join(profile, "DevToolsActivePort"), "utf8").split(/\r?\n/);
    const socket = new WebSocket(`ws://127.0.0.1:${port}${target}`);
    await new Promise((resolve, reject) => {
      socket.onopen = resolve;
      socket.onerror = reject;
    });
    socket.send(JSON.stringify({ id: 1, method: "Browser.close" }));
  } catch {}
  if (!(await exitWithin(chrome, 10000))) stopTree(chrome);
};

fs.writeFileSync(log, "");
fs.rmSync(path.join(profile, "DevToolsActivePort"), { force: true });
if (picture) fs.rmSync(picture, { force: true });

const server = spawn(process.execPath, [path.join(here, "serve.mjs"), "0"], { stdio: ["ignore", "pipe", "inherit"] });
let chrome = null;
let code = 1;

try {
  const url = await new Promise((resolve, reject) => {
    let text = "";
    server.stdout.on("data", (chunk) => {
      text += chunk;
      const found = /http:\/\/\S+/.exec(text);
      if (found) resolve(found[0]);
    });
    server.once("exit", (exit) => reject(new Error(`serve.mjs exited with code ${exit}`)));
  });

  const chromeArgs = [
    "--headless=new",
    `--user-data-dir=${profile}`,
    "--no-first-run",
    "--no-default-browser-check",
    "--disable-features=Translate",
    "--window-size=1280,900",
    ...(picture ? [`--screenshot=${picture}`, "--virtual-time-budget=20000"] : ["--remote-debugging-port=0"]),
    `${url}?script=${encodeURIComponent(name)}`,
  ];
  chrome = spawn(chromePath, chromeArgs, { stdio: "ignore" });

  if (picture) {
    if (!(await exitWithin(chrome, waitSeconds * 1000))) console.log(`Chrome did not exit in ${waitSeconds} s`);
  } else {
    for (let waited = 0; waited < waitSeconds * 1000 && !doneLine(); waited += 250) await sleep(250);
    await stopChrome(chrome);
  }

  process.stdout.write(readLog());
  const done = doneLine();
  if (done) code = Number(done[1]);
  else console.log(`no 'script done' line in ${waitSeconds} s`);
  if (picture) console.log(fs.existsSync(picture) ? `picture: ${picture}` : `no picture was written to ${picture}`);
} finally {
  if (chrome) stopTree(chrome);
  stopTree(server);
}

process.exit(code);
