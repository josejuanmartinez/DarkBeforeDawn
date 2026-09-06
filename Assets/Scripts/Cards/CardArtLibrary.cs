using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

// Replaces Runeboard's Illustrations component. That one streamed every sprite through Addressables
// under the label "default" and rebuilt its lookup tables at runtime; this is a plain asset holding
// direct sprite references, so there is no extra package to install, no async load, and no window
// during startup where art requests return null.
//
// Build or refresh it with Tools > Cards > Rebuild Card Art Library (see Editor/CardArtLibraryBuilder).
//
// The lookup-key normalization is deliberately identical to Illustrations.Normalize: strip diacritics,
// drop every non-alphanumeric character, lowercase. That is what lets a card named "Gap of Rohan"
// find GapOfRohan.jpg, and it means card JSON written for Runeboard resolves to the same art here.
[CreateAssetMenu(fileName = "CardArtLibrary", menuName = "Cards/Card Art Library")]
public class CardArtLibrary : ScriptableObject, ICardArtSource
{
    [System.Serializable]
    public class Entry
    {
        public Sprite sprite;
        // True for anything under Assets/Art/Cards. Mirrors Illustrations' separate cardArtByName
        // table, which existed so the center preview could refuse art from UI/animation folders.
        public bool isCardArt = true;
    }

    [SerializeField] private List<Entry> entries = new();

    private Dictionary<string, Sprite> byName;
    private Dictionary<string, Sprite> cardArtByName;

    public int Count => entries.Count;

    public IReadOnlyList<Entry> Entries => entries;

    public void SetEntries(List<Entry> newEntries)
    {
        entries = newEntries ?? new List<Entry>();
        byName = null;
        cardArtByName = null;
    }

    private void OnEnable()
    {
        // Force a rebuild after a domain reload or a reimport; the tables are cheap to build and
        // stale ones would hand back destroyed sprite references.
        byName = null;
        cardArtByName = null;
    }

    public bool TryGetSprite(string name, bool cardArtOnly, out Sprite sprite)
    {
        sprite = null;
        if (string.IsNullOrWhiteSpace(name)) return false;

        EnsureTables();
        Dictionary<string, Sprite> table = cardArtOnly ? cardArtByName : byName;
        return table.TryGetValue(Normalize(name), out sprite) && sprite != null;
    }

    public Sprite GetSprite(string name, bool cardArtOnly = false)
        => TryGetSprite(name, cardArtOnly, out Sprite sprite) ? sprite : null;

    // Any one card-art sprite, for decorative use (a loading screen's rotating card, a placeholder).
    public Sprite GetRandomCardArt()
    {
        EnsureTables();
        if (cardArtByName.Count == 0) return null;
        int index = Random.Range(0, cardArtByName.Count);
        foreach (KeyValuePair<string, Sprite> pair in cardArtByName)
        {
            if (index-- == 0) return pair.Value;
        }
        return null;
    }

    private void EnsureTables()
    {
        if (byName != null && cardArtByName != null) return;

        byName = new Dictionary<string, Sprite>();
        cardArtByName = new Dictionary<string, Sprite>();

        foreach (Entry entry in entries)
        {
            if (entry?.sprite == null) continue;

            // Register under the sprite name and, if it differs, the source texture name. Runeboard
            // needed both because a renamed image file often left Sprite.name behind.
            Register(byName, entry.sprite.name, entry.sprite);
            if (entry.sprite.texture != null) Register(byName, entry.sprite.texture.name, entry.sprite);

            if (!entry.isCardArt) continue;
            Register(cardArtByName, entry.sprite.name, entry.sprite);
            if (entry.sprite.texture != null) Register(cardArtByName, entry.sprite.texture.name, entry.sprite);
        }
    }

    // First writer wins, matching Illustrations.TryRegisterKey — a later duplicate key never
    // overwrites an earlier one, so lookups stay stable across rebuilds.
    private static void Register(Dictionary<string, Sprite> table, string rawName, Sprite sprite)
    {
        string key = Normalize(rawName);
        if (string.IsNullOrEmpty(key) || table.ContainsKey(key)) return;
        table[key] = sprite;
    }

    public static string Normalize(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return string.Empty;
        string sanitized = Regex.Replace(RemoveDiacritics(name), "[^A-Za-z0-9]", string.Empty);
        return sanitized.ToLowerInvariant();
    }

    private static string RemoveDiacritics(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return text;

        string normalized = text.Normalize(NormalizationForm.FormD);
        StringBuilder sb = new();
        foreach (char c in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) sb.Append(c);
        }
        return sb.ToString().Normalize(NormalizationForm.FormC);
    }
}
