# Реєстр дефектів проєктування коду

Варіант 7, CarRental. Модуль-донор — `RentService.Close`. Номери рядків — за тегом `baseline`.
Пріоритет = серйозність × частота (частота понад 4 рахується як 4).

| ID | Місце (файл:рядки) | Дефект | Серйозність | Частота | Пріоритет |
|---|---|---|---|---|---|
| D-01 | RentService.cs:107-123, 147-162, 225-239 | Дублювання коду (профільний) | 5 | 3 | 15 |
| D-02 | RentService.cs:128-208 (Close) | Довгий метод | 5 | 1 | 5 |
| D-03 | RentService.cs:28-29, 37, 50, 110-122, 150-176, 185-186, 265-279; InvoicePrinter.cs:20, 26; Customer.cs:29-31 | Магічне число | 3 | 4 | 12 |
| D-04 | RentService.cs:23-36 (Reserve), 91-104 (Quote) | Дублювання коду | 3 | 2 | 6 |
| D-05 | RentService.cs:178-188 (Close) | Глибока вкладеність | 3 | 1 | 3 |
| D-06 | RentService.cs:128-129; InvoicePrinter.cs:14-15; RentalRepository.cs:30-31; RentService.cs:20-21 | Довгий список параметрів | 3 | 4 | 12 |
| D-07 | RentService.cs:10-12 | Тимчасове поле | 2 | 3 | 6 |
| D-08 | AuditLog.cs:10-15 | Проковтнутий виняток | 4 | 1 | 4 |
| D-09 | RentService.cs:283-293, 194, 242 | Мертвий код | 2 | 3 | 6 |
| D-10 | RentService.cs:126-127, 247 | Коментар-виправдання | 3 | 2 | 6 |
| D-11 | Car.cs:8, Customer.cs:7, Rental.cs:18; RentService.cs:29, 108, 181, 184; Car.cs:26 | Одержимість елементарними типами | 3 | 4 | 12 |
| D-12 | RentService.cs:272-281, 199-203 | Заздрість до чужих даних | 3 | 2 | 6 |
| D-13 | RentService.cs:49, 181-187, 261-269; InvoicePrinter.cs:20, 30-39; Rental.cs:18 | Дробовик | 4 | 3 | 12 |
| D-14 | RentService.cs:7, 10-12; RentalRepository.cs:6-9; AuditLog.cs:6; Settings.cs:6-9 | Відкриті поля і глобальний стан | 3 | 4 | 12 |
| D-15 | RentalRepository.cs:55; InvoicePrinter.cs:30 | Імена не за конвенцією | 1 | 2 | 2 |
| D-16 | InvoicePrinter.cs:47-57 | Цикл робить три справи | 1 | 1 | 1 |

Записів: 16; різних типів дефекту: 15.

## Картки

### D-01. Дублювання коду (профільний)

- **Місце:** RentService.cs:107-123, 147-162, 225-239
- **Дефект:** формула прокату (тариф за класом, 250 км на добу, 0,25 за км, тижнева знижка) скопійована в Quote, Close і Extend; клон до 14 рядків, 3 входження
- **Доказ:** Фрагмент Quote (RentService.cs:107-122); та сама формула в Close і Extend

```csharp
double rate;
switch (car.CarClass)
{
    case "ECONOMY": rate = 32; break;
    case "COMPACT": rate = 41; break;
    case "SUV": rate = 68; break;  // у Extend: 65
    case "PREMIUM": rate = 95; break;
    default: rate = 32; break;
}
double baseCost = days * rate;
int overKm = estKm - days * 250;
double kmCost = overKm > 0 ? overKm * 0.25 : 0;
double cost = baseCost + kmCost;
if (days > 7)  // у Close: days >= 7
    cost = cost * 0.9;
```

- **Чим шкодить:** зміна тарифу вимагає трьох однакових правок; копії вже розійшлися: Extend рахує SUV за 65 замість 68, Quote дає тижневу знижку лише з 8-ї доби, а Close — із 7-ї
- **Категорія впливу:** змінюваність, надійність
- **Серйозність:** 5 з 5; **Частота:** 3; **Пріоритет:** 15
- **Гіпотеза виправлення:** Extract Method (DailyRate, RentCost), Replace Temp with Query, далі Extract Class для тарифу

### D-02. Довгий метод

- **Місце:** RentService.cs:128-208 (Close)
- **Дефект:** 64 логічні рядки, CC = 21, вкладеність 5, 6 параметрів; у методі змішано пошук, перевірку стану, тариф, пальне, штраф, шкоду, страховку, знижку, оновлення стану й друк
- **Доказ:** Початок Close (RentService.cs:142-169), рядок 169 перенесено

