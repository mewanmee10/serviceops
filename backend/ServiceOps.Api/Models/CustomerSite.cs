namespace ServiceOps.Api.Models;

public class CustomerSite
{
    public int SiteId { get; set; }

    public int CustomerId { get; set; }

    public string SiteName { get; set; } = string.Empty;

    public string AddressLine { get; set; } = string.Empty;

    public string? City { get; set; }

    public string? Province { get; set; }

    public string? PostalCode { get; set; }

    public string? LocationDetails { get; set; }

    public string? MapsUrl { get; set; }

    public decimal? Latitude { get; set; }

    public decimal? Longitude { get; set; }

    public Customer Customer { get; set; } = null!;

    public ICollection<Asset> Assets { get; set; } = new List<Asset>();

    public ICollection<ServiceRequest> ServiceRequests { get; set; }
        = new List<ServiceRequest>();
}