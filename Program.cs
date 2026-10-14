using System.Text;
using AISmartLearning.API.Data;
using AISmartLearning.API.Models;
using AISmartLearning.API.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c => {
    c.SwaggerDoc("v1", new() { Title = "AI Smart Learning API", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme { Name="Authorization", Type=Microsoft.OpenApi.Models.SecuritySchemeType.Http, Scheme="bearer", BearerFormat="JWT", In=Microsoft.OpenApi.Models.ParameterLocation.Header });
    c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement { { new Microsoft.OpenApi.Models.OpenApiSecurityScheme { Reference = new Microsoft.OpenApi.Models.OpenApiReference { Type=Microsoft.OpenApi.Models.ReferenceType.SecurityScheme, Id="Bearer" } }, Array.Empty<string>() } });
});
var cs = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("DefaultConnection is missing.");
builder.Services.AddDbContext<AppDbContext>(o => o.UseSqlServer(cs));
var key = builder.Configuration["Jwt:SigningKey"];
if (string.IsNullOrWhiteSpace(key) || key.Length < 32) throw new InvalidOperationException("Jwt:SigningKey must be at least 32 characters. Configure it using user-secrets.");
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o => o.TokenValidationParameters = new TokenValidationParameters { ValidateIssuer=true, ValidIssuer=builder.Configuration["Jwt:Issuer"], ValidateAudience=true, ValidAudience=builder.Configuration["Jwt:Audience"], ValidateIssuerSigningKey=true, IssuerSigningKey=new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)), ValidateLifetime=true, ClockSkew=TimeSpan.FromSeconds(30), RoleClaimType=System.Security.Claims.ClaimTypes.Role, NameClaimType=System.Security.Claims.ClaimTypes.Name });
builder.Services.AddAuthorization();
builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
builder.Services.AddScoped<JwtTokenService>();
builder.Services.AddScoped<InteractionLogService>();
builder.Services.AddScoped<AiService>();
var app = builder.Build();
if (app.Environment.IsDevelopment()) { app.UseSwagger(); app.UseSwaggerUI(); }
app.UseDefaultFiles(); app.UseStaticFiles(); app.UseAuthentication(); app.UseAuthorization(); app.MapControllers();
app.MapGet("/api/health", () => Results.Ok(new { status="ok", utc=DateTime.UtcNow })); app.MapFallbackToFile("index.html");
try { await using var scope=app.Services.CreateAsyncScope(); var db=scope.ServiceProvider.GetRequiredService<AppDbContext>(); await db.Database.EnsureCreatedAsync(); await DemoDataSeeder.SeedAsync(scope.ServiceProvider); }
catch(Exception ex) { app.Logger.LogError(ex, "Database initialization failed. Check SQL Server connection and schema."); }
app.Run();