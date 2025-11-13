using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using NAudio.Wave;

public class Server
{
    private static TcpListener textServer;
    private static TcpListener voiceServer;
    private static List<TcpClient> connectedClients = new List<TcpClient>();
    private static List<VoiceStream> voiceStreams = new List<VoiceStream>();
    private static bool isRunning = true;

    public static void StartServer()
    {
        Console.WriteLine("Запуск сервера...");
        Console.WriteLine("Текстовый порт: 3000");
        Console.WriteLine("Голосовой порт: 3001");

        textServer = new TcpListener(IPAddress.Any, 3000);
        textServer.Start();

        voiceServer = new TcpListener(IPAddress.Any, 3001);
        voiceServer.Start();

        Console.WriteLine("Сервер запущен. Ожидание подключений...");

        Thread textThread = new Thread(AcceptTextConnections);
        textThread.Start();

        Thread voiceThread = new Thread(AcceptVoiceConnections);
        voiceThread.Start();

        Console.WriteLine("Нажмите Enter для остановки сервера...");
        Console.ReadLine();
        isRunning = false;

        textServer.Stop();
        voiceServer.Stop();
    }

    static void AcceptTextConnections()
    {
        while (isRunning)
        {
            try
            {
                TcpClient client = textServer.AcceptTcpClient();
                lock (connectedClients)
                {
                    connectedClients.Add(client);
                }
                Console.WriteLine($"Текстовый клиент подключен: {client.Client.RemoteEndPoint}");

                Thread clientThread = new Thread(() => HandleTextClient(client));
                clientThread.Start();
            }
            catch (Exception ex)
            {
                if (isRunning) Console.WriteLine($"Ошибка принятия текстового подключения: {ex.Message}");
            }
        }
    }

    static void AcceptVoiceConnections()
    {
        while (isRunning)
        {
            try
            {
                TcpClient client = voiceServer.AcceptTcpClient();
                Console.WriteLine($"Голосовой клиент подключен: {client.Client.RemoteEndPoint}");

                VoiceStream voiceStream = new VoiceStream(client);
                lock (voiceStreams)
                {
                    voiceStreams.Add(voiceStream);
                }

                Thread voiceThread = new Thread(() => HandleVoiceClient(voiceStream));
                voiceThread.Start();
            }
            catch (Exception ex)
            {
                if (isRunning) Console.WriteLine($"Ошибка принятия голосового подключения: {ex.Message}");
            }
        }
    }

    static void HandleTextClient(TcpClient client)
    {
        NetworkStream stream = client.GetStream();
        byte[] buffer = new byte[1024];

        try
        {
            while (isRunning && client.Connected)
            {
                if (stream.DataAvailable)
                {
                    int bytesRead = stream.Read(buffer, 0, buffer.Length);
                    if (bytesRead == 0) break;

                    string message = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                    Console.WriteLine($"Текст от клиента: {message}");

                    BroadcastTextMessage(message, client);
                }
                Thread.Sleep(10);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка обработки текстового клиента: {ex.Message}");
        }
        finally
        {
            lock (connectedClients)
            {
                connectedClients.Remove(client);
            }
            client.Close();
            Console.WriteLine("Текстовый клиент отключен");
        }
    }

    static void HandleVoiceClient(VoiceStream sender)
    {
        try
        {
            Console.WriteLine("Голосовая связь активирована для клиента");

            while (isRunning && sender.Client.Connected)
            {
                BroadcastVoiceData(sender);
                Thread.Sleep(1);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка обработки голосового клиента: {ex.Message}");
        }
        finally
        {
            lock (voiceStreams)
            {
                voiceStreams.Remove(sender);
            }
            sender.Client.Close();
            Console.WriteLine("Голосовой клиент отключен");
        }
    }

    static void BroadcastTextMessage(string message, TcpClient sender)
    {
        byte[] data = Encoding.UTF8.GetBytes(message);
        List<TcpClient> clientsToRemove = new List<TcpClient>();

        lock (connectedClients)
        {
            foreach (TcpClient client in connectedClients)
            {
                if (client != sender && client.Connected)
                {
                    try
                    {
                        NetworkStream stream = client.GetStream();
                        stream.Write(data, 0, data.Length);
                    }
                    catch
                    {
                        clientsToRemove.Add(client);
                    }
                }
            }

            foreach (TcpClient client in clientsToRemove)
            {
                connectedClients.Remove(client);
            }
        }
    }

    static void BroadcastVoiceData(VoiceStream sender)
    {
        if (!sender.Client.Connected) return;

        byte[] buffer = new byte[4000];
        int bytesRead = 0;

        try
        {
            if (sender.Client.GetStream().DataAvailable)
            {
                bytesRead = sender.Client.GetStream().Read(buffer, 0, buffer.Length);
            }
        }
        catch
        {
            return;
        }

        if (bytesRead > 0)
        {
            List<VoiceStream> streamsToRemove = new List<VoiceStream>();

            lock (voiceStreams)
            {
                foreach (VoiceStream voiceStream in voiceStreams)
                {
                    // Выключение что б самого себя не слышать
                    if (voiceStream != sender && voiceStream.Client.Connected)
                    {
                        try
                        {
                            voiceStream.Client.GetStream().Write(buffer, 0, bytesRead);
                        }
                        catch
                        {
                            streamsToRemove.Add(voiceStream);
                        }
                    }
                }

                foreach (VoiceStream stream in streamsToRemove)
                {
                    voiceStreams.Remove(stream);
                }
            }
        }
    }
}

public class VoiceStream
{
    public TcpClient Client { get; private set; }

    public VoiceStream(TcpClient client)
    {
        this.Client = client;
    }
}