export function el(tag, attrs = {}, ...children) {
  const node = document.createElement(tag);
  for (const [key, value] of Object.entries(attrs)) {
    if (value === null || value === undefined || (value === false && !key.startsWith('aria-'))) continue;
    if (key.startsWith('on')) node.addEventListener(key.slice(2).toLowerCase(), value);
    else if (key === 'class') node.className = value;
    else if (key === 'checked' || key === 'disabled' || key === 'selected') node[key] = value;
    else node.setAttribute(key, value === true ? '' : value);
  }
  for (const child of children.flat(Infinity)) if (child !== null && child !== undefined && child !== false)
    node.append(child instanceof Node ? child : document.createTextNode(String(child)));
  return node;
}

const paths = {
  arrow: 'M5 12h14m-6-6 6 6-6 6', plus: 'M12 5v14M5 12h14', minus: 'M5 12h14',
  bag: 'M5 7h14l1 14H4L5 7Zm4 0V5a3 3 0 0 1 6 0v2',
  box: 'm3 6 9-4 9 4v12l-9 4-9-4V6Zm0 0 9 4 9-4M12 10v12',
  edit: 'm15 4 5 5M4 20l5-1L21 7l-5-5L4 14v6Z',
  trash: 'M3 6h18M9 6V3h6v3M6 6l1 15h10l1-15M10 10v7M14 10v7',
  check: 'm5 12 4 4L19 6', close: 'm6 6 12 12M6 18 18 6',
  grid: 'M3 3h7v7H3V3Zm11 0h7v7h-7V3ZM3 14h7v7H3v-7Zm11 0h7v7h-7v-7Z',
  people: 'M16 21v-2a4 4 0 0 0-4-4H6a4 4 0 0 0-4 4v2M16 3a4 4 0 0 1 0 8M22 21v-2a4 4 0 0 0-3-4M13 7a4 4 0 1 1-8 0 4 4 0 0 1 8 0Z'
};
export function icon(name) {
  const svg = document.createElementNS('http://www.w3.org/2000/svg', 'svg');
  svg.setAttribute('viewBox', '0 0 24 24'); svg.setAttribute('aria-hidden', 'true');
  const path = document.createElementNS(svg.namespaceURI, 'path'); path.setAttribute('d', paths[name] || paths.box); svg.append(path);
  return svg;
}
export const money = value => `${new Intl.NumberFormat('ka-GE', { minimumFractionDigits: 2, maximumFractionDigits: 2 }).format(Number(value) || 0)} ₾`;
export const date = value => new Intl.DateTimeFormat('ka-GE', { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(value));
export const statusNames = { Pending: 'მოლოდინში', Completed: 'დასრულებული', Cancelled: 'გაუქმებული' };
export const badge = value => el('span', { class: `badge ${['Pending','Completed','Cancelled'].includes(value) ? value.toLowerCase() : ''}` }, statusNames[value] || value);
export function imageFor(product) {
  const name = product.name.toLowerCase();
  if (/lenovo|laptop|ideapad|ლეპტოპ|კომპიუტერ/.test(name)) return '/images/laptop.svg';
  if (/mouse|m185|მაუს/.test(name)) return '/images/mouse.svg';
  if (/keyboard|k120|კლავიატურ/.test(name)) return '/images/keyboard.svg';
  return '/images/product.svg';
}
export function toast(message, error = false) {
  const notice = el('div', { class: `toast ${error ? 'toast-error' : ''}`, role: error ? 'alert' : 'status' },
    icon(error ? 'close' : 'check'), el('span', {}, message));
  const close = el('button', { class: 'icon-button', 'aria-label': 'შეტყობინების დახურვა', onClick: () => notice.remove() }, icon('close'));
  notice.append(close); document.querySelector('#notifications').append(notice);
  setTimeout(() => notice.remove(), 6500);
}
export function empty(title, description, href = '/products', label = 'პროდუქტების ნახვა') {
  return el('section', { class: 'empty-state' }, icon('bag'), el('h2', {}, title), el('p', {}, description), el('a', { class: 'button', href }, label));
}
export function loading() { return el('div', { class: 'loading', role: 'status' }, el('span', { class: 'spinner' }), 'მონაცემები იტვირთება…'); }
export function failure(error, retry) {
  return el('section', { class: 'empty-state', role: 'alert' }, el('h2', {}, error.status === 404 ? 'გვერდი ვერ მოიძებნა' : 'ჩატვირთვა ვერ მოხერხდა'), el('p', {}, error.message), el('button', { class: 'button', onClick: retry }, 'ხელახლა ცდა'));
}
export function heading(title, description, action) {
  return el('div', { class: 'page-heading' }, el('div', {}, el('h1', {}, title), description && el('p', {}, description)), action);
}
export function breadcrumbs(label, parent = { label: 'პროდუქტები', href: '/products' }) {
  return el('nav', { class: 'breadcrumbs', 'aria-label': 'გვერდის მდებარეობა' }, el('a', { href: '/' }, 'მთავარი'), el('span', {}, '/'), el('a', { href: parent.href }, parent.label), el('span', {}, '/'), el('span', {}, label));
}
export function field(label, input) {
  return el('label', { class: 'field' }, el('span', {}, label), input);
}
export function quantityControl(value, max, onChange = () => {}, label = 'რაოდენობა') {
  const input = el('input', { type: 'number', min: 1, max: Math.max(1, max), step: 1, value, 'aria-label': label, inputmode: 'numeric' });
  const change = next => {
    if (!Number.isSafeInteger(next) || next < 1 || next > max) { toast(`რაოდენობა უნდა იყოს 1-დან ${max}-მდე.`, true); input.value = value; return; }
    value = next; input.value = next; onChange(next);
  };
  input.addEventListener('input', () => { if (input.value !== '') change(Number(input.value)); });
  input.addEventListener('blur', () => { if (input.value === '') change(0); });
  return el('div', { class: 'quantity-control' }, el('button', { type: 'button', 'aria-label': `${label} შემცირება`, onClick: () => change(value - 1), disabled: max < 1 }, icon('minus')), input,
    el('button', { type: 'button', 'aria-label': `${label} გაზრდა`, onClick: () => change(value + 1), disabled: max < 1 }, icon('plus')));
}
export function confirmAction(title, description, confirmLabel = 'დადასტურება') {
  return new Promise(resolve => {
    const dialog = el('dialog', { class: 'confirm-dialog', 'aria-labelledby': 'confirm-title' },
      el('h2', { id: 'confirm-title' }, title), el('p', {}, description));
    const finish = answer => { dialog.close(); dialog.remove(); resolve(answer); };
    dialog.append(el('div', { class: 'form-actions' }, el('button', { class: 'button secondary', onClick: () => finish(false) }, 'გაუქმება'), el('button', { class: 'button danger', onClick: () => finish(true) }, confirmLabel)));
    dialog.addEventListener('cancel', event => { event.preventDefault(); finish(false); });
    document.body.append(dialog); dialog.showModal();
  });
}
