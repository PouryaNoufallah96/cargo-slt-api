namespace Utilities.Utilities
{
    public static class TimeExtension
    {
        public static long ToTimeStamp(this DateTime dateTime)
            => (dateTime.Ticks - 621355968000000000) / 10000000;

        public static DateTime ToDateTime(this long timeStamp, bool haveDifference = false)
            => haveDifference
                ? new DateTime(1970, 1, 1, 3, 30, 0, 0).AddSeconds(timeStamp).ToUniversalTime()
                : new DateTime(1970, 1, 1, 0, 0, 0, 0).AddSeconds(timeStamp).ToUniversalTime();

        public static long? ToTimeStamp(this DateTime? dateTime)
            => dateTime.HasValue ? dateTime.Value.ToTimeStamp() : null;

        public static DateTime? ToDateTime(this long? timeStamp, bool haveDifference = false)
            => timeStamp.HasValue
                ? haveDifference
                    ? new DateTime(1970, 1, 1, 3, 30, 0, 0).AddSeconds(timeStamp.Value).ToUniversalTime()
                    : new DateTime(1970, 1, 1, 0, 0, 0, 0).AddSeconds(timeStamp.Value).ToUniversalTime()
                : null;
    }
}
