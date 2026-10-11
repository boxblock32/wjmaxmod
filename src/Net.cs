using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.WebSockets;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace WJMAXOnline
{
	// 줄 단위 텍스트 메시지를 주고받는 TCP 연결 하나
	public class Conn
	{
		// 한 줄 최대 길이: 넘으면 악의적/깨진 연결로 보고 끊는다 (메모리 고갈 방지)
		public const int MAX_LINE = 2048;

		public int id = -1;

		public int cid; // 중계 손님 번호 (TCP 연결이면 0)

		public volatile bool closed;

		public readonly string remoteIp;

		// 마지막으로 줄을 받은 시각 (Environment.TickCount). 응답 없는 연결 정리용
		public volatile int lastRecv = Environment.TickCount;

		// 방장: 도배 막기용 (5초 동안 받은 줄 수, 마지막 채팅 시각)
		public int rateAt = Environment.TickCount, rateN, chatAt = Environment.TickCount - 10000;

		private readonly TcpClient tcp;

		private readonly StreamWriter writer;

		// 클라우드 중계 연결이면 TCP 대신 이걸로 보내고 닫는다 (받은 줄은 Feed 로 들어옴)
		private readonly Action<string> sendFn;

		private readonly Action closeFn;

		private Action<Conn, string> onLine;

		private bool feedClosed;

		public Conn(string ip, Action<string> send, Action close)
		{
			remoteIp = ip;
			sendFn = send;
			closeFn = close;
		}

		// 중계에서 받은 줄 (null = 끊김, 한 번만)
		public void Feed(string line)
		{
			if (line == null)
			{
				if (onLine == null || feedClosed)
				{
					return;
				}
				feedClosed = true;
				closed = true;
			}
			else
			{
				lastRecv = Environment.TickCount;
			}
			onLine?.Invoke(this, line);
		}

		public Conn(TcpClient t)
		{
			tcp = t;
			t.NoDelay = true;
			// 상대가 안 읽어도 게임이 멈추지 않게 전송 대기 제한
			t.SendTimeout = 2000;
			remoteIp = (t.Client.RemoteEndPoint as IPEndPoint)?.Address.ToString() ?? "";
			writer = new StreamWriter(t.GetStream(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false))
			{
				AutoFlush = true,
				NewLine = "\n"
			};
		}

		public void Start(Action<Conn, string> onLine)
		{
			if (tcp == null)
			{
				this.onLine = onLine;
				return;
			}
			new Thread((ThreadStart)delegate
			{
				try
				{
					StreamReader r = new StreamReader(tcp.GetStream(), Encoding.UTF8);
					StringBuilder sb = new StringBuilder();
					int ch;
					while (!closed && (ch = r.Read()) >= 0)
					{
						if (ch == '\n')
						{
							lastRecv = Environment.TickCount;
							onLine(this, sb.ToString().TrimEnd('\r'));
							sb.Length = 0;
						}
						else if (sb.Length >= MAX_LINE)
						{
							break;
						}
						else
						{
							sb.Append((char)ch);
						}
					}
				}
				catch (Exception)
				{
				}
				closed = true;
				onLine(this, null);
			})
			{
				IsBackground = true
			}.Start();
		}

		public void Send(string msg)
		{
			if (closed)
			{
				return;
			}
			if (sendFn != null)
			{
				sendFn(msg);
				return;
			}
			try
			{
				lock (writer)
				{
					writer.WriteLine(msg);
				}
			}
			catch (Exception)
			{
				Close();
			}
		}

		public void Close()
		{
			closed = true;
			try
			{
				closeFn?.Invoke();
				tcp?.Close();
			}
			catch (Exception)
			{
			}
		}
	}

	// 방장: 서버 + 접속자 목록 / 참가자: 서버 연결 하나. 수신은 메인 스레드에서 Poll 로 꺼낸다.
	public class Net
	{
		public struct Msg
		{
			public Conn from;

			public string line; // null = 연결 끊김
		}

		private readonly Queue<Msg> inbox = new Queue<Msg>();

		private TcpListener listener;

		public readonly List<Conn> clients = new List<Conn>();

		public Conn server;

		private void Post(Conn c, string line)
		{
			lock (inbox)
			{
				inbox.Enqueue(new Msg
				{
					from = c,
					line = line
				});
			}
		}

		public bool Poll(out Msg m)
		{
			lock (inbox)
			{
				if (inbox.Count > 0)
				{
					m = inbox.Dequeue();
					return true;
				}
			}
			m = default(Msg);
			return false;
		}

		public bool Hosting => listener != null;

		public void Host(int port)
		{
			listener = new TcpListener(IPAddress.Any, port);
			listener.Start();
			TcpListener l = listener;
			new Thread((ThreadStart)delegate
			{
				try
				{
					while (true)
					{
						Conn c = new Conn(l.AcceptTcpClient());
						Post(c, "__JOIN");
						c.Start(Post);
					}
				}
				catch (Exception)
				{
				}
			})
			{
				IsBackground = true
			}.Start();
		}

		public void Join(string host, int port)
		{
			new Thread((ThreadStart)delegate
			{
				try
				{
					TcpClient t = new TcpClient();
					IAsyncResult ar = t.BeginConnect(host, port, null, null);
					if (!ar.AsyncWaitHandle.WaitOne(8000))
					{
						t.Close();
						throw new TimeoutException("응답 없음");
					}
					t.EndConnect(ar);
					Conn c = new Conn(t);
					server = c;
					Post(c, "__CONNECTED");
					c.Start(Post);
				}
				catch (Exception e)
				{
					Post(null, "__FAIL|" + e.Message);
				}
			})
			{
				IsBackground = true
			}.Start();
		}

		// ---------------- 클라우드 중계 (Cloudflare Worker) ----------------
		// 방장: 중계에 방 코드로 방을 열고, 손님마다 가상 연결을 만든다.
		// 중계 -> 방장: "J 번호 IP" / "M 번호 줄" / "L 번호",  방장 -> 중계: "번호 줄" / "K 번호"(내보내기)
		private Ws relayWs;

		public void HostRelay(string url, string code)
		{
			Ws ws = relayWs = new Ws();
			Dictionary<int, Conn> conns = new Dictionary<int, Conn>();
			new Thread((ThreadStart)delegate
			{
				try
				{
					ws.Open(url + "/r/" + code + "?role=host");
				}
				catch (Exception e)
				{
					Post(null, "__RELAYFAIL|" + Ws.Why(e));
					return;
				}
				relayAccepted = "";
				Post(null, "__RELAYOK");
				ws.Run(delegate(string msg)
				{
					string[] p = msg.Split(new char[1] { ' ' }, 3);
					if (p.Length < 2 || !int.TryParse(p[1], out var cid))
					{
						return;
					}
					switch (p[0])
					{
					case "J":
					{
						Conn c = new Conn((p.Length > 2) ? p[2] : "", (string s) => ws.Send(cid + " " + s), delegate
						{
							ws.Send("K " + cid);
						});
						c.cid = cid;
						conns[cid] = c;
						Post(c, "__JOIN");
						c.Start(Post);
						break;
					}
					case "M":
						if (p.Length > 2 && conns.TryGetValue(cid, out var m))
						{
							m.Feed(p[2]);
						}
						break;
					case "L":
						if (conns.TryGetValue(cid, out var l))
						{
							conns.Remove(cid);
							l.Feed(null);
						}
						break;
					}
				}, delegate(bool byMe)
				{
					foreach (Conn c in conns.Values)
					{
						c.Feed(null);
					}
					conns.Clear();
					if (!byMe)
					{
						Post(null, "__RELAYLOST");
					}
				});
			})
			{
				IsBackground = true
			}.Start();
		}

		// 손님: 중계에 방 코드로 접속. 줄이 그대로 오간다
		public void JoinRelay(string url, string code)
		{
			Ws ws = relayWs = new Ws();
			new Thread((ThreadStart)delegate
			{
				try
				{
					ws.Open(url + "/r/" + code);
				}
				catch (Exception e)
				{
					Post(null, "__FAIL|" + Ws.Why(e));
					return;
				}
				Conn c = new Conn("relay", ws.Send, ws.Close);
				server = c;
				Post(c, "__CONNECTED");
				c.Start(Post);
				ws.Run(c.Feed, delegate
				{
					c.Feed(null);
				});
			})
			{
				IsBackground = true
			}.Start();
		}

		private string relayAccepted = "";

		// 중계 손님에게는 "B" 한 줄만 보내고 중계 서버가 나눠 준다 (방장 업로드가 인원수만큼 늘지 않음)
		public void Broadcast(string msg, Conn except = null)
		{
			bool relay = relayWs != null && !relayWs.closed;
			if (relay)
			{
				string acc = string.Join(",", from c in clients
					where c.cid > 0 && c.id >= 0 && !c.closed
					select c.cid);
				if (acc != relayAccepted)
				{
					relayAccepted = acc;
					relayWs.Send("A " + acc);
				}
				if (acc.Length > 0)
				{
					relayWs.Send("B " + ((except != null) ? except.cid : 0) + " " + msg);
				}
			}
			foreach (Conn c in clients.ToArray())
			{
				if (c != except && c.id >= 0 && !(relay && c.cid > 0))
				{
					c.Send(msg);
				}
			}
		}

		// 공개 방 목록에 올리기 / 내리기("-")
		public void RelayPublic(string info)
		{
			relayWs?.Send("P " + info);
		}

		public void Close()
		{
			try
			{
				listener?.Stop();
			}
			catch (Exception)
			{
			}
			listener = null;
			relayWs?.Close();
			foreach (Conn c in clients)
			{
				c.Close();
			}
			clients.Clear();
			server?.Close();
			server = null;
			lock (inbox)
			{
				inbox.Clear();
			}
		}
	}

	// 웹소켓 하나: 받기는 Run 을 부른 스레드, 보내기는 큐 + 전용 스레드 (게임 프레임이 네트워크를 기다리지 않게)
	public class Ws
	{
		private readonly ClientWebSocket ws = new ClientWebSocket();

		private readonly BlockingCollection<string> outq = new BlockingCollection<string>(4096);

		public volatile bool closed;

		public void Open(string url)
		{
			ws.Options.KeepAliveInterval = TimeSpan.FromSeconds(20.0);
			if (!ws.ConnectAsync(new Uri(url), CancellationToken.None).Wait(10000))
			{
				throw new TimeoutException("응답 없음");
			}
			new Thread((ThreadStart)delegate
			{
				try
				{
					StringBuilder sb = new StringBuilder();
					foreach (string s in outq.GetConsumingEnumerable())
					{
						// 그 사이 밀린 줄은 한 메시지로 묶어 보낸다 (중계 서버 메시지 수·지연 감소)
						sb.Length = 0;
						sb.Append(s);
						string more;
						while (sb.Length < 16000 && outq.TryTake(out more))
						{
							sb.Append('\n').Append(more);
						}
						byte[] b = Encoding.UTF8.GetBytes(sb.ToString());
						if (!ws.SendAsync(new ArraySegment<byte>(b), WebSocketMessageType.Text, endOfMessage: true, CancellationToken.None).Wait(5000))
						{
							break;
						}
					}
				}
				catch (Exception)
				{
				}
				// 큐에 남은 줄(나가기 BYE 등)을 다 보낸 뒤에 닫는다
				Close();
				Shut();
			})
			{
				IsBackground = true
			}.Start();
			sending = true;
		}

		private volatile bool sending;

		private void Shut()
		{
			try
			{
				ws.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None).Wait(2000);
			}
			catch (Exception)
			{
			}
		}

		// 받은 메시지마다 onMsg, 끊기면 onClosed(내가 닫았는지)
		public void Run(Action<string> onMsg, Action<bool> onClosed)
		{
			byte[] buf = new byte[8192];
			MemoryStream ms = new MemoryStream();
			try
			{
				while (ws.State == WebSocketState.Open)
				{
					WebSocketReceiveResult r = ws.ReceiveAsync(new ArraySegment<byte>(buf), CancellationToken.None).Result;
					if (r.MessageType == WebSocketMessageType.Close)
					{
						break;
					}
					ms.Write(buf, 0, r.Count);
					if (ms.Length > 131072)
					{
						break;
					}
					if (r.EndOfMessage)
					{
						string s = Encoding.UTF8.GetString(ms.GetBuffer(), 0, (int)ms.Length);
						ms.SetLength(0L);
						foreach (string line in s.Split('\n'))
						{
							if (line.Length <= Conn.MAX_LINE)
							{
								onMsg(line);
							}
						}
					}
				}
			}
			catch (Exception)
			{
			}
			bool byMe = closed;
			Close();
			onClosed(byMe);
		}

		public void Send(string s)
		{
			if (closed)
			{
				return;
			}
			try
			{
				outq.TryAdd(s);
			}
			catch (Exception)
			{
			}
		}

		public void Close()
		{
			if (closed)
			{
				return;
			}
			closed = true;
			try
			{
				outq.CompleteAdding();
			}
			catch (Exception)
			{
			}
			// 보내는 스레드가 있으면 그쪽이 남은 줄을 다 보내고 닫는다
			if (!sending)
			{
				Shut();
			}
		}

		public static string Why(Exception e)
		{
			while (e is AggregateException && e.InnerException != null)
			{
				e = e.InnerException;
			}
			return e.Message;
		}
	}

	// 중계 서버 주소: 기본은 아래 주소. OnlineMod\relay.txt 가 있으면 그 주소를 씀 (off 라고 쓰면 중계 안 씀)
	public static class Relay
	{
		private const string DEFAULT = "wss://wjmax-relay.srit-oauth-provider.workers.dev";

		public static string Url()
		{
			try
			{
				string f = Path.Combine(Path.GetDirectoryName(UnityEngine.Application.dataPath), "OnlineMod", "relay.txt");
				string u = File.Exists(f) ? File.ReadAllText(f).Trim().TrimEnd('/') : DEFAULT;
				return (u.StartsWith("wss://") || u.StartsWith("ws://")) ? u : null;
			}
			catch (Exception)
			{
				return null;
			}
		}

		// 공개 방 목록 (블로킹, 백그라운드 스레드에서). 한 줄 = 코드|버전|방식|인원|최대|게임중|방장이름
		public static string[] FetchRooms(string url)
		{
			string http = (url.StartsWith("wss://") ? "https://" : "http://") + url.Substring(url.IndexOf("://") + 3) + "/rooms";
			HttpWebRequest req = (HttpWebRequest)WebRequest.Create(http);
			req.Timeout = 6000;
			req.ReadWriteTimeout = 6000;
			using WebResponse resp = req.GetResponse();
			using StreamReader sr = new StreamReader(resp.GetResponseStream(), Encoding.UTF8);
			return sr.ReadToEnd().Split(new char[1] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
		}

		// 중계 방 코드: R + 무작위 9자리 (IP 코드는 첫 글자가 0~7 이라 안 겹침)
		public static string NewCode()
		{
			byte[] b = new byte[9];
			using (System.Security.Cryptography.RandomNumberGenerator g = System.Security.Cryptography.RandomNumberGenerator.Create())
			{
				g.GetBytes(b);
			}
			return "R" + new string(b.Select((byte x) => RoomCode.Alphabet[x & 0x1F]).ToArray());
		}

		public static bool IsCode(string code)
		{
			string c = RoomCode.Clean(code ?? "");
			return c.Length == RoomCode.LENGTH && c[0] == 'R' && c.All((char ch) => RoomCode.Alphabet.IndexOf(ch) >= 0);
		}
	}

	// 방 코드 = 방장 IP(4바이트) + 포트(2바이트)를 10자리 문자로 (헷갈리는 I,L,O,U 제외). 예) 6WADQ-3Y253
	public static class RoomCode
	{
		public const int LENGTH = 10;

		private const string A = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";

		public const string Alphabet = A;

		public static string Encode(string ip, int port)
		{
			byte[] b = IPAddress.Parse(ip).GetAddressBytes();
			ulong v = ((ulong)b[0] << 40) | ((ulong)b[1] << 32) | ((ulong)b[2] << 24) | ((ulong)b[3] << 16) | (ushort)port;
			char[] c = new char[LENGTH];
			for (int i = LENGTH - 1; i >= 0; i--)
			{
				c[i] = A[(int)(v & 0x1F)];
				v >>= 5;
			}
			return Format(new string(c));
		}

		public static bool Decode(string code, out string ip, out int port)
		{
			ip = null;
			port = 0;
			code = Clean(code);
			if (code.Length != LENGTH)
			{
				return false;
			}
			ulong v = 0uL;
			foreach (char ch in code)
			{
				int k = A.IndexOf(ch);
				if (k < 0)
				{
					return false;
				}
				v = (v << 5) | (uint)k;
			}
			if (v >> 48 != 0)
			{
				return false;
			}
			port = (int)(v & 0xFFFF);
			ip = ((v >> 40) & 0xFF) + "." + ((v >> 32) & 0xFF) + "." + ((v >> 24) & 0xFF) + "." + ((v >> 16) & 0xFF);
			return port > 0;
		}

		// 입력용 정리: 대문자, 구분자 제거, 헷갈리는 글자 보정
		public static string Clean(string code)
		{
			return code.ToUpperInvariant().Replace("-", "").Replace(" ", "").Replace('O', '0').Replace('I', '1').Replace('L', '1').Replace('U', 'V');
		}

		// 화면용: 5-5 로 끊어서 표시
		public static string Format(string clean)
		{
			if (clean.Length <= 5)
			{
				return clean;
			}
			return clean.Insert(5, "-");
		}
	}

	// 방장 PC의 외부 접속 주소 찾기: 공인 IP 직결 > 공유기 UPnP 자동 포트 개방 > 내부망 IP
	public static class Reach
	{
		private static string controlUrl;

		private static string service;

		private static int mappedPort;

		public static string Find(int port, out string note)
		{
			string pub = LocalIps().FirstOrDefault((string a) => !IsPrivate(a));
			if (pub != null)
			{
				note = "인터넷 직접 연결 (공인 IP)";
				return pub;
			}
			string ext = TryUpnp(port);
			if (ext != null && !IsPrivate(ext))
			{
				note = "공유기 자동 포트 개방 (UPnP)";
				return ext;
			}
			// 공유기를 못 열면 가상 랜(Radmin VPN 26.x, Hamachi 25.x, Tailscale 100.64~127.x) 주소가 있으면 그걸로
			string vpn = LocalIps().FirstOrDefault(IsVpn);
			if (vpn != null)
			{
				note = "가상 랜 주소 (Radmin VPN/Tailscale 등, 친구도 같은 네트워크에 있어야 함)";
				return vpn;
			}
			note = "공유기 UPnP를 쓸 수 없어 같은 공유기 안에서만 접속 가능 (포트 " + port + " 포워딩 필요)";
			return LocalIps().FirstOrDefault() ?? "127.0.0.1";
		}

		public static IEnumerable<string> LocalIps()
		{
			try
			{
				return (from a in Dns.GetHostAddresses(Dns.GetHostName())
					where a.AddressFamily == AddressFamily.InterNetwork
					select a.ToString()).ToArray();
			}
			catch (Exception)
			{
				return new string[0];
			}
		}

		public static bool IsVpn(string ip)
		{
			byte[] b = IPAddress.Parse(ip).GetAddressBytes();
			return b[0] == 25 || b[0] == 26 || (b[0] == 100 && b[1] >= 64 && b[1] < 128);
		}

		public static bool IsPrivate(string ip)
		{
			byte[] b = IPAddress.Parse(ip).GetAddressBytes();
			if (b[0] != 10 && b[0] != 127 && b[0] != 0 && (b[0] != 172 || b[1] < 16 || b[1] >= 32) && (b[0] != 192 || b[1] != 168) && (b[0] != 169 || b[1] != 254) && (b[0] != 100 || b[1] < 64 || b[1] >= 128))
			{
				// 25.x / 26.x 는 Hamachi·Radmin 가상망
				if (b[0] != 25)
				{
					return b[0] == 26;
				}
				return true;
			}
			return true;
		}

		private static string TryUpnp(int port)
		{
			try
			{
				string location = Discover();
				if (location == null)
				{
					return null;
				}
				string xml;
				using (WebClient wc = new WebClient())
				{
					xml = wc.DownloadString(location);
				}
				string[] array = new string[3] { "urn:schemas-upnp-org:service:WANIPConnection:2", "urn:schemas-upnp-org:service:WANIPConnection:1", "urn:schemas-upnp-org:service:WANPPPConnection:1" };
				foreach (string st in array)
				{
					int i = xml.IndexOf(st, StringComparison.Ordinal);
					Match m = (i >= 0) ? Regex.Match(xml.Substring(i), "<controlURL>(.*?)</controlURL>") : null;
					if (m != null && m.Success)
					{
						controlUrl = new Uri(new Uri(location), m.Groups[1].Value.Trim()).ToString();
						service = st;
						break;
					}
				}
				if (controlUrl == null)
				{
					return null;
				}
				string local;
				using (Socket s = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp))
				{
					s.Connect(new Uri(location).Host, 1);
					local = ((IPEndPoint)s.LocalEndPoint).Address.ToString();
				}
				// 6시간 뒤 공유기가 스스로 닫도록(강제 종료 시에도 포트가 영구히 열려 있지 않게). 임대 시간을 거부하는 공유기는 무기한으로 재시도
				foreach (int lease in new int[2] { 21600, 0 })
				{
					try
					{
						Soap("AddPortMapping", "<NewRemoteHost></NewRemoteHost><NewExternalPort>" + port + "</NewExternalPort><NewProtocol>TCP</NewProtocol><NewInternalPort>" + port + "</NewInternalPort><NewInternalClient>" + local + "</NewInternalClient><NewEnabled>1</NewEnabled><NewPortMappingDescription>WJMAX Online</NewPortMappingDescription><NewLeaseDuration>" + lease + "</NewLeaseDuration>");
						mappedPort = port;
						break;
					}
					catch (WebException)
					{
					}
				}
				if (mappedPort == 0)
				{
					return null;
				}
				Match ip = Regex.Match(Soap("GetExternalIPAddress", ""), "<NewExternalIPAddress>(.*?)</NewExternalIPAddress>");
				return ip.Success ? ip.Groups[1].Value.Trim() : null;
			}
			catch (Exception)
			{
				return null;
			}
		}

		public static void Release()
		{
			if (mappedPort == 0)
			{
				return;
			}
			int p = mappedPort;
			mappedPort = 0;
			try
			{
				Soap("DeletePortMapping", "<NewRemoteHost></NewRemoteHost><NewExternalPort>" + p + "</NewExternalPort><NewProtocol>TCP</NewProtocol>");
			}
			catch (Exception)
			{
			}
		}

		private static string Discover()
		{
			using UdpClient u = new UdpClient();
			u.Client.ReceiveTimeout = 2500;
			foreach (string st in new string[3] { "urn:schemas-upnp-org:device:InternetGatewayDevice:1", "urn:schemas-upnp-org:device:InternetGatewayDevice:2", "urn:schemas-upnp-org:service:WANIPConnection:1" })
			{
				byte[] req = Encoding.ASCII.GetBytes("M-SEARCH * HTTP/1.1\r\nHOST: 239.255.255.250:1900\r\nMAN: \"ssdp:discover\"\r\nMX: 2\r\nST: " + st + "\r\n\r\n");
				u.Send(req, req.Length, new IPEndPoint(IPAddress.Parse("239.255.255.250"), 1900));
			}
			IPEndPoint ep = new IPEndPoint(IPAddress.Any, 0);
			try
			{
				Match m = Regex.Match(Encoding.ASCII.GetString(u.Receive(ref ep)), "(?im)^location:\\s*(\\S+)");
				return m.Success ? m.Groups[1].Value : null;
			}
			catch (SocketException)
			{
				return null;
			}
		}

		private static string Soap(string action, string args)
		{
			string body = "<?xml version=\"1.0\"?><s:Envelope xmlns:s=\"http://schemas.xmlsoap.org/soap/envelope/\" s:encodingStyle=\"http://schemas.xmlsoap.org/soap/encoding/\"><s:Body><u:" + action + " xmlns:u=\"" + service + "\">" + args + "</u:" + action + "></s:Body></s:Envelope>";
			HttpWebRequest req = (HttpWebRequest)WebRequest.Create(controlUrl);
			req.Method = "POST";
			req.Timeout = 3000;
			req.ContentType = "text/xml; charset=\"utf-8\"";
			req.Headers.Add("SOAPACTION", "\"" + service + "#" + action + "\"");
			byte[] b = Encoding.UTF8.GetBytes(body);
			using (Stream s = req.GetRequestStream())
			{
				s.Write(b, 0, b.Length);
			}
			using WebResponse resp = req.GetResponse();
			using StreamReader sr = new StreamReader(resp.GetResponseStream());
			return sr.ReadToEnd();
		}
	}
}
