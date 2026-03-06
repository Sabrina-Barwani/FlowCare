using Microsoft.AspNetCore.Http;

namespace FlowCare.Api.DTOs
{
    // Customer booking request with optional attachment
    public class BookAppointmentRequest
    {
        public int SlotId { get; set; }
        public IFormFile? Attachment { get; set; }
    }
}