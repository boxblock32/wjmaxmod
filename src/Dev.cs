#if DEV
// 개발용: OnlineMod\dev\cmd.txt 에 명령을 쓰면 실행 (배포 빌드에는 포함되지 않음)
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Linq;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace WJMAXOnline
{
	public partial class OnlineMod
	{
		private static string DevDir => Path.Combine(Path.GetDirectoryName(Application.dataPath), "OnlineMod", "dev");

		private float devPoll;

		private static readonly HashSet<KeyCode> fakeKeys = new HashSet<KeyCode>();

		private static int fakeKeyFrame;

		// auto 명령: 곡마다 처음부터 오토 플레이 (결과 화면 확인용)
		private static bool devAuto;

		private void DevUpdate()
		{
			Application.runInBackground = true;
			if (devAuto && Singleton<Judgement>.Instance != null)
			{
				typeof(Judgement).GetField("isAutoPlay", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)?.SetValue(Singleton<Judgement>.Instance, true);
			}
			if (Time.frameCount > fakeKeyFrame)
			{
				fakeKeys.Clear();
			}
			if ((devPoll -= Time.unscaledDeltaTime) > 0f)
			{
				return;
			}
			devPoll = 0.3f;
			string file = Path.Combine(DevDir, "cmd.txt");
			if (!File.Exists(file))
			{
				return;
			}
			string[] lines = File.ReadAllLines(file);
			File.Delete(file);
			foreach (string line in lines)
			{
				try
				{
					DevCommand(line.Trim());
				}
				catch (Exception e)
				{
					File.AppendAllText(Path.Combine(DevDir, "log.txt"), line + " => " + e + "\n");
				}
			}
		}

		private static bool DevKey(KeyCode k)
		{
			return fakeKeys.Contains(k);
		}

		private void DevCommand(string line)
		{
			if (line.Length == 0)
			{
				return;
			}
			string[] p = line.Split(new char[1] { ' ' }, 2);
			string arg = (p.Length > 1) ? p[1] : "";
			switch (p[0])
			{
			case "shot":
				ScreenCapture.CaptureScreenshot(Path.Combine(DevDir, arg + ".png"));
				break;
			case "scene":
				if (arg == "FreeStyle")
				{
					BundlePackage.closet.load();
				}
				SceneLoader.LoadScene((SCENE)Enum.Parse(typeof(SCENE), arg));
				break;
			case "key":
				fakeKeys.Add((KeyCode)Enum.Parse(typeof(KeyCode), arg));
				fakeKeyFrame = Time.frameCount;
				break;
			case "dump":
				DevDump(arg);
				break;
			case "assets":
				DevAssets();
				break;
			default:
				DevExtra(p[0], arg);
				break;
			}
		}

		private static string devTyped = "";

		private static string DevTyped()
		{
			string s = devTyped;
			devTyped = "";
			return s;
		}

		private readonly List<Conn> bots = new List<Conn>();

		private readonly Dictionary<string, Conn> botConns = new Dictionary<string, Conn>();

		private int botTeamFlip;

		private static int botTicks = 150; // 봇 플레이 길이 (0.2초 단위)

		private bool devRank;

		private RectTransform devSprites;


		private void DevExtra(string cmd, string arg)
		{
			switch (cmd)
			{
			case "type":
				devTyped += arg.Replace("\\b", "\b");
				break;
			case "online":
				EnterOnline();
				break;
			case "pv":
			{
				UnityEngine.Video.VideoPlayer v = (lister == null) ? null : (fPreviewVideo.GetValue(lister) as UnityEngine.Video.VideoPlayer);
				string vs = (v == null) ? "-" : (v.url + " playing=" + v.isPlaying + " prepared=" + v.isPrepared + " frame=" + v.frame + " active=" + v.gameObject.activeInHierarchy + " enabled=" + v.enabled + " mode=" + v.renderMode + " rt=" + (v.targetTexture ? v.targetTexture.name : "-") + " go=" + v.gameObject.name);
				AudioSource pa = (lister == null) ? null : (fPreviewAudio.GetValue(lister) as AudioSource);
				string av = (pa == null) ? "-" : ("playing=" + pa.isPlaying + " time=" + pa.time.ToString("0.0") + " vol=" + pa.volume + " mute=" + pa.mute + " clip=" + (pa.clip ? pa.clip.name : "-") + " enabled=" + pa.enabled);
				StringBuilder sb = new StringBuilder("pv=" + vs + "\npa=" + av + "\nlisterMuted=" + listerMuted + " previewRoot=" + previewRoot + " previewAt=" + previewAt + "\ncur=" + GameValue.currentBundle?.RootFileName + "\n");
				foreach (RawImage ri in Resources.FindObjectsOfTypeAll<RawImage>())
				{
					sb.AppendLine("raw " + ri.name + " tex=" + (ri.texture ? ri.texture.name : "-") + " active=" + ri.gameObject.activeInHierarchy);
				}
				foreach (Image im in FindObjectsByType<Image>(FindObjectsSortMode.None))
				{
					if (im.rectTransform.rect.width >= 1000f && im.sprite != null)
					{
						sb.AppendLine("img " + im.transform.parent?.name + "/" + im.name + " sp=" + im.sprite.name + " tex=" + im.sprite.texture?.name + " a=" + im.color.a + " w=" + im.rectTransform.rect.width);
					}
				}
				File.WriteAllText(Path.Combine(DevDir, "pv.txt"), sb.ToString());
			}
				break;
			case "auto":
				devAuto = arg != "0";
				break;
			case "burst":
			{
				// 연출 확인용 연속 캡처: burst 이름 장수 간격초
				string[] a = arg.Split(' ');
				StartCoroutine(Burst(a[0], int.Parse(a[1]), float.Parse(a[2], IC)));
				break;
			}
			case "bot":
			{
				int n = (arg.Length > 0) ? int.Parse(arg) : 1;
				for (int i = 0; i < n; i++)
				{
					SpawnBot("봇" + (bots.Count + 1));
				}
				break;
			}
			case "botdrop":
				// 봇 하나를 갑자기 끊기 (재접속 테스트): botdrop 봇1
				if (botConns.TryGetValue(arg, out var dc))
				{
					dc.Close();
				}
				break;
			case "botback":
				SpawnBot(arg);
				break;
			case "botchat":
			{
				// botchat 봇1 안녕
				string[] a = arg.Split(new char[1] { ' ' }, 2);
				if (botConns.TryGetValue(a[0], out var cc))
				{
					cc.Send("CHAT|0|" + ((a.Length > 1) ? a[1] : "안녕"));
				}
				break;
			}
			case "botteam":
				if (botConns.TryGetValue(arg, out var tc))
				{
					tc.Send("TEAM|0|" + (botTeamFlip = (botTeamFlip + 1) % 3));
				}
				break;
			case "host":
			{
				// host [방식 0~3] [최대]
				string[] a = arg.Split(' ');
				StartHost((a.Length > 0 && a[0].Length > 0) ? int.Parse(a[0]) : 0, (a.Length > 1) ? int.Parse(a[1]) : 8);
				break;
			}
			case "join":
				codeText = arg;
				StartJoin();
				break;
			case "mt":
				matchType = int.Parse(arg);
				BroadcastRoster();
				break;
			case "rankdemo":
				devRank = !devRank;
				hudSig = null;
				break;
			case "levels":
			{
				// 난이도별 레벨 분포 (AI 실력 맞추기용)
				StringBuilder sb = new StringBuilder();
				foreach (KEY k in new KEY[2] { KEY.Key4, KEY.Key6 })
				{
					List<SheetPack> all = new List<SheetPack>();
					foreach (BaseMusicBundle b in BundlePackage.musicBundle.GetMusicBundles(FREESTYLE_CATEGORY.ALL, k).Where((BaseMusicBundle b) => !b.IsRandom))
					{
						all.AddRange(b.GetSheetPacksWithinLevel(k, new LevelRange(0f, 99f)));
					}
					foreach (IGrouping<MODE, SheetPack> g in all.GroupBy((SheetPack x) => x.mode).OrderBy((IGrouping<MODE, SheetPack> g) => g.Key))
					{
						sb.AppendLine(k + " " + g.Key + " n=" + g.Count() + " levels: " + string.Join(" ", g.GroupBy((SheetPack x) => Mathf.FloorToInt(x.level)).OrderBy((IGrouping<int, SheetPack> x) => x.Key).Select((IGrouping<int, SheetPack> x) => x.Key + ":" + x.Count())));
					}
				}
				File.WriteAllText(Path.Combine(DevDir, "levels.txt"), sb.ToString());
				break;
			}
			case "aitest":
			{
				// AI 실력 확인: 점수별로 레벨별 평균 정확도·풀콤 확률·클리어(70%) 확률 (판 컨디션 포함 5000번)
				System.Random r = new System.Random(1);
				StringBuilder sb = new StringBuilder();
				foreach (int rating in new int[8] { 150, 450, 750, 1050, 1350, 1750, 2000, 2400 })
				{
					sb.Append(rating.ToString().PadLeft(5) + " " + TierText(rating).PadRight(14) + " skill=" + SkillOf(rating).ToString("0.0") + " |");
					foreach (int lv in new int[9] { 5, 8, 11, 14, 17, 20, 22, 24, 26 })
					{
						float sum = 0f;
						int fc = 0;
						int clear = 0;
						for (int i = 0; i < 5000; i++)
						{
							SimAi(r, rating, lv, hard: false, Gauss(r, 0.45f), out float rate, out int brk, out _);
							sum += rate;
							fc += (brk == 0) ? 1 : 0;
							clear += (rate >= 70f) ? 1 : 0;
						}
						sb.Append($" L{lv}:{sum / 5000f:0.0}%/FC{fc / 50}%");
					}
					sb.AppendLine();
				}
				sb.AppendLine("delta win/lose vs same=" + RatingDelta(true, 1000, 1000) + "/" + RatingDelta(false, 1000, 1000) + " vs +200=" + RatingDelta(true, 1000, 1200) + "/" + RatingDelta(false, 1000, 1200) + " vs -200=" + RatingDelta(true, 1000, 800) + "/" + RatingDelta(false, 1000, 800));
				foreach (int t in new int[14] { 1, 99, 100, 299, 300, 899, 1200, 1259, 1260, 1499, 1500, 1999, 2000, 3500 })
				{
					sb.Append(t + "=" + TierText(t) + "  ");
				}
				File.WriteAllText(Path.Combine(DevDir, "aitest.txt"), sb.ToString());
				break;
			}
			case "sprites":
			{
				// 게임 그림 보기: sprites 이름 이름 ... (빈칸이면 지우기)
				if (devSprites != null)
				{
					Destroy(devSprites.gameObject);
					devSprites = null;
				}
				if (arg.Length == 0 || canvas == null)
				{
					break;
				}
				devSprites = Ui.Fill(canvas.transform, "devSprites");
				Ui.FillImg(devSprites, "bg", new Color(0.3f, 0.3f, 0.35f, 1f));
				string[] names = arg.Split(' ');
				for (int i = 0; i < names.Length; i++)
				{
					Vector2 pos = new Vector2(40 + i % 6 * 310, 40 + i / 6 * 340);
					Image im = Ui.Img(devSprites, "s" + i, pos, new Vector2(280f, 280f), Color.white, names[i]);
					im.preserveAspect = true;
					Ui.Txt(devSprites, "t" + i, pos + new Vector2(0f, 284f), new Vector2(300f, 30f), names[i] + ((Ui.Sp(names[i]) == null) ? " (없음)" : ""), 16f, Color.white);
				}
				break;
			}
			case "rate":
			{
				string[] r = arg.Split(' ');
				MyRating = int.Parse(r[0]);
				Wins = int.Parse(r[1]);
				Losses = int.Parse(r[2]);
				PlayerPrefs.Save();
				break;
			}
			case "fin":
			{
				// 곡 진행 중: 내 점수를 정해서 바로 끝내고 방으로 (fin 990000)
				int sc = int.Parse(arg);
				float rt = sc / 10000f;
				sentFinal = true;
				lastFin = "FIN|" + myId + "|" + sc + "|" + rt.ToString("0.00", IC) + "|1|0|500|10|0|0|1|400";
				ApplyFinal(lastFin.Split('|'));
				Send(lastFin);
				leaving = true;
				Singleton<Fader>.Instance.FadeOut(delegate
				{
					SceneLoader.LoadScene(SCENE.FreeStyle);
				});
				break;
			}
			case "botlen":
				botTicks = int.Parse(arg);
				break;
			case "botready":
				foreach (KeyValuePair<string, Conn> kv in botConns)
				{
					Player bp = players.FirstOrDefault((Player x) => x.name == kv.Key);
					if (bp != null)
					{
						kv.Value.Send("READY|" + bp.id + "|1");
					}
				}
				break;
			case "vsgo":
			{
				// 버서스 바로 시작: 나 + 첫 봇을 대전자로, 봇 준비, 시작 (키 입력 없이)
				for (int i = 0; i < players.Count; i++)
				{
					players[i].fighter = i < 2;
					players[i].ready = i < 2;
				}
				BroadcastRoster();
				string why = StartBlockReason();
				if (why == null)
				{
					BattleBegin();
				}
				else
				{
					File.AppendAllText(Path.Combine(DevDir, "log.txt"), "vsgo: " + why + "\n");
				}
				break;
			}
			case "botleave":
				foreach (Conn b in bots)
				{
					b.Close();
				}
				bots.Clear();
				break;
			case "glyphs":
			{
				StringBuilder sb = new StringBuilder();
				foreach (TMP_FontAsset f in Resources.FindObjectsOfTypeAll<TMP_FontAsset>())
				{
					sb.AppendLine(f.name + " mode=" + f.atlasPopulationMode + " fallbacks=" + (f.fallbackFontAssetTable?.Count ?? 0) + " " + string.Join(" ", new string[6] { "ONLINE", "0123", "abc", "←→↑↓", "◀▶", "●·" }.Select((string s) => s + "=" + f.HasCharacters(s))));
				}
				File.WriteAllText(Path.Combine(DevDir, "glyphs.txt"), sb.ToString());
				break;
			}
			case "fakehost":
				StartFakeHost();
				break;
			case "fhplay":
			{
				BaseMusicBundle b = GameValue.currentBundle ?? BundlePackage.musicBundle.GetMusicBundles(FREESTYLE_CATEGORY.ALL, KEY.Key4)[0];
				fakeHostConn?.Send("PLAY|" + b.RootFileName + "|" + (int)GameValue.key + "|0|-1|-1|0,1");
				break;
			}
			case "fhclose":
				fakeHostConn?.Close();
				fakeHostListener?.Stop();
				break;
			case "attack":
				DevAttack();
				break;
			case "selftest":
				DevSelfTest();
				break;
			case "state":
				File.WriteAllText(Path.Combine(DevDir, "state.txt"), $"scene={SceneLoader.Current} auto={(Singleton<Judgement>.Instance == null ? "-" : typeof(Judgement).GetField("isAutoPlay", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)?.GetValue(Singleton<Judgement>.Instance))}/{fPlayMode.GetValue(BundlePackage.closet)} rating={MyRating} applied={batApplied}/{bat.no} ai={aiMatch} online={online} scr={scr} popup={popup} host={isHost} conn={connected} type={matchType} players={players.Count} code={roomCode} inMatch={inMatch} live={matchLive} ids={string.Join(",", matchIds)} released={released} bat={bat.phase}/{bat.round} {bat.pts[0]}:{bat.pts[1]} win={bat.winner} cur={cursor}/{list.Length} cat={listCat} key={GameValue.key} hostSel={hostSel} status={status} windows={string.Join(",", CInput.windowList.Select((GameObject w) => w ? w.name : "null"))} lister={(lister != null ? fIsInput.GetValue(lister) + "/" + fIsPlayGame.GetValue(lister) : "-")} focus={focused} t={Time.time} fading={Fader.isFading} chat={chatLog.Count} players=[{string.Join(";", players.Select((Player x) => x.id + ":" + x.name + ":" + x.diffMode + ":" + x.ready + ":" + x.phase + ":" + x.score + ":" + x.done))}] pingT={pingTimer} conns=[{(net == null ? "" : string.Join(";", net.clients.Select((Conn c) => c.id + ":" + c.cid + ":" + (Environment.TickCount - c.lastRecv) + ":" + c.closed)))}]");
				break;
			}
		}

		private System.Collections.IEnumerator Burst(string name, int n, float step)
		{
			for (int i = 0; i < n; i++)
			{
				ScreenCapture.CaptureScreenshot(Path.Combine(DevDir, name + "_" + i + ".png"));
				yield return new WaitForSecondsRealtime(step);
			}
		}

		// 같은 PC 안에서 접속하는 가짜 플레이어: 로딩 후 점수를 올리다가 끝낸다
		private void SpawnBot(string name)
		{
			int.TryParse(portText, out var port);
			System.Net.Sockets.TcpClient t = new System.Net.Sockets.TcpClient("127.0.0.1", port);
			Conn c = new Conn(t);
			bots.Add(c);
			System.Random rnd = new System.Random(name.GetHashCode());
			// 내 프로필을 빌려 레벨만 바꿔서 보냄 (아이콘/플레이트 그림 확인용)
			string[] mp = ProfLine(myId).Split('|');
			mp[2] = (5 + rnd.Next(60)).ToString();
			mp[4] = rnd.Next(10, 300).ToString();
			string botProf = string.Join("|", mp);
			int id = -1;
			bool playing = false;
			c.Start(delegate(Conn conn, string line)
			{
				if (line == null)
				{
					return;
				}
				string[] p = line.Split('|');
				if (p[0] == "WELCOME")
				{
					id = int.Parse(p[1]);
					conn.Send("READY|" + id + "|1");
					conn.Send(botProf);
					conn.Send("DF|" + id + "|" + rnd.Next(4) + "|0|" + rnd.Next(2));
					// 실제 손님처럼 2초마다 핑 (안 보내면 방장이 끊긴 연결로 정리함)
					new System.Threading.Thread((System.Threading.ThreadStart)delegate
					{
						while (!conn.closed)
						{
							conn.Send("PING|0|" + (20 + rnd.Next(120)));
							System.Threading.Thread.Sleep(2000);
						}
					}) { IsBackground = true }.Start();
				}
				else if (p[0] == "LAD" && p.Length > 11 && p[1] == "1" && (p[10] == id.ToString() || p[11] == id.ToString()))
				{
					// 곡 밴: 3초 뒤 아무 곡이나
					new System.Threading.Thread((System.Threading.ThreadStart)delegate
					{
						System.Threading.Thread.Sleep(3000);
						conn.Send("BAN|" + id + "|" + rnd.Next(5));
					}).Start();
				}
				else if (p[0] == "PLAY" && p.Length > 6 && p[6].Split(',').Contains(id.ToString()))
				{
					playing = true;
					conn.Send("PH|" + id + "|1");
					new System.Threading.Thread((System.Threading.ThreadStart)delegate
					{
						System.Threading.Thread.Sleep(4000 + rnd.Next(3000));
						conn.Send("LOADED|" + id);
					}).Start();
				}
				else if (p[0] == "GO" && playing)
				{
					playing = false;
					// 실제 사람처럼: 플레이 30초 → 게임 결과 5초 → 방 복귀 후 준비
					new System.Threading.Thread((System.Threading.ThreadStart)delegate
					{
						conn.Send("PH|" + id + "|2");
						int score = 0;
						int combo = 0;
						float hp = 1f;
						for (int i = 0; i < botTicks && !conn.closed; i++)
						{
							System.Threading.Thread.Sleep(200);
							score += rnd.Next(800, 2000);
							combo = (rnd.Next(30) == 0) ? 0 : (combo + 2);
							hp = Mathf.Clamp01(hp + ((combo == 0) ? (-0.15f) : 0.02f));
							conn.Send($"SC|{id}|{score}|{combo}|{98f - rnd.Next(300) / 100f:0.00}|{hp:0.000}|{i / 40}");
						}
						conn.Send($"FIN|{id}|{score}|97.50|1|0|{rnd.Next(800, 1200)}|{rnd.Next(10, 80)}|{rnd.Next(0, 10)}|{rnd.Next(0, 5)}|{rnd.Next(0, 8)}|{rnd.Next(300, 1000)}");
						System.Threading.Thread.Sleep(2000);
						conn.Send("PH|" + id + "|3");
						System.Threading.Thread.Sleep(5000);
						conn.Send("ROOM|" + id);
						conn.Send("PH|" + id + "|0");
						conn.Send("READY|" + id + "|1");
					}).Start();
				}
			});
			c.Send("HELLO|" + name + "|" + VERSION + "|" + RoomCode.Clean(roomCode) + "|BOT" + name.GetHashCode().ToString("X8") + "TOKEN");
			botConns[name] = c;
		}

		// 손님 쪽 흐름 테스트용 가짜 방장 (127.0.0.1:7799 → 코드는 dev\fakecode.txt)
		private Conn fakeHostConn;

		private System.Net.Sockets.TcpListener fakeHostListener;

		private void StartFakeHost()
		{
			fakeHostListener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 7799);
			fakeHostListener.Start();
			string code = RoomCode.Encode("127.0.0.1", 7799);
			BaseMusicBundle sb = BundlePackage.musicBundle.GetMusicBundles(FREESTYLE_CATEGORY.ALL, KEY.Key4)[3];
			string selLine = "SEL|" + sb.RootFileName + "|0|1";
			File.WriteAllText(Path.Combine(DevDir, "fakecode.txt"), code);
			System.Net.Sockets.TcpListener l = fakeHostListener;
			new System.Threading.Thread((System.Threading.ThreadStart)delegate
			{
				Conn c = new Conn(l.AcceptTcpClient());
				fakeHostConn = c;
				string guest = "?";
				c.Start(delegate(Conn conn, string line)
				{
					if (line == null)
					{
						return;
					}
					string[] p = line.Split('|');
					switch (p[0])
					{
					case "HELLO":
						guest = p[1];
						conn.Send("WELCOME|1|" + code);
						conn.Send("ROSTER|8|" + code + "|0|-|0,가짜방장,1,0,0,0,0,10,1|1," + guest + ",1,0,0,1,0,0,1");
						conn.Send(selLine);
						break;
					case "LOADED":
						conn.Send("LD|2|2");
						conn.Send("GO");
						new System.Threading.Thread((System.Threading.ThreadStart)delegate
						{
							int score = 0;
							for (int i = 0; i < 100 && !conn.closed; i++)
							{
								System.Threading.Thread.Sleep(200);
								score += 3000;
								conn.Send($"SC|0|{score}|{i}|96.00|0.900|0");
							}
							conn.Send($"FIN|0|{score}|96.00|1|0|900|20|3|1|2|500");
						}).Start();
						break;
					case "ROOM":
						conn.Send("ROSTER|8|" + code + "|0|-|0,가짜방장,1,0,0,0,0,10,1|1," + guest + ",1,0,0,1,0,0,1");
						break;
					case "PING":
						conn.Send("PONG|" + p[1]);
						break;
					}
				});
			}).Start();
		}

		private void DevSelfTest()
		{
			string code = RoomCode.Encode("220.83.110.63", 7777);
			bool ok = code.Length == 11 && RoomCode.Decode(code, out var ip, out var port) && ip == "220.83.110.63" && port == 7777;
			ok &= RoomCode.Decode(code.ToLower().Replace("-", " "), out ip, out port) && port == 7777;
			ok &= !RoomCode.Decode("ABC", out ip, out port);
			
			ok &= Reach.IsPrivate("192.168.0.2") && Reach.IsPrivate("26.1.2.3") && !Reach.IsPrivate("220.83.110.63");
			ok &= CleanName("<size=500>해커|,;") == "size=500해커" && CleanName("") == "플레이어";
			ok &= Int("NaN", 0, 10) == 0 && Int("99999999", 0, 10) == 10 && Num("NaN", 0f, 1f) == 0f && Num("Infinity", 0f, 1f) == 0f && Num("-5", 0f, 1f) == 0f;
			File.WriteAllText(Path.Combine(DevDir, "selftest.txt"), (ok ? "PASS " : "FAIL ") + code);
		}

		private void DevDump(string filter)
		{
			StringBuilder sb = new StringBuilder();
			sb.AppendLine("scene=" + SceneLoader.Current + " screen=" + Screen.width + "x" + Screen.height);
			List<GameObject> roots = new List<GameObject>();
			for (int i = 0; i < SceneManager.sceneCount; i++)
			{
				roots.AddRange(SceneManager.GetSceneAt(i).GetRootGameObjects());
			}
			roots.AddRange(base.gameObject.scene.GetRootGameObjects());
			foreach (GameObject r in roots.Distinct())
			{
				if (filter.Length == 0 || r.name.Contains(filter))
				{
					Dump(r.transform, sb, 0);
				}
			}
			File.WriteAllText(Path.Combine(DevDir, "dump.txt"), sb.ToString());
		}

		private static void Dump(Transform t, StringBuilder sb, int depth)
		{
			sb.Append(' ', depth * 2).Append(t.name);
			if (!t.gameObject.activeSelf)
			{
				sb.Append(" (off)");
			}
			if (t is RectTransform rt)
			{
				sb.Append($" {rt.rect.width:0}x{rt.rect.height:0}@{rt.anchoredPosition.x:0},{rt.anchoredPosition.y:0} a=({rt.anchorMin.x:0.##},{rt.anchorMin.y:0.##})-({rt.anchorMax.x:0.##},{rt.anchorMax.y:0.##}) pv=({rt.pivot.x:0.##},{rt.pivot.y:0.##}) sd={rt.sizeDelta.x:0},{rt.sizeDelta.y:0}");
			}
			foreach (Component c in t.GetComponents<Component>())
			{
				if (c == null || c is Transform)
				{
					continue;
				}
				sb.Append(" [").Append(c.GetType().Name);
				if (c is Image img)
				{
					sb.Append($" sp={(img.sprite ? img.sprite.name : "-")} col=#{ColorUtility.ToHtmlStringRGBA(img.color)}");
				}
				else if (c is TMP_Text tx)
				{
					string s = tx.text ?? "";
					sb.Append($" font={(tx.font ? tx.font.name : "-")} sz={tx.fontSize} '{s.Replace("\n", "\\n").Substring(0, Math.Min(40, s.Length))}'");
				}
				sb.Append(']');
			}
			sb.AppendLine();
			foreach (Transform ch in t)
			{
				Dump(ch, sb, depth + 1);
			}
		}

		private void DevAssets()
		{
			StringBuilder sb = new StringBuilder();
			foreach (TMP_FontAsset f in Resources.FindObjectsOfTypeAll<TMP_FontAsset>())
			{
				sb.AppendLine("font " + f.name + " mode=" + f.atlasPopulationMode);
			}
			foreach (Sprite s in from s in Resources.FindObjectsOfTypeAll<Sprite>() orderby s.name select s)
			{
				sb.AppendLine($"sprite {s.name} {s.rect.width:0}x{s.rect.height:0} border={s.border}");
			}
			File.WriteAllText(Path.Combine(DevDir, "assets.txt"), sb.ToString());
		}

		// 보안 테스트: 비밀번호 틀린 접속 / 남의 번호로 점수 위조 / 아주 긴 줄
		private void DevAttack()
		{
			int.TryParse(portText, out var port);
			new System.Threading.Thread((System.Threading.ThreadStart)delegate
			{
				StringBuilder log = new StringBuilder();
				string Try(string[] lines)
				{
					try
					{
						using System.Net.Sockets.TcpClient t = new System.Net.Sockets.TcpClient("127.0.0.1", port);
						t.ReceiveTimeout = 3000;
						System.Net.Sockets.NetworkStream st = t.GetStream();
						byte[] b = Encoding.UTF8.GetBytes(string.Join("\n", lines) + "\n");
						st.Write(b, 0, b.Length);
						StreamReader r = new StreamReader(st);
						StringBuilder got = new StringBuilder();
						string l;
						while ((l = r.ReadLine()) != null)
						{
							got.Append(l.Substring(0, Math.Min(40, l.Length))).Append(" / ");
						}
						return "closed after: " + got;
					}
					catch (Exception e)
					{
						return "ex: " + e.GetType().Name;
					}
				}
				log.AppendLine("no code: " + Try(new string[1] { "HELLO|침입자|" + VERSION }));
				log.AppendLine("wrong code: " + Try(new string[1] { "HELLO|침입자|" + VERSION + "|0000000000" }));
				log.AppendLine("no hello: " + Try(new string[1] { "SC|0|999999|1|100|1" }));
				log.AppendLine("long line: " + Try(new string[1] { new string('A', 100000) }));
				File.WriteAllText(Path.Combine(DevDir, "attack.txt"), log.ToString());
			}).Start();
		}	}
}
#endif
