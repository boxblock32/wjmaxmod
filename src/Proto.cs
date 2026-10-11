// 통신: 방 만들기/참가/나가기, 메시지 처리, 명단, 프로필, 재접속, 핑.
// 메시지는 "종류|값|값..." 한 줄. 방장이 기준이고 손님 메시지는 방장이 검사한 뒤 다른 손님에게 전달한다.
//
// 손님 → 방장: HELLO|이름|버전|방코드|토큰  PROF|..  READY|id|0/1  DF|id|난이도|키|플러스  TEAM|id|팀  CHAT|id|글
//               PH|id|위치  ROOM|id  LOADED|id  SC|id|점수|콤보|정확도|HP|브레이크  FIN|id|..  BAN|id|번호  PING|시각|핑  BYE
// 방장 → 손님: WELCOME|id|코드  DENY  VERSION  FULL  BYE  ROSTER|최대|코드|방식|진행중id들|명단..  SEL|파일|키|카테고리
//               PLAY|파일|키|강제|난이도|판정|참가id들  LD|로딩|전체  GO  LAD|..  (DF, PROF, CHAT, SC, FIN 은 전달)
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEngine;

namespace WJMAXOnline
{
	public partial class OnlineMod
	{
		private string portText = "7777";

		private string codeText = "";

		private const int MAX_FAILS = 5;

		private readonly Dictionary<string, int> joinFails = new Dictionary<string, int>();

		private string joinCode = "";

		private readonly HashSet<string> kicked = new HashSet<string>(); // 방장: 내보낸 사람 토큰 (같은 방에 다시 못 들어옴)

		// 방장: 손님 내보내기
		private void Kick(int id)
		{
			Player pl = Find(id);
			if (pl == null || id == myId)
			{
				return;
			}
			if (pl.token.Length >= 8)
			{
				kicked.Add(pl.token);
			}
			foreach (Conn c in net?.clients.Where((Conn x) => x.id == id).ToList() ?? new List<Conn>())
			{
				c.Send("KICK");
				c.id = -1;
				c.Close();
			}
			AddChat("|" + pl.name + " 님을 내보냈어요");
			RemovePlayer(id);
		}

		// ---------------- 방 만들기 / 참가 / 나가기 ----------------
		private void StartHost(int type, int max)
		{
			if (!int.TryParse(portText, out var port) || port <= 0 || port > 65535)
			{
				Status("포트 번호가 잘못됐어요", Ui.Red);
				PlaySe(AudioManager.SE.error);
				return;
			}
			Disconnect();
			net = new Net();
			try
			{
				net.Host(port);
			}
			catch (Exception e)
			{
				net = null;
				Status("방 만들기 실패: " + e.Message, Ui.Red, 8f);
				PlaySe(AudioManager.SE.error);
				return;
			}
			PlayerPrefs.SetString("WJONLINE_PORT", portText);
			isHost = true;
			connected = true;
			joinFails.Clear();
			myId = 0;
			nextId = 1;
			players.Clear();
			matchType = type;
			maxPlayers = Mathf.Clamp(max, 2, 8);
			Player host = new Player
			{
				id = 0,
				name = myName,
				fighter = true
			};
			SetMyProfile(host);
			players.Add(host);
			roomCode = "코드 만드는 중...";
			reachNote = "";
			OnJoinedRoom();
			// relay.txt 에 중계 서버가 있으면 그걸로 (포트 개방 필요 없음). 안 되면 예전처럼 내 PC 직접 연결
			relayUrl = Relay.Url();
			roomPublic = createPublic;
			pubSent = "";
			if (relayUrl != null)
			{
				relayCode = Relay.NewCode();
				relayLost = 0;
				net.HostRelay(relayUrl, relayCode);
			}
			else
			{
				FindReach(port);
				if (roomPublic)
				{
					AddChat("|중계 서버(relay.txt)가 없어서 방 찾기 목록에는 안 올라가요. 코드로 초대하세요");
				}
			}
		}

		private bool roomPublic;

		private string pubSent = "";

		private float pubAt;

