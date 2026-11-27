using System;
using System.Globalization;
using CommandSystem;
using LabApi.Features.Wrappers;

namespace SpinVisual;

[CommandHandler(typeof(RemoteAdminCommandHandler))]
public sealed class SpinVisualCommand : ICommand
{
    private const float DefaultSpeed = 1440;
    private const string Usage = "用法：spinvisual <玩家编号|all> on [度/秒 [保留跟随视角0|1]] 或 spinvisual <玩家编号|all> off";

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

        if (arguments.Count < 2)
        {
            response = Usage;
            return false;
        }

        string target = arguments.At(0);
        bool all = target.Equals("all", StringComparison.OrdinalIgnoreCase);
        Player? player = null;
        if (!all)
        {
            if (!int.TryParse(target, out int playerId))
            {
                response = Usage;
                return false;
            }

            player = Player.Get(playerId);
            if (player == null || player.IsDestroyed)
            {
                response = "找不到该玩家。";
                return false;
            }
        }

        switch (arguments.At(1).ToLowerInvariant())
        {
            case "on" when arguments.Count <= 4:
                float speed = DefaultSpeed;
                if (arguments.Count >= 3 &&
                    (!float.TryParse(arguments.At(2), NumberStyles.Float, CultureInfo.InvariantCulture, out speed)
                     || !(speed > 0) || float.IsInfinity(speed)))
                {
                    response = "旋转速度须为大于 0 的有限数值。";
                    return false;
                }

                bool? preserveSpectatedView = null;
                if (arguments.Count == 4)
                {
                    string option = arguments.At(3);
                    if (option is not ("0" or "1"))
                    {
                        response = "保留跟随视角选项须为 0 或 1。";
                        return false;
                    }

                    preserveSpectatedView = option == "1";
                }

                if (player == null)
                {
                    int started = SpinVisualPlugin.StartAll(speed, preserveSpectatedView);
                    response = started > 0 ? $"已开启 {started} 名玩家的旋转效果。" : "当前没有可开启旋转效果的玩家。";
                    return started > 0;
                }

                if (!SpinVisualPlugin.TryStart(player, speed, preserveSpectatedView))
                {
                    response = "该玩家当前无法开启旋转效果。";
                    return false;
                }

                response = $"已开启 {player.Nickname} 的旋转效果。";
                return true;
            case "off" when arguments.Count == 2:
                if (player == null)
                {
                    SpinVisualPlugin.StopAll();
                    response = "已关闭所有玩家的旋转效果。";
                    return true;
                }

                SpinVisualPlugin.Stop(player);
                response = $"已关闭 {player.Nickname} 的旋转效果。";
                return true;
            default:
                response = Usage;
                return false;
        }
    }
}
