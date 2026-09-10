using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using TaxKeepVN.Application.Service.Implementations;
using TaxKeepVN.Application.Service.Interfaces;
using TaxKeepVN.Domain.IRepositories;
using TaxKeepVN.Infrastructure.Contexts;
using TaxKeepVN.Infrastructure.Repositories;
using TaxKeepVN.Infrastructure.Storage;
using TaxKeepVNManagementSystem.Middlewares;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Configure Database (InMemory for now if no connection string, or Postgres)
// Let's use PostgreSQL as specified in initial dependencies
// Make sure appsettings.json has connection string. We will use a dummy one if it crashes
var connString = builder.Configuration.GetConnectionString("DefaultConnection") ?? "Host=localhost;Database=TaxKeep;Username=postgres;Password=postgres";
builder.Services.AddDbContext<TaxKeepDbContext>(options =>
    options.UseNpgsql(connString));

// Register Unit of Work and Repositories
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));

// Register Application Services
builder.Services.AddScoped<IFileStorageService, LocalFileStorageService>();
builder.Services.AddScoped<IDependentDocumentService, DependentDocumentService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseMiddleware<GlobalExceptionMiddleware>();

app.UseHttpsRedirection();

app.UseStaticFiles(); // Allow serving files from wwwroot

app.UseAuthorization();

app.MapControllers();

app.Run();
