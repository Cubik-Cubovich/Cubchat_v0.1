using System;

class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("=== Кубакбасный Голосовой чат v0.1 ==="); // это я пишу в честь моего кота кубика
        Console.WriteLine("Кубавыберите режим: (1) Сервер, (2) Клиент"); // это я пишу в честь моего кота кубика
        string choice = Console.ReadLine();

        if (choice == "1")
        {
            Server.StartServer();
        }
        else if (choice == "2")
        {
            Client.StartClient();
        }
        else
        {
            Console.WriteLine("Неверный выбор.");
        }
    }
}