import { api } from './api.js?v=20261031-2';
import { el, icon, money, date, badge, statusNames, heading, breadcrumbs, empty, toast, confirmAction } from './ui.js?v=20261031-2';

export async function orderActions(order, afterChange) {
  const container = el('div', { class: 'order-actions' });
  if (order.status === 'Pending') {
    for (const status of ['Completed', 'Cancelled']) {
      const button = el('button', { class: `button ${status === 'Completed' ? '' : 'secondary'}`, onClick: async () => {
        if (!await confirmAction('შეკვეთის სტატუსის შეცვლა', `შეკვეთა #${order.id} გახდება „${statusNames[status]}“. ${status === 'Cancelled' ? 'მარაგი დაბრუნდება.' : 'დასრულებული შეკვეთა საბოლოოა.'}`, 'სტატუსის შეცვლა')) return;
        button.disabled = true;
        try { await api.put(`orders/${order.id}/status`, { status }); toast('შეკვეთის სტატუსი განახლდა.'); await afterChange(); }
        catch (error) { toast(error.message, true); button.disabled = false; }
      } }, status === 'Completed' ? 'დასრულება' : 'შეკვეთის გაუქმება');
      container.append(button);
    }
  }
  if (order.status === 'Pending' || order.status === 'Cancelled') {
    const remove = el('button', { class: 'button danger-outline', onClick: async () => {
      if (!await confirmAction('წავშალოთ შეკვეთა?', `შეკვეთა #${order.id} და მისი პოზიციები წაიშლება. მოქმედება შეუქცევადია.`, 'შეკვეთის წაშლა')) return;
      remove.disabled = true;
      try { await api.delete(`orders/${order.id}`); toast('შეკვეთა წაიშალა.'); location.href = '/orders'; }
      catch (error) { toast(error.message, true); remove.disabled = false; }
    } }, icon('trash'), 'წაშლა');
    container.append(remove);
  }
  return container;
}

export async function renderOrders(main, id = null) {
  const [orders, customers, products] = await Promise.all([id ? api.get(`orders/${id}`).then(order => [order]) : api.get('orders'), api.get('customers'), api.get('products')]);
  const customerName = order => { const c = customers.find(c => c.id === order.customerId); return c ? `${c.firstName} ${c.lastName}` : `მომხმარებელი #${order.customerId}`; };
  document.title = id ? `შეკვეთა #${id} — OnlineStore` : 'შეკვეთები — OnlineStore';
  if (id) {
    const order = orders[0];
    const customer = customers.find(c => c.id === order.customerId);
    const items = el('div', { class: 'table-scroll' }, el('table', {}, el('thead', {}, el('tr', {}, ['პროდუქტი', 'რაოდენობა', 'შეძენის ფასი', 'ჯამი'].map(s => el('th', { scope: 'col' }, s)))),
      el('tbody', {}, order.items.map(item => el('tr', {}, el('td', {}, products.find(p => p.id === item.productId)?.name || `პროდუქტი #${item.productId}`), el('td', {}, item.quantity), el('td', {}, money(item.unitPrice)), el('td', {}, money(item.subtotal)))))));
    main.replaceChildren(breadcrumbs(`შეკვეთა #${id}`, { label: 'შეკვეთები', href: '/orders' }), heading(`შეკვეთა #${id}`, date(order.orderDate), badge(order.status)),
      el('section', { class: 'order-detail' }, el('div', { class: 'order-customer' }, el('div', {}, el('span', { class: 'muted small' }, 'მომხმარებელი'), el('h2', {}, customerName(order)), customer && el('p', {}, customer.email), customer?.phone && el('p', {}, customer.phone)),
        el('div', { class: 'order-final-total' }, el('span', {}, 'შეკვეთის თანხა'), el('strong', {}, money(order.totalAmount)))), items,
        el('p', { class: 'muted small historical-note' }, 'პოზიციებში ნაჩვენებია შეკვეთის შექმნის მომენტში შენახული ფასები.'), await orderActions(order, () => renderOrders(main, id))));
    return;
  }
  const list = el('div', { class: 'orders-list' });
  const filter = el('select', { 'aria-label': 'შეკვეთების სტატუსით გაფილტვრა' }, el('option', { value: '' }, 'ყველა სტატუსი'), Object.entries(statusNames).map(([value,label]) => el('option', { value }, label)));
  function update() {
    const visible = [...orders].reverse().filter(o => !filter.value || o.status === filter.value);
    list.replaceChildren(...visible.map(order => el('article', { class: 'order-card' },
      el('div', { class: 'order-card-head' }, el('div', {}, el('a', { class: 'order-id', href: `/orders/${order.id}` }, `შეკვეთა #${order.id}`), el('p', { class: 'muted small' }, date(order.orderDate))), badge(order.status)),
      el('div', { class: 'order-card-body' }, el('div', {}, el('strong', {}, customerName(order)), el('p', { class: 'muted small' }, order.items.map(i => `${products.find(p => p.id === i.productId)?.name || `#${i.productId}`} × ${i.quantity}`).join(' · '))),
        el('div', { class: 'order-card-end' }, el('strong', { class: 'price' }, money(order.totalAmount)), el('a', { class: 'text-link', href: `/orders/${order.id}` }, 'დეტალები', icon('arrow')))))));
    if (!visible.length) list.replaceChildren(empty('შეკვეთა არ მოიძებნა', 'სასურველი პროდუქტები კალათაში დაამატე და პირველი შეკვეთა გააფორმე.'));
  }
  filter.addEventListener('change', update);
  main.replaceChildren(heading('შეკვეთები', 'ყველა შეკვეთა და მისი დეტალები ერთ სივრცეში.', el('a', { class: 'button', href: '/products' }, icon('plus'), 'ახალი შეკვეთა')), el('div', { class: 'orders-toolbar' }, el('span', { class: 'muted' }, `სულ ${orders.length} შეკვეთა`), filter), list);
  update();
}
