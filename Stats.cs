using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChessCom
{
    internal class Stats
    {
        public static int GetQueueCount()
        {
            return ChessApi.GetRequestCount();
        }

        public static int GetProcessedCount()
        {
            return ChessApi.GetProcessedRequestCount();
        }

        public static int GetTooManyConnectionCount()
        {
            return ChessApi.GetTooManyConnectionCount();
        }

        public static double GetHTTPExecutionTime()
        {
            return ChessApi.GetAverageExecutionTime().TotalMilliseconds;
        }
    }
}
