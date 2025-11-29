using System.Collections.Generic;
using System.Threading.Tasks;
using oculus_sport.Models;

namespace oculus_sport.Services.Storage
{
    public interface IDatabaseService
    {
        // Core Generic CRUD Methods (Constraints applied)
        Task<T?> GetItemAsync<T>(string id) where T : class;
        Task<IEnumerable<T>> GetItemsAsync<T>() where T : class;
        Task<bool> AddItemAsync<T>(T item) where T : class;
        Task<bool> UpdateItemAsync<T>(T item) where T : class;
        Task<bool> DeleteItemAsync<T>(string id) where T : class;

        // Specific Methods Required by FirebaseAuthService
        Task<User?> GetUserByFirebaseIdAsync(string userId);
        Task SaveUserProfileAsync(User user);
    }
}