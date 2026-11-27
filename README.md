# SpinVisual

SpinVisual rotates a player's character as seen by other clients. The player's first-person camera still follows their mouse.

Build with a .NET SDK that supports C# 12. Point the build at the SCP:SL dedicated server's Managed directory and an existing Harmony DLL:

```powershell
dotnet build SpinVisual.csproj -c Release "-p:SCP_SL_MANAGED=C:\path\to\SCPSL_Data\Managed" "-p:HARMONY_DLL=C:\path\to\0Harmony.dll"
```

Install `bin/Release/net48/SpinVisual.dll` in the target port's LabAPI plugins directory. That port must be able to load `0Harmony.dll` from its LabAPI dependencies.

In Remote Admin, use an account with `FacilityManagement` and enter one of these commands:

```text
spinvisual <player-id> on
spinvisual <player-id> on <degrees-per-second>
spinvisual <player-id> off
```

The default speed is 1440 degrees per second (4 turns per second). The command accepts any positive, finite speed, with no configured maximum. Running `on` again updates the speed. Role changes, disconnects, round restarts, and plugin disable clear the effect.
