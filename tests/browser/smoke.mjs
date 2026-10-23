// Browser smoke test for SchichtWerk.
//
// Starts headless Chrome, drives the published app through the Chrome DevTools Protocol
// and checks what a visitor sees: KPI values for every scenario, disabled buttons, reset,
// chart tooltip via mouse, keyboard and touch, keyboard focus, layout at several widths.
// No npm packages: only Node built-ins (http, child_process, global WebSocket) and Chrome.
//
// Usage:
//   node tests/browser/smoke.mjs --site <publish>/wwwroot [--screenshots <dir>] [--chrome <path>]
//   node tests/browser/smoke.mjs --url https://jerwelz.github.io/SchichtWerk/
//
// --site serves the folder under /SchichtWerk/ (like GitHub Pages) on a local port.
// Exit code 1 if any check fails.

import { spawn } from "node:child_process";
import { createServer } from "node:http";
import { existsSync, mkdirSync, mkdtempSync, readFileSync, rmSync, writeFileSync } from "node:fs";
import { extname, join, normalize } from "node:path";
import { tmpdir } from "node:os";

// ---------- Arguments ----------
const args = Object.fromEntries(process.argv.slice(2).reduce((pairs, arg, i, all) =>
  arg.startsWith("--") ? [...pairs, [arg.slice(2), all[i + 1]]] : pairs, []));
const chromePath = args.chrome ?? (process.platform === "win32"
  ? "C:/Program Files/Google/Chrome/Application/chrome.exe"
  : "google-chrome");
const screenshotDir = args.screenshots;
if (screenshotDir) mkdirSync(screenshotDir, { recursive: true });

// ---------- Static server (GitHub Pages layout: app under /SchichtWerk/) ----------
const mimeTypes = {
  ".html": "text/html; charset=utf-8", ".js": "text/javascript", ".mjs": "text/javascript",
  ".css": "text/css", ".json": "application/json", ".wasm": "application/wasm",
  ".png": "image/png", ".ico": "image/x-icon", ".svg": "image/svg+xml", ".dat": "application/octet-stream",
};

async function startServer(root) {
  const server = createServer((req, res) => {
    const path = decodeURIComponent(new URL(req.url, "http://localhost").pathname);
    if (!path.startsWith("/SchichtWerk/")) { res.writeHead(404).end(); return; }
    let file = normalize(join(root, path.slice("/SchichtWerk/".length) || "index.html"));
    if (!file.startsWith(normalize(root)) || !existsSync(file)) { res.writeHead(404).end(); return; }
    let body = readFileSync(file);
    if (file.endsWith("index.html")) // same base-path rewrite as the deploy workflow
      body = body.toString().replace('<base href="/" />', '<base href="/SchichtWerk/" />');
    res.writeHead(200, { "Content-Type": mimeTypes[extname(file)] ?? "application/octet-stream" }).end(body);
  });
  await new Promise(resolve => server.listen(0, "127.0.0.1", resolve));
  return server;
}

const server = args.site ? await startServer(args.site) : null;
const url = args.url ?? `http://127.0.0.1:${server.address().port}/SchichtWerk/`;

// ---------- Chrome + DevTools Protocol ----------
const sleep = ms => new Promise(resolve => setTimeout(resolve, ms));
const profile = mkdtempSync(join(tmpdir(), "schichtwerk-chrome-"));
const port = 9300 + Math.floor(Math.random() * 500);
const chromeArgs = ["--headless=new", "--disable-gpu", "--no-first-run", "--no-default-browser-check",
  `--remote-debugging-port=${port}`, `--user-data-dir=${profile}`, "about:blank"];
if (process.env.CI) chromeArgs.unshift("--no-sandbox"); // GitHub runners restrict Chrome's sandbox
const chrome = spawn(chromePath, chromeArgs, { stdio: "ignore" });

let pageTarget;
for (let i = 0; i < 100 && !pageTarget; i++) {
  try { pageTarget = (await (await fetch(`http://127.0.0.1:${port}/json/list`)).json()).find(t => t.type === "page"); }
  catch { await sleep(200); }
}
if (!pageTarget) throw new Error(`Chrome did not start (${chromePath})`);

