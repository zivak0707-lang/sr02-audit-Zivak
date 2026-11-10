using CarRental;

namespace Legacy.Tests;

// Тестовий годинник: підміняє шов RentService.Now() (М5, прийом 2).
internal sealed class FixedClockRentService : RentService
{
    public FixedClockRentService(DateTime moment)
    {
        Moment = moment;
    }

    public DateTime Moment { get; set; }

    protected override DateTime Now() => Moment;
}

// Характеризаційні тести: фіксують, як код працює ЗАРАЗ,
// навіть там, де це схоже на помилку (такі місця позначено TD-xx).
public class RentServiceCharacterizationTests
{
    private static readonly DateTime March = new(2026, 3, 5, 11, 0, 0);
    private static readonly DateTime July = new(2026, 7, 15, 11, 0, 0);

    // ---------- Розрахунковий вузол: сума при поверненні авто ----------

    [Fact]
    public void Close_Економ3ДобиВМежахПробігу_Повертає96()
    {
        var svc = new FixedClockRentService(March);
        var r = AddActive(svc, "ECONOMY", 3);

        double total = svc.Close(r.Number, 10600, 100, false, 0, false);

        Assert.Equal(96.0, total);
    }

    [Fact]
    public void Close_ПеревищенняПробігу_Додає025ЗаКм()
    {
        var svc = new FixedClockRentService(March);
        var r = AddActive(svc, "ECONOMY", 3);

        double total = svc.Close(r.Number, 10900, 100, false, 0, false);

        Assert.Equal(133.5, total);
    }

    [Theory]
    [InlineData(6, 192.0)]
    [InlineData(7, 201.6)]
    [InlineData(8, 230.4)]
    public void Close_ТижневаЗнижка_ДієВідСьомоїДоби(int days, double expected)
    {
        var svc = new FixedClockRentService(March);
        var r = AddActive(svc, "ECONOMY", days);

        double total = svc.Close(r.Number, 10000, 100, false, 0, false);

        Assert.Equal(expected, total);
    }

    [Fact]
    public void Close_ПовернувУДеньВидачі_РахуєОднуДобу()
    {
        var svc = new FixedClockRentService(March);
        var r = AddActive(svc, "ECONOMY", 0);

        double total = svc.Close(r.Number, 10000, 100, false, 0, false);

        Assert.Equal(32.0, total);
        Assert.Equal(1, r.Days);
    }

    [Fact]
    public void Close_ПробігМеншийЗаПочатковий_НеДаєВідємноїДоплати()
    {
        var svc = new FixedClockRentService(March);
        var r = AddActive(svc, "ECONOMY", 3);

        double total = svc.Close(r.Number, 9000, 100, false, 0, false);

        Assert.Equal(96.0, total);
    }

    [Fact]
    public void Close_ВеликеАвтоВлітку_Націнка15Відсотків()
    {
        var svc = new FixedClockRentService(July);
        var r = AddActive(svc, "SUV", 3);

        double total = svc.Close(r.Number, 10000, 100, false, 0, false);

        Assert.Equal(234.6, total);
    }

    // 3 год прострочення коштують 36, а 4 год — лише 32 (TD-05).
    [Theory]
    [InlineData(1, 96.0)]
    [InlineData(2, 120.0)]
    [InlineData(3, 132.0)]
    [InlineData(4, 128.0)]
    [InlineData(26, 160.0)]
    public void Close_Прострочення_ШтрафЗалежитьВідГодин(int lateHours, double expected)
    {
        var svc = new FixedClockRentService(March);
        var r = AddActive(svc, "ECONOMY", 3);
        r.PlannedReturn = March.AddHours(-lateHours);

        double total = svc.Close(r.Number, 10000, 100, false, 0, false);

        Assert.Equal(expected, total);
        Assert.Equal(lateHours, svc.lastLateHours);
    }

