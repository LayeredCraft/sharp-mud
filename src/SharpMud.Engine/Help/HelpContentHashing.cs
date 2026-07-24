using System.Security.Cryptography;
using System.Text;

namespace SharpMud.Engine.Help;

// Shared by HelpTopic.ContentHash and anything comparing it against a
// HelpTopicChunk.SourceContentHash - kept as one static function rather than
// duplicated so the exact hash inputs/encoding can't drift between the two.
internal static class HelpContentHashing
{
    public static string Compute(string text) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
}
