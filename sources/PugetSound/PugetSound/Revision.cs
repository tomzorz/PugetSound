namespace PugetSound
{
    public static class Revision
    {
        private const int Year = 2026;

        private const int Month = 06;

        private const int Day = 27;

        private const string DailyRevision = "beta";

        public static string CssQuery { get; } = $"{Year}{Month:D2}{Day:D2}{DailyRevision}";

        public static string Footer { get; } = $"{Year}-{Month:D2}-{Day:D2}-{DailyRevision}";
    }
}
