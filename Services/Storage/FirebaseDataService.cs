using oculus_sport.Models;
using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Maui.Storage; // Required for SecureStorage

namespace oculus_sport.Services.Storage
{
    public class FirebaseDataService
    {
        private readonly HttpClient _httpClient;
        private readonly string _projectId = "oculus-sport";
        private const string FacilitiesCollection = "facility";
        private const string UsersCollection = "users";

        public FirebaseDataService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        // --------------------------------------------------------------------
        // 1. HELPERS
        // --------------------------------------------------------------------
        private static string GetStringField(JsonElement fields, string name)
        {
            if (fields.ValueKind != JsonValueKind.Object) return string.Empty;
            if (!fields.TryGetProperty(name, out var field)) return string.Empty;
            if (field.ValueKind != JsonValueKind.Object) return string.Empty;
            if (!field.TryGetProperty("stringValue", out var value)) return string.Empty;
            return value.GetString() ?? string.Empty;
        }

        private decimal ParsePrice(JsonElement fields, string fieldName)
        {
            if (fields.TryGetProperty(fieldName, out var field))
            {
                if (field.TryGetProperty("integerValue", out var intVal))
                    return decimal.Parse(intVal.GetString());
                if (field.TryGetProperty("doubleValue", out var dblVal))
                    return decimal.Parse(dblVal.GetString(), System.Globalization.CultureInfo.InvariantCulture);
                if (field.TryGetProperty("stringValue", out var sVal))
                    return decimal.Parse(sVal.GetString(), System.Globalization.CultureInfo.InvariantCulture);
            }
            return 0;
        }

        private int ParseInt(JsonElement fields, string fieldName)
        {
            if (fields.TryGetProperty(fieldName, out var field) &&
                field.TryGetProperty("integerValue", out var value))
            {
                return int.Parse(value.GetString());
            }
            return 0;
        }

        // --------------------------------------------------------------------
        // 2. USERNAME LOOKUP (Restored from Backup)
        // --------------------------------------------------------------------
        public async Task<string> GetEmailFromUsernameAsync(string username)
        {
            if (string.IsNullOrWhiteSpace(username)) return string.Empty;

            var url = $"https://firestore.googleapis.com/v1/projects/{_projectId}/databases/(default)/documents:runQuery";

            var payload = new
            {
                structuredQuery = new
                {
                    from = new[] { new { collectionId = "users" } },
                    where = new
                    {
                        fieldFilter = new
                        {
                            field = new { fieldPath = "username" },
                            op = "EQUAL",
                            value = new { stringValue = username }
                        }
                    },
                    limit = 1
                }
            };

            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(url, content);

            if (!response.IsSuccessStatusCode)
            {
                Debug.WriteLine($"[Error] Username lookup failed: {response.StatusCode}");
                return string.Empty;
            }

            var result = await response.Content.ReadAsStringAsync();
            using (JsonDocument doc = JsonDocument.Parse(result))
            {
                foreach (var element in doc.RootElement.EnumerateArray())
                {
                    if (element.TryGetProperty("document", out JsonElement document))
                    {
                        if (document.TryGetProperty("fields", out JsonElement fields))
                        {
                            return GetStringField(fields, "email");
                        }
                    }
                }
            }
            return string.Empty;
        }

        // --------------------------------------------------------------------
        // 3. GET USER PROFILE (Restored from Backup + Merged Sync Logic)
        // --------------------------------------------------------------------
        public async Task<User?> GetUserFromFirestore(string uid, string idToken)
        {
            var url = $"https://firestore.googleapis.com/v1/projects/{_projectId}/databases/(default)/documents/{UsersCollection}/{uid}";

            var req = new HttpRequestMessage(HttpMethod.Get, url);

            // Only add token if provided (robust check)
            if (!string.IsNullOrEmpty(idToken))
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", idToken);

            var res = await _httpClient.SendAsync(req);

            if (res.StatusCode == System.Net.HttpStatusCode.NotFound ||
                res.StatusCode == System.Net.HttpStatusCode.Unauthorized ||
                res.StatusCode == System.Net.HttpStatusCode.Forbidden)
            {
                return null;
            }

            res.EnsureSuccessStatusCode();

            var json = await res.Content.ReadAsStringAsync();
            var doc = JsonSerializer.Deserialize<JsonElement>(json);

            if (!doc.TryGetProperty("fields", out var fields) || fields.ValueKind != JsonValueKind.Object)
                return null;

            return new User
            {
                Id = uid,
                Name = GetStringField(fields, "name"),
                Email = GetStringField(fields, "email"),
                StudentId = GetStringField(fields, "studentId"),
                PhoneNumber = GetStringField(fields, "phoneNumber"),
                Username = GetStringField(fields, "username") // 🟢 Added back Username
            };
        }

        // --------------------------------------------------------------------
        // 4. SAVE USER (Restored from Backup + Added Phone Number)
        // --------------------------------------------------------------------
        public async Task SaveUserToFirestoreAsync(User user, string idToken)
        {
            var url = $"https://firestore.googleapis.com/v1/projects/{_projectId}/databases/(default)/documents/users/{user.Id}";

            var payload = new
            {
                fields = new
                {
                    name = new { stringValue = user.Name },
                    email = new { stringValue = user.Email },
                    studentId = new { stringValue = user.StudentId },
                    phoneNumber = new { stringValue = user.PhoneNumber }, // From other dev
                    username = new { stringValue = user.Username } // 🟢 Added back Username
                }
            };

            var json = JsonSerializer.Serialize(payload);
            var request = new HttpRequestMessage(HttpMethod.Patch, url)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };

            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", idToken);

            var response = await _httpClient.SendAsync(request);
            var result = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                throw new Exception($"Firestore save failed: {result}");
        }

        // --------------------------------------------------------------------
        // 5. FACILITY LOGIC (Preserved from Other Dev)
        // --------------------------------------------------------------------
        public async Task ValidateFacilityCollectionAsync(string idToken)
        {
            Debug.WriteLine($"[DEBUG] Validating facilities with token...");
            var url = $"https://firestore.googleapis.com/v1/projects/{_projectId}/databases/(default)/documents/{FacilitiesCollection}";

            var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", idToken);

            var res = await _httpClient.SendAsync(req);

            if (!res.IsSuccessStatusCode)
            {
                var error = await res.Content.ReadAsStringAsync();
                Debug.WriteLine($"[ERROR] Facility fetch failed: {error}");
            }
        }

        public async Task<List<Facility>> GetFacilitiesAsync(string idToken)
        {
            var url = $"https://firestore.googleapis.com/v1/projects/{_projectId}/databases/(default)/documents/{FacilitiesCollection}";
            var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", idToken);

            var res = await _httpClient.SendAsync(req);
            var json = await res.Content.ReadAsStringAsync();

            if (!res.IsSuccessStatusCode)
                throw new Exception($"Facility fetch failed: {json}");

            var doc = JsonSerializer.Deserialize<JsonElement>(json);
            var facilities = new List<Facility>();

            if (doc.TryGetProperty("documents", out var documents))
            {
                foreach (var d in documents.EnumerateArray())
                {
                    var fields = d.GetProperty("fields");

                    facilities.Add(new Facility
                    {
                        FacilityName = GetStringField(fields, "facilityName"),
                        Location = GetStringField(fields, "location"),
                        ImageUrl = GetStringField(fields, "imageUrl"),
                        Category = GetStringField(fields, "category"),
                        Price = ParsePrice(fields, "price"),
                        Rating = ParseInt(fields, "rating")
                    });
                }
            }

            return facilities;
        }
    }
}