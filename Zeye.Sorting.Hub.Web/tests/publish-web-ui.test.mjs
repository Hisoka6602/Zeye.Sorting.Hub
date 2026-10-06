import test from "node:test";
import assert from "node:assert/strict";
import * as fs from "node:fs/promises";
import os from "node:os";
import path from "node:path";
import { publishWebUi, runProcess } from "../scripts/publish-web-ui.mjs";

async function write(file, value) {
  await fs.mkdir(path.dirname(file), { recursive: true });
  await fs.writeFile(file, typeof value === "string" ? value : JSON.stringify(value));
}
async function fixture(t) {
  const directory = await fs.mkdtemp(path.join(os.tmpdir(), "zsh-web-publish-"));
  t.after(async () => {
    assert.ok(path.resolve(directory).startsWith(path.join(path.resolve(os.tmpdir()), "zsh-web-publish-")));
    await fs.rm(directory, { recursive: true, force: true });
  });
  const root = path.join(directory, "web with spaces");
  await write(path.join(root, "package.json"), { name: "fixture", dependencies: { vite: "1.0.0" } });
  await write(path.join(root, "package-lock.json"), { lockfileVersion: 3, packages: { "node_modules/vite": { version: "1.0.0", integrity: "sha512-test" } } });
  await write(path.join(root, "src/main.js"), "export const value = 1;");
  await write(path.join(root, "public/logo.svg"), "<svg/>");
  await write(path.join(root, "scripts/build.mjs"), "// build");
  const calls = [];
  const install = async () => {
    const lock = JSON.parse(await fs.readFile(path.join(root, "package-lock.json"), "utf8"));
    await write(path.join(root, "node_modules/.package-lock.json"), lock);
    await write(path.join(root, "node_modules/vite/package.json"), { version: lock.packages["node_modules/vite"].version });
    await write(path.join(root, "node_modules/vite/bin/vite.js"), "// vite");
  };
  const build = async () => {
    await write(path.join(root, "dist/index.html"), '<script src="/assets/main.js"></script>');
    await write(path.join(root, "dist/assets/main.js"), await fs.readFile(path.join(root, "src/main.js"), "utf8"));
  };
  const runNpm = async (args, options) => {
    calls.push({ args, options });
    if (args[0] === "ci") await install();
    else await build();
  };
  const options = { root, runNpm, log() {}, cacheDirectory: path.join(directory, "npm-cache") };
  return { root, directory, options, calls, install, build, runNpm };
}

test("unchanged publications reuse the complete verified frontend, even without node_modules", async (t) => {
  const f = await fixture(t);
  assert.deepEqual(await publishWebUi(f.options), { restored: true, built: true });
  await fs.rm(path.join(f.root, "node_modules"), { recursive: true });
  assert.deepEqual(await publishWebUi(f.options), { restored: false, built: false });
  assert.equal(f.calls.length, 2);
});

test("source content changes rebuild without reinstalling, regardless of timestamps", async (t) => {
  const f = await fixture(t);
  await publishWebUi(f.options);
  const source = path.join(f.root, "src/main.js");
  await fs.writeFile(source, "export const value = 2;");
  await fs.utimes(source, new Date(0), new Date(0));
  assert.deepEqual(await publishWebUi(f.options), { restored: false, built: true });
  assert.equal(f.calls.filter((call) => call.args[0] === "ci").length, 1);
  await fs.rm(path.join(f.root, "public/logo.svg"));
  assert.equal((await publishWebUi(f.options)).built, true);
});

test("changed dependency lockfiles require a new matching installation", async (t) => {
  const f = await fixture(t);
  await publishWebUi(f.options);
  const lock = JSON.parse(await fs.readFile(path.join(f.root, "package-lock.json"), "utf8"));
  lock.packages["node_modules/vite"].version = "2.0.0";
  await write(path.join(f.root, "package-lock.json"), lock);
  assert.deepEqual(await publishWebUi(f.options), { restored: true, built: true });
  assert.equal(f.calls.filter((call) => call.args[0] === "ci").length, 2);
});

test("changing only build scripts does not reinstall dependencies", async (t) => {
  const f = await fixture(t);
  await publishWebUi(f.options);
  const manifest = JSON.parse(await fs.readFile(path.join(f.root, "package.json"), "utf8"));
  manifest.scripts = { build: "node scripts/build.mjs" };
  await write(path.join(f.root, "package.json"), manifest);
  assert.deepEqual(await publishWebUi(f.options), { restored: false, built: true });
  assert.equal(f.calls.filter((call) => call.args[0] === "ci").length, 1);
});

test("deleted and corrupted output assets cannot be reused", async (t) => {
  const f = await fixture(t);
  await publishWebUi(f.options);
  const asset = path.join(f.root, "dist/assets/main.js");
  await fs.rm(asset);
  assert.deepEqual(await publishWebUi(f.options), { restored: false, built: true });
  await fs.writeFile(asset, "corruption");
  assert.equal((await publishWebUi(f.options)).built, true);
});

