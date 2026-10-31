// Model-wide specifications only; memory/storage variants are never guessed.
// Sources: https://support.apple.com/en-bh/111902, https://support.apple.com/en-ph/111871,
// https://support.apple.com/en-ie/111886, Dell P2422H datasheet and Samsung Tab S8+ datasheet.
const reviewed = {
  'Apple MacBook Pro 14 Inch Space Grey': { 'პროცესორი': 'Apple M1 Pro', 'ეკრანი': '14.2″ Liquid Retina XDR (mini-LED)', 'განახლების სიხშირე': 'ადაპტიური, 120 Hz-მდე', 'გარჩევადობა': '3024 × 1964' },
  'iPhone 13 Pro': { 'ეკრანი': '6.1″ OLED · Super Retina XDR', 'განახლების სიხშირე': 'ProMotion, 120 Hz-მდე', 'პროცესორი': 'Apple A15 Bionic' },
  'iPad Mini 2021 Starlight': { 'ეკრანი': '8.3″ Liquid Retina · IPS', 'გარჩევადობა': '2266 × 1488', 'პროცესორი': 'Apple A15 Bionic', 'პორტი': 'USB-C' },
  'Samsung Galaxy Tab S8 Plus Grey': { 'ეკრანი': '12.4″ Super AMOLED', 'განახლების სიხშირე': '120 Hz', 'გარჩევადობა': '2800 × 1752', 'პროცესორი': 'Snapdragon 8 Gen 1' },
  'Dell P2422H Monitor': { 'ეკრანი': '23.8″ IPS', 'გარჩევადობა': '1920 × 1080 · Full HD', 'განახლების სიხშირე': '60 Hz', 'პორტები': 'HDMI · DisplayPort · VGA' }
};

export function specificationsFor(product, categoryName = '') {
  if (reviewed[product.name]) return Object.entries(reviewed[product.name]);
  const description = product.description || '';
  const specs = {};
  const ram = description.match(/\b(\d+)\s*GB\s*RAM\b/i);
  const ssd = description.match(/\b(\d+)\s*(GB|TB)\s*SSD\b/i);
  const gpu = description.match(/\bRTX\s*\d{4}\b/i);
  if (ram) specs['ოპერატიული მეხსიერება'] = `${ram[1]} GB RAM`;
  if (ssd) specs['მეხსიერება'] = `${ssd[1]} ${ssd[2].toUpperCase()} SSD`;
  if (gpu) specs['ვიდეობარათი'] = `NVIDIA GeForce ${gpu[0]}`;
  if (/Ryzen 7/i.test(description)) specs['პროცესორი'] = 'AMD Ryzen 7';
  if (/OLED/i.test(description)) specs['ეკრანის ტიპი'] = /AMOLED/i.test(description) ? 'AMOLED' : 'OLED';
  if (/Full HD/i.test(description)) specs['გარჩევადობა'] = 'Full HD';
  else if (/QHD/i.test(description)) specs['გარჩევადობა'] = 'QHD';
  if (/touchscreen/i.test(description)) specs['ეკრანი'] = 'სენსორული';
  if (/2-in-1/i.test(description)) specs['კონსტრუქცია'] = '2-in-1 · ლეპტოპი / ტაბლეტი';
  if (/dual screens/i.test(description)) specs['ეკრანი'] = 'ორი ეკრანი';
  if (/InfinityEdge/i.test(description)) specs['ეკრანი'] = 'InfinityEdge';
  if (/Bluetooth/i.test(description)) specs['კავშირი'] = 'Bluetooth';
  else if (/უსადენო|wireless/i.test(description)) specs['კავშირი'] = 'უსადენო';
  else if (/სადენიანი/i.test(description)) specs['კავშირი'] = 'სადენიანი';
  if (/3\.5მმ/i.test(description)) specs['კონექტორი'] = '3.5 მმ';
  else if (/USB-C/i.test(description)) specs['კონექტორი'] = 'USB-C';
  else if (/USB/i.test(description)) specs['კონექტორი'] = 'USB';
  if (/მექანიკური/i.test(description)) specs['მექანიზმი'] = 'მექანიკური';
  if (/TKL/i.test(description)) specs['განლაგება'] = 'TKL · ციფრული ბლოკის გარეშე';
  if (/RGB/i.test(description)) specs['განათება'] = 'RGB';
  else if (/თეთრი განათებით/i.test(description)) specs['განათება'] = 'თეთრი';
  else if (/განათებ/i.test(description)) specs['განათება'] = 'კი';
  if (/მიკროფონით/i.test(description)) specs['მიკროფონი'] = 'კი';
  if (/ხმაურის აქტიური|active noise cancellation/i.test(description)) specs['ხმაურის შემცირება'] = 'აქტიური · ANC';
  if (/AA ელემენტზე/i.test(description)) specs['კვება'] = 'AA ელემენტი';
  if (/ქსოვილის/i.test(description)) specs['ზედაპირი'] = 'ქსოვილი';
  if (/S Pen/i.test(description)) specs['სტილუსის მხარდაჭერა'] = 'S Pen';
  if (/12 hours/i.test(description)) specs['ბატარეა'] = '12 საათამდე';
  if (/LED light/i.test(description)) specs['განათების ტიპი'] = 'LED';
  if (/MagSafe/i.test(description)) specs['ტექნოლოგია'] = 'MagSafe';
  if (/Face ID/i.test(description)) specs['ბიომეტრია'] = 'Face ID';
  if (/fingerprint/i.test(description)) specs['ბიომეტრია'] = 'თითის ანაბეჭდი';
  if (/Alexa/i.test(description)) specs['ხმოვანი ასისტენტი'] = 'Alexa';
  else if (/Siri/i.test(description)) specs['ხმოვანი ასისტენტი'] = 'Siri';
  if (categoryName === 'Smartphones' || categoryName === 'Tablets') {
    specs['ოპერაციული სისტემა'] = /^(iPhone|iPad)/i.test(product.name) ? (/^iPad/.test(product.name) ? 'iPadOS' : 'iOS') : 'Android';
  }
  if (!Object.keys(specs).length) specs['მოდელი'] = product.name;
  return Object.entries(specs).slice(0, 5);
}
