# BTBS420 İşe Alım ve Aday Takip Sistemi

ASP.NET Core MVC tabanlı işe alım ve aday takip sistemi. Fonksiyonel gereksinimler için
[`docs/PROJECT_REQUIREMENTS.md`](docs/PROJECT_REQUIREMENTS.md), mimari/rol/yetki matrisi ve
bildirim kataloğu için [`docs/ARCHITECTURE_AND_ROLES.md`](docs/ARCHITECTURE_AND_ROLES.md),
manuel kabul testi senaryoları için [`docs/UAT_GUIDE.md`](docs/UAT_GUIDE.md) dosyalarına bakın.

## Teknoloji Yığını

- C# / ASP.NET Core MVC (.NET 10)
- Entity Framework Core (SQL Server sağlayıcısı)
- ASP.NET Core Identity
- Bootstrap

## Gereksinimler

- [.NET SDK 10.0.302](https://dotnet.microsoft.com/) (bkz. `global.json`)
- Docker (yerel SQL Server için) veya erişilebilir bir SQL Server instance'ı
- `dotnet-ef` global tool: `dotnet tool install --global dotnet-ef`

## Temiz Ortamda Kurulum

1. **Repoyu klonla ve bağımlılıkları geri yükle**

   ```
   git clone <repo-url>
   cd BTBS420-IK-Projesi
   dotnet restore BTBS420.RecruitmentSystem.sln
   ```

2. **SQL Server sağla**

   Yerelde Docker ile geçici bir SQL Server container'ı çalıştırılabilir:

   ```
   docker run -d --name btbs420-dev-sql -p 1433:1433 \
     -e ACCEPT_EULA=Y -e MSSQL_SA_PASSWORD="<güçlü-bir-şifre>" \
     mcr.microsoft.com/mssql/server:2022-latest
   ```

   Gerçek bir kurumsal SQL Server instance'ı da kullanılabilir.

3. **Connection string / secrets tanımla**

   Uygulama, connection string'i `ConnectionStrings:DefaultConnection`'dan okur ve boşsa
   başlangıçta hata fırlatır. **Gerçek connection string'i asla `appsettings.json`'a veya
   commit'e yazmayın.** Geliştirme ortamında ya .NET user-secrets ya da ortam değişkeni kullanın:

   ```
   dotnet user-secrets init --project src/BTBS420.RecruitmentSystem.Web
   dotnet user-secrets set "ConnectionStrings:DefaultConnection" \
     "Server=localhost,1433;Database=BTBS420_Dev;User Id=sa;Password=<şifre>;TrustServerCertificate=True;" \
     --project src/BTBS420.RecruitmentSystem.Web
   ```

   veya

   ```
   export ConnectionStrings__DefaultConnection="Server=localhost,1433;Database=BTBS420_Dev;User Id=sa;Password=<şifre>;TrustServerCertificate=True;"
   ```

4. **Migration'ları uygula**

   ```
   dotnet ef database update \
     --project src/BTBS420.RecruitmentSystem.Web \
     --startup-project src/BTBS420.RecruitmentSystem.Web
   ```

5. **Uygulamayı çalıştır**

   ```
   dotnet run --project src/BTBS420.RecruitmentSystem.Web
   ```

## Testleri Çalıştırma

### Birim / uygulama-içi (in-memory) testler

Bağımlılık gerektirmez, her zaman çalışır:

```
dotnet test tests/BTBS420.RecruitmentSystem.Web.Tests
```

### SQL Server entegrasyon testleri

`*SqlServerIntegrationTests.cs` dosyalarındaki testler gerçek bir SQL Server'a ihtiyaç duyar ve
ilgili ortam değişkeni tanımlı değilse otomatik olarak atlanır (skip). Ayrı bir `BTBS420_Test`
veritabanı kullanın — **geliştirme veritabanınıza (`BTBS420_Dev`) asla karıştırmayın**.

1. Test veritabanına migration'ları uygulayın (adım 4'teki komutu `BTBS420_Test`'i işaret eden bir
   `ConnectionStrings__DefaultConnection` ile çalıştırın).
2. Aşağıdaki ortam değişkenlerini aynı test connection string'ine ayarlayıp testleri çalıştırın
   (her dosya kendi `KANxx_TEST_SQLSERVER_CONNECTION_STRING` adını kullanır, hepsi aynı test
   veritabanını işaret edebilir):

   `KAN23`, `KAN30`, `KAN31`, `KAN32`, `KAN33`, `KAN34`, `KAN35`, `KAN36`, `KAN37`, `KAN38`,
   `KAN39`, `KAN40`, `KAN45`, `KAN46`, `KAN61`, `KAN65`, `KAN66`, `KAN67`, `KAN68`, `KAN94`
   (`_TEST_SQLSERVER_CONNECTION_STRING` son eki ile).

   ```
   export KAN46_TEST_SQLSERVER_CONNECTION_STRING="Server=localhost,1433;Database=BTBS420_Test;User Id=sa;Password=<şifre>;TrustServerCertificate=True;"
   # ... diğer KANxx değişkenleri için tekrarlayın
   dotnet test tests/BTBS420.RecruitmentSystem.Web.Tests
   ```

### CI

`.github/workflows/ci.yml`, `develop` branch'ine her push/PR'da geçici bir `mssql` service
container'ı ayağa kaldırır, migration'ları uygular, yukarıdaki tüm `KANxx` ortam değişkenlerini
set eder ve tam test suite'ini (unit + SQL entegrasyon) çalıştırır.

## Kod Tabanı Yapısı

```
src/BTBS420.RecruitmentSystem.Web/   Uygulama (Controllers, Models, ViewModels, Views, Data)
tests/BTBS420.RecruitmentSystem.Web.Tests/  Unit + entegrasyon testleri
docs/                                 Gereksinim, mimari/rol ve UAT dokümanları
.github/workflows/ci.yml              CI pipeline
```

## Geliştirme Süreci ve Katkı Kuralları

Jira iş akışı, git/commit kuralları ve manuel onay gerektiren işlemler için
[`AGENTS.md`](AGENTS.md) dosyasına bakın — bu README onu tekrar etmez, sadece ona yönlendirir.
