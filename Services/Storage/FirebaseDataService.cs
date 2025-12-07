using System.Text;
using System.Text.Json;
using System.Net.Http.Headers;
using oculus_sport.Models;
using Microsoft.Maui.Storage;

namespace oculus_sport.Services.Storage
{
    public class FirebaseDataService
    {
        private readonly HttpClient _httpClient = new HttpClient();
        private readonly string _projectId = "oculus-sport";

        // --------------------------------------------------------------------
        // 1. SAVE USER (Used during Sign Up)
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
                    // SAVE USERNAME
                    username = new { stringValue = user.Username }
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
        // 2. FETCH FULL PROFILE (Fixes "Guest" Issue)
        // --------------------------------------------------------------------
        public async Task<User?> GetUserProfileAsync(string userId)
        {
            var url = $"https://firestore.googleapis.com/v1/projects/{_projectId}/databases/(default)/documents/users/{userId}";

            var token = await SecureStorage.GetAsync("idToken");
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            if (!string.IsNullOrEmpty(token)) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode) return null;

            var result = await response.Content.ReadAsStringAsync();
            using (JsonDocument doc = JsonDocument.Parse(result))
            {
                if (doc.RootElement.TryGetProperty("fields", out JsonElement fields))
                {
                    var user = new User { Id = userId };

                    if (fields.TryGetProperty("name", out JsonElement n))
                        user.Name = n.GetProperty("stringValue").GetString() ?? "";

                    if (fields.TryGetProperty("email", out JsonElement e))
                        user.Email = e.GetProperty("stringValue").GetString() ?? "";

                    if (fields.TryGetProperty("username", out JsonElement u))
                        user.Username = u.GetProperty("stringValue").GetString() ?? "";

                    if (fields.TryGetProperty("studentId", out JsonElement s))
                        user.StudentId = s.GetProperty("stringValue").GetString() ?? "";

                    return user;
                }
            }
            return null;
        }

        // --------------------------------------------------------------------
        // 3. LOOKUP EMAIL BY USERNAME (For Login)
        // --------------------------------------------------------------------
        public async Task<string?> GetEmailFromUsernameAsync(string username)
        {
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

            // We usually need auth for runQuery, but if open rules, might work without. 
            // Better to attach token if available, but usually used during login (no token yet).
            // This relies on Firestore Rules allowing public read or restricted read.

            var response = await _httpClient.PostAsync(url, content);
            if (!response.IsSuccessStatusCode) return null;

            var result = await response.Content.ReadAsStringAsync();

            using (JsonDocument doc = JsonDocument.Parse(result))
            {
                foreach (var element in doc.RootElement.EnumerateArray())
                {
                    if (element.TryGetProperty("document", out JsonElement document))
                    {
                        if (document.TryGetProperty("fields", out JsonElement fields))
                        {
                            if (fields.TryGetProperty("email", out JsonElement emailObj))
                            {
                                return emailObj.GetProperty("stringValue").GetString();
                            }
                        }
                    }
                }
            }
            return null;
        }

        // --------------------------------------------------------------------
        // 4. GET FACILITIES
        // --------------------------------------------------------------------
        public async Task<List<Facility>> GetFacilitiesFromFirestoreAsync()
        {
            var url = $"https://firestore.googleapis.com/v1/projects/{_projectId}/databases/(default)/documents/facility";

            var token = await SecureStorage.GetAsync("idToken");
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            if (!string.IsNullOrEmpty(token))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }

            var response = await _httpClient.SendAsync(request);
            var result = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"[Firestore Error] {response.StatusCode}: {result}");
                return new List<Facility>();
            }

            var facilities = new List<Facility>();
            using (JsonDocument doc = JsonDocument.Parse(result))
            {
                if (doc.RootElement.TryGetProperty("documents", out JsonElement documents))
                {
                    foreach (var docElement in documents.EnumerateArray())
                    {
                        if (docElement.TryGetProperty("fields", out JsonElement fields))
                        {
                            var facility = new Facility();

                            if (fields.TryGetProperty("facilityName", out JsonElement nameObj))
                                facility.Name = nameObj.GetProperty("stringValue").GetString() ?? "";

                            if (fields.TryGetProperty("location", out JsonElement locObj))
                                facility.Location = locObj.GetProperty("stringValue").GetString() ?? "";

                            if (fields.TryGetProperty("imageUrl", out JsonElement imgObj))
                                facility.ImageUrl = imgObj.GetProperty("stringValue").GetString() ?? "dotnet_bot.png";

                            if (fields.TryGetProperty("price", out JsonElement priceObj))
                            {
                                if (priceObj.TryGetProperty("integerValue", out JsonElement iVal))
                                    facility.Price = $"RM {iVal.GetString()}";
                                else if (priceObj.TryGetProperty("doubleValue", out JsonElement dVal))
                                    facility.Price = $"RM {dVal.GetDouble()}";
                                else if (priceObj.TryGetProperty("stringValue", out JsonElement sVal))
                                    facility.Price = $"RM {sVal.GetString()}";
                            }

                            if (fields.TryGetProperty("rating", out JsonElement ratingObj))
                            {
                                if (ratingObj.TryGetProperty("doubleValue", out JsonElement dVal))
                                    facility.Rating = dVal.GetDouble();
                                else if (ratingObj.TryGetProperty("integerValue", out JsonElement iVal))
                                    facility.Rating = double.Parse(iVal.GetString() ?? "0");
                            }

                            facilities.Add(facility);
                        }
                    }
                }
            }
            return facilities;
        }
    }
}