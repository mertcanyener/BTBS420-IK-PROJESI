# Kullanıcı Kabul Testi (UAT) Kılavuzu

Bu kılavuz, temel işe alım yaşam döngüsünü rol bazında manuel olarak doğrulamak için kullanılır.
Senaryolar, `EndToEndHiringSqlServerIntegrationTests.cs`'teki otomatik uçtan uca testlerle aynı
akışı takip eder; otomatik testler geçiyor olsa bile gerçek tarayıcı/kullanıcı deneyimini
doğrulamak için bu kılavuz kullanılmalıdır. Rol/durum tanımları için
[`ARCHITECTURE_AND_ROLES.md`](ARCHITECTURE_AND_ROLES.md)'a bakın.

## Ön Koşul

Uygulama çalışır durumda, en az bir Admin kullanıcısı ve boş bir test veritabanı hazır olmalı.

## Senaryo 1 — Kabul Akışı (İşe Alındı ile biter)

| # | Rol | Adım | Beklenen Sonuç |
|---|---|---|---|
| 1 | Admin | Departman, pozisyon ve iş ilanı oluştur, ilanı Yayında durumuna al. | İlan, herkese açık ilan listesinde görünür. |
| 2 | Aday | Kayıt ol, profilini doldur, ilana başvur. | Başvuru "Yeni" durumunda oluşur. |
| 3 | İşe Alım Uzmanı | Başvuru havuzunda başvuruyu bul, mülakat planla (çevrim içi/yüz yüze), katılımcı ata. | Mülakat "Planlandı" durumunda oluşur; aday ve katılımcı bildirim alır. |
| 4 | İşe Alım Uzmanı | Mülakat tarihi geldiğinde mülakatı "Tamamlandı" yap. | Mülakat durumu Tamamlandı olur, audit kaydı oluşur. |
| 5 | Atanan Katılımcı | Değerlendirme formunu doldur (yetkinlik/genel puan, öneri, not). | Değerlendirme kaydedilir, başvuru detayında görünür. |
| 6 | İşe Alım Uzmanı | Teklif taslağı oluştur (maaş, başlangıç tarihi), onaya gönder. | Teklif "Yönetici Onayı Bekliyor" durumuna geçer. |
| 7 | İşe Alım Yöneticisi | Bekleyen teklifi onayla. | Teklif "Onaylandı" olur; aday bildirim alır. |
| 8 | Aday | Bildirimdeki teklifi görüntüle, **Kabul Et**. | Teklif "Kabul Edildi", başvuru **"İşe Alındı"** olur; uzman ve yönetici bildirim alır. |
| 9 | Admin | Admin Dashboard'da ilgili departmanı filtrele. | "İşe Alınan Aday Sayısı" metriği 1 artmış, "Devam Eden Süreç" metriğinde bu başvuru artık sayılmıyor. |
| 10 | Admin | Aktivite Kayıtları'nda başvuruyu filtrele. | Başvurunun "İşe Alındı" durum değişikliği audit kaydında görünür. |

## Senaryo 2 — Teklif Ret Akışı (Reddedildi ile biter)

1–7. adımlar Senaryo 1 ile aynıdır (farklı bir aday/ilan ile).

| # | Rol | Adım | Beklenen Sonuç |
|---|---|---|---|
| 8 | Aday | Onaylanmış teklifi **Reddet**. | Teklif **"Aday Tarafından Reddedildi"**, başvuru **"Reddedildi"** olur; uzman ve yönetici bildirim alır. |
| 9 | Admin | Admin Dashboard'da aynı departmanı filtrele. | "İşe Alınan Aday Sayısı" 0, "Devam Eden Süreç" metriğinde bu başvuru artık sayılmıyor (terminal durum). |
| 10 | Admin | Aktivite Kayıtları'nda başvuruyu filtrele. | Başvurunun "Reddedildi" durum değişikliği audit kaydında görünür. |

## Rol Bazlı Ek Kontroller

- **Admin:** Başka bir departmanın verisini görebildiğini, kullanıcı devre dışı bırakma ve rol
  atamanın çalıştığını doğrula.
- **İşe Alım Uzmanı:** Sorumlu olmadığı bir ilanın başvurusuna (başka uzmana ait) erişmeye
  çalıştığında "bulunamadı" hatası aldığını doğrula (yetkisiz erişim engeli).
- **İşe Alım Yöneticisi:** Kendi departmanı dışındaki bir teklife karar veremediğini doğrula.
- **Aday:** Aynı ilana ikinci kez başvuramadığını, başka bir adayın başvurusunu/belgesini
  göremediğini doğrula.
- **Dosya güvenliği:** Aday, imzası dosya uzantısıyla uyuşmayan veya boyut limitini aşan bir
  belge yüklemeyi denediğinde reddedildiğini doğrula.

## Sonuç Raporlama

Her senaryo için PASS/FAIL, tarih, test eden kişi ve varsa ekran görüntüsü/Jira bug linki
kaydedilmelidir.
