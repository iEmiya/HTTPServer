using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using NLog;

namespace HTTPServer
{
    // Класс-обработчик клиента
    class Client
    {
        // Отправка страницы с ошибкой
        private static void SendError(TcpClient client, HttpStatusCode code)
        {
            // Получаем строку вида "200 OK"
            // HttpStatusCode хранит в себе все статус-коды HTTP/1.1
            string codeStr = ((int)code).ToString() + " " + code.ToString();
            // Код простой HTML-странички
            string html = "<html><body><h1>" + codeStr + "</h1></body></html>";
            // Необходимые заголовки: ответ сервера, тип и длина содержимого. После двух пустых строк - само содержимое
            string str = "HTTP/1.1 " + codeStr + "\nContent-type: text/html\nContent-Length:" + html.Length.ToString() + "\n\n" + html;
            // Приведем строку к виду массива байт
            byte[] buffer = Encoding.ASCII.GetBytes(str);
            // Отправим его клиенту
            client.GetStream().Write(buffer, 0, buffer.Length);
            // Закроем соединение
            client.Close();
        }

        private readonly Logger _log;

        // Конструктор класса. Ему нужно передавать принятого клиента от TcpListener
        public Client(Logger log, TcpClient client)
        {
            _log = log;

            // Объявим строку, в которой будет хранится запрос клиента
            string request = "";
            // Буфер для хранения принятых от клиента данных
            byte[] buffer = new byte[1024];
            // Переменная для хранения количества байт, принятых от клиента
            int count;
            // Читаем из потока клиента до тех пор, пока от него поступают данные
            while ((count = client.GetStream().Read(buffer, 0, buffer.Length)) > 0)
            {
                // Преобразуем эти данные в строку и добавим ее к переменной Request
                request += Encoding.ASCII.GetString(buffer, 0, count);
                // Запрос должен обрываться последовательностью \r\n\r\n
                // Либо обрываем прием данных сами, если длина строки Request превышает 4 килобайта
                // Нам не нужно получать данные из POST-запроса (и т. п.), а обычный запрос
                // по идее не должен быть больше 4 килобайт
                if (request.IndexOf("\r\n\r\n", StringComparison.Ordinal) >= 0 || request.Length > 4096)
                {
                    break;
                }
            }

            // Парсим строку запроса с использованием регулярных выражений
            // При этом отсекаем все переменные GET-запроса
            Match reqMatch = Regex.Match(request, @"^\w+\s+([^\s\?]+)[^\s]*\s+HTTP/.*|");

            // Если запрос не удался
            if (reqMatch == Match.Empty)
            {
                // Передаем клиенту ошибку 400 - неверный запрос
                _log.Warn(HttpStatusCode.BadRequest + ": " + request);
                SendError(client, HttpStatusCode.BadRequest);
                return;
            }

            // Получаем строку запроса
            string requestUri = reqMatch.Groups[1].Value;

            // Приводим ее к изначальному виду, преобразуя экранированные символы
            // Например, "%20" -> " "
            requestUri = Uri.UnescapeDataString(requestUri);

            // Если в строке содержится двоеточие, передадим ошибку 400
            // Это нужно для защиты от URL типа http://example.com/../../file.txt
            if (requestUri.IndexOf("..", StringComparison.Ordinal) >= 0)
            {
                _log.Warn(HttpStatusCode.BadRequest + ": " + request);
                SendError(client, HttpStatusCode.BadRequest);
                return;
            }

            if (string.IsNullOrEmpty(requestUri))
            {
                requestUri += "/";
            }

            // Если строка запроса оканчивается на "/", то добавим к ней index.html
            if (requestUri.EndsWith("/"))
            {
                requestUri += "index.html";
            }

            string filePath;
            if (Settings.IsSite)
            {
                filePath = Settings.Folder + requestUri;
            }
            else
            {
                string fileName;
                try
                {
                    fileName = Path.GetFileName(requestUri);
                }
                catch (Exception)
                {
                    _log.Warn(HttpStatusCode.BadRequest + ": " + request);
                    SendError(client, HttpStatusCode.BadRequest);
                    return;
                }

                if (!Settings.Files.TryGetValue(fileName, out filePath))
                {
                    log.Warn(HttpStatusCode.Forbidden + ": " + request);
                    SendError(client, HttpStatusCode.Forbidden);
                    return;
                }
            }

            // Если в папке www не существует данного файла, посылаем ошибку 404
            if (!File.Exists(filePath))
            {
                _log.Warn(HttpStatusCode.NotFound + ": " + request + " => " + filePath);
                SendError(client, HttpStatusCode.NotFound);
                return;
            }

            // Тип содержимого
            string contentType = MimeTypes.GetMimeType(requestUri);

            // Открываем файл, страхуясь на случай ошибки
            FileStream fs;
            try
            {
                fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            }
            catch (Exception)
            {
                // Если случилась ошибка, посылаем клиенту ошибку 500
                _log.Warn(HttpStatusCode.InternalServerError + ": " + request + " => " + filePath);
                SendError(client, HttpStatusCode.InternalServerError);
                return;
            }

            // Посылаем заголовки
            string headers = "HTTP/1.1 200 OK\nContent-Type: " + contentType + "\nContent-Length: " + fs.Length + "\n\n";
            byte[] headersBuffer = Encoding.ASCII.GetBytes(headers);
            client.GetStream().Write(headersBuffer, 0, headersBuffer.Length);

            // Пока не достигнут конец файла
            while (fs.Position < fs.Length)
            {
                // Читаем данные из файла
                count = fs.Read(buffer, 0, buffer.Length);
                // И передаем их клиенту
                client.GetStream().Write(buffer, 0, count);
            }

            // Закроем файл и соединение
            fs.Close();
            client.Close();

            _log.Info(HttpStatusCode.OK + ": " + request + " => " + filePath);
        }
    }
}
