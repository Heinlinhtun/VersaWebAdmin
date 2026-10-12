using Versa.POS.Services;

static void Equal<T>(T expected, T actual)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new Exception($"Expected {expected}, got {actual}");
}

var selected = new DateTime(2026, 10, 5);
var midnight = BusinessDayService.CreateRange(selected, selected, TimeSpan.Zero);
Equal(selected, midnight.Start);
Equal(selected.AddDays(1).AddTicks(-1), midnight.End);
// The customer's October 4 sale must be excluded from October 5.
Equal(false, new DateTime(2026, 10, 4, 17, 52, 50) >= midnight.Start);
Equal(true, new DateTime(2026, 10, 5, 11, 2, 48) <= midnight.End);
Equal(selected, BusinessDayService.GetCurrentBusinessDate(selected.AddHours(23), TimeSpan.Zero));

var cutoff = TimeSpan.FromHours(20);
var custom = BusinessDayService.CreateRange(selected, selected, cutoff);
Equal(new DateTime(2026, 10, 4, 20, 0, 0), custom.Start);
Equal(new DateTime(2026, 10, 5, 20, 0, 0).AddTicks(-1), custom.End);
Equal(selected, BusinessDayService.GetCurrentBusinessDate(selected.AddHours(19), cutoff));
Equal(selected.AddDays(1), BusinessDayService.GetCurrentBusinessDate(selected.AddHours(20), cutoff));

var reversed = BusinessDayService.CreateRange(selected.AddDays(1), selected, TimeSpan.Zero);
Equal(selected, reversed.Start);
Equal(selected.AddDays(2).AddTicks(-1), reversed.End);
Console.WriteLine("Business-day regression checks passed.");
