namespace CarRental;

// Журнал операцій у файл (файл зайнятий - пропускаємо)
public class AuditLog
{
    public static int Written = 0;

    public void Write(string msg)
    {
        try
        {
            File.AppendAllText(Settings.AuditFile, DateTime.Now.ToString("s") + " " + msg + "\n");
            Written++;
        }
        catch (Exception) { }
    }
}
