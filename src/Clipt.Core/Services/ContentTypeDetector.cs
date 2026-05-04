using System.Text.Json;
using System.Text.RegularExpressions;
using Clipt.Core.Models;

namespace Clipt.Core.Services;

public sealed partial class ContentTypeDetector : IContentTypeDetector
{
    public ContentType Detect(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return ContentType.Text;
        }

        if (HexColorRegex().IsMatch(content.Trim()))
        {
            return ContentType.Color;
        }

        if (Uri.TryCreate(content.Trim(), UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
        {
            return ContentType.Url;
        }

        if (LooksLikeJson(content))
        {
            return ContentType.Json;
        }

        if (content.Contains("```", StringComparison.Ordinal) || content.Contains("# ", StringComparison.Ordinal))
        {
            return ContentType.Markdown;
        }

        if (content.Contains("public ", StringComparison.Ordinal)
            || content.Contains("using ", StringComparison.Ordinal)
            || content.Contains("SELECT ", StringComparison.OrdinalIgnoreCase))
        {
            return ContentType.Code;
        }

        return ContentType.Text;
    }

    private static bool LooksLikeJson(string content)
    {
        var trimmed = content.TrimStart();
        if (!trimmed.StartsWith('{') && !trimmed.StartsWith('['))
        {
            return false;
        }

        try
        {
            using var _ = JsonDocument.Parse(content);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    [GeneratedRegex("^#(?:[0-9a-fA-F]{3}|[0-9a-fA-F]{6})$")]
    private static partial Regex HexColorRegex();
}
