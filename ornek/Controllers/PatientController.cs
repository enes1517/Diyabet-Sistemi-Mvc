using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using System;
using System.Data;
using System.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ornek.Models;
using System.IO;
using System.Collections.Generic;
using Proje3.Models;

namespace ornek.Controllers
{
    public class PatientController : Controller
    {
        private readonly baglanti _baglanti;
        private readonly IConfiguration _configuration;
        private readonly ILogger _logger;
        private static DateTime? _lastDailyCheck;

        public PatientController(IConfiguration configuration, ILogger<PatientController> logger)
        {
            _configuration = configuration;
            _baglanti = new baglanti(_configuration.GetConnectionString("SqlConnection"));
            _logger = logger;
        }

        private bool IsPatient()
        {
            return HttpContext.Session.GetString("UserType") == "Hasta";
        }

        public IActionResult Index(DateTime? startDate, DateTime? endDate)
        {
            if (!IsPatient())
            {
                _logger.LogWarning("Unauthorized access to Patient/Index by non-patient user.");
                return RedirectToAction("Login", "Account");
            }

            int hastaId = HttpContext.Session.GetInt32("HastaID") ?? 0;
            int userId = HttpContext.Session.GetInt32("UserId") ?? 0;

            try
            {
                // Simulate a daily check
                DateTime now = DateTime.Now;
                if (_lastDailyCheck == null || _lastDailyCheck.Value.Date != now.Date)
                {
                    CheckDailyMeasurements();
                    _lastDailyCheck = now;
                }

                // User info
                DataTable dtKullanici = _baglanti.GetUserInfo(userId);
                if (dtKullanici.Rows.Count == 0)
                {
                    _logger.LogWarning("User info not found for UserId {UserId}", userId);
                    TempData["ErrorMessage"] = "Kullanıcı bilgileri bulunamadı.";
                    return RedirectToAction("Login", "Account");
                }
                ViewBag.Kullanici = dtKullanici.Rows[0];

                // Patient info
                string hastaQuery = @"
                SELECT h.Boy, h.Kilo
                FROM Hasta h
                WHERE h.HastaID = @HastaID
            ";
                DataTable dtHasta = _baglanti.ExecuteQuery(hastaQuery,
                    new SqlParameter[] { new SqlParameter("@HastaID", hastaId) });
                ViewBag.Hasta = dtHasta.Rows.Count > 0 ? dtHasta.Rows[0] : null;

                // Blood sugar records
                string kanSekeriQuery = @"
                SELECT KanSekeriID, OlcumDegeri, OlcumTarihi, OlcumSaati, OlcumTuru
                FROM KanSekeri
                WHERE HastaID = @HastaID
                ORDER BY OlcumTarihi DESC, OlcumSaati DESC
            ";
                DataTable dtKanSekeri = _baglanti.ExecuteQuery(kanSekeriQuery,
                    new SqlParameter[] { new SqlParameter("@HastaID", hastaId) });
                ViewBag.KanSekeri = dtKanSekeri;

                // Warnings
                string uyariQuery = @"
                SELECT u.UyariID, u.UyariTuru, u.UyariMesaji, u.UyariTarihi
                FROM Uyarilar u
                WHERE u.HastaID = @HastaID AND u.Okundu = 0
                ORDER BY u.UyariTarihi DESC
            ";
                DataTable dtUyarilar = _baglanti.ExecuteQuery(uyariQuery,
                    new SqlParameter[] { new SqlParameter("@HastaID", hastaId) });
                ViewBag.Uyarilar = dtUyarilar;

                // Calculate blood sugar average for valid time slots
                if (dtKanSekeri.Rows.Count > 0)
                {
                    decimal total = 0;
                    int validCount = 0;

                    foreach (DataRow row in dtKanSekeri.Rows)
                    {
                        TimeSpan olcumSaati = TimeSpan.Parse(row["OlcumSaati"].ToString());
                        if (IsValidTimeSlot(olcumSaati))
                        {
                            total += Convert.ToDecimal(row["OlcumDegeri"]);
                            validCount++;
                        }
                    }

                    if (validCount > 0)
                    {
                        decimal average = total / validCount;
                        string insulinRecommendation = GetInsulinRecommendation(average);
                        ViewBag.AverageBloodSugar = average;
                        ViewBag.InsulinRecommendation = insulinRecommendation;
                    }
                }

                // Diet records and adherence percentage
                string diyetQuery = @"
                SELECT dt.DiyetID, d.TurAdi, dt.UygulandiMi, dt.Tarih
                FROM DiyetTakip dt
                INNER JOIN DiyetTuru d ON dt.DiyetTuruID = d.DiyetTuruID
                WHERE dt.HastaID = @HastaID
                ORDER BY dt.Tarih DESC
            ";
                DataTable dtDiyet = _baglanti.ExecuteQuery(diyetQuery,
                    new SqlParameter[] { new SqlParameter("@HastaID", hastaId) });
                ViewBag.Diyet = dtDiyet;

                // Calculate diet adherence percentage
                if (dtDiyet.Rows.Count > 0)
                {
                    int appliedCount = 0;
                    foreach (DataRow row in dtDiyet.Rows)
                    {
                        if (Convert.ToBoolean(row["UygulandiMi"]))
                        {
                            appliedCount++;
                        }
                    }
                    double diyetAdherence = (double)appliedCount / dtDiyet.Rows.Count * 100;
                    ViewBag.DiyetAdherence = Math.Round(diyetAdherence, 2);
                }
                else
                {
                    ViewBag.DiyetAdherence = 0;
                }

                // Exercise records and adherence percentage
                string egzersizQuery = @"
                SELECT et.EgzersizID, e.TurAdi, et.YapildiMi, et.Tarih
                FROM EgzersizTakip et
                INNER JOIN EgzersizTuru e ON et.EgzersizTuruID = e.EgzersizTuruID
                WHERE et.HastaID = @HastaID
                ORDER BY et.Tarih DESC
            ";
                DataTable dtEgzersiz = _baglanti.ExecuteQuery(egzersizQuery,
                    new SqlParameter[] { new SqlParameter("@HastaID", hastaId) });
                ViewBag.Egzersiz = dtEgzersiz;

                // Calculate exercise adherence percentage
                if (dtEgzersiz.Rows.Count > 0)
                {
                    int doneCount = 0;
                    foreach (DataRow row in dtEgzersiz.Rows)
                    {
                        if (Convert.ToBoolean(row["YapildiMi"]))
                        {
                            doneCount++;
                        }
                    }
                    double egzersizAdherence = (double)doneCount / dtEgzersiz.Rows.Count * 100;
                    ViewBag.EgzersizAdherence = Math.Round(egzersizAdherence, 2);
                }
                else
                {
                    ViewBag.EgzersizAdherence = 0;
                }

                // Symptoms
                string belirtiQuery = @"
                SELECT hb.HastaBelirtiID, b.BelirtiAdi, hb.Tarih, hb.Siddet
                FROM HastaBelirtileri hb
                INNER JOIN Belirti b ON hb.BelirtiID = b.BelirtiID
                WHERE hb.HastaID = @HastaID
                ORDER BY hb.Tarih DESC
            ";
                ViewBag.Belirtiler = _baglanti.ExecuteQuery(belirtiQuery,
                    new SqlParameter[] { new SqlParameter("@HastaID", hastaId) });

                // Insulin records with date filtering
                // Set default date range: last 7 days if not provided
                startDate = startDate ?? DateTime.Today.AddDays(-7);
                endDate = endDate ?? DateTime.Today;
                ViewBag.StartDate = startDate;
                ViewBag.EndDate = endDate;

                string insulinQuery = @"
                SELECT i.InsulinID, i.Doz, i.UygulamaTarihi, i.UygulamaSaati, i.OrtalamaKanSekeri
                FROM Insulin i
                WHERE i.HastaID = @HastaID
                AND i.UygulamaTarihi >= @StartDate AND i.UygulamaTarihi <= @EndDate
                ORDER BY i.UygulamaTarihi DESC, i.UygulamaSaati DESC
            ";
                List<SqlParameter> insulinParams = new List<SqlParameter>
            {
                new SqlParameter("@HastaID", hastaId),
                new SqlParameter("@StartDate", startDate.Value),
                new SqlParameter("@EndDate", endDate.Value)
            };

                DataTable dtInsulin = _baglanti.ExecuteQuery(insulinQuery, insulinParams.ToArray());
                ViewBag.Insulin = dtInsulin;

                // Prepare data for blood sugar chart
                List<string> chartLabels = new List<string>();
                List<decimal> chartData = new List<decimal>();
                foreach (DataRow row in dtKanSekeri.Rows)
                {
                    DateTime tarih = Convert.ToDateTime(row["OlcumTarihi"]);
                    TimeSpan saat = TimeSpan.Parse(row["OlcumSaati"].ToString());
                    chartLabels.Add($"{tarih:dd.MM.yyyy} {saat}");
                    chartData.Add(Convert.ToDecimal(row["OlcumDegeri"]));
                }
                ViewBag.ChartLabels = chartLabels;
                ViewBag.ChartData = chartData;

                return View();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading Index page for HastaID {HastaID}", hastaId);
                TempData["ErrorMessage"] = $"Hata: {ex.Message}";
                return RedirectToAction("Login", "Account");
            }
        }

