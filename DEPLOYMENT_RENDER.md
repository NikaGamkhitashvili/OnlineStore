# Render-ზე განთავსება

აპლიკაცია: Render Docker Web Service, Free გეგმა, Frankfurt, პორტი 10000. Frontend და API ერთ სერვისშია; SQL Server ბაზა MonsterASP.NET-ის Free გეგმაზეა. `localhost` და Windows Authentication ღრუბელში კავშირის ვარიანტი არ არის.

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
| ConnectionStrings__OnlineStoreDb | MonsterASP-ის SQL კავშირი; მხოლოდ Render-ის secret კონფიგურაციაში |
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

MonsterASP-ის ახალ ცარიელ ბაზაში ასლი Microsoft SqlPackage Import-ით გადატანილია: 68 პროდუქტი, 12 კატეგორია, 3 მომხმარებელი, 3 შეკვეთა, 4 შეკვეთის პოზიცია, 38 იმპორტის ჩანაწერი და 3 EF მიგრაცია. ლოკალური ბაზა შენარჩუნებულია. Remote Access ჩართულია მომხმარებლის თანხმობით. პროვაიდერის მითითებით გამოიყენება `Encrypt=True;TrustServerCertificate=True`; კავშირი დაშიფრულია, თუმცა სერტიფიკატის ნდობის შემოწმება გამორთულია, რაც მომხმარებელმა ცალკე დაადასტურა. პაროლი დოკუმენტაციაში არ ინახება.

## შემოწმება

- `dotnet restore`, Debug build და Release publish შესრულებულია.
- 77 .NET ტესტი და 16 JavaScript ტესტი წარმატებულია, მათ შორის Production ავტორიზაცია, CSRF სათაური, rate limit, cookie/logout და Render HTTPS proxy.
- `Testing` გარემოში არსებული იზოლირებული უნივერსიტეტის API ტესტების ძველი კონტრაქტები შენარჩუნებულია; Production/Development ამ გამონაკლისს არ იყენებს. დაცვა ცალკე რეალურ Production კონფიგურაციაშია ტესტირებული.
- Render-ზე Docker image-ის build და deployment წარმატებით დასრულდა, commit `4d4abcc`, 2026-10-31.
- `/health` მხოლოდ აპლიკაციის პროცესის სიცოცხლეს ამოწმებს; SQL კავშირი დამატებით `/api/products`-ით და დაცული ოპერაციებით გადაამოწმეთ.
- Swagger ხელმისაწვდომია მხოლოდ Development-ში; Production `/swagger` 404 მოსალოდნელია.
- საბოლოო ონლაინ სტატუსი მხოლოდ წარმატებული deployment-ისა და რეალური URL/API/SQL შემოწმების შემდეგ დასტურდება.

ონლაინ მისამართი: https://onlinestore-nikagamkhitashvili.onrender.com/

რეალური შემოწმება: `/health` 200; `/api/products` 200 და 68 ჩანაწერი; `/api/categories` 200 და 12 ჩანაწერი; `/api/products?categoryId=7` 200 და 5 მაუსი. ანონიმური `/api/customers` და `/api/orders` 401; `/api/auth/session` 200 და `authenticated:false`; Production `/swagger/index.html` 404. ბრაუზერში კატალოგი ჩაიტვირთა, Mice ფილტრი და კალათაში დამატება მუშაობს, `/admin` შესვლის ფორმას აჩვენებს. მომხმარებლის მიერ დაყენებული პაროლით შესვლა ამ შემოწმების ფარგლებში არ შესრულებულა; ღრუბლის ბაზაში სატესტო შეკვეთები არ შექმნილა.

Render Free უმოქმედობისას იძინებს; შემდგომი პირველი მოთხოვნა შეიძლება 50 წამზე მეტხანს გაგრძელდეს. ბაზა გარე SQL Server-ზეა და Render-ის restart მას არ შლის. ახალი იმპორტირებული პროდუქტების ნულოვანი მარაგი არსებული დემო პოლიტიკაა; თანხები/მარაგი განთავსებისას არ შეცვლილა. ლოკალური და ღრუბლის ბაზები დამოუკიდებელი ასლებია.

Render და MonsterASP ანგარიშები მომზადებულია; Azure არ გამოიყენება. მხოლოდ კოდის ატვირთვა საიტის წარმატებულ განთავსებას არ ნიშნავს.

წყაროები: [Render Docker](https://render.com/docs/docker), [Blueprint](https://render.com/docs/blueprint-spec), [Free limits](https://render.com/docs/free), [Outbound IP](https://render.com/docs/outbound-ip-addresses).
