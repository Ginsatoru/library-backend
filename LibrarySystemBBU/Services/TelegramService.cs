using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LibrarySystemBBU.Services
{
    public interface ITelegramService
    {
        Task SendMessageAsync(string chatId, string text, CancellationToken cancellationToken = default);
    }

    public class TelegramService : ITelegramService
    {
        private readonly IHttpClientFactory _clientFactory;
        private readonly TelegramOptions _options;
        private readonly ILogger<TelegramService> _logger;

        public TelegramService(
            IHttpClientFactory clientFactory,
            IOptions<TelegramOptions> options,
            ILogger<TelegramService> logger)
        {
            _clientFactory = clientFactory;
            _options = options.Value;
            _logger = logger;

            if (string.IsNullOrWhiteSpace(_options.BotToken))
            {
                _logger.LogError("Telegram BotToken is not configured. Check appsettings.json under 'Telegram:BotToken'.");
                // you can throw here if you want:
                // throw new InvalidOperationException("Telegram BotToken is not configured.");
            }
        }

        public async Task SendMessageAsync(string chatId, string text, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(_options.BotToken))
                throw new InvalidOperationException("Telegram BotToken is not configured.");

            if (string.IsNullOrWhiteSpace(chatId))
                throw new ArgumentException("chatId is required.", nameof(chatId));

            var client = _clientFactory.CreateClient();
            var url = $"https://api.telegram.org/bot{_options.BotToken}/sendMessage";

            var payload = new
            {
                chat_id = chatId,
                text = text,
                parse_mode = "HTML"
            };

            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            try
            {
                var response = await client.PostAsync(url, content, cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    string body = await response.Content.ReadAsStringAsync(cancellationToken);
                    _logger.LogError("Telegram error: Status {Status}, Body: {Body}", response.StatusCode, body);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while sending Telegram message.");
            }
        }
    }
}
