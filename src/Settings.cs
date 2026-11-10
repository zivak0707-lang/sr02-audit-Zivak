namespace CarRental;

// Налаштування філії (міняємо руками перед релізом)
public static class Settings
{
    public static double FuelPrice = 2.10;   // EUR per liter, A-95, update every month
    public static string Currency = "EUR";
    public static bool PrintVat = false;     // VAT line on receipts is off since 2023
    public static string AuditFile = Path.Combine(Path.GetTempPath(), "carrental.log");
}
