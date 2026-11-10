namespace CarRental;

// Сервіс філії: бронювання, видача, повернення авто.
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
    private const double DefaultDailyRate = 32;

    private static readonly Dictionary<string, double> DailyRates = new()
    {
        ["ECONOMY"] = 32,
        ["COMPACT"] = 41,
        ["SUV"] = 68,
        ["PREMIUM"] = 95,
    };

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
        if (!CanBeRented(car, days))
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
        // Quote дає тижневу знижку лише з 8-ї доби (зафіксовано тестом)
        double cost = RentCost(days, estKm, DailyRate(car.CarClass), WeeklyDiscountFromDays + 1);
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
        int days = BilledDays(r.StartDate, now);
        int km = returnKm - r.StartKm;
        double cost = RentCost(days, km, DailyRate(car.CarClass), WeeklyDiscountFromDays);
        if (IsSummerSurcharge(car, now))
            cost = cost * SummerSurchargeFactor;
        double fuel = FuelCharge(r, fuelPercent);
        int lateHours = (int)(now - r.PlannedReturn).TotalHours;
        double late = LateFee(lateHours, DailyRate(car.CarClass));
        lastLateHours = lateHours;
        double dmg = DamageCharge(damaged, damageCost, r.Insurance);
        double ins = days * GetInsuranceRate(r.Insurance);
        double disc = c.LoyaltyDiscount();
        lastDiscount = disc;
        cost = cost - cost * disc;
        double total = Math.Round(cost + fuel + late + dmg + ins, 2);
        r.Days = days;
        r.Total = total;
        CompleteRental(r, returnKm, fuelPercent, damaged);
        log.Write("CLOSE " + r.Number + " total=" + total);
        if (printReceipt)
            printer.PrintReceipt(r, new ReturnCharges(cost, fuel, late, dmg, ins));
        return total;
    }

    // повернули в той же день = 1 доба
    private static int BilledDays(DateTime startDate, DateTime moment)
    {
        int days = (moment.Date - startDate.Date).Days;
        if (days < 1)
            days = 1;
        return days;
    }

    // пальне: доливаємо до рівня видачі + 15 за заправку
    private static double FuelCharge(Rental r, int fuelPercent)
    {
        double fuel = 0;
        if (fuelPercent < r.StartFuel)
        {
            double litersToRefill = (r.StartFuel - fuelPercent) * r.Car.TankLiters / FullTankPercent;
            fuel = litersToRefill * Settings.FuelPrice + RefuelFee;
        }
        return fuel;
    }

    // late: first hour free, up to 3 h - 12 per hour, then a day per started 24 h
    private static double LateFee(int lateHours, double dailyRate)
    {
        double late = 0;
        if (lateHours > HourlyLateHours)
            late = (lateHours / HoursPerDay + 1) * dailyRate;
        else if (lateHours > FreeLateHours)
            late = lateHours * LateFeePerHour;
        return late;
    }

    private static double DamageCharge(bool damaged, double damageCost, string insurance)
    {
        if (!damaged || insurance == "FULL")
            return 0;
        if (insurance == "BASIC" && damageCost > BasicInsuranceExcess)
            return BasicInsuranceExcess; // франшиза базової страховки
        return damageCost;
    }

    // Стан після повернення: оренда закрита, авто й клієнт оновлені.
    private static void CompleteRental(Rental r, int returnKm, int fuelPercent, bool damaged)
    {
        r.EndKm = returnKm;
        r.Status = RentalStatus.Closed;
        r.Car.Mileage = returnKm;
        r.Car.FuelPercent = fuelPercent;
        r.Car.Available = !damaged; // пошкоджене авто - на СТО
        r.Customer.RentalsCount++;
        r.Customer.AddPoints((int)(r.Total / 10));
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

    // літо (червень-серпень): великі машини +15%
    private static bool IsSummerSurcharge(Car car, DateTime moment)
    {
        return car.IsBigCar() && moment.Month >= SummerFirstMonth && moment.Month <= SummerLastMonth;
    }

    private static bool CanBeRented(Car car, int days)
    {
        return days > 0 && days <= MaxRentalDays && car.Available;
    }

    // Тариф за добу за класом авто; невідомий клас - як ECONOMY
    private static double DailyRate(string carClass)
    {
        if (carClass != null && DailyRates.TryGetValue(carClass, out double rate))
            return rate;
        return DefaultDailyRate;
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
}
