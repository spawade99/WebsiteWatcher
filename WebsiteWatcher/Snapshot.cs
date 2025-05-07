using HtmlAgilityPack;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Sql;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace WebsiteWatcher;

public class Snapshot(ILoggerFactory loggerFactory)
{
    private readonly ILogger _logger = loggerFactory.CreateLogger<Snapshot>();

    // Visit https://aka.ms/sqltrigger to learn how to use this trigger binding
    [Function(nameof(Snapshot))]
    [SqlOutput("Snapshots", "WebsiteWatcher")]
    public SnapshotRecord? Run(
        [SqlTrigger("Websites", "WebsiteWatcher")] IReadOnlyList<SqlChange<Website>> changes,
            FunctionContext context)
    {
        SnapshotRecord? snapshot = null;
        _logger.LogInformation($"SQL Changes: ");
        foreach (var change in changes)
        {
            string website = JsonSerializer.Serialize(change.Item);

            if (change.Operation != SqlChangeOperation.Insert) continue;

            HtmlWeb web = new HtmlWeb();
            HtmlDocument doc = web.Load(change.Item.Url);
            if ((change.Item.XPathExpression is not null))
            {
                var node = doc.DocumentNode.SelectSingleNode(change.Item?.XPathExpression);
                var content = node?.InnerText.Trim();

                _logger.LogInformation($"Website Data: {content}");
                snapshot = new SnapshotRecord(change.Item.Id, content);
            }

        }
        return snapshot;
    }
}

public record SnapshotRecord(Guid Id, string Content);