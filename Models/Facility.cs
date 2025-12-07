namespace oculus_sport.Models;

public class Facility
{
    public string Name { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = "badminton_court.png"; 
    public string Price { get; set; } = string.Empty;
    public double Rating { get; set; }
    public bool IsAvailable { get; set; }
    public string LocationMapUrl { get; set; } = string.Empty;
}