using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Proje3.Models
{
    // Kullanıcı model sınıfı
    public class Kullanici
    {
        public int KullaniciID { get; set; }

        [Required(ErrorMessage = "TC Kimlik numarası zorunludur.")]
        [RegularExpression(@"^[0-9]{11}$", ErrorMessage = "TC Kimlik numarası 11 haneli olmalıdır.")]
        public string TC { get; set; }

        [Required(ErrorMessage = "Ad alanı zorunludur.")]
        [StringLength(50, ErrorMessage = "Ad en fazla 50 karakter olabilir.")]
        public string Ad { get; set; }

        [Required(ErrorMessage = "Soyad alanı zorunludur.")]
        [StringLength(50, ErrorMessage = "Soyad en fazla 50 karakter olabilir.")]
        public string Soyad { get; set; }

        [Required(ErrorMessage = "E-posta alanı zorunludur.")]
        [EmailAddress(ErrorMessage = "Geçerli bir e-posta adresi giriniz.")]
        [StringLength(100, ErrorMessage = "E-posta en fazla 100 karakter olabilir.")]
        public string Email { get; set; }

        [Required(ErrorMessage = "Şifre alanı zorunludur.")]
        [StringLength(128, MinimumLength = 6, ErrorMessage = "Şifre en az 6, en fazla 128 karakter olabilir.")]
        public string Sifre { get; set; }

        [Required(ErrorMessage = "Kullanıcı tipi zorunludur.")]
        public string KullaniciTipi { get; set; } // "Doktor" veya "Hasta"

        public byte[] ProfilResim { get; set; }

        [Required(ErrorMessage = "Doğum tarihi zorunludur.")]
        [DataType(DataType.Date)]
        [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd}", ApplyFormatInEditMode = true)]
        public DateTime DogumTarihi { get; set; }

        [Required(ErrorMessage = "Cinsiyet zorunludur.")]
        public string Cinsiyet { get; set; } // "Erkek" veya "Kadın"

        [DataType(DataType.Date)]
        [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd}", ApplyFormatInEditMode = true)]
        public DateTime KayitTarihi { get; set; }

        // Navigation properties
        public virtual Doktor Doktor { get; set; }
        public virtual Hasta Hasta { get; set; }
    }

    // Doktor model sınıfı
    public class Doktor
    {
        public int DoktorID { get; set; }
        public int KullaniciID { get; set; }
        public string Uzmanlik { get; set; }

        // Navigation properties
        public virtual Kullanici Kullanici { get; set; }
        public virtual ICollection<HastaDr> HastaDoktorlar { get; set; }
        public virtual ICollection<Uyarilar> Uyarilar { get; set; }
    }

    // Hasta model sınıfı
    public class Hasta
    {
        public int HastaID { get; set; }
        public int KullaniciID { get; set; }
        public decimal? Boy { get; set; }
        public decimal? Kilo { get; set; }

        // BMI hesaplama (Boy metre cinsinden olmalı)
        public decimal? BMI
        {
            get
            {
                if (Boy.HasValue && Boy.Value > 0 && Kilo.HasValue && Kilo.Value > 0)
                {
                    // Boy santimetre cinsinden, metreye çeviriyoruz
                    decimal boyMetre = Boy.Value / 100;
                    return Kilo.Value / (boyMetre * boyMetre);
                }
                return null;
            }
        }

        // Navigation properties
        public virtual Kullanici Kullanici { get; set; }
        public virtual ICollection<HastaDr> HastaDoktorlar { get; set; }
        public virtual ICollection<KanSekeri> KanSekeriOlcumleri { get; set; }
        public virtual ICollection<HastaBelirtileri> Belirtiler { get; set; }
        public virtual ICollection<DiyetTakip> DiyetTakipleri { get; set; }
        public virtual ICollection<EgzersizTakip> EgzersizTakipleri { get; set; }
        public virtual ICollection<Insulin> InsulinUygulamalari { get; set; }
        public virtual ICollection<Uyarilar> Uyarilar { get; set; }
    }

    // Doktor-Hasta ilişkisi model sınıfı
    public class HastaDr
    {
        public int HastaDrID { get; set; }
        public int DoktorID { get; set; }
        public int HastaID { get; set; }
        public DateTime TanimlamaTarihi { get; set; }

        // Navigation properties
        public virtual Doktor Doktor { get; set; }
        public virtual Hasta Hasta { get; set; }
    }

    // Kan şekeri ölçüm model sınıfı
    public class KanSekeri
    {
        public int KanSekeriID { get; set; }
        public int HastaID { get; set; }

        [Required(ErrorMessage = "Ölçüm değeri zorunludur.")]
        [Range(0.1, 999.99, ErrorMessage = "Geçerli bir kan şekeri değeri giriniz.")]
        public decimal OlcumDegeri { get; set; }

        [Required(ErrorMessage = "Ölçüm tarihi zorunludur.")]
        [DataType(DataType.Date)]
        [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd}", ApplyFormatInEditMode = true)]
        public DateTime OlcumTarihi { get; set; }

        [Required(ErrorMessage = "Ölçüm saati zorunludur.")]
        [DataType(DataType.Time)]
        [DisplayFormat(DataFormatString = "{0:HH:mm}", ApplyFormatInEditMode = true)]
        public TimeSpan OlcumSaati { get; set; }

        [Required(ErrorMessage = "Ölçüm türü zorunludur.")]
        public string OlcumTuru { get; set; } // "Açlık", "Tokluk", "Gece" vb.

        // Navigation property
        public virtual Hasta Hasta { get; set; }
    }

    // Belirti model sınıfı
    public class Belirti
    {
        public int BelirtiID { get; set; }

        [Required(ErrorMessage = "Belirti adı zorunludur.")]
        [StringLength(100, ErrorMessage = "Belirti adı en fazla 100 karakter olabilir.")]
        public string BelirtiAdi { get; set; }

        // Navigation property
        public virtual ICollection<HastaBelirtileri> HastaBelirtileri { get; set; }
    }

    // Hasta belirti ilişkisi model sınıfı
    public class HastaBelirtileri
    {
        public int HastaBelirtiID { get; set; }
        public int HastaID { get; set; }
        public int BelirtiID { get; set; }

        [Required(ErrorMessage = "Tarih zorunludur.")]
        [DataType(DataType.Date)]
        [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd}", ApplyFormatInEditMode = true)]
        public DateTime Tarih { get; set; }

        [Required(ErrorMessage = "Şiddet derecesi zorunludur.")]
        [Range(1, 5, ErrorMessage = "Şiddet 1-5 arasında olmalıdır.")]
        public int Siddet { get; set; }

        // Navigation properties
        public virtual Hasta Hasta { get; set; }
        public virtual Belirti Belirti { get; set; }
    }

    // Diyet türü model sınıfı
    public class DiyetTuru
    {
        public int DiyetTuruID { get; set; }

        [Required(ErrorMessage = "Diyet türü adı zorunludur.")]
        [StringLength(50, ErrorMessage = "Diyet türü adı en fazla 50 karakter olabilir.")]
        public string TurAdi { get; set; }

        [StringLength(500, ErrorMessage = "Açıklama en fazla 500 karakter olabilir.")]
        public string Aciklama { get; set; }

        // Navigation property
        public virtual ICollection<DiyetTakip> DiyetTakipleri { get; set; }
    }

    // Egzersiz türü model sınıfı
    public class EgzersizTuru
    {
        public int EgzersizTuruID { get; set; }

        [Required(ErrorMessage = "Egzersiz türü adı zorunludur.")]
        [StringLength(50, ErrorMessage = "Egzersiz türü adı en fazla 50 karakter olabilir.")]
        public string TurAdi { get; set; }

        [StringLength(500, ErrorMessage = "Açıklama en fazla 500 karakter olabilir.")]
        public string Aciklama { get; set; }

        // Navigation property
        public virtual ICollection<EgzersizTakip> EgzersizTakipleri { get; set; }
    }

    // Diyet takip model sınıfı
    public class DiyetTakip
    {
        public int DiyetID { get; set; }
        public int HastaID { get; set; }
        public int DiyetTuruID { get; set; }
        public bool UygulandiMi { get; set; }

        [Required(ErrorMessage = "Tarih zorunludur.")]
        [DataType(DataType.Date)]
        [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd}", ApplyFormatInEditMode = true)]
        public DateTime Tarih { get; set; }

        // Navigation properties
        public virtual Hasta Hasta { get; set; }
        public virtual DiyetTuru DiyetTuru { get; set; }
    }

    // Egzersiz takip model sınıfı
    public class EgzersizTakip
    {
        public int EgzersizID { get; set; }
        public int HastaID { get; set; }
        public int EgzersizTuruID { get; set; }
        public bool YapildiMi { get; set; }

        [Required(ErrorMessage = "Tarih zorunludur.")]
        [DataType(DataType.Date)]
        [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd}", ApplyFormatInEditMode = true)]
        public DateTime Tarih { get; set; }

        // Navigation properties
        public virtual Hasta Hasta { get; set; }
        public virtual EgzersizTuru EgzersizTuru { get; set; }
    }

    // İnsülin uygulama model sınıfı
    public class Insulin
    {
        public int InsulinID { get; set; }
        public int HastaID { get; set; }

        [Required(ErrorMessage = "Doz zorunludur.")]
        [Range(0.1, 99.9, ErrorMessage = "Geçerli bir insülin dozu giriniz.")]
        public decimal Doz { get; set; }

        [Required(ErrorMessage = "Uygulama tarihi zorunludur.")]
        [DataType(DataType.Date)]
        [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd}", ApplyFormatInEditMode = true)]
        public DateTime UygulamaTarihi { get; set; }

        [Required(ErrorMessage = "Uygulama saati zorunludur.")]
        [DataType(DataType.Time)]
        [DisplayFormat(DataFormatString = "{0:HH:mm}", ApplyFormatInEditMode = true)]
        public TimeSpan UygulamaSaati { get; set; }

        [Required(ErrorMessage = "Ortalama kan şekeri zorunludur.")]
        [Range(0.1, 999.99, ErrorMessage = "Geçerli bir kan şekeri değeri giriniz.")]
        public decimal OrtalamaKanSekeri { get; set; }

        // Navigation property
        public virtual Hasta Hasta { get; set; }
    }

    // Uyarılar model sınıfı
    public class Uyarilar
    {
        public int UyariID { get; set; }
        public int HastaID { get; set; }
        public int DoktorID { get; set; }

        [Required(ErrorMessage = "Uyarı türü zorunludur.")]
        [StringLength(50, ErrorMessage = "Uyarı türü en fazla 50 karakter olabilir.")]
        public string UyariTuru { get; set; }

        [Required(ErrorMessage = "Uyarı mesajı zorunludur.")]
        [StringLength(500, ErrorMessage = "Uyarı mesajı en fazla 500 karakter olabilir.")]
        public string UyariMesaji { get; set; }

        public DateTime UyariTarihi { get; set; }
        public bool Okundu { get; set; }

        // Navigation properties
        public virtual Hasta Hasta { get; set; }
        public virtual Doktor Doktor { get; set; }
    }

    // Giriş modeli
    public class LoginModel
    {
        [Required(ErrorMessage = "TC Kimlik numarası zorunludur.")]
        [RegularExpression(@"^[0-9]{11}$", ErrorMessage = "TC Kimlik numarası 11 haneli olmalıdır.")]
        public string TC { get; set; }

        [Required(ErrorMessage = "Şifre zorunludur.")]
        [DataType(DataType.Password)]
        public string Sifre { get; set; }
    }

    // Kullanıcı kayıt modeli
    public class RegisterModel
    {
        [Required(ErrorMessage = "TC Kimlik numarası zorunludur.")]
        [RegularExpression(@"^[0-9]{11}$", ErrorMessage = "TC Kimlik numarası 11 haneli olmalıdır.")]
        public string TC { get; set; }

        [Required(ErrorMessage = "Ad alanı zorunludur.")]
        [StringLength(50, ErrorMessage = "Ad en fazla 50 karakter olabilir.")]
        public string Ad { get; set; }

        [Required(ErrorMessage = "Soyad alanı zorunludur.")]
        [StringLength(50, ErrorMessage = "Soyad en fazla 50 karakter olabilir.")]
        public string Soyad { get; set; }

        [Required(ErrorMessage = "E-posta alanı zorunludur.")]
        [EmailAddress(ErrorMessage = "Geçerli bir e-posta adresi giriniz.")]
        [StringLength(100, ErrorMessage = "E-posta en fazla 100 karakter olabilir.")]
        public string Email { get; set; }

        [Required(ErrorMessage = "Şifre alanı zorunludur.")]
        [StringLength(128, MinimumLength = 6, ErrorMessage = "Şifre en az 6, en fazla 128 karakter olabilir.")]
        [DataType(DataType.Password)]
        public string Sifre { get; set; }

        [Required(ErrorMessage = "Şifre tekrarı zorunludur.")]
        [DataType(DataType.Password)]
        [Compare("Sifre", ErrorMessage = "Şifreler eşleşmiyor.")]
        public string SifreTekrar { get; set; }

        [Required(ErrorMessage = "Kullanıcı tipi zorunludur.")]
        public string KullaniciTipi { get; set; } // "Doktor" veya "Hasta"

        [Required(ErrorMessage = "Doğum tarihi zorunludur.")]
        [DataType(DataType.Date)]
        [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd}", ApplyFormatInEditMode = true)]
        public DateTime DogumTarihi { get; set; }

        [Required(ErrorMessage = "Cinsiyet zorunludur.")]
        public string Cinsiyet { get; set; } // "Erkek" veya "Kadın"

        // Doktor için
        public string Uzmanlik { get; set; }

        // Hasta için
        public decimal? Boy { get; set; }
        public decimal? Kilo { get; set; }
    }
    public class KullaniciViewModel
    {
        public string Ad { get; set; }
        public string Soyad { get; set; }
        public string Email { get; set; }
        public byte[] ProfilResim { get; set; }
        public bool HasProfilResim => ProfilResim != null;
    }

    public class PatientProfileViewModel
    {
        public string Ad { get; set; }
        public string Soyad { get; set; }
        public string Email { get; set; }
        public byte[] ProfilResim { get; set; }
        public decimal? Boy { get; set; }
        public decimal? Kilo { get; set; }
        public bool HasProfilResim => ProfilResim != null;
    }

}