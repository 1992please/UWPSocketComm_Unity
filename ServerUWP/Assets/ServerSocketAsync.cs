using System;
using System.Net.Sockets;
using System.Net;
using System.Threading;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
[Serializable]
public class ObjectToTransfer
{
    public int PlayerID;
    public int ObjectID;
    public byte[] TextureRawData;
    public int Width;
    public int Height;
    public TextureFormat Format;
    public bool mipmap;
    public ObjectToTransfer(int _PlayerID, int _ObjectID, Texture2D ScannedTexture)
    {
        PlayerID = _PlayerID;
        ObjectID = _ObjectID;
        TextureRawData = ScannedTexture.GetRawTextureData();
        Width = ScannedTexture.width;
        Height = ScannedTexture.height;
        Format = ScannedTexture.format;
        mipmap = ScannedTexture.mipmapCount > 1;
    }

    public Texture2D GetTexture()
    {
        Texture2D OutputTexture = new Texture2D(Width, Height, Format, mipmap);
        OutputTexture.LoadRawTextureData(TextureRawData);
        OutputTexture.Apply();
        return OutputTexture;
    }
}
public class StateObject
{
    // Client  socket.
    public Socket workSocket = null;
    // Size of receive buffer.
    public const int BufferSize = 1024;
    // Receive buffer.
    public byte[] buffer = new byte[BufferSize];
    // Received data string.
    public byte[] Data;

    public int DataLength = -1;

    public int BytesLeft = 0;

    public int BytesRead = 0;
}


public class ServerSocketAsync : MonoBehaviour
{
    public Text testText;
    static string test = "";

    public static int PortNum = 3003;

    public RawImage image;
    // Thread signal.
    public static ManualResetEvent allDone = new ManualResetEvent(false);

    static ObjectToTransfer obj;
    static Socket listener;
    private void Start()
    {
        //RecieveThread = new Thread(StartListening);
        //RecieveThread.Start();
        StartListening();
    }

    void Update()
    {
        if (obj != null)
        {
            image.texture = obj.GetTexture();
        }
        testText.text = test;
    }

    public static void StartListening()
    {

        // Establish the local endpoint for the socket.
        // The DNS name of the computer
        // running the listener is "host.contoso.com".
        test += "" + (GetIPAddress());
        IPEndPoint localEndPoint = new IPEndPoint(IPAddress.Any, PortNum);

        // Create a TCP/IP socket.
        listener = new Socket(AddressFamily.InterNetwork,
            SocketType.Stream, ProtocolType.Tcp);

        // Bind the socket to the local endpoint and listen for incoming connections.
        try
        {
            listener.Bind(localEndPoint);
            listener.Listen(100);
            // Set the event to nonsignaled state.
            allDone.Reset();
            listener.BeginAccept(new AsyncCallback(AcceptCallback), listener);
            //while (true)
            //{


            //    // Start an asynchronous socket to listen for connections.
            //    print("Waiting for a connection...");


            //    // Wait until a connection is made before continuing.
            //    allDone.WaitOne();
            //}

        }
        catch (Exception e)
        {
            test += (e.ToString());
        }
    }

    public static void AcceptCallback(IAsyncResult ar)
    {
        // Signal the main thread to continue.
        allDone.Set();

        // Get the socket that handles the client request.
        Socket listener = (Socket)ar.AsyncState;
        Socket handler = listener.EndAccept(ar);

        // Create the state object.
        StateObject state = new StateObject();
        state.workSocket = handler;
        handler.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0,
            new AsyncCallback(ReadCallback), state);
    }

    public static void ReadCallback(IAsyncResult ar)
    {
        // Retrieve the state object and the handler socket
        // from the asynchronous state object.
        StateObject state = (StateObject)ar.AsyncState;
        Socket handler = state.workSocket;

        // Read data from the client socket. 
        int bytesRead = handler.EndReceive(ar);
        if (bytesRead > 0)
        {
            if (state.DataLength <= 0)
            {
                state.DataLength = BitConverter.ToInt32(state.buffer, 0);
                state.BytesLeft = state.DataLength + 4 - bytesRead;
                state.Data = new byte[state.DataLength];
                Array.Copy(state.buffer, 4, state.Data, 0, bytesRead - 4);
                state.BytesRead = bytesRead - 4;
                if (state.BytesLeft > 0)
                {
                    // Not all data received. Get more.
                    handler.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0,
                    new AsyncCallback(ReadCallback), state);
                }
                else
                {
                    ConstructObj(state.Data);
                }
            }
            else
            {
                // There  might be more data, so store the data received so far
                Array.Copy(state.buffer, 0, state.Data, state.BytesRead, bytesRead);
                state.BytesRead += bytesRead;
                state.BytesLeft -= bytesRead;
                if (state.BytesLeft > 0)
                {
                    // Not all data received. Get more.
                    handler.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0,
                    new AsyncCallback(ReadCallback), state);
                }
                else
                {
                    // All the data has been read from the 
                    // client. Display it on the console.
                    ConstructObj(state.Data);
                    // Echo the thanks back to the client.
                    // Send(handler, content);
                }
            }
        }
        else
        {
            test += ("WTF");
        }
    }

    static void ConstructObj(byte[] Data)
    {
        //print("Read " + Data.Length + " bytes from socket. \n Data :");
        //test += (Data.Length);
        //test += (Data[1]);
        //test += (Data[3]);
        listener.BeginAccept(new AsyncCallback(AcceptCallback), listener);

        obj = JsonUtility.FromJson<ObjectToTransfer>(Encoding.UTF8.GetString(Data));
    }

    private static void Send(Socket handler, String data)
    {
        // Convert the string data to byte data using ASCII encoding.
        byte[] byteData = Encoding.UTF8.GetBytes(data);

        // Begin sending the data to the remote device.
        handler.BeginSend(byteData, 0, byteData.Length, 0,
            new AsyncCallback(SendCallback), handler);
    }

    private static void SendCallback(IAsyncResult ar)
    {
        try
        {
            // Retrieve the socket from the state object.
            Socket handler = (Socket)ar.AsyncState;

            // Complete sending the data to the remote device.
            int bytesSent = handler.EndSend(ar);
            print("Sent" + bytesSent + "bytes to client.");

            handler.Shutdown(SocketShutdown.Both);
            handler.Close();

        }
        catch (Exception e)
        {
            print(e.ToString());
        }
    }

    private void OnDestroy()
    {
        // RecieveThread.Abort();
    }


    public static string GetIPAddress()
    {
        var host = Dns.GetHostEntry(Dns.GetHostName());
        foreach (var ip in host.AddressList)
        {
            if (ip.AddressFamily == AddressFamily.InterNetwork)
            {
                return ip.ToString();
            }
        }
        throw new Exception("Local IP Address Not Found!");
    }
}
