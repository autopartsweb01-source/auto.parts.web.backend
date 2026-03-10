using AutoParts.Api.Auth;
using AutoParts.Api.Data;
using AutoParts.Api.Services;
using AutoParts.Api.Services.ClientApi;
using AutoParts.Api.Services.Security;
using AutoParts.Api.Infrastructure.Http;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using OfficeOpenXml;
using System.Text;

// ---------- EPPlus 7 License ----------
ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

var builder = WebApplication.CreateBuilder(args);

// ---------- DB ----------
builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// ---------- Services ----------
builder.Services.AddScoped<JwtTokenService>();
builder.Services.AddScoped<TwilioOtpService>();
builder.Services.AddScoped<EmailService>();
builder.Services.AddScoped<ICartService, CartService>();
builder.Services.AddScoped<IOrderService, OrderService>();
builder.Services.AddScoped<RazorpayService>();
builder.Services.AddScoped<IAdminOrderService, AdminOrderService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IOcrService, OcrService>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<ITokenStore, InMemoryTokenStore>();
builder.Services.AddTransient<AuthForwardingHandler>();
builder.Services.AddHttpClient<IAuthApiClient, AuthApiClient>(c =>
{
    c.BaseAddress = new Uri("https://hisuatchemistapi.ongc.co.in/api/");
    c.DefaultRequestHeaders.Accept.Clear();
    c.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
});
builder.Services.AddHttpClient<IUserApiClient, UserApiClient>(c =>
{
    c.BaseAddress = new Uri("https://hisuatchemistapi.ongc.co.in/api/");
}).AddHttpMessageHandler<AuthForwardingHandler>();
builder.Services.AddHttpClient<IOtpApiClient, OtpApiClient>(c =>
{
    c.BaseAddress = new Uri("https://hisuatchemistapi.ongc.co.in/api/");
}).AddHttpMessageHandler<AuthForwardingHandler>();

// ---------- Auth / JWT ----------
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
.AddJwtBearer(opt =>
{
    opt.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = false,
        ValidateAudience = false,
        ValidateIssuerSigningKey = false,
        RequireSignedTokens = false,
        ValidateLifetime = true,
        SignatureValidator = (token, parameters) => new System.IdentityModel.Tokens.Jwt.JwtSecurityToken(token)
    };
    opt.Events = new JwtBearerEvents
    {
        OnMessageReceived = ctx =>
        {
            // Support 'Authorization: Bearer <token>' normally; no changes needed.
            return Task.CompletedTask;
        }
    };
});

builder.Services.AddAuthorization();

// ---------- Controllers ----------
builder.Services.AddControllers();

// ---------- CORS ----------
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowWeb",
        b => b.WithOrigins(
                "https://radhesyam.com",
                "https://www.radhesyam.com",
                "http://radhesyam.com",
                "http://www.radhesyam.com",
                "https://sitarammedical.com",
                "https://www.sitarammedical.com",
                "https://auto-parts-web.vercel.app",
                "http://localhost:4200",
                "http://127.0.0.1:4200")
              .SetIsOriginAllowedToAllowWildcardSubdomains()
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials());
});

// ---------- Swagger + JWT Support ----------
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "AutoParts API",
        Version = "v1"
    });
    c.MapType<IFormFile>(() => new OpenApiSchema { Type = "string", Format = "binary" });
    c.MapType<IEnumerable<IFormFile>>(() => new OpenApiSchema { Type = "array", Items = new OpenApiSchema { Type = "string", Format = "binary" } });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter token like: Bearer {your token}"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
    c.CustomSchemaIds(type => type.FullName?.Replace("+", "."));
});

var app = builder.Build();

// ---------- Pipeline ----------
var swaggerEnabled = app.Configuration.GetValue<bool>("Swagger:Enabled");
if (swaggerEnabled || app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowWeb");

// app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
