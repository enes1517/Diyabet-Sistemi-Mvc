using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using System;
using System.Data;
using Microsoft.Extensions.Configuration;
using System.Data.SqlClient;
using ornek.Models;
using System.Threading.Tasks;
using System.IO;

namespace ornek.Controllers
{
    public class DoctorController : Controller
    {
        private readonly baglanti _baglanti;
        private readonly IConfiguration _configuration;
        private readonly IEmailService _emailService;

        public DoctorController(IConfiguration configuration, IEmailService emailService)
        {
            _configuration = configuration;
            _baglanti = new baglanti(_configuration.GetConnectionString("SqlConnection"));
            _emailService = emailService;
        }

        private bool IsDoctor()
        {
            return HttpContext.Session.GetString("UserType") == "Doktor";
        }

        public IActionResult Index()
        {
            if (!IsDoctor())
                return RedirectToAction("Login", "Account");

            int doktorId = HttpContext.Session.GetInt32("DoktorID") ?? 0;
            int userId = HttpContext.Session.GetInt32("UserId") ?? 0;

            DataTable dtDoktor = _baglanti.GetUserInfo(userId);
            ViewBag.DoktorBilgi = dtDoktor;

            string hastaQuery = @"
                SELECT h.HastaID, k.Ad, k.Soyad, k.TC, k.Email, h.Boy, h.Kilo
                FROM Hasta h
                INNER JOIN Kullanici k ON h.KullaniciID = k.KullaniciID
                INNER JOIN HastaDr hd ON h.HastaID = hd.HastaID
                WHERE hd.DoktorID = @DoktorID
                ORDER BY k.Ad, k.Soyad
            ";

            DataTable dtHastalar = _baglanti.ExecuteQuery(hastaQuery,
                new SqlParameter[] { new SqlParameter("@DoktorID", doktorId) });

            string uyariQuery = @"
                SELECT u.UyariID, u.UyariTuru, u.UyariMesaji, u.UyariTarihi, 
                       k.Ad + ' ' + k.Soyad AS HastaAdSoyad
                FROM Uyarilar u
                INNER JOIN Hasta h ON u.HastaID = h.HastaID
                INNER JOIN Kullanici k ON h.KullaniciID = k.KullaniciID
                WHERE u.DoktorID = @DoktorID AND u.Okundu = 0
                ORDER BY u.UyariTarihi DESC
            ";

            DataTable dtUyarilar = _baglanti.ExecuteQuery(uyariQuery,
                new SqlParameter[] { new SqlParameter("@DoktorID", doktorId) });

            ViewBag.Hastalar = dtHastalar;
            ViewBag.Uyarilar = dtUyarilar;

            return View();
        }

