namespace PugetSound
{
    public static class Revision
    {
        private const int Year = 2021;

        private const int Month = 04;

        private const int Day = 18;

        private const string DailyRevision = "beta";

        public static string CssQuery { get; } = $"{Year}{Month:D2}{Day:D2}{DailyRevision}";

        public static string Footer { get; } = $"{Year}-{Month:D2}-{Day:D2}-{DailyRevision}";
    }
}
