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

        public FirebaseAuthService(HttpClient httpClient)
        {
            _httpClient = httpClient;
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


        // --------- login user using email and passw
        public async Task<User> LoginAsync(string email, string password)
        {
            var url = $"https://identitytoolkit.googleapis.com/v1/accounts:signInWithPassword?key={ApiKey}";
            var payload = new { email, password, returnSecureToken = true };
            var json = JsonSerializer.Serialize(payload);

            var response = await _httpClient.PostAsync(url, new StringContent(json, Encoding.UTF8, "application/json"));
            var result = await response.Content.ReadAsStringAsync();

            Console.WriteLine("Firebase raw response:");
            Console.WriteLine(result);

            var authResponse = JsonSerializer.Deserialize<FirebaseAuthResponse>(
                result,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
            );

            if (!response.IsSuccessStatusCode || !string.IsNullOrEmpty(authResponse?.Error?.Message))
            {
                var message = authResponse?.Error?.Message ?? "Unknown error";
                throw new Exception($"Login failed: {message}");
            }

            if (string.IsNullOrWhiteSpace(authResponse?.IdToken))
                throw new Exception("Firebase did not return a valid idToken.");

            _currentUser = new User
            {
                Id = authResponse.LocalId,
                Email = authResponse.Email
            };

            await SecureStorage.SetAsync("idToken", authResponse.IdToken);
            await SecureStorage.SetAsync("refreshToken", authResponse.RefreshToken);

            Console.WriteLine($"Login successful for user: {authResponse.Email} (ID: {authResponse.LocalId})");

            return _currentUser!;
        }


        // ------------- sign up new user
        public async Task<User> SignUpAsync(string email, string password, string name, string studentId)
        {
            var url = $"https://identitytoolkit.googleapis.com/v1/accounts:signUp?key={ApiKey}";
            var payload = new { email, password, returnSecureToken = true };
            var json = JsonSerializer.Serialize(payload);

            var response = await _httpClient.PostAsync(url, new StringContent(json, Encoding.UTF8, "application/json"));
            var result = await response.Content.ReadAsStringAsync();

            Console.WriteLine("Firebase signup raw response:");
            Console.WriteLine(result);

            var authResponse = JsonSerializer.Deserialize<FirebaseAuthResponse>(
                result,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
            );

            if (!response.IsSuccessStatusCode || !string.IsNullOrEmpty(authResponse?.Error?.Message))
            {
                var message = authResponse?.Error?.Message ?? "Unknown error";

                Console.WriteLine($"Firebase signup error: {message}");

                throw new Exception(message switch
                {
                    "EMAIL_EXISTS" => "This email is already registered.",
                    "INVALID_EMAIL" => "Invalid email format.",
                    string s when s.Contains("WEAK_PASSWORD") => "Password must be at least 6 characters.",
                    _ => $"Signup failed: {message}"
                });
            }

            if (string.IsNullOrWhiteSpace(authResponse?.IdToken))
                throw new Exception("Firebase did not return a valid idToken.");

            _currentUser = new User
            {
                Id = authResponse.LocalId,
                Email = authResponse.Email,
                Name = name,
                StudentId = studentId
            };

            //------------------- Save profile info into Firestore (CONNECT TO FIREBASEDATABASERVICE.CS)
            var dataService = new FirebaseDataService();
            await dataService.SaveUserToFirestoreAsync(_currentUser, authResponse.IdToken);

            await SecureStorage.SetAsync("idToken", authResponse.IdToken);
            await SecureStorage.SetAsync("refreshToken", authResponse.RefreshToken);

            return _currentUser!;
        }


        // ---------------- Refresh token
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

        // ------------------Log out user, clear stored tokens
        public async Task LogoutAsync()
        {
            _currentUser = null;
            await SecureStorage.SetAsync("idToken", string.Empty);
            await SecureStorage.SetAsync("refreshToken", string.Empty);
        }

        public User? GetCurrentUser() => _currentUser;
    }
}
