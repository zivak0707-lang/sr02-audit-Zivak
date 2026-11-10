namespace CarRental;

public class Customer
{
    public int Id;
    public string Name;
    public string Level = "BASIC";  // BASIC / SILVER / GOLD
    public string Phone;            // як ввели на рецепції
    public int DriverAge;
    public int LicenseYears;
    public int Points;
    public int RentalsCount;

    public Customer(int id, string name, string phone, int driverAge, int licenseYears)
    {
        this.Id = id;
        this.Name = name;
        this.Phone = phone;
        this.DriverAge = driverAge;
        this.LicenseYears = licenseYears;
    }

    // 1 бал за кожні 10 EUR; 400 - SILVER, 1000 - GOLD
    public void AddPoints(int pts)
    {
        if (pts <= 0)
            return;
        Points = Points + pts;
        if (Points >= 1000)
            Level = "GOLD";
        else if (Points >= 400)
            Level = "SILVER"; // TODO: пониження рівня?
    }
}
