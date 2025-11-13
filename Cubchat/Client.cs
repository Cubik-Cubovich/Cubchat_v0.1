using System;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using NAudio.Wave;

public class Client
{
    private static TcpClient textClient;
    private static TcpClient voiceClient;
    private static NetworkStream textStream;
    private static VoiceHandler voiceHandler;
    private static bool isConnected = false;

    public static void StartClient()
    {
        Console.Write("Введите IP-адрес сервера: ");
        string serverIp = Console.ReadLine();

        try
        {
            textClient = new TcpClient(serverIp, 3000);
            textStream = textClient.GetStream();
            isConnected = true;

            Console.WriteLine("Подключено к текстовому чату!");

            Thread textListenThread = new Thread(ListenForTextMessages);
            textListenThread.Start();

            CommandLoop();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка подключения: {ex.Message}");
        }
        finally
        {
            Disconnect();
        }
    }

    static void CommandLoop()
    {
        while (isConnected)
        {
            Console.WriteLine("\nКоманды: start - голосовой чат, stop - отключить голос, exit - выход");
            string input = Console.ReadLine();

            if (input == "start")
            {
                StartVoiceChat();
            }
            else if (input == "stop")
            {
                StopVoiceChat();
            }
            else if (input == "exit")
            {
                break;
            }
            else
            {
                SendTextMessage(input);
            }
        }
    }

    static void StartVoiceChat()
    {
        try
        {
            if (voiceClient == null || !voiceClient.Connected)
            {
                Console.Write("Введите IP-адрес сервера для голосового чата: ");
                string serverIp = Console.ReadLine();

                voiceClient = new TcpClient(serverIp, 3001);
                voiceHandler = new VoiceHandler(voiceClient);
                voiceHandler.Start();

                Console.WriteLine("Голосовой чат активирован! Говорите...");
                Console.WriteLine("Примечание: вы не слышите свой собственный голос");
            }
            else
            {
                Console.WriteLine("Голосовой чат уже активен");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка подключения голосового чата: {ex.Message}");
        }
    }

    static void StopVoiceChat()
    {
        voiceHandler?.Stop();
        voiceClient?.Close();
        Console.WriteLine("Голосовой чат отключен");
    }

    static void SendTextMessage(string message)
    {
        try
        {
            byte[] data = Encoding.UTF8.GetBytes(message);
            textStream.Write(data, 0, data.Length);
        }
        catch
        {
            Console.WriteLine("Ошибка отправки сообщения");
        }
    }

    static void ListenForTextMessages()
    {
        byte[] buffer = new byte[1024];

        try
        {
            while (isConnected && textClient.Connected)
            {
                if (textStream.DataAvailable)
                {
                    int bytesRead = textStream.Read(buffer, 0, buffer.Length);
                    if (bytesRead == 0) break;

                    string message = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                    Console.WriteLine($"\n>>> {message}");
                }
                Thread.Sleep(10);
            }
        }
        catch
        {
            Console.WriteLine("Соединение с сервером разорвано.");
        }
    }

    static void Disconnect()
    {
        isConnected = false;

        StopVoiceChat();

        textStream?.Close();
        textClient?.Close();

        Console.WriteLine("Отключено от сервера");
    }
}

public class VoiceHandler
{
    private TcpClient client;
    private NetworkStream stream;
    private BufferedWaveProvider waveProvider;
    private WaveInEvent waveIn;
    private WaveOutEvent waveOut;
    private bool isActive = false;

    public VoiceHandler(TcpClient client)
    {
        this.client = client;
        this.stream = client.GetStream();

        WaveFormat waveFormat = new WaveFormat(16000, 16, 1);
        waveProvider = new BufferedWaveProvider(waveFormat);
        waveProvider.DiscardOnBufferOverflow = true;
        waveProvider.BufferLength = 32000;
    }

    public void Start()
    {
        waveIn = new WaveInEvent();
        waveIn.DeviceNumber = 0;
        waveIn.WaveFormat = new WaveFormat(16000, 16, 1);
        waveIn.BufferMilliseconds = 50;
        waveIn.DataAvailable += OnAudioDataAvailable;

        waveOut = new WaveOutEvent();
        waveOut.Init(waveProvider);

        waveIn.StartRecording();
        waveOut.Play();
        isActive = true;

        Thread voiceListenThread = new Thread(ListenForVoice);
        voiceListenThread.Start();

        Console.WriteLine("Микрофон активирован, динамики активированы");
    }

    private void OnAudioDataAvailable(object sender, WaveInEventArgs e)
    {
        try
        {
            if (isActive && client.Connected && e.BytesRecorded > 0)
            {
                stream.Write(e.Buffer, 0, e.BytesRecorded);
            }
        }
        catch
        {
        }
    }

    private void ListenForVoice()
    {
        byte[] buffer = new byte[4000];

        try
        {
            while (isActive && client.Connected)
            {
                if (stream.DataAvailable)
                {
                    int bytesRead = stream.Read(buffer, 0, buffer.Length);
                    if (bytesRead > 0)
                    {
                        waveProvider.AddSamples(buffer, 0, bytesRead);
                    }
                }
                Thread.Sleep(1);
            }
        }
        catch
        {
        }
    }

    public void Stop()
    {
        isActive = false;

        waveIn?.StopRecording();
        waveIn?.Dispose();

        waveOut?.Stop();
        waveOut?.Dispose();

        client?.Close();

        Console.WriteLine("Голосовая связь деактивирована");
    }
}