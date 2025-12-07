using System.Threading.Tasks;
using oculus_sport.Models;

namespace oculus_sport.Services.Auth
{
    public interface IAuthService
    {
        // Parameter name changed to 'input' to imply Email OR Username
        Task<User> LoginAsync(string input, string password);

        // Added username parameter
        Task<User> SignUpAsync(string email, string password, string name, string studentId, string username);

        Task LogoutAsync();
        User? GetCurrentUser();
        Task<string?> RefreshIdTokenAsync();
    }
}