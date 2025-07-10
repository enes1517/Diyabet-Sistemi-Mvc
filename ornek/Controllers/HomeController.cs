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

        private string GenerateRandomPassword()
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
            var random = new Random();
            return new string(Enumerable.Repeat(chars, 8)
                .Select(s => s[random.Next(s.Length)]).ToArray());
        }

        public IActionResult Login()
        {
            return View();
        }

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

        public IActionResult Register()
        {
            return View();
        }

        [HttpPost]
        public IActionResult Register(Models.RegisterModel model)
        {
            if (ModelState.IsValid)
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();
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

                    return RedirectToAction("Login");
                }
            }
            return View(model);
        }

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


    }
}