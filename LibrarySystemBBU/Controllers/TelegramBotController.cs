// File: Controllers/TelegramBotController.cs
using System;
using System.Text.Json;
using System.Threading.Tasks;
using LibrarySystemBBU.Data;
using LibrarySystemBBU.Models;
using LibrarySystemBBU.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LibrarySystemBBU.Controllers
{
    [ApiController]
    [Route("api/telegram")]
    public class TelegramBotController : ControllerBase
    {
        private readonly DataContext _db;
        private readonly ITelegramService _telegram;
        private readonly ILogger<TelegramBotController> _logger;

        public TelegramBotController(
            DataContext db,
            ITelegramService telegram,
            ILogger<TelegramBotController> logger)
        {
            _db = db;
            _telegram = telegram;
            _logger = logger;
        }

        /// <summary>
        /// Telegram will POST updates here: /api/telegram/webhook
        /// </summary>
        [HttpPost("webhook")]
        public async Task<IActionResult> Webhook([FromBody] JsonElement updateJson)
        {
            try
            {
                var update = JsonSerializer.Deserialize<TelegramUpdate>(
                    updateJson.GetRawText(),
                    new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                if (update == null)
                {
                    _logger.LogWarning("Telegram update is null.");
                    return Ok();
                }

                var msg = update.Message ?? update.EditedMessage;
                if (msg == null)
                {
                    _logger.LogInformation("No message in Telegram update (no Message/EditedMessage).");
                    return Ok();
                }

                var chatId = msg.Chat?.Id;
                var text = msg.Text?.Trim();

                if (chatId == null || string.IsNullOrWhiteSpace(text))
                {
                    _logger.LogInformation("No chatId or text in Telegram message.");
                    return Ok();
                }

                var chatIdString = chatId.Value.ToString();
                var fromUserId = msg.From?.Id;
                var username = msg.From?.Username;

                _logger.LogInformation("Incoming Telegram message from chat {ChatId}: {Text}", chatIdString, text);

                // /start or /link
                if (text.StartsWith("/start", StringComparison.OrdinalIgnoreCase) ||
                    text.StartsWith("/link", StringComparison.OrdinalIgnoreCase))
                {
                    var parts = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length < 2)
                    {
                        await _telegram.SendMessageAsync(
                            chatIdString,
                            "សូមវាយ <b>/start CARDNUMBER</b> ឬ ចុចលីង <b>Connect Telegram</b> ពីគេហទំព័របណ្ណាល័យ។\n" +
                            "ឧទាហរណ៍: <code>/start BBU-2025-00123</code>");
                        return Ok();
                    }

                    // 🔑 Either:
                    // - TelegramPairToken (new self-connect deep link)
                    // - DICardNumber (old manual flow)
                    var key = parts[1];

                    Member? member = null;

                    // 1️⃣ Try deep-link token from registration
                    member = await _db.Members
                        .FirstOrDefaultAsync(m => m.TelegramPairToken == key);

                    // 2️⃣ If not found, fallback to DI Card Number
                    if (member == null)
                    {
                        member = await _db.Members
                            .FirstOrDefaultAsync(m => m.DICardNumber == key);
                    }

                    if (member == null)
                    {
                        await _telegram.SendMessageAsync(
                            chatIdString,
                            "មិនរកឃើញសមាជិកទេ។\n" +
                            "• សូមពិនិត្យ Card Number ម្តងទៀត, ឬ\n" +
                            "• ចូលទៅគេហទំព័របណ្ណាល័យ ហើយចុចលីង <b>Connect Telegram</b> ម្ដងទៀត។");
                        return Ok();
                    }

                    // ✅ Save Telegram identifiers onto Member
                    member.TelegramChatId = chatIdString;

                    if (fromUserId.HasValue)
                    {
                        member.TelegramUserId = fromUserId.Value;
                    }

                    if (!string.IsNullOrWhiteSpace(username))
                    {
                        member.TelegramUsername = username;
                    }

                    // Optionally clear token so link can’t be reused
                    if (!string.IsNullOrWhiteSpace(member.TelegramPairToken) &&
                        member.TelegramPairToken == key)
                    {
                        member.TelegramPairToken = null;
                    }

                    await _db.SaveChangesAsync();

                    await _telegram.SendMessageAsync(
                        chatIdString,
                        $"សួស្តី <b>{member.FullName}</b> 👋\n" +
                        $"ការភ្ជាប់ Telegram Bot ជាមួយគណនីបណ្ណាល័យរបស់អ្នកបានជោគជ័យ។ ✅\n\n" +
                        $"ចាប់ពីពេលនេះទៅ បណ្ណាល័យនឹងផ្ញើសាររំលឹកថ្ងៃសងសៀវភៅមកទីនេះ (3 ថ្ងៃមុនថ្ងៃសង, នៅថ្ងៃសង, និងក្រោយថ្ងៃសង)។");

                    _logger.LogInformation(
                        "Member {MemberId} linked with chatId {ChatId}, TG user {UserId}, username {Username}",
                        member.MemberId, chatIdString, fromUserId, username);
                }
                else
                {
                    // Any other message -> show help
                    await _telegram.SendMessageAsync(
                        chatIdString,
                        "សួស្តី 🙋‍♂️\n\n" +
                        "សម្រាប់ភ្ជាប់ Telegram ជាមួយគណនីបណ្ណាល័យ BBU អ្នកអាចធ្វើបានពីររបៀប៖\n\n" +
                        "1️⃣ ចូលគេហទំព័របណ្ណាល័យ (Member Login) ហើយចុចប៊ូតុង <b>Connect Telegram</b>\n" +
                        "   ➜ Telegram នឹងបើក Bot ហើយសូមចុច <b>Start</b> នៅទីនោះ។\n\n" +
                        "2️⃣ ឬ វាយបញ្ចូល<br/><code>/start CARDNUMBER</code>\n" +
                        "   ➜ CARDNUMBER គឺលេខលើ Library/DI Card របស់អ្នក។");
                }

                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling Telegram webhook.");
                // Telegram expects 200 OK even if internal error
                return Ok();
            }
        }
    }

    #region Telegram DTOs

    public class TelegramUpdate
    {
        public TelegramMessage? Message { get; set; }
        public TelegramMessage? EditedMessage { get; set; }
        public TelegramCallbackQuery? CallbackQuery { get; set; }
    }

    public class TelegramMessage
    {
        public long Message_Id { get; set; }
        public TelegramUser? From { get; set; }
        public TelegramChat? Chat { get; set; }
        public DateTime Date { get; set; }
        public string? Text { get; set; }
    }

    public class TelegramChat
    {
        public long Id { get; set; }
        public string? Type { get; set; }
        public string? Title { get; set; }
        public string? Username { get; set; }
        public string? First_Name { get; set; }
        public string? Last_Name { get; set; }
    }

    public class TelegramUser
    {
        public long Id { get; set; }
        public bool Is_Bot { get; set; }
        public string? First_Name { get; set; }
        public string? Last_Name { get; set; }
        public string? Username { get; set; }
        public string? Language_Code { get; set; }
    }

    public class TelegramCallbackQuery
    {
        public string? Id { get; set; }
        public TelegramUser? From { get; set; }
        public TelegramMessage? Message { get; set; }
        public string? Data { get; set; }
    }

    #endregion
}