    [Fact]
    public void Close_ПошкодженняЗБазовоюСтраховкою_НеБільше300()
    {
        var svc = new FixedClockRentService(March);
        var r = AddActive(svc, "ECONOMY", 3, "BASIC");

        double total = svc.Close(r.Number, 10000, 100, true, 850, false);

        Assert.Equal(423.0, total); // 96 + франшиза 300 + страховка 3 × 9
    }

    [Fact]
    public void Close_ПошкодженняЗПовноюСтраховкою_ШкодуНеСплачує()
    {
        var svc = new FixedClockRentService(March);
        var r = AddActive(svc, "ECONOMY", 3, "FULL");

        double total = svc.Close(r.Number, 10000, 100, true, 850, false);

        Assert.Equal(153.0, total); // 96 + страховка 3 × 19
    }

    [Fact]
    public void Close_НеповнийБак_ДоливПлюс15ЗаЗаправку()
    {
        var svc = new FixedClockRentService(March);
        var r = AddActive(svc, "ECONOMY", 3);

        double total = svc.Close(r.Number, 10000, 50, false, 0, false);

        Assert.Equal(153.0, total); // 96 + 20 л × 2,10 + 15
    }

    [Fact]
    public void Close_ЗолотийКлієнт_Знижка10ЛишеНаПрокат()
    {
        var svc = new FixedClockRentService(March);
        var gold = new Customer(77, "Gold Driver", "000", 40, 15) { Level = "GOLD" };
        var r = AddActive(svc, "ECONOMY", 3, "NONE", gold);

        double total = svc.Close(r.Number, 10000, 50, false, 0, false);

        Assert.Equal(143.4, total); // 96 × 0,9 + пальне 57 без знижки
        Assert.Equal(0.10, svc.lastDiscount);
    }

    // ---------- Дублювання формули: три копії рахують по-різному ----------

    // Quote дає тижневу знижку лише з 8-ї доби, Close — з 7-ї (TD-02).
    [Fact]
    public void Quote_7Діб_БезТижневоїЗнижки()
    {
        var svc = new FixedClockRentService(March);

        double quote = svc.Quote(1, "AA1024KB", 7, 0);

        Assert.Equal(224.0, quote);
    }

    // Extend рахує SUV за 65 на добу, а Quote і Close — за 68 (TD-01).
    [Fact]
    public void Extend_SUV_РахуєЗаТарифом65()
    {
        var svc = new FixedClockRentService(March);
        var r = AddActive(svc, "SUV", 2);
        r.PlannedReturn = r.StartDate.AddDays(3);

        double estimate = svc.Extend(r.Number, 1, 10000);

        Assert.Equal(260.0, estimate);
        Assert.Equal(4, r.Days);
    }

    // ---------- Сценарії помилок ----------

    // Коментар над Close обіцяє 0, а метод повертає -1 (D-09).
    [Fact]
    public void Close_НевідомийНомер_ПовертаєМінус1ІЗаписуєПомилку()
    {
        var svc = new FixedClockRentService(March);

        double total = svc.Close("X-404", 10000, 100, false, 0, false);

        Assert.Equal(-1.0, total);
        Assert.Equal("Rental X-404 not found", svc.lastError);
    }

    [Fact]
    public void Close_ОрендаЛишеЗаброньована_КидаєInvalidOperation()
    {
        var svc = new FixedClockRentService(March);
        Rental r = svc.Reserve(1, "AA5120KI", 0, 2, "BASIC");

        var ex = Assert.Throws<InvalidOperationException>(
            () => svc.Close(r.Number, 10000, 100, false, 0, false));

        Assert.Contains("only active rentals can be closed", ex.Message);
    }

    [Fact]
    public void Reserve_ВодійМолодший21_ПовертаєNullІПомилку()
    {
        var svc = new FixedClockRentService(March);

        Rental r = svc.Reserve(4, "AA5120KI", 0, 2, "BASIC");

        Assert.Null(r);
        Assert.Equal("Customer 4 may not drive ECONOMY", svc.lastError);
    }

