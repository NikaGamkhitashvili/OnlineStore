const key = 'onlinestore.cart.v1';
const validInt = n => Number.isSafeInteger(n) && n > 0 && n <= 2147483647;

export function createCartStore(storage, changed = () => {}) {
  let memory = [];
  let storageAvailable = Boolean(storage);
  function read() {
    let parsed;
    try { parsed = storageAvailable ? JSON.parse(storage.getItem(key) || '[]') : memory; }
    catch { storageAvailable = false; parsed = memory; }
    if (!Array.isArray(parsed)) return [];
    const unique = new Map();
    for (const item of parsed) {
      if (item && validInt(item.productId) && validInt(item.quantity) && !unique.has(item.productId))
        unique.set(item.productId, { productId: item.productId, quantity: item.quantity });
      if (unique.size === 100) break;
    }
    return [...unique.values()];
  }
  function save(items) {
    memory = items;
    try { if (storageAvailable) storage.setItem(key, JSON.stringify(items)); }
    catch { storageAvailable = false; }
    changed();
  }
  return {
    items: read,
    count: () => read().reduce((sum, item) => sum + item.quantity, 0),
    persistent: () => storageAvailable,
    add(product, quantity = 1) {
      if (!validInt(product.id) || !validInt(quantity)) throw new Error('აირჩიე სწორი რაოდენობა.');
      const items = read();
      const existing = items.find(item => item.productId === product.id);
      const next = (existing?.quantity || 0) + quantity;
      if (next > product.stockQuantity) throw new Error(`კალათაში უკვე დამატებულის ჩათვლით ხელმისაწვდომია ${product.stockQuantity} ცალი.`);
      if (!existing && items.length >= 100) throw new Error('კალათაში მაქსიმუმ 100 განსხვავებული პროდუქტი დაიშვება.');
      if (existing) existing.quantity = next;
      else items.push({ productId: product.id, quantity });
      save(items);
    },
    setQuantity(productId, quantity, stock) {
      if (!validInt(quantity) || quantity > stock) throw new Error('არჩეული რაოდენობა მარაგს აღემატება.');
      save(read().map(item => item.productId === productId ? { productId, quantity } : item));
    },
    remove(productId) { save(read().filter(item => item.productId !== productId)); },
    clear() { save([]); }
  };
}

let browserStorage;
try { browserStorage = globalThis.localStorage; } catch { browserStorage = null; }
export const cart = createCartStore(browserStorage, () => {
  if (typeof window !== 'undefined') window.dispatchEvent(new Event('cartchange'));
});

export function reconcileCart(products) {
  return cart.items().map(item => ({ ...item, product: products.find(p => p.id === item.productId) }));
}
export const cartIsValid = lines => lines.length > 0 && lines.every(line => line.product && line.quantity <= line.product.stockQuantity);
export const cartTotal = lines => lines.reduce((sum, line) => sum + (line.product ? Math.round(line.product.price * 100) * line.quantity : 0), 0) / 100;
