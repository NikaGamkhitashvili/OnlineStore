import { el, failure, loading } from './ui.js?v=20261031-2';
import { cart } from './cart-store.js?v=20261031-2';
import { api } from './api.js?v=20261031-2';

const path = location.pathname.replace(/\/$/, '') || '/';
const main = document.querySelector('#main');
function updateCount() {
  const count = cart.count();
  document.querySelector('#cart-count').textContent = count > 99 ? '99+' : count;
  document.querySelector('.cart-link').setAttribute('aria-label', `კალათა, ${count} ნივთი`);
}
updateCount();
window.addEventListener('cartchange', updateCount);
window.addEventListener('storage', updateCount);
document.querySelector('#exchange-rate-button').addEventListener('click', async event => {
  const button = event.currentTarget;
  const result = document.querySelector('#exchange-rate-result');
  button.disabled = true; result.textContent = 'კურსი იტვირთება…';
  try {
    const rate = await api.get('exchange-rates?baseCurrency=USD&targetCurrency=GEL');
    result.textContent = `1 USD = ${rate.rate} GEL · ${rate.date}`;
  } catch { result.textContent = 'კურსი ვერ მივიღეთ. სცადე ხელახლა.'; }
  finally { button.disabled = false; }
});
document.querySelector('#header-search').addEventListener('submit', event => {
  event.preventDefault();
  const search = document.querySelector('#global-search').value.trim();
  location.href = '/products' + (search ? `?search=${encodeURIComponent(search)}` : '');
});
document.querySelector('#global-search').value = new URLSearchParams(location.search).get('search') || '';
document.querySelector('#menu-toggle').addEventListener('click', event => {
  const button = event.currentTarget;
  const open = button.getAttribute('aria-expanded') !== 'true';
  button.setAttribute('aria-expanded', String(open));
  document.querySelector('#main-nav').classList.toggle('is-open', open);
});
const nav = path === '/' ? 'home' : path.split('/')[1];
document.querySelector(`[data-nav="${['home','products','wishlist','orders','admin'].includes(nav) ? nav : 'none'}"]`)?.setAttribute('aria-current', 'page');

async function render() {
  main.replaceChildren(loading());
  try {
    if (path === '/' || path === '/products') {
      const module = await import('./products.js?v=20261031-2'); await module.renderCatalog(main, path === '/');
    } else if (/^\/products\/\d+$/.test(path)) {
      const module = await import('./products.js?v=20261031-2'); await module.renderProduct(main, Number(path.split('/')[2]));
    } else if (path === '/wishlist') {
      const module = await import('./products.js?v=20261031-2'); await module.renderWishlist(main);
    } else if (path === '/cart') {
      const module = await import('./cart.js?v=20261031-2'); await module.renderCart(main);
    } else if (path === '/checkout') {
      const module = await import('./checkout.js?v=20261031-2'); await module.renderCheckout(main);
    } else if (path === '/orders' || /^\/orders\/\d+$/.test(path)) {
      const module = await import('./orders.js?v=20261031-2'); await module.renderOrders(main, path.split('/')[2] ? Number(path.split('/')[2]) : null);
    } else if (path === '/admin') {
      const module = await import('./admin.js?v=20261031-2'); await module.renderAdmin(main);
    } else main.replaceChildren(el('h1', {}, 'გვერდი ვერ მოიძებნა'), el('a', { href: '/' }, 'მთავარზე დაბრუნება'));
  } catch (error) { main.replaceChildren(failure(error, render)); }
}
render();
