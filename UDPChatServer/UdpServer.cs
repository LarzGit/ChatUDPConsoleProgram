using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace UdpChatServer
{
    /// <summary>
    /// Простой UDP-сервер для обработки входящих сообщений
    /// </summary>
    public class UdpServer
    {
        private readonly UdpClient _udpClient;
        private readonly Dictionary<string, IPEndPoint> _clients = new Dictionary<string, IPEndPoint>();

        public UdpServer(int port)
        {
            // Открываем сокет на указанном порту для приема UDP-пакетов
            _udpClient = new UdpClient(port);
        }

        /// <summary>
        /// Запустить сервер и обрабатывать сообщения бесконечно
        /// </summary>
        public async Task StartAsync()
        {
            while (true)
            {
                // Ожидаем входящее UDP-сообщение (асинхронно)
                UdpReceiveResult result = await _udpClient.ReceiveAsync(); // не блокирует поток:contentReference[oaicite:11]{index=11}
                string message = Encoding.UTF8.GetString(result.Buffer);
                IPEndPoint remoteEP = result.RemoteEndPoint; // Адрес отправителя

                Console.WriteLine($"Получено от {remoteEP}: {message}");

                // Запоминаем нового клиента (по строковому представлению эндпоинта)
                if (!_clients.ContainsKey(remoteEP.ToString()))
                {
                    _clients.Add(remoteEP.ToString(), remoteEP);
                }

                // Формируем ответ (например, простое эхо или обработка команды)
                string response = $"[Echo от сервера] {message}";
                byte[] responseBytes = Encoding.UTF8.GetBytes(response);

                // Отправляем ответ клиенту по его IPEndPoint (асинхронно):contentReference[oaicite:12]{index=12}
                await _udpClient.SendAsync(responseBytes, responseBytes.Length, remoteEP);
            }
        }
    }
}
