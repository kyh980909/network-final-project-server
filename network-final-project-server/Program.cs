using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace network_final_project_server
{
    class Program
    {
        static Socket mainSock; // 서버 소켓
        static List<Socket> connectedClients; // 클라이언트 소켓저장
        public static void StartListening()
        {
            IPEndPoint endPoint = new IPEndPoint(IPAddress.Parse("210.123.255.192"), 9000);
            mainSock = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            connectedClients = new List<Socket>();

            try
            {
                mainSock.Bind(endPoint);
                mainSock.Listen(10);
                Console.WriteLine("Server Info: {0}", endPoint);
                Console.WriteLine("Server Listening...");
                while (true)
                {
                    mainSock.BeginAccept(AcceptCallback, null);

                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }

            Console.WriteLine("\n계속하려면 엔터를 누르세요...");
            Console.Read();
        }

        public static void AcceptCallback(IAsyncResult ar)
        {
            Console.WriteLine("AcceptCallback");
            Socket client = mainSock.EndAccept(ar);
            mainSock.BeginAccept(AcceptCallback, null);

            AsyncObject obj = new AsyncObject(4096);
            obj.WorkingSocket = client;

            connectedClients.Add(client);

            client.BeginReceive(obj.Buffer, 0, 4096, 0, DataReceived, obj);
           
        }

        public static void DataReceived(IAsyncResult ar)
        {
            Console.WriteLine("DataReceived");
            AsyncObject obj = (AsyncObject) ar.AsyncState;

            try
            {
                int received = obj.WorkingSocket.EndReceive(ar);

                if (received <= 0)
                {
                    obj.WorkingSocket.Disconnect(false);
                    obj.WorkingSocket.Close();
                    return;
                }

                string text = Encoding.UTF8.GetString(obj.Buffer).Trim('\0');

                Console.WriteLine(string.Format("{0} : {1}",DateTime.Now, text));

                for(int i = connectedClients.Count - 1; i >= 0; i--)
                {
                    Socket socket = connectedClients[i];
                    if(socket != obj.WorkingSocket)
                    {
                        try
                        {
                            socket.Send(obj.Buffer);
                        } catch
                        {
                            try { socket.Dispose(); } catch { }
                            connectedClients.RemoveAt(i);
                        }
                    }
                }
                obj.WorkingSocket.Send(obj.Buffer);
                obj.ClearBuffer();
                Console.WriteLine("Send data");
                obj.WorkingSocket.BeginReceive(obj.Buffer, 0, 4096, 0, DataReceived, obj);
                Console.WriteLine("BeginReceive");
            } catch {
                Console.WriteLine(string.Format("{0} : 클라이언트 연결 종료", DateTime.Now));
            }
        }

        static void Main(string[] args)
        {
            StartListening();
        }
    }
}
