import assert from 'node:assert/strict';
import { readFileSync, writeFileSync, existsSync } from 'node:fs';
import { imageFor, photoFor } from '../OnlineStore.API/wwwroot/js/product-images.js';

const read = file => JSON.parse(readFileSync(new URL(`../verification/${file}`, import.meta.url), 'utf8'));
const before = read('PhotosProductsBefore.json');
const after = read('PhotosProductsAfter.json');
assert.deepEqual(after, before, 'Business product data changed');
assert.equal(after.length, 30);
const images = after.map(product => {
  const src = imageFor(product);
  assert.ok(src.startsWith('/images/'));
  assert.ok(existsSync(new URL(`../OnlineStore.API/wwwroot${src}`, import.meta.url)));
  return { id: product.id, model: product.name, src, photograph: Boolean(photoFor(product)) };
});
assert.equal(images.filter(i => i.photograph).length, 10);
const report = { total: images.length, photographs: 10, placeholders: 20, productDataUnchanged: true,
  allImageFilesLocal: true, images };
writeFileSync(new URL('../verification/ProductPhotosVerification.json', import.meta.url), JSON.stringify(report, null, 2));
console.log('30 local image files verified: 10 real photos, 20 unchanged illustrations. Product data unchanged.');