```csharp
DateTime now = DateTime.Now;
int days = (now.Date - r.StartDate.Date).Days;
if (days < 1)
    days = 1; // повернули в той же день = 1 доба
... // 9 рядків тарифу (D-01)
if (car.IsBigCar() && now.Month >= 6 && now.Month <= 8)
    cost = cost * 1.15;
double fuel = 0;
if (fuelPercent < r.StartFuel)
    fuel = (r.StartFuel - fuelPercent) * car.TankLiters
        / 100.0 * Settings.FuelPrice + 15;
... // ще 40 рядків
```

- **Чим шкодить:** для покриття всіх шляхів потрібен 21 тестовий випадок; будь-яка зміна правила зачіпає весь метод
- **Категорія впливу:** читаність, змінюваність, тестованість
- **Серйозність:** 5 з 5; **Частота:** 1; **Пріоритет:** 5
- **Гіпотеза виправлення:** Extract Method на нарахування (пальне, штраф, шкода, дні), Decompose Conditional

### D-03. Магічне число

- **Місце:** RentService.cs:28-29, 37, 50, 110-122, 150-176, 185-186, 265-279; InvoicePrinter.cs:20, 26; Customer.cs:29-31
- **Дефект:** понад 30 числових літералів у логіці: 32/41/68/95/65, 250, 0.25, 0.9, 7, 1.15, 6, 8, 15, 12, 3, 24, 300, 500, 21, 25, 30, 0.10/0.05/0.02, 400, 1000
- **Доказ:** RentService.cs:173-176, 50 і InvoicePrinter.cs:26

```csharp
if (lateHours > 3)
    late = (lateHours / 24 + 1) * rate;
else if (lateHours > 1)
    late = lateHours * 12;
...
r.Deposit = car.IsBigCar() ? 500 : 300;
...
Console.WriteLine("  incl. VAT: "
    + (r.Total / 6).ToString("F2"));
```

- **Чим шкодить:** значення 250 і 0.25 живуть у чотирьох місцях; незрозуміло, що означає 6 у r.Total / 6 або 3 у lateHours > 3
- **Категорія впливу:** читаність, змінюваність
- **Серйозність:** 3 з 5; **Частота:** 4; **Пріоритет:** 12
- **Гіпотеза виправлення:** Replace Magic Number with Constant

### D-04. Дублювання коду

- **Місце:** RentService.cs:23-36 (Reserve), 91-104 (Quote)
- **Дефект:** перевірка клієнта й авто (вік, стаж, PREMIUM з 25 років) повторена дослівно, 11 рядків
- **Доказ:** Reserve (RentService.cs:23-36); у Quote рядки 91-104 ідентичні

```csharp
Customer cust = repo.find_customer(customerId);
Car car = repo.FindCar(plate);
string err = null;
if (cust == null || car == null)
    err = "Customer " + customerId + " or car "
        + plate + " not found";
else if (cust.DriverAge < 21 || cust.LicenseYears < 2
    || (cust.DriverAge < 25 && car.CarClass == "PREMIUM"))
    err = "Customer " + customerId
        + " may not drive " + car.CarClass;
if (err != null) { lastError = err; ... }
```

- **Чим шкодить:** нове правило допуску водія треба вносити двічі; Quote і Reserve можуть почати по-різному відмовляти тому самому клієнту
- **Категорія впливу:** змінюваність, надійність
- **Серйозність:** 3 з 5; **Частота:** 2; **Пріоритет:** 6
- **Гіпотеза виправлення:** Extract Method (DriverRejected)

### D-05. Глибока вкладеність

- **Місце:** RentService.cs:178-188 (Close)
- **Дефект:** розрахунок шкоди: чотири вкладені if, глибина 5 (поріг 3)
- **Доказ:** RentService.cs:178-188

```csharp
double dmg = 0;
if (damaged)
{
    if (r.Insurance != "FULL")
    {
        dmg = damageCost;
        if (r.Insurance == "BASIC")
            if (dmg > 300)
                dmg = 300;
    }
}
```

- **Чим шкодить:** умову «BASIC і понад 300» видно лише після розбору трьох рівнів; легко пропустити гілку FULL
- **Категорія впливу:** читаність, тестованість
- **Серйозність:** 3 з 5; **Частота:** 1; **Пріоритет:** 3
- **Гіпотеза виправлення:** Replace Nested Conditional with Guard Clauses

### D-06. Довгий список параметрів

- **Місце:** RentService.cs:128-129; InvoicePrinter.cs:14-15; RentalRepository.cs:30-31; RentService.cs:20-21
- **Дефект:** Close (6), PrintReceipt (6), AddDemo (6), Reserve (5); серед параметрів Close два булеві прапорці damaged і printReceipt
- **Доказ:** Сигнатури Close і PrintReceipt

