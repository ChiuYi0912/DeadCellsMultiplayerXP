using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using dc.en;
using dc.pr;
using DeadCellsMultiplayerX.Server.Core;

namespace DeadCellsMultiplayerX.Server.World.Authority
{
    public class ServerHero : KingSkin
    {
        public ServerHero(Level lvl, int x, int y) : base(lvl, x, y)
        {

        }

        /// <summary>
        /// 更新完后获取信息
        /// </summary>
        public override void postUpdate()
        {
            base.postUpdate();
        }

        /// <summary>
        /// 帧更新之前写入
        /// </summary>
        public override void fixedUpdate()
        {
            base.fixedUpdate();
        }
    }
}