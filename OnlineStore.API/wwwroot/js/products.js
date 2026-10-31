import { api } from './api.js?v=render-1';
import { cart } from './cart-store.js?v=render-1';
import { photoFor, imageAlt } from './product-images.js?v=render-1';
import { catalogPage, sourceFor, sourceImages, ratingFor, brandFor } from './catalog-data.js?v=render-1';
import { wishlist } from './wishlist-store.js?v=render-1';
import { el, icon, money, imageFor, toast, empty, heading, breadcrumbs, quantityControl } from './ui.js?v=render-1';

const categoryLabel = name => ({
  Laptops: 'ლეპტოპები',
  'Computers and Monitors': 'კომპიუტერები და მონიტორები',
  Keyboards: 'კლავიატურები', Mice: 'მაუსები', Headphones: 'ყურსასმენები',
  'Other Accessories': 'სხვა აქსესუარები', Smartphones: 'სმარტფონები',
  Tablets: 'ტაბლეტები', 'Mobile Accessories': 'მობილურის აქსესუარები'
}[name] || name);
function addToCart(product, quantity = 1) {
  try {
    cart.add(product, quantity);
    toast(`${product.name} დაემატა კალათაში.`);
    if (!cart.persistent()) toast('ბრაუზერმა შენახვა შეზღუდა — კალათა მხოლოდ ამ გვერდის გახსნისას შენარჩუნდება.', true);
  } catch (error) { toast(error.message, true); }
}

function wishlistButton(product) {
  const button = el('button', { class: 'button secondary wishlist-button', 'aria-pressed': wishlist.has(product.id), onClick: () => {
    wishlist.toggle(product.id); update();
    if (!wishlist.persistent()) toast('ბრაუზერში შენახვა შეზღუდულია — სია მხოლოდ ამ გვერდზე შენარჩუნდება.', true);
  } });
  function update() { button.textContent = wishlist.has(product.id) ? 'სიიდან ამოშლა' : 'სურვილების სიაში'; button.setAttribute('aria-pressed', String(wishlist.has(product.id))); }
  update(); return button;
}

function ratingLabel(product) {
  const source = sourceFor(product);
  return el('div', { class: 'product-source-summary' },
    ratingFor(product) !== null && el('span', {}, `შეფასება ${ratingFor(product)} / 5`),
    source?.discountPercentage > 0 && el('small', {}, `წყაროს დემო ფასდაკლება: ${source.discountPercentage}%`));
}

function productCard(product, categories) {
  const category = categoryLabel(categories.find(c => c.id === product.categoryId)?.name) || 'პროდუქტი';
  const button = el('button', { class: 'button add-button', disabled: product.stockQuantity === 0, onClick: () => addToCart(product), 'aria-label': `${product.name} კალათაში დამატება` }, icon('plus'), 'კალათაში');
  return el('article', { class: 'product-card' },
    el('a', { class: 'product-image', href: `/products/${product.id}`, 'aria-label': `${product.name} დეტალები` }, el('img', { src: imageFor(product), alt: imageAlt(product), class: photoFor(product) || sourceImages(product).length ? 'real-photo' : null, loading: 'lazy', width: 420, height: 300 }),
      product.stockQuantity === 0 && el('span', { class: 'sold-out' }, 'ამოწურულია')),
    el('div', { class: 'product-info' }, el('span', { class: 'category-label' }, category),
      el('h3', {}, el('a', { href: `/products/${product.id}` }, product.name)),
      brandFor(product) && el('small', { class: 'muted' }, brandFor(product)), ratingLabel(product),
      el('span', { class: `stock ${product.stockQuantity ? '' : 'unavailable'}` }, product.stockQuantity ? `${product.stockQuantity} ცალი მარაგში` : 'ამჟამად არ არის მარაგში'),
      el('div', { class: 'product-card-bottom' }, el('strong', { class: 'price' }, money(product.price)), button), wishlistButton(product)));
}

