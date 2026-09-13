using System.Collections.Generic;
using System.Globalization;
using System.Linq;

// Builds the PC card's body text. Lifted unchanged from Runeboard, where it lived at the bottom of
// Assets/Scripts/Actions/MaterialRetrievalOrAction.cs — it is pure text formatting with no gameplay
// dependency, so it moves into the card module rather than dragging that action file along.
public static class PcDescriptionBuilder
{
    public static string NormalizeLookupKey(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        return new string(value.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
    }

    public static string BuildBody(CardData data, bool includeFoundingText)
    {
        if (data == null) return string.Empty;

        string regionName = FormatDisplayRegionName(data.region);
        bool hasRegion = !string.IsNullOrWhiteSpace(regionName);

        System.Text.StringBuilder sb = new();
        if (hasRegion)
        {
            sb.Append(regionName).Append(". ");
        }

        // Population centres do not produce resources. Resource grants belong exclusively to Land
        // cards, whose own face and compact token render the granted materials.

        // A settlement is never played from the hand: it is picked as the turn's destination once
        // its land is on the board, and one play there (a character or encounter born here, or an
        // object of a kind it trades in) taps it until the next turn.
        if (includeFoundingText)
        {
            sb.Append("Destination: recruit characters and investigate encounters born here.");
            string wares = FormatObjectTypes(data);
            if (!string.IsNullOrEmpty(wares)) sb.Append(" Trades in ").Append(wares).Append('.');
            sb.Append(" One play here taps it for the turn.");
        }

        return sb.ToString().Trim();
    }

    public static string FormatObjectTypes(CardData data)
    {
        if (data?.objectTypes == null) return string.Empty;
        var tags = data.objectTypes.Distinct().Where(t => t != ObjectTypeEnum.None).Select(CardData.FormatObjectTypeTag).ToList();
        return tags.Count == 0 ? string.Empty : string.Join(", ", tags);
    }

    public static string FormatDisplayRegionName(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;

        List<char> chars = new(value.Length + 4);
        for (int i = 0; i < value.Length; i++)
        {
            char current = value[i];
            if (ShouldInsertWordSpace(value, i))
            {
                chars.Add(' ');
            }
            chars.Add(current);
        }

        string formatted = new string(chars.ToArray()).Trim().ToLowerInvariant();
        return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(formatted);
    }

    private static string BuildResourceSummary(CardData data)
    {
        List<string> parts = new();
        if (data.leatherGranted > 0) parts.Add($"{data.leatherGranted}<sprite name=\"leather\">");
        if (data.timberGranted > 0) parts.Add($"{data.timberGranted}<sprite name=\"timber\">");
        if (data.mountsGranted > 0) parts.Add($"{data.mountsGranted}<sprite name=\"mounts\">");
        if (data.ironGranted > 0) parts.Add($"{data.ironGranted}<sprite name=\"iron\">");
        if (data.steelGranted > 0) parts.Add($"{data.steelGranted}<sprite name=\"steel\">");
        if (data.mithrilGranted > 0) parts.Add($"{data.mithrilGranted}<sprite name=\"mithril\">");
        if (data.goldGranted > 0) parts.Add($"{data.goldGranted}<sprite name=\"gold\">");
        return string.Join(" ", parts);
    }

    // Splits a run-together authored name ("GapOfRohan", "WitheredHeath") into display words. Shared
    // with Card.FormatCardTitle, which applies the same rule to the card's own name.
    public static bool ShouldInsertWordSpace(string value, int index)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        if (index <= 0 || index >= value.Length) return false;

        char current = value[index];
        if (!char.IsUpper(current)) return false;

        char previous = value[index - 1];
        if (char.IsWhiteSpace(previous)) return false;

        if (char.IsLower(previous) || char.IsDigit(previous))
        {
            return true;
        }

        if (!char.IsUpper(previous)) return false;

        // An acronym run ends where the next char is lowercase: "PCName" splits before "Name".
        if (index + 1 < value.Length && char.IsLower(value[index + 1]))
        {
            return true;
        }

        return false;
    }
}
