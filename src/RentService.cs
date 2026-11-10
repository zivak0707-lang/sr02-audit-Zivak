namespace CarRental;

// Сервіс філії: бронювання, видача, повернення авто.
// Тарифи/добу: ECONOMY 32, COMPACT 41, SUV 68, PREMIUM 95
public class RentService
{
    private InvoicePrinter printer = new InvoicePrinter();
    private AuditLog log = new AuditLog();

    // Назовні - лише читання; змінює їх тільки сам сервіс.
    public RentalRepository repo { get; private set; }
    public string lastError { get; private set; } = "";
    public int lastLateHours { get; private set; }     // for the return screen
    public double lastDiscount { get; private set; }   // for the return screen

    private const int MinDriverAge = 21;
    private const int MinLicenseYears = 2;
    private const int PremiumMinDriverAge = 25;
    private const int MaxRentalDays = 30;
    private const double BigCarDeposit = 500;
    private const double StandardDeposit = 300;
    private const int IncludedKmPerDay = 250;
    private const double ExtraKmPrice = 0.25;
    private const int WeeklyDiscountFromDays = 7;
    private const double WeeklyDiscountFactor = 0.9;
    private const double SummerSurchargeFactor = 1.15;
    private const int SummerFirstMonth = 6;
    private const int SummerLastMonth = 8;
    private const double FullTankPercent = 100.0;
    private const double RefuelFee = 15;
    private const int FreeLateHours = 1;
    private const int HourlyLateHours = 3;
    private const int LateFeePerHour = 12;
    private const int HoursPerDay = 24;
    private const double BasicInsuranceExcess = 300;
    private const double ExtendSuvDailyRate = 65;

    public RentService()
    {
        repo = new RentalRepository();
    }

    // Шов для тестів: поточний момент можна підмінити в нащадку.
    protected virtual DateTime Now()
    {
        return DateTime.Now;
    }

    // Нове бронювання. null - якщо не можна (див. lastError)
    public Rental Reserve(int customerId, string plate, int startInDays, int days,
        string insurance)
    {
        if (DriverRejected(customerId, plate, out Customer cust, out Car car))
            return null;
        if (days <= 0 || days > MaxRentalDays || !car.Available)
        {
            lastError = "Car " + plate + " is not available for " + days + " days";
            return null;
        }
        Rental r = new Rental();
        r.Number = "R-" + RentalRepository.nextNo++;
        r.Customer = cust;
        r.Car = car;
        r.Days = days;
        r.StartDate = Now().Date.AddDays(startInDays);
        r.PlannedReturn = r.StartDate.AddDays(days);
        r.Insurance = insurance == null ? "NONE" : insurance.ToUpper();
        r.Deposit = car.IsBigCar() ? BigCarDeposit : StandardDeposit;
        car.Available = false;   // reserved car is blocked for others
        repo.Rentals.Add(r);
        log.Write("RESERVE " + r.Number + " " + plate);
        return r;
    }

    // Перевірка клієнта й авто (спільна для Reserve і Quote).
    // true - відмова: текст помилки в lastError і на консолі.
    private bool DriverRejected(int customerId, string plate, out Customer cust, out Car car)
    {
        cust = repo.FindCustomer(customerId);
        car = repo.FindCar(plate);
        string err = null;
        if (cust == null || car == null)
            err = "Customer " + customerId + " or car " + plate + " not found";
        else if (cust.DriverAge < MinDriverAge || cust.LicenseYears < MinLicenseYears
            || (cust.DriverAge < PremiumMinDriverAge && car.CarClass == "PREMIUM"))
            err = "Customer " + customerId + " may not drive " + car.CarClass;
        if (err == null)
            return false;
        lastError = err;
        Console.WriteLine("  ! " + err);
        return true;
    }

    public bool Pickup(string rentalNo, int km, int fuelPercent)
    {
        Rental r = FindRental(rentalNo);
        if (r == null)
            return false;
        if (r.Status != RentalStatus.Reserved)
            throw new InvalidOperationException("Rental " + r.Number + " is " + r.Status
                + ", cannot pick up");
        r.StartKm = km;
        r.StartFuel = fuelPercent;
        r.StartDate = Now().Date;
        r.PlannedReturn = Now().Date.AddDays(r.Days);
        r.Status = RentalStatus.Active;
        log.Write("PICKUP " + r.Number + " km=" + km);
        return true;
    }

    public bool Cancel(string rentalNo)
    {
        Rental r = FindRental(rentalNo);
        if (r == null || r.Status != RentalStatus.Reserved)
        {
            lastError = "Rental " + rentalNo + " cannot be cancelled"; // Active - only Close
            return false;
        }
        r.Status = RentalStatus.Cancelled;
        r.Car.Available = true;
        log.Write("CANCEL " + r.Number);
        return true;
    }

    // Попередня ціна - така ж, як при закритті. -1 = помилка
    public double Quote(int customerId, string plate, int days, int estKm)
    {
        if (DriverRejected(customerId, plate, out _, out Car car))
            return -1;
        if (days <= 0)
            return -1;
        double rate = DailyRate(car.CarClass);
        // Quote дає тижневу знижку лише з 8-ї доби (зафіксовано тестом)
        double cost = RentCost(days, estKm, rate, WeeklyDiscountFromDays + 1);
        return Math.Round(cost, 2);
    }

