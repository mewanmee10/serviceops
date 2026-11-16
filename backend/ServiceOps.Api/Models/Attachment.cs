namespace ServiceOps.Api.Models;

public class Attachment
{
    public int AttachmentId { get; set; }

    public int? ServiceRequestId { get; set; }

    public int? WorkOrderId { get; set; }

    public string FileName { get; set; } = string.Empty;

    public string FileType { get; set; } = string.Empty;

    public string FilePath { get; set; } = string.Empty;

    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

    public ServiceRequest? ServiceRequest { get; set; }

    public WorkOrder? WorkOrder { get; set; }
}