		// 방장: 공개 방이면 방 찾기 목록에 정보를 올림 (바뀔 때 + 30초마다). 비공개면 "-" 로 내림
		private void PublishRoom()
		{
			if (!isHost || net == null || relayUrl == null || relayCode == null || RoomCode.Clean(roomCode) != relayCode)
			{
				return;
			}
			string info = roomPublic ? (VERSION + "|" + matchType + "|" + Mathf.Min(99, players.Count((Player p) => !p.offline)) + "|" + maxPlayers + "|" + ((inMatch || bat.phase != BAT_NONE) ? 1 : 0) + "|" + CleanName(myName)) : "-";
			if (info == pubSent && (info == "-" || Time.unscaledTime - pubAt < 30f))
			{
				return;
			}
			pubSent = info;
			pubAt = Time.unscaledTime;
			net.RelayPublic(info);
		}


		private string relayUrl;

		private string relayCode;

		private int relayLost;

		private void FindReach(int port)
		{
			new Thread((ThreadStart)delegate
			{
				string note;
				string ip = Reach.Find(port, out note);
				reachResult = ip + "|" + note;
			})
			{
				IsBackground = true
			}.Start();
		}

		// 방장: 중계 서버 소식
		private void RelayEvent(string line)
		{
			string[] p = line.Split('|');
			switch (p[0])
			{
			case "__RELAYOK":
				roomCode = RoomCode.Format(relayCode);
				reachNote = "클라우드 중계 서버";
				pubSent = "";
				PublishRoom();
				if (relayLost == 0)
				{
					AddChat("|접속 상태: 클라우드 중계 서버 (포트 열 필요 없음)");
					AddChat("|" + (roomPublic ? "공개 방: 방 찾기 목록에 보여요" : "비공개 방: 코드를 아는 사람만 들어올 수 있어요"));
				}
				else
				{
					AddChat("|중계 서버에 다시 연결됐어요");
				}
				break;
			case "__RELAYLOST":
				// 중계가 끊기면 손님들도 끊긴다. 같은 코드로 다시 열면 손님들이 재접속해 온다
				if (++relayLost <= 3)
				{
					AddChat("|중계 서버 연결이 끊겨서 다시 연결하는 중...");
					net.HostRelay(relayUrl, relayCode);
				}
				else
				{
					AddChat("|중계 서버 연결이 계속 끊겨요. 방을 다시 만들어 주세요");
				}
				break;
			case "__RELAYFAIL":
				AddChat("|중계 서버에 연결하지 못했어요" + ((p.Length > 1) ? (" (" + p[1] + ")") : "") + ". 내 PC 직접 연결로 바꿀게요");
				int.TryParse(portText, out var port);
				FindReach(port);
				break;
			}
		}

		private void StartJoin()
		{
			string ip = null;
			int port = 0;
			bool relay = Relay.IsCode(codeText);
			if (relay && Relay.Url() == null)
			{
				Status("중계 서버 주소(OnlineMod\\relay.txt)가 없어서 이 코드로 접속할 수 없어요", Ui.Red, 8f);
				PlaySe(AudioManager.SE.error);
				return;
			}
			if (!relay && !RoomCode.Decode(codeText, out ip, out port))
			{
				Status("방 코드가 올바르지 않아요 (10자리)", Ui.Red);
				PlaySe(AudioManager.SE.error);
				return;
			}
			PlayerPrefs.SetString("WJONLINE_CODE", codeText);
			Disconnect();
			joinCode = RoomCode.Clean(codeText);
			// 게임을 켜 둔 동안은 같은 토큰: 나갔다 다시 들어와도 방장이 같은 사람으로 알아본다 (끊김 자리가 남아 있으면 그 자리로)
			if (myToken.Length == 0)
			{
				myToken = Guid.NewGuid().ToString("N").Substring(0, 16).ToUpperInvariant();
			}
			hostBye = false;
			gaveUp = false;
			net = new Net();
			NetJoin(joinCode, ip, port);
			connecting = true;
			Status("방에 접속하는 중...", Ui.Gold, 999f);
		}

		// 방에 처음 들어왔을 때 (방장·손님 공통)
		private void OnJoinedRoom()
		{
			popup = Popup.None;
			chatLog.Clear();
			hostSel = "";
			sentSel = "";
			sentDiff = "";
			bat = new BattleState();
			lastMatch = null;
			ResetRoomList();
			SetScr(Scr.Room);
			PlaySe(AudioManager.SE.Confirm);
		}

