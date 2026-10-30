using Microsoft.EntityFrameworkCore;
using OnlineStore.Core.Entities;

namespace OnlineStore.Infrastructure.Data;

// Development catalog only. Prices/configurations are illustrative GEL demo data,
// not live retail offers. Existing product fields and all orders remain untouched.
public class CatalogSeeder(OnlineStoreDbContext db, StoreTransaction transaction)
{
    private const int TargetTotal = 30;

    public async Task SeedAsync(CancellationToken ct = default) => await transaction.RunAsync(async () =>
    {
        var products = await db.Products.ToListAsync(ct);
        // A full catalog is a no-op, including after an administrator edits it.
        if (products.Count >= TargetTotal) return false;
        var categories = await db.Categories.ToListAsync(ct);
        var usedNames = products.Select(p => p.Name.Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var group in Groups)
        {
            var category = categories.FirstOrDefault(c => string.Equals(c.Name.Trim(), group.Name, StringComparison.OrdinalIgnoreCase));
            if (category is null)
            {
                category = new Category { Name = group.Name, Description = group.Description };
                categories.Add(category);
                db.Categories.Add(category);
            }

            // User-approved regrouping of the four original products, by stable names
            // and legacy category names. Never change their price, stock, name or description.
            foreach (var product in products)
            {
                var legacyCategory = categories.FirstOrDefault(c => c.Id != 0 && c.Id == product.CategoryId);
                if (legacyCategory is not null && group.LegacyProducts.TryGetValue(product.Name, out var legacyName)
                    && legacyCategory.Name == legacyName)
                    product.Category = category;
            }

            var groupCount = products.Count(p => ReferenceEquals(p.Category, category)
                || (category.Id != 0 && p.CategoryId == category.Id));
            foreach (var demo in group.Products)
            {
                if (products.Count >= TargetTotal || groupCount >= group.Target) break;
                if (!usedNames.Add(demo.Name)) continue;
                var product = new Product
                {
                    Name = demo.Name, Description = demo.Description, Price = demo.Price,
                    StockQuantity = demo.Stock, Category = category
                };
                products.Add(product);
                db.Products.Add(product);
                groupCount++;
            }
        }
        return true;
    }, ct);

    private record DemoProduct(string Name, string Description, decimal Price, int Stock);
    private record CatalogGroup(string Name, string Description, int Target,
        Dictionary<string, string> LegacyProducts, DemoProduct[] Products);

