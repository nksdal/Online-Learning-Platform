# Nikladori — Online Learning Platform

Nikladori არის ონლაინ სასწავლო პლატფორმა, სადაც მოსწავლეებს შეუძლიათ კურსების აღმოჩენა, გაკვეთილების გავლა, დავალებების ჩაბარება და შეფასებების ნახვა. მასწავლებლები მართავენ კურსებსა და სასწავლო მასალებს, ადმინისტრატორები კი მართავენ ანგარიშებსა და კურსების გამოქვეყნების პროცესს.

> **პროექტის მდგომარეობა:** Backend და Angular ინტერფეისები მოიცავს მოსწავლის, მასწავლებლისა და ადმინისტრატორის ძირითად სამუშაო პროცესებს. მხარდაჭერილია გაკვეთილის ვიდეოები, დავალების ფაილები და ადმინისტრატორის სტატისტიკა. ელფოსტისთვის გამოიყენება Gmail SMTP; საჭიროა ცალკე Gmail ანგარიში და App Password. სისტემა ჯერ არ არის განთავსებული production გარემოში.

## მომხმარებლის როლები

| როლი | შესაძლებლობები |
|---|---|
| **მოსწავლე (Student)** | ანგარიშის შექმნა და დადასტურება, კურსების ნახვა და ჩარიცხვა, გაკვეთილების გავლა, დავალებების ჩაბარება, დისკუსიებში მონაწილეობა და შეფასებების ნახვა. |
| **მასწავლებელი (Teacher)** | საკუთარი კურსებისა და გაკვეთილების შექმნა/მართვა, დავალებების შექმნა და მოსწავლეების ნამუშევრების შეფასება. როლს ანიჭებს ადმინისტრატორი. |
| **ადმინისტრატორი (Admin)** | მომხმარებლებისა და როლების მართვა, კურსების დამტკიცება ან უარყოფა, კატეგორიებისა და შიგთავსის მოდერაცია, სტატისტიკის ნახვა. |

თითოეულ ანგარიშს აქვს ერთი როლი. ახალი ანგარიშის საწყისი როლია Student.

## ტექნოლოგიები

- **Backend:** .NET 10, ASP.NET Core Web API, C#
- **Frontend:** Angular 21, TypeScript
- **მონაცემთა ბაზა:** Microsoft SQL Server, Entity Framework Core
- **ავთენტიფიკაცია:** ASP.NET Core Identity, JWT, განახლებადი refresh token-ები HttpOnly cookie-ში
- **ელფოსტა:** Gmail SMTP, MailKit
- **ფაილები და სურათები:** Cloudinary (არასავალდებულო)
- **ტესტები:** xUnit, Vitest
- **ლოკალური გარემო:** Docker Compose

## საჭირო ხელსაწყოები

- .NET SDK 10.0.401
- Docker Desktop და WSL2
- Node.js 24.x და npm 11.x
- Git

## გაშვება ლოკალურად

ჩამოტვირთეთ კოდი და აღადგინეთ .NET ინსტრუმენტები:

```powershell
git clone https://github.com/nksdal/Online-Learning-Platform.git
cd Online-Learning-Platform
dotnet tool restore
dotnet build
dotnet test
```

შექმენით პირადი `.env` ფაილი `.env.example`-ის ასლიდან:

```powershell
Copy-Item .env.example .env
```

`.env` ფაილში შეცვალეთ SQL Server-ის პაროლები და `JWT_SIGNING_KEY`. Gmail-ით წერილების გასაგზავნად შეავსეთ `EMAIL_USER`, `EMAIL_PASS` და `EMAIL_FROM`. გამოიყენეთ Gmail-ის App Password და არა თქვენი Google ანგარიშის ჩვეულებრივი პაროლი. `APP_URL` ლოკალური გაშვებისთვის დატოვეთ `http://localhost:4200`.

კურსების, გაკვეთილების, დავალებებისა და ფაილების ატვირთვისთვის სურვილისამებრ შეავსეთ `CLOUDINARY_CLOUD_NAME`, `CLOUDINARY_API_KEY` და `CLOUDINARY_API_SECRET`. საიდუმლო მნიშვნელობები არ დაამატოთ Git-ში; `.env` უკვე იგნორირებულია.

გაუშვით API, SQL Server და მონაცემთა ბაზის მიგრაციები:

```powershell
docker compose up --build --detach
docker compose ps
```

გაუშვით Angular სხვა PowerShell ფანჯარაში:

```powershell
cd frontend
npm install
npm start
```

შემდეგ გახსენით [http://localhost:4200](http://localhost:4200).

### ადგილობრივი მისამართები

| მისამართი | დანიშნულება |
|---|---|
| `http://localhost:4200` | Angular ვებ-აპლიკაცია |
| `http://localhost:5220/swagger` | Swagger UI |
| `http://localhost:5220/health/live` | API პროცესის შემოწმება |
| `http://localhost:5220/health/ready` | API-სა და მონაცემთა ბაზის მზადყოფნის შემოწმება |
| `localhost,14330` | SQL Server-ის ლოკალური კავშირი |

ლოკალური მონაცემთა ბაზა ინახება Docker-ის სახელდებულ volume-ში. `docker compose down` კონტეინერებს აჩერებს და მონაცემებს ტოვებს; `docker compose down --volumes` მონაცემთა ბაზასაც წაშლის.

## დოკუმენტაცია

- [არქიტექტურა და დიზაინის გადაწყვეტილებები](docs/architecture.md)
- [მონაცემთა ბაზის სტრუქტურა](docs/database.md)
- [API endpoint-ების ჩამონათვალი](docs/endpoints.md)
- [პრეზენტაციის გეგმა და როლების დემო-სცენარი](docs/presentation.md)
- [პროექტის მზადყოფნისა და შემოწმების ანგარიში](docs/project-readiness.md)
- [უსაფრთხოება და deployment-ის მოთხოვნები](docs/security.md)
- [DigitalOcean Droplet-ზე განთავსება SQL Server Express-ით](deploy/production/README.md)

## უსაფრთხოება და deployment

არასოდეს ატვირთოთ პაროლები, API key-ები ან სხვა საიდუმლო მნიშვნელობები GitHub-ზე. ლოკალური კონფიგურაცია შეინახეთ `.env`-ში ან .NET User Secrets-ში. `.env.example` შეიცავს მხოლოდ მაგალითებს.

ლოკალური Compose გარემო განვითარებისათვისაა და production deployment-ად არ ითვლება. საჯარო გაშვებამდე საჭიროა production გარემოს კონფიგურაცია, HTTPS, საიდუმლო მნიშვნელობების უსაფრთხო შენახვა, მონაცემთა ბაზის backup/restore გეგმა და როლების სრული ხელით შემოწმება. SQL Server-ის ლოკალური კონტეინერი production-ისთვის განკუთვნილი არ არის.