        public IActionResult HastaDetay(int id)
        {
            if (!IsDoctor())
                return RedirectToAction("Login", "Account");

            int doktorId = HttpContext.Session.GetInt32("DoktorID") ?? 0;

            string kontrolQuery = @"
                SELECT COUNT(*) FROM HastaDr 
                WHERE DoktorID = @DoktorID AND HastaID = @HastaID
            ";

            object kontrolSonuc = _baglanti.ExecuteScalar(kontrolQuery,
                new SqlParameter[] {
                    new SqlParameter("@DoktorID", doktorId),
                    new SqlParameter("@HastaID", id)
                });

            if (Convert.ToInt32(kontrolSonuc) == 0)
            {
                TempData["ErrorMessage"] = "Bu hastaya erişim yetkiniz bulunmamaktadır.";
                return RedirectToAction("Index");
            }

            string hastaQuery = @"
                SELECT h.HastaID, k.KullaniciID, k.Ad, k.Soyad, k.TC, k.Email, k.DogumTarihi, k.Cinsiyet,
                       h.Boy, h.Kilo, k.ProfilResim
                FROM Hasta h
                INNER JOIN Kullanici k ON h.KullaniciID = k.KullaniciID
                WHERE h.HastaID = @HastaID
            ";

            DataTable dtHasta = _baglanti.ExecuteQuery(hastaQuery,
                new SqlParameter[] { new SqlParameter("@HastaID", id) });

            if (dtHasta.Rows.Count == 0)
            {
                TempData["ErrorMessage"] = "Hasta bulunamadı.";
                return RedirectToAction("Index");
            }

            string kanSekeriQuery = @"
                SELECT KanSekeriID, OlcumDegeri, OlcumTarihi, OlcumSaati, OlcumTuru
                FROM KanSekeri
                WHERE HastaID = @HastaID
                ORDER BY OlcumTarihi DESC, OlcumSaati DESC
            ";

            DataTable dtKanSekeri = _baglanti.ExecuteQuery(kanSekeriQuery,
                new SqlParameter[] { new SqlParameter("@HastaID", id) });

            string belirtiQuery = @"
                SELECT hb.HastaBelirtiID, b.BelirtiAdi, hb.Tarih, hb.Siddet
                FROM HastaBelirtileri hb
                INNER JOIN Belirti b ON hb.BelirtiID = b.BelirtiID
                WHERE hb.HastaID = @HastaID
                ORDER BY hb.Tarih DESC
            ";

            DataTable dtBelirtiler = _baglanti.ExecuteQuery(belirtiQuery,
                new SqlParameter[] { new SqlParameter("@HastaID", id) });

            string insulinQuery = @"
                SELECT InsulinID, Doz, UygulamaTarihi, UygulamaSaati, OrtalamaKanSekeri
                FROM Insulin
                WHERE HastaID = @HastaID
                ORDER BY UygulamaTarihi DESC, UygulamaSaati DESC
            ";

            DataTable dtInsulin = _baglanti.ExecuteQuery(insulinQuery,
                new SqlParameter[] { new SqlParameter("@HastaID", id) });

            string diyetQuery = @"
                SELECT dt.DiyetID, d.TurAdi, dt.UygulandiMi, dt.Tarih
                FROM DiyetTakip dt
                INNER JOIN DiyetTuru d ON dt.DiyetTuruID = d.DiyetTuruID
                WHERE dt.HastaID = @HastaID
                ORDER BY dt.Tarih DESC
            ";

            DataTable dtDiyet = _baglanti.ExecuteQuery(diyetQuery,
                new SqlParameter[] { new SqlParameter("@HastaID", id) });

            string egzersizQuery = @"
                SELECT et.EgzersizID, e.TurAdi, et.YapildiMi, et.Tarih
                FROM EgzersizTakip et
                INNER JOIN EgzersizTuru e ON et.EgzersizTuruID = e.EgzersizTuruID
                WHERE et.HastaID = @HastaID
                ORDER BY et.Tarih DESC
            ";

            DataTable dtEgzersiz = _baglanti.ExecuteQuery(egzersizQuery,
                new SqlParameter[] { new SqlParameter("@HastaID", id) });

            ViewBag.Hasta = dtHasta.Rows[0];
            ViewBag.KanSekeri = dtKanSekeri;
            ViewBag.Belirtiler = dtBelirtiler;
            ViewBag.Insulin = dtInsulin;
            ViewBag.Diyet = dtDiyet;
            ViewBag.Egzersiz = dtEgzersiz;

            return View();
        }

        [HttpGet]
        public IActionResult HastaEkle()
        {
            if (!IsDoctor())
                return RedirectToAction("Login", "Account");

            return View();
        }

