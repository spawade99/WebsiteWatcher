using Azure.Storage.Blobs;
using HtmlAgilityPack;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Sql;
using Microsoft.Extensions.Logging;
using PuppeteerSharp;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace WebsiteWatcher;

public class PdfCreator(ILoggerFactory loggerFactory)
{
    private readonly ILogger _logger = loggerFactory.CreateLogger<Snapshot>();

    // Visit https://aka.ms/sqltrigger to learn how to use this trigger binding
    [Function(nameof(PdfCreator))]
    public async Task Run(
        [SqlTrigger("Websites", "WebsiteWatcher")] SqlChange<Website>[] changes,
            FunctionContext context)
    {
        foreach (var change in changes)
        {
            if (change.Operation != SqlChangeOperation.Insert) continue;

            var pdfStream = await ConvertPageToPDFAsync(change.Item.Url);
            if (pdfStream == null)
            {
                _logger.LogError("Failed to convert page to PDF.");
                return ;
            }
            _logger.LogInformation($"PDF Stream: {pdfStream.Length} bytes");
            //get swtorage connection string from environment variable
            var storageConnection = Environment.GetEnvironmentVariable("ConnectionStrings:WebsiteWatcherStorage");
            BlobClient blobClient = new BlobClient(storageConnection, "pdfs", $"{change.Item.Id}.pdf");
            await blobClient.UploadAsync(pdfStream);
        }
    }
    private async Task<Stream> ConvertPageToPDFAsync(string url)
    {
        var browserFetcher = new BrowserFetcher();
        await browserFetcher.DownloadAsync();

        await using var browser = await Puppeteer.LaunchAsync(new LaunchOptions
        {
            Headless = true
        });
        var page = await browser.NewPageAsync();
        await page.GoToAsync(url);
        await page.EvaluateExpressionHandleAsync("document.fonts.ready");
        var result = await page.PdfStreamAsync();
        result.Position = 0;
        return result;
    }
}