```csharp
public double Close(string rentalNo, int returnKm,
    int fuelPercent, bool damaged,
    double damageCost, bool printReceipt)
...
public void PrintReceipt(Rental r, double rent,
    double fuel, double late,
    double damage, double insurance)
```

- **Чим шкодить:** у виклику Close("R-1002", 66100, 75, true, 850, true) неможливо без підказки зрозуміти, що означає кожне значення; легко переставити аргументи
- **Категорія впливу:** читаність, надійність
- **Серйозність:** 3 з 5; **Частота:** 4; **Пріоритет:** 12
- **Гіпотеза виправлення:** Introduce Parameter Object, Remove Flag Argument

### D-07. Тимчасове поле

- **Місце:** RentService.cs:10-12
- **Дефект:** lastError, lastLateHours, lastDiscount заповнюються лише в окремих сценаріях і передають результат «через бічні двері»
- **Доказ:** RentService.cs:10-12

```csharp
public string lastError = "";
public int lastLateHours;   // for the return screen
public double lastDiscount; // for the return screen
```

- **Чим шкодить:** значення поля залежить від того, який метод викликали останнім; після Quote у lastLateHours лишається число з попереднього Close
- **Категорія впливу:** надійність, тестованість
- **Серйозність:** 2 з 5; **Частота:** 3; **Пріоритет:** 6
- **Гіпотеза виправлення:** повертати об’єкт-результат замість полів

### D-08. Проковтнутий виняток

- **Місце:** AuditLog.cs:10-15
- **Дефект:** порожній catch (Exception) { } навколо запису журналу
- **Доказ:** AuditLog.cs:8-16, рядок 12 перенесено

```csharp
public void Write(string msg)
{
    try
    {
        File.AppendAllText(Settings.AuditFile,
            DateTime.Now.ToString("s")
            + " " + msg + "\n");
        Written++;
    }
    catch (Exception) { }
}
```

- **Чим шкодить:** коли диск заповнений або файл зайнятий, операції проходять без сліду в журналі, і ніхто про це не дізнається
- **Категорія впливу:** надійність
- **Серйозність:** 4 з 5; **Частота:** 1; **Пріоритет:** 4
- **Гіпотеза виправлення:** Replace Error Code with Exception / логування збою (зміна поведінки)

### D-09. Мертвий код

- **Місце:** RentService.cs:283-293, 194, 242
- **Дефект:** невикористаний приватний метод CalcOldTariff і два закоментовані рядки (ПДВ, депозит)
- **Доказ:** RentService.cs:283-293 і 194

```csharp
// старий тариф (до 2022) - для старих договорів
private double CalcOldTariff(int days, string cls)
{
    double r = 30;
    if (cls == "SUV" || cls == "PREMIUM")
        r = 60;
    ...
}
...
//total = total * 1.2; // ПДВ, вимкнено з 2023
```

- **Чим шкодить:** читач витрачає час на «старий тариф», якого ніхто не викликає; закоментовані рядки виглядають як вимкнені вимоги
- **Категорія впливу:** читаність
- **Серйозність:** 2 з 5; **Частота:** 3; **Пріоритет:** 6
- **Гіпотеза виправлення:** Remove Dead Code

### D-10. Коментар-виправдання

- **Місце:** RentService.cs:126-127, 247
- **Дефект:** коментар над Close обіцяє «0, якщо не знайдено», а метод повертає -1; коментар «TEMP: з планшета...» виправдовує нормалізацію номера, що живе з першої версії
- **Доказ:** RentService.cs:127-136

```csharp
// Повертає суму до сплати або 0, якщо не знайдено.
public double Close(string rentalNo, ...)
{
    Rental r = FindRental(rentalNo);
    if (r == null)
    {
        lastError = "Rental " + rentalNo
            + " not found";
        return -1;
    }
```

- **Чим шкодить:** коментар бреше: той, хто йому повірить, перевірятиме total == 0 і пропустить помилку
- **Категорія впливу:** читаність, надійність
- **Серйозність:** 3 з 5; **Частота:** 2; **Пріоритет:** 6
- **Гіпотеза виправлення:** привести коментар і код у відповідність (після рішення TD-06)

### D-11. Одержимість елементарними типами

- **Місце:** Car.cs:8, Customer.cs:7, Rental.cs:18; RentService.cs:29, 108, 181, 184; Car.cs:26
- **Дефект:** клас авто, рівень клієнта і страховка — рядки; порівняння з літералами "PREMIUM", "FULL", "GOLD" розкидані по чотирьох файлах, вибір тарифу — switch за рядком
- **Доказ:** Car.cs:8, Customer.cs:7, Rental.cs:18, RentService.cs:181

```csharp
public string CarClass;   // ECONOMY, COMPACT, SUV, PREMIUM
...
public string Level = "BASIC";  // BASIC / SILVER / GOLD
...
public string Insurance = "NONE";  // NONE, BASIC, FULL
...
if (r.Insurance != "FULL")
```