        [HttpPost]
        public async Task<IActionResult> HastaEkle(string ad, string soyad, string tc, string email, DateTime dogumTarihi, string cinsiyet, decimal boy, decimal kilo, IFormFile profilResim)
        {
            if (!IsDoctor())
                return RedirectToAction("Login", "Account");

            int doktorId = HttpContext.Session.GetInt32("DoktorID") ?? 0;

            try
            {
                // Validate T.C. number (11 digits)
                if (tc.Length != 11 || !long.TryParse(tc, out _))
                {
                    TempData["ErrorMessage"] = "T.C. Kimlik Numarası 11 haneli bir sayı olmalıdır.";
                    return View();
                }

                // Check if T.C. number already exists
                string checkTcQuery = "SELECT COUNT(*) FROM Kullanici WHERE TC = @TC";
                object tcExists = _baglanti.ExecuteScalar(checkTcQuery,
                    new SqlParameter[] { new SqlParameter("@TC", tc) });

                if (Convert.ToInt32(tcExists) > 0)
                {
                    TempData["ErrorMessage"] = "Bu T.C. Kimlik Numarası ile zaten bir kullanıcı kayıtlı.";
                    return View();
                }

                // Generate random password
                string generatedPassword = GenerateRandomPassword();
                string hashedPassword = _baglanti.HashPassword(generatedPassword);

                // Handle profile picture
                byte[] imageData = null;
                if (profilResim != null && profilResim.Length > 0)
                {
                    using (var memoryStream = new MemoryStream())
                    {
                        await profilResim.CopyToAsync(memoryStream);
                        imageData = memoryStream.ToArray();
                    }
                }

                // Insert into Kullanici table
                string insertKullaniciQuery = @"
                    INSERT INTO Kullanici (Ad, Soyad, TC, Email, Sifre, DogumTarihi, Cinsiyet, KullaniciTipi, ProfilResim)
                    OUTPUT INSERTED.KullaniciID
                    VALUES (@Ad, @Soyad, @TC, @Email, @Sifre, @DogumTarihi, @Cinsiyet, 'Hasta', @ProfilResim)
                ";

                SqlParameter[] kullaniciParams = {
                    new SqlParameter("@Ad", ad),
                    new SqlParameter("@Soyad", soyad),
                    new SqlParameter("@TC", tc),
                    new SqlParameter("@Email", email),
                    new SqlParameter("@Sifre", hashedPassword),
                    new SqlParameter("@DogumTarihi", dogumTarihi),
                    new SqlParameter("@Cinsiyet", cinsiyet),
                    new SqlParameter("@ProfilResim", (object)imageData ?? DBNull.Value)
                };

                object kullaniciId = _baglanti.ExecuteScalar(insertKullaniciQuery, kullaniciParams);

                if (kullaniciId == null)
                {
                    TempData["ErrorMessage"] = "Kullanıcı kaydı oluşturulamadı.";
                    return View();
                }

                // Insert into Hasta table
                string insertHastaQuery = @"
                    INSERT INTO Hasta (KullaniciID, Boy, Kilo)
                    OUTPUT INSERTED.HastaID
                    VALUES (@KullaniciID, @Boy, @Kilo)
                ";

                SqlParameter[] hastaParams = {
                    new SqlParameter("@KullaniciID", Convert.ToInt32(kullaniciId)),
                    new SqlParameter("@Boy", boy),
                    new SqlParameter("@Kilo", kilo)
                };

                object hastaId = _baglanti.ExecuteScalar(insertHastaQuery, hastaParams);

                if (hastaId == null)
                {
                    // Rollback Kullanici insertion if Hasta fails
                    string deleteKullaniciQuery = "DELETE FROM Kullanici WHERE KullaniciID = @KullaniciID";
                    _baglanti.ExecuteNonQuery(deleteKullaniciQuery,
                        new SqlParameter[] { new SqlParameter("@KullaniciID", Convert.ToInt32(kullaniciId)) });
                    TempData["ErrorMessage"] = "Hasta kaydı oluşturulamadı.";
                    return View();
                }

                // Link patient to doctor
                string insertHastaDrQuery = @"
                    INSERT INTO HastaDr (DoktorID, HastaID)
                    VALUES (@DoktorID, @HastaID)
                ";

                int sonuc = _baglanti.ExecuteNonQuery(insertHastaDrQuery,
                    new SqlParameter[] {
                        new SqlParameter("@DoktorID", doktorId),
                        new SqlParameter("@HastaID", Convert.ToInt32(hastaId))
                    });

                if (sonuc > 0)
                {
                    // Send email with login credentials
                    string subject = "Diyabet Takip Sistemi - Giriş Bilgileriniz";
                    string body = $@"
                        Sayın {ad} {soyad},
                        
                        Diyabet Takip Sistemine hoş geldiniz! Aşağıdaki bilgilerle sisteme giriş yapabilirsiniz:
                        
                        Kullanıcı Adı (T.C. Kimlik No): {tc}
                        Şifre: {generatedPassword}
                        
                        Giriş yaptıktan sonra şifrenizi değiştirmenizi öneririz.
                        
                        Saygılar,
                        Diyabet Takip Sistemi Ekibi
                    ";

                    await _emailService.SendEmailAsync(email, subject, body);

                    TempData["SuccessMessage"] = $"Hasta başarıyla sisteme tanıtıldı. Giriş bilgileri {email} adresine gönderildi.";
                }
                else
                {
                    // Rollback if linking fails
                    string deleteHastaQuery = "DELETE FROM Hasta WHERE HastaID = @HastaID";
                    string deleteKullaniciQuery = "DELETE FROM Kullanici WHERE KullaniciID = @KullaniciID";
                    _baglanti.ExecuteNonQuery(deleteHastaQuery,
                        new SqlParameter[] { new SqlParameter("@HastaID", Convert.ToInt32(hastaId)) });
                    _baglanti.ExecuteNonQuery(deleteKullaniciQuery,
                        new SqlParameter[] { new SqlParameter("@KullaniciID", Convert.ToInt32(kullaniciId)) });
                    TempData["ErrorMessage"] = "Hasta doktora bağlanamadı.";
                }
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Hata: {ex.Message}";
            }

            return RedirectToAction("HastaEkle");
        }

