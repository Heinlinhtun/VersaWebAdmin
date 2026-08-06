namespace VersaWebAdmin.Models;

public sealed record SystemMetricCardViewModel(
    string Title,
    string Subtitle,
    string Key,
    double Percent,
    long Used,
    long Total);
