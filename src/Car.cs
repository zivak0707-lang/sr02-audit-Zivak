namespace CarRental;

// Автомобіль парку філії
public class Car
{
    public string Plate;
    public string Model;
    // TODO TD-07: рядок замість типу домену; R12 Replace Primitive with Value Object
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
