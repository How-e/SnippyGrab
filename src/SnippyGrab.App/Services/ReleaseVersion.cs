using System.Text.RegularExpressions;

namespace SnippyGrab.App.Services;

internal sealed record ReleaseVersion(int Major, int Minor, int Patch, string[] Pre) : IComparable<ReleaseVersion>
{
    internal static ReleaseVersion? Parse(string value)
    {
        var match = Regex.Match(value, @"^v?(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)(?:-([0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*))?(?:\+[0-9A-Za-z.-]+)?$");
        if (!match.Success || !int.TryParse(match.Groups[1].Value, out var major) || !int.TryParse(match.Groups[2].Value, out var minor) || !int.TryParse(match.Groups[3].Value, out var patch)) return null;
        var pre = match.Groups[4].Success ? match.Groups[4].Value.Split('.') : [];
        if (pre.Any(p => p.Length > 1 && p[0] == '0' && p.All(char.IsAsciiDigit))) return null;
        return new(major, minor, patch, pre);
    }
    public int CompareTo(ReleaseVersion? other)
    {
        if (other is null) return 1;
        foreach (var pair in new[] { (Major, other.Major), (Minor, other.Minor), (Patch, other.Patch) }) { var c = pair.Item1.CompareTo(pair.Item2); if (c != 0) return c; }
        if (Pre.Length == 0 || other.Pre.Length == 0) return (Pre.Length == 0 ? 1 : 0).CompareTo(other.Pre.Length == 0 ? 1 : 0);
        // Early alpha releases used a descriptive label before their date. Compare
        // those published builds by date/revision so "queue" cannot outrank a later "design".
        static string[] OrderedPre(string[] pre) => pre.Length == 4 && pre[0] == "alpha" && pre[1].Any(char.IsAsciiLetter) && Regex.IsMatch(pre[2], "^[0-9]{8}$") && pre[3].All(char.IsAsciiDigit) ? [pre[0], pre[2], pre[3]] : pre;
        var left = OrderedPre(Pre); var right = OrderedPre(other.Pre);
        for (var i = 0; i < Math.Min(left.Length, right.Length); i++)
        {
            var a = left[i]; var b = right[i]; var an = a.All(char.IsAsciiDigit); var bn = b.All(char.IsAsciiDigit);
            var c = an && bn ? a.Length != b.Length ? a.Length.CompareTo(b.Length) : string.CompareOrdinal(a, b) : an != bn ? (an ? -1 : 1) : string.CompareOrdinal(a, b);
            if (c != 0) return c;
        }
        return left.Length.CompareTo(right.Length);
    }
}
