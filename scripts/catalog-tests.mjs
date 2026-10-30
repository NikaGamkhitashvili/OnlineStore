import test from 'node:test';
import assert from 'node:assert/strict';
import { catalogPage, sourceImages } from '../OnlineStore.API/wwwroot/js/catalog-data.js';
import { createWishlist } from '../OnlineStore.API/wwwroot/js/wishlist-store.js';
import { createCartStore, cartTotal } from '../OnlineStore.API/wwwroot/js/cart-store.js';

const products = [
  { id:1, name:'Local mouse', price:20, stockQuantity:5, categoryId:1 },
  { id:31, name:'Phone', price:100, stockQuantity:0, categoryId:2, importMetadata:{ brand:'Acme', source:{ rating:4.8, images:['https://cdn.dummyjson.com/product-images/phone/1.webp'] } } },
  { id:32, name:'Laptop', price:500, stockQuantity:2, categoryId:3, importMetadata:{ brand:'Acme', source:{ rating:3.5 } } }
];
test('brand/name search, category, inventory, price and rating filters compose', () => {
  assert.equal(catalogPage(products,{minRating:'0'}).total,3);
  assert.equal(catalogPage(products,{search:'ACME'}).total,2);
  assert.equal(catalogPage(products,{search:'mouse'}).products[0].id,1);
  assert.equal(catalogPage(products,{categoryId:2,minPrice:90,maxPrice:110,minRating:4}).products[0].id,31);
  assert.equal(catalogPage(products,{inStock:true,minRating:4}).total,0);
  assert.equal(catalogPage(products,{minPrice:600,maxPrice:100}).total,0);
});
test('sorting and pagination are stable, clamped and do not mutate API products', () => {
  for (const [sort,first] of [['price-asc',1],['price-desc',32],['name',32],['rating',31]]) assert.equal(catalogPage(products,{sort}).products[0].id,first);
  assert.equal(catalogPage(products,{page:99,pageSize:2}).page,2);
  assert.equal(catalogPage(products,{page:-1,pageSize:2}).products.length,2);
  assert.deepEqual(products.map(p=>p.id),[1,31,32]);
});
test('gallery accepts only unique, safe DummyJSON product URLs', () => {
  const good='https://cdn.dummyjson.com/product-images/phone/1.webp';
  assert.deepEqual(sourceImages({importMetadata:{source:{images:[good,good,'javascript:alert(1)','https://example.com/a.webp','https://cdn.dummyjson.com/elsewhere.png','https://x:y@cdn.dummyjson.com/product-images/x'],thumbnail:good}}}),[good]);
  assert.deepEqual(sourceImages(products[0]),[]);
});
test('wishlist persists internal IDs, toggles, sanitizes and handles unavailable storage', () => {
  let text='[31,31,null,-1,"32"]'; const storage={getItem:()=>text,setItem:(_,v)=>{text=v;}};
  const list=createWishlist(storage); assert.deepEqual(list.ids(),[31]); list.toggle(32);
  assert.deepEqual(createWishlist(storage).ids(),[31,32]); list.toggle(31); assert.deepEqual(list.ids(),[32]);
  const blocked=createWishlist({getItem:()=>{throw Error();},setItem:()=>{throw Error();}}); blocked.toggle(31); assert.equal(blocked.has(31),true); assert.equal(blocked.persistent(),false);
});
test('imported products use internal cart IDs and store prices, never source stock', () => {
  let saved='[]'; const cart=createCartStore({getItem:()=>saved,setItem:(_,value)=>{saved=value;}});
  assert.throws(()=>cart.add(products[1])); cart.add(products[2],2);
  assert.deepEqual(cart.items(),[{productId:32,quantity:2}]);
  assert.equal(cartTotal(cart.items().map(item=>({...item,product:products.find(p=>p.id===item.productId)}))),1000);
});
