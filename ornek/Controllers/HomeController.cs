using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Net.Mail;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;
using Proje3.Models;

namespace Proje3.Controllers
{
    public class HomeController : Controller
    {
        private readonly string connectionString = "Data Source=(localdb)\\MSSQLLocalDB;Initial Catalog =Diabetes; Integrated Security = True;";

        // Password hashing function
        private string HashPassword(string password)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
                StringBuilder builder = new StringBuilder();
                foreach (byte b in bytes)
                {
                    builder.Append(b.ToString("x2"));
                }
                return builder.ToString();
            }
        }

        // Generate random password
        private string GenerateRandomPassword()
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
            var random = new Random();
            return new string(Enumerable.Repeat(chars, 8)
                .Select(s => s[random.Next(s.Length)]).ToArray());
        }

        // Login GET
        public IActionResult Login()
        {
            return View();
        }

        // Login POST
        [HttpPost]
        public IActionResult Login(Models.LoginModel model)
        {
            if (ModelState.IsValid)
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    string query = "SELECT KullaniciID, KullaniciTipi FROM Kullanici WHERE TC = @TC AND Sifre = @Sifre";
                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@TC", model.TC);
                        cmd.Parameters.AddWithValue("@Sifre", HashPassword(model.Sifre));
                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                HttpContext.Session.SetString("KullaniciID", reader["KullaniciID"].ToString());
                                HttpContext.Session.SetString("KullaniciTipi", reader["KullaniciTipi"].ToString());
                                if (reader["KullaniciTipi"].ToString() == "Doktor")
                                    return RedirectToAction("DoktorDashboard");
                                else
                                    return RedirectToAction("HastaDashboard");
                            }
                            else
                            {
                                ModelState.AddModelError("", "Geçersiz TC veya þifre.");
                            }
                        }
                    }
                }
            }
            return View(model);
        }

        // Register GET
        public IActionResult Register()
        {
            return View();
        }

        // Register POST
        [HttpPost]
        public IActionResult Register(Models.RegisterModel model)
        {
            if (ModelState.IsValid)
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    // Check if TC or Email already exists
                    string checkQuery = "SELECT COUNT(*) FROM Kullanici WHERE TC = @TC OR Email = @Email";
                    using (SqlCommand checkCmd = new SqlCommand(checkQuery, conn))
                    {
                        checkCmd.Parameters.AddWithValue("@TC", model.TC);
                        checkCmd.Parameters.AddWithValue("@Email", model.Email);
                        int count = (int)checkCmd.ExecuteScalar();
                        if (count > 0)
                        {
                            ModelState.AddModelError("", "TC veya E-posta zaten kayýtlý.");
                            return View(model);
                        }
                    }

                    // Insert Kullanici
                    string kullaniciQuery = @"INSERT INTO Kullanici (TC, Ad, Soyad, Email, Sifre, KullaniciTipi, DogumTarihi, Cinsiyet, KayitTarihi)
                                            VALUES (@TC, @Ad, @Soyad, @Email, @Sifre, @KullaniciTipi, @DogumTarihi, @Cinsiyet, @KayitTarihi);
                                            SELECT SCOPE_IDENTITY();";
                    int kullaniciID;
                    using (SqlCommand cmd = new SqlCommand(kullaniciQuery, conn))
                    {
                        cmd.Parameters.AddWithValue("@TC", model.TC);
                        cmd.Parameters.AddWithValue("@Ad", model.Ad);
                        cmd.Parameters.AddWithValue("@Soyad", model.Soyad);
                        cmd.Parameters.AddWithValue("@Email", model.Email);
                        cmd.Parameters.AddWithValue("@Sifre", HashPassword(model.Sifre));
                        cmd.Parameters.AddWithValue("@KullaniciTipi", model.KullaniciTipi);
                        cmd.Parameters.AddWithValue("@DogumTarihi", model.DogumTarihi);
                        cmd.Parameters.AddWithValue("@Cinsiyet", model.Cinsiyet);
                        cmd.Parameters.AddWithValue("@KayitTarihi", DateTime.Now);
                        kullaniciID = Convert.ToInt32(cmd.ExecuteScalar());
                    }

                    if (model.KullaniciTipi == "Doktor")
                    {
                        string doktorQuery = "INSERT INTO Doktor (KullaniciID, Uzmanlik) VALUES (@KullaniciID, @Uzmanlik)";
                        using (SqlCommand cmd = new SqlCommand(doktorQuery, conn))
                        {
                            cmd.Parameters.AddWithValue("@KullaniciID", kullaniciID);
                            cmd.Parameters.AddWithValue("@Uzmanlik", model.Uzmanlik ?? "");
                            cmd.ExecuteNonQuery();
                        }
                    }
                    else
                    {
                        string hastaQuery = "INSERT INTO Hasta (KullaniciID, Boy, Kilo) VALUES (@KullaniciID, @Boy, @Kilo)";
                        using (SqlCommand cmd = new SqlCommand(hastaQuery, conn))
                        {
                            cmd.Parameters.AddWithValue("@KullaniciID", kullaniciID);
                            cmd.Parameters.AddWithValue("@Boy", model.Boy ?? (object)DBNull.Value);
                            cmd.Parameters.AddWithValue("@Kilo", model.Kilo ?? (object)DBNull.Value);
                            cmd.ExecuteNonQuery();
                        }
                    }

                    // Simulate sending email with credentials (in real app, use SMTP)
                    return RedirectToAction("Login");
                }
            }
            return View(model);
        }

        // Doktor Dashboard
        public IActionResult DoktorDashboard()
        {
            if (HttpContext.Session.GetString("KullaniciTipi") != "Doktor") return RedirectToAction("Login");
            List<Models.Hasta> hastalar = new List<Models.Hasta>();
            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();
                string query = @"SELECT h.HastaID, h.KullaniciID, h.Boy, h.Kilo, k.Ad, k.Soyad, k.Email
                               FROM Hasta h JOIN Kullanici k ON h.KullaniciID = k.KullaniciID
                               JOIN HastaDr hd ON h.HastaID = hd.HastaID
                               WHERE hd.DoktorID = (SELECT DoktorID FROM Doktor WHERE KullaniciID = @KullaniciID)";
                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@KullaniciID", HttpContext.Session.GetString("KullaniciID"));
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            hastalar.Add(new Models.Hasta
                            {
                                HastaID = (int)reader["HastaID"],
                                KullaniciID = (int)reader["KullaniciID"],
                                Boy = reader["Boy"] != DBNull.Value ? (decimal?)reader["Boy"] : null,
                                Kilo = reader["Kilo"] != DBNull.Value ? (decimal?)reader["Kilo"] : null,
                                Kullanici = new Models.Kullanici
                                {
                                    Ad = reader["Ad"].ToString(),
                                    Soyad = reader["Soyad"].ToString(),
                                    Email = reader["Email"].ToString()
                                }
                            });
                        }
                    }
                }
            }
            return View(hastalar);
        }

        // Hasta Dashboard
        public IActionResult HastaDashboard()
        {
            if (HttpContext.Session.GetString("KullaniciTipi") != "Hasta") return RedirectToAction("Login");
            Models.Hasta hasta = new Models.Hasta();
            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();
                // Get patient info
                string query = @"SELECT h.HastaID, h.KullaniciID, h.Boy, h.Kilo, k.Ad, k.Soyad, k.Email
                               FROM Hasta h JOIN Kullanici k ON h.KullaniciID = k.KullaniciID
                               WHERE h.KullaniciID = @KullaniciID";
                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@KullaniciID", HttpContext.Session.GetString("KullaniciID"));
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            hasta = new Models.Hasta
                            {
                                HastaID = (int)reader["HastaID"],
                                KullaniciID = (int)reader["KullaniciID"],
                                Boy = reader["Boy"] != DBNull.Value ? (decimal?)reader["Boy"] : null,
                                Kilo = reader["Kilo"] != DBNull.Value ? (decimal?)reader["Kilo"] : null,
                                Kullanici = new Models.Kullanici
                                {
                                    Ad = reader["Ad"].ToString(),
                                    Soyad = reader["Soyad"].ToString(),
                                    Email = reader["Email"].ToString()
                                }
                            };
                        }
                    }
                }

                // Get diet plans
                string diyetQuery = @"SELECT dt.DiyetID, dt.Tarih, dt.UygulandiMi, d.TurAdi
                                    FROM DiyetTakip dt
                                    JOIN DiyetTuru d ON dt.DiyetTuruID = d.DiyetTuruID
                                    WHERE dt.HastaID = @HastaID
                                    ORDER BY dt.Tarih DESC";
                List<dynamic> diyetler = new List<dynamic>();
                using (SqlCommand cmd = new SqlCommand(diyetQuery, conn))
                {
                    cmd.Parameters.AddWithValue("@HastaID", hasta.HastaID);
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            diyetler.Add(new
                            {
                                DiyetID = (int)reader["DiyetID"],
                                Tarih = (DateTime)reader["Tarih"],
                                UygulandiMi = (bool)reader["UygulandiMi"],
                                TurAdi = reader["TurAdi"].ToString()
                            });
                        }
                    }
                }
                ViewBag.Diyetler = diyetler;

                // Get exercise plans
                string egzersizQuery = @"SELECT et.EgzersizID, et.Tarih, et.YapildiMi, e.TurAdi
                                       FROM EgzersizTakip et
                                       JOIN EgzersizTuru e ON et.EgzersizTuruID = e.EgzersizTuruID
                                       WHERE et.HastaID = @HastaID
                                       ORDER BY et.Tarih DESC";
                List<dynamic> egzersizler = new List<dynamic>();
                using (SqlCommand cmd = new SqlCommand(egzersizQuery, conn))
                {
                    cmd.Parameters.AddWithValue("@HastaID", hasta.HastaID);
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            egzersizler.Add(new
                            {
                                EgzersizID = (int)reader["EgzersizID"],
                                Tarih = (DateTime)reader["Tarih"],
                                YapildiMi = (bool)reader["YapildiMi"],
                                TurAdi = reader["TurAdi"].ToString()
                            });
                        }
                    }
                }
                ViewBag.Egzersizler = egzersizler;
            }
            return View(hasta);
        }

        // Add Hasta by Doktor
        [HttpGet]
        public IActionResult AddHasta()
        {
            if (HttpContext.Session.GetString("KullaniciTipi") != "Doktor") return RedirectToAction("Login");
            return View();
        }

        [HttpPost]
        public IActionResult AddHasta(Models.RegisterModel model)
        {
            if (HttpContext.Session.GetString("KullaniciTipi") != "Doktor") return RedirectToAction("Login");
            if (ModelState.IsValid)
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    // Check if TC or Email exists
                    string checkQuery = "SELECT COUNT(*) FROM Kullanici WHERE TC = @TC OR Email = @Email";
                    using (SqlCommand checkCmd = new SqlCommand(checkQuery, conn))
                    {
                        checkCmd.Parameters.AddWithValue("@TC", model.TC);
                        checkCmd.Parameters.AddWithValue("@Email", model.Email);
                        int count = (int)checkCmd.ExecuteScalar();
                        if (count > 0)
                        {
                            ModelState.AddModelError("", "TC veya E-posta zaten kayýtlý.");
                            return View(model);
                        }
                    }

                    // Generate random password
                    string generatedPassword = GenerateRandomPassword();
                    string hashedPassword = HashPassword(generatedPassword);

                    // Insert Kullanici
                    string kullaniciQuery = @"INSERT INTO Kullanici (TC, Ad, Soyad, Email, Sifre, KullaniciTipi, DogumTarihi, Cinsiyet, KayitTarihi)
                                            VALUES (@TC, @Ad, @Soyad, @Email, @Sifre, @KullaniciTipi, @DogumTarihi, @Cinsiyet, @KayitTarihi);
                                            SELECT SCOPE_IDENTITY();";
                    int kullaniciID;
                    using (SqlCommand cmd = new SqlCommand(kullaniciQuery, conn))
                    {
                        cmd.Parameters.AddWithValue("@TC", model.TC);
                        cmd.Parameters.AddWithValue("@Ad", model.Ad);
                        cmd.Parameters.AddWithValue("@Soyad", model.Soyad);
                        cmd.Parameters.AddWithValue("@Email", model.Email);
                        cmd.Parameters.AddWithValue("@Sifre", hashedPassword);
                        cmd.Parameters.AddWithValue("@KullaniciTipi", "Hasta");
                        cmd.Parameters.AddWithValue("@DogumTarihi", model.DogumTarihi);
                        cmd.Parameters.AddWithValue("@Cinsiyet", model.Cinsiyet);
                        cmd.Parameters.AddWithValue("@KayitTarihi", DateTime.Now);
                        kullaniciID = Convert.ToInt32(cmd.ExecuteScalar());
                    }

                    // Insert Hasta
                    string hastaQuery = "INSERT INTO Hasta (KullaniciID, Boy, Kilo) VALUES (@KullaniciID, @Boy, @Kilo); SELECT SCOPE_IDENTITY();";
                    int hastaID;
                    using (SqlCommand cmd = new SqlCommand(hastaQuery, conn))
                    {
                        cmd.Parameters.AddWithValue("@KullaniciID", kullaniciID);
                        cmd.Parameters.AddWithValue("@Boy", model.Boy ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@Kilo", model.Kilo ?? (object)DBNull.Value);
                        hastaID = Convert.ToInt32(cmd.ExecuteScalar());
                    }

                    // Assign to Doktor
                    string doktorQuery = "SELECT DoktorID FROM Doktor WHERE KullaniciID = @KullaniciID";
                    int doktorID;
                    using (SqlCommand cmd = new SqlCommand(doktorQuery, conn))
                    {
                        cmd.Parameters.AddWithValue("@KullaniciID", HttpContext.Session.GetString("KullaniciID"));
                        doktorID = (int)cmd.ExecuteScalar();
                    }

                    string hastaDrQuery = "INSERT INTO HastaDr (DoktorID, HastaID, TanimlamaTarihi) VALUES (@DoktorID, @HastaID, @TanimlamaTarihi)";
                    using (SqlCommand cmd = new SqlCommand(hastaDrQuery, conn))
                    {
                        cmd.Parameters.AddWithValue("@DoktorID", doktorID);
                        cmd.Parameters.AddWithValue("@HastaID", hastaID);
                        cmd.Parameters.AddWithValue("@TanimlamaTarihi", DateTime.Now);
                        cmd.ExecuteNonQuery();
                    }

                    // Send email with credentials
                    try
                    {
                        var smtpClient = new SmtpClient("smtp.gmail.com")
                        {
                            Port = 587,
                            Credentials = new NetworkCredential("your-email@gmail.com", "your-app-password"),
                            EnableSsl = true,
                        };

                        var mailMessage = new MailMessage
                        {
                            From = new MailAddress("your-email@gmail.com"),
                            Subject = "Diyabet Takip Sistemi - Giriþ Bilgileriniz",
                            Body = $@"Sayýn {model.Ad} {model.Soyad},
                            
                            Diyabet Takip Sistemine hoþ geldiniz! Aþaðýdaki bilgilerle sisteme giriþ yapabilirsiniz:
                            
                            Kullanýcý Adý (T.C. Kimlik No): {model.TC}
                            Þifre: {generatedPassword}
                            
                            Giriþ yaptýktan sonra þifrenizi deðiþtirmenizi öneririz.
                            
                            Saygýlar,
                            Diyabet Takip Sistemi Ekibi",
                            IsBodyHtml = false,
                        };
                        mailMessage.To.Add(model.Email);
                        smtpClient.Send(mailMessage);
                    }
                    catch (Exception ex)
                    {
                        ModelState.AddModelError("", $"E-posta gönderilemedi: {ex.Message}");
                        // Optionally, rollback the insertion if email fails
                        return View(model);
                    }

                    return RedirectToAction("DoktorDashboard");
                }
            }
            return View(model);
        }

        // Kan Sekeri Ekle
        [HttpGet]
        public IActionResult AddKanSekeri()
        {
            if (HttpContext.Session.GetString("KullaniciTipi") != "Hasta") return RedirectToAction("Login");
            return View();
        }

        [HttpPost]
        public IActionResult AddKanSekeri(Models.KanSekeri model)
        {
            if (HttpContext.Session.GetString("KullaniciTipi") != "Hasta") return RedirectToAction("Login");
            if (ModelState.IsValid)
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    string hastaQuery = "SELECT HastaID FROM Hasta WHERE KullaniciID = @KullaniciID";
                    int hastaID;
                    using (SqlCommand cmd = new SqlCommand(hastaQuery, conn))
                    {
                        cmd.Parameters.AddWithValue("@KullaniciID", HttpContext.Session.GetString("KullaniciID"));
                        hastaID = (int)cmd.ExecuteScalar();
                    }

                    string query = @"INSERT INTO KanSekeri (HastaID, OlcumDegeri, OlcumTarihi, OlcumSaati, OlcumTuru)
                                   VALUES (@HastaID, @OlcumDegeri, @OlcumTarihi, @OlcumSaati, @OlcumTuru)";
                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@HastaID", hastaID);
                        cmd.Parameters.AddWithValue("@OlcumDegeri", model.OlcumDegeri);
                        cmd.Parameters.AddWithValue("@OlcumTarihi", model.OlcumTarihi);
                        cmd.Parameters.AddWithValue("@OlcumSaati", model.OlcumSaati);
                        cmd.Parameters.AddWithValue("@OlcumTuru", model.OlcumTuru);
                        cmd.ExecuteNonQuery();
                    }
                }
                return RedirectToAction("HastaDashboard");
            }
            return View(model);
        }

        // Add Uyari by Doktor
        [HttpGet]
        public IActionResult AddUyari(int hastaID)
        {
            if (HttpContext.Session.GetString("KullaniciTipi") != "Doktor") return RedirectToAction("Login");
            ViewBag.HastaID = hastaID;
            return View();
        }

        [HttpPost]
        public IActionResult AddUyari(Models.Uyarilar model)
        {
            if (HttpContext.Session.GetString("KullaniciTipi") != "Doktor") return RedirectToAction("Login");
            if (ModelState.IsValid)
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    string doktorQuery = "SELECT DoktorID FROM Doktor WHERE KullaniciID = @KullaniciID";
                    int doktorID;
                    using (SqlCommand cmd = new SqlCommand(doktorQuery, conn))
                    {
                        cmd.Parameters.AddWithValue("@KullaniciID", HttpContext.Session.GetString("KullaniciID"));
                        doktorID = (int)cmd.ExecuteScalar();
                    }

                    string query = @"INSERT INTO Uyarilar (HastaID, DoktorID, UyariTuru, UyariMesaji, UyariTarihi, Okundu)
                                   VALUES (@HastaID, @DoktorID, @UyariTuru, @UyariMesaji, @UyariTarihi, @Okundu)";
                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@HastaID", model.HastaID);
                        cmd.Parameters.AddWithValue("@DoktorID", doktorID);
                        cmd.Parameters.AddWithValue("@UyariTuru", model.UyariTuru);
                        cmd.Parameters.AddWithValue("@UyariMesaji", model.UyariMesaji);
                        cmd.Parameters.AddWithValue("@UyariTarihi", DateTime.Now);
                        cmd.Parameters.AddWithValue("@Okundu", false);
                        cmd.ExecuteNonQuery();
                    }
                }
                return RedirectToAction("DoktorDashboard");
            }
            return View(model);
        }

        // Update Diet Status
        [HttpPost]
        public IActionResult UpdateDiyetDurumu(int diyetID, bool uygulandiMi)
        {
            if (HttpContext.Session.GetString("KullaniciTipREQUESTEDi") != "Hasta") return RedirectToAction("Login");
            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();
                string hastaQuery = "SELECT HastaID FROM Hasta WHERE KullaniciID = @KullaniciID";
                int hastaID;
                using (SqlCommand cmd = new SqlCommand(hastaQuery, conn))
                {
                    cmd.Parameters.AddWithValue("@KullaniciID", HttpContext.Session.GetString("KullaniciID"));
                    hastaID = (int)cmd.ExecuteScalar();
                }

                string query = @"UPDATE DiyetTakip SET UygulandiMi = @UygulandiMi 
                               WHERE DiyetID = @DiyetID AND HastaID = @HastaID";
                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@UygulandiMi", uygulandiMi);
                    cmd.Parameters.AddWithValue("@DiyetID", diyetID);
                    cmd.Parameters.AddWithValue("@HastaID", hastaID);
                    int rowsAffected = cmd.ExecuteNonQuery();
                    if (rowsAffected == 0)
                    {
                        return BadRequest("Diyet kaydý bulunamadý veya yetkiniz yok.");
                    }
                }
            }
            return RedirectToAction("HastaDashboard");
        }

        // Update Exercise Status
        [HttpPost]
        public IActionResult UpdateEgzersizDurumu(int egzersizID, bool yapildiMi)
        {
            if (HttpContext.Session.GetString("KullaniciTipi") != "Hasta") return RedirectToAction("Login");
            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();
                string hastaQuery = "SELECT HastaID FROM Hasta WHERE KullaniciID = @KullaniciID";
                int hastaID;
                using (SqlCommand cmd = new SqlCommand(hastaQuery, conn))
                {
                    cmd.Parameters.AddWithValue("@KullaniciID", HttpContext.Session.GetString("KullaniciID"));
                    hastaID = (int)cmd.ExecuteScalar();
                }

                string query = @"UPDATE EgzersizTakip SET YapildiMi = @YapildiMi 
                               WHERE EgzersizID = @EgzersizID AND HastaID = @HastaID";
                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@YapildiMi", yapildiMi);
                    cmd.Parameters.AddWithValue("@EgzersizID", egzersizID);
                    cmd.Parameters.AddWithValue("@HastaID", hastaID);
                    int rowsAffected = cmd.ExecuteNonQuery();
                    if (rowsAffected == 0)
                    {
                        return BadRequest("Egzersiz kaydý bulunamadý veya yetkiniz yok.");
                    }
                }
            }
            return RedirectToAction("HastaDashboard");
        }

        // Add Symptom
        [HttpGet]
        public IActionResult AddBelirti()
        {
            if (HttpContext.Session.GetString("KullaniciTipi") != "Hasta") return RedirectToAction("Login");
            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();
                string query = "SELECT BelirtiID, BelirtiAdi FROM Belirti";
                List<Belirti> belirtiler = new List<Belirti>();
                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            belirtiler.Add(new Belirti
                            {
                                BelirtiID = (int)reader["BelirtiID"],
                                BelirtiAdi = reader["BelirtiAdi"].ToString()
                            });
                        }
                    }
                }
                ViewBag.Belirtiler = belirtiler;
            }
            return View();
        }

        [HttpPost]
        public IActionResult AddBelirti(Models.HastaBelirtileri model)
        {
            if (HttpContext.Session.GetString("KullaniciTipi") != "Hasta") return RedirectToAction("Login");
            if (ModelState.IsValid)
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    string hastaQuery = "SELECT HastaID FROM Hasta WHERE KullaniciID = @KullaniciID";
                    int hastaID;
                    using (SqlCommand cmd = new SqlCommand(hastaQuery, conn))
                    {
                        cmd.Parameters.AddWithValue("@KullaniciID", HttpContext.Session.GetString("KullaniciID"));
                        hastaID = (int)cmd.ExecuteScalar();
                    }

                    string query = @"INSERT INTO HastaBelirtileri (HastaID, BelirtiID, Tarih, Siddet)
                                   VALUES (@HastaID, @BelirtiID, @Tarih, @Siddet)";
                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@HastaID", hastaID);
                        cmd.Parameters.AddWithValue("@BelirtiID", model.BelirtiID);
                        cmd.Parameters.AddWithValue("@Tarih", model.Tarih);
                        cmd.Parameters.AddWithValue("@Siddet", model.Siddet);
                        cmd.ExecuteNonQuery();
                    }
                }
                return RedirectToAction("HastaDashboard");
            }
            // Reload symptoms list if validation fails
            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();
                string query = "SELECT BelirtiID, BelirtiAdi FROM Belirti";
                List<Belirti> belirtiler = new List<Belirti>();
                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            belirtiler.Add(new Belirti
                            {
                                BelirtiID = (int)reader["BelirtiID"],
                                BelirtiAdi = reader["BelirtiAdi"].ToString()
                            });
                        }
                    }
                }
                ViewBag.Belirtiler = belirtiler;
            }
            return View(model);
        }
    }
}