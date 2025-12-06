using oculus_sport.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection.Metadata;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Net;
using System.Globalization;

namespace oculus_sport.Services.Storage
{
    public class FirebaseDataService
    {
        private readonly HttpClient _httpClient;
        private readonly string _projectId = "oculus-sport";
        private const string FacilitiesCollection = "facility";


        //------------ sync login + profile
        private const string UsersCollection = "users";
        public FirebaseDataService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }
        //-------------- sync user login w firestore
        private static string GetStringField(JsonElement fields, string name)
        {
            if (fields.ValueKind != JsonValueKind.Object) return string.Empty;
            if (!fields.TryGetProperty(name, out var field)) return string.Empty;
            if (field.ValueKind != JsonValueKind.Object) return string.Empty;
            if (!field.TryGetProperty("stringValue", out var value)) return string.Empty;
            return value.GetString() ?? string.Empty;
        }

        public async Task<User?> GetUserFromFirestore(string uid, string idToken)
        {
            var url = $"https://firestore.googleapis.com/v1/projects/{_projectId}/databases/(default)/documents/{UsersCollection}/{uid}";

            var req = new HttpRequestMessage(HttpMethod.Get, url);
            Debug.WriteLine($"[DEBUG] idToken: {idToken}");

            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", idToken);

            var res = await _httpClient.SendAsync(req);

            //---handle not-found or unauthorized gracefully
            if (res.StatusCode == System.Net.HttpStatusCode.NotFound ||
                res.StatusCode == System.Net.HttpStatusCode.Unauthorized ||
                res.StatusCode == System.Net.HttpStatusCode.Forbidden)
            {
                return null;
            }

            //---throw for other non-success to surface unexpected errors
            res.EnsureSuccessStatusCode();

            var json = await res.Content.ReadAsStringAsync();
            var doc = JsonSerializer.Deserialize<JsonElement>(json);

            //--firestore doc should have fields
            if (!doc.TryGetProperty("fields", out var fields) || fields.ValueKind != JsonValueKind.Object)
                return null;

            var name = GetStringField(fields, "name");
            var email = GetStringField(fields, "email");
            var studentId = GetStringField(fields, "studentId");
            var phoneNumber = GetStringField(fields, "phoneNumber");

            return new User
            {
                Id = uid,
                Name = name,
                Email = email,
                StudentId = studentId,
                PhoneNumber = phoneNumber
            };
        }

