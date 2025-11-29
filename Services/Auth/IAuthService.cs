using System.Threading.Tasks;
using oculus_sport.Models;

namespace oculus_sport.Services.Auth
{
    public interface IAuthService
    {
        Task<User> LoginAsync(string email, string password);

        // Added the four required parameters (name, studentId) and return User.
        Task<User> SignUpAsync(string email, string password, string name, string studentId);

        void Logout();

        User? GetCurrentUser();
    }
}