		private void Disconnect()
		{
			// 일부러 나가는 것: 상대가 재접속을 기다리지 않게 알린다
			if (connected)
			{
				if (isHost)
				{
					net?.Broadcast("BYE");
				}
				else
				{
					net?.server?.Send("BYE");
				}
			}
			BattleForfeit();
			aiMatch = false;
			reconnecting = false;
			chatting = false;
			if (isHost && net != null)
			{
				new Thread(Reach.Release)
				{
					IsBackground = true
				}.Start();
			}
			net?.Close();
			net = null;
			isHost = false;
			connected = false;
			connecting = false;
			// 곡 진행 중이면 HUD·결과 화면을 위해 플레이어 목록은 남겨둔다
			if (!inMatch)
			{
				myId = -1;
				players.Clear();
			}
			pendingPlay = null;
			released = true;
			matchLive = false;
			roomCode = "";
		}

		// 방에서 나가 로비로
		private void LeaveRoom()
		{
			bool ai = aiMatch;
			Disconnect();
			if (ai)
			{
				EndAiLadder();
				Status("래더 매치를 포기해서 패배로 처리됐어요 (" + batDelta + "점)", Ui.Red, 6f);
				return;
			}
			SetScr(Scr.Lobby);
			Status("방에서 나왔어요", Ui.Gray);
		}

		private void PumpNet()
		{
			Net.Msg m;
			while (net != null && net.Poll(out m))
			{
				try
				{
					if (isHost)
					{
						HostHandle(m);
					}
					else
					{
						ClientHandle(m);
					}
				}
				catch (Exception e)
				{
					Debug.LogWarning("[WJMAXOnline] bad message: " + e.Message);
				}
			}
		}

		// 내 메시지: 방장이면 모두에게, 손님이면 방장에게
		private void Send(string line)
		{
			if (isHost)
			{
				net?.Broadcast(line);
			}
			else
			{
				net?.server?.Send(line);
			}
		}

		// ---------------- 방장: 손님 메시지 ----------------
		// 손님이 보낸 번호는 믿지 않고 연결에 붙은 번호(from.id)를 쓴다
		private void HostHandle(Net.Msg m)
		{
			Conn from = m.from;
			if (from == null)
			{
				RelayEvent(m.line);
				return;
			}
			if (m.line == "__JOIN")
			{
				joinFails.TryGetValue(from.remoteIp, out var fails);
				if (fails >= MAX_FAILS)
				{
					from.Close();
				}
				else
				{
					net.clients.Add(from);
				}
				return;
			}
			if (m.line == null)
			{
				if (net.clients.Remove(from) && from.id >= 0)
				{
					PlayerLost(from.id);
				}
				return;
			}
			string[] p = m.line.Split('|');
			if (from.id < 0)
			{
				// 인사 전에는 HELLO 만 받는다
				if (p[0] == "HELLO")
				{
					Hello(from, p);
				}
				else
				{
					from.Close();
				}
				return;
			}
			int id = from.id;
			Player pl = Find(id);
			if (pl == null)
			{
				return;
			}
			switch (p[0])
			{
			case "LOADED":
				SetLoaded(id);
				break;
			case "CHAT":
				if (p.Length >= 3 && CleanText(p[2], 60).Length > 0)
				{
					string text = CleanText(p[2], 60);
					AddChat(pl.name + "|" + text);
					net.Broadcast("CHAT|" + id + "|" + text);
				}
				break;
			case "DF":
				if (p.Length >= 5)
				{
					string df = "DF|" + id + "|" + Int(p[2], -1, (int)MODE.COUNT - 1) + "|" + Int(p[3], 0, (int)KEY.Count - 1) + "|" + ((p[4] == "1") ? 1 : 0) + "|" + ((p.Length > 5) ? Int(p[5], 1, 99) : 0);
					ApplyDiff(df.Split('|'));
					net.Broadcast(df, from);
				}
				break;
			case "BAN":
				if (p.Length >= 3)
				{
					BattleBan(id, Int(p[2], 0, 4));
				}
				break;
			case "PING":
				if (p.Length >= 3)
				{
					pl.ping = Int(p[2], 0, 9999);
				}
				from.Send("PONG|" + Int(p[1], int.MinValue, int.MaxValue));
				break;
			case "PROF":
				p[1] = id.ToString(IC);
				if (ApplyProf(p))
				{
					net.Broadcast(ProfLine(id));
				}
				break;
			case "BYE":
				from.id = -1;
				from.Close();
				RemovePlayer(id);
				break;
			case "TEAM":
				if (p.Length >= 3 && !matchLive && bat.phase == BAT_NONE)
				{
					pl.team = Int(p[2], 0, 2);
				}
				BroadcastRoster();
				break;
			case "SC":
				if (p.Length >= 6)
				{
					string line = "SC|" + id + "|" + Int(p[2], 0, Judgement.MAX_SCORE) + "|" + Int(p[3], 0, Judgement.MAX_COMBO) + "|" + Num(p[4], 0f, 100f).ToString("0.00", IC) + "|" + Num(p[5], 0f, 1f).ToString("0.000", IC) + "|" + ((p.Length > 6) ? Int(p[6], 0, Judgement.MAX_COMBO) : 0);
					ApplyScore(line.Split('|'));
					net.Broadcast(line, from);
				}
				break;
			case "FIN":
				if (p.Length >= 12)
				{
					string line2 = "FIN|" + id + "|" + Int(p[2], 0, Judgement.MAX_SCORE) + "|" + Num(p[3], 0f, 100f).ToString("0.00", IC) + "|" + ((p[4] == "1") ? 1 : 0) + "|" + ((p[5] == "1") ? 1 : 0) + "|" + string.Join("|", p.Skip(6).Take(6).Select((string x) => Int(x, 0, Judgement.MAX_COMBO)));
					ApplyFinal(line2.Split('|'));
					net.Broadcast(line2, from);
				}
				break;
			case "PH":
				if (p.Length >= 3)
				{
					pl.phase = Int(p[2], 0, PH_MAX);
				}
				BroadcastRoster();
				break;
			case "READY":
				if (p.Length >= 3)
				{
					pl.ready = p[2] == "1";
				}
				BroadcastRoster();
				break;
			case "ROOM":
				pl.inRoom = true;
				BroadcastRoster();
				if (matchLive && !released)
				{
					SetLoaded(-1);
				}
				break;
			}
		}

