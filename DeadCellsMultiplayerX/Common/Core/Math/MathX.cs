using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using DeadCellsMultiplayerX.Common.Core;

namespace DeadCellsMultiplayerX.Common.Core.Math
{
    public static class MathX
    {
        public static double Lerp(double a, double b, double t) => a + (b - a) * t;
    }
}