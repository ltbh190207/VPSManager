// LicenseManager.cs - Version đã sửa lỗi hết hạn và cleanup code
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Newtonsoft.Json;

namespace VPSManager
{
    public class FirestoreDocument
    {
        public string name { get; set; }
        public Dictionary<string, FirestoreField> fields { get; set; }
        public string createTime { get; set; }
        public string updateTime { get; set; }
    }

    public class FirestoreField
    {
        public string stringValue { get; set; }
        public long? integerValue { get; set; }
        public object nullValue { get; set; }
    }

    public class KeyData
    {
        public long createdAt { get; set; }
        public long? expiresAt { get; set; }
        public long? usedAt { get; set; }
        public string usedBy { get; set; }
    }

    public class LicenseManager
    {
        private const string FIREBASE_API_KEY = "AIzaSyCe3V1JFEI9w3UoREuehqMx9gxtz-Yw1oc";
        private const string FIRESTORE_BASE_URL = "https://firestore.googleapis.com/v1/projects/vpsmanagerweb/databases/(default)/documents";

        private readonly string keyFilePath;
        private readonly HttpClient httpClient;
        private readonly string currentHWID;

        public string CurrentKey { get; private set; }

        public LicenseManager(string appDataPath, HttpClient sharedHttpClient, string hwid)
        {
            keyFilePath = Path.Combine(appDataPath, "license.dat");
            httpClient = sharedHttpClient;
            currentHWID = hwid;
        }

        // -----------------------------------------
        // CHECK LICENSE
        // -----------------------------------------
        public bool CheckLicense()
        {
            string savedKey = LoadSavedKey();

            if (string.IsNullOrEmpty(savedKey))
            {
                return ShowKeyInputDialog();
            }
            else
            {
                CurrentKey = savedKey;
                var result = VerifyKeyWithFirestore(savedKey).Result;

                if (!result)
                {
                    try { File.Delete(keyFilePath); } catch { }

                    MessageBox.Show(
                        "Key đã lưu không hợp lệ hoặc đã hết hạn.\nVui lòng nhập key mới.",
                        "Key không hợp lệ",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning
                    );

                    return ShowKeyInputDialog();
                }

                return true;
            }
        }

        // -----------------------------------------
        // LOAD & SAVE LOCAL KEY
        // -----------------------------------------
        private string LoadSavedKey()
        {
            try
            {
                if (File.Exists(keyFilePath))
                {
                    string encrypted = File.ReadAllText(keyFilePath);
                    return DecryptString(encrypted);
                }
            }
            catch { }

            return null;
        }

        private void SaveKey(string key)
        {
            try
            {
                File.WriteAllText(keyFilePath, EncryptString(key));
            }
            catch { }
        }

        // -----------------------------------------
        // INPUT DIALOG
        // -----------------------------------------
        private bool ShowKeyInputDialog()
        {
            Form keyForm = new Form
            {
                Text = "Xác thực License Key",
                Size = new Size(450, 200),
                StartPosition = FormStartPosition.CenterScreen,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false,
                MinimizeBox = false,
                BackColor = Color.FromArgb(30, 30, 30)
            };

            Label lblInfo = new Label
            {
                Text = "Vui lòng nhập License Key:",
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10f),
                Location = new Point(20, 20),
                AutoSize = true
            };

            Label lblHWID = new Label
            {
                Text = "Hardware ID: " + currentHWID,
                ForeColor = Color.Gray,
                Font = new Font("Segoe UI", 8f),
                Location = new Point(20, 50),
                AutoSize = true
            };

            TextBox txtKey = new TextBox
            {
                Location = new Point(20, 80),
                Size = new Size(390, 25),
                Font = new Font("Segoe UI", 10f),
                BackColor = Color.FromArgb(50, 50, 55),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };

