using Clipt.Core.Models;

namespace Clipt.Core.Services;

public interface IContentTypeDetector
{
    ContentType Detect(string content);
}