		private void Hello(Conn from, string[] p)
		{
			if (p.Length >= 3 && p[2] != VERSION)
			{
				from.Send("VERSION");
				from.Close();
				return;
			}
			if (p.Length < 4 || !roomCode.Contains("-") || p[3] != RoomCode.Clean(roomCode))
			{
				// 방 코드 없이(또는 틀린 코드로) 들어온 접속은 거절. 같은 IP 가 계속 틀리면 이 방이 열려 있는 동안 차단
				joinFails.TryGetValue(from.remoteIp, out var fails);
				joinFails[from.remoteIp] = fails + 1;
				from.Send("DENY");
				from.Close();
				return;
			}
			// 재접속: 같은 토큰이면 원래 자리(번호·점수·팀)를 그대로 돌려준다
			string token = (p.Length > 4) ? CleanCode(p[4].ToUpperInvariant()) : "";
			Player back = (token.Length < 8) ? null : players.FirstOrDefault((Player x) => x.id != 0 && x.token == token);
			if (kicked.Contains(token))
			{
				from.Send("KICK");
				from.Close();
				return;
			}
			if (back != null)
			{
				foreach (Conn old in net.clients)
				{
					if (old != from && old.id == back.id)
					{
						old.id = -1;
						old.Close();
					}
				}
				back.offline = false;
				from.id = back.id;
				from.Send("WELCOME|" + back.id + "|" + roomCode);
				BroadcastRoster();
				SendRoomState(from);
				AddChat("|" + back.name + " 님이 다시 접속했어요");
				return;
			}
			if (players.Count >= maxPlayers)
			{
				from.Send("FULL");
				from.Close();
				return;
			}
			Player pl = new Player
			{
				id = nextId++,
				name = CleanName(p[1]),
				token = token,
				team = FewestTeam(),
				// 버서스: 대전자 자리가 비어 있으면 대전자로
				fighter = players.Count((Player x) => x.fighter) < 2
			};
			players.Add(pl);
			from.id = pl.id;
			from.Send("WELCOME|" + pl.id + "|" + roomCode);
			BroadcastRoster();
			SendRoomState(from);
			AddChat("|" + pl.name + " 님이 들어왔어요");
			net.Broadcast("CHAT|-1|" + pl.name + " 님이 들어왔어요", from);
			PlaySe(AudioManager.SE.Confirm);
		}

