namespace CarRental;

// Складові суми при поверненні авто (для чека)
public record ReturnCharges(double Rent, double Fuel, double Late, double Damage, double Insurance);
