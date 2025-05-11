using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Proje3.Models;
using System;
using System.Data;
using Microsoft.Extensions.Configuration;
using System.Data.SqlClient;
using ornek.Models;
using System.IO;

namespace ornek.Controllers
{
    public class AccountController : Controller
    {
        private readonly baglanti _baglanti;
        private readonly IConfiguration _configuration;
        private readonly string _connectionString;

        public AccountController(IConfiguration configuration)
        {
            _configuration = configuration;
            // Bağlantı dizesi doğrudan appsettings.json'dan alınmalı
            _connectionString = _configuration.GetConnectionString("SqlConnection");
            _baglanti = new baglanti(_connectionString);
        }

        // Giriş sayfasını gösterme
        [HttpGet]
        public IActionResult Login()
        {
            // Kullanıcı zaten giriş yapmışsa, kullanıcı tipine göre yönlendir
            if (HttpContext.Session.GetString("UserType") != null)
            {
                if (HttpContext.Session.GetString("UserType") == "Doktor")
                    return RedirectToAction("Index", "Doctor");
                else if (HttpContext.Session.GetString("UserType") == "Hasta")
                    return RedirectToAction("Index", "Patient");
            }

            return View();
        }

        // Giriş işlemi
        [HttpPost]
        public IActionResult Login(LoginModel model)
        {
            if (ModelState.IsValid)
            {
                string userType;
                int userId;

                if (_baglanti.ValidateUser(model.TC, model.Sifre, out userType, out userId))
                {
                    // Session'a kullanıcı bilgilerini kaydet
                    HttpContext.Session.SetInt32("UserId", userId);
                    HttpContext.Session.SetString("UserType", userType);

                    // Kullanıcı tipine göre ilgili sayfaya yönlendir
                    if (userType == "Doktor")
                    {
                        DataTable dt = _baglanti.ExecuteQuery("SELECT DoktorID FROM Doktor WHERE KullaniciID = @KullaniciID",
                            new SqlParameter[] { new SqlParameter("@KullaniciID", userId) });

                        if (dt.Rows.Count > 0)
                        {
                            int doktorId = Convert.ToInt32(dt.Rows[0]["DoktorID"]);
                            HttpContext.Session.SetInt32("DoktorID", doktorId);
                        }

                        return RedirectToAction("Index", "Doctor");
                    }
                    else if (userType == "Hasta")
                    {
                        DataTable dt = _baglanti.ExecuteQuery("SELECT HastaID FROM Hasta WHERE KullaniciID = @KullaniciID",
                            new SqlParameter[] { new SqlParameter("@KullaniciID", userId) });

                        if (dt.Rows.Count > 0)
                        {
                            int hastaId = Convert.ToInt32(dt.Rows[0]["HastaID"]);
                            HttpContext.Session.SetInt32("HastaID", hastaId);
                        }

                        return RedirectToAction("Index", "Patient");
                    }
                }

                ModelState.AddModelError("", "TC Kimlik numarası veya şifre hatalı.");
            }

            return View(model);
        }

        // Kayıt sayfasını gösterme
        [HttpGet]
        public IActionResult Register()
        {
            return View();
        }

        // Kayıt işlemi
        [HttpPost]
        public IActionResult Register(RegisterModel model, IFormFile profilResim)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    // TC ve Email kontrolü
                    object tcKontrol = _baglanti.ExecuteScalar(
                        "SELECT COUNT(*) FROM Kullanici WHERE TC = @TC",
                        new SqlParameter[] { new SqlParameter("@TC", model.TC) }
                    );

                    object emailKontrol = _baglanti.ExecuteScalar(
                        "SELECT COUNT(*) FROM Kullanici WHERE Email = @Email",
                        new SqlParameter[] { new SqlParameter("@Email", model.Email) }
                    );

                    if (Convert.ToInt32(tcKontrol) > 0)
                    {
                        ModelState.AddModelError("TC", "Bu TC Kimlik numarası ile kayıtlı bir kullanıcı bulunmaktadır.");
                        return View(model);
                    }

                    if (Convert.ToInt32(emailKontrol) > 0)
                    {
                        ModelState.AddModelError("Email", "Bu e-posta adresi ile kayıtlı bir kullanıcı bulunmaktadır.");
                        return View(model);
                    }