export async function renderCatalog(main, home) {
  const [products, categories] = await Promise.all([api.get('products'), api.get('categories')]);
  document.title = home ? 'OnlineStore — შენი ყოველდღიური არჩევანი' : 'პროდუქტები — OnlineStore';
  const params = new URLSearchParams(location.search);
  let activeCategory = Number(params.get('categoryId')) || 0;
  let page = Number(params.get('page')) || 1;
  const search = el('input', { id: 'catalog-search', type: 'search', value: params.get('search') || '', placeholder: 'სახელით ან ბრენდით ძებნა', maxlength: 100 });
  const sort = el('select', { id: 'catalog-sort', 'aria-label': 'პროდუქტების დალაგება' },
    el('option', { value: 'default' }, 'რეკომენდებული'), el('option', { value: 'price-asc' }, 'ფასი: დაბლიდან მაღლისკენ'), el('option', { value: 'price-desc' }, 'ფასი: მაღლიდან დაბლისკენ'), el('option', { value: 'name' }, 'სახელის მიხედვით'), el('option', { value: 'rating' }, 'შეფასების მიხედვით'));
  sort.value = params.get('sort') || 'default';
  const inStock = el('input', { type: 'checkbox', id: 'in-stock' });
  const minPrice = el('input', { type: 'number', min: 0, step: '0.01', value: params.get('minPrice') || '', 'aria-label': 'მინიმალური ფასი ლარში', placeholder: 'მინ. ₾' });
  const maxPrice = el('input', { type: 'number', min: 0, step: '0.01', value: params.get('maxPrice') || '', 'aria-label': 'მაქსიმალური ფასი ლარში', placeholder: 'მაქს. ₾' });
  const minRating = el('select', { 'aria-label': 'მინიმალური შეფასება' }, el('option', { value: 0 }, 'ყველა შეფასება'), [3,4,4.5].map(r => el('option', { value: r }, `${r} და მეტი`)));
  minRating.value = params.get('minRating') || '0';
  const pagination = el('nav', { class: 'catalog-pagination', 'aria-label': 'კატალოგის გვერდები' });
  const count = el('p', { class: 'result-count', role: 'status' });
  const grid = el('div', { class: 'product-grid' });
  const categoryList = el('div', { class: 'category-list' });
  const categoryChoices = [{ id: 0, name: 'ყველა პროდუქტი' }, ...categories.filter(c => products.some(p => p.categoryId === c.id))];
  function chooseCategory(id) { activeCategory = id; page = 1; update(); }
  function update() {
    const query = search.value.trim().toLocaleLowerCase();
    const result = catalogPage(products, { search: search.value, categoryId: activeCategory, inStock: inStock.checked, minPrice: minPrice.value, maxPrice: maxPrice.value, minRating: minRating.value, sort: sort.value, page });
    const visible = result.products; page = result.page;
    count.textContent = `ნაპოვნია ${result.total} პროდუქტი · გვერდი ${page} / ${result.pages}`;
    grid.replaceChildren(...visible.map(p => productCard(p, categories)));
    if (!visible.length) grid.replaceChildren(empty('პროდუქტი ვერ მოიძებნა', 'შეცვალე საძიებო სიტყვა ან კატეგორია.', '/products', 'ფილტრების გასუფთავება'));
    categoryList.replaceChildren(...categoryChoices.map(c => el('button', { class: activeCategory === c.id ? 'category-filter active' : 'category-filter', 'aria-pressed': activeCategory === c.id, onClick: () => chooseCategory(c.id) }, el('span', {}, categoryLabel(c.name)), el('span', { class: 'category-count' }, c.id ? products.filter(p => p.categoryId === c.id).length : products.length))));
    const url = new URL(location.href);
    activeCategory ? url.searchParams.set('categoryId', activeCategory) : url.searchParams.delete('categoryId');
    query ? url.searchParams.set('search', search.value.trim()) : url.searchParams.delete('search');
    for (const [key, value] of Object.entries({ page, sort: sort.value, minPrice: minPrice.value, maxPrice: maxPrice.value, minRating: minRating.value })) {
      value === '' || value === '0' || value === 'default' || (key === 'page' && value === 1) ? url.searchParams.delete(key) : url.searchParams.set(key, value);
    }
    history.replaceState(null, '', url);
    pagination.replaceChildren(el('button', { class: 'button secondary', disabled: page === 1, onClick: () => { page--; update(); } }, 'წინა'),
      el('span', { role: 'status' }, `${page} / ${result.pages}`), el('button', { class: 'button secondary', disabled: page === result.pages, onClick: () => { page++; update(); } }, 'შემდეგი'));
    pagination.hidden = result.pages === 1;
  }
  for (const control of [search, sort, inStock, minPrice, maxPrice, minRating]) control.addEventListener(control.tagName === 'SELECT' || control.type === 'checkbox' ? 'change' : 'input', () => { page = 1; update(); });
  const sidebar = el('aside', { class: 'catalog-sidebar', 'aria-label': 'კატალოგის ფილტრები' }, el('h2', {}, 'კატეგორიები'), categoryList,
    el('div', { class: 'filter-section' }, el('h2', {}, 'ხელმისაწვდომობა'), el('label', { class: 'check-label' }, inStock, 'მხოლოდ მარაგში')),
    el('div', { class: 'filter-section price-filter' }, el('h2', {}, 'ფასი და შეფასება'), minPrice, maxPrice, minRating),
    el('div', { class: 'sidebar-note' }, icon('box'), el('p', {}, 'მოძებნე სასურველი ნივთი და დაამატე კალათაში.')));
  const header = heading(home ? 'აღმოაჩინე შენი შემდეგი არჩევანი' : 'პროდუქტების კატალოგი', 'კომპიუტერები, აქსესუარები და ყოველდღიური საჭიროებები.', el('a', { class: 'text-link', href: home ? '/products' : '/cart' }, home ? 'სრული კატალოგი' : 'კალათის ნახვა', icon('arrow')));
  if (home) {
    const title = header.querySelector('h1');
    title.replaceWith(el('h2', { class: 'catalog-title' }, title.textContent));
  }
  const content = el('section', { class: 'catalog-results', 'aria-label': 'პროდუქტები' }, el('div', { class: 'catalog-toolbar' }, el('label', { class: 'search-field', for: 'catalog-search' }, el('span', { class: 'sr-only' }, 'კატალოგში ძებნა'), search), sort), count, grid, pagination);
  main.replaceChildren();
  if (home) main.append(el('section', { class: 'hero' }, el('div', { class: 'hero-copy' }, el('h1', {}, 'კარგი არჩევანი.\nყოველდღე.'), el('p', {}, 'ტექნიკა და აქსესუარები, რომლებიც შენს ყოველდღიურობას ერგება. აღმოაჩინე ის, რაც გჭირდება.'), el('a', { class: 'button hero-button', href: '#catalog' }, 'აღმოაჩინე პროდუქტები', icon('arrow'))), el('div', { class: 'hero-art' }, el('img', { src: '/images/laptop.svg', alt: 'ლეპტოპის საილუსტრაციო გამოსახულება', width: 600, height: 400 }), el('span', { class: 'hero-art-label' }, 'შენი სამუშაო სივრცისთვის'))));
  const catalog = el('section', { id: 'catalog' }, header, el('div', { class: 'catalog-layout' }, sidebar, content));
  main.append(catalog); update();
}

