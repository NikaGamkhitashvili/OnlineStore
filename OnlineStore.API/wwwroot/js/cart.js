import { api } from './api.js';
import { cart, reconcileCart, cartIsValid, cartTotal } from './cart-store.js';
import { photoFor } from './product-images.js';
import { el, icon, money, imageFor, empty, heading, quantityControl, toast } from './ui.js';

export function orderSummary(lines, action) {
  return el('aside', { class: 'order-summary' }, el('h2', {}, 'შეკვეთის შეჯამება'),
    el('div', { class: 'summary-line' }, el('span', {}, 'პროდუქტები'), el('span', {}, `${lines.reduce((s,l) => s + l.quantity, 0)} ცალი`)),
    el('div', { class: 'summary-total' }, el('span', {}, 'სავარაუდო ჯამი'), el('strong', {}, money(cartTotal(lines)))),
    el('p', { class: 'muted small' }, 'საბოლოო თანხა სერვერზე გამოითვლება შეკვეთის შექმნისას.'), action,
    el('p', { class: 'small summary-disclaimer' }, 'სასწავლო დემო — გადახდა არ ხდება.'));
}

export async function renderCart(main) {
  document.title = 'შენი კალათა — OnlineStore';
  if (!cart.items().length) {
    main.replaceChildren(heading('შენი კალათა', 'შეინახე აქ შენი შემდეგი არჩევანი.'), empty('კალათა ჯერ ცარიელია', 'დაათვალიერე პროდუქტები და დაამატე სასურველი ნივთები.'));
    return;
  }
  const products = await api.get('products');
  const lines = reconcileCart(products);
  const list = el('div', { class: 'cart-items' });
  const summary = el('div');
  const refreshSummary = () => {
    const action = cartIsValid(lines) ? el('a', { class: 'button full', href: '/checkout' }, 'შეკვეთის გაფორმება', icon('arrow')) : el('button', { class: 'button full', disabled: true }, 'შეამოწმე კალათა');
    summary.replaceChildren(orderSummary(lines, action));
  };
  for (const line of lines) {
    const { product, productId, quantity } = line;
    const unavailable = !product || product.stockQuantity < quantity;
    const warning = unavailable ? el('p', { class: 'inline-error', role: 'alert' }, product ? `მარაგში დარჩენილია ${product.stockQuantity} ცალი. შეამცირე რაოდენობა ან ამოშალე ნივთი.` : 'შეკვეთამდე ამოშალე ეს ნივთი.') : null;
    const remove = el('button', { class: 'text-button remove-button', onClick: () => { cart.remove(productId); renderCart(main).catch(error => toast(error.message, true)); }, 'aria-label': `${product?.name || 'პროდუქტი'} კალათიდან ამოშლა` }, icon('trash'), 'ამოშლა');
    const subtotal = el('strong', { class: 'cart-subtotal' }, product ? money(product.price * quantity) : '—');
    list.append(el('article', { class: 'cart-item' }, el('img', { src: product ? imageFor(product) : '/images/product.svg', class: product && photoFor(product) ? 'real-photo' : null, alt: '', width: 140, height: 110 }),
      el('div', { class: 'cart-item-copy' }, el('h2', {}, product ? el('a', { href: `/products/${productId}` }, product.name) : `პროდუქტი #${productId} აღარ არის ხელმისაწვდომი`),
        product && el('p', { class: 'muted' }, money(product.price), ' / ცალი'),
        warning, remove),
      product && product.stockQuantity > 0 && quantityControl(quantity, product.stockQuantity, next => {
        try { cart.setQuantity(productId, next, product.stockQuantity); line.quantity = next; subtotal.textContent = money(product.price * next); warning?.remove(); refreshSummary(); }
        catch (error) { toast(error.message, true); }
      }, `${product.name} რაოდენობა`),
      subtotal));
  }
  refreshSummary();
  main.replaceChildren(heading('შენი კალათა', `${lines.length} განსხვავებული პროდუქტი`, el('a', { class: 'text-link', href: '/products' }, 'შოპინგის გაგრძელება', icon('arrow'))),
    el('div', { class: 'checkout-layout' }, list, summary));
}
