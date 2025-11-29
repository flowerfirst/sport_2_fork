using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using oculus_sport.Models;
using SQLite; // Requires the sqlite-net-pcl NuGet package

namespace oculus_sport.Services.Storage
{
    // Service to handle local, offline data storage using SQLite.
    public class LocalDataService
    {
        private const string DatabaseFilename = "OculusSports.db3";

        // Define the path where the database file will be stored on the device
        // FileSystem.AppDataDirectory resolves to a secure, writeable location per platform.
        public static string DatabasePath =>
            Path.Combine(FileSystem.AppDataDirectory, DatabaseFilename);

        private SQLiteAsyncConnection _database;

        public LocalDataService()
        {
            // The database connection is initialized lazily when first accessed.
        }

        // --- Database Initialization Logic ---

        /// <summary>
        /// Initializes the SQLite connection and creates the tables if they do not exist.
        /// </summary>
        private async Task Init()
        {
            if (_database != null)
                return;

            // Initialize the database connection, enabling read/write and creation.
            _database = new SQLiteAsyncConnection(DatabasePath,
                SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.Create | SQLiteOpenFlags.SharedCache);

            // Create tables for the models we want to store locally (User profile and Booking history)
            await _database.CreateTableAsync<User>();
            await _database.CreateTableAsync<Booking>();
        }

        // ----------------------------------------------------------------
        // CORE GENERIC CRUD IMPLEMENTATION FOR LOCAL STORAGE
        // ----------------------------------------------------------------

        /// <summary>
        /// Retrieves a single item from the local SQLite database by its ID (Primary Key).
        /// </summary>
        public async Task<T?> GetItemAsync<T>(string id) where T : new()
        {
            await Init();
            // Use FindAsync with the primary key (T must have a PrimaryKey attribute)
            return await _database.FindAsync<T>(id);
        }

        /// <summary>
        /// Saves or updates an item in the local SQLite database.
        /// If the PrimaryKey exists, it updates; otherwise, it inserts.
        /// </summary>
        public async Task<int> SaveItemAsync<T>(T item) where T : new()
        {
            await Init();
            // This method handles both Insert and Update based on PrimaryKey existence.
            return await _database.InsertOrReplaceAsync(item);
        }

        // ----------------------------------------------------------------
        // SPECIFIC OFFLINE DATA METHODS (Used by ViewModels)
        // ----------------------------------------------------------------

        /// <summary>
        /// Retrieves the locally cached user profile. Used on app startup when offline.
        /// </summary>
        public async Task<User?> GetLocalUserProfileAsync()
        {
            await Init();
            // Assuming only one user profile is stored locally at a time.
            return await _database.Table<User>().FirstOrDefaultAsync();
        }

        /// <summary>
        /// Saves the user profile to local storage. Used immediately after login/sync.
        /// </summary>
        public async Task SaveLocalUserProfileAsync(User user)
        {
            await Init();
            // Delete any existing user profiles first to ensure only one remains.
            await _database.DeleteAllAsync<User>();
            await _database.InsertAsync(user);
        }

        /// <summary>
        /// Retrieves the locally cached booking history for the current user.
        /// </summary>
        public async Task<List<Booking>> GetLocalBookingHistoryAsync(string userId)
        {
            await Init();
            // Query bookings filtered by the indexed UserId property
            return await _database.Table<Booking>()
                                  .Where(b => b.UserId == userId)
                                  .OrderByDescending(b => b.Date)
                                  .ToListAsync();
        }

        /// <summary>
        /// Saves a new booking to the local history cache. Used immediately after a successful Firebase booking sync.
        /// </summary>
        public async Task SaveBookingToLocalCacheAsync(Booking booking)
        {
            await Init();
            await _database.InsertOrReplaceAsync(booking);
        }
    }
}