            Button btnVerify = new Button
            {
                Text = "Xác thực",
                Location = new Point(250, 120),
                Size = new Size(80, 30),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(0, 122, 204),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnVerify.FlatAppearance.BorderSize = 0;

            Button btnCancel = new Button
            {
                Text = "Hủy",
                Location = new Point(340, 120),
                Size = new Size(70, 30),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(232, 17, 35),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnCancel.FlatAppearance.BorderSize = 0;

            bool keyVerified = false;

            // Verify click
            btnVerify.Click += async (s, e) =>
            {
                string key = txtKey.Text.Trim();

                if (string.IsNullOrEmpty(key))
                {
                    MessageBox.Show("Vui lòng nhập key!", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                btnVerify.Enabled = false;
                btnVerify.Text = "Đang kiểm tra...";

                bool valid = await VerifyKeyWithFirestore(key);

                if (valid)
                {
                    CurrentKey = key;
                    SaveKey(key);
                    keyVerified = true;

                    MessageBox.Show("Key hợp lệ! Chào mừng bạn.", "Thành công", MessageBoxButtons.OK, MessageBoxIcon.Information);

                    keyForm.DialogResult = DialogResult.OK;
                    keyForm.Close();
                }
                else
                {
                    MessageBox.Show("Key không hợp lệ hoặc đã hết hạn!", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    btnVerify.Enabled = true;
                    btnVerify.Text = "Xác thực";
                }
            };

            btnCancel.Click += (s, e) =>
            {
                keyForm.DialogResult = DialogResult.Cancel;
                keyForm.Close();
            };

            keyForm.Controls.AddRange(new Control[] { lblInfo, lblHWID, txtKey, btnVerify, btnCancel });

            return keyForm.ShowDialog() == DialogResult.OK && keyVerified;
        }

        // -----------------------------------------
        // VERIFY LICENSE
        // -----------------------------------------
        private async Task<bool> VerifyKeyWithFirestore(string key)
        {
            try
            {
                string url = $"{FIRESTORE_BASE_URL}/licenses/{key}?key={FIREBASE_API_KEY}";
                var response = await httpClient.GetAsync(url);
                string jsonResponse = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                    return false;

                var firestoreDoc = JsonConvert.DeserializeObject<FirestoreDocument>(jsonResponse);
                if (firestoreDoc?.fields == null)
                    return false;

                KeyData keyData = ParseKeyData(firestoreDoc);

                // 1) Check expiration (ĐÃ SỬA)
                if (keyData.expiresAt.HasValue)
                {
                    DateTime expiry = DateTimeOffset
                        .FromUnixTimeMilliseconds(keyData.expiresAt.Value)
                        .UtcDateTime
                        .ToLocalTime();
                      

                    if (DateTime.Now > expiry)
                    {
                        MessageBox.Show("Key đã hết hạn!", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return false;
                    }
                }

                // 2) Key chưa sử dụng → gán cho máy này
                if (string.IsNullOrEmpty(keyData.usedBy))
                {
                    return await UpdateKeyUsageFirestore(key, currentHWID);
                }

                // 3) Key đã sử dụng nhưng đúng máy
                if (keyData.usedBy == currentHWID)
                {
                    return true;
                }

                // 4) Sai máy
                MessageBox.Show("Key đã được dùng trên máy khác!", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Lỗi xác thực key: " + ex.Message, "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
        }

        private KeyData ParseKeyData(FirestoreDocument doc)
        {
            var fields = doc.fields;

            KeyData keyData = new KeyData
            {
                createdAt = fields.ContainsKey("createdAt") ?
                    (fields["createdAt"].integerValue ?? long.Parse(fields["createdAt"].stringValue)) : 0,

                expiresAt = fields.ContainsKey("expiresAt") ?
                    (fields["expiresAt"].integerValue ?? long.Parse(fields["expiresAt"].stringValue)) : null,

                usedAt = fields.ContainsKey("usedAt") && fields["usedAt"].integerValue.HasValue ?
                    fields["usedAt"].integerValue.Value : null,

                usedBy = fields.ContainsKey("usedBy") ? fields["usedBy"].stringValue : null
            };

            return keyData;
        }

        // -----------------------------------------
        // UPDATE USAGE
        // -----------------------------------------
        private async Task<bool> UpdateKeyUsageFirestore(string key, string hwid)
        {
            try
            {
                string url =
                    $"{FIRESTORE_BASE_URL}/licenses/{key}?updateMask.fieldPaths=usedBy&updateMask.fieldPaths=usedAt&key={FIREBASE_API_KEY}";

                var updateDoc = new
                {
                    fields = new
                    {
                        usedBy = new { stringValue = hwid },
                        usedAt = new { integerValue = DateTimeOffset.Now.ToUnixTimeMilliseconds() }
                    }
                };

                string jsonData = JsonConvert.SerializeObject(updateDoc);
                var content = new StringContent(jsonData, Encoding.UTF8, "application/json");

                var request = new HttpRequestMessage(new HttpMethod("PATCH"), url)
                {
                    Content = content
                };

                var response = await httpClient.SendAsync(request);
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        // -----------------------------------------
        // ENCRYPT / DECRYPT
        // -----------------------------------------
        private string EncryptString(string plainText)
        {
            byte[] key = Encoding.UTF8.GetBytes("VPSManagerKey123");
            byte[] iv = Encoding.UTF8.GetBytes("VPSManagerIV1234");

            using (Aes aes = Aes.Create())
            {
                aes.Key = key;
                aes.IV = iv;

                using (MemoryStream ms = new MemoryStream())
                using (CryptoStream cs = new CryptoStream(ms, aes.CreateEncryptor(), CryptoStreamMode.Write))
                {
                    byte[] bytes = Encoding.UTF8.GetBytes(plainText);
                    cs.Write(bytes, 0, bytes.Length);
                    cs.FlushFinalBlock();
                    return Convert.ToBase64String(ms.ToArray());
                }
            }
        }

        private string DecryptString(string cipherText)
        {
            byte[] key = Encoding.UTF8.GetBytes("VPSManagerKey123");
            byte[] iv = Encoding.UTF8.GetBytes("VPSManagerIV1234");

            using (Aes aes = Aes.Create())
            {
                aes.Key = key;
                aes.IV = iv;

                using (MemoryStream ms = new MemoryStream(Convert.FromBase64String(cipherText)))
                using (CryptoStream cs = new CryptoStream(ms, aes.CreateDecryptor(), CryptoStreamMode.Read))
                using (StreamReader reader = new StreamReader(cs))
                {
                    return reader.ReadToEnd();
                }
            }
        }

        // -----------------------------------------
        // LICENSE INFO
        // -----------------------------------------
        public void ShowLicenseInfo()
        {
            string expiryInfo = "Không giới hạn";

            Task.Run(async () =>
            {
                try
                {
                    string url = $"{FIRESTORE_BASE_URL}/licenses/{CurrentKey}?key={FIREBASE_API_KEY}";
                    var response = await httpClient.GetAsync(url);

                    if (response.IsSuccessStatusCode)
                    {
                        string jsonResponse = await response.Content.ReadAsStringAsync();
                        var doc = JsonConvert.DeserializeObject<FirestoreDocument>(jsonResponse);

                        if (doc?.fields != null && doc.fields.ContainsKey("expiresAt") &&
                            doc.fields["expiresAt"].integerValue.HasValue)
                        {
                            var expiry = DateTimeOffset
                                .FromUnixTimeMilliseconds(doc.fields["expiresAt"].integerValue.Value)
                                .UtcDateTime
                              
                                .ToLocalTime();
                              

                            expiryInfo = expiry.ToString("dd/MM/yyyy HH:mm");
                        }
                    }
                }
                catch { }

                Form.ActiveForm?.Invoke((Action)(() =>
                {
                    string info =
                        $"Thông tin License:\n\n" +
                        $"License Key: {CurrentKey}\n" +
                        $"Hardware ID: {currentHWID}\n" +
                        $"Hạn sử dụng: {expiryInfo}\n\n" +
                        $"Trạng thái: Đang hoạt động";

                    MessageBox.Show(info, "Thông tin License", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }));
            });
        }
    }
}
