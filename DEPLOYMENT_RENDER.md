# Render-ზე განთავსება

აპლიკაცია: Docker Web Service, Free გეგმა, Frankfurt, პორტი 10000. Frontend და API ერთ სერვისშია. SQL Server ბაზა ცალკე, მაგალითად Azure SQL-ის ანგარიშისთვის ხელმისაწვდომი უფასო შეთავაზებით. `localhost` და Windows Authentication ღრუბელში კავშირის ვარიანტი არ არის.

## მომზადებული ფაილები

- `Dockerfile`: .NET 10 Release publish და ASP.NET runtime, არაპრივილეგირებული `app` მომხმარებელი.
- `.dockerignore`: build output, Git, ლოკალური საიდუმლოებები და ბაზის ასლები image-ში არ ხვდება.
- `render.yaml`: Free სერვისი, HTTPS proxy, `/health`, ხელით deployment; პაროლები YAML-ში არ ინახება.
- Cookie ავტორიზაცია და API ფილტრი: მხოლოდ პროდუქტების/კატეგორიების/კურსის GET არის საჯარო; მომხმარებლები, შეკვეთები და ცვლილებები ადმინისტრატორს მოითხოვს.

## Render-ის კონფიგურაცია

Render Dashboard → New → Blueprint → GitHub `NikaGamkhitashvili/OnlineStore` → branch `main`. ალტერნატივაა New → Web Service → Docker; Root Directory ცარიელი, Dockerfile `./Dockerfile`, Free plan, Auto-Deploy Off.

| Environment variable | მნიშვნელობა |
|---|---|
| ASPNETCORE_ENVIRONMENT | Production |
| Hosting__RenderProxy | true, მხოლოდ Render-ის ingress-ის უკან |
| ConnectionStrings__OnlineStoreDb | ღრუბლის SQL კავშირი, TLS და სანდო სერტიფიკატის შემოწმებით |
| Admin__Username | admin ან არჩეული სახელი |
| Admin__Password | პირადი ძლიერი პაროლი, მინიმუმ 16 სიმბოლო; მხოლოდ secret გარემოს კონფიგურაციაში |
| SeedData__Enabled | false |
| SeedData__CatalogEnabled | false |

პაროლს მომხმარებელი თავად ადგენს Render-ში; GitHub-ში, README-ში ან ჩატში არ იწერება. პაროლის გარეშე შესვლა 503-ით უარყოფილია, სხვა დაცული ოპერაციები 401-ით. გვერდი `/admin` შესვლის ფორმას აჩვენებს. სესია 2 საათია, cookie HttpOnly/Secure/SameSite=Strict; პაროლი ბრაუზერის localStorage-ში არ ინახება. ცვლილების მოთხოვნები `X-Store-Request: 1` სათაურს იყენებს; CORS დაშვებული არ არის. Login შეზღუდულია 5 მცდელობით წუთში თითო უშუალო კავშირის IP-ზე. Render-ზე რამდენიმე კლიენტი შეიძლება ერთ proxy IP-ს იზიარებდეს; მცირე ერთადმინისტრატორიანი დემოსთვის ეს მისაღებია.

ეს არის დაცული სასწავლო მართვის სისტემა, ერთი ადმინისტრატორით. მომხმარებლების თვითრეგისტრაცია/პირადი ანგარიშები არ დამატებულა; დემო checkout და შეკვეთების ისტორია ადმინისტრატორის სესიას მოითხოვს. რეალური გადახდა არ ხდება.

Data Protection-ის გასაღებები Render Free-ის დროებით ფაილურ სისტემაშია; restart/redeploy-ის შემდეგ შეიძლება ხელახალი შესვლა გახდეს საჭირო. მუდმივი მომხმარებლის სესიებისთვის მომავალში საჭირო იქნება გასაღებების დაცული გარე საცავი. მომხმარებლის მონაცემები SQL-ში რჩება.

## SQL ბაზის გადატანა

არსებული ლოკალური ბაზა არ წაიშლება/არ გადაკეთდება. ასლის გადასატანად გამოიყენება Microsoft SqlPackage-ის read-only Export → BACPAC → ახალი ღრუბლის ბაზის Import. BACPAC შეიძლება შეიცავდეს მომხმარებლებსა და შეკვეთებს; ინახება მხოლოდ იგნორირებულ `tmp/`-ში და GitHub/Docker image-ში არ შედის.

თუ ახალ ცარიელ ბაზაზე მიგრაციებით მუშაობა აირჩევა, EF სქემას ქმნის, მონაცემები ცალკე უნდა ჩაიტვირთოს. startup-ზე მიგრაციები და seed ავტომატურად არ ეშვება; Production-ში DummyJSON-ის იმპორტი გამორთულია. გადატანის შემდეგ გადაამოწმეთ ყველა მიგრაცია, 68 პროდუქტი, 12 კატეგორია და შენარჩუნებული შეკვეთები.

Azure SQL firewall-ში მხოლოდ საჭირო Render outbound დიაპაზონები დაუშვით: service → Connect → Outbound. Microsoft Entra/SQL ადმინისტრატორის უფლებები და ახალი credential-ები მომხმარებელმა თავად უნდა დაადასტუროს. უფასო SQL შეთავაზებაზე ლიმიტის ამოწურვის ქცევა იყოს Auto-pause until next month; ფასიანი რესურსი შეთანხმების გარეშე არ იქმნება.

## შემოწმება

- `dotnet restore`, Debug build და Release publish შესრულებულია.
- 77 .NET ტესტი და 16 JavaScript ტესტი წარმატებულია, მათ შორის Production ავტორიზაცია, CSRF სათაური, rate limit, cookie/logout და Render HTTPS proxy.
- `Testing` გარემოში არსებული იზოლირებული უნივერსიტეტის API ტესტების ძველი კონტრაქტები შენარჩუნებულია; Production/Development ამ გამონაკლისს არ იყენებს. დაცვა ცალკე რეალურ Production კონფიგურაციაშია ტესტირებული.
- ლოკალური Docker CLI არ არის ხელმისაწვდომი; Docker image-ის რეალური build/deploy Render-ის ანგარიშის მომზადების შემდეგ უნდა შემოწმდეს.
- `/health` მხოლოდ აპლიკაციის პროცესის სიცოცხლეს ამოწმებს; SQL კავშირი დამატებით `/api/products`-ით და დაცული ოპერაციებით გადაამოწმეთ.
- Swagger ხელმისაწვდომია მხოლოდ Development-ში; Production `/swagger` 404 მოსალოდნელია.
- საბოლოო ონლაინ სტატუსი მხოლოდ წარმატებული deployment-ისა და რეალური URL/API/SQL შემოწმების შემდეგ დასტურდება.

Render-ის email verification, GitHub-ის რეპოზიტორიის წვდომა, Azure ანგარიში და უფასო SQL შეთავაზების ხელმისაწვდომობა ანგარიშის მხარეს უნდა დასრულდეს. მხოლოდ კოდის ატვირთვა საიტის წარმატებულ განთავსებას არ ნიშნავს.

წყაროები: [Render Docker](https://render.com/docs/docker), [Blueprint](https://render.com/docs/blueprint-spec), [Free limits](https://render.com/docs/free), [Outbound IP](https://render.com/docs/outbound-ip-addresses).
