using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Scene skin library and selection. Skin assets own the editable parameters.</summary>
[DefaultExecutionOrder(-60), DisallowMultipleComponent]
public sealed class SkinManager : MonoBehaviour
{
    [SerializeField] private List<BoardSkin> skins = new();
    [SerializeField] private BoardSkin activeSkin;
    public IReadOnlyList<BoardSkin> Skins => skins.AsReadOnly();
    public BoardSkin ActiveSkin => activeSkin != null && skins.Contains(activeSkin) ? activeSkin : FirstAvailable();
    public event Action<BoardSkin> SkinChanged;
    private BoardSkin lastSkin;
    private int lastRevision;

    private BoardSkin FirstAvailable()
    {
        foreach (var skin in skins) if (skin != null) return skin;
        return BoardSkin.Default;
    }
    private void Awake() { lastSkin = ActiveSkin; lastRevision = lastSkin != null ? lastSkin.Revision : 0; }
    private void Update()
    {
        var skin = ActiveSkin;
        if (skin != lastSkin || (skin != null && skin.Revision != lastRevision)) ApplyActiveSkin();
    }
    public void AddSkin(BoardSkin skin)
    {
        if (skin == null) throw new ArgumentNullException(nameof(skin));
        if (!skins.Contains(skin)) skins.Add(skin);
    }
    public void SetSkin(BoardSkin skin)
    {
        if (skin == null || !skins.Contains(skin)) throw new ArgumentException("Select a skin from this manager's library.", nameof(skin));
        activeSkin = skin;
        ApplyActiveSkin();
    }
    public void SetSkin(int index)
    {
        if (index < 0 || index >= skins.Count) throw new ArgumentOutOfRangeException(nameof(index));
        SetSkin(skins[index]);
    }
    [ContextMenu("Apply Active Skin")]
    public void ApplyActiveSkin()
    {
        lastSkin = ActiveSkin;
        lastRevision = lastSkin != null ? lastSkin.Revision : 0;
        if (Application.isPlaying) SkinChanged?.Invoke(lastSkin);
    }
}
