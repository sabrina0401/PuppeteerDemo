
using System;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using PuppeteerSharp;
using PuppeteerSharp.Media;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();


// 加入根路徑測試端點
app.MapGet("/", (Func<IResult>)(() => Results.Ok(new
    {
        message = "Screenshot API is running",
        version = "1.0.0",
        endpoints = new[]
        {
            "POST /screenshot - Capture screenshot (jpg/png/pdf)"
        }
    })))
    .WithName("Root")
    .WithOpenApi();

// 通用截圖 API - 支援所有格式
app.MapPost("/screenshot", (Func<ScreenshotRequest, Task<IResult>>)(async (ScreenshotRequest request) =>
    {
        var validationResult = ScreenshotHelpers.ValidateRequest(request);
        if (validationResult != null)
            return validationResult;

        try
        {
            string filePath;
            string format;

            switch (request.Format?.ToLower())
            {
                case "jpg":
                case "jpeg":
                    filePath = await ScreenshotHelpers.CaptureScreenshotAsync(request.Url, request.SavePath, ScreenshotType.Jpeg, request.Quality ?? 80);
                    format = "JPEG";
                    break;
                case "png":
                    filePath = await ScreenshotHelpers.CaptureScreenshotAsync(request.Url, request.SavePath, ScreenshotType.Png);
                    format = "PNG";
                    break;
                case "pdf":
                    filePath = await ScreenshotHelpers.CapturePdfAsync(request.Url, request.SavePath, request.PdfOptions);
                    format = "PDF";
                    break;
                default:
                    return Results.BadRequest(new ScreenshotResponse
                    {
                        Success = false,
                        Message = "Invalid format. Supported formats: jpg, png, pdf"
                    });
            }

            return Results.Ok(new ScreenshotResponse
            {
                Success = true,
                Message = $"{format} captured successfully",
                FilePath = filePath,
                Format = format
            });
        }
        catch (Exception ex)
        {
            return Results.BadRequest(new ScreenshotResponse
            {
                Success = false,
                Message = $"Error: {ex.Message}"
            });
        }
    }))
    .WithName("CaptureScreenshot")
    .WithOpenApi();


app.Run();

// Helper 類別
public static class ScreenshotHelpers
{
    // 驗證請求
    public static IResult? ValidateRequest(ScreenshotRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Url))
        {
            return Results.BadRequest(new ScreenshotResponse
            {
                Success = false,
                Message = "URL is required"
            });
        }

        if (!Uri.TryCreate(request.Url, UriKind.Absolute, out _))
        {
            return Results.BadRequest(new ScreenshotResponse
            {
                Success = false,
                Message = "Invalid URL format"
            });
        }

        if (string.IsNullOrWhiteSpace(request.SavePath))
        {
            return Results.BadRequest(new ScreenshotResponse
            {
                Success = false,
                Message = "SavePath is required"
            });
        }

        return null;
    }

    // 截圖核心方法
    public static async Task<string> CaptureScreenshotAsync(string url, string savePath, ScreenshotType type, int quality = 80)
    {
        // 確保目錄存在
        var directory = Path.GetDirectoryName(savePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        // 確保檔案有正確的副檔名
        var extension = type == ScreenshotType.Jpeg ? ".jpg" : ".png";
        if (!savePath.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
        {
            savePath += extension;
        }

        var browserFetcher = new BrowserFetcher();
        await browserFetcher.DownloadAsync();

        var browser = await Puppeteer.LaunchAsync(new LaunchOptions
        {
            Headless = true,
            Args = new[] { "--no-sandbox", "--disable-setuid-sandbox" }
        });

        try
        {
            var page = await browser.NewPageAsync();
            try
            {
                await page.SetViewportAsync(new ViewPortOptions
                {
                    Width = 1920,
                    Height = 1080
                });

                await page.GoToAsync(url, new NavigationOptions
                {
                    WaitUntil = new[] { WaitUntilNavigation.Networkidle0 }
                });

                await page.ScreenshotAsync(savePath, new ScreenshotOptions
                {
                    Type = type,
                    Quality = type == ScreenshotType.Jpeg ? quality : null,
                    FullPage = true
                });
            }
            finally
            {
                await page.CloseAsync();
            }
        }
        finally
        {
            await browser.CloseAsync();
        }

        return savePath;
    }

    // PDF 核心方法
    public static async Task<string> CapturePdfAsync(string url, string savePath, PdfOptionsRequest? options = null)
    {
        // 確保目錄存在
        var directory = Path.GetDirectoryName(savePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        // 確保檔案有 .pdf 副檔名
        if (!savePath.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
        {
            savePath += ".pdf";
        }

        var browserFetcher = new BrowserFetcher();
        await browserFetcher.DownloadAsync();

        var browser = await Puppeteer.LaunchAsync(new LaunchOptions
        {
            Headless = true,
            Args = new[] { "--no-sandbox", "--disable-setuid-sandbox" }
        });

        try
        {
            var page = await browser.NewPageAsync();
            try
            {
                await page.GoToAsync(url, new NavigationOptions
                {
                    WaitUntil = new[] { WaitUntilNavigation.Networkidle0 }
                });

                await page.PdfAsync(savePath, new PdfOptions
                {
                    Format = options?.Format ?? PaperFormat.A4,
                    PrintBackground = options?.PrintBackground ?? true,
                    Landscape = options?.Landscape ?? false,
                    MarginOptions = new MarginOptions
                    {
                        Top = options?.MarginTop ?? "10mm",
                        Right = options?.MarginRight ?? "10mm",
                        Bottom = options?.MarginBottom ?? "10mm",
                        Left = options?.MarginLeft ?? "10mm"
                    }
                });
            }
            finally
            {
                await page.CloseAsync();
            }
        }
        finally
        {
            await browser.CloseAsync();
        }

        return savePath;
    }
}

// 請求模型
public record ScreenshotRequest
{
    [Required]
    public string Url { get; init; } = string.Empty;

    [Required]
    public string SavePath { get; init; } = string.Empty;

    public string? Format { get; init; } = "png"; // jpg, png, pdf

    public int? Quality { get; init; } = 80; // 僅用於 JPEG (0-100)

    public PdfOptionsRequest? PdfOptions { get; init; }
}

// PDF 選項
public record PdfOptionsRequest
{
    public PaperFormat Format { get; init; } = PaperFormat.A4;
    public bool PrintBackground { get; init; } = true;
    public bool Landscape { get; init; } = false;
    public string MarginTop { get; init; } = "10mm";
    public string MarginRight { get; init; } = "10mm";
    public string MarginBottom { get; init; } = "10mm";
    public string MarginLeft { get; init; } = "10mm";
}

// 回應模型
public record ScreenshotResponse
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
    public string? FilePath { get; init; }
    public string? Format { get; init; }
}