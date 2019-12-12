using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using MySql.Data.MySqlClient;

namespace network_final_project_server
{
    class Program
    {
        static Socket mainSock; // 서버 소켓
        static Dictionary<string, Socket> connectedClients; // 클라이언트 소켓저장
        public static void StartListening()
        {
            IPEndPoint endPoint = new IPEndPoint(IPAddress.Parse("192.168.1.186"), 9000);
            mainSock = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            connectedClients = new Dictionary<string, Socket>();

            try
            {
                mainSock.Bind(endPoint);
                mainSock.Listen(10);
                Console.WriteLine("Server Info: {0}", endPoint);
                Console.WriteLine("Server Listening...");
                while (true)
                {
                    mainSock.BeginAccept(AcceptCallback, null);
                    Thread.Sleep(10);
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
            Socket client = mainSock.EndAccept(ar);
            mainSock.BeginAccept(AcceptCallback, null);

            AsyncObject obj = new AsyncObject(4096);
            obj.WorkingSocket = client;
            client.BeginReceive(obj.Buffer, 0, 4096, 0, DataReceived, obj);
        }

        public static void DataReceived(IAsyncResult ar)
        {
            AsyncObject obj = (AsyncObject)ar.AsyncState;

            try
            {
                int received = obj.WorkingSocket.EndReceive(ar);

                if (received <= 0)
                {
                    obj.WorkingSocket.Disconnect(false);
                    obj.WorkingSocket.Close();
                    return;
                }

                string data = Encoding.UTF8.GetString(obj.Buffer).Trim('\0');

                var Response = new JObject(); // 반환할 데이터를 담을 객체
                var readJson = JObject.Parse(data); // 받은 json 데이터 파싱
                string req = readJson["req"].ToString();
                Console.WriteLine(string.Format("{0} : {1}", DateTime.Now, readJson));
                string connStr = string.Format(@"server=localhost;
                                              user=root;
                                              password=1234;
                                              database=network");
                switch (req)
                {
                    case "login":
                        using (MySqlConnection conn = new MySqlConnection(connStr))
                        {
                            conn.Open();
                            string sql = "SELECT * FROM user WHERE id = '" + readJson["id"].ToString() + "'";
                            MySqlCommand cmd = new MySqlCommand(sql, conn);
                            MySqlDataReader rdr = cmd.ExecuteReader();

                            if (rdr.Read())
                            { // 아이디 존재 여부 체크
                                if (rdr["pw"].ToString() == readJson["pw"].ToString())
                                { // 아이디와 비밀번호가 맞는지 체크
                                    connectedClients.Add(readJson["id"].ToString(), obj.WorkingSocket);   // 로그인 성공한 유저의 아이디와 소켓을 딕셔너리에 저장
                                    Response.Add("res", "true");
                                }
                                else
                                {
                                    Response.Add("res", "false");
                                    Response.Add("result", "비밀번호가 틀렸습니다.");
                                }
                            }
                            else
                            {
                                Response.Add("res", "false");
                                Response.Add("result", "존재하지 않는 아이디 입니다.");
                            }
                        }
                        break;

                    case "register":
                        using (MySqlConnection conn = new MySqlConnection(connStr))
                        {
                            conn.Open();
                            string sql = "SELECT * FROM user WHERE id = '" + readJson["id"].ToString() + "'";
                            MySqlCommand cmd = new MySqlCommand(sql, conn);
                            MySqlDataReader rdr = cmd.ExecuteReader();

                            if (!rdr.Read())
                            {  // 아이디 중복체크
                                rdr.Close();
                                cmd = new MySqlCommand("INSERT INTO user (id,pw) VALUES ('" + readJson["id"].ToString() + "','" + readJson["pw"].ToString() + "' )", conn);
                                cmd.ExecuteNonQuery();
                                Response.Add("res", "true");
                            }
                            else
                            {
                                Response.Add("res", "false");
                                Response.Add("result", "존재하는 아이디 입니다.");
                            }
                        }
                        break;

                    case "logout":
                        Console.WriteLine(connectedClients[readJson["id"].ToString()]);
                        connectedClients.Remove(readJson["id"].ToString());
                        break;
                    case "list":
                        var onlineList = new JArray();
                        foreach (var client in connectedClients.Keys)
                        {
                            onlineList.Add(client);
                        }
                        Response.Add("result", onlineList);
                        break;
                }
                byte[] responseData = Encoding.UTF8.GetBytes(Response.ToString());
                if (req == "logout")
                {
                    foreach (var client in connectedClients)
                    {
                        Console.WriteLine("연결된 클라이언트: " + client.Key);
                    }
                }
                else if (req == "list")
                {
                    foreach (KeyValuePair<string, Socket> clients in connectedClients)
                    {
                        Socket socket = clients.Value;

                        try
                        {
                            //Console.WriteLine(clients.Key);
                            socket.Send(responseData);
                            Console.WriteLine(string.Format("{0} : {1}", DateTime.Now, Response));
                        }
                        catch
                        {
                            try
                            {
                                Console.WriteLine("sibal");
                                socket.Dispose();
                            }
                            catch { }
                        }

                    }
                }
                else
                {
                    Console.WriteLine(string.Format("{0} : {1}", DateTime.Now, Response));
                    obj.WorkingSocket.Send(responseData);
                    obj.ClearBuffer();
                    obj.WorkingSocket.BeginReceive(obj.Buffer, 0, 4096, 0, DataReceived, obj);
                }
            }
            catch
            {
                Console.WriteLine(string.Format("{0} : 클라이언트 연결 종료", DateTime.Now));
            }
        }

        static void Main(string[] args)
        {
            StartListening();
        }
    }
}