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
using System.Threading.Tasks;

namespace SmtpServerServiceLibrary
{
    public delegate void DataTransmissionHandler(object sender, string message);

    public class SmtpRelayServer
    {
        private TcpListener listener;

        public bool IsRunning { get; private set; }

        public event DataTransmissionHandler DataRead;
        protected void OnDataRead(string message)
        {
            if (DataRead != null)
                DataRead(this, message);
        }

        public event DataTransmissionHandler DataWritten;
        protected void OnDataWritten(string message)
        {
            if (DataWritten != null)
                DataWritten(this, message);
        }

        public bool Startup()
        {
            Shutdown();
            IsRunning = true;
            
            listener = new TcpListener(IPAddress.Any, 25);
            listener.Start();
            listener.BeginAcceptSocket(OnClientConnect, null);

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
            Socket socket = listener.EndAcceptSocket(ar);
            Task.Run(() => ProcessSingleClient(socket));

            //Have listener start listening for connections again
            listener.BeginAcceptSocket(OnClientConnect, null);
        }

        private async void ProcessSingleClient(Socket socket)
        {
            string clientIP = ((IPEndPoint)socket.RemoteEndPoint).Address.ToString();
            Log.Information("Client connected - {0}", socket.RemoteEndPoint);
            await WriteLine(socket, "220 localhost -- Knightware proxy server");

            //This will hold the properties that will be parsed out below
            MailMessage emailMessage = new MailMessage();
            const string dataEndString = ".";

            using (MemoryStream readBackBuffer = new MemoryStream())
            {
                while (IsRunning && socket != null && socket.Connected)
                {
                    try
                    {
                        string msg = await ReadLine(socket, readBackBuffer);
                        if (msg == null || msg.StartsWith("QUIT"))
                        {
                            //Connection lost.  Close now
                            socket.Close();
                            break;
                        }
                        else if(msg == string.Empty)
                        {
                            //Need to wait for additional data
                            continue;
                        }

                        //message has successfully been received
                        else if (msg.StartsWith("EHLO"))
                        {
                            Log.Debug("Received EHLO from: " + msg.Substring(5));
                            await WriteLine(socket, "250 OK");
                        }
                        else if (msg.StartsWith("RCPT TO:"))
                        {
                            //This line will have the display name, next line will have email address
                            string email = msg.Substring(8).Replace("<", "").Replace(">", "");
                            emailMessage.To.Add(email);
                            await WriteLine(socket, "250 OK");
                        }
                        else if (msg.StartsWith("MAIL FROM:"))
                        {
                            string from = msg.Substring(10).Replace("<", "").Replace(">", "");
                            emailMessage.From = new MailAddress(from);
                            await WriteLine(socket, "250 OK");
                        }
                        else if (msg.StartsWith("DATA"))
                        {
                            await WriteLine(socket, "354 Start mail input; end with <CR><LF>" + dataEndString + "<CR><LF>");
                            await ProcessClientDataSection(socket, readBackBuffer, emailMessage, dataEndString);
                            await WriteLine(socket, "250 OK");
                        }
                        else
                        {
                            Log.Warning("Unrecognized data: {0}", msg);
                            OnDataRead(msg);
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

        private async Task ProcessClientDataSection(Socket socket, MemoryStream backBuffer, MailMessage emailMessage, string terminatingMessage)
        {
            string boundaryString = null;
            string dataContentType = null;

            //Process a sub-set of lines specific to a data block
            while (IsRunning && socket.Connected)
            {
                string msg = await ReadLine(socket, backBuffer);
                if (msg == null)
                {
                    //Null, as opposed to an empty string, indicates a socket read failure
                    break;
                }
                else if (msg == string.Empty)
                {
                    //Need to wait for additional data
                    continue;
                }

                else if(msg.StartsWith("From: "))
                {
                    //Should I be doing something with this?
                }
                else if(msg.StartsWith("To: "))
                {
                    //Should I be doing something with this?
                }
                else if (msg.StartsWith("Subject: "))
                {
                    emailMessage.Subject = msg.Substring(9);
                }
                else if(msg.StartsWith("Reply-To:"))
                {
                    emailMessage.ReplyToList.Add(CleanEmailString(msg, "Reply-To:"));
                }
                else if (msg.StartsWith("Content-Type:"))
                {
                    //Expecting something like this: Content-Type: multipart/mixed; boundary="__EmAiL_MiMe_BoUnDaRy_StRiNg__"
                    string[] parts = msg.Split(new string[] { ": ", "; ", "=" }, StringSplitOptions.RemoveEmptyEntries);
                    dataContentType = parts[1];
                    for (int i = 2; i < parts.Length; i++)
                    {
                        if (parts[i] == "boundary")
                        {
                            boundaryString = parts[i + 1].Replace("\"", "");
                        }
                    }
                }
                else if (boundaryString != null && msg == "--" + boundaryString)
                {
                    //Process a full section of data
                    await ProcessClientDataBoundary(socket, emailMessage, backBuffer, boundaryString);
                }
                else if (msg == terminatingMessage)
                {
                    //We're done receiving data
                    break;
                }
            }
        }

        private async Task ProcessClientDataBoundary(Socket socket, MailMessage emailMessage, MemoryStream backBuffer, string boundaryString)
        {
            string contentType = null;
            string contentDescription = null;
            string contentDisposition = null;
            Encoding contentTransferEncoding = null;
            StringBuilder encodedData = new StringBuilder();
            bool readingEncodedData = false;
            bool isAttachment = false;
            string fileName = null;

            //Process a sub-set of lines specific to a data block
            while (IsRunning && socket.Connected)
            {
                string msg = await ReadLine(socket, backBuffer);
                if (msg == null)
                {
                    //Null, as opposed to an empty string, indicates a socket read failure
                    break;
                }
                else if(msg == string.Empty)
                {
                    //Flip our encode data read status
                    readingEncodedData = !readingEncodedData;
                }
                else if(msg == boundaryString || msg == "--" + boundaryString || msg == "--" + boundaryString + "--")
                {
                    break;
                }
                
                if(readingEncodedData)
                {
                    //Once the transfer encoding has been set, start writing data
                    encodedData.Append(msg);
                }
                else if(msg.StartsWith("Content-Type: "))
                {
                    contentType = msg.Substring(14);
                }
                else if(msg.StartsWith("Content-Description: "))
                {
                    contentDescription = msg.Substring(21);
                }
                else if(msg.StartsWith("Content-Disposition: "))
                {
                    contentDisposition = msg.Substring(21);
                    string[] split = contentDisposition.Split(new char[] { ';', '=', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    for(int i=0 ; i<split.Length ; i++)
                    {
                        string part = split[i].Replace(" ", "");
                        if (part == "attachment")
                            isAttachment = true;
                        else if (part == "filename")
                            fileName = split[i + 1].Replace("\"", "");
                    }
                }
                else if(msg.StartsWith("Content-Transfer-Encoding: "))
                {
                    string encodingType = msg.Substring(27);
                    if(encodingType == "base64")
                    {
                        contentTransferEncoding = Encoding.Unicode;
                    }
                    else
                    {
                        throw new NotSupportedException(string.Format("Encoding type of {0} is not implemented", encodingType));
                    }

                    //As soon as the encoding type is provided, we are in data reader mode
                    readingEncodedData = true;
                }
            }

            //Did we just parse out an attachment?
            if(isAttachment && encodedData.Length > 0)
            {
                //encodedData.Seek(0, SeekOrigin.Begin);
                //emailMessage.Attachments.Add(new Attachment(encodedData, contentType));

                //DEBUG
                //string base64TempFile = Path.Combine(@"c:\temp", fileName + ".base64.txt");
                //string encodedDataString = encod
                //File.WriteAllText(base64TempFile, encodedData.ToString());

                //string tempFile = Path.Combine(@"c:\temp", fileName);
                //File.WriteAllBytes()

                //Process.Start(base64TempFile);
            }
        }

        private string CleanEmailString(string emailLine, string prefixToRemove = "")
        {
            return emailLine.Substring(prefixToRemove.Length).Replace("<", "").Replace(">", "").Replace(" ", "");
        }

        private Task WriteLine(Socket socket, string message)
        {
            OnDataWritten(message);

            TaskFactory factory = new TaskFactory();
            return factory.FromAsync(
                (callback, state) => 
                    {
                        byte[] buffer = Encoding.ASCII.GetBytes(message + string.Intern("\r\n"));
                        return socket.BeginSend(buffer, 0, buffer.Length, SocketFlags.None, callback, state);
                    },
                (result) =>
                {
                    try
                    {
                        if (IsRunning)
                            socket.EndSend(result);
                    }
                    catch (Exception ex)
                    {
                        Log.Warning("{0} occurred while processing send: {1}", ex.GetType().Name, ex.Message);
                    }
                },
                    null);
        }

        private async Task<string> ReadLine(Socket socket, MemoryStream backBuffer)
        {
            //First, let's process the back buffer
            string backBufferLine = ProcessReadBackBuffer(backBuffer);
            if (!string.IsNullOrEmpty(backBufferLine))
                return backBufferLine;

            //Let's read some socket data
            byte[] buffer = new byte[8192];
            TaskFactory factory = new TaskFactory();
            int bytesRead = await factory.FromAsync(
                (callback, state) => socket.BeginReceive(buffer, 0, buffer.Length, SocketFlags.None, callback, state),
                (result) =>
                {
                    try
                    {
                        if (IsRunning)
                            return socket.EndReceive(result);
                    }
                    catch (Exception ex)
                    {
                        Log.Warning("{0} occurred while processing read: {1}", ex.GetType().Name, ex.Message);
                    }
                    return -1;
                },
                    null);

            //Now let's process the full backbuffer
            if (bytesRead > 0)
            {
                backBuffer.Write(buffer, 0, bytesRead);
                string response = ProcessReadBackBuffer(backBuffer);

                //For debug, we're writing out explicit \r\n text...
                if(!string.IsNullOrEmpty(response))
                    OnDataRead(response.Replace("\r\n", "<\\r><\\n>\r\n"));

                return response;
            }
            return string.Empty;
        }

        private string ProcessReadBackBuffer(MemoryStream backBuffer)
        {
            //Now let's process the full backbuffer
            if (backBuffer.Position > 0)
            {
                int totalBytesRead;
                string backbufferLine = ReadLine(backBuffer.GetBuffer(), 0, (int)backBuffer.Position, out totalBytesRead);
                if (totalBytesRead > 0)
                {
                    //We found a line in the back buffer.  Let's update our back buffer and return it directly
                    byte[] remainingBytes = new byte[backBuffer.Position - totalBytesRead];
                    backBuffer.Seek(totalBytesRead, SeekOrigin.Begin);
                    backBuffer.Read(remainingBytes, 0, remainingBytes.Length);

                    backBuffer.Seek(0, SeekOrigin.Begin);
                    backBuffer.Write(remainingBytes, 0, remainingBytes.Length);

                    return backbufferLine;
                }
            }
            return string.Empty;
        }

        private string ReadLine(byte[] buffer, int startPosition, int maxLength, out int totalBytesRead)
        {
            if (buffer != null && buffer.Length > 0)
            {
                for (int i = startPosition; i < maxLength - 1; i++)
                {
                    if (buffer[i] == '\r' && buffer[i + 1] == '\n')
                    {
                        string response = Encoding.ASCII.GetString(buffer, 0, i);
                        totalBytesRead = i + 2;
                        return response;
                    }
                }
            }
            totalBytesRead = 0;
            return string.Empty;
        }
    }
}