        [HttpGet]
        public IActionResult KanSekeriEkle()
        {
            if (!IsPatient())
            {
                _logger.LogWarning("Unauthorized access to Patient/KanSekeriEkle by non-patient user.");
                return RedirectToAction("Login", "Account");
            }

            try
            {
                string belirtiQuery = "SELECT BelirtiID, BelirtiAdi FROM Belirti";
                ViewBag.Belirtiler = _baglanti.ExecuteQuery(belirtiQuery);

                // Fetch warnings for the patient to display on the page
                int hastaId = HttpContext.Session.GetInt32("HastaID") ?? 0;
                string uyariQuery = @"
            SELECT u.UyariID, u.UyariTuru, u.UyariMesaji, u.UyariTarihi
            FROM Uyarilar u
            WHERE u.HastaID = @HastaID AND u.Okundu = 0
            ORDER BY u.UyariTarihi DESC
        ";
                DataTable dtUyarilar = _baglanti.ExecuteQuery(uyariQuery,
                    new SqlParameter[] { new SqlParameter("@HastaID", hastaId) });
                ViewBag.Uyarilar = dtUyarilar;

                return View();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading KanSekeriEkle page.");
                TempData["ErrorMessage"] = $"Hata: {ex.Message}";
                return RedirectToAction("Index");
            }
        }

