using System;
namespace HPSocket.Common
{
    public static class TimeHelper
    {
        public static long ToUnixTime(this DateTime dateTime) => (dateTime.ToUniversalTime().Ticks - 621355968000000000) / 10000000;

        public static long GetCurrentUnixTime() => ToUnixTime(DateTime.Now);
    }
}
