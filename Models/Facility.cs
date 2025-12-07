

namespace oculus_sport.Models
{
    public class Facility
    {
        public string FacilityId { get; set; }
        public string FacilityName { get; set; }
        public string Location { get; set; }
        public string ImageUrl { get; set; }
        public decimal Price { get; set; }
        public double Rating { get; set; }
        public string Category { get; set; } = string.Empty;

        //[JsonPropertyName("isAvailable")]
        //public bool IsAvailable { get; set; }

        //public string IdToken { get; set; }
    }



}

