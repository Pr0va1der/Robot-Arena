'use strict';

const crypto = require('node:crypto');
const fs = require('node:fs');
const path = require('node:path');

const IGNORED_VENDOR_FILES = new Set([
  'Editor/BuildLogYG2.txt',
  'Editor/PluginPrefs.json',
]);

function getVendorFiles(vendorRoot) {
  const files = [];
  function visit(currentRoot) {
    for (const entry of fs.readdirSync(currentRoot, { withFileTypes: true })) {
      const absolutePath = path.join(currentRoot, entry.name);
      if (entry.isDirectory()) {
        visit(absolutePath);
        continue;
      }

      if (!entry.isFile()) {
        continue;
      }

      const relativePath = path.relative(vendorRoot, absolutePath).split(path.sep).join('/');
      if (!IGNORED_VENDOR_FILES.has(relativePath)) {
        files.push({ absolutePath, relativePath });
      }
    }
  }

  visit(vendorRoot);
  return files.sort((left, right) => left.relativePath < right.relativePath
    ? -1
    : left.relativePath > right.relativePath
      ? 1
      : 0);
}

function computeVendoredFingerprint(vendorRoot) {
  const hash = crypto.createHash('sha256');
  for (const file of getVendorFiles(vendorRoot)) {
    hash.update(Buffer.from(file.relativePath, 'utf8'));
    hash.update(Buffer.from([0]));
    hash.update(fs.readFileSync(file.absolutePath));
    hash.update(Buffer.from([0]));
  }

  return hash.digest('hex').toUpperCase();
}

function computeFileSha256(filePath) {
  return crypto.createHash('sha256').update(fs.readFileSync(filePath)).digest('hex').toUpperCase();
}

function isSha256(value) {
  return typeof value === 'string' && /^[0-9a-f]{64}$/i.test(value);
}

function isSafeRelativePath(value) {
  if (typeof value !== 'string' || value.length === 0) {
    return false;
  }

  const normalizedValue = value.replaceAll('\\', '/');
  return !normalizedValue.startsWith('/') &&
    !/^[A-Za-z]:\//.test(normalizedValue) &&
    !normalizedValue.split('/').includes('..');
}

function validateManifestList(
  errors,
  manifest,
  fieldName,
  required = true) {
  const values = manifest[fieldName];
  if (!Array.isArray(values)) {
    if (required) {
      errors.push(`Manifest ${fieldName} is required.`);
    }
    return [];
  }

  if (required && values.length === 0) {
    errors.push(`Manifest ${fieldName} is required.`);
  }

  if (values.some(value => typeof value !== 'string' || value.length === 0)) {
    errors.push(`Manifest ${fieldName} must contain non-empty strings.`);
  }

  if (new Set(values).size !== values.length) {
    errors.push(`Manifest ${fieldName} must not contain duplicates.`);
  }

  return values;
}

function validateManifestShape(manifest) {
  const errors = [];
  for (const fieldName of [
    'plugin',
    'pluginVersion',
    'versionFile',
    'templateFile',
    'unityTemplate',
    'vendorRoot',
    'platform',
    'sdkLoader',
    'sdkInitializer',
  ]) {
    if (typeof manifest[fieldName] !== 'string' || manifest[fieldName].length === 0) {
      errors.push(`Manifest ${fieldName} is required.`);
    }
  }

  if (!isSha256(manifest.sourceArchiveSha256)) {
    errors.push('Manifest sourceArchiveSha256 must be a 64-character hexadecimal hash.');
  }
  if (!isSha256(manifest.vendoredFingerprint)) {
    errors.push('Manifest vendoredFingerprint must be a 64-character hexadecimal hash.');
  }

  for (const fieldName of ['versionFile', 'templateFile', 'vendorRoot']) {
    if (typeof manifest[fieldName] === 'string' &&
        manifest[fieldName].length > 0 &&
        !isSafeRelativePath(manifest[fieldName])) {
      errors.push(`Manifest ${fieldName} must be a relative path without '..'.`);
    }
  }

  const modules = validateManifestList(errors, manifest, 'modules');
  const requiredVendorFiles = validateManifestList(errors, manifest, 'requiredVendorFiles');
  const requiredDefines = validateManifestList(errors, manifest, 'requiredDefines');
  const requiredMarkers = validateManifestList(errors, manifest, 'requiredArtifactMarkers');
  const exactlyOnceMarkers = validateManifestList(errors, manifest, 'exactlyOnceArtifactMarkers');
  validateManifestList(errors, manifest, 'forbiddenArtifactMarkers');

  for (const relativePath of requiredVendorFiles) {
    if (typeof relativePath === 'string' &&
        relativePath.length > 0 &&
        !isSafeRelativePath(relativePath)) {
      errors.push('Manifest requiredVendorFiles entries must be relative paths without "..".');
    }
  }

  if (typeof manifest.platform === 'string' &&
      manifest.platform.length > 0 &&
      !requiredDefines.includes(manifest.platform)) {
    errors.push(
      `Manifest platform must also be listed in requiredDefines: ${manifest.platform}.`);
  }

  for (const marker of exactlyOnceMarkers) {
    if (!requiredMarkers.includes(marker)) {
      errors.push(
        `Manifest exactlyOnceArtifactMarkers must also be listed in requiredArtifactMarkers: ${marker}.`);
    }
  }

  return { errors, modules };
}