const socket = new WebSocket(pageTarget.webSocketDebuggerUrl);
await new Promise(resolve => socket.addEventListener("open", resolve));
let nextId = 0;
const pending = new Map();
const browserProblems = [];
socket.addEventListener("message", event => {
  const msg = JSON.parse(event.data);
  if (msg.id && pending.has(msg.id)) { pending.get(msg.id)(msg); pending.delete(msg.id); }
  if (msg.method === "Runtime.exceptionThrown") browserProblems.push("exception: " + msg.params.exceptionDetails.text);
  if (msg.method === "Runtime.consoleAPICalled" && ["error", "warning"].includes(msg.params.type))
    browserProblems.push(`console.${msg.params.type}: ` + msg.params.args.map(a => a.value ?? a.description).join(" "));
  if (msg.method === "Network.responseReceived" && msg.params.response.status >= 400)
    browserProblems.push(`HTTP ${msg.params.response.status} ${msg.params.response.url}`);
});
const send = (method, params = {}) => new Promise((resolve, reject) => {
  const id = ++nextId;
  pending.set(id, msg => msg.error ? reject(new Error(`${method}: ${msg.error.message}`)) : resolve(msg.result));
  socket.send(JSON.stringify({ id, method, params }));
});
const evaluate = async expression => {
  const result = await send("Runtime.evaluate", { expression, returnByValue: true, awaitPromise: true });
  if (result.exceptionDetails) throw new Error(`evaluate failed: ${result.exceptionDetails.text}\n${expression}`);
  return result.result.value;
};
const waitFor = async (expression, timeout = 60000) => {
  for (const end = Date.now() + timeout; Date.now() < end; await sleep(100))
    if (await evaluate(expression)) return;
  throw new Error("timeout waiting for " + expression);
};

await send("Runtime.enable");
await send("Network.enable");
await send("Page.enable");

// ---------- Page helpers ----------
async function setViewport(width, height, { mobile = false, touch = false, dark = false } = {}) {
  await send("Emulation.setDeviceMetricsOverride", { width, height, deviceScaleFactor: mobile ? 2 : 1, mobile });
  await send("Emulation.setTouchEmulationEnabled", touch ? { enabled: true, maxTouchPoints: 5 } : { enabled: false });
  await send("Emulation.setEmulatedMedia", { features: [{ name: "prefers-color-scheme", value: dark ? "dark" : "light" }] });
}

async function load() {
  await send("Page.navigate", { url });
  await waitFor("!!document.querySelector('.kpi-value')");
  await sleep(300);
}

async function screenshot(name) {
  if (!screenshotDir) return;
  const { cssContentSize: size } = await send("Page.getLayoutMetrics");
  const { data } = await send("Page.captureScreenshot",
    { format: "png", captureBeyondViewport: true, clip: { x: 0, y: 0, width: size.width, height: size.height, scale: 1 } });
  writeFileSync(join(screenshotDir, `${name}.png`), Buffer.from(data, "base64"));
}

/** Screenshot of one element as currently visible (no viewport resize, so hover state stays). */
async function screenshotElement(name, selector) {
  if (!screenshotDir) return;
  await evaluate(`document.querySelector(${JSON.stringify(selector)}).scrollIntoView({ block: "start" })`);
  await sleep(100);
  const clip = await evaluate(`(() => { const r = document.querySelector(${JSON.stringify(selector)}).getBoundingClientRect();
    return { x: r.x + scrollX, y: r.y + scrollY, width: r.width, height: Math.min(r.height, innerHeight - r.y), scale: 1 }; })()`);
  const { data } = await send("Page.captureScreenshot", { format: "png", clip });
  writeFileSync(join(screenshotDir, `${name}.png`), Buffer.from(data, "base64"));
}

const changedRows = () => evaluate(`[...document.querySelectorAll(".comparison tr.is-changed th")].map(th => th.textContent)`);

const text = selector => evaluate(`document.querySelector(${JSON.stringify(selector)})?.textContent.replace(/\\s+/g, " ").trim() ?? null`);

async function clickButton(label) {
  const clicked = await evaluate(`(() => {
    const button = [...document.querySelectorAll("button")].find(b => b.textContent.includes(${JSON.stringify(label)}));
    if (!button || button.disabled) return false;
    button.click();
    return true; })()`);
  if (!clicked) throw new Error("button not clickable: " + label);
  await sleep(250);
}