- **Чим шкодить:** помилка в регістрі ("Full") тихо дає іншу суму; компілятор не допомагає
- **Категорія впливу:** надійність, змінюваність
- **Серйозність:** 3 з 5; **Частота:** 4; **Пріоритет:** 12
- **Гіпотеза виправлення:** Replace Conditional with Table зараз; Replace Primitive with Value Object — у борг

### D-12. Заздрість до чужих даних

- **Місце:** RentService.cs:272-281, 199-203
- **Дефект:** GetLoyaltyDiscount читає лише поля Customer; Close напряму змінює поля Car і Customer
- **Доказ:** RentService.cs:272-281

```csharp
public double GetLoyaltyDiscount(Customer c)
{
    if (c.Level == "GOLD")
        return 0.10;
    if (c.Level == "SILVER")
        return 0.05;
    if (c.RentalsCount >= 3 && c.Points > 100)
        return 0.02;
    return 0;
}
```

- **Чим шкодить:** правило знижки живе далеко від даних, на яких воно побудоване; щоб змінити рівні лояльності, треба правити сервіс
- **Категорія впливу:** змінюваність
- **Серйозність:** 3 з 5; **Частота:** 2; **Пріоритет:** 6
- **Гіпотеза виправлення:** Move Method у Customer

### D-13. Дробовик

- **Місце:** RentService.cs:49, 181-187, 261-269; InvoicePrinter.cs:20, 30-39; Rental.cs:18
- **Дефект:** знання про страховку і про 250 км на добу розкидане по трьох файлах
- **Доказ:** RentService.cs:157, InvoicePrinter.cs:20 і 34

```csharp
// RentService.Close
int overKm = km - days * 250;
...
// InvoicePrinter.PrintReceipt
+ " (included " + r.Days * 250 + ")");
...
// InvoicePrinter.insuranceName
case "BASIC": return "basic cover, excess 300";
```

- **Чим шкодить:** новий страховий продукт або інша норма пробігу вимагають правок у RentService, InvoicePrinter і Rental одночасно; пропуск одного місця дає неправильний чек
- **Категорія впливу:** змінюваність
- **Серйозність:** 4 з 5; **Частота:** 3; **Пріоритет:** 12
- **Гіпотеза виправлення:** Extract Class для тарифу, сталі замість дублів

### D-14. Відкриті поля і глобальний стан

- **Місце:** RentService.cs:7, 10-12; RentalRepository.cs:6-9; AuditLog.cs:6; Settings.cs:6-9
- **Дефект:** усі класи тримають публічні поля; nextNo, Written і Settings — змінні статичні поля
- **Доказ:** RentService.cs:7, RentalRepository.cs:6-9

```csharp
public RentalRepository repo;
...
public static int nextNo = 1004;
public List<Car> Cars = new List<Car>();
public List<Rental> Rentals = new List<Rental>();
```

- **Чим шкодить:** будь-який код може змінити repo або lastError сервісу; статичний nextNo ділиться між усіма екземплярами й тестами
- **Категорія впливу:** надійність, тестованість
- **Серйозність:** 3 з 5; **Частота:** 4; **Пріоритет:** 12
- **Гіпотеза виправлення:** Encapsulate Field

### D-15. Імена не за конвенцією

- **Місце:** RentalRepository.cs:55; InvoicePrinter.cs:30
- **Дефект:** find_customer і insuranceName порушують PascalCase для методів C#
- **Доказ:** RentalRepository.cs:47, 55 і InvoicePrinter.cs:30

```csharp
public Car FindCar(string plate)
...
public Customer find_customer(int id)
...
public string insuranceName(string code)
```

- **Чим шкодить:** поруч стоять FindCar і find_customer — читач гадає, чи це різні механізми
- **Категорія впливу:** читаність
- **Серйозність:** 1 з 5; **Частота:** 2; **Пріоритет:** 2
- **Гіпотеза виправлення:** Rename

### D-16. Цикл робить три справи

- **Місце:** InvoicePrinter.cs:47-57
- **Дефект:** один цикл одночасно друкує рядки, рахує стани і підсумовує виручку
- **Доказ:** InvoicePrinter.cs:47-57

```csharp
foreach (Rental r in rentals)
{
    Console.WriteLine("  " + r.Describe() ...);
    if (r.Status == RentalStatus.Closed)
        closed++;
    else if (...)
        cancelled++;
    else
        open++;
    sum = sum + r.Total;
}
```

- **Чим шкодить:** щоб змінити формат рядка, доводиться читати і лічильники; лічильники не можна перевикористати окремо
- **Категорія впливу:** читаність
- **Серйозність:** 1 з 5; **Частота:** 1; **Пріоритет:** 1
- **Гіпотеза виправлення:** Split Loop