    // ---------- Зміна стану оренди ----------

    [Fact]
    public void Pickup_ЗаброньованаОренда_СтаєАктивною()
    {
        var svc = new FixedClockRentService(March);
        Rental r = svc.Reserve(1, "AA5120KI", 0, 2, "FULL");

        bool ok = svc.Pickup(r.Number, 87020, 100);

        Assert.True(ok);
        Assert.Equal(RentalStatus.Active, r.Status);
        Assert.Equal(March.Date.AddDays(2), r.PlannedReturn);
    }

    [Fact]
    public void Cancel_ВиданаОренда_НеСкасовуєтьсяПовертаєFalse()
    {
        var svc = new FixedClockRentService(March);
        Rental r = svc.Reserve(1, "AA5120KI", 0, 2, "FULL");
        svc.Pickup(r.Number, 87020, 100);

        bool ok = svc.Cancel(r.Number);

        Assert.False(ok);
        Assert.Equal(RentalStatus.Active, r.Status);
    }

    [Fact]
    public void Close_ПошкодженеАвто_ЗакриваєОрендуІБлокуєАвто()
    {
        var svc = new FixedClockRentService(March);
        var r = AddActive(svc, "ECONOMY", 3, "FULL");

        svc.Close(r.Number, 10450, 80, true, 200, false);

        Assert.Equal(RentalStatus.Closed, r.Status);
        Assert.False(r.Car.Available);
        Assert.Equal(10450, r.Car.Mileage);
        Assert.Equal(80, r.Car.FuelPercent);
    }

    [Fact]
    public void Close_ЗакриттяОренди_НараховуєБалиЗаКожні10Євро()
    {
        var svc = new FixedClockRentService(March);
        var r = AddActive(svc, "ECONOMY", 3);

        svc.Close(r.Number, 10900, 100, false, 0, false);

        Assert.Equal(13, r.Customer.Points); // 133,5 / 10
        Assert.Equal(1, r.Customer.RentalsCount);
    }

    [Fact]
    public void Close_НомерМалимиЛітерамиЗПробілом_ЗнаходитьОренду()
    {
        var svc = new FixedClockRentService(March);
        var r = AddActive(svc, "ECONOMY", 3);

        double total = svc.Close(r.Number.ToLower() + " ", 10600, 100, false, 0, false);

        Assert.Equal(96.0, total);
    }

    // Pickup ставить повернення на 00:00, тож повернення вчасно
    // о 10:00 запланованого дня вже вважається простроченням (TD-04).
    [Fact]
    public void Close_ПоверненняВЗапланованийДень_НараховуєДобуШтрафу()
    {
        var svc = new FixedClockRentService(March.Date.AddHours(10));
        Rental r = svc.Reserve(1, "AA5120KI", 0, 2, "none");
        svc.Pickup(r.Number, 87020, 100);
        svc.Moment = March.Date.AddDays(2).AddHours(10);

        double total = svc.Close(r.Number, 87020, 100, false, 0, false);

        Assert.Equal(96.0, total); // 2 × 32 + штраф 32
        Assert.Equal(10, svc.lastLateHours);
    }

    private static Rental AddActive(FixedClockRentService svc, string carClass, int days,
        string insurance = "NONE", Customer customer = null)
    {
        var car = new Car("TT-" + carClass, "Test " + carClass, carClass, 40, 10000);
        var r = new Rental
        {
            Number = "T-" + (svc.repo.Rentals.Count + 1),
            Customer = customer ?? new Customer(99, "Test Driver", "000", 30, 10),
            Car = car,
            Status = RentalStatus.Active,
            StartDate = svc.Moment.Date.AddDays(-days),
            PlannedReturn = svc.Moment.AddHours(1),
            Days = days,
            StartKm = 10000,
            Insurance = insurance,
        };
        car.Available = false;
        svc.repo.Rentals.Add(r);
        return r;
    }
}
