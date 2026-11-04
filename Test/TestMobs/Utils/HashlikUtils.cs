using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using dc;
using Hashlink.Reflection.Types;
using Serilog;

namespace TestMobs.Utils
{
    public static class HashlikUtils
    {
        public static void InvokeHashlinkMethod(Entity self, string methodName, string? stopAtTypeName = null, params object[] extraArgs)
        {
            var type = (HashlinkObjectType)self.HashlinkObj.Type;

            if (stopAtTypeName is null)
                type = type.Super;
            else
                while (type is not null && type.Name != stopAtTypeName)
                    type = type.Super;

            var fn = type?.FindProto(methodName)?.Function;
            if (fn is null)
                return;

            extraArgs ??= Array.Empty<object>();
            var args = new object[extraArgs.Length + 1];
            args[0] = self;
            Array.Copy(extraArgs, 0, args, 1, extraArgs.Length);

            fn.DynamicInvoke(args);
        }
    }
}