        // ----------- 1. save user sign up info into firestore using REST API
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
                    phoneNumber = new {stringValue = user.PhoneNumber}
                }
            };

            var json = JsonSerializer.Serialize(payload);

            var request = new HttpRequestMessage(HttpMethod.Patch, url)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };

            request.Headers.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", idToken);

            var response = await _httpClient.SendAsync(request);
            var result = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                throw new Exception($"Firestore save failed: {result}");
        }

        // --------- 2. get facility from firestore

        public async Task ValidateFacilityCollectionAsync(string idToken)
        {
            Debug.WriteLine($"[DEBUG] idToken: {idToken}");

            var url = $"https://firestore.googleapis.com/v1/projects/{_projectId}/databases/(default)/documents/{FacilitiesCollection}";

            var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", idToken);

            var res = await _httpClient.SendAsync(req);
            Debug.WriteLine($"[DEBUG] Firestore status: {res.StatusCode}");

            var json = await res.Content.ReadAsStringAsync();
            Debug.WriteLine($"[DEBUG] Raw facility collection response: {json}");

            if (!res.IsSuccessStatusCode)
            {
                Debug.WriteLine($"[ERROR] Facility collection fetch failed: {json}");
                return;
            }

            var doc = JsonSerializer.Deserialize<JsonElement>(json);

            if (!doc.TryGetProperty("documents", out var documents))
            {
                Debug.WriteLine("[DEBUG] No documents property found in facility collection response.");
                return;
            }

            foreach (var d in documents.EnumerateArray())
            {
                var name = d.GetProperty("name").GetString();
                var docId = name?.Split('/').Last();
                Debug.WriteLine($"[DEBUG] Found facility document ID: {docId}");
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
        private decimal ParsePrice(JsonElement fields, string fieldName)
        {
            if (fields.TryGetProperty(fieldName, out var field))
            {
                if (field.TryGetProperty("integerValue", out var intVal))
                    return decimal.Parse(intVal.GetString());
                if (field.TryGetProperty("doubleValue", out var dblVal))
                    return decimal.Parse(dblVal.GetString(), System.Globalization.CultureInfo.InvariantCulture);
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


        // IDatabaseService implementation using Firestore
        //public class FirebaseDataService : IDatabaseService
        //{
        //    // Backing field left null until first use
        //    private IFirebaseFirestore? _firestoreClient;

        //    // Lazy accessor — resolves the plugin at first use (avoids DI-time exception).
        //    private IFirebaseFirestore FirestoreClient
        //    {
        //        get
        //        {
        //            if (_firestoreClient != null)
        //                return _firestoreClient;

        //            // Try to resolve the platform implementation.
        //            _firestoreClient = CrossFirebaseFirestore.Current;

        //            if (_firestoreClient == null)
        //                throw new InvalidOperationException("Firestore plugin not available on this platform.");

        //            return _firestoreClient;
        //        }
        //    }

        //public FirebaseDataService()
        //{
        //    // Do not access CrossFirebaseFirestore.Current here to avoid NotImplementedException
        //    // during DI/container construction. Access happens lazily via FirestoreClient.
        //}

        //private static string GetCollectionName<T>() where T : class
        //{
        //    return $"{typeof(T).Name.ToLower()}s";
        //}

        //public async Task<T?> GetItemAsync<T>(string id) where T : class
        //{
        //    try
        //    {
        //        var collectionName = GetCollectionName<T>();

        //        var snapshot = await FirestoreClient
        //            .GetCollection(collectionName)
        //            .GetDocument(id)
        //            .GetDocumentSnapshotAsync<T>();

        //        if (snapshot.Data != null)
        //            return snapshot.Data;

        //        return null;
        //    }
        //    catch (Exception ex)
        //    {
        //        Debug.WriteLine($"Firestore GetItemAsync error: {ex.Message}");
        //        return null;
        //    }
        //}

        //public async Task<IEnumerable<T>> GetItemsAsync<T>() where T : class
        //{
        //    try
        //    {
        //        var collectionName = GetCollectionName<T>();

        //        var querySnapshot = await FirestoreClient
        //            .GetCollection(collectionName)
        //            .GetDocumentsAsync<T>();

        //        return querySnapshot.Documents
        //            .Where(d => d.Data != null)
        //            .Select(d => d.Data)!;
        //    }
        //    catch (Exception ex)
        //    {
        //        Debug.WriteLine($"Firestore GetItemsAsync error: {ex.Message}");
        //        return Enumerable.Empty<T>();
        //    }
        //}

        //public async Task<bool> AddItemAsync<T>(T item) where T : class
        //{
        //    try
        //    {
        //        var collectionName = GetCollectionName<T>();
        //        await FirestoreClient
        //            .GetCollection(collectionName)
        //            .AddDocumentAsync(item);

        //        return true;
        //    }
        //    catch (Exception ex)
        //    {
        //        Debug.WriteLine($"[Firestore Error] AddItemAsync failed for {typeof(T).Name}: {ex.Message}");
        //        return false;
        //    }
        //}

        //public async Task<bool> UpdateItemAsync<T>(T item) where T : class
        //{
        //    var idProperty = typeof(T).GetProperty("Id");
        //    string id = idProperty?.GetValue(item)?.ToString() ?? string.Empty;

        //    if (string.IsNullOrEmpty(id))
        //    {
        //        Debug.WriteLine($"[Firestore Error] Cannot update {typeof(T).Name}: 'Id' property is required.");
        //        return false;
        //    }

        //    try
        //    {
        //        var collectionName = GetCollectionName<T>();

        //        await FirestoreClient
        //            .GetCollection(collectionName)
        //            .GetDocument(id)
        //            .SetDataAsync(item);

        //        return true;
        //    }
        //    catch (Exception ex)
        //    {
        //        Debug.WriteLine($"[Firestore Error] UpdateItemAsync failed for {typeof(T).Name} ID {id}: {ex.Message}");
        //        return false;
        //    }
        //}

        //public async Task<bool> DeleteItemAsync<T>(string id) where T : class
        //{
        //    try
        //    {
        //        var collectionName = GetCollectionName<T>();
        //        await FirestoreClient
        //            .GetCollection(collectionName)
        //            .GetDocument(id)
        //            .DeleteDocumentAsync();

        //        return true;
        //    }
        //    catch (Exception ex)
        //    {
        //        Debug.WriteLine($"[Firestore Error] DeleteItemAsync failed for {typeof(T).Name} ID {id}: {ex.Message}");
        //        return false;
        //    }
        //}

        //public async Task<User?> GetUserByFirebaseIdAsync(string userId)
        //{
        //    return await GetItemAsync<User>(userId);
        //}

        //public async Task SaveUserProfileAsync(User user)
        //{
        //    if (string.IsNullOrEmpty(user.Id))
        //        throw new ArgumentException("User ID (Firebase UID) must be set before saving the profile.");

        //    await UpdateItemAsync(user);
        //}
        //}
    }
}
