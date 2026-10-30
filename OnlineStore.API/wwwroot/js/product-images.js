import { productPhotos } from './product-photos.js?v=render-1';
import { sourceImages } from './catalog-data.js?v=render-1';

// Exact-name matches only: never substitute a different brand/model's photograph.
export const photoFor = product => productPhotos[product.name.trim().toLowerCase()];
export const imageAlt = product => `${product.name} — ${sourceImages(product).length ? 'DummyJSON-ის პროდუქტის ფოტო' : photoFor(product) ? 'რეალური ფოტო' : 'დროებითი საილუსტრაციო გამოსახულება'}`;

export function imageFor(product) {
  const images = sourceImages(product);
  if (images.length) return images[0];
  const photo = photoFor(product);
  if (photo) return photo.src;
  return illustrationFor(product);
}

// Unverified models keep their existing illustration; never guess a photo match.
export function illustrationFor(product) {
  const name = product.name.toLowerCase();
  if (/mousepad|gigantus|მაუსპად/.test(name)) return '/images/mousepad.svg';
  if (/webcam|c920|ვებკამერ/.test(name)) return '/images/webcam.svg';
  if (/usb-c hub|usb hub|anker 332|ჰაბი/.test(name)) return '/images/hub.svg';
  if (/portable ssd|samsung t7|გარე ssd/.test(name)) return '/images/ssd.svg';
  if (/monitor|odyssey|p2422|მონიტორ/.test(name)) return '/images/monitor.svg';
  if (/optiplex|pro tower|thinkcentre|desktop|დესკტოპ/.test(name)) return '/images/desktop.svg';
  if (/headphone|headset|wh-ch|wh-1000|cloud iii|blackshark|ყურსასმენ/.test(name)) return '/images/headphones.svg';
  if (/keyboard|k120|mx keys|g413|alloy|ornata|კლავიატურ/.test(name)) return '/images/keyboard.svg';
  if (/mouse|m185|mx master|g305|deathadder|pulsefire|მაუს/.test(name)) return '/images/mouse.svg';
  if (/laptop|ideapad|vivobook|pavilion|inspiron|loq|tuf gaming a15|victus|ლეპტოპ/.test(name)) return '/images/laptop.svg';
  return '/images/product.svg';
}
