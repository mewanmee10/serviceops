namespace ServiceOps.Api.Models;

public class Technician
{
    public int TechnicianId { get; set; }

    public int UserId { get; set; }

    public string Skill { get; set; } = string.Empty;

    public bool Availability { get; set; } = true;

    public int CurrentWorkload { get; set; }

    public User User { get; set; } = null!;

    public ICollection<WorkOrder> WorkOrders { get; set; }
        = new List<WorkOrder>();
}