    // Закриття оренди при поверненні авто.
    // Повертає суму до сплати або 0, якщо не знайдено.
    public double Close(string rentalNo, int returnKm, int fuelPercent, bool damaged,
        double damageCost, bool printReceipt)
    {
        Rental r = FindRental(rentalNo);
        if (r == null)
        {
            lastError = "Rental " + rentalNo + " not found";
            return -1;
        }
        if (r.Status != RentalStatus.Active)
            throw new InvalidOperationException("Rental " + r.Number + " is " + r.Status
                + ", only active rentals can be closed");
        Car car = r.Car;
        Customer c = r.Customer;
        DateTime now = Now();
        int days = (now.Date - r.StartDate.Date).Days;
        if (days < 1)
            days = 1; // повернули в той же день = 1 доба
        int km = returnKm - r.StartKm;
        double rate = DailyRate(car.CarClass);
        double cost = RentCost(days, km, rate, WeeklyDiscountFromDays);
        // літо (червень-серпень): великі машини +15%
        if (car.IsBigCar() && now.Month >= SummerFirstMonth && now.Month <= SummerLastMonth)
            cost = cost * SummerSurchargeFactor;
        // пальне: доливаємо до рівня видачі + 15 за заправку
        double fuel = 0;
        if (fuelPercent < r.StartFuel)
            fuel = (r.StartFuel - fuelPercent) * car.TankLiters / FullTankPercent * Settings.FuelPrice
                + RefuelFee;
        // late: first hour free, up to 3 h - 12 per hour, then a day per started 24 h
        int lateHours = (int)(now - r.PlannedReturn).TotalHours;
        double late = 0;
        if (lateHours > HourlyLateHours)
            late = (lateHours / HoursPerDay + 1) * rate;
        else if (lateHours > FreeLateHours)
            late = lateHours * LateFeePerHour;
        lastLateHours = lateHours;
        double dmg = 0;
        if (damaged)
        {
            if (r.Insurance != "FULL")
            {
                dmg = damageCost;
                if (r.Insurance == "BASIC")
                    if (dmg > BasicInsuranceExcess)
                        dmg = BasicInsuranceExcess; // франшиза базової страховки
            }
        }
        double ins = days * GetInsuranceRate(r.Insurance);
        double disc = GetLoyaltyDiscount(c);
        lastDiscount = disc;
        cost = cost - cost * disc;
        double total = Math.Round(cost + fuel + late + dmg + ins, 2);
        r.EndKm = returnKm;
        r.Days = days;
        r.Total = total;
        r.Status = RentalStatus.Closed;
        car.Mileage = returnKm;
        car.FuelPercent = fuelPercent;
        car.Available = !damaged; // пошкоджене авто - на СТО
        c.RentalsCount++;
        c.AddPoints((int)(total / 10));
        log.Write("CLOSE " + r.Number + " total=" + total);
        if (printReceipt)
            printer.PrintReceipt(r, cost, fuel, late, dmg, ins);
        return total;
    }

    // Продовження: нова вартість прокату за весь період
    public double Extend(string rentalNo, int extraDays, int kmSoFar)
    {
        Rental r = FindRental(rentalNo);
        if (r == null || r.Status != RentalStatus.Active || extraDays <= 0)
        {
            lastError = "Only active rentals can be extended";
            return 0;
        }
        DateTime newReturn = r.PlannedReturn.AddDays(extraDays);
        int totalDays = (newReturn.Date - r.StartDate.Date).Days;
        if (totalDays > MaxRentalDays)
            return 0; // більше 30 днів - тільки через офіс
        int km = kmSoFar - r.StartKm;
        Car car = r.Car;
        // Extend досі рахує SUV за старим тарифом 65 (зафіксовано тестом)
        double rate = car.CarClass == "SUV" ? ExtendSuvDailyRate : DailyRate(car.CarClass);
        double cost = RentCost(totalDays, km, rate, WeeklyDiscountFromDays);
        r.PlannedReturn = newReturn;
        r.Days = totalDays;
        log.Write("EXTEND " + r.Number + " +" + extraDays);
        return Math.Round(cost, 2);
    }

    // Прокат за дні й пробіг - єдина формула для Quote, Close і Extend.
    // discountFromDays - з якої доби діє тижнева знижка.
    private static double RentCost(int days, int km, double rate, int discountFromDays)
    {
        double baseCost = days * rate;
        int overKm = km - days * IncludedKmPerDay;
        // 250 км на добу входять у ціну, далі 0.25 за км
        double kmCost = overKm > 0 ? overKm * ExtraKmPrice : 0;
        double cost = baseCost + kmCost;
        if (days >= discountFromDays)
            cost = cost * WeeklyDiscountFactor; // знижка 10% за тиждень
        return cost;
    }

    // Тариф за добу за класом авто
    private static double DailyRate(string carClass)
    {
        switch (carClass)
        {
            case "ECONOMY": return 32;
            case "COMPACT": return 41;
            case "SUV": return 68;
            case "PREMIUM": return 95;
            default: return 32;
        }
    }

    // TEMP: з планшета номер приходить малими літерами
    public Rental FindRental(string no)
    {
        if (no == null)
            return null;
        foreach (Rental r in repo.Rentals)
        {
            if (r.Number == no.ToUpper().Trim())
                return r;
        }
        return null;
    }

    // ціна страховки за добу (тариф 2021)
    public double GetInsuranceRate(string code)
    {
        switch (code)
        {
            case "BASIC": return 9;
            case "FULL": return 19;
            default: return 0;
        }
    }

    // знижка лояльності - тільки на прокат, не на пальне
    public double GetLoyaltyDiscount(Customer c)
    {
        if (c.Level == "GOLD")
            return 0.10;
        if (c.Level == "SILVER")
            return 0.05;
        if (c.RentalsCount >= 3 && c.Points > 100)
            return 0.02; // "welcome back" від маркетингу
        return 0;
    }
}