export async function renderProduct(main, id) {
  const [product, categories] = await Promise.all([api.get(`products/${id}`), api.get('categories')]);
  document.title = `${product.name} — OnlineStore`;
  const quantityPicker = quantityControl(1, product.stockQuantity);
  const category = categories.find(c => c.id === product.categoryId);
  const available = product.stockQuantity > 0;
  const source = sourceFor(product);
  const images = sourceImages(product);
  const photo = photoFor(product);
  const mainImage = el('img', { src: imageFor(product), alt: imageAlt(product), class: photo || images.length ? 'real-photo' : null, width: 600, height: 430 });
  const thumbnails = el('div', { class: 'product-gallery', 'aria-label': 'პროდუქტის ფოტოები' });
  images.forEach((src, index) => thumbnails.append(el('button', { class: 'gallery-choice', 'aria-label': `${product.name} ფოტო ${index + 1}`, 'aria-pressed': index === 0, onClick: event => {
    mainImage.src = src;
    thumbnails.querySelectorAll('button').forEach(button => button.setAttribute('aria-pressed', String(button === event.currentTarget)));
  } }, el('img', { src, alt: '', width: 64, height: 64, loading: 'lazy' }))));
  main.replaceChildren(breadcrumbs(product.name), el('section', { class: 'product-detail' },
    el('div', { class: 'detail-image' }, mainImage, thumbnails),
    el('div', { class: 'detail-copy' }, el('a', { class: 'category-label', href: `/products?categoryId=${product.categoryId}` }, categoryLabel(category?.name) || 'კატეგორია'), el('h1', {}, product.name), el('p', { class: 'detail-description' }, product.description || 'ამ პროდუქტისთვის დამატებითი აღწერა არ არის მითითებული.'),
      brandFor(product) && el('p', {}, `ბრენდი: ${brandFor(product)}`), ratingLabel(product),
      el('strong', { class: 'detail-price' }, money(product.price)), el('p', { class: `stock ${available ? '' : 'unavailable'}` }, available ? `${product.stockQuantity} ცალი ხელმისაწვდომია` : 'მარაგი ამოწურულია'),
      available && el('div', { class: 'purchase-controls' }, quantityPicker, el('button', { class: 'button', onClick: () => addToCart(product, Number(quantityPicker.querySelector('input').value)) }, icon('bag'), 'კალათაში დამატება')),
      !available && el('button', { class: 'button', disabled: true }, 'ამჟამად არ არის მარაგში'),
      wishlistButton(product),
      el('div', { class: 'detail-note' }, icon('box'), el('p', {}, 'რაოდენობასა და საბოლოო თანხას შეკვეთის გაფორმებისას გადავამოწმებთ.')))));
  if (source) {
    const specs = el('dl', { class: 'product-specs' });
    const fields = {
      id: 'DummyJSON ID', category: 'წყაროს კატეგორია', sku: 'SKU', stock: 'წყაროს საცდელი მარაგი (არ იყიდება)',
      availabilityStatus: 'წყაროს დემო ხელმისაწვდომობა', warrantyInformation: 'წყაროს დემო გარანტია', shippingInformation: 'წყაროს დემო მიწოდება',
      returnPolicy: 'წყაროს დემო დაბრუნების პირობა', minimumOrderQuantity: 'წყაროს დემო მინიმალური რაოდენობა', weight: 'წონა (წყაროს ერთეული)',
      dimensions: 'ზომები (წყაროს ერთეული)', tags: 'ტეგები', price: 'წყაროს ფასი (USD-ის დაშვებით)', discountPercentage: 'წყაროს დემო ფასდაკლება (%)'
    };
    for (const [key, label] of Object.entries(fields)) if (source[key] !== undefined && source[key] !== null)
      specs.append(el('dt', {}, label), el('dd', {}, typeof source[key] === 'object' ? JSON.stringify(source[key]) : source[key]));
    const reviews = el('section', { class: 'product-reviews' }, el('h2', {}, 'წყაროს დემო შეფასებები'));
    if (!source.reviews?.length) reviews.append(el('p', {}, 'შეფასება არ არის მოწოდებული.'));
    for (const review of source.reviews || []) reviews.append(el('article', {},
      el('strong', {}, review.reviewerName || 'სახელი არ არის მითითებული'),
      el('p', {}, review.rating === undefined ? '' : `${review.rating} / 5`, review.date ? ` · ${review.date.slice(0,10)}` : ''), el('p', {}, review.comment || '')));
    main.append(el('section', { class: 'source-details' }, el('h2', {}, 'მახასიათებლები და წყაროს ინფორმაცია'),
      el('p', { class: 'muted' }, 'ეს ინფორმაცია DummyJSON-ის სასწავლო მონაცემებია და მაღაზიის გარანტიის ან მიწოდების დაპირებას არ წარმოადგენს. GEL ფასი მაღაზიაში ცალკე იმართება; წყაროს ფასდაკლება დამატებით არ გამოიქვითება.'), specs,
      el('details', {}, el('summary', {}, 'წყაროს სრული მეტამონაცემები'), el('pre', { class: 'source-json' }, JSON.stringify(source, null, 2)))), reviews);
  }
}

export async function renderWishlist(main) {
  document.title = 'სურვილების სია — OnlineStore';
  const [products, categories] = await Promise.all([api.get('products'), api.get('categories')]);
  const grid = el('div', { class: 'product-grid' });
  function draw() {
    const saved = products.filter(p => wishlist.has(p.id));
    grid.replaceChildren(...saved.map(p => productCard(p, categories)));
    if (!saved.length) grid.replaceChildren(empty('სურვილების სია ცარიელია', 'სასურველი პროდუქტები შეინახე კატალოგიდან. სია ამ ბრაუზერში ინახება.'));
  }
  main.replaceChildren(heading('სურვილების სია', 'შენახულია ამ ბრაუზერში · ანგარიშის შექმნა საჭირო არ არის.'), grid);
  window.addEventListener('wishlistchange', draw); draw();
}
