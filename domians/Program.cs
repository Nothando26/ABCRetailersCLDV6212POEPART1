using System.Diagnostics;

namespace domians
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Process.Start("notepad.exe");
            Console.ReadKey();

            var psi = new ProcessStartInfo
            {
                FileName = "https://www.iiemsa.co.za/",
                UseShellExecute = true
            };
            Process.Start(psi);

        }
    }
}
