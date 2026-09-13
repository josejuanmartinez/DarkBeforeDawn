# Rules harness

Runs the pure-rules checks in `Tests/*.cs` (the `eval_file` scripts that only touch `MatchRules`,
`CardData` and `RegionMap`) outside Unity, against the assembly `dotnet build Assembly-CSharp.csproj`
produces in `Temp/bin/Debug`. Checks that need the Editor (Play mode, `Resources`, prefabs) cannot run here.

    dotnet build Assembly-CSharp.csproj -nologo -v q            # from the project root
    cd AgentScripts/RulesHarness
    python wrap.py MatchRulesChecks MatchEdgeChecks MatchStageFeedbackChecks DestinationChecks EndTurnManaChecks ManaTimingChecks SettlementChecks
    dotnet build -nologo -v q && dotnet run --no-build

`wrap.py` wraps each script as a static method under `Generated/`; `Program.cs` runs them with a
15-second hang guard. `UnityManaged` in the csproj points at the installed Editor; adjust it when
the Editor version changes.
