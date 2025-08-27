Bu proje, hasta ve doktor verilerini, günlük ölçümleri ve diyabet bakımı için bildirimleri yönetmek amacıyla ASP.NET MVC ile geliştirilmiş bir Diyabet Sistemi’dir.
Özellikler
Hasta Yönetimi: PatientController aracılığıyla hastalar ve ölçümlerin takibi.
Doktor Yönetimi: DoctorController ile doktor kontrol panelleri veya hasta gözetimi.
Hesap Yönetimi: AccountController ile giriş ve kayıt işlemleri.
Günlük Kontroller: Ölçüm kontrolü için DailyMeasurementCheckService.cs.
E-posta Bildirimleri: EmailService.cs ve arayüzü ile bildirim gönderme.
Veritabanı Bağlantısı: baglanti.cs ile bağlantılar, models.cs ile varlıklar.
Ana Arayüz: HomeController ile ana sayfa.
Özellikler, kan şekeri seviyesi izleme, hatırlatmalar ve hasta/doktor rolleri içerir.

Kullanılan Teknolojiler
C# ASP.NET MVC
E-posta hizmetleri (SMTP)
Veritabanı (SQL, baglanti.cs aracılığıyla)

Nasıl Çalıştırılır
Depoyu klonlayın: git clone https://github.com/enes1517/Diyabet-Sistemi-Mvc.git
Visual Studio’da çözüm dosyasını (ornek.sln) açın.
Veritabanı ve e-posta için appsettings.json’u yapılandırın.
Projeyi derleyin ve çalıştırın.
Not: Bildirimler için e-posta kimlik bilgilerini ayarlayın.
