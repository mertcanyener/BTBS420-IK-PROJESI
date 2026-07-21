# BTBS420 İşe Alım ve Aday Takip Sistemi

## Teknoloji Yığını

- C#
- ASP.NET Core MVC
- Entity Framework Core
- SQL Server
- ASP.NET Core Identity
- Bootstrap

## Kullanıcı Rolleri

- Admin
- İşe Alım Uzmanı
- İşe Alım Yöneticisi
- Aday

## Ana Modüller

### 1. Kimlik Doğrulama

- Kullanıcı adı veya e-posta ile giriş
- Aday hesabı oluşturma
- Şifre sıfırlama
- Rol bazlı erişim kontrolü

### 2. Kullanıcı ve Rol Yönetimi

- Kullanıcı ekleme ve düzenleme
- Kullanıcıyı devre dışı bırakma
- Rol atama
- Kullanıcı işlem geçmişini görüntüleme

### 3. Departman, Pozisyon ve Yetkinlik Yönetimi

- Departman CRUD işlemleri
- Pozisyon ve kıdem seviyesi yönetimi
- Yetkinlik, deneyim, eğitim, dil ve konum filtreleri

### 4. İş İlanı Yönetimi

İlan bilgileri:

- Pozisyon
- Departman
- Konum
- Çalışma şekli
- Açıklama
- Gereksinimler
- Son başvuru tarihi

İlan durumları:

- Taslak
- Yayında
- Başvurular Kapalı
- Pozisyon Dolduruldu

İlan görünürlüğü:

- Şirket İçi
- Herkese Açık
- Pasif

### 5. Aday Profili

- Kişisel ve mesleki bilgiler
- Eğitim ve deneyim bilgileri
- Özgeçmiş yükleme
- Ön yazı ve sertifika yükleme
- Açık pozisyonları arama ve filtreleme
- İş ilanına başvurma

### 6. Başvuru Yönetimi

Başvuru durumları:

- Yeni
- Ön Eleme
- Mülakat
- Teklif
- İşe Alındı
- Reddedildi
- Geri Çekildi

Özellikler:

- Başvuru detaylarını görüntüleme
- Başvuru durumunu değiştirme
- Durum geçmişini kaydetme
- Başvuruyu arşivleme
- Yeniden değerlendirmeye alma
- Adayın uygun aşamada başvurusunu geri çekmesi

### 7. Mülakat Yönetimi

- Çevrim içi veya yüz yüze mülakat oluşturma
- Tarih, saat, konum ve toplantı bağlantısı belirleme
- Katılımcı ve değerlendirme paneli atama
- Takvim çakışmalarını kontrol etme

Mülakat durumları:

- Planlandı
- Tamamlandı
- Ertelendi
- İptal Edildi

### 8. Değerlendirme ve Teklif

- Değerlendirme notu
- Yetkinlik puanı
- Genel puan
- Öneri
- Kullanıcının kendi değerlendirmesini düzenlemesi
- İş teklifi oluşturma
- Teklifi onaya gönderme
- Aday kararını takip etme

### 9. Dashboard

Admin:

- Toplam kullanıcı
- Aktif ilan
- Toplam başvuru
- Devam eden süreç
- İşe alınan aday sayısı

İşe Alım Uzmanı:

- Sorumlu ilanlar
- Aday havuzu
- Yaklaşan mülakatlar
- Bekleyen teklifler

İşe Alım Yöneticisi:

- Kısa liste adayları
- Bekleyen değerlendirmeler
- Açık ekip pozisyonları
- İşe alım ilerlemesi

### 10. Aktivite Kayıtları

- Giriş işlemleri
- İlan oluşturma, düzenleme ve silme
- Başvuru durum değişiklikleri
- Mülakat işlemleri
- Değerlendirme ve teklif işlemleri
- Tarih, kullanıcı, ilan, aday ve işlem türüne göre filtreleme
- Admin tarafından dışa aktarma

## Temel İş Kuralları

- Kullanıcı yalnızca rolünün izin verdiği işlemleri yapabilir.
- Aday aynı ilana iki kez başvuramaz.
- Süresi dolmuş ilana başvuru yapılamaz.
- Pasif veya başvuruları kapalı ilana başvuru yapılamaz.
- Başvuru durum değişiklikleri geçmişe kaydedilir.
- Kullanıcı yalnızca kendi değerlendirmesini düzenleyebilir.
- Aynı katılımcı için çakışan mülakat oluşturulamaz.
- Devre dışı kullanıcı sisteme giriş yapamaz.
- Önemli işlemler aktivite kayıtlarına yazılır.