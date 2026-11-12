using System.Diagnostics;
using dc.tool;
using Hashlink.Proxy.Clousre;
using ModCore.Utilities;

namespace DeadCellsMultiplayerX.Client.Integration.Hooks
{
    /// <summary>
    /// 会话存档 Hook：把大厅下发的存档字节回放给引擎，并屏蔽 Steam 云存档。
    /// 存档字节通过委托读取，适配器不持有会话。
    /// </summary>
    internal sealed class GuestSaveHookAdapter
    {
        private readonly Func<byte[]?> saveData;

        public GuestSaveHookAdapter(Func<byte[]?> saveData)
        {
            this.saveData = saveData
                ?? throw new ArgumentNullException(nameof(saveData));
        }

        public void Save(Hook__Save.orig_save orig, dc.User u, bool onlyGameData)
        {
        }

        public dc.haxe.io.Bytes FileGetBytes(
            Hook__File.orig_getBytes orig,
            dc.String file)
        {
            var fn = file.ToString();
            if (fn.StartsWith("user"))
            {
                var data = saveData();
                Debug.Assert(data != null);

                var bytes = dc.haxe.io.Bytes.Class.alloc(data.Length);
                data.CopyTo(bytes.AsSpan());
                return bytes;
            }

            return orig(file);
        }

        public bool? GetSteamCloudStatus(HashlinkClosure orig)
        {
            return null;
        }

        public void SaveSteamCloudStatus(HashlinkClosure orig)
        {
        }
    }
}
