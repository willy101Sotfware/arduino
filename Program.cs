using System;
using System.Threading.Tasks;

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("Iniciando sistema de dispensación...");
        
        try
        {
            // Puedes cambiar "COM1" por el puerto COM donde está conectado tu dispositivo
            using (var dispenser = new MetavixDispenser("COM1"))
            {
                Console.WriteLine("Sistema iniciado correctamente");
                
                // Mantener el programa corriendo
                while (true)
                {
                    // El dispenser maneja los eventos automáticamente
                    await Task.Delay(100); // Pequeña pausa para no consumir CPU
                    
                    // Puedes agregar aquí código para manejar entrada del usuario
                    // Por ejemplo:
                    if (Console.KeyAvailable)
                    {
                        var key = Console.ReadKey(true);
                        if (key.Key == ConsoleKey.Q)
                        {
                            Console.WriteLine("Cerrando el programa...");
                            break;
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            Console.WriteLine("Presiona cualquier tecla para salir...");
            Console.ReadKey();
        }
    }
}
