import { cp, mkdir, readFile, rm, writeFile } from "node:fs/promises";
import { join, resolve } from "node:path";

const root = resolve(import.meta.dirname, "..");
const dist = join(root, "dist");
const allowedPermissions = ["activeTab", "nativeMessaging", "scripting"];

await rm(dist, { recursive: true, force: true });
await mkdir(dist, { recursive: true });

for (const family of ["chromium", "firefox"]) {
  const manifestPath = join(root, "manifests", `${family}.json`);
  const manifest = JSON.parse(await readFile(manifestPath, "utf8"));
  const permissions = [...manifest.permissions].sort();
  if (
    manifest.manifest_version !== 3 ||
    JSON.stringify(permissions) !== JSON.stringify(allowedPermissions) ||
    "host_permissions" in manifest ||
    "content_scripts" in manifest
  ) {
    throw new Error(`${family} manifest violates the reviewed permission boundary.`);
  }

  const destination = join(dist, family);
  await mkdir(destination, { recursive: true });
  await cp(join(root, "src", "background.js"), join(destination, "background.js"));
  await writeFile(
    join(destination, "manifest.json"),
    `${JSON.stringify(manifest, null, 2)}\n`,
    "utf8",
  );
}

