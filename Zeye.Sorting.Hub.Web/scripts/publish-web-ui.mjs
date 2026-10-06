#!/usr/bin/env node
import { createHash, randomUUID } from "node:crypto";
import { spawn } from "node:child_process";
import * as fs from "node:fs/promises";
import path from "node:path";
import { fileURLToPath } from "node:url";

const format = 1;
const officialRegistry = "https://registry.npmjs.org/";
const fallbackRegistry = "https://registry.npmmirror.com/";
const sleep = (milliseconds) => new Promise((resolve) => setTimeout(resolve, milliseconds));
const digest = (value) => createHash("sha256").update(value).digest("hex");
async function json(file) {
  try { return JSON.parse(await fs.readFile(file, "utf8")); }
  catch (error) { if (error.code === "ENOENT" || error instanceof SyntaxError) return null; throw error; }
}
async function exists(file) { try { await fs.access(file); return true; } catch { return false; } }
async function save(file, value) {
  await fs.mkdir(path.dirname(file), { recursive: true });
  const temporary = `${file}.${randomUUID()}.tmp`;
  try { await fs.writeFile(temporary, JSON.stringify(value)); await fs.rename(temporary, file); }
  finally { await fs.rm(temporary, { force: true }); }
}
async function files(directory) {
  if (!await exists(directory)) return [];
  const result = [];
  for (const entry of await fs.readdir(directory, { withFileTypes: true })) {
    const fullPath = path.join(directory, entry.name);
    if (entry.isDirectory()) result.push(...await files(fullPath));
    else if (entry.isFile()) result.push(fullPath);
    else throw new Error(`Publish input must be a regular file or directory: ${fullPath}`);
  }
  return result.sort();
}
async function hashes(root, paths) {
  const result = {};
  for (const file of [...paths].sort()) result[path.relative(root, file).split(path.sep).join("/")] = digest(await fs.readFile(file));
  return result;
}
async function dependencyInputs(root) {
  const manifest = await json(path.join(root, "package.json"));
  if (!manifest) throw new Error("A valid frontend package.json is required.");
  const relevant = {};
  for (const key of ["dependencies", "devDependencies", "optionalDependencies", "peerDependencies", "peerDependenciesMeta", "overrides", "workspaces", "packageManager", "engines", "os", "cpu"]) {
    if (manifest[key] !== undefined) relevant[key] = manifest[key];
  }
  relevant.lifecycle = Object.fromEntries(["preinstall", "install", "postinstall", "prepublish", "preprepare", "prepare", "postprepare"]
    .filter((name) => manifest.scripts?.[name]).map((name) => [name, manifest.scripts[name]]));
  const inputs = [];
  for (const name of ["package-lock.json", ".npmrc"]) if (await exists(path.join(root, name))) inputs.push(path.join(root, name));
  if (Object.keys(relevant.lifecycle).length) inputs.push(...await files(path.join(root, "scripts")));
  return { manifest: relevant, inputs: await hashes(root, inputs) };
}
async function sourceFingerprint(root, dependencies) {
  const inputs = [];
  for (const directory of ["src", "public", "scripts", "worker", ".openai"]) inputs.push(...await files(path.join(root, directory)));
  for (const entry of await fs.readdir(root, { withFileTypes: true })) {
    if (entry.isFile() && (entry.name.startsWith(".env") || /\.(?:html|[cm]?js|ts|json)$/.test(entry.name) || entry.name === ".npmrc")) inputs.push(path.join(root, entry.name));
  }
  const environment = Object.fromEntries(Object.entries(process.env).filter(([key]) => key.startsWith("VITE_") || key === "NODE_ENV").sort());
  return digest(JSON.stringify({ format, dependencies, environment, inputs: await hashes(root, inputs) }));
}
async function outputManifest(root) {
  const client = path.join(root, "dist");
  if (!await exists(path.join(client, "index.html"))) return null;
  return hashes(client, await files(client));
}
async function dependenciesReady(root, lock) {
  const installed = await json(path.join(root, "node_modules", ".package-lock.json"));
  if (!installed?.packages || !Object.keys(installed.packages).length) return false;
  const manifest = await json(path.join(root, "package.json"));
  for (const name of Object.keys({ ...manifest.dependencies, ...manifest.devDependencies })) {
    if (!installed.packages[`node_modules/${name}`]) return false;
  }
  for (const [location, item] of Object.entries(installed.packages)) {
    const expected = lock.packages?.[location];
    if (!location.startsWith("node_modules/") || location.split("/").includes("..") || !expected || item.link || item.version !== expected.version || item.integrity !== expected.integrity) return false;
    const actual = await json(path.join(root, location, "package.json"));
    if (!actual || actual.version !== item.version) return false;
  }
  return exists(path.join(root, "node_modules", "vite", "bin", "vite.js"));
}
function alive(pid) {
  if (!Number.isInteger(pid) || pid <= 0 || pid > 2147483647) return true;
  try { process.kill(pid, 0); return true; } catch (error) { return error.code !== "ESRCH"; }
}
async function acquireLock(file, log, timeout) {
  await fs.mkdir(path.dirname(file), { recursive: true });
  const owner = { pid: process.pid, token: randomUUID() };
  const started = Date.now();
  let notified = false;
  while (true) {
    try {
      const handle = await fs.open(file, "wx");
      try { await handle.writeFile(JSON.stringify(owner)); } finally { await handle.close(); }
      return async () => { if ((await json(file))?.token === owner.token) await fs.rm(file, { force: true }); };
    } catch (error) { if (error.code !== "EEXIST") throw error; }
    // Serialize recovery so two publishers cannot both unlink an abandoned lock.
    const previous = await json(file);
    if (previous && !alive(previous.pid)) {
      const recovery = `${file}.recovery`;
      let handle;
      try {
        handle = await fs.open(recovery, "wx");
        const current = await json(file);
        if (current && !alive(current.pid)) await fs.rm(file, { force: true });
      } catch (error) { if (error.code !== "EEXIST" && error.code !== "ENOENT") throw error; }
      finally { if (handle) { await handle.close(); await fs.rm(recovery, { force: true }); } }
    }
    if (!notified) { log("Another frontend publish is running; waiting for its cache."); notified = true; }
    if (Date.now() - started > timeout) throw new Error(`Timed out waiting for frontend publish. Check the process owning ${file}.`);
    await sleep(150);
  }
}
async function npmCli() {
  const candidates = [];
  if (process.env.npm_execpath) candidates.push(process.env.npm_execpath);
  for (const directory of (process.env.PATH ?? "").split(path.delimiter)) {
    candidates.push(path.join(directory, "node_modules", "npm", "bin", "npm-cli.js"));
    if (process.platform !== "win32") {
      try { candidates.push(await fs.realpath(path.join(directory, "npm"))); } catch { /* Search the next PATH entry. */ }
    }
  }
  candidates.push(path.join(path.dirname(process.execPath), "node_modules", "npm", "bin", "npm-cli.js"));
  for (const candidate of candidates) if (candidate.endsWith("npm-cli.js") && await exists(candidate)) return candidate;
  throw new Error("npm was not found. Install Node.js/npm on the publish machine.");
}
export function runProcess(command, args, { cwd, timeout = 180000, log = console.log, label = "command" } = {}) {
  return new Promise((resolve, reject) => {
    const child = spawn(command, args, { cwd, stdio: "inherit", windowsHide: true, detached: process.platform !== "win32" });
    const started = Date.now();
    let timedOut = false;
    const progress = setInterval(() => log(`Still running (${Math.round((Date.now() - started) / 1000)}s): ${label}`), 15000);
    const deadline = setTimeout(() => {
      timedOut = true;
      if (!child.pid) return;
      if (process.platform === "win32") {
        // This PID comes from our own spawn; terminate only that child tree.
        const kill = spawn("taskkill.exe", ["/PID", String(child.pid), "/T", "/F"], { stdio: "ignore", windowsHide: true });
        kill.on("error", () => child.kill());
        kill.on("exit", (code) => { if (code !== 0) child.kill(); });
      } else {
        try { process.kill(-child.pid, "SIGKILL"); } catch { child.kill("SIGKILL"); }
      }
    }, timeout);
    const clear = () => { clearInterval(progress); clearTimeout(deadline); };
    child.on("error", (error) => { clear(); reject(error); });
    child.on("close", (code) => {
      clear();
      if (timedOut) reject(new Error(`Command exceeded ${Math.round(timeout / 1000)} seconds.`));
      else if (code !== 0) reject(new Error(`npm command exited with code ${code}.`));
      else resolve();
    });
  });
}
async function restore(root, options, run, log) {
  const common = ["ci", "--include=dev", "--include=optional", "--no-audit", "--no-fund", `--cache=${options.cacheDirectory}`];
  if (await exists(path.join(options.cacheDirectory, "_cacache"))) {
    try {
      log("Restoring dependencies from the local npm cache (offline).");
      await run([...common, "--offline"], { timeout: options.installTimeout });
      return;
    } catch (error) { log(`Offline cache could not complete the restore: ${error.message}`); }
  }
  const sources = [options.registry];
  if (options.fallbackRegistry && options.fallbackRegistry !== options.registry) sources.push(options.fallbackRegistry);
  for (const [index, registry] of sources.entries()) {
    const address = new URL(registry);
    if (address.protocol !== "https:" || address.username || address.password) throw new Error("Use an HTTPS npm registry without credentials in its URL.");
    log(`Restoring dependencies from ${address.origin}, with a bounded timeout.`);
    try {
      await run([...common, "--prefer-offline", "--fetch-retries=0", "--fetch-timeout=15000", `--registry=${registry}`, "--replace-registry-host=npmjs"], { timeout: options.installTimeout });
      return;
    } catch (error) {
      if (index === sources.length - 1) throw new Error(`Dependency restore failed. Check npm network access or supply WebNpmRegistry. ${error.message}`);
      log(`Primary npm source failed: ${error.message} Trying the fallback; lockfile integrity checks remain enabled.`);
    }
  }
}
export async function publishWebUi({ root, restoreDependencies = true, forceBuild = false, forceRestore = false,
  registry = officialRegistry, fallbackRegistry: backup = fallbackRegistry, cacheDirectory = path.resolve(root, "../artifacts/npm-publish-cache"),
  installTimeout = 90000, buildTimeout = 180000, lockTimeout = 180000, runNpm, log = (message) => console.log(`[Web publish] ${message}`) }) {
  root = path.resolve(root);
  const stateDirectory = path.resolve(root, "../artifacts/web-publish", digest(root).slice(0, 16));
  const unlock = await acquireLock(path.join(stateDirectory, "publish.lock"), log, lockTimeout);
  try {
    const dependencies = digest(JSON.stringify({ format, node: process.version, platform: process.platform, architecture: process.arch,
      inputs: await dependencyInputs(root) }));
    const fingerprint = await sourceFingerprint(root, dependencies);
    const buildStateFile = path.join(stateDirectory, "build.json");
    const buildState = await json(buildStateFile);
    const currentClient = await outputManifest(root);
    if (!forceBuild && !forceRestore && buildState?.fingerprint === fingerprint && currentClient && JSON.stringify(buildState.client) === JSON.stringify(currentClient)) {
      log("Frontend inputs and output hashes are unchanged; reusing the verified build.");
      return { restored: false, built: false };
    }
    const cli = runNpm ? null : await npmCli();
    const run = runNpm ?? ((args, settings) => runProcess(process.execPath, [cli, ...args], { cwd: root, log, label: args[0] === "run" ? "frontend build" : "dependency installation", ...settings }));
    const lock = await json(path.join(root, "package-lock.json"));
    if (!lock?.packages) throw new Error("A valid package-lock.json is required for frontend publication.");
    const dependencyStateFile = path.join(root, "node_modules", ".zsh-publish.json");
    const dependencyState = await json(dependencyStateFile);
    const ready = await dependenciesReady(root, lock);
    let restored = false;
    if (forceRestore || dependencyState?.fingerprint !== dependencies || !ready) {
      if (!restoreDependencies) {
        if (forceRestore || !ready) throw new Error("Frontend dependencies are missing or do not match package-lock.json; enable RestoreWebDependencies.");
      } else {
        await fs.rm(dependencyStateFile, { force: true });
        await restore(root, { registry, fallbackRegistry: backup, cacheDirectory, installTimeout }, run, log);
        if (!await dependenciesReady(root, lock)) throw new Error("npm restore did not leave a complete, matching dependency tree.");
        restored = true;
      }
      await save(dependencyStateFile, { fingerprint: dependencies });
    } else log("Dependency inputs and installed package versions are unchanged; skipping npm ci.");
    await fs.rm(buildStateFile, { force: true });
    log("Building the frontend because its inputs or outputs changed.");
    await run(["run", "build"], { timeout: buildTimeout });
    const client = await outputManifest(root);
    if (!client) throw new Error("Frontend build did not produce all required artifacts.");
    if (fingerprint !== await sourceFingerprint(root, dependencies)) throw new Error("Frontend inputs changed during publication. Publish again to create a consistent package.");
    await save(buildStateFile, { fingerprint, client });
    log(`Frontend build complete; ${Object.keys(client).length} verified files will be bundled.`);
    return { restored, built: true };
  } finally { await unlock(); }
}
if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  const args = Object.fromEntries(Array.from({ length: (process.argv.length - 2) / 2 }, (_, index) => [process.argv[index * 2 + 2], process.argv[index * 2 + 3]]));
  publishWebUi({ root: path.resolve(path.dirname(fileURLToPath(import.meta.url)), ".."),
    restoreDependencies: args["--restore-dependencies"]?.toLowerCase() !== "false", forceBuild: args["--force-build"]?.toLowerCase() === "true", forceRestore: args["--force-restore"]?.toLowerCase() === "true",
    registry: args["--registry"] || officialRegistry, fallbackRegistry: args["--fallback-registry"]?.toLowerCase() === "none" ? "" : args["--fallback-registry"] ?? fallbackRegistry })
    .catch((error) => { console.error(`[Web publish] ${error.message}`); process.exitCode = 1; });
}
