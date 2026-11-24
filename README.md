# OnlineLearningPlatform

OnlineLearningPlatform არის ონლაინ სწავლების პლატფორმის პროექტი. მისი მიზანია მოსწავლეებს მისცეს კურსებისა და გაკვეთილების შესწავლის, დავალებების შესრულებისა და პროგრესის თვალყურის დევნების საშუალება. მასწავლებლებს უნდა შეეძლოთ კურსებისა და სასწავლო მასალების მართვა, ხოლო ადმინისტრატორებს — მომხმარებლებისა და კურსების მოდერაცია.

> **მიმდინარე მდგომარეობა:** ეს `main` branch შეიცავს Backend-ის საწყის სტრუქტურასა და პროექტის საერთო build კონფიგურაციას. ქვემოთ აღწერილი მომხმარებლის შესაძლებლობები პროექტის მიზნობრივი ფუნქციებია და არა ყველა უკვე დასრულებული ფუნქციის დადასტურება.

## მიზნობრივი როლები

| როლი | დაგეგმილი შესაძლებლობები |
|---|---|
| **Student (მოსწავლე)** | ანგარიშის შექმნა, კურსების ნახვა და ჩარიცხვა, გაკვეთილების გავლა, დავალებების ჩაბარება და შეფასებების ნახვა. |
| **Teacher (მასწავლებელი)** | კურსებისა და გაკვეთილების შექმნა, დავალებების მომზადება და ნამუშევრების შეფასება. |
| **Admin (ადმინისტრატორი)** | მომხმარებლებისა და როლების მართვა, კურსების განხილვა და შიგთავსის მოდერაცია. |

## ტექნოლოგიური არჩევანი

| ნაწილი | ტექნოლოგია |
|---|---|
| Backend | .NET 10, ASP.NET Core Web API, C# |
| მონაცემთა ბაზა | Microsoft SQL Server, Entity Framework Core |
| ავთენტიფიკაცია | ASP.NET Core Identity და JWT (დაგეგმილი) |
| შემოწმება და ლოგირება | FluentValidation და Serilog (დაგეგმილი) |
| API დოკუმენტაცია | Swagger / OpenAPI (დაგეგმილი) |
| Frontend | Angular და TypeScript (დაგეგმილი) |
| ტესტები და კონტეინერები | xUnit, Docker, Docker Compose (დაგეგმილი) |

## პროექტის სტრუქტურა

```text
src/
  OnlineLearningPlatform.Domain/          დომენის ობიექტები და წესები
  OnlineLearningPlatform.Application/     გამოყენების შემთხვევები, DTO-ები, ვალიდაცია და ინტერფეისები
  OnlineLearningPlatform.Infrastructure/  მონაცემთა ბაზა და გარე ინტეგრაციები
  OnlineLearningPlatform.Api/             API, middleware და ავთენტიფიკაციის კონფიგურაცია
tests/
  OnlineLearningPlatform.UnitTests/
  OnlineLearningPlatform.IntegrationTests/
frontend/                                  Angular აპლიკაცია (დაგეგმილი)
deploy/                                    deployment რესურსები (დაგეგმილი)
docs/                                      არქიტექტურისა და დიზაინის დოკუმენტები
```

დამოკიდებულებები მიმართულია შიდა ფენებისკენ. დეტალებისთვის იხილეთ [არქიტექტურის დოკუმენტი](docs/architecture.md).

## საჭირო ხელსაწყოები

- .NET SDK 10.0.401 (`global.json`-ში მითითებული ვერსია)
- Git
- Docker Desktop WSL2 backend-ით და Node.js/npm — იმ ეტაპებისთვის, სადაც ეს ხელსაწყოები იქნება საჭირო

## Build და ტესტები

```powershell
git clone https://github.com/nksdal/Online-Learning-Platform.git
cd Online-Learning-Platform
dotnet --version
dotnet tool restore
dotnet build
dotnet test
```

`dotnet --version`-მა უნდა აჩვენოს `10.0.401`. კონკრეტული გაშვების ინსტრუქციები დაემატება, როცა API, მონაცემთა ბაზისა და Frontend-ის გაშვების გარემო ამ branch-ში იქნება მზად.

## უსაფრთხოება

არასოდეს შეინახოთ პაროლები, API key-ები ან სხვა საიდუმლო მნიშვნელობები Git-ში. გამოიყენეთ `.env` ან .NET User Secrets; `.env` იგნორირებულია. GitHub-ზე ატვირთული `.env.example` უნდა შეიცავდეს მხოლოდ ცარიელ მნიშვნელობებს ან მაგალითებს.
