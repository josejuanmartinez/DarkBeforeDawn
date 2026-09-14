// Unity CLI eval_file in Edit mode. Replaces the board's spatial layout, not its match rules.
if (UnityEngine.Application.isPlaying) throw new System.InvalidOperationException("Apply in Edit mode.");
var skin = BoardSkin.Default;
UnityEditor.Undo.RecordObject(skin,"Open fantasy game table");
skin.openTable = true;
skin.displayName = "The Twilight Table";
skin.colors.atmosphere = new UnityEngine.Color(.012f,.025f,.025f,.42f);
skin.chrome.headerLabels[0].size=27;
skin.chrome.headerLabels[0].bounds=new BoardSkin.Bounds(.02f,0,.32f,1);
skin.chrome.header=new BoardSkin.Bounds(0,.928f,1,1);
skin.players.opponentAvatar=new BoardSkin.Bounds(.02f,.665f,.157f,.925f);
skin.players.humanAvatar=new BoardSkin.Bounds(.02f,.105f,.157f,.365f);
skin.players.opponentMaterials=new BoardSkin.Bounds(.013f,.552f,.164f,.675f);
skin.players.humanMaterials=new BoardSkin.Bounds(.013f,.005f,.164f,.119f);
skin.players.amountSize=24;
skin.players.materialSize=10;
skin.players.headingSize=11;
skin.chrome.handGap=12;
skin.zones=new [] {
    new BoardSkin.ZoneStyle(BoardZoneId.OpponentLands,"ENEMY LANDS",.24f,.795f,.74f,.928f,SkinColorRole.Gold),
    new BoardSkin.ZoneStyle(BoardZoneId.OpponentArmies,"THE ENEMY COMPANY",.20f,.57f,.78f,.795f,SkinColorRole.Gold),
    new BoardSkin.ZoneStyle(BoardZoneId.HumanArmies,"YOUR COMPANY",.20f,.345f,.78f,.565f,SkinColorRole.Teal),
    new BoardSkin.ZoneStyle(BoardZoneId.HumanLands,"YOUR LANDS",.24f,.245f,.74f,.345f,SkinColorRole.Teal),
    new BoardSkin.ZoneStyle(BoardZoneId.Hand,"",.185f,.002f,.803f,.258f,SkinColorRole.Teal),
    new BoardSkin.ZoneStyle(BoardZoneId.Environment,"THE WORLD",.025f,.39f,.155f,.55f,SkinColorRole.Gold),
    new BoardSkin.ZoneStyle(BoardZoneId.OpponentSettlements,"ENEMY DESTINATION",.823f,.70f,.922f,.921f,SkinColorRole.Gold),
    new BoardSkin.ZoneStyle(BoardZoneId.HumanSettlements,"YOUR DESTINATION",.823f,.43f,.922f,.654f,SkinColorRole.Teal),
    new BoardSkin.ZoneStyle(BoardZoneId.OpponentDiscard,"DISCARD",.932f,.742f,.99f,.901f,SkinColorRole.Gold),
    new BoardSkin.ZoneStyle(BoardZoneId.HumanDiscard,"DISCARD",.932f,.472f,.99f,.631f,SkinColorRole.Teal),
    new BoardSkin.ZoneStyle(BoardZoneId.OpponentVictory,"VICTORY",.932f,.651f,.99f,.735f,SkinColorRole.Gold),
    new BoardSkin.ZoneStyle(BoardZoneId.HumanVictory,"VICTORY",.932f,.38f,.99f,.466f,SkinColorRole.Teal)
};
UnityEditor.EditorUtility.SetDirty(skin);
UnityEditor.AssetDatabase.SaveAssets();
var presentation=UnityEngine.Object.FindFirstObjectByType<BoardPresentation>();
if (presentation != null) { presentation.enabled=false; presentation.enabled=true; }
return "Open battlefield, portrait pieces, fanned hand and separated player stations saved.";
