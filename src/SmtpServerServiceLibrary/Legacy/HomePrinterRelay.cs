using Serilog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SmtpServerServiceLibrary.Legacy
{
    public class HomePrinterRelay
    {
        private TcpListener listener;
        private readonly HomePrinterRelaySettings settings;

        public bool IsRunning { get; private set; }

        public HomePrinterRelay(HomePrinterRelaySettings settings)
        {
            if(settings == null)
                throw new ArgumentNullException("settings", "Settings cannot be null.");

            this.settings = settings;
        }

        public bool Startup()
        {
            Shutdown();
            IsRunning = true;

            listener = new TcpListener(IPAddress.Any, settings.SmtpPort);
            listener.Start();
            listener.BeginAcceptTcpClient(OnClientConnect, null);

            return true;
        }

        public void Shutdown()
        {
            IsRunning = false;

            if (listener != null)
            {
                listener.Stop();
                listener = null;
            }
        }

        private void OnClientConnect(IAsyncResult ar)
        {
            if (!IsRunning || listener == null)
                return;

            //Get our socket and start listening to it
            var client = listener.EndAcceptTcpClient(ar);
            Task.Run(() => ProcessSingleClient(client));

            //Have listener start listening for connections again
            listener.BeginAcceptTcpClient(OnClientConnect, null);
        }

        private async void ProcessSingleClient(TcpClient client)
        {
            var stream = client.GetStream();
            var reader = new StreamReader(stream);
            var writer = new StreamWriter(stream);

            string clientIP = ((IPEndPoint)client.Client.RemoteEndPoint).Address.ToString();
            Log.Information("Client connected - {clientIP}", clientIP);
            await WriteLine(writer, "220 localhost -- Knightware proxy server");

            using (MemoryStream readBackBuffer = new MemoryStream())
            {
                while (IsRunning && client.Connected)
                {
                    try
                    {
                        string msg = await ReadLine(reader);
                        if (string.IsNullOrEmpty(msg) || msg.StartsWith("QUIT") || msg.StartsWith("quit"))
                        {
                            //Connection lost.  Close now
                            Log.Information("Closing connection to {clientIP}", clientIP);
                            client.Close();
                            break;
                        }

                        //message has successfully been received
                        else if (msg.StartsWith("EHLO"))
                        {
                            Log.Debug("Received EHLO from: " + msg.Substring(5));
                            await WriteLine(writer, "250 OK");
                        }
                        else if (msg.StartsWith("RCPT TO:"))
                        {
                            //This line will have the display name, next line will have email address
                            string emailTo = CleanEmailString(msg, "RCPT TO:");
                            //TODO: Do I need the send-to email?
                            await WriteLine(writer, "250 OK");
                        }
                        else if (msg.StartsWith("MAIL FROM:"))
                        {
                            string emailFrom = CleanEmailString(msg, "MAIL FROM:");
                            //TODO:  Do I need the from email?
                            await WriteLine(writer, "250 OK");
                        }
                        else if (msg.StartsWith("DATA"))
                        {
                            string tempFile = Path.GetTempFileName();
                            if (File.Exists(tempFile))
                                File.Delete(tempFile);

                            using (var fileStream = File.CreateText(tempFile))
                            {
                                await WriteLine(writer, "354 Start mail input; end with <CR><LF>.<CR><LF>");
                                string currentLine;
                                while ((currentLine = await ReadLine(reader)) != ".")
                                {
                                    if (currentLine.StartsWith(".."))
                                        currentLine = currentLine.Replace("..", ".");

                                    await fileStream.WriteLineAsync(currentLine);
                                }
                            }
                            await WriteLine(writer, "250 OK");

                            //Do actual data processing on a background thread
                            ThreadPool.QueueUserWorkItem((WaitCallback)((state) => ProcessData(tempFile)));
                        }
                        else
                        {
                            Log.Warning("Unrecognized data: {0}", msg);
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Warning("{0} occurred while processing client: {1}", ex.GetType().Name, ex.Message);
                    }
                }
            }

            Log.Information("Client disconnected - {0}", clientIP);
        }

        private void ProcessData(string fileName)
        {
            const string startLineText = "Content-Transfer-Encoding: base64";

            try
            {
                bool startTextFound = false;
                bool isReading = false;
                StringBuilder builder = new StringBuilder();
                foreach (var line in File.ReadAllLines(fileName))
                {
                    if (!isReading)
                    {
                        if (line == startLineText)
                        {
                            startTextFound = true;
                        }
                        else if (startTextFound)
                        {
                            //Next line is blank
                            isReading = true;
                        }
                    }
                    else
                    {
                        if (string.IsNullOrEmpty(line))
                        {
                            //We're done reading the data block
                            DateTime now = DateTime.Now;
                            string newFileName = string.Format("{0}-{1:D2}-{2:D2} {3:D2}-{4:D2}-{5:D2} - Scan.pdf",
                                now.Year, now.Month, now.Day, now.Hour, now.Minute, now.Second);

                            string newFile = Path.Combine(settings.FilePath, newFileName);
                            File.WriteAllBytes(newFile, Convert.FromBase64String(builder.ToString()));
                            break;
                        }
                        else
                        {
                            //We are reading text lines now, so append this one
                            builder.AppendLine(line);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning("exception occurred while processing email: {exception}", ex);
            }
            finally
            {
                if (File.Exists(fileName))
                    File.Delete(fileName);
            }
        }

        private string CleanEmailString(string emailLine, string prefixToRemove = "")
        {
            return emailLine.Substring(prefixToRemove.Length).Replace("<", "").Replace(">", "").Replace(" ", "");
        }

        private async Task WriteLine(StreamWriter writer, string message)
        {
            Log.Debug("Sending: {msg}", message);
            await writer.WriteLineAsync(message);
            await writer.FlushAsync();
        }

        private async Task<string> ReadLine(StreamReader reader)
        {
            string response = await reader.ReadLineAsync();

            if (!string.IsNullOrEmpty(response))
                Log.Debug("Received: {msg}", response);

            return response;
        }
    }
}
