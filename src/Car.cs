namespace CarRental;

// Автомобіль парку філії
public class Car
{
    public string Plate;
    public string Model;
    public string CarClass;      // ECONOMY, COMPACT, SUV, PREMIUM
    public int TankLiters;
    public int Mileage;
    public int FuelPercent = 100;
    public bool Available = true;

    public Car(string plate, string model, string carClass, int tankLiters, int mileage)
    {
        Plate = plate;
        Model = model;
        CarClass = carClass;
        TankLiters = tankLiters;
        Mileage = mileage;
    }

    // big cars: higher deposit, summer price
    public bool IsBigCar()
    {
        return CarClass == "SUV" || CarClass == "PREMIUM";
    }
}
