using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using System;
using System.Data;
using System.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using ornek.Models;

namespace ornek.Controllers
{
    public class PatientController : Controller
    {
        private readonly baglanti _baglanti;
        private readonly IConfiguration _configuration;

        public PatientController(IConfiguration configuration)
        {
            _configuration = configuration;
            _baglanti = new baglanti(_configuration.GetConnectionString("SqlConnection"));
        }

        private bool IsPatient()
        {
            return HttpContext.Session.GetString("UserType") == "Hasta";
        }

        public IActionResult Index()
        {
            if (!IsPatient())
                return RedirectToAction("Login", "Account");

            int hastaId = HttpContext.Session.GetInt32("HastaID") ?? 0;
            int userId = HttpContext.Session.GetInt32("UserId") ?? 0;

            DataTable dtKullanici = _baglanti.GetUserInfo(userId);
            ViewBag.Kullanici = dtKullanici;

            string hastaQuery = @"
                SELECT h.Boy, h.Kilo
                FROM Hasta h
                WHERE h.HastaID = @HastaID
            ";
            DataTable dtHasta = _baglanti.ExecuteQuery(hastaQuery,
                new SqlParameter[] { new SqlParameter("@HastaID", hastaId) });
            ViewBag.Hasta = dtHasta.Rows[0];

            string kanSekeriQuery = @"
                SELECT KanSekeriID, OlcumDegeri, OlcumTarihi, OlcumSaati, OlcumTuru
                FROM KanSekeri
                WHERE HastaID = @HastaID
                ORDER BY OlcumTarihi DESC, OlcumSaati DESC
            ";
            ViewBag.KanSekeri = _baglanti.ExecuteQuery(kanSekeriQuery,
                new SqlParameter[] { new SqlParameter("@HastaID", hastaId) });

            string diyetQuery = @"
                SELECT dt.DiyetID, d.TurAdi, dt.UygulandiMi, dt.Tarih
                FROM DiyetTakip dt
                INNER JOIN DiyetTuru d ON dt.DiyetTuruID = d.DiyetTuruID
                WHERE dt.HastaID = @HastaID
                ORDER BY dt.Tarih DESC
            ";
            ViewBag.Diyet = _baglanti.ExecuteQuery(diyetQuery,
                new SqlParameter[] { new SqlParameter("@HastaID", hastaId) });

            string egzersizQuery = @"
                SELECT et.EgzersizID, e.TurAdi, et.YapildiMi, et.Tarih
                FROM EgzersizTakip et
                INNER JOIN EgzersizTuru e ON et.EgzersizTuruID = e.EgzersizTuruID
                WHERE et.HastaID = @HastaID
                ORDER BY et.Tarih DESC
            ";
            ViewBag.Egzersiz = _baglanti.ExecuteQuery(egzersizQuery,
                new SqlParameter[] { new SqlParameter("@HastaID", hastaId) });

            string belirtiQuery = @"
                SELECT hb.HastaBelirtiID, b.BelirtiAdi, hb.Tarih, hb.Siddet
                FROM HastaBelirtileri hb
                INNER JOIN Belirti b ON hb.BelirtiID = b.BelirtiID
                WHERE hb.HastaID = @HastaID
                ORDER BY hb.Tarih DESC
            ";
            ViewBag.Belirtiler = _baglanti.ExecuteQuery(belirtiQuery,
                new SqlParameter[] { new SqlParameter("@HastaID", hastaId) });

            return View();
        }

        [HttpGet]
        public IActionResult KanSekeriEkle()
        {
            if (!IsPatient())
                return RedirectToAction("Login", "Account");

            string belirtiQuery = "SELECT BelirtiID, BelirtiAdi FROM Belirti";
            ViewBag.Belirtiler = _baglanti.ExecuteQuery(belirtiQuery);
            return View();
        }

        [HttpPost]
        public IActionResult KanSekeriEkle(decimal olcumDegeri, DateTime olcumTarihi, string olcumSaati, string olcumTuru, int? belirtiId, int? siddet)
        {
            if (!IsPatient())
                return RedirectToAction("Login", "Account");

            int hastaId = HttpContext.Session.GetInt32("HastaID") ?? 0;

            try
            {
                string insertQuery = @"
                    INSERT INTO KanSekeri (HastaID, OlcumDegeri, OlcumTarihi, OlcumSaati, OlcumTuru)
                    VALUES (@HastaID, @OlcumDegeri, @OlcumTarihi, @OlcumSaati, @OlcumTuru)
                ";

                SqlParameter[] parameters = {
                    new SqlParameter("@HastaID", hastaId),
                    new SqlParameter("@OlcumDegeri", olcumDegeri),
                    new SqlParameter("@OlcumTarihi", olcumTarihi),
                    new SqlParameter("@OlcumSaati", olcumSaati),
                    new SqlParameter("@OlcumTuru", olcumTuru)
                };

                int sonuc = _baglanti.ExecuteNonQuery(insertQuery, parameters);

                if (sonuc > 0 && belirtiId.HasValue && siddet.HasValue)
                {
                    string belirtiInsertQuery = @"
                        INSERT INTO HastaBelirtileri (HastaID, BelirtiID, Tarih, Siddet)
                        VALUES (@HastaID, @BelirtiID, @Tarih, @Siddet)
                    ";

                    SqlParameter[] belirtiParams = {
                        new SqlParameter("@HastaID", hastaId),
                        new SqlParameter("@BelirtiID", belirtiId.Value),
                        new SqlParameter("@Tarih", olcumTarihi),
                        new SqlParameter("@Siddet", siddet.Value)
                    };

                    _baglanti.ExecuteNonQuery(belirtiInsertQuery, belirtiParams);
                }

                TempData["SuccessMessage"] = "Kan şekeri ölçümü başarıyla kaydedildi.";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Hata: {ex.Message}";
            }

            return RedirectToAction("Index");
        }

        [HttpPost]
        public IActionResult DiyetGuncelle(int diyetId, bool uygulandiMi)
        {
            if (!IsPatient())
                return RedirectToAction("Login", "Account");

            try
            {
                string updateQuery = @"
                    UPDATE DiyetTakip
                    SET UygulandiMi = @UygulandiMi
                    WHERE DiyetID = @DiyetID
                ";

                SqlParameter[] parameters = {
                    new SqlParameter("@UygulandiMi", uygulandiMi),
                    new SqlParameter("@DiyetID", diyetId)
                };

                int sonuc = _baglanti.ExecuteNonQuery(updateQuery, parameters);

                TempData["SuccessMessage"] = sonuc > 0 ? "Diyet durumu güncellendi." : "Diyet durumu güncellenemedi.";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Hata: {ex.Message}";
            }

            return RedirectToAction("Index");
        }

        [HttpPost]
        public IActionResult EgzersizGuncelle(int egzersizId, bool yapildiMi)
        {
            if (!IsPatient())
                return RedirectToAction("Login", "Account");

            try
            {
                string updateQuery = @"
                    UPDATE EgzersizTakip
                    SET YapildiMi = @YapildiMi
                    WHERE EgzersizID = @EgzersizID
                ";

                SqlParameter[] parameters = {
                    new SqlParameter("@YapildiMi", yapildiMi),
                    new SqlParameter("@EgzersizID", egzersizId)
                };

                int sonuc = _baglanti.ExecuteNonQuery(updateQuery, parameters);

                TempData["SuccessMessage"] = sonuc > 0 ? "Egzersiz durumu güncellendi." : "Egzersiz durumu güncellenemedi.";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Hata: {ex.Message}";
            }

            return RedirectToAction("Index");
        }
    }
}