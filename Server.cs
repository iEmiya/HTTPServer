using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using NLog;

namespace HTTPServer
{
    class Server
    {
        internal static readonly Logger Log = LogManager.GetCurrentClassLogger();


        private readonly Logger _log;
        private readonly TcpListener _listener; // Объект, принимающий TCP-клиентов

        // Запуск сервера
        public Server(int port)
        {
            _log = Log;

            _listener = new TcpListener(IPAddress.Any, port); // Создаем "слушателя" для указанного порта
            _listener.Start(); // Запускаем его

            // В бесконечном цикле
            while (true)
            {
                // Принимаем новых клиентов. После того, как клиент был принят, он передается в новый поток (ClientThread)
                // с использованием пула потоков.
                ThreadPool.QueueUserWorkItem(new WaitCallback(ClientThread), _listener.AcceptTcpClient());
            }
        }

        private void ClientThread(Object stateInfo)
        {
            // Просто создаем новый экземпляр класса Client и передаем ему приведенный к классу TcpClient объект StateInfo
            new Client(_log, (TcpClient)stateInfo);
        }

        // Остановка сервера
        ~Server()
        {
            // Если "слушатель" был создан
            // Остановим его
            _listener?.Stop();
        }

        static void Main(string[] args)
        {
            Log.Info("Запускаем приложение...");
            if (!SetCurrentDirectory())
                return;
            
            // Определим нужное максимальное количество потоков
            // Пусть будет по 4 на каждый процессор
            int maxThreadsCount = Environment.ProcessorCount * 4;
            // Установим максимальное количество рабочих потоков
            ThreadPool.SetMaxThreads(maxThreadsCount, maxThreadsCount);
            // Установим минимальное количество рабочих потоков
            ThreadPool.SetMinThreads(2, 2);

            Settings.Initialization();

            // Создадим новый сервер на порту
            new Server(Settings.Port);
        }

        private static bool SetCurrentDirectory()
        {
            string location = typeof(Server).Assembly.Location;
            string currentDirectory = Path.GetDirectoryName(location);
            if (string.IsNullOrEmpty(currentDirectory))
            {
                Log.Error("Не удалось определить каталог для работы");
                return false;
            }
            Environment.CurrentDirectory = currentDirectory;
            return true;
        }
    }
}
