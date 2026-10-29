import test from 'node:test';
import assert from 'node:assert/strict';
import { createCartStore, cartIsValid, cartTotal } from '../OnlineStore.API/wwwroot/js/cart-store.js';

function storage(initial = '[]') {
  let value = initial;
  return { getItem: () => value, setItem: (_, next) => { value = next; } };
}
const product = { id: 1, name: 'Mouse', price: 12.35, stockQuantity: 5 };

test('cart persists only IDs and quantities, merging repeated additions', () => {
  const data = storage(); const cart = createCartStore(data);
  cart.add(product, 2); cart.add(product, 1);
  assert.deepEqual(createCartStore(data).items(), [{ productId: 1, quantity: 3 }]);
  assert.equal(cart.count(), 3);
});
test('add and quantity changes cannot exceed stock', () => {
  const cart = createCartStore(storage()); cart.add(product, 4);
  assert.throws(() => cart.add(product, 2));
  assert.throws(() => cart.setQuantity(1, 6, 5));
  for (const quantity of [0, -1, 1.5, NaN, Infinity]) assert.throws(() => cart.add(product, quantity));
  assert.equal(cart.count(), 4);
});
test('zero-stock products cannot be added', () => {
  assert.throws(() => createCartStore(storage()).add({ ...product, stockQuantity: 0 }));
});
test('quantity change, removal and clearing persist correctly', () => {
  const data = storage(); const cart = createCartStore(data); cart.add(product);
  cart.setQuantity(1, 3, 5); assert.equal(cart.count(), 3);
  cart.remove(1); assert.equal(cart.count(), 0);
  cart.add(product); cart.clear(); assert.deepEqual(createCartStore(data).items(), []);
});
test('corrupt or manipulated browser storage is sanitized', () => {
  for (const text of ['{bad', 'null', '{}']) assert.deepEqual(createCartStore(storage(text)).items(), []);
  const cart = createCartStore(storage('[null,{"productId":1,"quantity":2},{"productId":1,"quantity":5},{"productId":2,"quantity":-1},{"productId":"3","quantity":2}]'));
  assert.deepEqual(cart.items(), [{ productId: 1, quantity: 2 }]);
});
test('blocked browser storage degrades to an in-memory cart', () => {
  const cart = createCartStore({ getItem: () => { throw new Error('blocked'); }, setItem: () => { throw new Error('blocked'); } });
  cart.add(product, 2); assert.equal(cart.count(), 2); assert.equal(cart.persistent(), false);
});
test('deleted products or reduced stock block checkout', () => {
  assert.equal(cartIsValid([]), false);
  assert.equal(cartIsValid([{ quantity: 1, product: undefined }]), false);
  assert.equal(cartIsValid([{ quantity: 6, product }]), false);
  assert.equal(cartIsValid([{ quantity: 2, product }]), true);
});
test('estimated totals use current prices and integer cents', () => {
  assert.equal(cartTotal([{ quantity: 3, product }, { quantity: 2, product: { price: 25.10 } }]), 87.25);
});
test('cart rejects more than 100 unique lines', () => {
  const cart = createCartStore(storage());
  for (let id = 1; id <= 100; id++) cart.add({ ...product, id });
  assert.throws(() => cart.add({ ...product, id: 101 }));
  assert.equal(cart.items().length, 100);
});
