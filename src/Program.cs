namespace CarRental;

// Демо-день філії (показуємо новим співробітникам)
public class Program
{
    public static void Main(string[] args)
    {
        Console.WriteLine("=== CarRental branch console - demo day ===");
        RentService svc = new RentService();
        InvoicePrinter printer = new InvoicePrinter();
        printer.PrintFleet(svc.repo.Cars);
        Console.WriteLine("-- Quotes --");
        Console.WriteLine("Fabia 3d 900km: " + svc.Quote(1, "AA1024KB", 3, 900).ToString("F2"));
        Console.WriteLine("SUV 7d 1400km: " + svc.Quote(1, "AA3072KE", 7, 1400).ToString("F2"));
        Console.WriteLine("SUV 8d 1400km: " + svc.Quote(1, "AA3072KE", 8, 1400).ToString("F2"));
        Console.WriteLine("BMW 10d 3100km: " + svc.Quote(2, "AA4096KH", 10, 3100).ToString("F2"));
        Console.WriteLine("BMW for customer 3: " + svc.Quote(3, "AA4096KH", 2, 300));
        Console.WriteLine("Fabia 0 days: " + svc.Quote(1, "AA1024KB", 0, 0));
        ShowReservations(svc);
        Console.WriteLine("-- Returns --");
        svc.Close("R-1001", 42070, 70, false, 0, true);
        svc.Close("r-1002 ", 66100, 75, true, 850, true);
        Console.WriteLine("R-1002 late hours: " + svc.lastLateHours
            + ", loyalty discount: " + svc.lastDiscount);
        double t = svc.Close("R-1005", 87160, 100, false, 0, false); // Clio picked up today
        Console.WriteLine("R-1005 same-day return, total " + t.ToString("F2"));
        t = svc.Close("R-2001", 1000, 100, false, 0, false);
        Console.WriteLine("R-2001: " + t + " (" + svc.lastError + ")");
        Customer olena = svc.repo.FindCustomer(2);
        Console.WriteLine(olena.Name + ": " + olena.Points + " points, level " + olena.Level);
        printer.PrintSummary(svc.repo.Rentals);
    }

    static void ShowReservations(RentService svc)
    {
        Console.WriteLine("-- Reservations --");
        Rental r1 = svc.Reserve(1, "AA5120KI", 0, 4, "none");
        bool ok = svc.Cancel(r1.Number);
        Console.WriteLine("Reserved and cancelled " + r1.Describe() + ": " + ok);
        Rental r2 = svc.Reserve(4, "AA5120KI", 0, 2, "BASIC");
        Console.WriteLine("Customer 4: " + (r2 == null ? "refused" : r2.Number));
        Rental r3 = svc.Reserve(1, "AA5120KI", 0, 2, "FULL");
        svc.Pickup(r3.Number, 87020, 100);
        ok = svc.Cancel(r3.Number);
        Console.WriteLine("Picked up " + r3.Describe() + ", cancel: " + ok);
        Rental r4 = svc.Reserve(2, "AA4096KH", 2, 3, "BASIC");
        Console.WriteLine("Reserved " + r4.Describe() + ", deposit " + r4.Deposit);
        try { svc.Close(r4.Number, 15100, 100, false, 0, false); }
        catch (InvalidOperationException e) { Console.WriteLine("Error: " + e.Message); }
        double est = svc.Extend("R-1003", 2, 29040);
        Console.WriteLine("Extend R-1003 by 2 days, new estimate: " + est.ToString("F2"));
        est = svc.Extend(r4.Number, 1, 0);
        Console.WriteLine("Extend " + r4.Number + ": " + est + " (" + svc.lastError + ")");
    }
}