                    // Şifreyi hashle
                    string hashedPassword = _baglanti.HashPassword(model.Sifre);

                    // Profil resmi işleme
                    byte[] imageData = null;
                    if (profilResim != null && profilResim.Length > 0)
                    {
                        using (var memoryStream = new MemoryStream())
                        {
                            profilResim.CopyTo(memoryStream);
                            imageData = memoryStream.ToArray();
                        }
                    }

                    // Kullanıcı kaydı
                    string insertKullaniciQuery = @"
                        INSERT INTO Kullanici (TC, Ad, Soyad, Email, Sifre, KullaniciTipi, ProfilResim, DogumTarihi, Cinsiyet)
                        VALUES (@TC, @Ad, @Soyad, @Email, @Sifre, @KullaniciTipi, @ProfilResim, @DogumTarihi, @Cinsiyet);
                        SELECT SCOPE_IDENTITY();
                    ";

                    SqlParameter[] kullaniciParams = {
                        new SqlParameter("@TC", model.TC),
                        new SqlParameter("@Ad", model.Ad),
                        new SqlParameter("@Soyad", model.Soyad),
                        new SqlParameter("@Email", model.Email),
                        new SqlParameter("@Sifre", hashedPassword),
                        new SqlParameter("@KullaniciTipi", model.KullaniciTipi),
                        new SqlParameter("@ProfilResim", (object)imageData ?? DBNull.Value),
                        new SqlParameter("@DogumTarihi", model.DogumTarihi),
                        new SqlParameter("@Cinsiyet", model.Cinsiyet)
                    };

                    // Kullanıcı kaydını yap ve ID'yi al
                    object kullaniciId = _baglanti.ExecuteScalar(insertKullaniciQuery, kullaniciParams);

                    if (kullaniciId == null || kullaniciId == DBNull.Value)
                    {
                        // ID alınamadıysa hata fırlat
                        throw new Exception("Kullanıcı kaydı yapılamadı. Veritabanından ID alınamadı.");
                    }

                    int userId = Convert.ToInt32(kullaniciId);

                    // Kullanıcı tipine göre Doktor veya Hasta tablosuna kayıt yap
                    if (model.KullaniciTipi == "Doktor")
                    {
                        string insertDoktorQuery = @"
                            INSERT INTO Doktor (KullaniciID, Uzmanlik)
                            VALUES (@KullaniciID, @Uzmanlik);
                        ";

                        SqlParameter[] doktorParams = {
                            new SqlParameter("@KullaniciID", userId),
                            new SqlParameter("@Uzmanlik", string.IsNullOrEmpty(model.Uzmanlik) ? DBNull.Value : (object)model.Uzmanlik)
                        };

                        int affectedRows = _baglanti.ExecuteNonQuery(insertDoktorQuery, doktorParams);
                        if (affectedRows <= 0)
                        {
                            throw new Exception("Doktor kaydı yapılamadı.");
                        }
                    }
                    else if (model.KullaniciTipi == "Hasta")
                    {
                        string insertHastaQuery = @"
                            INSERT INTO Hasta (KullaniciID, Boy, Kilo)
                            VALUES (@KullaniciID, @Boy, @Kilo);
                        ";

                        SqlParameter[] hastaParams = {
                            new SqlParameter("@KullaniciID", userId),
                            new SqlParameter("@Boy", model.Boy.HasValue ? (object)model.Boy.Value : DBNull.Value),
                            new SqlParameter("@Kilo", model.Kilo.HasValue ? (object)model.Kilo.Value : DBNull.Value)
                        };

                        int affectedRows = _baglanti.ExecuteNonQuery(insertHastaQuery, hastaParams);
                        if (affectedRows <= 0)
                        {
                            throw new Exception("Hasta kaydı yapılamadı.");
                        }
                    }

                    TempData["SuccessMessage"] = "Kayıt işlemi başarıyla tamamlandı. Giriş yapabilirsiniz.";
                    return RedirectToAction("Login");
                }
                catch (Exception ex)
                {
                    // Detaylı hata mesajı göster
                    ModelState.AddModelError("", $"Kayıt sırasında bir hata oluştu: {ex.Message}");
                    // Hata logla
                    Console.WriteLine($"Kayıt hatası: {ex.ToString()}");
                }
            }

            return View(model);
        }

        // Çıkış işlemi
        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Login");
        }
    }
}