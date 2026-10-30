import assert from 'node:assert/strict';
import { readFileSync, existsSync, writeFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { imageFor, illustrationFor } from '../OnlineStore.API/wwwroot/js/product-images.js';

const read = name => JSON.parse(readFileSync(new URL(`../verification/${name}`, import.meta.url), 'utf8'));
const before = read('CatalogBefore.json');
const after = read('CatalogAfter.json');
assert.equal(after.Products.length, 30);
assert.equal(after.Categories.length, 9);
assert.equal(new Set(after.Products.map(p => p.Name.trim().toLowerCase())).size, 30);
for (const table of ['Customers', 'Orders', 'OrderItems']) assert.deepEqual(after[table], before[table], `${table} changed`);
for (const old of before.Categories) assert.deepEqual(after.Categories.find(c => c.Id === old.Id), old);
for (const old of before.Products) {
  const current = after.Products.find(p => p.Id === old.Id);
  assert.ok(current, `Original product ${old.Id} missing`);
  for (const [field,value] of Object.entries(old)) if (field !== 'CategoryId') assert.deepEqual(current[field], value, `Product ${old.Id}: ${field} changed`);
}
const expected = { Laptops: [7,'laptop'], 'Computers and Monitors': [5,null], Keyboards: [5,'keyboard'], Mice: [5,'mouse'], Headphones: [4,'headphones'], 'Other Accessories': [4,null] };
const counts = {};
for (const [categoryName,[count,imageType]] of Object.entries(expected)) {
  const category = after.Categories.find(c => c.Name === categoryName);
  assert.ok(category, `Missing category ${categoryName}`);
  const products = after.Products.filter(p => p.CategoryId === category.Id);
  assert.equal(products.length, count);
  counts[categoryName] = products.length;
  for (const product of products) {
    assert.ok(product.Price > 0 && product.StockQuantity >= 0 && product.Description);
    const image = imageFor({ name: product.Name });
    assert.notEqual(image, '/images/product.svg', `Generic image for ${product.Name}`);
    if (imageType) assert.equal(illustrationFor({ name: product.Name }), `/images/${imageType}.svg`, `Wrong fallback image type for ${product.Name}`);
    assert.ok(existsSync(fileURLToPath(new URL(`../OnlineStore.API/wwwroot${image}`, import.meta.url))));
  }
}
const restartPath = new URL('../verification/CatalogAfterRestart.json', import.meta.url);
if (existsSync(restartPath)) assert.deepEqual(read('CatalogAfterRestart.json'), after, 'Restart changed existing data or inserted duplicates');
const report = { verifiedAtUtc: new Date().toISOString(), totalProducts: after.Products.length, totalCategories: after.Categories.length,
  addedProducts: after.Products.length - before.Products.length, categoryCounts: counts, originalRecordsPreserved: true,
  ordersCustomersAndItemsUnchanged: true, localImagesForAllProducts: true, restartUnchanged: existsSync(restartPath) };
writeFileSync(new URL('../verification/CatalogVerification.json', import.meta.url), JSON.stringify(report, null, 2));
console.log(JSON.stringify(report, null, 2));
