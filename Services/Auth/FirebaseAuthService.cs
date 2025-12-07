using Microsoft.Maui.Storage;
using oculus_sport.Models;
using oculus_sport.Services.Storage;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace oculus_sport.Services.Auth
{
    public class FirebaseAuthService : IAuthService
    {
        private readonly HttpClient _httpClient;
        private const string ApiKey = "AIzaSyCYLKCEnZv33cviHuNRy4Go8IZVWcu-0aI";
        private User? _currentUser;
        private readonly FirebaseDataService _dataService;

        public FirebaseAuthService(HttpClient httpClient)
        {
            _httpClient = httpClient;
            _dataService = new FirebaseDataService();
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

            var authResponse = JsonSerializer.Deserialize<FirebaseAuthResponse>(
                result,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
            );

            if (!response.IsSuccessStatusCode || !string.IsNullOrEmpty(authResponse?.Error?.Message))
            {
                throw new Exception($"Login failed: {authResponse?.Error?.Message ?? "Unknown error"}");
            }

            // Save tokens
            await SecureStorage.SetAsync("idToken", authResponse.IdToken);
            await SecureStorage.SetAsync("refreshToken", authResponse.RefreshToken);

            // 3. FETCH FULL PROFILE
            var fullProfile = await _dataService.GetUserProfileAsync(authResponse.LocalId);

            if (fullProfile != null)
            {
                _currentUser = fullProfile;
            }
            else
            {
                _currentUser = new User { Id = authResponse.LocalId, Email = authResponse.Email, Name = "Guest" };
            }

            return _currentUser;
        }

        // SIGN UP
        public async Task<User> SignUpAsync(string email, string password, string name, string studentId, string username)
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
                Username = username // Set Username
            };

            // Save to Firestore
            await _dataService.SaveUserToFirestoreAsync(_currentUser, authResponse.IdToken);

            await SecureStorage.SetAsync("idToken", authResponse.IdToken);
            await SecureStorage.SetAsync("refreshToken", authResponse.RefreshToken);

            return _currentUser!;
        }

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

            var json = JsonSerializer.Deserialize<JsonElement>(result);
            var newIdToken = json.GetProperty("id_token").GetString();

            if (!string.IsNullOrWhiteSpace(newIdToken))
                await SecureStorage.SetAsync("idToken", newIdToken);

            return newIdToken;
        }

        public async Task LogoutAsync()
        {
            _currentUser = null;
            await SecureStorage.SetAsync("idToken", string.Empty);
            await SecureStorage.SetAsync("refreshToken", string.Empty);
        }

        public User? GetCurrentUser() => _currentUser;
    }
}