		// 새로 들어온(또는 다시 접속한) 사람에게: 모두의 프로필·난이도, 방장 곡, 대전 상태
		private void SendRoomState(Conn to)
		{
			foreach (Player p in players)
			{
				to.Send(ProfLine(p.id));
				to.Send("DF|" + p.id + "|" + p.diffMode + "|" + p.diffKey + "|" + (p.plus ? 1 : 0) + "|" + p.speed10);
			}
			if (sentSel.Length > 0)
			{
				to.Send("SEL|" + sentSel);
			}
			if (bat.phase != BAT_NONE)
			{
				to.Send(BattleLine());
			}
		}

		private int FewestTeam()
		{
			int[] n = new int[3];
			foreach (Player p in players)
			{
				n[p.team]++;
			}
			return (n[1] < n[0]) ? 1 : 0;
		}

		private void BroadcastRoster()
		{
			if (net != null && isHost)
			{
				net.Broadcast(RosterLine());
			}
		}

		private string RosterLine()
		{
			string live = matchLive ? string.Join(",", matchIds) : "-";
			InitVsRange();
			return "ROSTER|" + maxPlayers + "|" + roomCode + "|" + matchType + ":" + vsMin + ":" + vsMax + ":" + (versusHard ? 1 : 0) + "|" + live + "|" + string.Join("|", players.Select((Player p) => p.id + "," + p.name + "," + (p.inRoom ? 1 : 0) + "," + (p.ready ? 1 : 0) + "," + p.phase + "," + p.team + "," + (p.offline ? 1 : 0) + "," + p.ping + "," + (p.fighter ? 1 : 0)));
		}

		private void RemovePlayer(int id)
		{
			Player pl = Find(id);
			if (pl == null)
			{
				return;
			}
			players.Remove(pl);
			NoticeLeft(pl.name);
			AddChat("|" + pl.name + " 님이 나갔어요");
			net?.Broadcast("CHAT|-1|" + pl.name + " 님이 나갔어요");
			// 버서스: 대전자가 나가면 관전자 중 먼저 들어온 사람을 대전자로
			if (pl.fighter && matchType == MT_VERSUS)
			{
				Player next = players.FirstOrDefault((Player x) => !x.fighter);
				if (next != null)
				{
					next.fighter = true;
				}
			}
			BroadcastRoster();
			if (matchLive && !released)
			{
				SetLoaded(-1);
			}
		}

		// 방장: 손님 연결이 갑자기 끊김 → 바로 내보내지 않고 자리를 잠시 비워둔다
		private void PlayerLost(int id)
		{
			Player pl = Find(id);
			if (pl == null)
			{
				return;
			}
			pl.offline = true;
			pl.offlineSince = Time.unscaledTime;
			BroadcastRoster();
			AddChat("|" + pl.name + " 님의 연결이 끊겼어요. 재접속을 기다리는 중...");
			if (matchLive && !released)
			{
				SetLoaded(-1);
			}
		}

