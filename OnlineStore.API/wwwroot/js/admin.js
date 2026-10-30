import { api } from './api.js?v=render-1';
import { el, icon, money, date, badge, heading, field, toast, confirmAction, imageFor } from './ui.js?v=render-1';

const tabs = { products: 'პროდუქტები', categories: 'კატეგორიები', customers: 'მომხმარებლები', orders: 'შეკვეთები' };
const singular = { products: 'პროდუქტის', categories: 'კატეგორიის', customers: 'მომხმარებლის' };

export async function renderAdmin(main) {
  document.title = 'დემო მართვა — OnlineStore';
  const data = {};
  await Promise.all(Object.keys(tabs).map(async key => { data[key] = await api.get(key); }));
  let active = new URLSearchParams(location.search).get('tab') || 'products';
  if (!tabs[active]) active = 'products';
  const side = el('aside', { class: 'admin-sidebar' }, el('h2', {}, 'მართვის სივრცე'));
  side.append(el('button', { class: 'button secondary', onClick: async () => { await api.post('auth/logout'); location.reload(); } }, 'ანგარიშიდან გასვლა'));
  const content = el('section', { class: 'admin-content' });
  const search = el('input', { type: 'search', placeholder: 'ჩანაწერის ძებნა…', 'aria-label': 'ადმინისტრაციული ჩანაწერების ძებნა' });
  const tableArea = el('div', { class: 'table-scroll admin-table' });
  const addButton = el('button', { class: 'button', onClick: () => openEditor(active, null) });
  const title = el('h2');
  const count = el('span', { class: 'muted small', role: 'status' });
  const stats = el('div', { class: 'admin-stats' }, Object.entries(tabs).map(([key,label]) => el('div', {}, el('span', {}, label), el('strong', {}, data[key].length))));

  const fullName = customer => `${customer.firstName} ${customer.lastName}`;
  const labelFor = (type, item) => type === 'customers' ? fullName(item) : type === 'orders' ? `შეკვეთა #${item.id}` : item.name;
  const editButtons = item => el('div', { class: 'row-actions' },
    el('button', { class: 'icon-button', title: 'რედაქტირება', 'aria-label': `${labelFor(active, item)} რედაქტირება`, onClick: () => openEditor(active, item) }, icon('edit')),
    el('button', { class: 'icon-button danger-text', title: 'წაშლა', 'aria-label': `${labelFor(active, item)} წაშლა`, onClick: () => removeItem(active, item) }, icon('trash')));

  function drawTable() {
    const query = search.value.trim().toLocaleLowerCase();
    const rows = data[active].filter(item => `${item.id} ${labelFor(active,item)} ${item.email || ''}`.toLocaleLowerCase().includes(query));
    count.textContent = `${rows.length} ჩანაწერი`;
    const headers = active === 'products' ? ['პროდუქტი', 'კატეგორია', 'ფასი', 'მარაგი', 'მოქმედება'] : active === 'categories' ? ['კატეგორია', 'აღწერა', 'პროდუქტები', 'მოქმედება'] : active === 'customers' ? ['მომხმარებელი', 'ელფოსტა', 'ტელეფონი', 'მოქმედება'] : ['შეკვეთა', 'მომხმარებელი', 'თარიღი', 'სტატუსი', 'ჯამი', 'მოქმედება'];
    const body = el('tbody');
    for (const item of rows) {
      let cells;
      if (active === 'products') cells = [el('div', { class: 'table-product' }, el('img', { src: imageFor(item), alt: '', width: 56, height: 44 }), el('div', {}, el('strong', {}, item.name), el('small', {}, `ID ${item.id}`))), data.categories.find(c => c.id === item.categoryId)?.name || `#${item.categoryId}`, money(item.price), el('span', { class: `stock ${item.stockQuantity === 0 ? 'unavailable' : ''}` }, `${item.stockQuantity} ცალი`), editButtons(item)];
      if (active === 'categories') cells = [el('div', {}, el('strong', {}, item.name), el('small', { class: 'muted' }, `ID ${item.id}`)), item.description || '—', data.products.filter(p => p.categoryId === item.id).length, editButtons(item)];
      if (active === 'customers') cells = [el('div', {}, el('strong', {}, fullName(item)), el('small', { class: 'muted' }, `ID ${item.id}`)), item.email, item.phone || '—', editButtons(item)];
      if (active === 'orders') {
        const customer = data.customers.find(c => c.id === item.customerId);
        cells = [`#${item.id}`, customer ? fullName(customer) : `#${item.customerId}`, date(item.orderDate), badge(item.status), money(item.totalAmount), el('a', { class: 'text-link', href: `/orders/${item.id}` }, 'მართვა', icon('arrow'))];
      }
      body.append(el('tr', {}, cells.map(cell => el('td', {}, cell))));
    }
    if (!rows.length) body.append(el('tr', {}, el('td', { colspan: headers.length, class: 'table-empty' }, query ? 'ძებნის შესაბამისი ჩანაწერი არ მოიძებნა.' : 'ჯერ ჩანაწერი არ არის. დაამატე პირველი.')));
    tableArea.replaceChildren(el('table', {}, el('thead', {}, el('tr', {}, headers.map(text => el('th', { scope: 'col' }, text)))), body));
  }

  function selectTab(key) {
    active = key; search.value = ''; title.textContent = tabs[key];
    side.querySelectorAll('button').forEach(button => button.setAttribute('aria-pressed', String(button.dataset.tab === key)));
    addButton.hidden = key === 'orders'; addButton.replaceChildren(icon('plus'), `${singular[key] || ''} დამატება`);
    const url = new URL(location.href); url.searchParams.set('tab', key); history.replaceState(null, '', url);
    drawTable();
  }
  Object.entries(tabs).forEach(([key,label]) => side.append(el('button', { class: 'admin-tab', 'data-tab': key, onClick: () => selectTab(key) }, icon(key === 'customers' ? 'people' : key === 'orders' ? 'bag' : key === 'products' ? 'box' : 'grid'), label, el('span', {}, data[key].length))));
  const sync = el('button', { class: 'button secondary', onClick: async () => {
    sync.disabled = true; sync.textContent = 'სინქრონიზაცია მიმდინარეობს…';
    try {
      const result = await api.post('catalog-import/dummyjson');
      toast(`DummyJSON: დამატებულია ${result.created}, განახლებულია ${result.updated}. მაღაზიის ფასი და მარაგი შენარჩუნებულია.`);
      await renderAdmin(main);
    } catch (error) { toast(error.message, true); sync.disabled = false; sync.textContent = 'DummyJSON სინქრონიზაცია'; }
  } }, 'DummyJSON სინქრონიზაცია');
  side.append(sync, el('small', { class: 'muted' }, 'მხოლოდ ტექნიკა. ახალი მაღაზიის მარაგი: 0. წყაროს საცდელი მარაგი არ იყიდება.'), el('a', { href: '/swagger', class: 'admin-api-link' }, 'Swagger API ↗'));
  search.addEventListener('input', drawTable);
  content.append(el('div', { class: 'admin-section-head' }, el('div', {}, title, count), addButton), el('div', { class: 'admin-search' }, search), tableArea);
  main.replaceChildren(heading('მაღაზიის მართვა', 'კატალოგი, მომხმარებლები და შეკვეთები.'),
    el('div', { class: 'demo-banner', role: 'note' }, el('strong', {}, 'დაცული ადმინისტრირება'), el('span', {}, 'ადმინისტრატორის სესია · სასწავლო დემო · რეალური გადახდის გარეშე.')),
    stats, el('div', { class: 'admin-layout' }, side, content));
  selectTab(active);

  async function removeItem(type, item) {
    if (!await confirmAction('წავშალოთ ჩანაწერი?', `„${labelFor(type,item)}“ წაიშლება. მოქმედება შეუქცევადია. დაკავშირებული ჩანაწერების არსებობისას სერვერი წაშლას შეზღუდავს.`, 'ჩანაწერის წაშლა')) return;
    try { await api.delete(`${type}/${item.id}`); toast('ჩანაწერი წარმატებით წაიშალა.'); await renderAdmin(main); }
    catch (error) { toast(error.message, true); }
  }

  function openEditor(type, item) {
    const dialog = el('dialog', { class: 'editor-dialog', 'aria-labelledby': 'editor-title' });
    const form = el('form', { class: 'admin-form' });
    const errorBox = el('p', { class: 'inline-error', role: 'alert', hidden: true });
    const close = () => { dialog.close(); dialog.remove(); };
    dialog.addEventListener('close', () => dialog.remove());
    const fields = type === 'products' ? [
      ['name','დასახელება','text',true,100], ['description','აღწერა','textarea',false,500],
      ['price','ფასი (₾)','number',true], ['stockQuantity','მარაგი','number',true], ['categoryId','კატეგორია','select',true]
    ] : type === 'categories' ? [['name','დასახელება','text',true,100], ['description','აღწერა','textarea',false,300]] : [
      ['firstName','სახელი','text',true,50], ['lastName','გვარი','text',true,50], ['email','ელფოსტა','email',true,150], ['phone','ტელეფონი','tel',false,30]
    ];
    form.append(el('div', { class: 'dialog-heading' }, el('h2', { id: 'editor-title' }, `${singular[type]} ${item ? 'რედაქტირება' : 'დამატება'}`), el('button', { type: 'button', class: 'icon-button', 'aria-label': 'ფორმის დახურვა', onClick: close }, icon('close'))));
    const inputs = {};
    const fieldGrid = el('div', { class: 'form-grid' });
    for (const [name,label,kind,required,maxlength] of fields) {
      let input;
      const attrs = { id: `editor-${name}`, name, required, ...(maxlength ? { maxlength } : {}) };
      if (kind === 'select') {
        input = el('select', attrs, el('option', { value: '' }, 'აირჩიე კატეგორია'), data.categories.map(c => el('option', { value: c.id, selected: c.id === item?.categoryId }, c.name)));
      } else if (kind === 'textarea') input = el('textarea', { ...attrs, rows: 3 }, item?.[name] || '');
      else {
        input = el('input', { ...attrs, type: kind, value: item?.[name] ?? (name === 'stockQuantity' ? 0 : ''),
          ...(kind === 'number' ? { min: name === 'price' ? '0.01' : 0, max: name === 'price' ? '9999999999999999.99' : 2147483647, step: name === 'price' ? '0.01' : 1 } : {}) });
      }
      input.addEventListener('input', () => input.setCustomValidity(''));
      inputs[name] = input; fieldGrid.append(field(label + (required ? ' *' : ''), input));
    }
    const submit = el('button', { class: 'button', type: 'submit' }, item ? 'ცვლილებების შენახვა' : 'შენახვა');
    form.append(fieldGrid);
    if (type === 'products' && !data.categories.length) form.append(el('p', { class: 'inline-error' }, 'პროდუქტის დამატებამდე შექმენი კატეგორია.'));
    form.append(errorBox,
      el('div', { class: 'form-actions' }, el('button', { class: 'button secondary', type: 'button', onClick: close }, 'გაუქმება'), submit));
    form.addEventListener('submit', async event => {
      event.preventDefault();
      for (const input of Object.values(inputs)) if (input.required && !input.value.trim()) input.setCustomValidity('შეავსე სავალდებულო ველი.');
      if (!form.reportValidity() || submit.disabled) return;
      const payload = Object.fromEntries(Object.entries(inputs).map(([name,input]) => [name, ['price','stockQuantity','categoryId'].includes(name) ? Number(input.value) : input.value.trim()]));
      if (type === 'customers' && !payload.phone) payload.phone = null;
      submit.disabled = true; submit.textContent = 'ინახება…'; errorBox.hidden = true;
      try {
        if (item) await api.put(`${type}/${item.id}`, payload); else await api.post(type, payload);
        close(); toast('მონაცემები წარმატებით შეინახა.'); await renderAdmin(main);
      } catch (error) { errorBox.textContent = error.message; errorBox.hidden = false; submit.disabled = false; submit.textContent = 'ხელახლა შენახვა'; }
    });
    dialog.append(form); document.body.append(dialog); dialog.showModal(); inputs[fields[0][0]].focus();
  }
}
