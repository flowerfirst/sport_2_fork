using System.Threading.Tasks;
using oculus_sport.Models;

namespace oculus_sport.Services.Auth
{
    public interface IAuthService
    {
        Task<User> LoginAsync(string input, string password);

        Task<User> SignUpAsync(string email, string password, string name, string phoneNumber, string studentId, string username);

        Task LogoutAsync();
        User? GetCurrentUser();
        Task<string?> RefreshIdTokenAsync();
        Task<User?> GetCachedUserAsync();

    }
}