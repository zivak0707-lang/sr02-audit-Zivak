namespace CarRental;

// Reserved -> Active (видача) -> Closed; Reserved -> Cancelled
public enum RentalStatus { Reserved, Active, Closed, Cancelled }

public class Rental
{
    public string Number;              // R-1001
    public Customer Customer;
    public Car Car;
    public RentalStatus Status = RentalStatus.Reserved;
    public DateTime StartDate;
    public DateTime PlannedReturn;
    public int Days;                   // planned days (after Close - billed days)
    public int StartKm;
    public int StartFuel = 100;        // %
    public int EndKm;
    public string Insurance = "NONE";  // NONE, BASIC, FULL
    public double Deposit;
    public double Total;

    public string Describe()
    {
        return Number + " " + Car.Plate + " " + Customer.Name + " [" + Status + "]";
    }
}
