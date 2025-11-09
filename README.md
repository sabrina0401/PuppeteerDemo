# PuppeteerDemo - 網頁截圖 API

基於 ASP.NET Core 和 PuppeteerSharp 的網頁截圖服務

## 功能

- 📸 網頁截圖（JPG/PNG）
- 📄 網頁轉 PDF
- 🎨 自訂品質和尺寸
- 🚀 RESTful API

## 技術棧

- .NET 9.0
- ASP.NET Core Minimal API
- PuppeteerSharp

## API 端點

### POST /screenshot

擷取網頁截圖或產生 PDF

**請求範例：**

\`\`\`json
{
  "url": "https://www.google.com",
  "savePath": "C:/screenshots/google.png",
  "format": "png"
}
\`\`\`

## 執行專案

\`\`\`bash
dotnet restore
dotnet run
\`\`\`

## 授權

MIT License
