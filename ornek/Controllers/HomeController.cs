using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;
using Proje3.Models;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Proje3.Controllers
{
    public class HomeController : Controller
    {
        private readonly string connectionString = "Data Source=(localdb)\\MSSQLLocalDB;Initial Catalog = DiyabetTakip; Integrated Security = True;";

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
    }
}