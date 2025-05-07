using System;
using Azure.Storage.Blobs;
using HtmlAgilityPack;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Sql;
using Microsoft.Extensions.Logging;
using PuppeteerSharp;

namespace WebsiteWatcher;

public class Watcher(ILoggerFactory loggerFactory)
{
    private readonly ILogger _logger = loggerFactory.CreateLogger<Watcher>();

    private const string sqlCommand = @"  select w.Id,w.Url,w.XPathExpression, w.Timestamp from Websites as w
                                     left join Snapshots s on s.Id=w.Id
                                        Where s.Timestamp=(select max(Timestamp) from Snapshots where Id=w.Id)";

    [Function("Watcher")]
    [SqlOutput("Snapshots", "WebsiteWatcher")]
    public async Task<SnapshotRecord?> Run([TimerTrigger("*/20 * * * * *")] TimerInfo myTimer, [SqlInput(sqlCommand, "WebsiteWatcher")] IReadOnlyList<WebsiteModel> websites)
    {
        SnapshotRecord? result = null;
        foreach (WebsiteModel websiteModel in websites)
        {

            HtmlWeb web = new HtmlWeb();
            HtmlDocument doc = web.Load(websiteModel.Url);
            if ((websiteModel.XPathExpression is not null))
            {
                var node = doc.DocumentNode.SelectSingleNode(websiteModel?.XPathExpression);
                var content = node?.InnerText.Trim();
                content = content.Replace("Microsoft Entra", "Azure ");
                bool contentUpdated = content != websiteModel.LatestContent;
                if (contentUpdated)
                {
                    _logger.LogInformation($"Content updated for website: {websiteModel.Url}");
                    var pdfStream = await ConvertPageToPDFAsync(websiteModel.Url);

                    //get swtorage connection string from environment variable
                    var storageConnection = Environment.GetEnvironmentVariable("ConnectionStrings:WebsiteWatcherStorage");

                    //create blob client
                    BlobServiceClient blobServiceClient = new BlobServiceClient(storageConnection);
                    BlobContainerClient containerClient = blobServiceClient.GetBlobContainerClient("pdfs");

                    // Ensure the container exists
                    await containerClient.CreateIfNotExistsAsync();

                    // Create blob client
                    BlobClient blobClient = containerClient.GetBlobClient($"{websiteModel.Id}-{DateTime.UtcNow:MMddyyyy}.pdf");
                    await blobClient.UploadAsync(pdfStream, true);
                    _logger.LogInformation($"New PDF uploaded to blob storage with name {DateTime.UtcNow:MMddyyyy}.pdf");
                    result = new SnapshotRecord(websiteModel.Id, content);
                }
            }
        }
        return result;
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

public class WebsiteModel
{
    public Guid Id { get; set; }
    public string Url { get; set; }
    public string? XPathExpression { get; set; }
    public string LatestContent { get; set; }
}