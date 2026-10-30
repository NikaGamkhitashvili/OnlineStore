import test from 'node:test';
import assert from 'node:assert/strict';
import { existsSync, readFileSync } from 'node:fs';
import { createHash } from 'node:crypto';
import { imageFor, illustrationFor, photoFor, imageAlt } from '../OnlineStore.API/wwwroot/js/product-images.js';
import { productPhotos } from '../OnlineStore.API/wwwroot/js/product-photos.js';

test('product illustrations follow device type instead of broad brand matching', () => {
  const examples = {
    'Lenovo IdeaPad 15': 'laptop', 'ASUS Vivobook 15': 'laptop', 'HP Victus 15': 'laptop',
    'Lenovo ThinkCentre neo 50t': 'desktop', 'Dell OptiPlex 7010 SFF': 'desktop',
    'Samsung Odyssey G5 27': 'monitor', 'Dell P2422H Monitor': 'monitor',
    'Logitech G413 TKL SE': 'keyboard', 'HyperX Alloy Origins Core': 'keyboard',
    'Logitech MX Master 3S': 'mouse', 'Razer DeathAdder Essential': 'mouse',
    'Sony WH-1000XM4': 'headphones', 'HyperX Cloud III': 'headphones',
    'Samsung T7 Portable SSD 1TB': 'ssd', 'Logitech C920 HD Pro Webcam': 'webcam',
    'Anker 332 USB-C Hub': 'hub', 'Razer Gigantus V2 Large': 'mousepad',
    'Unknown future product': 'product'
  };
  for (const [name,type] of Object.entries(examples)) {
    const asset = illustrationFor({ name });
    assert.equal(asset, `/images/${type}.svg`, name);
    assert.ok(existsSync(new URL(`../OnlineStore.API/wwwroot${asset}`, import.meta.url)));
  }
});

test('reviewed photographs match exact models and have local WebP files with credits', () => {
  assert.equal(Object.keys(productPhotos).length, 10);
  assert.equal(new Set(Object.values(productPhotos).map(p => p.src)).size, 10);
  assert.equal(new Set(Object.values(productPhotos).map(p => p.sha256)).size, 10);
  const manifest = JSON.parse(readFileSync(new URL('../OnlineStore.API/wwwroot/images/products/sources.json', import.meta.url), 'utf8'));
  assert.deepEqual(productPhotos, manifest);
  for (const photo of Object.values(productPhotos)) {
    assert.equal(imageFor({ name: photo.model }), photo.src);
    assert.deepEqual(photoFor({ name: ` ${photo.model.toUpperCase()} ` }), photo);
    assert.match(imageAlt({ name: photo.model }), /რეალური ფოტო/);
    assert.match(photo.source, /^https:\/\/(commons\.wikimedia\.org\/wiki\/File:|unsplash\.com\/photos\/)/);
    assert.match(photo.license, /^CC (BY|BY-SA) 4\.0$|^CC0 1\.0$|^Unsplash License$/);
    assert.ok(photo.author && photo.changes && photo.licenseUrl);
    const bytes = readFileSync(new URL(`../OnlineStore.API/wwwroot${photo.src}`, import.meta.url));
    assert.equal(bytes.toString('ascii', 0, 4), 'RIFF');
    assert.equal(bytes.toString('ascii', 8, 12), 'WEBP');
    assert.equal(createHash('sha256').update(bytes).digest('hex'), photo.sha256);
    const credits = readFileSync(new URL('../OnlineStore.API/wwwroot/images/products/credits.html', import.meta.url), 'utf8');
    assert.ok(credits.includes(photo.author) && credits.includes(photo.source) && credits.includes(photo.licenseUrl));
  }
  for (const name of ['HP Pavilion 15', 'Sony WH-1000XM4', 'Samsung T7 Portable SSD 1TB']) assert.ok(photoFor({ name }));
  for (const name of ['UI QA Mouse', 'Sony WH-CH520', 'Logitech MX Master 3', 'Logitech MX Keys S', 'Logitech C920s']) {
    assert.equal(photoFor({ name }), undefined);
    assert.equal(imageFor({ name }), illustrationFor({ name }));
    assert.match(imageAlt({ name }), /დროებითი/);
  }
});
