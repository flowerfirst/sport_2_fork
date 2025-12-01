using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using oculus_sport.Models;
using System.Diagnostics;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace oculus_sport.Services.Storage
{
    public class FirebaseDataService
    {
        private readonly HttpClient _httpClient = new HttpClient();
        private readonly string _projectId = "oculus-sport";

        // ----------- save user sign up info into firestore using REST API
        public async Task SaveUserToFirestoreAsync(User user, string idToken)
        {
            var url = $"https://firestore.googleapis.com/v1/projects/{_projectId}/databases/(default)/documents/users/{user.Id}";

            var payload = new
            {
                fields = new
                {
                    name = new { stringValue = user.Name },
                    email = new { stringValue = user.Email },
                    studentId = new { stringValue = user.StudentId }
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