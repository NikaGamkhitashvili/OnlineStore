"""Download only the individually reviewed photographs below. No database writes.

Requires Pillow. Run from any directory with Python; network access is needed only
when preparing assets, never when displaying them in the storefront.
"""
import hashlib
import html
import io
import json
import re
import time
from pathlib import Path
from urllib.parse import quote, urlsplit
from urllib.request import Request, urlopen
from urllib.error import HTTPError

from PIL import Image, ImageOps

ROOT = Path(__file__).resolve().parents[1]
DEST = ROOT / 'OnlineStore.API/wwwroot/images/products'
EVIDENCE = ROOT / 'verification/photo-sources'
# Exact model identity and licenses were reviewed on each file's description page.
SOURCES = [
    ('Logitech M185', 'Logitech M185 mouse HS05.jpg', 'Hayden Schiff', 'CC BY 4.0', 'by/4.0', 'logitech-m185'),
    ('Logitech K120', 'Logitech K120 Keyboard.jpg', 'TaurusEmerald', 'CC BY-SA 4.0', 'by-sa/4.0', 'logitech-k120'),
    ('ASUS Vivobook 15', 'Asus Vivobook 15.jpg', 'LeopardWin', 'CC0 1.0', 'publicdomain/zero/1.0', 'asus-vivobook-15'),
    ('Lenovo ThinkCentre neo 50t', 'Lenovo ThinkCentre neo 50t obverse.jpg', 'Rakoon', 'CC0 1.0', 'publicdomain/zero/1.0', 'lenovo-thinkcentre-neo-50t'),
    ('Logitech MX Master 3S', 'Logitech MX Master 3S HS01.jpg', 'Hayden Schiff', 'CC BY 4.0', 'by/4.0', 'logitech-mx-master-3s'),
    ('Logitech G305 LIGHTSPEED', 'Une souris Logitech G305..jpg', 'Julien Sinan', 'CC BY-SA 4.0', 'by-sa/4.0', 'logitech-g305'),
    ('Logitech C920 HD Pro Webcam', 'Webcam 01.jpg', 'Pmwiki1', 'CC0 1.0', 'publicdomain/zero/1.0', 'logitech-c920'),
    ('HP Pavilion 15', 'HP Pavilion 15 cs3095nr.png', 'Dienthoaiquangcao68', 'CC BY-SA 4.0', 'by-sa/4.0', 'hp-pavilion-15'),
]
UNSPLASH = [
    ('Sony WH-1000XM4', 'Varad Murti', 'https://unsplash.com/photos/black-wireless-headphones-on-white-table-GxWwjOjJ5Ss',
     'https://images.unsplash.com/photo-1614860243518-c12eb2fdf66c?fm=jpg&w=1600&q=90&fit=max', 'sony-wh-1000xm4'),
    ('Samsung T7 Portable SSD 1TB', 'Subhra Jyoti Paul', 'https://unsplash.com/photos/samsung-portable-ssd-next-to-a-backlit-keyboard-sYoBuj-0zms',
     'https://images.unsplash.com/photo-1756836857604-6888b1ff6a1b?fm=jpg&w=1600&q=90&fit=max', 'samsung-t7'),
]


def download(url):
    if urlsplit(url).hostname not in {'commons.wikimedia.org', 'upload.wikimedia.org', 'images.unsplash.com'}:
        raise ValueError('Unexpected photo host')
    cache = ROOT / 'tmp/product-photo-downloads' / hashlib.sha256(url.encode()).hexdigest()
    if cache.exists():
        return cache.read_bytes()
    for attempt in range(3):
        try:
            with urlopen(Request(url, headers={'User-Agent': 'OnlineStoreUniversityProject/1.0'}), timeout=45) as response:
                data = response.read()
            cache.parent.mkdir(parents=True, exist_ok=True)
            cache.write_bytes(data)
            return data
        except HTTPError as error:
            if error.code != 429 or attempt == 2:
                raise
            time.sleep(10 * (attempt + 1))


