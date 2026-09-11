'use strict';

const assert = require('node:assert/strict');
const fs = require('node:fs');
const os = require('node:os');
const path = require('node:path');
const test = require('node:test');

const provenance = require('./RobotArenaPluginYG2Provenance');

const repositoryRoot = path.join(__dirname, '..');
const manifest = JSON.parse(fs.readFileSync(
  path.join(__dirname, 'RobotArenaPluginYG2Integration.json'),
  'utf8'));
const vendorRoot = path.join(repositoryRoot, ...manifest.vendorRoot.split('/'));

test('vendored PluginYG2 receipt matches the checked-in tree', () => {
  const errors = provenance.validateProvenance({
    manifest,
    vendorRoot,
    requireArchive: false,
  });

  assert.deepEqual(errors, []);
  assert.equal(
    provenance.computeVendoredFingerprint(vendorRoot),
    manifest.vendoredFingerprint);
});

test('provenance validation rejects malformed manifest coordinates', () => {
  const mutatedManifest = {
    ...manifest,
    plugin: '',
    pluginVersion: '',
    versionFile: '',
    templateFile: '',
    platform: '',
    sdkLoader: '',
    sdkInitializer: '',
    sourceArchiveSha256: 'not-a-hash',
    modules: ['Core', 'Core'],
    requiredVendorFiles: ['Scripts/Basic/YG2.cs', 'Scripts/Basic/YG2.cs'],
    requiredDefines: ['ROBOTARENA_PLUGINYG2', 'ROBOTARENA_PLUGINYG2'],
    requiredArtifactMarkers: ['custom-marker'],
    exactlyOnceArtifactMarkers: ['game_api_pause'],
    forbiddenArtifactMarkers: ['legacy-custom-marker'],
    vendoredFingerprint: '0'.repeat(64),
  };

  const errors = provenance.validateProvenance({
    manifest: mutatedManifest,
    vendorRoot,
    requireArchive: false,
  });

  assert.ok(errors.some(error => error.includes('plugin')));
  assert.ok(errors.some(error => error.includes('pluginVersion')));
  assert.ok(errors.some(error => error.includes('versionFile')));
  assert.ok(errors.some(error => error.includes('templateFile')));
  assert.ok(errors.some(error => error.includes('platform')));
  assert.ok(errors.some(error => error.includes('sdkLoader')));
  assert.ok(errors.some(error => error.includes('sdkInitializer')));
  assert.ok(errors.some(error => error.includes('sourceArchiveSha256')));
  assert.ok(errors.some(error => error.includes('modules')));
  assert.ok(errors.some(error => error.includes('requiredDefines')));
  assert.ok(errors.some(error => error.includes('requiredVendorFiles')));
  assert.ok(errors.some(error => error.includes('artifact markers')) ||
    errors.some(error => error.includes('exactlyOnceArtifactMarkers')));
  assert.ok(errors.some(error => error.includes('fingerprint')));
});

test('provenance validation accepts changed coordinates declared by the manifest', () => {
  const mutatedManifest = {
    ...manifest,
    pluginVersion: 'v2.0091',
    versionFile: 'Assets/Other/Version.txt',
    templateFile: 'Assets/WebGLTemplates/Custom/index.html',
    platform: 'CustomPlatform',
    sdkLoader: '<script src="/custom-sdk.js"></script>',
    sdkInitializer: 'Custom.init()',
    sourceArchiveSha256: '0'.repeat(64),
    modules: ['EnvirData'],
    requiredDefines: ['CustomPlatform'],
    requiredArtifactMarkers: ['custom-marker'],
    exactlyOnceArtifactMarkers: ['custom-marker'],
    forbiddenArtifactMarkers: ['legacy-custom-marker'],
  };

  const errors = provenance.validateProvenance({
    manifest: mutatedManifest,
    vendorRoot,
    requireArchive: false,
  });

  assert.equal(errors.length, 0);
});

test('provenance validation rejects manifest paths outside declared roots', () => {
  const mutatedManifest = {
    ...manifest,
    versionFile: '../outside-version.txt',
    templateFile: '/outside-template.html',
    vendorRoot: '../outside-vendor',
    requiredVendorFiles: ['../outside-vendor.txt'],
  };

  const errors = provenance.validateProvenance({
    manifest: mutatedManifest,
    vendorRoot,
    requireArchive: false,
  });

  assert.ok(errors.some(error => error.includes('versionFile')));
  assert.ok(errors.some(error => error.includes('templateFile')));
  assert.ok(errors.some(error => error.includes('vendorRoot')));
  assert.ok(errors.some(error => error.includes('requiredVendorFiles')));
});

test('provenance validation requires an explicit forbidden-marker list', () => {
  const mutatedManifest = { ...manifest };
  delete mutatedManifest.forbiddenArtifactMarkers;

  const errors = provenance.validateProvenance({
    manifest: mutatedManifest,
    vendorRoot,
    requireArchive: false,
  });

  assert.ok(errors.some(error => error.includes('forbiddenArtifactMarkers')));
});

