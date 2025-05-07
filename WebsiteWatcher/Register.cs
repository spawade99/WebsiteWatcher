using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Sql;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace WebsiteWatcher;

public class Register(ILogger<Register> logger)
{
    private readonly ILogger<Register> _logger = logger;

    [Function(nameof(Register))]
    [SqlOutput("dbo.Websites", "WebsiteWatcher")]
    public async Task<Website?> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous,"post")] HttpRequest req)
    {
        _logger.LogInformation("C# HTTP trigger function processed a request.");

        var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
        if (requestBody is null)
            return null; //no ouput to sql

        var options = new JsonSerializerOptions() { PropertyNameCaseInsensitive = false };
        var newWebsite = JsonSerializer.Deserialize<Website>(requestBody, options);

        if (newWebsite == null)
            return null; //no ouput to sql

        newWebsite.Id = Guid.NewGuid();

        return newWebsite;

    }
}

public class Website
{
    public Guid Id { get; set; }
    public string Url { get; set; }
    public string? XPathExpression { get; set; }
}
