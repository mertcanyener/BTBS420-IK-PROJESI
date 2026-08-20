# Mimari, Rol/Yetki Matrisi ve Bildirim Kataloğu

Bu doküman koddan (Controllers, Authorization, Notifications, ActivityLogging) derlenmiştir;
uygulamayla farklılık tespit edilirse bu dosya güncel kod ile yeniden karşılaştırılmalıdır.
Fonksiyonel modül/durum listeleri için [`PROJECT_REQUIREMENTS.md`](PROJECT_REQUIREMENTS.md)'a bakın.

## Mimari Özeti

- **ASP.NET Core MVC** uygulaması, `src/BTBS420.RecruitmentSystem.Web` altında.
- **Entity Framework Core** + SQL Server, `ApplicationDbContext` (`Data/`) üzerinden.
- **ASP.NET Core Identity**, rol bazlı kimlik doğrulama için.
- Katmanlar:
  - `Controllers/` — HTTP uç noktaları, iş kuralı orkestrasyonu.
  - `Models/` — domain varlıkları ve durum makineleri (`*Statuses.cs` dosyalarında
    `IsValidTransition` ile tanımlı geçiş kuralları).
  - `ViewModels/`, `Views/` — sunum katmanı.
  - `Authorization/` — rol politikaları (`AuthorizationPolicies`) ve departman/sorumluluk bazlı
    kapsam filtreleme (`RecruitmentScopeService`, `RecruitmentScope`).
  - `Notifications/` — uygulama içi bildirim yayınlama (`NotificationService`,
    `INotificationPublisher`), olay bazlı ve tekrarsız (`StageIfMissingAsync`, `EventKey` unique).
  - `ActivityLogging/` — audit log kayıtları (`IActivityLogService`, `ActivityActionCodes`).

## Roller

| Rol | Sabit (`SystemRoles`) | Yetki Politikası |
|---|---|---|
| Admin | `Admin` | `AuthorizationPolicies.AdminOnly` |
| İşe Alım Uzmanı | `RecruitmentSpecialist` | `AuthorizationPolicies.RecruitmentSpecialistOnly` |
| İşe Alım Yöneticisi | `HiringManager` | `AuthorizationPolicies.HiringManagerOnly` |
| Aday | `Candidate` | `AuthorizationPolicies.CandidateOnly` |

`AuthorizationPolicies.RecruitmentStaffOnly`, Admin + Uzman + Yönetici rollerinin birleşimidir
(örn. `OffersController` bu politikayla korunur, ekran içi eylemler ayrıca kapsamla daraltılır).

## Kapsam (Scope) Kuralı — `RecruitmentScopeService`

| Rol | Kapsam |
|---|---|
| Admin | Sınırsız (`RecruitmentScope.Unrestricted`) — tüm departman/ilan/başvurulara erişir. |
| İşe Alım Uzmanı | Yalnızca kendisinin **sorumlu kullanıcı** olarak atandığı ilanlar/başvurular (`ForResponsibleUser`). |
| İşe Alım Yöneticisi | Yalnızca kendi **departmanındaki** ilanlar/başvurular (`ForDepartment`). |
| Aday | Kapsam servisine girmez; her controller kendi profiline ait kayıtları
  (`CandidateProfileId`/`ApplicationUserId` eşleşmesi) doğrudan filtreler. |

Kapsam dışı bir kayda erişim denemesi genelde `NotFound` (404) döner — `Forbidden` değil, kaydın
varlığını sızdırmamak için (bkz. her modülün `*ErisemezNotFoundDoner` / `KapsamDisi*` testleri).

## Rol × Modül Erişim Matrisi

| Modül | Admin | Uzman | Yönetici | Aday |
|---|---|---|---|---|
| Departman/Pozisyon yönetimi | ✅ | ❌ | ❌ | ❌ |
| İlan oluşturma/düzenleme | ✅ | Sorumlu olduğu ilanlar | ❌ | ❌ |
| Başvuru havuzu (ApplicationsPool) | ✅ tümü | Sorumlu ilanlarının başvuruları | Departmanının başvuruları (karar/onay) | ❌ |
| Mülakat planlama/tamamlama/iptal | ✅ | Sorumlu ilanlarının başvuruları için | ❌ (görüntüleme) | Kendi mülakatını görüntüler |
| Değerlendirme girme/düzenleme | ✅ | Atanan panelist ise | Atanan panelist ise | ❌ |
| Teklif oluşturma/gönderme | ✅ | Sorumlu ilanlarının başvuruları için | ❌ | ❌ |
| Teklif onaylama/reddetme | ✅ | ❌ | Departmanının teklifleri | ❌ |
| Teklif kabul/ret (aday kararı) | ❌ | ❌ | ❌ | Kendi teklifi |
| Admin/Manager/Specialist Dashboard | ✅ (Admin) | ✅ (kendi) | ✅ (kendi) | ❌ |
| Aktivite Kayıtları (Activity Logs) | ✅ | ❌ | ❌ | ❌ |

## Bildirim Kataloğu

Bildirimler `NotificationService.StageIfMissingAsync` ile eklenir; aynı
`(RecipientUserId, EventKey)` çifti için tekrar bildirim oluşturulmaz (unique index korumalı).

| Olay | `EventKey` deseni | Alıcı(lar) | Tetikleyen |
|---|---|---|---|
| Mülakat planlandı | `interview-created:{interviewId}:{startTicks}` | Aday + atanan katılımcılar | `ApplicationsPoolController.CreateInterview` |
| Mülakat iptal edildi | `interview-cancelled:{interviewId}` | Aday + atanan katılımcılar | `InterviewsController.Cancel` |
| Mülakat ertelendi | `interview-postponed:{interviewId}:{newStartTicks}` | Aday + atanan katılımcılar | `InterviewsController.Postpone` |
| Teklif onaylandı | `offer-status-changed:{offerId}:approved` | Aday | `OffersController.Approve` |
| Aday teklif kararını bildirdi (kabul/ret) | `offer-status-changed:{offerId}:{accepted\|rejected_by_candidate}` | Teklifi oluşturan uzman + onaylayan yönetici | `JobApplicationsController.AcceptOffer` / `RejectOffer` |

## Audit (Activity Log) Aksiyon Kodları — `ActivityActionCodes`

`authentication.succeeded/failed/signed-out`, `user.registered`, `password-reset.requested/succeeded`,
`authorization.denied`, `entity.created`, `entity.updated`, `entity.status-changed`,
`entity.archived`, `entity.deleted`, `entity.downloaded`. Her kayıt `TargetEntityType` +
`TargetEntityId` ile ilişkili varlığı tutar (`ActivityEntityTypes`).

## Test / Commit / Jira Tamamlama Kontrol Listesi

Ayrıntılı süreç kuralları için **[`AGENTS.md`](../AGENTS.md)** tek doğru kaynaktır — burada
tekrarlanmaz. Özet kontrol listesi:

- [ ] Jira task'ı okundu, kabul kriterleri netleşti, "Devam Ediyor"a alındı.
- [ ] Değişiklik yalnızca task kapsamında.
- [ ] `dotnet build` başarılı.
- [ ] İlgili testler (`dotnet test`) geçiyor — build/test başarısızsa task Done yapılmaz.
- [ ] `git diff` ile değişiklikler gözden geçirildi, ilgisiz dosya/gizli bilgi yok.
- [ ] Task'a özel commit, Türkçe mesaj, Jira anahtarı başlıkta.
- [ ] Push edildi, Jira'ya tamamlanma yorumu eklendi.
- [ ] Kabul kriterleri karşılanıyorsa Jira "Tamam"a alındı.
