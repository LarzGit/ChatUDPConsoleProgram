using System;
using System.Threading.Tasks;

namespace UdpChatServer
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("UDP-сервер запущен на порту 11000...");
            UdpServer server = new UdpServer(11000);
            await server.StartAsync(); // Запуск асинхронного приема сообщений
        }
    }
}
