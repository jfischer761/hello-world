using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddSingleton<IX12Parser, X12Parser>();
builder.Services.AddSingleton<IX12Validator, X12Validator>();
builder.Services.AddSingleton<IX12AcknowledgmentGenerator, X12AcknowledgmentGenerator>();

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", builder =>
    {
        builder.AllowAnyOrigin()
               .AllowAnyMethod()
               .AllowAnyHeader();
    });
});

var app = builder.Build();

app.UseCors("AllowAll");

// Define the validation endpoint
app.MapPost("/validate", async (HttpContext context, IX12Parser parser, IX12Validator validator, IX12AcknowledgmentGenerator ackGenerator) =>
{
    using var reader = new StreamReader(context.Request.Body);
    var ediContent = await reader.ReadToEndAsync();

    try
    {
        // Parse the EDI document
        var document = parser.Parse(ediContent);
        
        // Validate the document
        var validationResults = validator.Validate(document);
        
        if (validationResults.IsValid)
        {
            return Results.Ok(new { message = "EDI document is valid", transactionType = document.TransactionType });
        }
        else
        {
            // Generate 999 acknowledgment for invalid document
            var acknowledgment = ackGenerator.Generate(document, validationResults);
            return Results.BadRequest(new { message = "EDI document is invalid", errors = validationResults.Errors, acknowledgment });
        }
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { message = "Failed to process EDI document", error = ex.Message });
    }
});

app.Run();