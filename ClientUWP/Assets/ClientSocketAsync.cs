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
public class ClientSocketAsync : MonoBehaviour {
    public Text outputText;
    public InputField inputText;
    public string ServerIp = "172.16.1.103";
    public Texture2D texToSend;
    // Cached Socket object that will be used by each call for the lifetime of this class
    Socket _socket = null;

    // Signaling object used to notify when an asynchronous operation is completed
    static ManualResetEvent _clientDone = new ManualResetEvent(false);

    // Define a timeout in milliseconds for each asynchronous call. If a response is not received within this 
    // timeout period, the call is aborted.
    const int TIMEOUT_MILLISECONDS = 5000;

    // The maximum size of the data buffer to use with the asynchronous socket methods
    const int MAX_BUFFER_SIZE = 1024;

    public int portNumber = 3003;

    static string text = "";
    ObjectToTransfer obj;
    private void Start()
    {
        obj = new ObjectToTransfer(4, 5, texToSend);
       // Connect();
    }
    private void Update()
    {
        outputText.text = text;
    }


    /// <summary>
    /// Attempt a TCP socket connection to the given host over the given port
    /// </summary>
    /// <param name="hostName">The name of the host</param>
    /// <param name="portNumber">The port number to connect</param>
    /// <returns>A string representing the result of this connection attempt</returns>
    public void Connect()
    {
        string result = string.Empty;

        // Create DnsEndPoint. The hostName and port are passed in to this method.
        ServerIp = inputText.text;
        IPEndPoint hostEntry = new IPEndPoint(IPAddress.Parse(ServerIp), portNumber);

        // Create a stream-based, TCP socket using the InterNetwork Address Family. 
        _socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

        // Create a SocketAsyncEventArgs object to be used in the connection request
        SocketAsyncEventArgs socketEventArg = new SocketAsyncEventArgs();
        socketEventArg.RemoteEndPoint = hostEntry;

        // Inline event handler for the Completed event.
        // Note: This event handler was implemented inline in order to make this method self-contained.
        socketEventArg.Completed += new EventHandler<SocketAsyncEventArgs>(delegate (object s, SocketAsyncEventArgs e)
        {
            // Retrieve the result of this request
            result = e.SocketError.ToString();

            print(result);
            text += result;
            // Signal that the request is complete, unblocking the UI thread
            _clientDone.Set();
            //print("Connected");
            if(result == "Success")
            {

                Send();
            }

        });

        // Sets the state of the event to nonsignaled, causing threads to block
        _clientDone.Reset();

        // Make an asynchronous Connect request over the socket
        _socket.ConnectAsync(socketEventArg);

        // Block the UI thread for a maximum of TIMEOUT_MILLISECONDS milliseconds.
        // If no response comes back within this time then proceed
        _clientDone.WaitOne(TIMEOUT_MILLISECONDS);
    }

    /// <summary>
    /// Send the given data to the server using the established connection
    /// </summary>
    /// <param name="data">The data to send to the server</param>
    /// <returns>The result of the Send request</returns>
    public string Send()
    {
        string response = "Operation Timeout";

        // We are re-using the _socket object initialized in the Connect method
        if (_socket != null)
        {
            // Create SocketAsyncEventArgs context object
            SocketAsyncEventArgs socketEventArg = new SocketAsyncEventArgs();

            // Set properties on context object
            socketEventArg.RemoteEndPoint = _socket.RemoteEndPoint;
            socketEventArg.UserToken = null;

            // Inline event handler for the Completed event.
            // Note: This event handler was implemented inline in order 
            // to make this method self-contained.
            socketEventArg.Completed += new EventHandler<SocketAsyncEventArgs>(delegate (object s, SocketAsyncEventArgs e)
            {
                response = e.SocketError.ToString();

                // Unblock the UI thread
                _clientDone.Set();
            });

            // Add the data to be sent into the buffer


            // Prepare the package and send it.
            byte[] Data = ObjectToByteArray(obj);
            //byte[] Data = { 1, 2, 10, 4, 5, 6 };
            byte[] Package = new byte[4 + Data.Length];
            byte[] dataLength = BitConverter.GetBytes(Data.Length);
            dataLength.CopyTo(Package, 0);
            Data.CopyTo(Package, 4);

            socketEventArg.SetBuffer(Package, 0, Package.Length);

            // Sets the state of the event to nonsignaled, causing threads to block
            _clientDone.Reset();

            // Make an asynchronous Send request over the socket
            _socket.SendAsync(socketEventArg);

            // Block the UI thread for a maximum of TIMEOUT_MILLISECONDS milliseconds.
            // If no response comes back within this time then proceed
            _clientDone.WaitOne(TIMEOUT_MILLISECONDS);
        }
        else
        {
            response = "Socket is not initialized";
        }

        return response;
    }


    public string Receive()
    {
        string response = "Operation Timeout";

        // We are receiving over an established socket connection
        if (_socket != null)
        {
            // Create SocketAsyncEventArgs context object
            SocketAsyncEventArgs socketEventArg = new SocketAsyncEventArgs();
            socketEventArg.RemoteEndPoint = _socket.RemoteEndPoint;

            // Setup the buffer to receive the data
            socketEventArg.SetBuffer(new Byte[MAX_BUFFER_SIZE], 0, MAX_BUFFER_SIZE);

            // Inline event handler for the Completed event.
            // Note: This even handler was implemented inline in order to make 
            // this method self-contained.
            socketEventArg.Completed += new EventHandler<SocketAsyncEventArgs>(delegate (object s, SocketAsyncEventArgs e)
            {
                if (e.SocketError == SocketError.Success)
                {
                    // Retrieve the data from the buffer
                    response = Encoding.UTF8.GetString(e.Buffer, e.Offset, e.BytesTransferred);
                    response = response.Trim('\0');
                }
                else
                {
                    response = e.SocketError.ToString();
                }

                _clientDone.Set();
            });

            // Sets the state of the event to nonsignaled, causing threads to block
            _clientDone.Reset();

            // Make an asynchronous Receive request over the socket
            _socket.ReceiveAsync(socketEventArg);

            // Block the UI thread for a maximum of TIMEOUT_MILLISECONDS milliseconds.
            // If no response comes back within this time then proceed
            _clientDone.WaitOne(TIMEOUT_MILLISECONDS);
        }
        else
        {
            response = "Socket is not initialized";
        }

        return response;
    }


    public static byte[] ObjectToByteArray(ObjectToTransfer obj)
    {
        return Encoding.UTF8.GetBytes(JsonUtility.ToJson(obj));
    }

    public static ObjectToTransfer ByteArrayToObject(byte[] arrBytes)
    {
        return JsonUtility.FromJson<ObjectToTransfer>(ASCIIEncoding.UTF8.GetString(arrBytes));
    }
}
