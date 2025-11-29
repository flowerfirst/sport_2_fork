using System;
using SQLite;

namespace oculus_sport.Models
{
    public class User
    {
        // [PrimaryKey] is necessary for the local database key.
        // This 'Id' is the same as the Firebase UID, ensuring consistency.
        [PrimaryKey]
        public string? Id { get; set; }

        public string? Email { get; set; }
        public string? Name { get; set; }

        // Partner added StudentId, which matches your requirements for the Profile page
        public string? StudentId { get; set; }
    }
}