        [HttpGet]
        public IActionResult DiyetOlustur(int hastaId)
        {
            if (!IsDoctor())
                return RedirectToAction("Login", "Account");

            string hastaQuery = @"
                SELECT h.HastaID, k.Ad, k.Soyad
                FROM Hasta h
                INNER JOIN Kullanici k ON h.KullaniciID = k.KullaniciID
                WHERE h.HastaID = @HastaID
            ";

            DataTable dtHasta = _baglanti.ExecuteQuery(hastaQuery,
                new SqlParameter[] { new SqlParameter("@HastaID", hastaId) });

            if (dtHasta.Rows.Count == 0)
            {
                TempData["ErrorMessage"] = "Hasta bulunamadı.";
                return RedirectToAction("Index");
            }

            string diyetQuery = "SELECT DiyetTuruID, TurAdi, Aciklama FROM DiyetTuru";
            DataTable dtDiyetTurleri = _baglanti.ExecuteQuery(diyetQuery);

            ViewBag.Hasta = dtHasta.Rows[0];
            ViewBag.DiyetTurleri = dtDiyetTurleri;
            ViewBag.HastaID = hastaId;

            return View();
        }

        [HttpPost]
        public IActionResult DiyetOlustur(int hastaId, int diyetTuruId, DateTime baslangicTarihi, int sureSayisi)
        {
            if (!IsDoctor())
                return RedirectToAction("Login", "Account");

            try
            {
                for (int i = 0; i < sureSayisi; i++)
                {
                    DateTime tarih = baslangicTarihi.AddDays(i);

                    string insertQuery = @"
                        INSERT INTO DiyetTakip (HastaID, DiyetTuruID, UygulandiMi, Tarih)
                        VALUES (@HastaID, @DiyetTuruID, 0, @Tarih)
                    ";

                    SqlParameter[] parameters = {
                        new SqlParameter("@HastaID", hastaId),
                        new SqlParameter("@DiyetTuruID", diyetTuruId),
                        new SqlParameter("@Tarih", tarih)
                    };

                    _baglanti.ExecuteNonQuery(insertQuery, parameters);
                }

                TempData["SuccessMessage"] = "Diyet planı başarıyla oluşturulmuştur.";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Hata: {ex.Message}";
            }

            return RedirectToAction("HastaDetay", new { id = hastaId });
        }

        [HttpGet]
        public IActionResult EgzersizOlustur(int hastaId)
        {
            if (!IsDoctor())
                return RedirectToAction("Login", "Account");

            string hastaQuery = @"
                SELECT h.HastaID, k.Ad, k.Soyad
                FROM Hasta h
                INNER JOIN Kullanici k ON h.KullaniciID = k.KullaniciID
                WHERE h.HastaID = @HastaID
            ";

            DataTable dtHasta = _baglanti.ExecuteQuery(hastaQuery,
                new SqlParameter[] { new SqlParameter("@HastaID", hastaId) });

            if (dtHasta.Rows.Count == 0)
            {
                TempData["ErrorMessage"] = "Hasta bulunamadı.";
                return RedirectToAction("Index");
            }

            string egzersizQuery = "SELECT EgzersizTuruID, TurAdi, Aciklama FROM EgzersizTuru";
            DataTable dtEgzersizTurleri = _baglanti.ExecuteQuery(egzersizQuery);

            ViewBag.Hasta = dtHasta.Rows[0];
            ViewBag.EgzersizTurleri = dtEgzersizTurleri;
            ViewBag.HastaID = hastaId;

            return View();
        }