function validateProvenance({ manifest, vendorRoot, archivePath, requireArchive = false }) {
  const errors = [];
  if (!manifest) {
    return ['PluginYG2 provenance manifest is missing.'];
  }

  const manifestShape = validateManifestShape(manifest);
  errors.push(...manifestShape.errors);

  if (!vendorRoot || !fs.existsSync(vendorRoot)) {
    errors.push('Vendored PluginYG2 directory is missing.');
  } else {
    for (const relativePath of manifest.requiredVendorFiles || []) {
      if (typeof relativePath !== 'string' ||
          relativePath.length === 0 ||
          !isSafeRelativePath(relativePath)) {
        continue;
      }

      if (!fs.existsSync(path.join(vendorRoot, ...relativePath.split('/')))) {
        errors.push(`Vendored PluginYG2 file is missing: ${relativePath}.`);
      }
    }

    const modulesRoot = path.join(vendorRoot, 'Modules');
    if (fs.existsSync(modulesRoot)) {
      for (const entry of fs.readdirSync(modulesRoot, { withFileTypes: true })) {
        if (entry.isDirectory() && !manifestShape.modules.includes(entry.name)) {
          errors.push(`Unsupported PluginYG2 module directory is present: ${entry.name}.`);
        }
      }
    }

    const actualFingerprint = computeVendoredFingerprint(vendorRoot);
    if (isSha256(manifest.vendoredFingerprint) &&
        manifest.vendoredFingerprint !== actualFingerprint) {
      errors.push(
        `Vendored PluginYG2 fingerprint mismatch: expected ${manifest.vendoredFingerprint}, ` +
        `got ${actualFingerprint}.`);
    }
  }

  if (requireArchive && !archivePath) {
    errors.push('An upstream PluginYG2 archive is required for the offline import check.');
  }
  if (archivePath) {
    if (!fs.existsSync(archivePath)) {
      errors.push(`PluginYG2 source archive is missing: ${archivePath}.`);
    } else if (computeFileSha256(archivePath) !== manifest.sourceArchiveSha256) {
      errors.push('PluginYG2 source archive SHA-256 does not match the manifest receipt.');
    }
  }

  return errors;
}

function parseArguments(argumentsList) {
  const options = { requireArchive: true };
  for (let index = 0; index < argumentsList.length; index++) {
    const argument = argumentsList[index];
    if (argument === '--project') {
      options.projectRoot = path.resolve(argumentsList[++index]);
    } else if (argument === '--archive') {
      options.archivePath = path.resolve(argumentsList[++index]);
    } else if (argument === '--no-archive') {
      options.requireArchive = false;
    } else if (argument === '--help' || argument === '-h') {
      options.help = true;
    } else {
      throw new Error(`Unknown argument: ${argument}`);
    }
  }

  return options;
}

function printHelp() {
  console.log([
    'Usage: node Tools/RobotArenaPluginYG2Provenance.js --project <project> --archive <unitypackage>',
    '',
    'Verifies the pinned upstream archive and the imported PluginYG2 tree.',
    '--no-archive  Only check the imported tree (release builds use this offline check).',
  ].join('\n'));
}

function main(argumentsList = process.argv.slice(2)) {
  const options = parseArguments(argumentsList);
  if (options.help) {
    printHelp();
    return 0;
  }

  const projectRoot = options.projectRoot || path.resolve(__dirname, '..');
  const manifestPath = path.join(projectRoot, 'Tools', 'RobotArenaPluginYG2Integration.json');
  const manifest = JSON.parse(fs.readFileSync(manifestPath, 'utf8'));
  const vendorRoot = isSafeRelativePath(manifest.vendorRoot)
    ? path.join(projectRoot, manifest.vendorRoot)
    : '';
  const errors = validateProvenance({
    manifest,
    vendorRoot,
    archivePath: options.archivePath,
    requireArchive: options.requireArchive,
  });
  const result = {
    valid: errors.length === 0,
    plugin: manifest.plugin,
    pluginVersion: manifest.pluginVersion,
    sourceArchiveSha256: manifest.sourceArchiveSha256,
    vendoredFingerprint: fs.existsSync(vendorRoot)
      ? computeVendoredFingerprint(vendorRoot)
      : null,
    archive: options.archivePath || null,
    errors,
  };
  console.log(JSON.stringify(result, null, 2));
  return result.valid ? 0 : 1;
}

if (require.main === module) {
  try {
    process.exitCode = main();
  } catch (error) {
    console.error(error.stack || error.message);
    process.exitCode = 1;
  }
}

module.exports = {
  computeFileSha256,
  computeVendoredFingerprint,
  getVendorFiles,
  validateProvenance,
};
