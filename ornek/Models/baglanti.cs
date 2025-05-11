
using System.Data;
using System.Text;
using System;
using System.Data;
using System.Data.SqlClient;
using System.Security.Cryptography;
using System.Text;

namespace ornek.Models
{
    



   
        public class baglanti
        {
            private readonly string _connectionString;

            public baglanti(string connectionString)
            {
                _connectionString = connectionString;
            }

            // Veritabanı bağlantısı açma
            private SqlConnection GetConnection()
            {
                SqlConnection connection = new SqlConnection(_connectionString);
                if (connection.State != ConnectionState.Open)
                    connection.Open();
                return connection;
            }

            // Şifre hashleme metodu
            public string HashPassword(string password)
            {
                using (SHA256 sha256 = SHA256.Create())
                {
                    byte[] hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
                    StringBuilder builder = new StringBuilder();
                    for (int i = 0; i < hashedBytes.Length; i++)
                    {
                        builder.Append(hashedBytes[i].ToString("x2"));
                    }
                    return builder.ToString();
                }
            }

            // Kullanıcı girişi doğrulama
            public bool ValidateUser(string tcKimlik, string password, out string userType, out int userId)
            {
                userType = string.Empty;
                userId = 0;
                string hashedPassword = HashPassword(password);

                try
                {
                    using (SqlConnection connection = GetConnection())
                    {
                        string query = "SELECT KullaniciID, KullaniciTipi FROM Kullanici WHERE TC = @TC AND Sifre = @Sifre";
                        using (SqlCommand command = new SqlCommand(query, connection))
                        {
                            command.Parameters.AddWithValue("@TC", tcKimlik);
                            command.Parameters.AddWithValue("@Sifre", hashedPassword);

                            using (SqlDataReader reader = command.ExecuteReader())
                            {
                                if (reader.Read())
                                {
                                    userId = Convert.ToInt32(reader["KullaniciID"]);
                                    userType = reader["KullaniciTipi"].ToString();
                                    return true;
                                }
                            }
                        }
                    }
                    return false;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Hata: {ex.Message}");
                    return false;
                }
            }

            // Kullanıcı bilgilerini getirme
            public DataTable GetUserInfo(int userId)
            {
                DataTable dt = new DataTable();
                try
                {
                    using (SqlConnection connection = GetConnection())
                    {
                        string query = "SELECT * FROM Kullanici WHERE KullaniciID = @KullaniciID";
                        using (SqlCommand command = new SqlCommand(query, connection))
                        {
                            command.Parameters.AddWithValue("@KullaniciID", userId);
                            using (SqlDataAdapter adapter = new SqlDataAdapter(command))
                            {
                                adapter.Fill(dt);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Hata: {ex.Message}");
                }
                return dt;
            }

            // Genel sorgu çalıştırma metodu
            public DataTable ExecuteQuery(string query, SqlParameter[] parameters = null)
            {
                DataTable dt = new DataTable();
                try
                {
                    using (SqlConnection connection = GetConnection())
                    {
                        using (SqlCommand command = new SqlCommand(query, connection))
                        {
                            if (parameters != null)
                            {
                                command.Parameters.AddRange(parameters);
                            }
                            using (SqlDataAdapter adapter = new SqlDataAdapter(command))
                            {
                                adapter.Fill(dt);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Hata: {ex.Message}");
                }
                return dt;
            }

            // Genel komut çalıştırma metodu (INSERT, UPDATE, DELETE)
            public int ExecuteNonQuery(string query, SqlParameter[] parameters = null)
            {
                int result = 0;
                try
                {
                    using (SqlConnection connection = GetConnection())
                    {
                        using (SqlCommand command = new SqlCommand(query, connection))
                        {
                            if (parameters != null)
                            {
                                command.Parameters.AddRange(parameters);
                            }
                            result = command.ExecuteNonQuery();
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Hata: {ex.Message}");
                }
                return result;
            }

            // Tekil değer döndüren sorgu çalıştırma metodu
            public object ExecuteScalar(string query, SqlParameter[] parameters = null)
            {
                object result = null;
                try
                {
                    using (SqlConnection connection = GetConnection())
                    {
                        using (SqlCommand command = new SqlCommand(query, connection))
                        {
                            if (parameters != null)
                            {
                                command.Parameters.AddRange(parameters);
                            }
                            result = command.ExecuteScalar();
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Hata: {ex.Message}");
                }
                return result;
            }
        }
    

}
