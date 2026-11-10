namespace CarRental;

// Друк чеків і звітів (принтер у філії - консоль)
public class InvoicePrinter
{
    public void PrintFleet(List<Car> cars)
    {
        Console.WriteLine("Fleet:");
        foreach (Car c in cars)
            Console.WriteLine("  " + c.Plate + "  " + c.Model.PadRight(16) + c.CarClass.PadRight(9)
                + c.Mileage + " km  " + (c.Available ? "available" : "on rent"));
    }

    public void PrintReceipt(Rental r, ReturnCharges charges)
    {
        Console.WriteLine("  ---- Receipt " + r.Number + ": " + r.Customer.Name
            + ", " + r.Car.Model + " (" + r.Car.Plate + ")");
        Console.WriteLine("  Days: " + r.Days + ", km: " + (r.EndKm - r.StartKm)
            + " (included " + r.Days * 250 + ")");
        Console.WriteLine("  Rent: " + charges.Rent.ToString("F2") + "  Fuel: "
            + charges.Fuel.ToString("F2") + "  Late: " + charges.Late.ToString("F2"));
        Console.WriteLine("  Damage: " + charges.Damage.ToString("F2") + "  Insurance: "
            + charges.Insurance.ToString("F2") + " (" + InsuranceName(r.Insurance) + ")");
        if (Settings.PrintVat)
            Console.WriteLine("  incl. VAT: " + (r.Total / 6).ToString("F2"));
        Console.WriteLine("  TOTAL: " + r.Total.ToString("F2") + " " + Settings.Currency);
    }

    public string InsuranceName(string code)
    {
        switch (code)
        {
            case "BASIC": return "basic cover, excess 300";
            case "FULL": return "full cover";
            case "NONE": return "no cover";
        }
        return code;
    }

    // звіт за день: усі оренди, лічильники, виручка
    public void PrintSummary(List<Rental> rentals)
    {
        Console.WriteLine("-- Day summary --");
        foreach (Rental r in rentals)
            Console.WriteLine("  " + r.Describe() + "  " + r.Total.ToString("F2"));
        int open = 0, closed = 0, cancelled = 0;
        foreach (Rental r in rentals)
        {
            if (r.Status == RentalStatus.Closed)
                closed++;
            else if (r.Status == RentalStatus.Cancelled)
                cancelled++;
            else
                open++;
        }
        double sum = 0;
        foreach (Rental r in rentals)
            sum = sum + r.Total; // у незакритих Total = 0
        Console.WriteLine("Open: " + open + ", closed: " + closed + ", cancelled: " + cancelled);
        Console.WriteLine("Revenue: " + sum.ToString("F2") + " " + Settings.Currency);
    }
}
