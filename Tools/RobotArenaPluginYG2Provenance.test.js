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
const vendorRoot = path.join(repositoryRoot, 'Assets', 'PluginYourGames');

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

test('provenance validation rejects mutated policy coordinates', () => {
  const mutatedManifest = {
    ...manifest,
    pluginVersion: 'v2.0091',
    platform: 'CustomPlatform',
    sourceArchiveSha256: '0'.repeat(64),
    modules: ['Core', 'YandexGames', 'Advertising'],
    requiredDefines: ['ROBOTARENA_PLUGINYG2'],
    vendoredFingerprint: '0'.repeat(64),
  };

  const errors = provenance.validateProvenance({
    manifest: mutatedManifest,
    vendorRoot,
    requireArchive: false,
  });

  assert.ok(errors.some(error => error.includes('version')));
  assert.ok(errors.some(error => error.includes('platform')));
  assert.ok(errors.some(error => error.includes('SHA-256')));
  assert.ok(errors.some(error => error.includes('modules')));
  assert.ok(errors.some(error => error.includes('defines')));
  assert.ok(errors.some(error => error.includes('fingerprint')));
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
    const archiveHash = provenance.computeFileSha256(archivePath);
    const archiveManifest = {
      ...manifest,
      sourceArchiveSha256: archiveHash,
    };

    assert.deepEqual(provenance.validateProvenance({
      manifest: archiveManifest,
      vendorRoot,
      archivePath,
      requireArchive: true,
    }), [
      'PluginYG2 source archive SHA-256 does not match the pinned upstream receipt.',
    ]);
  } finally {
    fs.rmSync(temporaryRoot, { recursive: true, force: true });
  }
});
