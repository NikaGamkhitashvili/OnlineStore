export const sourceFor = product => product.importMetadata?.source || null;
export const brandFor = product => product.importMetadata?.brand || '';
export const ratingFor = product => Number.isFinite(sourceFor(product)?.rating) ? sourceFor(product).rating : null;

export function catalogPage(products, { search = '', categoryId = 0, inStock = false, minPrice = '', maxPrice = '', minRating = 0, sort = 'default', page = 1, pageSize = 12 } = {}) {
  const query = search.trim().toLocaleLowerCase();
  const filtered = products.filter(p => (!categoryId || p.categoryId === categoryId)
    && (!inStock || p.stockQuantity > 0)
    && `${p.name} ${brandFor(p)}`.toLocaleLowerCase().includes(query)
    && (minPrice === '' || p.price >= Number(minPrice))
    && (maxPrice === '' || p.price <= Number(maxPrice))
    && (!(Number(minRating) > 0) || (ratingFor(p) !== null && ratingFor(p) >= Number(minRating))));
  const compare = {
    'price-asc': (a,b) => a.price - b.price,
    'price-desc': (a,b) => b.price - a.price,
    name: (a,b) => a.name.localeCompare(b.name, 'ka'),
    rating: (a,b) => (ratingFor(b) ?? -1) - (ratingFor(a) ?? -1),
    default: (a,b) => Number(Boolean(b.importMetadata)) - Number(Boolean(a.importMetadata)) || a.id - b.id
  }[sort];
  filtered.sort(compare || ((a,b) => a.id - b.id));
  const pages = Math.max(1, Math.ceil(filtered.length / pageSize));
  page = Math.max(1, Math.min(pages, Math.trunc(Number(page)) || 1));
  return { products: filtered.slice((page - 1) * pageSize, page * pageSize), total: filtered.length, page, pages };
}

export function sourceImages(product) {
  const source = sourceFor(product);
  return [...new Set([...(source?.images || []), source?.thumbnail])].filter(value => {
    try { const url = new URL(value); return url.protocol === 'https:' && url.hostname === 'cdn.dummyjson.com' && url.pathname.startsWith('/product-images/') && !url.username && !url.password; }
    catch { return false; }
  });
}