        [HttpPost]
        public IActionResult EgzersizOlustur(int hastaId, int egzersizTuruId, DateTime baslangicTarihi, int sureSayisi)
        {
            if (!IsDoctor())
                return RedirectToAction("Login", "Account");

            try
            {
                for (int i = 0; i < sureSayisi; i++)
                {
                    DateTime tarih = baslangicTarihi.AddDays(i);

                    string insertQuery = @"
                        INSERT INTO EgzersizTakip (HastaID, EgzersizTuruID, YapildiMi, Tarih)
                        VALUES (@HastaID, @EgzersizTuruID, 0, @Tarih)
                    ";

                    SqlParameter[] parameters = {
                        new SqlParameter("@HastaID", hastaId),
                        new SqlParameter("@EgzersizTuruID", egzersizTuruId),
                        new SqlParameter("@Tarih", tarih)
                    };

                    _baglanti.ExecuteNonQuery(insertQuery, parameters);
                }

                TempData["SuccessMessage"] = "Egzersiz planı başarıyla oluşturulmuştur.";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Hata: {ex.Message}";
            }

            return RedirectToAction("HastaDetay", new { id = hastaId });
        }

        [HttpPost]
        public IActionResult UyariOlustur(int hastaId, string uyariTuru, string uyariMesaji)
        {
            if (!IsDoctor())
                return RedirectToAction("Login", "Account");

            int doktorId = HttpContext.Session.GetInt32("DoktorID") ?? 0;

            try
            {
                string insertQuery = @"
                    INSERT INTO Uyarilar (HastaID, DoktorID, UyariTuru, UyariMesaji, UyariTarihi, Okundu)
                    VALUES (@HastaID, @DoktorID, @UyariTuru, @UyariMesaji, GETDATE(), 0)
                ";

                SqlParameter[] parameters = {
                    new SqlParameter("@HastaID", hastaId),
                    new SqlParameter("@DoktorID", doktorId),
                    new SqlParameter("@UyariTuru", uyariTuru),
                    new SqlParameter("@UyariMesaji", uyariMesaji)
                };

                int sonuc = _baglanti.ExecuteNonQuery(insertQuery, parameters);

                if (sonuc > 0)
                {
                    TempData["SuccessMessage"] = "Uyarı başarıyla oluşturulmuştur.";
                }
                else
                {
                    TempData["ErrorMessage"] = "Uyarı oluşturulurken bir hata oluştu.";
                }
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Hata: {ex.Message}";
            }

            return RedirectToAction("HastaDetay", new { id = hastaId });
        }

        public IActionResult Profil()
        {
            if (!IsDoctor())
                return RedirectToAction("Login", "Account");

            int userId = HttpContext.Session.GetInt32("UserId") ?? 0;
            DataTable dtKullanici = _baglanti.GetUserInfo(userId);

            if (dtKullanici.Rows.Count == 0)
            {
                TempData["ErrorMessage"] = "Kullanıcı bulunamadı.";
                return RedirectToAction("Index");
            }

            int doktorId = HttpContext.Session.GetInt32("DoktorID") ?? 0;
            string doktorQuery = "SELECT Uzmanlik FROM Doktor WHERE DoktorID = @DoktorID";
            DataTable dtDoktor = _baglanti.ExecuteQuery(doktorQuery,
                new SqlParameter[] { new SqlParameter("@DoktorID", doktorId) });

            ViewBag.Kullanici = dtKullanici.Rows[0];
            ViewBag.Doktor = dtDoktor.Rows.Count > 0 ? dtDoktor.Rows[0] : null;

            return View();
        }

