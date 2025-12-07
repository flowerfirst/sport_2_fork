using System;
using SQLite;

namespace oculus_sport.Models
{
    public class User
    {
        // [PrimaryKey] is necessary for the local database key.
        // This 'Id' is the same as the Firebase UID, ensuring consistency.
        [PrimaryKey]
        public string Id { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string StudentId { get; set; } = string.Empty;
    }
}