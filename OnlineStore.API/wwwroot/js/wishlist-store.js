export function createWishlist(storage, changed = () => {}) {
  const key = 'onlinestore-wishlist-v1';
  let ids = [];
  try { const stored = JSON.parse(storage.getItem(key) || '[]'); if (Array.isArray(stored)) ids = [...new Set(stored.filter(id => Number.isSafeInteger(id) && id > 0))]; } catch { /* Unavailable storage starts an in-memory list. */ }
  let persistent = true;
  return {
    ids: () => [...ids], has: id => ids.includes(id), persistent: () => persistent,
    toggle(id) {
      if (!Number.isSafeInteger(id) || id < 1) return;
      ids = ids.includes(id) ? ids.filter(value => value !== id) : [...ids, id];
      try { storage.setItem(key, JSON.stringify(ids)); } catch { persistent = false; }
      changed();
    }
  };
}
export const wishlist = createWishlist({ getItem: key => globalThis.localStorage.getItem(key), setItem: (key, value) => globalThis.localStorage.setItem(key, value) }, () => globalThis.dispatchEvent(new Event('wishlistchange')));