test("failed and incomplete builds never acquire a reusable success stamp", async (t) => {
  const f = await fixture(t);
  await assert.rejects(publishWebUi({ ...f.options, runNpm: async (args, options) => {
    if (args[0] === "ci") return f.runNpm(args, options);
    await write(path.join(f.root, "dist/index.html"), "partial");
    throw new Error("broken build");
  } }), /broken build/);
  assert.deepEqual(await publishWebUi(f.options), { restored: false, built: true });
  assert.deepEqual(await publishWebUi(f.options), { restored: false, built: false });
});

test("disabled restores reject incomplete dependencies rather than publishing stale code", async (t) => {
  const f = await fixture(t);
  await assert.rejects(publishWebUi({ ...f.options, restoreDependencies: false }), /enable RestoreWebDependencies/);
  assert.equal(f.calls.length, 0);
  await f.install();
  assert.deepEqual(await publishWebUi({ ...f.options, restoreDependencies: false }), { restored: false, built: true });
});

test("force switches rebuild or restore only when requested", async (t) => {
  const f = await fixture(t);
  await publishWebUi(f.options);
  assert.deepEqual(await publishWebUi({ ...f.options, forceBuild: true }), { restored: false, built: true });
  assert.deepEqual(await publishWebUi({ ...f.options, forceRestore: true }), { restored: true, built: true });
});

test("offline cache failure and primary-source failure fall back with bounded waits and unchanged lockfile", async (t) => {
  const f = await fixture(t);
  await fs.mkdir(path.join(f.options.cacheDirectory, "_cacache"), { recursive: true });
  const before = await fs.readFile(path.join(f.root, "package-lock.json"), "utf8");
  const attempts = [];
  await publishWebUi({ ...f.options, runNpm: async (args, options) => {
    attempts.push({ args, options });
    if (args.includes("--offline") || args.includes("--registry=https://registry.npmjs.org/")) throw new Error("source unavailable");
    await f.runNpm(args, options);
  } });
  assert.equal(attempts[0].args.includes("--offline"), true);
  assert.equal(attempts[2].args.includes("--registry=https://registry.npmmirror.com/"), true);
  assert.equal(attempts[2].args.includes("--fetch-retries=0"), true);
  assert.equal(attempts[2].args.includes("--fetch-timeout=15000"), true);
  assert.equal(attempts[2].options.timeout, 90000);
  assert.equal(await fs.readFile(path.join(f.root, "package-lock.json"), "utf8"), before);
});

test("simultaneous publications share one install and one build", async (t) => {
  const f = await fixture(t);
  const options = { ...f.options, runNpm: async (args, settings) => {
    await new Promise((resolve) => setTimeout(resolve, 60));
    return f.runNpm(args, settings);
  } };
  const results = await Promise.all([publishWebUi(options), publishWebUi(options)]);
  assert.equal(results.filter((result) => result.built).length, 1);
  assert.equal(f.calls.length, 2);
});

test("abandoned publish locks are recovered safely", async (t) => {
  const f = await fixture(t);
  await publishWebUi(f.options);
  const stateDirectories = await fs.readdir(path.join(f.directory, "artifacts/web-publish"));
  await write(path.join(f.directory, "artifacts/web-publish", stateDirectories[0], "publish.lock"), { pid: 2147483646, token: "abandoned" });
  assert.deepEqual(await publishWebUi(f.options), { restored: false, built: false });
});

test("a real stuck child process is terminated at the deadline", async () => {
  await assert.rejects(runProcess(process.execPath, ["-e", "setInterval(() => {}, 1000)"], { timeout: 250, log() {} }), /exceeded/);
});

test("TypeScript configuration changes invalidate the build without reinstalling", async (t) => {
  const f = await fixture(t);
  await publishWebUi(f.options);
  await write(path.join(f.root, "tsconfig.app.json"), { compilerOptions: { strict: true } });
  assert.deepEqual(await publishWebUi(f.options), { restored: false, built: true });
  assert.equal(f.calls.filter((call) => call.args[0] === "ci").length, 1);
});

test("publish cache metadata stays outside the public frontend output", async (t) => {
  const f = await fixture(t);
  await publishWebUi(f.options);
  assert.deepEqual((await fs.readdir(path.join(f.root, "dist"))).sort(), ["assets", "index.html"]);
  const stateDirectories = await fs.readdir(path.join(f.directory, "artifacts/web-publish"));
  assert.equal(stateDirectories.length, 1);
  const state = JSON.parse(await fs.readFile(path.join(f.directory, "artifacts/web-publish", stateDirectories[0], "build.json"), "utf8"));
  assert.ok(state.fingerprint);
  assert.ok(state.client["index.html"]);
});
