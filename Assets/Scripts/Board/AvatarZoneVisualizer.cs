/// <summary>An avatar is a persistent full card with the board's normal inspection behaviour.</summary>
public sealed class AvatarZoneVisualizer : CardZoneVisualizer
{
    public override bool UsesFullCards => true;
    public int Owner;
    public UnityEngine.UI.Text HealthLabel;
    protected override void LateUpdate()
    {
        base.LateUpdate();
        if (HealthLabel != null)
            HealthLabel.text = (Owner == 0 ? "YOUR AVATAR" : "OPPONENT AVATAR") + " · " +
                (board.Match?.Rules?.Players[Owner].Life ?? 20) + " HEALTH";
    }
}