def save_photo(raw, slug):
    with Image.open(io.BytesIO(raw)) as image:
        if image.format not in {'JPEG', 'MPO', 'PNG', 'WEBP'}:
            raise ValueError('Not a supported photograph: ' + str(image.format))
        photo = ImageOps.exif_transpose(image).convert('RGBA')
        photo.thumbnail((900, 660), Image.Resampling.LANCZOS)
        canvas = Image.new('RGB', (960, 720), 'white')
        canvas.paste(photo, ((960 - photo.width) // 2, (720 - photo.height) // 2), photo)
        target = DEST / (slug + '.webp')
        canvas.save(target, 'WEBP', quality=85, method=6)
    return target


def main():
    DEST.mkdir(parents=True, exist_ok=True)
    EVIDENCE.mkdir(parents=True, exist_ok=True)
    manifest = DEST / 'sources.json'
    photos = json.loads(manifest.read_text(encoding='utf-8')) if manifest.exists() else {}
    for model, title, author, license_name, license_path, slug in SOURCES:
        if model.lower() in photos:
            continue
        source = 'https://commons.wikimedia.org/wiki/File:' + quote(title.replace(' ', '_'))
        page = download(source).decode('utf-8')
        license_url = ('https://creativecommons.org/' if license_path.startswith('publicdomain')
                       else 'https://creativecommons.org/licenses/') + license_path + '/'
        if license_path not in page:
            raise ValueError('License changed; review before downloading: ' + source)
        match = re.search(r'fullImageLink[^>]*>\s*<a href="([^"]+)"', page)
        if not match:
            raise ValueError('Original image link missing: ' + source)
        original_url = html.unescape(match[1]).split('?')[0]
        raw = download(original_url)
        target = save_photo(raw, slug)
        (EVIDENCE / (slug + '.html')).write_text(page, encoding='utf-8')
        photos[model.lower()] = {
            'model': model, 'src': '/images/products/' + target.name,
            'author': author, 'source': source, 'originalUrl': original_url,
            'license': license_name, 'licenseUrl': license_url,
            'changes': 'Resized, centered on a white 960×720 canvas and converted to WebP; metadata removed.',
            'checkedOn': '2026-10-31', 'sha256': hashlib.sha256(target.read_bytes()).hexdigest(),
            'originalSha256': hashlib.sha256(raw).hexdigest(),
        }
        print(model + ': ' + str(target.stat().st_size) + ' bytes')
    # Each source page explicitly identifies the model and says "Free to use under
    # the Unsplash License"; reviewed alongside https://unsplash.com/license.
    for model, author, source, original_url, slug in UNSPLASH:
        if model.lower() in photos:
            continue
        raw = download(original_url)
        target = save_photo(raw, slug)
        photos[model.lower()] = {
            'model': model, 'src': '/images/products/' + target.name,
            'author': author, 'source': source, 'originalUrl': original_url,
            'license': 'Unsplash License', 'licenseUrl': 'https://unsplash.com/license',
            'changes': 'Resized, centered on a white 960×720 canvas and converted to WebP; metadata removed.',
            'checkedOn': '2026-10-31', 'sha256': hashlib.sha256(target.read_bytes()).hexdigest(),
            'originalSha256': hashlib.sha256(raw).hexdigest(),
        }
        print(model + ': ' + str(target.stat().st_size) + ' bytes')
    output = '// Individually reviewed real photographs. See /images/products/credits.html and sources.json.\nexport const productPhotos = '
    output += json.dumps(photos, ensure_ascii=False, indent=2) + ';\n'
    (ROOT / 'OnlineStore.API/wwwroot/js/product-photos.js').write_text(output, encoding='utf-8')
    (DEST / 'sources.json').write_text(json.dumps(photos, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')


if __name__ == '__main__':
    main()