async function pressKey(key, code, keyCode, textValue) {
  await send("Input.dispatchKeyEvent", { type: "keyDown", key, code, windowsVirtualKeyCode: keyCode, text: textValue });
  await send("Input.dispatchKeyEvent", { type: "keyUp", key, code, windowsVirtualKeyCode: keyCode });
  await sleep(200);
}
const pressEnter = () => pressKey("Enter", "Enter", 13, "\r");
const pressArrowLeft = () => pressKey("ArrowLeft", "ArrowLeft", 37);

async function tap(x, y) {
  await send("Input.dispatchTouchEvent", { type: "touchStart", touchPoints: [{ x, y }] });
  await sleep(50);
  await send("Input.dispatchTouchEvent", { type: "touchEnd", touchPoints: [] });
  await sleep(400);
}

/** Viewport coordinates of a time (minutes since shift start) in the middle of the chart. */
async function chartPoint(minutes) {
  await evaluate(`document.querySelector(".area-chart").scrollIntoView({ block: "center" })`);
  await sleep(100);
  const r = await evaluate(`(() => { const r = document.querySelector(".plot-svg").getBoundingClientRect(); return { x: r.x, y: r.y, w: r.width, h: r.height }; })()`);
  return { x: r.x + r.w * minutes / 480, y: r.y + r.h / 2 };
}

const readState = () => evaluate(`(() => {
  const clean = s => s.replace(/[✓\\s]+/g, " ").trim();
  const button = label => [...document.querySelectorAll("button")].find(b => b.textContent.includes(label)).disabled;
  return {
    kpis: [...document.querySelectorAll(".kpi-value")].map(e => clean(e.textContent)),
    disabled: [button("Materialmangel"), button("Gutteile als Ausschuss"), button("Zurücksetzen")],
    lines: document.querySelectorAll(".plot-svg polyline").length,
    downtimeSegments: document.querySelectorAll(".segment-downtime").length,
    changedSegments: document.querySelectorAll(".segment.is-changed").length,
    rejectMarks: document.querySelectorAll(".reject-mark").length,
    reasons: [...document.querySelectorAll(".reason")].map(e => clean(e.textContent)),
    endGood: [...document.querySelectorAll(".intervals tbody tr")].at(-1).lastElementChild.textContent,
  }; })()`);

// ---------- Checks ----------
const results = [];
function check(name, actual, expected) {
  const pass = JSON.stringify(actual) === JSON.stringify(expected);
  results.push({ name, pass, actual, expected });
}

function checkScenario(name, state, expected) {
  for (const [key, value] of Object.entries(expected)) check(`${name}: ${key}`, state[key], value);
}

const baselineReasons = ["Materialmangel 20 Min.", "Umrüsten 15 Min.", "Maschinenstörung 10 Min."];
const shortageReasons = ["Materialmangel 30 Min. (+10 Min.)", "Umrüsten 15 Min.", "Maschinenstörung 10 Min."];
const baseline = { kpis: ["77,8 %", "700 Stück", "45 Min.", "4,0 %"], disabled: [false, false, true], lines: 2,
  downtimeSegments: 3, changedSegments: 0, rejectMarks: 29, reasons: baselineReasons, endGood: "700" };

