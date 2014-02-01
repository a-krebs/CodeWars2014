// code primarily from http://forum.codecall.net/classes-code-snippets/26089-c-example-high-performance-tcp-server-client.html
// Edited by Windward Studios, Inc. (www.windward.net). No copyright claimed by Windward on changes.

using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using log4net;

namespace PlayerCSharpAI2
{
	public class TcpClient
	{
		private const int BUFFER_SIZE = 65536*4;
		private const int port = 1707;
		private IPEndPoint clientEndpoint;
		private ConnectionInfo connection;
		private IPlayerCallback playerCallback;

		private static readonly ILog log = LogManager.GetLogger(typeof (TcpClient));

		private class ConnectionInfo
		{
			public ConnectionInfo()
			{
				messageBuffer = new FifoByteBuffer(BUFFER_SIZE*2);
				SocketReadBuffer = new byte[BUFFER_SIZE];
			}

			public Socket Socket { get; set; }
			public byte[] SocketReadBuffer { get; private set; }

			// we store up a message here.
			// length: 0  => have less than 4 bytes in (including 0)
			//         >0 => have part/all of a message - length bytes have been removed from the buffer
			private readonly FifoByteBuffer messageBuffer;
			private int messageLength;

			public void ReceivedData(int bytesRead)
			{
				messageBuffer.Write(SocketReadBuffer, 0, bytesRead);
			}

			public bool HasMessage
			{
				get
				{
					// we may need to extract the length
					if (messageLength == 0 && messageBuffer.Count >= 4)
						messageLength = BitConverter.ToInt32(messageBuffer.Read(4), 0);
					return messageLength > 0 && messageLength <= messageBuffer.Count;
				}
			}

			public string Message
			{
				get
				{
					string rtn = Encoding.UTF8.GetString(messageBuffer.Read(messageLength));
					messageLength = 0;
					return rtn;
				}
			}
		}

		public void Start(IPlayerCallback callback, string address)
		{
			try
			{
				// lookup IP address if name
				if (address.Any(ch => (!char.IsNumber(ch)) && (ch != '.')))
				{
					IPAddress[] addresslist = Dns.GetHostAddresses(address);
					address = addresslist[0].ToString();
				}

				playerCallback = callback;
				IPAddress add = IPAddress.Parse(address);
				clientEndpoint = new IPEndPoint(add, port);

				Socket clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp) { SendBufferSize = BUFFER_SIZE, ReceiveBufferSize = BUFFER_SIZE };
				connection = new ConnectionInfo { Socket = clientSocket };
				connection.Socket.Connect(clientEndpoint);
				;
				connection.Socket.BeginReceive(connection.SocketReadBuffer, 0, connection.SocketReadBuffer.Length, SocketFlags.None,
					ReceiveCallback, connection);
			}
			catch (Exception ex)
			{
				log.Warn("Start", ex);
				throw;
			}
		}

		private void ReceiveCallback(IAsyncResult result)
		{
			ConnectionInfo info = (ConnectionInfo) result.AsyncState;
			try
			{
				int bytesRead = info.Socket.EndReceive(result);
				if (0 == bytesRead)
				{
					Close();
					return;
				}

				lock (connection)
					connection.ReceivedData(bytesRead);

				// only way we have multiple messages is an error on the server side - but that could happen.
				while (true)
				{
					string message;
					lock (connection)
					{
						if (!connection.HasMessage)
							break;
						message = connection.Message;
					}
					playerCallback.IncomingMessage(message);
				}

				connection.Socket.BeginReceive(connection.SocketReadBuffer, 0, connection.SocketReadBuffer.Length, SocketFlags.None,
					ReceiveCallback, connection);
			}
			catch (Exception ex)
			{
				log.Warn("ReceiveCallback", ex);
				Close();
				playerCallback.ConnectionLost(ex);
			}
		}

		public void SendMessage(string msg)
		{
			if (string.IsNullOrEmpty(msg))
			{
				log.Warn("ERROR sending 0 byte message");
				Console.Out.WriteLine("ERROR sending 0 byte message");
				return;
			}
			try
			{
				byte[] bytes = Encoding.UTF8.GetBytes(msg);
				byte[] length = BitConverter.GetBytes(bytes.Length);
				connection.Socket.Send(length, length.Length, SocketFlags.None);
				for (int offset = 0; offset < bytes.Length; offset += BUFFER_SIZE)
					connection.Socket.Send(bytes, offset, Math.Min(bytes.Length - offset, BUFFER_SIZE), SocketFlags.None);
			}
			catch (Exception ex)
			{
				log.Warn("SendMessage", ex);
				Console.Out.WriteLine("ERROR sending message - exception: {0}", ex);
			}
		}

		public void Close()
		{
			if (connection == null)
				return;
			try
			{
				if (connection.Socket.Connected)
					connection.Socket.Close();
			}
			catch (Exception)
			{
				// nada
			}
			connection = null;
			Thread.Sleep(50);
		}
	}
}