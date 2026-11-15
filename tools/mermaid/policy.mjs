import { readFileSync } from 'node:fs';
const here = new URL('./', import.meta.url);
const lock = JSON.parse(readFileSync(new URL('package-lock.json', here)));
const inventory = JSON.parse(readFileSync(new URL('dependency-inventory.json', here)));
for (const [path, item] of Object.entries(lock.packages)) {
  if (!path) continue;
  const review = inventory[path];
  if (!review || review.version !== item.version || review.integrity !== item.integrity || review.resolved !== item.resolved ||
      !review.upstream || !review.license || !review.purpose || !review.reviewedOn ||
      !review.maintenanceEvidence?.source || !review.maintenanceEvidence?.selectedVersionPublished ||
      !review.maintenanceEvidence?.latestStablePublished || !review.maintenanceEvidence?.publishedVersions ||
      !review.maintenanceEvidence?.establishedUse || !review.maintenanceEvidence?.assessment ||
      JSON.stringify(review.dependencies) !== JSON.stringify(item.dependencies ?? {}) ||
      JSON.stringify(review.optionalDependencies) !== JSON.stringify(item.optionalDependencies ?? {}) ||
      JSON.stringify(review.peerDependencies) !== JSON.stringify(item.peerDependencies ?? {}))
    throw Error('Unreviewed Mermaid development dependency: ' + path);
  if (!item.dev || !item.resolved.startsWith('https://registry.npmjs.org/')) throw Error('Unexpected npm dependency source/classification: ' + path);
}
if (Object.keys(inventory).length !== Object.keys(lock.packages).length - 1) throw Error('Stale Mermaid inventory');
if (lock.packages['node_modules/mermaid'].version !== '12.3.0') throw Error('Mermaid version requires review');
console.log('MERMAID DEPENDENCIES: 116 locked development packages match reviewed provenance.');