try {
  // --- Desktop: values for every scenario ---
  await setViewport(1280, 900);
  await load();
  const initial = await readState();
  checkScenario("Ausgangslage", initial, baseline);
  check("Ausgangslage: no highlighted comparison rows", await changedRows(), []);
  await screenshot("desktop-1-ausgangslage");

  await clickButton("10 Min. Materialmangel");
  checkScenario("Materialmangel", await readState(), { kpis: ["75,8 %", "682 Stück", "55 Min.", "4,1 %"],
    disabled: [true, false, false], lines: 3, downtimeSegments: 4, changedSegments: 1, rejectMarks: 29,
    reasons: shortageReasons, endGood: "682" });
  check("Materialmangel: explains rising reject share",
    (await text(".kpi-note"))?.startsWith("Gleich viele Ausschussteile, aber weniger produziert (29 ÷ 711 statt 29 ÷ 729)"), true);
  check("Materialmangel: highlighted comparison rows", await changedRows(),
    ["OEE", "Verfügbarkeit", "Qualität", "Produziert", "Gutteile", "Laufzeit", "Stillstand"]);
  await screenshot("desktop-2-materialmangel");

  await clickButton("10 Gutteile als Ausschuss");
  checkScenario("Beide", await readState(), { kpis: ["74,7 %", "672 Stück", "55 Min.", "5,5 %"],
    disabled: [true, true, false], lines: 3, downtimeSegments: 4, changedSegments: 1, rejectMarks: 31,
    reasons: shortageReasons, endGood: "672" });
  check("Beide: no reject-share note (rejects changed)", await text(".kpi-note"), null);
  await screenshot("desktop-3-beide");

  await clickButton("Zurücksetzen");
  check("Zurücksetzen: identical to first load", await readState(), initial);

  await clickButton("10 Gutteile als Ausschuss");
  checkScenario("Ausschuss", await readState(), { kpis: ["76,7 %", "690 Stück", "45 Min.", "5,3 %"],
    disabled: [false, true, false], lines: 3, downtimeSegments: 3, changedSegments: 0, rejectMarks: 31,
    reasons: baselineReasons, endGood: "690" });
  check("Ausschuss: highlighted comparison rows", await changedRows(), ["OEE", "Qualität", "Gutteile", "Ausschuss"]);
  await screenshot("desktop-4-ausschuss");
  await clickButton("Zurücksetzen");

  // --- Keyboard: focus is not lost when a button disables itself ---
  await evaluate(`[...document.querySelectorAll("button")].find(b => b.textContent.includes("Materialmangel")).focus()`);
  await pressEnter();
  check("Keyboard: Enter applies material shortage", (await readState()).kpis[0], "75,8 %");
  check("Keyboard: focus moves to result summary", await evaluate(`document.activeElement.classList.contains("summary")`), true);
  await evaluate(`[...document.querySelectorAll("button")].find(b => b.textContent.includes("Zurücksetzen")).focus()`);
  await pressEnter();
  check("Keyboard: Enter resets", (await readState()).kpis[0], "77,8 %");
  check("Keyboard: focus moves to first action", await evaluate(`document.activeElement.textContent.includes("Materialmangel")`), true);

  // --- Keyboard: chart exploration ---
  await clickButton("10 Min. Materialmangel");
  await evaluate(`document.querySelector(".plot").focus()`);
  await pressArrowLeft();
  await pressArrowLeft();
  check("Keyboard: arrows move through intervals", await text(".tooltip-title"), "13:45–13:50 · Produktion");
  check("Keyboard: screen-reader readout", (await text(".chart [aria-live]"))?.startsWith("13:45–13:50, Produktion."), true);
  await evaluate(`document.activeElement.blur()`);

  // --- Mouse: hover tooltip ---
  const hover = await chartPoint(337.5);
  await send("Input.dispatchMouseEvent", { type: "mouseMoved", x: hover.x, y: hover.y });
  await sleep(250);
  check("Mouse: tooltip at 11:35", await text(".tooltip-title"), "11:35–11:40 · Stillstand: Materialmangel");
  check("Mouse: tooltip inside chart", await evaluate(`(() => { const t = document.querySelector(".tooltip").getBoundingClientRect(),
    p = document.querySelector(".plot").getBoundingClientRect(); return t.left >= p.left && t.right <= p.right; })()`), true);
  await screenshotElement("desktop-5-tooltip", ".area-chart");
  await send("Input.dispatchMouseEvent", { type: "mouseMoved", x: 5, y: 5 });
  await sleep(200);
  check("Mouse: tooltip hidden after leaving", await text(".tooltip-title"), null);

  // --- Dark mode ---
  await setViewport(1280, 900, { dark: true });
  await screenshot("desktop-6-dunkel");

  // --- Layout at tablet and phone widths ---
  for (const [width, height, mobile] of [[1024, 768, false], [768, 1024, true], [390, 844, true], [360, 780, true]]) {
    await setViewport(width, height, { mobile, touch: mobile });
    await load();
    check(`${width}px: no horizontal scrolling`,
      await evaluate(`document.documentElement.scrollWidth <= document.documentElement.clientWidth`), true);
    const order = await evaluate(`[...document.querySelectorAll(".dashboard [class*='area-']")]
      .map(e => [e.className.match(/area-\\w+/)[0], e.getBoundingClientRect()])
      .sort((a, b) => a[1].top - b[1].top || a[1].left - b[1].left).map(e => e[0])`);
    if (width < 992) check(`${width}px: controls before chart`, order.indexOf("area-controls") < order.indexOf("area-chart"), true);
    const smallTargets = await evaluate(`[...document.querySelectorAll("button, summary, .github-link, .plot")]
      .filter(e => { const r = e.getBoundingClientRect(); return r.height < 44; }).map(e => e.textContent.trim().slice(0, 30))`);
    check(`${width}px: touch targets at least 44 px high`, smallTargets, []);
    if (width === 768) await screenshot("tablet-768-ausgangslage");
    if (width === 1024) await screenshot("tablet-1024-ausgangslage");
  }

  // --- Touch (390 px phone, touch emulation): tap buttons and chart ---
  await setViewport(390, 844, { mobile: true, touch: true });
  await load();
  await screenshot("mobil-1-ausgangslage");
  const buttonCenter = await evaluate(`(() => { const b = [...document.querySelectorAll("button")].find(b => b.textContent.includes("Materialmangel"));
    b.scrollIntoView({ block: "center" }); const r = b.getBoundingClientRect(); return { x: r.x + r.width / 2, y: r.y + r.height / 2 }; })()`);
  await tap(buttonCenter.x, buttonCenter.y);
  check("Touch: tap applies material shortage", (await readState()).kpis[0], "75,8 %");

  const touchPoint = await chartPoint(337.5);
  await tap(touchPoint.x, touchPoint.y);
  check("Touch: tooltip stays after tap", await text(".tooltip-title"), "11:35–11:40 · Stillstand: Materialmangel");
  await screenshotElement("mobil-3-tooltip-touch", ".area-chart");
  const otherPoint = await chartPoint(97.5);
  await tap(otherPoint.x, otherPoint.y);
  check("Touch: second tap moves tooltip", await text(".tooltip-title"), "07:35–07:40 · Produktion");
  const outside = await evaluate(`(() => { const r = document.querySelector("#chart-title").getBoundingClientRect(); return { x: r.x + 10, y: r.y + r.height / 2 }; })()`);
  await tap(outside.x, outside.y);
  check("Touch: tap outside closes tooltip", await text(".tooltip-title"), null);

  const rejectButton = await evaluate(`(() => { const b = [...document.querySelectorAll("button")].find(b => b.textContent.includes("Gutteile als Ausschuss"));
    b.scrollIntoView({ block: "center" }); const r = b.getBoundingClientRect(); return { x: r.x + r.width / 2, y: r.y + r.height / 2 }; })()`);
  await tap(rejectButton.x, rejectButton.y);
  await screenshot("mobil-2-beide");
  await screenshotElement("mobil-4-diagramm", ".area-chart");
  await setViewport(390, 844, { mobile: true, touch: true, dark: true });
  await screenshotElement("mobil-5-diagramm-dunkel", ".area-chart");
} catch (error) {
  results.push({ name: "Unexpected error", pass: false, actual: String(error.stack ?? error), expected: "" });
} finally {
  socket.close();
  chrome.kill();
  server?.close();
  await sleep(300);
  try { rmSync(profile, { recursive: true, force: true }); } catch { /* Chrome may still hold files on Windows */ }
}

// ---------- Report ----------
for (const r of results) {
  console.log(`${r.pass ? "PASS" : "FAIL"}  ${r.name}`);
  if (!r.pass) console.log(`      expected: ${JSON.stringify(r.expected)}\n      actual:   ${JSON.stringify(r.actual)}`);
}
console.log(browserProblems.length ? "\nBrowser problems:\n  " + browserProblems.join("\n  ") : "\nBrowser problems: none");
const failed = results.filter(r => !r.pass).length + browserProblems.length;
console.log(`\n${results.length - results.filter(r => !r.pass).length} of ${results.length} checks passed against ${url}`);
process.exit(failed ? 1 : 0);
