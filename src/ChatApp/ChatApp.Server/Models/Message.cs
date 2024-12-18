﻿using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace ChatApp.Server.Models;

public class Message
{
    public Message() { }
    public Message(string message)
    {
        Content = message;
        Date = DateTime.UtcNow;
        Role = "User";
    }
    public string Id { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime Date { get; set; }

    public ChatMessageContent ToChatMessageContent()
    {
        // todo: is there anything else we can do here, maybe with date?
        // or author? https://learn.microsoft.com/en-us/dotnet/api/microsoft.semantickernel.chatmessagecontent?view=semantic-kernel-dotnet
        var role = Role.ToLower() switch
        {
            "user" => AuthorRole.User,
            "assistant" => AuthorRole.Assistant,
            "tool" => AuthorRole.Tool,
            "system" => AuthorRole.System,
            _ => throw new ArgumentException("Invalid role", nameof(Role))
        };

        return new ChatMessageContent
        {
            Role = role,
            Content = Content,
        };
    }
}

public class HistoryMessage : Message
{
    public string ConversationId { get; set; } = string.Empty;
    public string Feedback { get; set; } = string.Empty;

    public HistoryMessage() { }

    public HistoryMessage(Message message)
    {
        Id = message.Id;
        Role = message.Role;
        Content = message.Content;
        Date = message.Date;
    }
}
