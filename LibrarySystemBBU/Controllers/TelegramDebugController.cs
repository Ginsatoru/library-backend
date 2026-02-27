// File: Controllers/TelegramDebugController.cs
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using LibrarySystemBBU.Models;
using LibrarySystemBBU.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace LibrarySystemBBU.Controllers
{
    // This makes the URL exactly: /TelegramDebug
    [Route("TelegramDebug")]
    public class TelegramDebugController : Controller
    {
        private readonly IHttpClientFactory _http;
        private readonly LibrarySystemBBU.Services.TelegramOptions _tgOptions;

        public TelegramDebugController(
            IHttpClientFactory http,
            IOptions<LibrarySystemBBU.Services.TelegramOptions> options)
        {
            _http = http;
            _tgOptions = options.Value;
        }

        // GET /TelegramDebug
        [HttpGet("")]
        public async Task<IActionResult> Index()
        {
            var list = new List<TelegramDebugRow>();

            if (string.IsNullOrWhiteSpace(_tgOptions.BotToken))
            {
                ViewBag.Error = "Telegram BotToken is not configured. Please set Telegram:BotToken in appsettings.json.";
                return View(list);
            }

            var url = $"https://api.telegram.org/bot{_tgOptions.BotToken}/getUpdates";

            try
            {
                var client = _http.CreateClient();
                var json = await client.GetStringAsync(url);

                using var doc = JsonDocument.Parse(json);

                if (doc.RootElement.TryGetProperty("result", out var resultArray))
                {
                    foreach (var item in resultArray.EnumerateArray())
                    {
                        if (!item.TryGetProperty("message", out var msg))
                            continue;

                        long chatId = 0;
                        string? username = null;
                        string? text = null;

                        if (msg.TryGetProperty("chat", out var chatEl) &&
                            chatEl.TryGetProperty("id", out var chatIdEl))
                        {
                            chatId = chatIdEl.GetInt64();
                        }

                        if (msg.TryGetProperty("from", out var fromEl) &&
                            fromEl.TryGetProperty("username", out var unEl))
                        {
                            username = unEl.GetString();
                        }

                        if (msg.TryGetProperty("text", out var textEl))
                        {
                            text = textEl.GetString();
                        }

                        list.Add(new TelegramDebugRow
                        {
                            ChatId = chatId,
                            Username = username,
                            Text = text
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                ViewBag.Error = "Error calling Telegram getUpdates: " + ex.Message;
            }

            return View(list);
        }
    }
}
