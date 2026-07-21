# Codex Çalışma Kuralları

## Jira

- Jira sitesi: https://mertcode00-35.atlassian.net
- Jira projesi: KAN
- Yalnızca KAN projesindeki tasklar üzerinde çalış.
- Aynı anda yalnızca bir task üzerinde çalış.
- Taska başlamadan önce açıklamasını ve kabul kriterlerini oku.
- Kod değişikliğine başlamadan önce taskı In Progress durumuna geçir.
- Kabul kriterleri karşılanmadan taskı Done yapma.

## Geliştirme Süreci

Her task için:

1. Taskı oku.
2. Mevcut kodu incele.
3. Kısa plan oluştur.
4. Taskı In Progress yap.
5. Yalnızca task kapsamındaki değişiklikleri yap.
6. Projeyi build et.
7. İlgili testleri çalıştır.
8. Git diff ile değişiklikleri kontrol et.
9. Taska özel commit oluştur.
10. Commiti GitHub'a push et.
11. Jira taskına tamamlanma yorumu ekle.
12. Kabul kriterleri karşılanıyorsa taskı Done yap.

## Git Kuralları

- Her Jira taskı için ayrı commit oluştur.
- Commit başlığında Jira task anahtarını kullan.
- Commit mesajlarını Türkçe yaz.
- Commit açıklamasında neyin neden değiştirildiğini belirt.
- İlgisiz dosyaları commite ekleme.
- Şifre, token ve connection string gibi gizli bilgileri commite ekleme.

Örnek:

KAN-5: Departman yönetimi eklendi

- Departman listeleme ve ekleme ekranları oluşturuldu.
- Form doğrulama kuralları eklendi.
- Silme yerine pasife alma işlemi kullanıldı.

Bu değişiklik, sistem yöneticisinin departmanları güvenli şekilde
yönetebilmesi için yapıldı.

## Hata Kuralları

- Build başarısızsa taskı Done yapma.
- Test başarısızsa taskı Done yapma.
- Task belirsizse kapsam dışı özellik üretme.
- Çözülemeyen engeli Jira taskına yorum olarak yaz.