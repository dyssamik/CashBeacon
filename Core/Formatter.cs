using System.Xml.Linq;

namespace CashBeacon;

public static class Formatter
{
    public static string FormatDocument(string xml)
    {
        try
        {
            var doc = XDocument.Parse(xml);
            var content = doc.Descendants("LayoutResult").FirstOrDefault()?.Value;
            return WrapMonospace(string.IsNullOrWhiteSpace(content) ? "Нет данных." : content);
        }
        catch (Exception ex)
        {
            return WrapMonospace($"Ошибка: {ex.Message}");
        }
    }

    private static string WrapMonospace(string text)
    {
        var escaped = text
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;");
        return $"<pre>{escaped}</pre>";
    }
}