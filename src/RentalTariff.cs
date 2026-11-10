namespace CarRental;

// Тариф прокату: ціна доби, пробіг, тижнева знижка.
// Єдине місце, де записано формулу прокату.
public static class RentalTariff
{
    public const int IncludedKmPerDay = 250;
    public const int WeeklyDiscountFromDays = 7;
    private const double ExtraKmPrice = 0.25;
    private const double WeeklyDiscountFactor = 0.9;
    private const double DefaultDailyRate = 32;

    private static readonly Dictionary<string, double> DailyRates = new()
    {
        ["ECONOMY"] = 32,
        ["COMPACT"] = 41,
        ["SUV"] = 68,
        ["PREMIUM"] = 95,
    };

    // Тариф за добу за класом авто; невідомий клас - як ECONOMY
    public static double DailyRate(string carClass)
    {
        if (carClass != null && DailyRates.TryGetValue(carClass, out double rate))
            return rate;
        return DefaultDailyRate;
    }

    // Прокат за дні й пробіг; discountFromDays - з якої доби діє тижнева знижка
    public static double RentCost(int days, int km, double rate, int discountFromDays)
    {
        double cost = days * rate + ExtraKmCost(days, km);
        if (days >= discountFromDays)
            cost = cost * WeeklyDiscountFactor; // знижка 10% за тиждень
        return cost;
    }

    // 250 км на добу входять у ціну, далі 0.25 за км
    private static double ExtraKmCost(int days, int km)
    {
        int overKm = km - days * IncludedKmPerDay;
        return overKm > 0 ? overKm * ExtraKmPrice : 0;
    }
}
