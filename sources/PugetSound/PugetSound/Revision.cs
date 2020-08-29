namespace PugetSound
{
    public static class Revision
    {
        private const int Year = 2020;

        private const int Month = 8;

        private const int Day = 29;

        private const string DailyRevision = "delta";

        public static string CssQuery { get; } = $"{Year}{Month:D2}{Day:D2}{DailyRevision}";

        public static string Footer { get; } = $"{Year}-{Month:D2}-{Day:D2}-{DailyRevision}";
    }
}