		// ---------------- 손님: 방장 메시지 ----------------
		private void ClientHandle(Net.Msg m)
		{
			if (m.line == null)
			{
				if (m.from == net.server)
				{
					bool was = connected;
					// 갑자기 끊김(와이파이 등): 같은 코드로 자동 재접속을 시도한다
					if ((was || reconnecting) && online && !hostBye && !gaveUp)
					{
						BeginReconnect();
						return;
					}
					was |= gaveUp;
					gaveUp = false;
					string keep = status;
					Disconnect();
					if (online && scr != Scr.None)
					{
						SetScr(Scr.Lobby);
					}
					if (keep.Contains("코드가 맞지") || keep.Contains("가득") || keep.Contains("버전") || keep.Contains("다시 연결하지") || keep.Contains("내보냈"))
					{
						Status(keep, Ui.Red, 8f);
					}
					else
					{
						Status(was ? "방이 닫혔어요" : "접속이 끊겼어요", Ui.Red);
					}
				}
				return;
			}
			string[] p = m.line.Split('|');
			switch (p[0])
			{
			case "__CONNECTED":
				m.from.Send("HELLO|" + CleanName(myName) + "|" + VERSION + "|" + joinCode + "|" + myToken);
				break;
			case "__FAIL":
				if (reconnecting)
				{
					net?.Close();
					net = null;
					break;
				}
				Disconnect();
				Status("접속 실패: 방장이 방을 열었는지, 코드가 맞는지 확인하세요", Ui.Red, 8f);
				PlaySe(AudioManager.SE.error);
				break;
			case "BYE":
				hostBye = true;
				break;
			case "KICK":
				gaveUp = true;
				Status("방장이 방에서 내보냈어요", Ui.Red, 8f);
				PlaySe(AudioManager.SE.error);
				break;
			case "DENY":
				gaveUp = reconnecting;
				Status("방 코드가 맞지 않아요", Ui.Red, 8f);
				PlaySe(AudioManager.SE.error);
				break;
			case "VERSION":
				gaveUp = reconnecting;
				Status("방장과 온라인 모드 버전이 달라요", Ui.Red, 8f);
				PlaySe(AudioManager.SE.error);
				break;
			case "FULL":
				gaveUp = reconnecting;
				Status("방이 가득 찼어요", Ui.Red, 8f);
				PlaySe(AudioManager.SE.error);
				break;
			case "WELCOME":
				myId = Int(p[1], 1, int.MaxValue);
				roomCode = (p.Length > 2) ? CleanCode(p[2]) : RoomCode.Format(RoomCode.Clean(codeText));
				connected = true;
				connecting = false;
				status = "";
				pingWaitSince = -1f;
				m.from.Send(ProfLine(myId));
				if (reconnecting)
				{
					Rejoined();
				}
				else
				{
					OnJoinedRoom();
				}
				break;
			case "ROSTER":
				ApplyRoster(p);
				break;
			case "CHAT":
				if (p.Length >= 3)
				{
					int cid = Int(p[1], -1, int.MaxValue);
					AddChat(((cid < 0) ? "" : (Find(cid)?.name ?? "?")) + "|" + CleanText(p[2], 60));
				}
				break;
			case "SEL":
				if (p.Length >= 4)
				{
					hostSel = CleanRoot(p[1]) + "|" + Int(p[2], 0, (int)KEY.Count - 1) + "|" + Int(p[3], 0, (int)FREESTYLE_CATEGORY.COUNT - 1);
				}
				break;
			case "DF":
				ApplyDiff(p);
				break;
			case "LAD":
				ApplyBattle(p);
				break;
			case "PROF":
				ApplyProf(p);
				break;
			case "PONG":
				myPing = Mathf.Clamp((int)(Time.realtimeSinceStartup * 1000f) - Int(p[1], int.MinValue, int.MaxValue), 0, 9999);
				pingWaitSince = -1f;
				if (Me != null)
				{
					Me.ping = myPing;
				}
				break;
			case "PLAY":
				ReceivePlay(m.line);
				break;
			case "LD":
				totalCount = Int(p[2], 0, 8);
				loadedCount = Int(p[1], 0, totalCount);
				if (p.Length > 3)
				{
					HashSet<string> done = new HashSet<string>(p[3].Split(','));
					foreach (Player lp in players)
					{
						lp.loaded = done.Contains(lp.id.ToString(IC));
					}
				}
				break;
			case "GO":
				released = true;
				break;
			case "SC":
				ApplyScore(p);
				break;
			case "FIN":
				ApplyFinal(p);
				break;
			}
		}

		private void ApplyRoster(string[] p)
		{
			if (p.Length < 5)
			{
				return;
			}
			maxPlayers = Int(p[1], 2, 8);
			roomCode = CleanCode(p[2]);
			string[] mt = p[3].Split(':');
			matchType = Int(mt[0], 0, 3);
			if (mt.Length >= 4)
			{
				vsMin = Int(mt[1], 1, LV_MAX);
				vsMax = Int(mt[2], vsMin, LV_MAX);
				versusHard = mt[3] == "1";
			}
			bool wasLive = matchLive;
			matchLive = p[4] != "-";
			if (matchLive && !wasLive && !inMatch)
			{
				// 내가 안 들어간 판 (관전): 참가자 목록만 받아 둔다
				matchIds.Clear();
				foreach (string s in p[4].Split(','))
				{
					matchIds.Add(Int(s, 0, int.MaxValue));
				}
				ResetMatchScores();
			}
			List<Player> next = new List<Player>();
			for (int i = 5; i < p.Length && next.Count < 8; i++)
			{
				string[] e = p[i].Split(',');
				if (e.Length < 9)
				{
					continue;
				}
				int id = Int(e[0], 0, int.MaxValue);
				Player pl = Find(id) ?? new Player
				{
					id = id
				};
				pl.name = CleanName(e[1]);
				pl.inRoom = e[2] == "1";
				pl.ready = e[3] == "1";
				if (pl.id != myId)
				{
					pl.phase = Int(e[4], 0, PH_MAX);
					pl.ping = Int(e[7], 0, 9999);
				}
				else
				{
					pl.ping = myPing;
				}
				pl.team = Int(e[5], 0, 2);
				pl.offline = e[6] == "1";
				pl.fighter = e[8] == "1";
				next.Add(pl);
			}
			foreach (Player gone in players.Where((Player o) => o.id != myId && !next.Any((Player n) => n.id == o.id)))
			{
				NoticeLeft(gone.name);
			}
			players.Clear();
			players.AddRange(next);
		}

