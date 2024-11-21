using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Authentication.
builder.Services.AddAuthorization();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(); // so on change we dont re-deploy
builder.Services.ConfigureOptions<JwtBearerConfigureOptions>();

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();

public class JwtBearerConfigureOptions(IConfiguration configuration) : IConfigureNamedOptions<JwtBearerOptions>
{
    private const string ConfigurationSectionName = "JwtBearer";
    public void Configure(string? name, JwtBearerOptions options)
    {
        configuration.GetSection(ConfigurationSectionName).Bind(options);
    }
    public void Configure(JwtBearerOptions options)
    {
        Configure(options);
    }
}