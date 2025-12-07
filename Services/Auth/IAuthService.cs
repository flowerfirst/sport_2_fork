using System.Threading.Tasks;
using oculus_sport.Models;

namespace oculus_sport.Services.Auth
{
    public interface IAuthService
    {
        // Parameter name changed to 'input' to imply Email OR Username
        Task<User> LoginAsync(string input, string password);

        // Added the four required parameters (name, studentId) and return User.
        Task<User> SignUpAsync(string email, string password, string name, string studentId, string phoneNumber);

        Task LogoutAsync();

        // get current user - profile page
        User? GetCurrentUser();
        Task<string?> RefreshIdTokenAsync();

    }
}