test('provenance validation has no hidden copy for each declared coordinate', () => {
  const coordinateVariants = {
    plugin: 'CustomPlugin',
    pluginVersion: 'v9.0.0',
    versionFile: 'Custom/Version.txt',
    templateFile: 'Custom/index.html',
    unityTemplate: 'PROJECT:CustomPlugin',
    platform: 'CustomPlatform',
    modules: ['EnvirData'],
    requiredVendorFiles: ['Modules/EnvirData/Scripts/EnvirData_yg.cs'],
    requiredDefines: ['CUSTOM_DEFINE'],
    sdkLoader: '<script src="/custom-sdk.js"></script>',
    sdkInitializer: 'Custom.init()',
    requiredArtifactMarkers: ['custom-marker'],
    exactlyOnceArtifactMarkers: ['custom-marker'],
    forbiddenArtifactMarkers: ['legacy-custom-marker'],
    sourceArchiveSha256: '0'.repeat(64),
  };

  for (const [fieldName, value] of Object.entries(coordinateVariants)) {
    const candidateManifest = { ...manifest, [fieldName]: value };
    if (fieldName === 'requiredArtifactMarkers') {
      candidateManifest.exactlyOnceArtifactMarkers = [value[0]];
    }
    if (fieldName === 'exactlyOnceArtifactMarkers') {
      candidateManifest.requiredArtifactMarkers = [value[0]];
    }
    if (fieldName === 'platform') {
      candidateManifest.requiredDefines = [value];
    }
    if (fieldName === 'requiredDefines') {
      candidateManifest.platform = value[0];
    }
    assert.deepEqual(
      provenance.validateProvenance({
        manifest: candidateManifest,
        vendorRoot,
        requireArchive: false,
      }),
      [],
      `manifest field ${fieldName} was rejected by a hidden policy copy`);
  }
});

test('vendored fingerprint changes when an imported file changes', () => {
  const temporaryRoot = fs.mkdtempSync(path.join(os.tmpdir(), 'robot-arena-provenance-'));
  try {
    const firstPath = path.join(temporaryRoot, 'first.txt');
    const secondPath = path.join(temporaryRoot, 'nested', 'second.txt');
    fs.mkdirSync(path.dirname(secondPath));
    fs.writeFileSync(firstPath, 'first', 'utf8');
    fs.writeFileSync(secondPath, 'second', 'utf8');
    const firstFingerprint = provenance.computeVendoredFingerprint(temporaryRoot);

    fs.writeFileSync(secondPath, 'mutated', 'utf8');

    assert.notEqual(
      provenance.computeVendoredFingerprint(temporaryRoot),
      firstFingerprint);
  } finally {
    fs.rmSync(temporaryRoot, { recursive: true, force: true });
  }
});

test('offline archive verification compares the actual archive bytes', () => {
  const temporaryRoot = fs.mkdtempSync(path.join(os.tmpdir(), 'robot-arena-archive-'));
  try {
    const archivePath = path.join(temporaryRoot, 'PluginYG2.unitypackage');
    fs.writeFileSync(archivePath, 'synthetic upstream archive', 'utf8');

    assert.deepEqual(provenance.validateProvenance({
      manifest,
      vendorRoot,
      archivePath,
      requireArchive: true,
    }), [
      'PluginYG2 source archive SHA-256 does not match the manifest receipt.',
    ]);
  } finally {
    fs.rmSync(temporaryRoot, { recursive: true, force: true });
  }
});

test('provenance validation follows the manifest coordinates without hidden policy copies', () => {
  const temporaryRoot = fs.mkdtempSync(path.join(os.tmpdir(), 'robot-arena-manifest-policy-'));
  const vendorRoot = path.join(temporaryRoot, 'vendor');
  const requiredVendorPath = path.join(vendorRoot, 'Custom', 'Required.txt');
  const modulePath = path.join(vendorRoot, 'Modules', 'CustomCore', 'module.txt');
  const archivePath = path.join(temporaryRoot, 'custom-package.unitypackage');

  try {
    fs.mkdirSync(path.dirname(requiredVendorPath), { recursive: true });
    fs.mkdirSync(path.dirname(modulePath), { recursive: true });
    fs.writeFileSync(requiredVendorPath, 'custom vendor file', 'utf8');
    fs.writeFileSync(modulePath, 'custom module file', 'utf8');
    fs.writeFileSync(archivePath, 'custom upstream archive', 'utf8');

    const customManifest = {
      ...manifest,
      plugin: 'CustomPlugin',
      pluginVersion: 'v9.0.0',
      versionFile: 'Custom/Version.txt',
      templateFile: 'Custom/index.html',
      platform: 'CustomPlatform',
      modules: ['CustomCore'],
      requiredDefines: ['CustomPlatform'],
      sdkLoader: '<script src="/custom-sdk.js"></script>',
      sdkInitializer: 'Custom.init()',
      requiredVendorFiles: ['Custom/Required.txt'],
      requiredArtifactMarkers: ['custom-marker'],
      exactlyOnceArtifactMarkers: ['custom-marker'],
      forbiddenArtifactMarkers: ['legacy-custom-marker'],
      vendoredFingerprint: '',
      sourceArchiveSha256: provenance.computeFileSha256(archivePath),
    };
    customManifest.vendoredFingerprint = provenance.computeVendoredFingerprint(vendorRoot);

    assert.deepEqual(provenance.validateProvenance({
      manifest: customManifest,
      vendorRoot,
      archivePath,
      requireArchive: true,
    }), []);
  } finally {
    fs.rmSync(temporaryRoot, { recursive: true, force: true });
  }
});
