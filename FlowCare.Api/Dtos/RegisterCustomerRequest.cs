using Microsoft.AspNetCore.Mvc;

namespace FlowCare.Api.Dtos;

public class RegisterCustomerRequest
{
    [FromForm(Name = "fullName")]
    public string FullName { get; set; } = default!;

    [FromForm(Name = "username")]
    public string Username { get; set; } = default!;

    [FromForm(Name = "password")]
    public string Password { get; set; } = default!;

    [FromForm(Name = "phone")]
    public string Phone { get; set; } = default!;

    [FromForm(Name = "idImage")]
    public IFormFile IdImage { get; set; } = default!;
}