        [HttpPost]
        public IActionResult KanSekeriEkle(decimal olcumDegeri, DateTime olcumTarihi, string olcumSaati, string olcumTuru, int? belirtiId, int? siddet)
        {
            if (!IsPatient())
            {
                _logger.LogWarning("Unauthorized POST to Patient/KanSekeriEkle by non-patient user.");
                return RedirectToAction("Login", "Account");
            }

            int hastaId = HttpContext.Session.GetInt32("HastaID") ?? 0;

            try
            {
                // Convert string time to TimeSpan for database
                TimeSpan timeSpan = TimeSpan.Parse(olcumSaati);
                bool isValidTime = IsValidTimeSlot(timeSpan);

                // Insert blood sugar measurement (always record, regardless of time)
                string insertQuery = @"
        INSERT INTO KanSekeri (HastaID, OlcumDegeri, OlcumTarihi, OlcumSaati, OlcumTuru)
        VALUES (@HastaID, @OlcumDegeri, @OlcumTarihi, @OlcumSaati, @OlcumTuru)
    ";

                SqlParameter[] parameters = {
        new SqlParameter("@HastaID", hastaId),
        new SqlParameter("@OlcumDegeri", olcumDegeri),
        new SqlParameter("@OlcumTarihi", olcumTarihi),
        new SqlParameter("@OlcumSaati", timeSpan),
        new SqlParameter("@OlcumTuru", olcumTuru)
    };

                int sonuc = _baglanti.ExecuteNonQuery(insertQuery, parameters);

                if (sonuc > 0)
                {
                    // Insert symptom if provided
                    if (belirtiId.HasValue && siddet.HasValue)
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

                    // Check blood sugar level and generate warning based on the table
                    string uyariTuru = "";
                    string uyariMesaji = "";
                    int doktorId = GetDoktorId(hastaId);

                    if (olcumDegeri < 70)
                    {
                        uyariTuru = "Hipoglisemi Riski";
                        uyariMesaji = "Hastanın kan şekeri seviyesi 70 mg/dL'nin altına düştü. Hipoglisemi riski! Hızlı müdahale gerekebilir.";
                    }
                    else if (olcumDegeri >= 70 && olcumDegeri <= 110)
                    {
                        uyariTuru = "Normal Seviye";
                        uyariMesaji = "Kan şekeri seviyeniz normal aralıktadır ancak doktor bilgilendirildi. Hiçbir işlem gerekmez.";
                    }
                    else if (olcumDegeri >= 111 && olcumDegeri <= 150)
                    {
                        uyariTuru = "Orta Yüksek Seviye";
                        uyariMesaji = "Hastanın kan şekeri 111-150 mg/dL arasındadır. Durum izlenmeli.";
                    }
                    else if (olcumDegeri >= 151 && olcumDegeri <= 200)
                    {
                        uyariTuru = "Yüksek Seviye";
                        uyariMesaji = "Hastanın kan şekeri 151-200 mg/dL arasındadır. Diyabet kontrolü gereklidir.";
                    }
                    else if (olcumDegeri > 200)
                    {
                        uyariTuru = "Çok Yüksek Seviye (Hiperglisemi)";
                        uyariMesaji = "Hastanın kan şekeri 200 mg/dL'nin üzerinde. Hiperglisemi durumu. Acil müdahale gerekebilir.";
                    }

                    // Generate warning for all levels (including 70 to 200 mg/dL)
                    string uyariInsertQuery = @"
                INSERT INTO Uyarilar (HastaID, DoktorID, UyariTuru, UyariMesaji, UyariTarihi, Okundu)
                VALUES (@HastaID, @DoktorID, @UyariTuru, @UyariMesaji, GETDATE(), 0)
            ";

                    SqlParameter[] uyariParams = {
                new SqlParameter("@HastaID", hastaId),
                new SqlParameter("@DoktorID", doktorId),
                new SqlParameter("@UyariTuru", uyariTuru),
                new SqlParameter("@UyariMesaji", uyariMesaji)
            };

                    _baglanti.ExecuteNonQuery(uyariInsertQuery, uyariParams);

                    // Warn for out-of-range time slot
                    if (!isValidTime)
                    {
                        string uyariTur = "Geçersiz Ölçüm Zamanı";
                        string uyariMesaj = $"Ölçüm {olcumSaati} saatinde yapıldı. Önerilen zaman aralıkları dışında olduğu için ortalamaya dahil edilmeyecektir.";
                        AddWarning(hastaId, uyariMesaj, uyariTur);
                    }

                    // Check for missing measurements (less than 3 per day, only valid times)
                    string countQuery = @"
            SELECT COUNT(*) FROM KanSekeri
            WHERE HastaID = @HastaID AND OlcumTarihi = @OlcumTarihi 
            AND (
                OlcumSaati BETWEEN '07:00:00' AND '08:00:00' OR
                OlcumSaati BETWEEN '13:00:00' AND '14:00:00' OR
                OlcumSaati BETWEEN '15:00:00' AND '16:00:00' OR
                OlcumSaati BETWEEN '18:00:00' AND '19:00:00' OR
                OlcumSaati BETWEEN '22:00:00' AND '23:00:00'
            )
        ";
                    object measurementCount = _baglanti.ExecuteScalar(countQuery,
                        new SqlParameter[] {
                new SqlParameter("@HastaID", hastaId),
                new SqlParameter("@OlcumTarihi", olcumTarihi.Date)
                        });

                    int dailyValidMeasurements = Convert.ToInt32(measurementCount);
                    if (dailyValidMeasurements < 3)
                    {
                        AddWarning(hastaId, $"Hastanın günlük kan şekeri ölçüm sayısı yetersiz (<3). Durum izlenmelidir.", "Ölçüm Eksikliği (3'ten Az Giriş)");
                    }

                    // Check for missing expected time slots
                    string recordedTimesQuery = @"
            SELECT OlcumSaati FROM KanSekeri
            WHERE HastaID = @HastaID AND OlcumTarihi = @OlcumTarihi
            AND (
                OlcumSaati BETWEEN '07:00:00' AND '08:00:00' OR
                OlcumSaati BETWEEN '13:00:00' AND '14:00:00' OR
                OlcumSaati BETWEEN '15:00:00' AND '16:00:00' OR
                OlcumSaati BETWEEN '18:00:00' AND '19:00:00' OR
                OlcumSaati BETWEEN '22:00:00' AND '23:00:00'
            )
        ";
                    DataTable dtRecordedTimes = _baglanti.ExecuteQuery(recordedTimesQuery,
                        new SqlParameter[] {
                new SqlParameter("@HastaID", hastaId),
                new SqlParameter("@OlcumTarihi", olcumTarihi.Date)
                        });

                    HashSet<string> recordedTimeSlots = new HashSet<string>();
                    foreach (DataRow row in dtRecordedTimes.Rows)
                    {
                        TimeSpan time = TimeSpan.Parse(row["OlcumSaati"].ToString());
                        if (time >= TimeSpan.Parse("07:00:00") && time <= TimeSpan.Parse("08:00:00"))
                            recordedTimeSlots.Add("07:00-08:00");
                        else if (time >= TimeSpan.Parse("13:00:00") && time <= TimeSpan.Parse("14:00:00"))
                            recordedTimeSlots.Add("13:00-14:00");
                        else if (time >= TimeSpan.Parse("15:00:00") && time <= TimeSpan.Parse("16:00:00"))
                            recordedTimeSlots.Add("15:00-16:00");
                        else if (time >= TimeSpan.Parse("18:00:00") && time <= TimeSpan.Parse("19:00:00"))
                            recordedTimeSlots.Add("18:00-19:00");
                        else if (time >= TimeSpan.Parse("22:00:00") && time <= TimeSpan.Parse("23:00:00"))
                            recordedTimeSlots.Add("22:00-23:00");
                    }

                    HashSet<string> expectedTimeSlots = new HashSet<string> { "07:00-08:00", "13:00-14:00", "15:00-16:00", "18:00-19:00", "22:00-23:00" };
                    foreach (string timeSlot in expectedTimeSlots)
                    {
                        if (!recordedTimeSlots.Contains(timeSlot))
                        {
                            string uyariTur = "Ölçüm Eksikliği";
                            string uyariMesaj = $"Ölçüm eksik! {timeSlot} aralığında ölçüm alınmadı.";
                            AddWarning(hastaId, uyariMesaj, uyariTur);
                        }
                    }

                    _logger.LogInformation("Blood sugar measurement added for HastaID {HastaID}", hastaId);
                    TempData["SuccessMessage"] = "Kan şekeri ölçümü başarıyla kaydedildi.";
                }
                else
                {
                    _logger.LogWarning("Failed to insert blood sugar measurement for HastaID {HastaID}", hastaId);
                    TempData["ErrorMessage"] = "Kan şekeri ölçümü kaydedilemedi.";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding blood sugar measurement for HastaID {HastaID}", hastaId);
                TempData["ErrorMessage"] = $"Hata: {ex.Message}";
            }

            return RedirectToAction("Index");
        }

        // Method to check daily measurements for all patients
        private void CheckDailyMeasurements()
        {
            try
            {
                // Get all patients
                string hastaQuery = "SELECT HastaID FROM Hasta";
                DataTable dtHastalar = _baglanti.ExecuteQuery(hastaQuery);

                DateTime today = DateTime.Today;

                foreach (DataRow hastaRow in dtHastalar.Rows)
                {
                    int hastaId = Convert.ToInt32(hastaRow["HastaID"]);

                    // Check if a warning for missing measurements already exists for today
                    string existingWarningQuery = @"
                SELECT COUNT(*) FROM Uyarilar
                WHERE HastaID = @HastaID 
                AND UyariTuru = 'Ölçüm Eksikliği (Hiç Giriş Yok)'
                AND CAST(UyariTarihi AS DATE) = @Today
            ";
                    object warningCount = _baglanti.ExecuteScalar(existingWarningQuery,
                        new SqlParameter[] {
                            new SqlParameter("@HastaID", hastaId),
                            new SqlParameter("@Today", today)
                        });

                    if (Convert.ToInt32(warningCount) > 0)
                    {
                        _logger.LogDebug("Skipping HastaID {HastaID} as a warning already exists for today.", hastaId);
                        continue; // Skip if a warning already exists for today
                    }

                    // Check daily measurements (any measurement, not just valid time slots)
                    string countQuery = @"
                SELECT COUNT(*) FROM KanSekeri
                WHERE HastaID = @HastaID AND OlcumTarihi = @OlcumTarihi
            ";
                    object measurementCount = _baglanti.ExecuteScalar(countQuery,
                        new SqlParameter[] {
                            new SqlParameter("@HastaID", hastaId),
                            new SqlParameter("@OlcumTarihi", today)
                        });

                    int dailyMeasurements = Convert.ToInt32(measurementCount);
                    if (dailyMeasurements == 0)
                    {
                        AddWarning(hastaId, "Hasta gün boyunca kan şekeri ölçümü yapmamıştır. Acil takip önerilir.", "Ölçüm Eksikliği (Hiç Giriş Yok)");
                        _logger.LogInformation("Warning added for HastaID {HastaID} due to no measurements today.", hastaId);
                    }
                }

                _logger.LogInformation("Daily measurement check completed for all patients.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking daily measurements for patients.");
            }
        }

        // Helper method to check if the measurement time is within valid time slots
        private bool IsValidTimeSlot(TimeSpan time)
        {
            return (time >= TimeSpan.Parse("07:00:00") && time <= TimeSpan.Parse("08:00:00")) ||
                   (time >= TimeSpan.Parse("13:00:00") && time <= TimeSpan.Parse("14:00:00")) ||
                   (time >= TimeSpan.Parse("15:00:00") && time <= TimeSpan.Parse("16:00:00")) ||
                   (time >= TimeSpan.Parse("18:00:00") && time <= TimeSpan.Parse("19:00:00")) ||
                   (time >= TimeSpan.Parse("22:00:00") && time <= TimeSpan.Parse("23:00:00"));
        }

        // Helper method to get the doctor's ID for the patient
        private int GetDoktorId(int hastaId)
        {
            string doktorQuery = @"
    SELECT DoktorID FROM HastaDr WHERE HastaID = @HastaID
";
            object doktorIdObj = _baglanti.ExecuteScalar(doktorQuery,
                new SqlParameter[] { new SqlParameter("@HastaID", hastaId) });
            return doktorIdObj != null ? Convert.ToInt32(doktorIdObj) : 0;
        }

        [HttpPost]
        public IActionResult DiyetGuncelle(int diyetId, bool uygulandiMi)
        {
            if (!IsPatient())
            {
                _logger.LogWarning("Unauthorized POST to Patient/DiyetGuncelle by non-patient user.");
                return RedirectToAction("Login", "Account");
            }

            try
            {
                _logger.LogDebug("Received diyetId: {DiyetId}, uygulandiMi: {UygulandiMi}", diyetId, uygulandiMi);

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

                if (sonuc > 0)
                {
                    _logger.LogInformation("Diet status updated for DiyetID {DiyetID}, UygulandiMi: {UygulandiMi}", diyetId, uygulandiMi);
                    TempData["SuccessMessage"] = uygulandiMi ? "Diyet uygulandı olarak güncellendi." : "Diyet uygulanmadı olarak güncellendi.";
                }
                else
                {
                    _logger.LogWarning("No rows updated for DiyetID {DiyetID}", diyetId);
                    TempData["ErrorMessage"] = "Diyet durumu güncellenemedi. Kayıt bulunamadı.";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating diet status for DiyetID {DiyetID}", diyetId);
                TempData["ErrorMessage"] = $"Hata: {ex.Message}";
            }

            return RedirectToAction("Index");
        }

        [HttpPost]
        public IActionResult EgzersizGuncelle(int egzersizId, bool yapildiMi)
        {
            if (!IsPatient())
            {
                _logger.LogWarning("Unauthorized POST to Patient/EgzersizGuncelle by non-patient user.");
                return RedirectToAction("Login", "Account");
            }

            _logger.LogDebug("Received egzersizId: {EgzersizId}, yapildiMi: {YapildiMi}", egzersizId, yapildiMi);

            try
            {
                string checkQuery = "SELECT COUNT(*) FROM EgzersizTakip WHERE EgzersizID = @EgzersizID";
                object checkResult = _baglanti.ExecuteScalar(checkQuery, new SqlParameter[] { new SqlParameter("@EgzersizID", egzersizId) });
                if (Convert.ToInt32(checkResult) == 0)
                {
                    _logger.LogWarning("EgzersizID {EgzersizID} not found in EgzersizTakip.", egzersizId);
                    TempData["ErrorMessage"] = "Egzersiz kaydı bulunamadı.";
                    return RedirectToAction("Index");
                }

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

                if (sonuc > 0)
                {
                    _logger.LogInformation("Exercise status updated successfully for EgzersizID {EgzersizID}, YapildiMi: {YapildiMi}", egzersizId, yapildiMi);
                    TempData["SuccessMessage"] = yapildiMi ? "Egzersiz yapıldı olarak işaretlendi." : "Egzersiz yapılmadı olarak işaretlendi.";
                }
                else
                {
                    _logger.LogWarning("No rows updated for EgzersizID {EgzersizID}.", egzersizId);
                    TempData["ErrorMessage"] = "Egzersiz durumu güncellenemedi. Kayıt bulunamadı.";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating exercise status for EgzersizID {EgzersizID}", egzersizId);
                TempData["ErrorMessage"] = $"Hata: {ex.Message}";
            }

            return RedirectToAction("Index");
        }

        [HttpGet]
        public IActionResult Profil()
        {
            if (!IsPatient())
            {
                _logger.LogWarning("Unauthorized access to Patient/Profil by non-patient user.");
                return RedirectToAction("Login", "Account");
            }

            int userId = HttpContext.Session.GetInt32("UserId") ?? 0;
            int hastaId = HttpContext.Session.GetInt32("HastaID") ?? 0;

            try
            {
                DataTable dtKullanici = _baglanti.GetUserInfo(userId);
                if (dtKullanici.Rows.Count == 0)
                {
                    _logger.LogWarning("User info not found for UserId {UserId}", userId);
                    TempData["ErrorMessage"] = "Kullanıcı bulunamadı.";
                    return RedirectToAction("Index");
                }

                string hastaQuery = @"
            SELECT Boy, Kilo
            FROM Hasta
            WHERE HastaID = @HastaID
        ";
                DataTable dtHasta = _baglanti.ExecuteQuery(hastaQuery,
                    new SqlParameter[] { new SqlParameter("@HastaID", hastaId) });

                var model = new PatientProfileViewModel
                {
                    Ad = dtKullanici.Rows[0]["Ad"]?.ToString(),
                    Soyad = dtKullanici.Rows[0]["Soyad"]?.ToString(),
                    Email = dtKullanici.Rows[0]["Email"]?.ToString(),
                    ProfilResim = !DBNull.Value.Equals(dtKullanici.Rows[0]["ProfilResim"]) ? (byte[])dtKullanici.Rows[0]["ProfilResim"] : null,
                    Boy = dtHasta.Rows.Count > 0 && !DBNull.Value.Equals(dtHasta.Rows[0]["Boy"]) ? Convert.ToDecimal(dtHasta.Rows[0]["Boy"]) : null,
                    Kilo = dtHasta.Rows.Count > 0 && !DBNull.Value.Equals(dtHasta.Rows[0]["Kilo"]) ? Convert.ToDecimal(dtHasta.Rows[0]["Kilo"]) : null
                };

                _logger.LogInformation("Profile loaded for UserId {UserId}, HastaID {HastaID}", userId, hastaId);
                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading profile for UserId {UserId}, HastaID {HastaID}", userId, hastaId);
                TempData["ErrorMessage"] = $"Hata: {ex.Message}";
                return RedirectToAction("Index");
            }
        }

        [HttpPost]
        public IActionResult ProfilGuncelle(string ad, string soyad, string email, decimal? boy, decimal? kilo, IFormFile profilResim)
        {
            if (!IsPatient())
            {
                _logger.LogWarning("Unauthorized POST to Patient/ProfilGuncelle by non-patient user.");
                return RedirectToAction("Login", "Account");
            }

            int userId = HttpContext.Session.GetInt32("UserId") ?? 0;
            int hastaId = HttpContext.Session.GetInt32("HastaID") ?? 0;

            try
            {
                if (string.IsNullOrEmpty(ad) || string.IsNullOrEmpty(soyad) || string.IsNullOrEmpty(email))
                {
                    _logger.LogWarning("Invalid input data for profile update for UserId {UserId}", userId);
                    TempData["ErrorMessage"] = "Ad, soyad ve e-posta alanları zorunludur.";
                    return RedirectToAction("Profil");
                }

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
                    using (var memoryStream = new MemoryStream())
                    {
                        profilResim.CopyTo(memoryStream);
                        imageData = memoryStream.ToArray();
                    }
                    profilResimEklemesi = ", ProfilResim = @ProfilResim";
                    kullaniciParams.Add(new SqlParameter("@ProfilResim", imageData));
                }

                updateKullaniciQuery = string.Format(updateKullaniciQuery, profilResimEklemesi);
                int kullaniciSonuc = _baglanti.ExecuteNonQuery(updateKullaniciQuery, kullaniciParams.ToArray());

                string updateHastaQuery = @"
            UPDATE Hasta SET
            Boy = @Boy,
            Kilo = @Kilo
            WHERE HastaID = @HastaID
        ";

                SqlParameter[] hastaParams = {
            new SqlParameter("@Boy", (object)boy ?? DBNull.Value),
            new SqlParameter("@Kilo", (object)kilo ?? DBNull.Value),
            new SqlParameter("@HastaID", hastaId)
        };

                int hastaSonuc = _baglanti.ExecuteNonQuery(updateHastaQuery, hastaParams);

                _logger.LogInformation("Profile updated for UserId {UserId}, HastaID {HastaID}", userId, hastaId);
                TempData["SuccessMessage"] = "Profiliniz başarıyla güncellenmiştir.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating profile for UserId {UserId}, HastaID {HastaID}", userId, hastaId);
                TempData["ErrorMessage"] = $"Hata: {ex.Message}";
            }

            return RedirectToAction("Profil");
        }

        [HttpPost]
        public IActionResult SifreDegistir(string eskiSifre, string yeniSifre, string yeniSifreTekrar)
        {
            if (!IsPatient())
            {
                _logger.LogWarning("Unauthorized POST to Patient/SifreDegistir by non-patient user.");
                return RedirectToAction("Login", "Account");
            }

            int userId = HttpContext.Session.GetInt32("UserId") ?? 0;

            try
            {
                if (userId == 0)
                {
                    _logger.LogWarning("Session data missing for UserId in SifreDegistir.");
                    TempData["ErrorMessage"] = "Oturum bilgileri eksik. Lütfen tekrar giriş yapın.";
                    return RedirectToAction("Login", "Account");
                }

                if (string.IsNullOrEmpty(eskiSifre) || string.IsNullOrEmpty(yeniSifre) || string.IsNullOrEmpty(yeniSifreTekrar))
                {
                    _logger.LogWarning("Incomplete password change input for UserId {UserId}", userId);
                    TempData["ErrorMessage"] = "Tüm şifre alanları doldurulmalıdır.";
                    return RedirectToAction("Profil");
                }

                if (yeniSifre != yeniSifreTekrar)
                {
                    _logger.LogWarning("New passwords do not match for UserId {UserId}", userId);
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
                    _logger.LogWarning("Incorrect old password for UserId {UserId}", userId);
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
                    _logger.LogInformation("Password changed successfully for UserId {UserId}", userId);
                    TempData["SuccessMessage"] = "Şifreniz başarıyla değiştirilmiştir.";
                }
                else
                {
                    _logger.LogWarning("Password change failed for UserId {UserId}", userId);
                    TempData["ErrorMessage"] = "Şifre değiştirilirken bir hata oluştu.";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error changing password for UserId {UserId}", userId);
                TempData["ErrorMessage"] = $"Hata: {ex.Message}";
            }

            return RedirectToAction("Profil");
        }

        private string GetInsulinRecommendation(decimal averageBloodSugar)
        {
            if (averageBloodSugar >= 111 && averageBloodSugar <= 150)
                return "1 mL";
            else if (averageBloodSugar >= 151 && averageBloodSugar <= 200)
                return "2 mL";
            else if (averageBloodSugar > 200)
                return "3 mL";
            else
                return "Insülin gerekmiyor";
        }

        private void AddWarning(int hastaId, string uyariMesaji, string uyariTuru)
        {
            // Get the patient's assigned doctor
            string doktorQuery = @"
        SELECT DoktorID FROM HastaDr WHERE HastaID = @HastaID
    ";
            object doktorIdObj = _baglanti.ExecuteScalar(doktorQuery,
                new SqlParameter[] { new SqlParameter("@HastaID", hastaId) });
            int doktorId = doktorIdObj != null ? Convert.ToInt32(doktorIdObj) : 0;

            string uyariInsertQuery = @"
        INSERT INTO Uyarilar (HastaID, DoktorID, UyariTuru, UyariMesaji, UyariTarihi, Okundu)
        VALUES (@HastaID, @DoktorID, @UyariTuru, @UyariMesaji, GETDATE(), 0)
    ";

            SqlParameter[] uyariParams = {
        new SqlParameter("@HastaID", hastaId),
        new SqlParameter("@DoktorID", doktorId),
        new SqlParameter("@UyariTuru", uyariTuru),
        new SqlParameter("@UyariMesaji", uyariMesaji)
    };

            _baglanti.ExecuteNonQuery(uyariInsertQuery, uyariParams);
        }
    }
}