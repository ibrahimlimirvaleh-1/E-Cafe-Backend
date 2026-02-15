using ECafe.Api.Middlewares;
using ECafe.Application;
using ECafe.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

// Global error handling (ən yuxarıda)
app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// Auth əlavə edəndə bunlar lazım olacaq:
// app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
