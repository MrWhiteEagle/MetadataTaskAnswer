namespace Import.ConnectionSupport
{
    public interface IConsoleIO
    {
        public void Write(string message);
        public void WriteLine(string message);
        public string? ReadLine();
        public void Clear();

    }
    public class ConsoleIO : IConsoleIO
    {
        public void WriteLine(string message) => Console.WriteLine(message);
        public void Write(string message) => Console.Write(message);
        public string? ReadLine() => Console.ReadLine();
        public void Clear()
        {
            if (Console.IsOutputRedirected)
            {
                this.WriteLine("--- Console cleared ---");
            }
            else
            {
                Console.Clear();
            }
        }
    }
}
