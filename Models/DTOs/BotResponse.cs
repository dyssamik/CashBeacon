namespace CashBeacon;

public record InlineButton(string Label, string CallbackData);

public record BotResponse(
    string Text,
    IReadOnlyList<InlineButton>? Buttons = null,
    bool IsMonospace = false
);