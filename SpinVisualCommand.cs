using System;
using System.Globalization;
using CommandSystem;
using LabApi.Features.Wrappers;

namespace SpinVisual;

[CommandHandler(typeof(RemoteAdminCommandHandler))]
public sealed class SpinVisualCommand : ICommand
{
    private const float DefaultSpeed = 1440;
    private const string Usage = "用法：spinvisual <玩家编号> on [度/秒] 或 spinvisual <玩家编号> off";

    public string Command => "spinvisual";
    public string[] Aliases => Array.Empty<string>();
    public string Description => "开启或关闭其他玩家可见的旋转效果。";

    public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
    {
        if (!sender.CheckPermission(PlayerPermissions.FacilityManagement, out response))
            return false;

        if (!SpinVisualPlugin.IsAvailable)
        {
            response = "旋转效果未加载。";
            return false;
        }

        if (arguments.Count < 2 || !int.TryParse(arguments.At(0), out int playerId))
        {
            response = Usage;
            return false;
        }

        Player? player = Player.Get(playerId);
        if (player == null || player.IsDestroyed)
        {
            response = "找不到该玩家。";
            return false;
        }

        switch (arguments.At(1).ToLowerInvariant())
        {
            case "on" when arguments.Count <= 3:
                float speed = DefaultSpeed;
                if (arguments.Count == 3 &&
                    (!float.TryParse(arguments.At(2), NumberStyles.Float, CultureInfo.InvariantCulture, out speed)
                     || !(speed > 0) || float.IsInfinity(speed)))
                {
                    response = "旋转速度须为大于 0 的有限数值。";
                    return false;
                }

                if (!SpinVisualPlugin.TryStart(player, speed))
                {
                    response = "该玩家当前无法开启旋转效果。";
                    return false;
                }

                response = $"已开启 {player.Nickname} 的旋转效果。";
                return true;
            case "off" when arguments.Count == 2:
                SpinVisualPlugin.Stop(player);
                response = $"已关闭 {player.Nickname} 的旋转效果。";
                return true;
            default:
                response = Usage;
                return false;
        }
    }
}
