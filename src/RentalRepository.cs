namespace CarRental;

// Сховище в пам'яті (БД - у версії 2.0), дані демо-філії
public class RentalRepository
{
    public static int nextNo = 1004;   // R-1001..R-1003 are the demo rentals below
    public List<Car> Cars = new List<Car>();
    public List<Customer> Customers = new List<Customer>();
    public List<Rental> Rentals = new List<Rental>();

    public RentalRepository()
    {
        Cars.Add(new Car("AA1024KB", "Skoda Fabia", "ECONOMY", 45, 41250));
        Cars.Add(new Car("AA2048KC", "Toyota Corolla", "COMPACT", 50, 63800));
        Cars.Add(new Car("AA3072KE", "Kia Sportage", "SUV", 62, 28400));
        Cars.Add(new Car("AA4096KH", "BMW 520d", "PREMIUM", 66, 15100));
        Cars.Add(new Car("AA5120KI", "Renault Clio", "ECONOMY", 42, 87020));
        Customers.Add(new Customer(1, "Ivan Petrenko", "+380501112233", 34, 12));
        Customers.Add(new Customer(2, "Olena Kovalenko", "067-222-33-44", 41, 20));
        Customers.Add(new Customer(3, "Taras Melnyk", "0933334455", 23, 3));
        Customers.Add(new Customer(4, "Andrii Bondar", "+38 050 444 55 66", 19, 1));
        Customers[1].Level = "GOLD";
        Customers[1].Points = 1250;
        AddDemo("R-1001", 1, Cars[0], 3, 4, "BASIC");
        AddDemo("R-1002", 2, Cars[1], 8, 8, "BASIC");
        AddDemo("R-1003", 3, Cars[2], 2, 5, "FULL");
        Rentals[1].PlannedReturn = DateTime.Now.AddHours(-5); // overdue one for the demo
    }

    private void AddDemo(string no, int customerId, Car car, int daysAgo, int plannedDays,
        string insurance)
    {
        Rental r = new Rental();
        r.Number = no;
        r.Customer = FindCustomer(customerId);
        r.Car = car;
        r.Status = RentalStatus.Active;
        r.StartDate = DateTime.Today.AddDays(-daysAgo);
        r.PlannedReturn = r.StartDate.AddDays(plannedDays);
        r.Days = plannedDays;
        r.StartKm = car.Mileage;
        r.Insurance = insurance;
        car.Available = false;
        Rentals.Add(r);
    }

    public Car FindCar(string plate)
    {
        foreach (Car c in Cars)
            if (c.Plate == plate)
                return c;
        return null;
    }

    public Customer FindCustomer(int id)
    {
        for (int i = 0; i < Customers.Count; i++)
        {
            if (Customers[i].Id == id)
                return Customers[i];
        }
        return null;
    }
}
