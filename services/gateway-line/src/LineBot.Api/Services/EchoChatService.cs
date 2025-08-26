// SPDX-License-Identifier: MIT
using LineBot.Api.Models;

namespace LineBot.Api.Services;

public interface IChatService
{
    Task<ChatResponse> GenerateReplyAsync(ChatRequest request, CancellationToken cancellationToken = default);
}

public class EchoChatService : IChatService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<EchoChatService> _logger;

    public EchoChatService(IConfiguration configuration, ILogger<EchoChatService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public Task<ChatResponse> GenerateReplyAsync(ChatRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var inputText = request.Message.Text ?? string.Empty;
            var maxCharsPerMessage = request.Limits.MaxCharsPerMessage;

            if (string.IsNullOrWhiteSpace(inputText))
            {
                return Task.FromResult(new ChatResponse
                {
                    Status = "ok",
                    Messages = new List<string> { _configuration["Chat:Fallbacks:Default"] ?? "The service is temporarily unavailable." },
                    FallbackUsed = true
                });
            }

            // Split text if it exceeds the character limit
            var messages = SplitTextByCharacterLimit(inputText, maxCharsPerMessage);

            _logger.LogInformation("Echo chat generated {MessageCount} messages for input length {InputLength}", 
                messages.Count, inputText.Length);

            return Task.FromResult(new ChatResponse
            {
                Status = "ok",
                Messages = messages,
                FallbackUsed = false
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in Echo chat service");
            return Task.FromResult(new ChatResponse
            {
                Status = "error",
                Messages = new List<string> { _configuration["Chat:Fallbacks:Default"] ?? "The service is temporarily unavailable." },
                FallbackUsed = true,
                Error = new ChatError
                {
                    Code = "internal_error",
                    Message = ex.Message
                }
            });
        }
    }

    private List<string> SplitTextByCharacterLimit(string text, int maxCharsPerMessage)
    {
        var messages = new List<string>();
        
        if (text.Length <= maxCharsPerMessage)
        {
            messages.Add(text);
            return messages;
        }

        // Split by UTF-16 code units (LINE's character counting rule)
        var remainingText = text;
        while (remainingText.Length > 0)
        {
            if (remainingText.Length <= maxCharsPerMessage)
            {
                messages.Add(remainingText);
                break;
            }

            // Find a good split point (prefer whitespace or line breaks)
            var splitIndex = maxCharsPerMessage;
            
            // Look backwards for a whitespace character to split on
            for (var i = maxCharsPerMessage - 1; i >= Math.Max(0, maxCharsPerMessage - 100); i--)
            {
                if (char.IsWhiteSpace(remainingText[i]))
                {
                    splitIndex = i;
                    break;
                }
            }

            messages.Add(remainingText.Substring(0, splitIndex));
            remainingText = remainingText.Substring(splitIndex).TrimStart();
        }

        _logger.LogDebug("Split text of {TotalChars} characters into {MessageCount} messages", 
            text.Length, messages.Count);

        return messages;
    }
}