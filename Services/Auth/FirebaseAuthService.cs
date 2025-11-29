using System;
using System.Threading.Tasks;
using Plugin.Firebase.Auth;
using oculus_sport.Models;
using oculus_sport.Services.Storage;

namespace oculus_sport.Services.Auth
{
    public class FirebaseAuthService : IAuthService
    {
        private readonly IDatabaseService _databaseService;
        private readonly IFirebaseAuth _auth;
        private User? _currentUser;
        private readonly IDisposable _authListenerHandle;

        public FirebaseAuthService(IDatabaseService databaseService)
        {
            _databaseService = databaseService ?? throw new ArgumentNullException(nameof(databaseService));

            // Note: This might throw if Firebase isn't init yet on platform level.
            // Ideally, wrap in try-catch or check initialization state.
            try
            {
                _auth = CrossFirebaseAuth.Current;
            }
            catch
            {
                _auth = null;
            }

            if (_auth != null)
            {
                _authListenerHandle = _auth.AddAuthStateListener(auth =>
                {
                    var fbUser = auth.CurrentUser;

                    if (fbUser != null)
                    {
                        _currentUser = new User
                        {
                            Id = fbUser.Uid,
                            Email = fbUser.Email,
                            Name = fbUser.DisplayName
                        };
                    }
                    else
                    {
                        _currentUser = null;
                    }
                });
            }
        }

        public async Task<User> LoginAsync(string email, string password)
        {
            if (_auth == null) throw new InvalidOperationException("Firebase Auth not initialized.");

            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
                throw new ArgumentException("Email and password must be provided.");

            try
            {
                var fbUser = await _auth.SignInWithEmailAndPasswordAsync(email, password);

                if (fbUser == null)
                    throw new Exception("Login succeeded but no Firebase user returned.");

                // Load profile from Firestore
                var profile = await _databaseService.GetUserByFirebaseIdAsync(fbUser.Uid);

                if (profile == null)
                {
                    // Fallback if profile missing in DB but auth succeeded
                    return new User { Id = fbUser.Uid, Email = fbUser.Email };
                }

                _currentUser = profile;
                return profile;
            }
            catch (Exception ex)
            {
                throw new Exception($"Login failed: {ex.Message}", ex);
            }
        }

        public async Task<User> SignUpAsync(string email, string password, string name, string studentId)
        {
            if (_auth == null) throw new InvalidOperationException("Firebase Auth not initialized.");

            if (string.IsNullOrWhiteSpace(email) ||
                string.IsNullOrWhiteSpace(password) ||
                string.IsNullOrWhiteSpace(name) ||
                string.IsNullOrWhiteSpace(studentId))
            {
                throw new ArgumentException("All fields must be provided.");
            }

            try
            {
                var fbUser = await _auth.CreateUserAsync(email, password);

                if (fbUser == null)
                    throw new Exception("Failed to create Firebase user.");

                var newUser = new User
                {
                    Id = fbUser.Uid,
                    Email = email,
                    Name = name,
                    StudentId = studentId
                };

                await _databaseService.SaveUserProfileAsync(newUser);

                _currentUser = newUser;
                return newUser;
            }
            catch (Exception ex)
            {
                throw new Exception($"Sign up failed: {ex.Message}", ex);
            }
        }

        public void Logout()
        {
            try
            {
                _auth?.SignOutAsync();
            }
            catch (Exception ex)
            {
                throw new Exception("Error signing out.", ex);
            }
            finally
            {
                _currentUser = null;
            }
        }

        public User? GetCurrentUser()
        {
            if (_currentUser != null)
                return _currentUser;

            if (_auth == null) return null;

            var fb = _auth.CurrentUser;

            if (fb != null)
            {
                return new User
                {
                    Id = fb.Uid,
                    Email = fb.Email,
                    Name = fb.DisplayName
                };
            }

            return null;
        }
    }
}