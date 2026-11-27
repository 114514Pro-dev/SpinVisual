# SpinVisual

SpinVisual rotates a player's character as seen by other clients. The player's first-person camera still follows their mouse.

Build with a .NET SDK that supports C# 12. Point the build at the SCP:SL dedicated server's Managed directory and an existing Harmony DLL:

```powershell
dotnet build SpinVisual.csproj -c Release "-p:SCP_SL_MANAGED=C:\path\to\SCPSL_Data\Managed" "-p:HARMONY_DLL=C:\path\to\0Harmony.dll"
```

Install `bin/Release/net48/SpinVisual.dll` in the target port's LabAPI plugins directory. That port must be able to load `0Harmony.dll` from its LabAPI dependencies.

In Remote Admin, use an account with `FacilityManagement` and enter one of these commands:

```text
spinvisual <player-id|all> on [degrees-per-second [0|1]]
spinvisual <player-id|all> off
```

The default speed is 1440 degrees per second (4 turns per second). The command accepts any positive, finite speed. Running `on` again updates the speed. Role changes, disconnects, round restarts, and plugin disable clear the effect.

After the speed, pass `1` to preserve the player's normal first-person view for spectators currently following that player. Those spectators still see other players spin. Pass `0` to show the spin even when someone is following that player. A new spin defaults to `0`. Omit the option to keep an active spin's current setting.

For example, `spinvisual all on 2400 1` starts the effect for all currently eligible players. Each spectator sees the normal first-person view of the player they follow and sees everyone else spin. `spinvisual all off` stops all active spins.
