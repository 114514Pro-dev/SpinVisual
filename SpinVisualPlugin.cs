using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using LabApi.Events.Arguments.PlayerEvents;
using LabApi.Events.Handlers;
using LabApi.Features;
using LabApi.Features.Wrappers;
using LabApi.Loader.Features.Plugins;
using PlayerRoles.FirstPersonControl;
using PlayerRoles.FirstPersonControl.NetworkMessages;
using RelativePositioning;
using UnityEngine;
using Logger = LabApi.Features.Console.Logger;

namespace SpinVisual;

public sealed class SpinVisualPlugin : Plugin
{
    private static readonly Dictionary<ReferenceHub, Spin> Spins = new();
    private readonly Harmony _harmony = new("com.cement.scpsl.spin-visual");
    private MethodInfo? _target;

    public override string Name => "SpinVisual";
    public override string Description => "Player rotation visible to other clients.";
    public override string Author => "Cement";
    public override Version Version => new(1, 0, 0);
    public override Version RequiredApiVersion => new(LabApiProperties.CompiledVersion);

    internal static bool IsAvailable { get; private set; }

    public override void Enable()
    {
        try
        {
            _target = AccessTools.Method(typeof(FpcServerPositionDistributor), "GetNewSyncData",
                new[] { typeof(ReferenceHub), typeof(ReferenceHub), typeof(FirstPersonMovementModule), typeof(bool) })
                ?? throw new MissingMethodException(nameof(FpcServerPositionDistributor), "GetNewSyncData");
            var transpiler = new HarmonyMethod(typeof(SpinVisualPlugin), nameof(TranspileSyncData));
            _harmony.Patch(_target, transpiler: transpiler);

            PlayerEvents.ChangedRole += OnChangedRole;
            PlayerEvents.Left += OnLeft;
            ServerEvents.RoundRestarted += Clear;
            IsAvailable = true;
        }
        catch (Exception exception)
        {
            Disable();
            Logger.Error($"[SpinVisual] Failed to initialize observer rotation: {exception.GetBaseException()}");
        }
    }

    public override void Disable()
    {
        IsAvailable = false;
        PlayerEvents.ChangedRole -= OnChangedRole;
        PlayerEvents.Left -= OnLeft;
        ServerEvents.RoundRestarted -= Clear;
        Clear();
        if (_target != null)
        {
            _harmony.Unpatch(_target, HarmonyPatchType.Transpiler, _harmony.Id);
            _target = null;
        }
    }

    internal static bool TryStart(Player player, float degreesPerSecond)
    {
        ReferenceHub hub = player.ReferenceHub;
        if (!player.IsAlive || hub.roleManager.CurrentRole is not IFpcRole role || !role.FpcModule.ModuleReady)
            return false;

        FpcMouseLook mouseLook = role.FpcModule.MouseLook;
        double now = Time.timeAsDouble;
        float yaw = Spins.TryGetValue(hub, out Spin existing) && ReferenceEquals(mouseLook, existing.MouseLook)
            ? existing.GetYaw(now)
            : mouseLook.CurrentHorizontal;
        Spins[hub] = new Spin(mouseLook, yaw, now, degreesPerSecond);
        return true;
    }

    internal static void Stop(Player player) => Spins.Remove(player.ReferenceHub);

    private static void OnChangedRole(PlayerChangedRoleEventArgs ev) => Stop(ev.Player);
    private static void OnLeft(PlayerLeftEventArgs ev) => Stop(ev.Player);
    private static void Clear() => Spins.Clear();

    private static IEnumerable<CodeInstruction> TranspileSyncData(IEnumerable<CodeInstruction> instructions)
    {
        ConstructorInfo constructor = AccessTools.Constructor(typeof(FpcSyncData),
            new[] { typeof(FpcSyncData), typeof(PlayerMovementState), typeof(bool), typeof(RelativePosition), typeof(FpcMouseLook) })
            ?? throw new MissingMethodException(nameof(FpcSyncData), ".ctor");
        MethodInfo factory = AccessTools.Method(typeof(SpinVisualPlugin), nameof(CreateSyncData));
        int replacements = 0;

        foreach (CodeInstruction instruction in instructions)
        {
            if (!instruction.Is(OpCodes.Newobj, constructor))
            {
                yield return instruction;
                continue;
            }

            replacements++;
            // GetNewSyncData takes the observer first and the player being synchronized second.
            yield return new CodeInstruction(OpCodes.Ldarg_1).MoveLabelsFrom(instruction).MoveBlocksFrom(instruction);
            yield return new CodeInstruction(OpCodes.Call, factory);
        }

        if (replacements != 1)
            throw new InvalidOperationException($"Expected one observer sync constructor, found {replacements}.");
    }

    private static FpcSyncData CreateSyncData(FpcSyncData previous, PlayerMovementState movementState,
        bool grounded, RelativePosition position, FpcMouseLook mouseLook, ReferenceHub target)
    {
        if (!Spins.TryGetValue(target, out Spin spin) || !ReferenceEquals(mouseLook, spin.MouseLook))
            return new FpcSyncData(previous, movementState, grounded, position, mouseLook);

        float originalYaw = mouseLook.CurrentHorizontal;
        try
        {
            // Use the native constructor so its per-observer delta cache tracks the outgoing yaw.
            mouseLook.CurrentHorizontal = spin.GetYaw(Time.timeAsDouble);
            return new FpcSyncData(previous, movementState, grounded, position, mouseLook);
        }
        finally
        {
            mouseLook.CurrentHorizontal = originalYaw;
        }
    }

    private readonly struct Spin
    {
        internal Spin(FpcMouseLook mouseLook, float yaw, double startedAt, float degreesPerSecond)
        {
            MouseLook = mouseLook;
            Yaw = yaw;
            StartedAt = startedAt;
            DegreesPerSecond = degreesPerSecond;
        }

        internal FpcMouseLook MouseLook { get; }
        private float Yaw { get; }
        private double StartedAt { get; }
        private float DegreesPerSecond { get; }

        internal float GetYaw(double now) => (float)((Yaw + (now - StartedAt) * DegreesPerSecond) % 360d);
    }
}
