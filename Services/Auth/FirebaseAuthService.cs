using Microsoft.Maui.Storage;
using oculus_sport.Models;
using oculus_sport.Services.Storage;
using System.Diagnostics;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace oculus_sport.Services.Auth
{
    public class FirebaseRefreshResponse
    {
        public string IdToken { get; set; } = string.Empty;
        public string RefreshToken { get; set; } = string.Empty;
        public string ExpiresIn { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
    }

    public class FirebaseAuthService : IAuthService
    {
        private readonly HttpClient _httpClient;
        private readonly FirebaseDataService _dataService;
        private const string ApiKey = "AIzaSyCYLKCEnZv33cviHuNRy4Go8IZVWcu-0aI";
        private User? _currentUser;
        private readonly FirebaseDataService _dataService;


        public FirebaseAuthService(HttpClient httpClient, FirebaseDataService dataService)
        {
            _httpClient = httpClient;
            _dataService = dataService;
        }

        private class FirebaseAuthResponse
        {
            public string LocalId { get; set; } = string.Empty;
            public string Email { get; set; } = string.Empty;
            public string IdToken { get; set; } = string.Empty;
            public string RefreshToken { get; set; } = string.Empty;
            public FirebaseError? Error { get; set; }
        }

        private class FirebaseError
        {
            public string Message { get; set; } = string.Empty;
        }

        // LOGIN
        public async Task<User> LoginAsync(string input, string password)
        {
            string emailToLogin = input;

            // 1. Check if input is a Username (missing '@')
            if (!input.Contains("@"))
            {
                var foundEmail = await _dataService.GetEmailFromUsernameAsync(input);
                if (string.IsNullOrEmpty(foundEmail))
                {
                    throw new Exception("Username not found.");
                }
                emailToLogin = foundEmail;
            }

            // 2. Standard Login
            var url = $"https://identitytoolkit.googleapis.com/v1/accounts:signInWithPassword?key={ApiKey}";
            var payload = new { email = emailToLogin, password, returnSecureToken = true };
            var json = JsonSerializer.Serialize(payload);

            var response = await _httpClient.PostAsync(url, new StringContent(json, Encoding.UTF8, "application/json"));
            var result = await response.Content.ReadAsStringAsync();

            //Console.WriteLine("Firebase raw response:");
            //Console.WriteLine(result);

            var authResponse = JsonSerializer.Deserialize<FirebaseAuthResponse>(result, new JsonSerializerOptions { PropertyNameCaseInsensitive = true } );

            if (!response.IsSuccessStatusCode || !string.IsNullOrEmpty(authResponse?.Error?.Message))
            {
                throw new Exception($"Login failed: {authResponse?.Error?.Message ?? "Unknown error"}");
            }

            if (string.IsNullOrWhiteSpace(authResponse?.IdToken))
                throw new Exception("Firebase did not return a valid idToken.");

            _currentUser = new User
            {
                Id = authResponse.LocalId,
                Email = authResponse.Email,
                IdToken = authResponse.IdToken
            };

            await SecureStorage.SetAsync("idToken", authResponse.IdToken);
            await SecureStorage.SetAsync("refreshToken", authResponse.RefreshToken);

            Preferences.Set("LastUserId", authResponse.LocalId);
            // Debug: check expiry right after login
            bool expired = IsTokenExpired(authResponse.IdToken);
            Debug.WriteLine($"[Login] Token expires at: {expired}");

            Console.WriteLine($"Login successful for user: {authResponse.Email} (ID: {authResponse.LocalId})");

            // --------- fetch profile from firestore
            var profile = await _dataService.GetUserFromFirestore(authResponse.LocalId, authResponse.IdToken);
            if(profile != null)
            {
                _currentUser.Name = profile.Name;
                _currentUser.Email = profile.Email;
                _currentUser.StudentId = profile.StudentId;
                _currentUser.PhoneNumber = profile.PhoneNumber;
            }

            // -------- sync homepage username with login

            return _currentUser!;
        }

            return _currentUser;
        }

        // ------------- sign up new user
        public async Task<User> SignUpAsync(string email, string password, string name, string studentId, string phoneNumber)
        {
            var url = $"https://identitytoolkit.googleapis.com/v1/accounts:signUp?key={ApiKey}";
            var payload = new { email, password, returnSecureToken = true };
            var json = JsonSerializer.Serialize(payload);

            var response = await _httpClient.PostAsync(url, new StringContent(json, Encoding.UTF8, "application/json"));
            var result = await response.Content.ReadAsStringAsync();

            var authResponse = JsonSerializer.Deserialize<FirebaseAuthResponse>(
                result,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
            );

            if (!response.IsSuccessStatusCode || !string.IsNullOrEmpty(authResponse?.Error?.Message))
            {
                throw new Exception($"Signup failed: {authResponse?.Error?.Message}");
            }

            _currentUser = new User
            {
                Id = authResponse.LocalId,
                Email = authResponse.Email,
                Name = name,
                StudentId = studentId,
                PhoneNumber = phoneNumber
            };

            //------------------- Save profile info into Firestore (CONNECT TO FIREBASEDATABASERVICE.CS)
            //var dataService = new FirebaseDataService(_httpClient); //updatedd
            await _dataService.SaveUserToFirestoreAsync(_currentUser, authResponse.IdToken);

            await SecureStorage.SetAsync("idToken", authResponse.IdToken);
            await SecureStorage.SetAsync("refreshToken", authResponse.RefreshToken);

            return _currentUser!;
        }


        // ---------------- Refresh token
        //public async Task<string?> RefreshIdTokenAsync()
        //{
        //    var refreshToken = await SecureStorage.GetAsync("refreshToken");
        //    if (string.IsNullOrWhiteSpace(refreshToken)) return null;

        //    var url = $"https://securetoken.googleapis.com/v1/token?key={ApiKey}";
        //    var content = new FormUrlEncodedContent(new[]
        //    {
        //        new KeyValuePair<string, string>("grant_type", "refresh_token"),
        //        new KeyValuePair<string, string>("refresh_token", refreshToken)
        //    });

        //    var response = await _httpClient.PostAsync(url, content);
        //    var result = await response.Content.ReadAsStringAsync();

        //    if (!response.IsSuccessStatusCode) return null;

        //    var json = JsonSerializer.Deserialize<JsonElement>(result);
        //    var newIdToken = json.GetProperty("id_token").GetString();

        //    if (!string.IsNullOrWhiteSpace(newIdToken))
        //        await SecureStorage.SetAsync("idToken", newIdToken);

        //    return newIdToken;
        //}

        public async Task<string?> RefreshIdTokenAsync()
        {
            var refreshToken = await SecureStorage.GetAsync("refreshToken");
            if (string.IsNullOrWhiteSpace(refreshToken)) return null;

            var url = $"https://securetoken.googleapis.com/v1/token?key={ApiKey}";
            var content = new FormUrlEncodedContent(new[]
            {
            new KeyValuePair<string, string>("grant_type", "refresh_token"),
            new KeyValuePair<string, string>("refresh_token", refreshToken)
        });

            var response = await _httpClient.PostAsync(url, content);
            var result = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode) return null;

            var refreshResponse = JsonSerializer.Deserialize<FirebaseRefreshResponse>(result,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (refreshResponse == null || string.IsNullOrWhiteSpace(refreshResponse.IdToken))
                return null;

            // Save updated tokens
            await SecureStorage.SetAsync("idToken", refreshResponse.IdToken);
            if (!string.IsNullOrWhiteSpace(refreshResponse.RefreshToken))
                await SecureStorage.SetAsync("refreshToken", refreshResponse.RefreshToken);

            return refreshResponse.IdToken;
        }

        private bool IsTokenExpired(string idToken)
        {
            var parts = idToken.Split('.');
            if (parts.Length != 3)
            {
                Debug.WriteLine("[TokenCheck] Invalid JWT format.");
                return true;
            }

            var payload = parts[1];
            var jsonBytes = Convert.FromBase64String(
                payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=')
            );
            var json = System.Text.Encoding.UTF8.GetString(jsonBytes);

            var expMatch = System.Text.RegularExpressions.Regex.Match(json, "\"exp\":(\\d+)");
            if (!expMatch.Success)
            {
                Debug.WriteLine("[TokenCheck] No exp claim found.");
                return true;
            }

            var expUnix = long.Parse(expMatch.Groups[1].Value);
            var expDate = DateTimeOffset.FromUnixTimeSeconds(expUnix);

            Debug.WriteLine($"[TokenCheck] Token expires at: {expDate:yyyy-MM-dd HH:mm:ss} UTC");
            Debug.WriteLine($"[TokenCheck] Current time: {DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
            Debug.WriteLine($"[TokenCheck] Token expired? {expDate < DateTimeOffset.UtcNow}");

            return expDate < DateTimeOffset.UtcNow;
        }

        public async Task LogoutAsync()
        {
            await Task.Delay(200);

            _currentUser = null;
            //await SecureStorage.SetAsync("idToken", string.Empty);
            //await SecureStorage.SetAsync("refreshToken", string.Empty);
            SecureStorage.Remove("idToken");
            SecureStorage.Remove("refreshToken");
            await Task.CompletedTask;
        }

        public User? GetCurrentUser() => _currentUser;
    }


}
