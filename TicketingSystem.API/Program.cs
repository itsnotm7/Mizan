using TicketingSystem.API.Extensions;
using TicketingSystem.API.Middleware;
using TicketingSystem.API.OpenApi;
using TicketingSystem.Infrastructure;
using TicketingSystem.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddInfrastructure(builder.Configuration); // DbContext + SQL Server + auth services
builder.Services.AddJwtAuth(builder.Configuration);

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi(options => options.AddDocumentTransformer<BearerSecurityTransformer>());

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseExceptionHandler();
app.UseStatusCodePages(); // Gives empty responses like 401 and 403 a ProblemDetails body too

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUI(options => options.SwaggerEndpoint("/openapi/v1.json", "Mizan API"));

    // Fill an empty database with demo users and a team (Development only)
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await DbSeeder.SeedAsync(db);
}

app.UseHttpsRedirection();

// Order matters: first find out who the caller is, then check what they may do
app.UseAuthentication();
app.UseAuthorization();

// Every controller requires a login unless it opts out with [AllowAnonymous],
// so forgetting [Authorize] on a new endpoint can't make it public
app.MapControllers().RequireAuthorization();

app.Run();