        [HttpPost]
        public IActionResult ProfilGuncelle(string ad, string soyad, string email, string uzmanlik, IFormFile profilResim)
        {
            if (!IsDoctor())
                return RedirectToAction("Login", "Account");

            int userId = HttpContext.Session.GetInt32("UserId") ?? 0;
            int doktorId = HttpContext.Session.GetInt32("DoktorID") ?? 0;

            try
            {
                string updateKullaniciQuery = @"
                    UPDATE Kullanici SET
                    Ad = @Ad,
                    Soyad = @Soyad,
                    Email = @Email
                    {0}
                    WHERE KullaniciID = @KullaniciID
                ";

                List<SqlParameter> kullaniciParams = new List<SqlParameter>
                {
                    new SqlParameter("@Ad", ad),
                    new SqlParameter("@Soyad", soyad),
                    new SqlParameter("@Email", email),
                    new SqlParameter("@KullaniciID", userId)
                };

                string profilResimEklemesi = "";
                if (profilResim != null && profilResim.Length > 0)
                {
                    byte[] imageData;
                    using (var memoryStream = new System.IO.MemoryStream())
                    {
                        profilResim.CopyTo(memoryStream);
                        imageData = memoryStream.ToArray();
                    }

                    profilResimEklemesi = ", ProfilResim = @ProfilResim";
                    kullaniciParams.Add(new SqlParameter("@ProfilResim", imageData));
                }

                updateKullaniciQuery = string.Format(updateKullaniciQuery, profilResimEklemesi);
                _baglanti.ExecuteNonQuery(updateKullaniciQuery, kullaniciParams.ToArray());

                string updateDoktorQuery = @"
                    UPDATE Doktor SET
                    Uzmanlik = @Uzmanlik
                    WHERE DoktorID = @DoktorID
                ";

                SqlParameter[] doktorParams = {
                    new SqlParameter("@Uzmanlik", string.IsNullOrEmpty(uzmanlik) ? DBNull.Value : (object)uzmanlik),
                    new SqlParameter("@DoktorID", doktorId)
                };

                _baglanti.ExecuteNonQuery(updateDoktorQuery, doktorParams);

                TempData["SuccessMessage"] = "Profiliniz başarıyla güncellenmiştir.";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Hata: {ex.Message}";
            }

            return RedirectToAction("Profil");
        }

        [HttpPost]
        public IActionResult SifreDegistir(string eskiSifre, string yeniSifre, string yeniSifreTekrar)
        {
            if (!IsDoctor())
                return RedirectToAction("Login", "Account");

            int userId = HttpContext.Session.GetInt32("UserId") ?? 0;

            try
            {
                if (yeniSifre != yeniSifreTekrar)
                {
                    TempData["ErrorMessage"] = "Yeni şifreler eşleşmiyor.";
                    return RedirectToAction("Profil");
                }

                string hashedEskiSifre = _baglanti.HashPassword(eskiSifre);
                string checkQuery = @"
                    SELECT COUNT(*) FROM Kullanici
                    WHERE KullaniciID = @KullaniciID AND Sifre = @Sifre
                ";

                object checkResult = _baglanti.ExecuteScalar(checkQuery,
                    new SqlParameter[] {
                        new SqlParameter("@KullaniciID", userId),
                        new SqlParameter("@Sifre", hashedEskiSifre)
                    });

                if (Convert.ToInt32(checkResult) == 0)
                {
                    TempData["ErrorMessage"] = "Mevcut şifreniz yanlış.";
                    return RedirectToAction("Profil");
                }

                string hashedYeniSifre = _baglanti.HashPassword(yeniSifre);

                string updateQuery = @"
                    UPDATE Kullanici SET
                    Sifre = @YeniSifre
                    WHERE KullaniciID = @KullaniciID
                ";

                int sonuc = _baglanti.ExecuteNonQuery(updateQuery,
                    new SqlParameter[] {
                        new SqlParameter("@YeniSifre", hashedYeniSifre),
                        new SqlParameter("@KullaniciID", userId)
                    });

                if (sonuc > 0)
                {
                    TempData["SuccessMessage"] = "Şifreniz başarıyla değiştirilmiştir.";
                }
                else
                {
                    TempData["ErrorMessage"] = "Şifre değiştirilirken bir hata oluştu.";
                }
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Hata: {ex.Message}";
            }

            return RedirectToAction("Profil");
        }

        private string GenerateRandomPassword()
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
            var random = new Random();
            return new string(Enumerable.Repeat(chars, 8)
                .Select(s => s[random.Next(s.Length)]).ToArray());
        }
    }
}