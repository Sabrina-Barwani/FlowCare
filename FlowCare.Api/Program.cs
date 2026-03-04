using FlowCare.Api.Auth;
using FlowCare.Api.Data;
using static FlowCare.Api.Entities.Enums;
using FlowCare.Api.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("basic", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "basic",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Description = "Basic Authentication header"
    });

    options.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "basic"
                }
            },
            new string[] {}
        }
    });
});
// Configure Entity Framework with PostgreSQL provider
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IPasswordHasher, PasswordHasher>();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();

builder.Services.AddAuthentication("Basic")
    .AddScheme<AuthenticationSchemeOptions, BasicAuthenticationHandler>("Basic", null);

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", p =>
    p.RequireRole(UserRole.Admin.ToString()));

    options.AddPolicy("ManagerOrAdmin", p =>
        p.RequireRole(UserRole.Admin.ToString(), UserRole.BranchManager.ToString()));

    options.AddPolicy("StaffOrAbove", p =>
        p.RequireRole(UserRole.Admin.ToString(), UserRole.BranchManager.ToString(), UserRole.Staff.ToString()));

    options.AddPolicy("CustomerOnly", p =>
        p.RequireRole(UserRole.Customer.ToString()));
});
builder.Services.AddSingleton<IAuthorizationHandler, SameBranchHandler>();
builder.Services.AddScoped<SlotService>();
builder.Services.AddScoped<CustomerService>();
builder.Services.AddScoped<IFileStorage, LocalFileStorage>();
var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthentication();

app.UseAuthorization();

app.MapControllers();
await DbSeeder.SeedAsync(app.Services, app.Configuration);
app.Run();
