#if SPINVISUAL_PLAYTEST
using System;
using System.Linq;
using CommandSystem;
using LabApi.Features.Wrappers;
using PlayerRoles.FirstPersonControl;
using UnityEngine;

namespace SpinVisual;

[CommandHandler(typeof(RemoteAdminCommandHandler))]
public sealed class SurfaceProbeCommand : ICommand
{
    public string Command => "spinprobe";
    public string[] Aliases => Array.Empty<string>();
    public string Description => "检查旋转与倒立测试场景。";

    public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
    {
        if (!sender.CheckPermission(PlayerPermissions.FacilityManagement, out response))
            return false;

        if (arguments.Count == 2 && arguments.At(0) == "open")
        {
            string name = arguments.At(1);
            Door? door = Door.List.SingleOrDefault(candidate => candidate.NameTag == name);
            if (door == null)
            {
                response = "没有找到指定的门。";
                return false;
            }

            door.IsOpened = true;
            response = $"OPEN {name} {door.Position:F2}";
            return true;
        }

        if (arguments.Count < 3 || !int.TryParse(arguments.At(1), out int fromId) ||
            !int.TryParse(arguments.At(2), out int toId) ||
            Player.Get(fromId) is not { } from || Player.Get(toId) is not { } to)
        {
            response = "未找到有效目标。";
            return false;
        }

        if (arguments.At(0) == "face" && from.ReferenceHub.roleManager.CurrentRole is IFpcRole fpc)
        {
            Vector3 origin = from.Camera.position;
            Vector3 at = to.Camera.position - Vector3.up * 0.35f;
            Vector3 euler = Quaternion.LookRotation(at - origin).eulerAngles;
            fpc.FpcModule.MouseLook.CurrentHorizontal = euler.y;
            fpc.FpcModule.MouseLook.CurrentVertical = Mathf.DeltaAngle(0f, euler.x);

            if (Physics.Linecast(origin, at, out RaycastHit hit, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) &&
                hit.collider.GetComponentInParent<ReferenceHub>() != to.ReferenceHub)
            {
                response = $"BLOCKED {hit.collider.name} from={from.Position:F2} to={to.Position:F2} look={from.LookRotation:F2}";
                return false;
            }

            response = $"CLEAR from={from.Position:F2} to={to.Position:F2} look={from.LookRotation:F2}";
            return true;
        }

        if (arguments.At(0) == "flip" && arguments.Count == 4 &&
            arguments.At(3) is ("0" or "1") && to.ConnectionToClient is { } connection)
        {
            Vector3 realScale = from.Scale;
            Vector3 sentScale = arguments.At(3) == "1" ? Vector3.Scale(realScale, new Vector3(-1, -1, 1)) : realScale;
            connection.Send(new SyncedScaleMessages.ScaleMessage(sentScale, from.ReferenceHub));
            response = $"SCALE {sentScale:F2} authoritative={from.Scale:F2}";
            return true;
        }

        response = "未知测试操作。";
        return false;
    }
}
#endif