		// ---------------- 재접속 / 핑 ----------------
		private const float RECONNECT_TIME = 30f; // 손님이 다시 붙어보는 시간

		private const float RESERVE_TIME = 60f; // 방장이 끊긴 사람 자리를 비워두는 시간

		private const int HOST_IDLE_MS = 20000; // 이 시간 동안 아무것도 안 오면 끊긴 연결로 본다

		private const float CLIENT_IDLE = 15f;

		private bool reconnecting;

		private bool hostBye;

		private bool gaveUp;

		private float reconnectUntil;

		private float retryAt;

		private string myToken = "";

		private int myPing;

		private float pingWaitSince = -1f;

		private float pingTimer;

		// 중계 코드면 중계 서버로, 아니면 방장 PC 로 직접
		private void NetJoin(string code, string ip, int port)
		{
			string url = Relay.IsCode(code) ? Relay.Url() : null;
			if (url != null)
			{
				net.JoinRelay(url, RoomCode.Clean(code));
			}
			else
			{
				net.Join(ip, port);
			}
		}

		private void UpdateConnection()
		{
			float now = Time.realtimeSinceStartup;
			if (reconnecting && net == null && now >= retryAt)
			{
				string ip = null;
				int port = 0;
				if (now > reconnectUntil || (!Relay.IsCode(joinCode) && !RoomCode.Decode(joinCode, out ip, out port)))
				{
					GiveUpReconnect();
					return;
				}
				retryAt = now + 2f;
				net = new Net();
				NetJoin(joinCode, ip, port);
			}
			if (!connected || net == null)
			{
				return;
			}
			if ((pingTimer -= Time.unscaledDeltaTime) <= 0f)
			{
				pingTimer = 2f;
				if (isHost)
				{
					foreach (Conn c in net.clients.ToArray())
					{
						if (c.id >= 0 && Environment.TickCount - c.lastRecv > HOST_IDLE_MS)
						{
							c.Close();
						}
					}
					foreach (Player p in players.Where((Player p) => p.offline && Time.unscaledTime - p.offlineSince > RESERVE_TIME).ToList())
					{
						RemovePlayer(p.id);
					}
					if (net.clients.Count > 0)
					{
						BroadcastRoster();
					}
					PublishRoom();
				}
				else
				{
					if (pingWaitSince < 0f)
					{
						pingWaitSince = now;
					}
					net.server?.Send("PING|" + (int)(now * 1000f) + "|" + myPing);
				}
			}
			// 응답이 계속 없으면(공유기 끊김 등 TCP 가 모르는 끊김) 끊긴 것으로 보고 재접속
			if (!isHost && pingWaitSince >= 0f && now - pingWaitSince > CLIENT_IDLE)
			{
				pingWaitSince = -1f;
				net.server?.Close();
			}
		}

		private void BeginReconnect()
		{
			float now = Time.realtimeSinceStartup;
			if (!reconnecting)
			{
				reconnecting = true;
				reconnectUntil = now + RECONNECT_TIME;
				Status("연결이 끊겨 다시 접속하는 중...", Ui.Gold, RECONNECT_TIME + 10f);
				PlaySe(AudioManager.SE.error);
			}
			net?.Close();
			net = null;
			connected = false;
			pingWaitSince = -1f;
			retryAt = now + 1.5f;
		}

