import { api } from './api.js?v=render-1';
import { cart, reconcileCart, cartIsValid } from './cart-store.js?v=render-1';
import { orderSummary } from './cart.js?v=render-1';
import { el, icon, money, heading, empty, field } from './ui.js?v=render-1';

export async function renderCheckout(main) {
  document.title = 'შეკვეთის გაფორმება — OnlineStore';
  if (!cart.items().length) { main.replaceChildren(empty('კალათა ცარიელია', 'შეკვეთის გასაფორმებლად ჯერ პროდუქტი დაამატე.')); return; }
  const [products, customers] = await Promise.all([api.get('products'), api.get('customers')]);
  const lines = reconcileCart(products);
  if (!cartIsValid(lines)) { main.replaceChildren(empty('კალათას გადამოწმება სჭირდება', 'ზოგი პროდუქტის რაოდენობა ან ხელმისაწვდომობა შეიცვალა.', '/cart', 'კალათის გადამოწმება')); return; }
  const snapshot = JSON.stringify(cart.items());
  const customerSelect = el('select', { id: 'checkout-customer', name: 'customerId', required: true }, el('option', { value: '' }, 'აირჩიე მომხმარებელი'),
    customers.map(c => el('option', { value: c.id }, `${c.firstName} ${c.lastName} · ${c.email}`)));
  const customerInfo = el('div', { class: 'customer-preview' });
  customerSelect.addEventListener('change', () => {
    const customer = customers.find(c => c.id === Number(customerSelect.value));
    customerInfo.replaceChildren(...(customer ? [el('strong', {}, `${customer.firstName} ${customer.lastName}`), el('span', {}, customer.email), customer.phone && el('span', {}, customer.phone)].filter(Boolean) : []));
  });
  const submit = el('button', { type: 'submit', form: 'checkout-form', class: 'button full', disabled: !customers.length }, 'შეკვეთის დადასტურება', icon('arrow'));
  const errorBox = el('p', { class: 'inline-error', role: 'alert', hidden: true });
  const review = el('div', { class: 'review-items' }, lines.map(line => el('div', { class: 'review-item' }, el('div', {}, el('strong', {}, line.product.name), el('span', { class: 'muted small' }, `${line.quantity} × ${money(line.product.price)}`)), el('strong', {}, money(line.quantity * line.product.price)))));
  const form = el('form', { id: 'checkout-form', class: 'checkout-form' },
    el('section', { class: 'form-section' }, el('h2', {}, 'ვისთვის ვაფორმებთ შეკვეთას?'), el('p', { class: 'muted' }, 'აირჩიე სისტემაში არსებული მომხმარებელი.'), field('მომხმარებელი', customerSelect), customerInfo,
      !customers.length && el('p', { class: 'inline-error' }, 'ჯერ მომხმარებელი არ არსებობს. ', el('a', { href: '/admin?tab=customers' }, 'დაამატე დემო მართვის გვერდიდან.'))),
    el('section', { class: 'form-section' }, el('h2', {}, 'შეამოწმე შენი არჩევანი'), review, el('a', { class: 'text-link', href: '/cart' }, 'კალათის შეცვლა')),
    errorBox);
  form.addEventListener('submit', async event => {
    event.preventDefault();
    if (!form.reportValidity() || submit.disabled) return;
    if (snapshot !== JSON.stringify(cart.items())) { errorBox.textContent = 'კალათა სხვა ჩანართში შეიცვალა. გადადი კალათაში და გადაამოწმე.'; errorBox.hidden = false; return; }
    submit.disabled = true; submit.textContent = 'შეკვეთა იგზავნება…'; errorBox.hidden = true;
    try {
      const order = await api.post('orders', { customerId: Number(customerSelect.value), items: lines.map(line => ({ productId: line.productId, quantity: line.quantity })) });
      cart.clear();
      main.replaceChildren(el('section', { class: 'success-state' }, el('div', { class: 'success-icon' }, icon('check')), el('h1', {}, 'შეკვეთა მიღებულია!'),
        el('p', {}, `შენი შეკვეთის ნომერია #${order.id}.`), el('div', { class: 'success-total' }, el('span', {}, 'სერვერზე გამოთვლილი ჯამი'), el('strong', {}, money(order.totalAmount))),
        el('p', { class: 'muted' }, 'სასწავლო დემო — თანხა არ ჩამოგჭრია და გადახდა არ განხორციელებულა.'),
        el('div', { class: 'form-actions' }, el('a', { class: 'button', href: `/orders/${order.id}` }, 'შეკვეთის ნახვა', icon('arrow')), el('a', { class: 'button secondary', href: '/products' }, 'შოპინგის გაგრძელება'))));
      main.focus();
    } catch (error) {
      errorBox.textContent = `${error.status === 409 ? 'მარაგი შეიცვალა. დაბრუნდი კალათაში და გადაამოწმე რაოდენობა. ' : ''}${error.message}`;
      errorBox.hidden = false; errorBox.scrollIntoView({ block: 'nearest' }); submit.disabled = false; submit.textContent = 'შეკვეთის დადასტურება';
    }
  });
  main.replaceChildren(heading('შეკვეთის გაფორმება', 'ერთი ნაბიჯი შენს ახალ არჩევანამდე.', el('a', { class: 'text-link', href: '/cart' }, 'კალათაში დაბრუნება')),
    el('div', { class: 'checkout-layout' }, form, orderSummary(lines, submit)));
}
