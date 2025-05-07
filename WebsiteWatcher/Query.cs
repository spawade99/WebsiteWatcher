using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Sql;
using Microsoft.Extensions.Logging;

namespace WebsiteWatcher
{
    public class Query(ILogger<Query> logger)
    {
        private readonly ILogger<Query> _logger = logger;
        private const string commandString = @"select w.Id,w.Url,s.[Timestamp] as LastTimestamp from Websites as w
                                                left join Snapshots s on s.Id=w.Id
                                                Where s.Timestamp=(select max(Timestamp) from Snapshots where Id=w.Id) AND
                                                    s.[Timestamp] Between DATEADD(hour,-3,GETUTCDATE()) AND GETUTCDATE()";

        [Function("Query")]
        public IActionResult Run([HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequest req, [SqlInput(commandString, "WebsiteWatcher")] IReadOnlyList<dynamic> data)
        {
            return new OkObjectResult(data);
        }
    }
}