		private void GiveUpReconnect()
		{
			Disconnect();
			if (online && scr != Scr.None)
			{
				SetScr(Scr.Lobby);
			}
			Status("방과 다시 연결하지 못했어요", Ui.Red, 8f);
		}

		// 다시 붙었을 때: 끊긴 동안 못 보낸 내 상태를 다시 알린다
		private void Rejoined()
		{
			reconnecting = false;
			Status("다시 연결됐어요", Ui.Green);
			sentPhase = -1;
			Player me = Me;
			Conn s = net.server;
			if (me == null || s == null)
			{
				return;
			}
			if (me.inRoom)
			{
				s.Send("ROOM|" + myId);
			}
			if (me.ready)
			{
				s.Send("READY|" + myId + "|1");
			}
			s.Send("TEAM|" + myId + "|" + me.team);
			sentDiff = "";
			if (inMatch && me.loaded && !released)
			{
				s.Send("LOADED|" + myId);
			}
			if (lastFin != null)
			{
				s.Send(lastFin);
			}
		}

		// ---------------- 프로필 ----------------
		// 게임 오른쪽 위 프로필(아이콘, 레벨, [칭호], 숙련도)과 같은 정보 + 업적 수 + 래더 티어.
		// 아이콘/플레이트는 아이템 ID 만 보내고, 그림은 각자 게임 안의 같은 아이템에서 꺼낸다.
		private static void SetMyProfile(Player me)
		{
			try
			{
				me.level = BundlePackage.user.UserLevel;
				me.mastery = (int)BundlePackage.user.Mastery;
				me.clears = BundlePackage.user.GetClearCount(isPlusMode: false);
				me.tag = CleanText(BundlePackage.closet.userTag, 16);
				ArchiveItem[] arch = BundlePackage.itemBundle.GetArchiveItems();
				me.ach = arch.Count((ArchiveItem a) => a.record != null && a.record.IsClear);
				me.achTotal = arch.Length;
				me.tier = MyRating;
				me.lp = 0;
				me.icon = CleanId(BundlePackage.closet.GetEquipID(Item.TYPE.ICON));
				me.plate = CleanId(BundlePackage.closet.GetEquipID(Item.TYPE.PLATE));
			}
			catch (Exception e)
			{
				Debug.LogWarning("[WJMAXOnline] profile read failed: " + e.Message);
			}
		}

		private string ProfLine(int id)
		{
			Player p = Find(id);
			if (p == null)
			{
				p = new Player
				{
					id = id
				};
				SetMyProfile(p);
			}
			else if (id == myId)
			{
				SetMyProfile(p);
			}
			return "PROF|" + id + "|" + p.level + "|" + p.mastery + "|" + p.clears + "|" + p.icon + "|" + p.plate + "|" + p.tag + "|" + p.ach + "|" + p.achTotal + "|" + p.tier + "|" + p.lp;
		}

		private bool ApplyProf(string[] p)
		{
			Player pl = (p.Length >= 12) ? Find(Int(p[1], -1, int.MaxValue)) : null;
			if (pl == null)
			{
				return false;
			}
			pl.level = Int(p[2], 0, 9999);
			pl.mastery = Int(p[3], 0, 4);
			pl.clears = Int(p[4], 0, 99999);
			pl.icon = CleanId(p[5]);
			pl.plate = CleanId(p[6]);
			pl.tag = CleanText(p[7], 16);
			pl.achTotal = Int(p[9], 0, 9999);
			pl.ach = Int(p[8], 0, pl.achTotal);
			pl.tier = Int(p[10], 1, RATING_MAX);
			pl.lp = 0;
			return true;
		}

		// 아이템 그림 (없는 ID 면 null)
		private static Sprite IconSprite(string id)
		{
			if (string.IsNullOrEmpty(id))
			{
				return null;
			}
			try
			{
				return BundlePackage.itemBundle.GetItem<ItemIcon>(id)?.ItemSprite;
			}
			catch (Exception)
			{
				return null;
			}
		}

		private static Sprite PlateSprite(string id)
		{
			if (string.IsNullOrEmpty(id))
			{
				return null;
			}
			try
			{
				return BundlePackage.itemBundle.GetItem<ItemPlate>(id)?.ItemSprite;
			}
			catch (Exception)
			{
				return null;
			}
		}
	}
}
