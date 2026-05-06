namespace Clipt.Core.Services;

public sealed record ClipboardWriteOptions
{
    public static readonly ClipboardWriteOptions Default = new();

    public static readonly ClipboardWriteOptions PlainText = new()
    {
        PasteMode = PasteMode.PlainText,
    };

    public PasteMode PasteMode { get; init; } = PasteMode.Auto;
}

public enum PasteMode
{
    /// <summary>
    /// Use the best available format from the clipboard item.
    /// </summary>
    Auto,

    /// <summary>
    /// Strip formatting and write only Unicode text.
    /// </summary>
    PlainText,
}