    private static readonly CatalogGroup[] Groups =
    [
        new("Laptops", "ლეპტოპები სწავლისთვის, სამუშაოდ და თამაშებისთვის", 7,
            new() { ["Lenovo IdeaPad 15"] = "კომპიუტერები" },
            [
                new("Lenovo IdeaPad 15", "15.6-დუიმიანი ყოველდღიური ლეპტოპი. დემო კომპლექტაცია: 16GB RAM, 512GB SSD.", 1899.99m, 8),
                new("ASUS Vivobook 15", "15.6-დუიმიანი ლეპტოპი სწავლისა და ოფისისთვის. დემო კომპლექტაცია: 16GB RAM, 512GB SSD.", 1799m, 12),
                new("HP Pavilion 15", "საშინაო და საოფისე ლეპტოპი Full HD ეკრანით. დემო კომპლექტაცია: 16GB RAM, 512GB SSD.", 2199m, 7),
                new("Dell Inspiron 15", "15.6-დუიმიანი ლეპტოპი დოკუმენტებისთვის, სწავლასა და ვიდეოზარებისთვის. დემო: 16GB RAM, 512GB SSD.", 2099m, 9),
                new("Lenovo LOQ 15", "გეიმინგ ლეპტოპი გაგრილების აქტიური სისტემით. დემო კომპლექტაცია: 16GB RAM, 512GB SSD, RTX 4050.", 2999m, 6),
                new("ASUS TUF Gaming A15", "15.6-დუიმიანი გეიმინგ ლეპტოპი. დემო კომპლექტაცია: Ryzen 7, 16GB RAM, 1TB SSD, RTX 4060.", 3699m, 4),
                new("HP Victus 15", "ლეპტოპი თამაშებისთვის და ყოველდღიური სამუშაოსთვის. დემო კომპლექტაცია: 16GB RAM, 512GB SSD, RTX 3050.", 2599m, 5)
            ]),
        new("Computers and Monitors", "დესკტოპ კომპიუტერები და მონიტორები სამუშაო სივრცისთვის", 5, new(),
            [
                new("Dell OptiPlex 7010 SFF", "კომპაქტური საოფისე დესკტოპი. დემო კომპლექტაცია: 16GB RAM და 512GB SSD. მონიტორი არ მოჰყვება.", 1899m, 6),
                new("HP Pro Tower 400 G9", "საოფისე Tower კომპიუტერი. დემო კომპლექტაცია: 16GB RAM და 512GB SSD. მონიტორი იყიდება ცალკე.", 1999m, 5),
                new("Lenovo ThinkCentre neo 50t", "დესკტოპ კომპიუტერი სასწავლო და ბიზნეს ამოცანებისთვის. დემო: 16GB RAM, 512GB SSD; მონიტორის გარეშე.", 1699m, 7),
                new("Samsung Odyssey G5 27", "27-დუიმიანი გეიმინგ მონიტორი. დემო ვარიანტი: QHD ეკრანი, HDMI და DisplayPort კავშირი.", 799m, 11),
                new("Dell P2422H Monitor", "23.8-დუიმიანი Full HD IPS მონიტორი რეგულირებადი სადგამით, ოფისისა და სწავლისთვის.", 599m, 10)
            ]),
        new("Keyboards", "საოფისე და გეიმინგ კლავიატურები", 5,
            new() { ["Logitech K120"] = "აქსესუარები" },
            [
                new("Logitech K120", "სრული ზომის სადენიანი USB კლავიატურა ოფისისა და სწავლისთვის.", 49.50m, 20),
                new("Logitech MX Keys S", "უსადენო კლავიატურა განათებული ღილაკებითა და რამდენიმე მოწყობილობას შორის გადართვით.", 399m, 9),
                new("Logitech G413 TKL SE", "კომპაქტური მექანიკური USB კლავიატურა TKL განლაგებითა და თეთრი განათებით.", 249m, 13),
                new("HyperX Alloy Origins Core", "კომპაქტური მექანიკური გეიმინგ კლავიატურა RGB განათებით და TKL განლაგებით.", 299m, 8),
                new("Razer Ornata V3", "დაბალი პროფილის სადენიანი გეიმინგ კლავიატურა, განათებითა და ხელის საყრდენით.", 289m, 10)
            ]),
        new("Mice", "უსადენო, საოფისე და გეიმინგ მაუსები", 5,
            new() { ["Logitech M185"] = "აქსესუარები", ["UI QA Mouse"] = "UI QA აქსესუარები" },
            [
                new("Logitech M185", "კომპაქტური უსადენო მაუსი USB მიმღებით ყოველდღიური გამოყენებისთვის.", 39.90m, 30),
                new("Logitech MX Master 3S", "ერგონომიული უსადენო მაუსი მშვიდი ღილაკებითა და სწრაფი scroll ბორბლით.", 299m, 12),
                new("Logitech G305 LIGHTSPEED", "უსადენო გეიმინგ მაუსი USB მიმღებით; მუშაობს AA ელემენტზე.", 149m, 18),
                new("Razer DeathAdder Essential", "ერგონომიული სადენიანი USB გეიმინგ მაუსი მარჯვენა ხელისთვის.", 79.90m, 22),
                new("HyperX Pulsefire Haste", "მსუბუქი სადენიანი გეიმინგ მაუსი მოქნილი კაბელითა და პერფორირებული კორპუსით.", 129.90m, 14)
            ]),
        new("Headphones", "უსადენო ყურსასმენები და გეიმინგ ჰედსეტები", 4, new(),
            [
                new("Sony WH-CH520", "მსუბუქი უსადენო Bluetooth ყურსასმენები მუსიკისა და ყოველდღიური ზარებისთვის.", 179m, 16),
                new("Sony WH-1000XM4", "უსადენო ყურსასმენები ხმაურის აქტიური შემცირებით და დასაკეცი კონსტრუქციით.", 799m, 6),
                new("HyperX Cloud III", "სადენიანი გეიმინგ ჰედსეტი მიკროფონით, კომპიუტერსა და თავსებად მოწყობილობებზე გამოსაყენებლად.", 299m, 11),
                new("Razer BlackShark V2 X", "მსუბუქი გეიმინგ ჰედსეტი მიკროფონით და 3.5მმ სადენიანი კავშირით.", 169.90m, 15)
            ]),
        new("Other Accessories", "მეხსიერება, ვებკამერები, ჰაბები და სამუშაო მაგიდის აქსესუარები", 4, new(),
            [
                new("Samsung T7 Portable SSD 1TB", "1TB მოცულობის კომპაქტური გარე SSD USB-C კავშირით, ფაილების გადასატანად და სარეზერვო ასლებისთვის.", 299m, 17),
                new("Logitech C920 HD Pro Webcam", "Full HD USB ვებკამერა ვიდეოზარებისთვის, ონლაინ სწავლებისა და შეხვედრებისთვის.", 249m, 12),
                new("Anker 332 USB-C Hub", "USB-C ჰაბი HDMI, USB-A და USB-C პორტებით, ლეპტოპის დამატებით მოწყობილობებთან დასაკავშირებლად.", 129.90m, 20),
                new("Razer Gigantus V2 Large", "დიდი ზომის ქსოვილის მაუსპადი რბილი ზედაპირითა და მოცურების საწინააღმდეგო ძირით.", 69.90m, 25)
            ])
    ];
}
