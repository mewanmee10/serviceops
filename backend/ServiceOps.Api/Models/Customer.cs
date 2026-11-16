namespace ServiceOps.Api.Models;

public class Customer
{
    public int CustomerId { get; set; }

    public int UserId { get; set; }

    public string CustomerName { get; set; } = string.Empty;

    public string? AddressLine { get; set; }

    public string? City { get; set; }

    public string? Province { get; set; }

    public string? PostalCode { get; set; }

    public string? Notes { get; set; }

    public User User { get; set; } = null!;

    public ICollection<CustomerSite> CustomerSites { get; set; }
        = new List<CustomerSite>();

    public ICollection<ServiceRequest> ServiceRequests { get; set; }
        = new List<ServiceRequest>();

    public ICollection<CustomerFeedback> Feedbacks { get; set; }
        = new List<CustomerFeedback>();

    public ICollection<CustomerConfirmation> Confirmations { get; set; }
        = new List<CustomerConfirmation>();
}