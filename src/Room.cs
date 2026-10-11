// 방 (디맥 오픈 매치 방 구성): 왼쪽 = 곡 정보 + 채팅, 가운데 = 곡 목록, 오른쪽 = PLAYERS + START.
// 곡은 방장이 고르고(손님 화면의 목록은 방장 곡을 따라감) 난이도·플러스 모드·게임 설정·옷장은 각자 고른다.
// 방장 곡이 내 게임에 없으면 관전(이번 곡은 쉬고 대기실에서 실시간 점수를 봄).
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.Video;

namespace WJMAXOnline
{
	public partial class OnlineMod
	{
		// ---------------- 채팅 ----------------
		private bool chatting;

		private string chatText = "";

		private readonly List<string> chatLog = new List<string>(); // "이름|글" (이름이 비면 알림)

		private void AddChat(string line)
		{
			chatLog.Add(line);
			if (chatLog.Count > 50)
			{
				chatLog.RemoveAt(0);
			}
		}

		private void SendChat(string text)
		{
			text = CleanText(text, 60);
			if (text.Length == 0)
			{
				return;
			}
			if (isHost)
			{
				AddChat(myName + "|" + text);
				net?.Broadcast("CHAT|" + myId + "|" + text);
			}
			else
			{
				net?.server?.Send("CHAT|" + myId + "|" + text);
			}
		}

		// ---------------- 곡 목록 ----------------
		private FREESTYLE_CATEGORY listCat = FREESTYLE_CATEGORY.ALL;

		private BaseMusicBundle[] list = new BaseMusicBundle[0];

		private int cursor;

		private string hostSel = ""; // 손님: 방장이 고른 곡 "파일명|키|카테고리"

		private string sentSel = ""; // 방장: 마지막으로 알린 곡

		private string mirrored = ""; // 손님: 목록에 반영한 방장 곡

		private bool songMissing; // 손님: 방장 곡이 내 게임에 없음

		private string sentDiff = "";

		private float previewAt;

		private string previewRoot = "";

		private MODE myMode = MODE.Messi;

		private static readonly MethodInfo mPlayVideo = typeof(MusicSelectRoot).GetMethod("PlayVideo", F);

		private BaseMusicBundle Cur => (cursor >= 0 && cursor < list.Length) ? list[cursor] : null;

		private void ResetRoomList()
		{
			myMode = (MODE)Mathf.Clamp(PlayerPrefs.GetInt("WJONLINE_MODE", 0), 0, (int)MODE.COUNT - 1);
			mirrored = "";
			songMissing = false;
			string last = GameValue.lastPlayed?.id;
			BuildList(FREESTYLE_CATEGORY.ALL, last);
		}

		// 게임 곡 선택 화면과 같은 목록 (같은 정렬 설정). 랜덤 곡은 온라인에서 뺀다
		private void BuildList(FREESTYLE_CATEGORY cat, string keepRoot)
		{
			listCat = cat;
			try
			{
				list = BundlePackage.musicBundle.SearchBundles(cat).Where((BaseMusicBundle b) => !b.IsRandom).ToArray();
			}
			catch (Exception e)
			{
				Debug.LogWarning("[WJMAXOnline] list failed: " + e.Message);
				list = new BaseMusicBundle[0];
			}
			if (list.Length == 0 && cat != FREESTYLE_CATEGORY.ALL)
			{
				BuildList(FREESTYLE_CATEGORY.ALL, keepRoot);
				return;
			}
			cursor = Mathf.Max(0, Array.FindIndex(list, (BaseMusicBundle b) => b.RootFileName == keepRoot));
		}

		// 곡 파일명 → 내 게임의 곡 (모든 난이도가 든 원본, 없으면 null)
		private static BaseMusicBundle BaseOf(string root, KEY key)
		{
			return BundlePackage.musicBundle.GetMusicBundles(FREESTYLE_CATEGORY.ALL, key).FirstOrDefault((BaseMusicBundle b) => b.RootFileName == root && !(b is SoloMusicBundle));
		}

		// 지금 방의 곡 (방장 = 내 커서, 손님 = 방장 곡)
		private BaseMusicBundle RoomSong()
		{
			if (isHost)
			{
				BaseMusicBundle c = Cur;
				return (c == null) ? null : BaseOf(c.RootFileName, GameValue.key);
			}
			string[] s = hostSel.Split('|');
			return (s.Length < 3 || songMissing) ? null : BaseOf(s[0], (KEY)Int(s[1], 0, (int)KEY.Count - 1));
		}

		private static float LevelOf(BaseMusicBundle b, KEY key, int mode)
		{
			return b?.GetSheetPacksWithinLevel(key, new LevelRange(0f, 99f)).FirstOrDefault((SheetPack x) => (int)x.mode == mode)?.level ?? 0f;
		}

		// 곡에 맞춘 내 난이도 (원하는 난이도가 없으면 게임처럼 가까운 것)
		private MODE MyModeFor(BaseMusicBundle b)
		{
			return (b == null) ? myMode : b.ChangeMode(GameValue.key, myMode);
		}

		private void MoveCursor(int d)
		{
			if (list.Length == 0)
			{
				return;
			}
			cursor = (int)Mathf.Repeat(cursor + d, list.Length);
			PlaySe(AudioManager.SE.SlideMove);
		}

		private void MoveCategory(int d)
		{
			string keep = Cur?.RootFileName;
			FREESTYLE_CATEGORY c = listCat;
			for (int i = 0; i < (int)FREESTYLE_CATEGORY.COUNT; i++)
			{
				c = (FREESTYLE_CATEGORY)Mathf.Repeat((int)c + d, (int)FREESTYLE_CATEGORY.COUNT);
				if (BundlePackage.musicBundle.GetMusicBundles(c, GameValue.key).Any((BaseMusicBundle b) => !b.IsRandom))
				{
					break;
				}
			}
			BuildList(c, keep);
			PlaySe(AudioManager.SE.SlideMove);
		}

		private void SwitchKey()
		{
			string keep = Cur?.RootFileName;
			KEY next = (GameValue.key == KEY.Key4) ? KEY.Key6 : KEY.Key4;
			if (!BundlePackage.musicBundle.GetMusicBundles(FREESTYLE_CATEGORY.ALL, next).Any())
			{
				PlaySe(AudioManager.SE.error);
				return;
			}
			GameValue.key = next;
			BuildList(listCat, keep);
			PlaySe(AudioManager.SE.Confirm);
		}

		// 게임 검색과 같은 규칙 (곡 정보의 검색어에 포함)
		private void SearchSong(string str)
		{
			str = (str ?? "").Trim().ToLower().Replace(" ", "");
			if (str.Length == 0)
			{
				return;
			}
			BaseMusicBundle hit = BundlePackage.musicBundle.GetMusicBundles(FREESTYLE_CATEGORY.ALL, GameValue.key).Where((BaseMusicBundle b) => !b.IsRandom && (b.info.search ?? b.title ?? "").Replace(" ", "").ToLower().Split(',').Any((string t) => t.Contains(str))).OrderBy((BaseMusicBundle b) => b.title.Length).FirstOrDefault();
			if (hit == null)
			{
				Status("검색 결과가 없어요", Ui.Gray);
				PlaySe(AudioManager.SE.error);
				return;
			}
			if (!list.Any((BaseMusicBundle b) => b.RootFileName == hit.RootFileName))
			{
				listCat = FREESTYLE_CATEGORY.ALL;
			}
			BuildList(listCat, hit.RootFileName);
			PlaySe(AudioManager.SE.Confirm);
		}

		// ---------------- 매 프레임 ----------------
		private void RoomTick()
		{
			if (!connected || inMatch)
			{
				return;
			}
			if (isHost)
			{
				BaseMusicBundle c = Cur;
				string here = (c == null || battleMode) ? "" : (c.RootFileName + "|" + (int)GameValue.key + "|" + (int)listCat);
				if (here != sentSel)
				{
					sentSel = here;
					net?.Broadcast("SEL|" + ((here.Length > 0) ? here : "||0"));
				}
			}
			else if (hostSel != mirrored)
			{
				MirrorHostSong();
			}
			// 내 난이도 알리기 (곡이 없으면 -1 = 관전)
			BaseMusicBundle song = RoomSong();
			string diff = ((song != null) ? ((int)MyModeFor(song)).ToString() : "-1") + "|" + (int)GameValue.key + "|" + (BundlePackage.closet.isPlusMode ? 1 : 0) + "|" + Mathf.RoundToInt(BundlePackage.setting.NoteSpeed * 10f);
			if (diff != sentDiff)
			{
				sentDiff = diff;
				string line = "DF|" + myId + "|" + diff;
				ApplyDiff(line.Split('|'));
				Send(line);
			}
			if (scr == Scr.Room)
			{
				NoteSongChange();
			}
			// 고른 곡 미리듣기 (게임 곡 선택 화면의 미리보기 영상 재생기를 그대로 사용)
			// 대전: 룰렛이 멈춘 뒤에야 이번 라운드 곡 미리듣기 (그 전엔 곡이 드러나지 않게)
			BaseMusicBundle pb = (scr == Scr.Room && !battleMode) ? Cur : null;
			if (scr == Scr.Battle && bat.phase == BAT_REVEAL && !Roulette(out _, out _) && bat.round >= 1 && bat.round <= bat.order.Count)
			{
				pb = CandBundle(bat.cands[bat.order[bat.round - 1]], out _, out _);
			}
			string root = pb?.RootFileName ?? "";
			if (root != previewRoot)
			{
				previewRoot = root;
				previewAt = Time.unscaledTime + 0.35f;
			}
			if (previewAt > 0f && Time.unscaledTime >= previewAt && lister != null && !ListerPlaying)
			{
				previewAt = 0f;
				BaseMusicBundle b = pb;
				if (b != null && root.Length > 0)
				{
					try
					{
						mPlayVideo.Invoke(lister, new object[1] { b.previewUrl });
						// 로비에서 멈춰 둔 곡과 같은 곡이면 게임이 다시 틀지 않으므로 직접 이어 튼다
						VideoPlayer pv = fPreviewVideo.GetValue(lister) as VideoPlayer;
						if (pv != null && !pv.isPlaying)
						{
							pv.Play();
						}
					}
					catch (Exception)
					{
					}
				}
			}
		}

		// 손님: 방장 곡 위치로 내 목록을 옮긴다
		private void MirrorHostSong()
		{
			mirrored = hostSel;
			string[] s = hostSel.Split('|');
			if (s.Length < 3 || s[0].Length == 0)
			{
				songMissing = false;
				return;
			}
			KEY key = (KEY)Int(s[1], 0, (int)KEY.Count - 1);
			FREESTYLE_CATEGORY cat = (FREESTYLE_CATEGORY)Int(s[2], 0, (int)FREESTYLE_CATEGORY.COUNT - 1);
			GameValue.key = key;
			songMissing = BaseOf(s[0], key) == null;
			if (songMissing)
			{
				Status("방장이 고른 곡이 내 게임에 없어서 이번 곡은 관전해요", Ui.Gold, 6f);
				return;
			}
			BuildList(cat, s[0]);
			if (Cur == null || Cur.RootFileName != s[0])
			{
				BuildList(FREESTYLE_CATEGORY.ALL, s[0]);
			}
		}

		private void ApplyDiff(string[] p)
		{
			Player pl = (p.Length >= 5) ? Find(Int(p[1], -1, int.MaxValue)) : null;
			if (pl != null)
			{
				pl.diffMode = Int(p[2], -1, (int)MODE.COUNT - 1);
				pl.diffKey = Int(p[3], 0, (int)KEY.Count - 1);
				pl.plus = p[4] == "1";
				pl.speed10 = (p.Length > 5) ? Int(p[5], 0, 99) : 0;
			}
		}

		private static string ModeName(int m)
		{
			return m switch
			{
				0 => "메시",
				1 => "엔젤",
				2 => "왁굳",
				3 => "민수",
				4 => "거짓말",
				_ => "-",
			};
		}

		private static string KeyName(int k)
		{
			return ((KEY)k) switch
			{
				KEY.Key5 => "5B",
				KEY.Key6 => "6B",
				_ => "4B",
			};
		}

		// "왁굳+ Lv.13" (방 곡 기준)
		private string DiffText(Player p)
		{
			if (p.diffMode < 0)
			{
				return "관전";
			}
			string s = ModeName(p.diffMode) + (p.plus ? "+" : "");
			float lv = LevelOf(RoomSong(), (KEY)p.diffKey, p.diffMode);
			return (lv > 0f) ? (s + " Lv." + Mathf.FloorToInt(lv)) : s;
		}

		// ---------------- 시작 조건 ----------------
		private List<Player> Participants => players.Where((Player p) => p.diffMode >= 0 && !p.offline && p.inRoom).ToList();

		private string StartBlockReason()
		{
			Player off = players.FirstOrDefault((Player p) => p.offline);
			if (off != null)
			{
				return off.name + " 님의 재접속을 기다리는 중";
			}
			if (matchLive)
			{
				return "아직 곡을 플레이 중인 사람이 있어요";
			}
			if (players.Count < 2)
			{
				return "2명 이상 모여야 시작할 수 있어요";
			}
			Player away = players.FirstOrDefault((Player p) => !p.inRoom);
			if (away != null)
			{
				return away.name + " 님이 아직 결과 화면에 있어요";
			}
			switch (matchType)
			{
			case MT_LADDER:
				if (players.Count != 2)
				{
					return "래더 매치는 1:1 이에요. 상대를 기다리는 중";
				}
				break;
			case MT_VERSUS:
			{
				List<Player> f = players.Where((Player p) => p.fighter).ToList();
				if (f.Count != 2)
				{
					return "대전자 2명을 정해 주세요 (F6)";
				}
				Player nr = f.FirstOrDefault((Player p) => p.id != myId && !p.ready);
				if (nr != null)
				{
					return nr.name + " 님의 준비를 기다리는 중";
				}
				return null;
			}
			default:
				if (RoomSong() == null)
				{
					return "곡을 골라 주세요";
				}
				break;
			}
			Player notReady = players.FirstOrDefault((Player p) => p.id != myId && !p.ready && p.diffMode >= 0);
			if (notReady != null)
			{
				return notReady.name + " 님의 준비를 기다리는 중";
			}
			if (teamMode && Participants.Select((Player p) => p.team).Distinct().Count() < 2)
			{
				return "팀전은 두 팀 이상이 있어야 해요 (F9 팀 바꾸기)";
			}
			return null;
		}

		// ---------------- 키 입력 ----------------
		private bool confirmLeave;

		private bool kickOpen; // 방장: 내보내기 목록

		private int kickSel;

		private int pcur; // 방장: PLAYERS 커서 (버서스 대전자 지정)

		private void InputRoom()
		{
			if (chatting)
			{
				InputChat();
				return;
			}
			if (confirmLeave)
			{
				if (Enter())
				{
					PlaySe(AudioManager.SE.Confirm);
					confirmLeave = false;
					LeaveRoom();
				}
				else if (Key(KeyCode.Escape))
				{
					PlaySe(AudioManager.SE.SlideMove);
					confirmLeave = false;
				}
				return;
			}
			if (kickOpen)
			{
				List<Player> others = players.Where((Player p) => p.id != myId).ToList();
				if (others.Count == 0 || Key(KeyCode.Escape) || Key(KeyCode.F8))
				{
					kickOpen = false;
					PlaySe(AudioManager.SE.SlideMove);
					return;
				}
				kickSel = Mathf.Clamp(kickSel, 0, others.Count - 1);
				if (Key(KeyCode.UpArrow))
				{
					kickSel = (kickSel + others.Count - 1) % others.Count;
					PlaySe(AudioManager.SE.SlideMove);
				}
				else if (Key(KeyCode.DownArrow))
				{
					kickSel = (kickSel + 1) % others.Count;
					PlaySe(AudioManager.SE.SlideMove);
				}
				else if (Enter())
				{
					PlaySe(AudioManager.SE.Confirm);
					Kick(others[kickSel].id);
					kickOpen = false;
				}
				return;
			}
			if (Key(KeyCode.Escape))
			{
				PlaySe(AudioManager.SE.Confirm);
				confirmLeave = true;
				return;
			}
			if (isHost && Key(KeyCode.F8))
			{
				if (players.Count < 2)
				{
					Status("내보낼 사람이 없어요", Ui.Gray);
					PlaySe(AudioManager.SE.error);
					return;
				}
				kickOpen = true;
				kickSel = 0;
				PlaySe(AudioManager.SE.Confirm);
				return;
			}
			if (Key(KeyCode.F3))
			{
				chatting = true;
				chatText = "";
				PlaySe(AudioManager.SE.SlideMove);
				return;
			}
			if (Key(KeyCode.F1) && roomCode.Contains("-"))
			{
				GUIUtility.systemCopyBuffer = roomCode;
				Status("방 코드를 복사했어요. 친구에게 붙여넣기로 보내 주세요!", Ui.Green);
				PlaySe(AudioManager.SE.Confirm);
				return;
			}
			if (Key(KeyCode.F11))
			{
				PlaySe(AudioManager.SE.Confirm);
				LoadHistory();
				SetScr((lastMatch != null) ? Scr.Result : Scr.History);
				return;
			}
			// 게임 설정 / 옷장 / 배속 (게임 키와 같음)
			MainSubMenu sub = SubMenu;
			if ((Key(KeyCode.LeftControl) || Key(KeyCode.RightControl)) && sub != null)
			{
				sub.gameOptionManager.OpenMenu();
				return;
			}
			if (Key(KeyCode.Space) && sub != null)
			{
				sub.closetManager.OpenMenu();
				return;
			}
			int speed = (Key(KeyCode.Alpha1) ? (-1) : 0) + (Key(KeyCode.Alpha2) ? 1 : 0);
			if (speed != 0)
			{
				BundlePackage.setting.NoteSpeed += 0.1f * speed;
				PlaySe(AudioManager.SE.SlideMove);
			}
			if (Key(KeyCode.F9) && teamMode && Me != null)
			{
				if (matchLive)
				{
					Status("곡 진행 중에는 팀을 바꿀 수 없어요", Ui.Gold);
					PlaySe(AudioManager.SE.error);
					return;
				}
				Me.team = (Me.team + 1) % 3;
				PlaySe(AudioManager.SE.SlideMove);
				if (isHost)
				{
					BroadcastRoster();
				}
				else
				{
					net?.server?.Send("TEAM|" + myId + "|" + Me.team);
				}
				return;
			}
			if (isHost && Key(KeyCode.F12))
			{
				if (matchLive || bat.phase != BAT_NONE)
				{
					Status("곡 진행 중에는 대전 방식을 바꿀 수 없어요", Ui.Gold);
					PlaySe(AudioManager.SE.error);
					return;
				}
				matchType = (matchType + 1) % 3;
				if (matchType == MT_VERSUS)
				{
					// 대전자 2명: 방장 + 먼저 들어온 사람
					for (int i = 0; i < players.Count; i++)
					{
						players[i].fighter = i < 2;
					}
				}
				PlaySe(AudioManager.SE.SlideMove);
				BroadcastRoster();
				return;
			}
			if (matchType == MT_VERSUS)
			{
				InputVersusHost();
			}
			else
			{
				InputSongSelect();
			}
			if (Enter())
			{
				if (isHost)
				{
					string reason = StartBlockReason();
					if (reason != null)
					{
						Status(reason, Ui.Gold);
						PlaySe(AudioManager.SE.error);
					}
					else if (battleMode)
					{
						BattleBegin();
					}
					else
					{
						StartSongMatch();
					}
				}
				else if (Me != null)
				{
					if (matchType == MT_VERSUS && !Me.fighter)
					{
						Status("관전자예요. 방장이 대전자로 올려 주면 플레이할 수 있어요", Ui.Gray);
						PlaySe(AudioManager.SE.error);
						return;
					}
					if (Me.diffMode < 0 && !battleMode)
					{
						Status("방장이 고른 곡이 없어서 이번 곡은 관전이에요", Ui.Gray);
						PlaySe(AudioManager.SE.error);
						return;
					}
					Me.ready = !Me.ready;
					net?.server?.Send("READY|" + myId + "|" + (Me.ready ? 1 : 0));
					PlaySe(Me.ready ? AudioManager.SE.Confirm : AudioManager.SE.SlideMove);
				}
			}
		}

		// 곡·난이도 고르기 (FREE / TEAM). 손님은 난이도만
		private void InputSongSelect()
		{
			BaseMusicBundle song = RoomSong();
			if ((Key(KeyCode.LeftArrow) || Key(KeyCode.RightArrow)) && song != null)
			{
				song.ChangeMode(GameValue.key, myMode);
				if (song.ChangeMode(GameValue.key, Key(KeyCode.LeftArrow)))
				{
					myMode = song.ApplyMode;
					PlayerPrefs.SetInt("WJONLINE_MODE", (int)myMode);
					PlaySe(AudioManager.SE.SlideMove);
				}
			}
			if (!isHost)
			{
				return;
			}
			if (Repeat(KeyCode.UpArrow))
			{
				MoveCursor(-1);
			}
			else if (Repeat(KeyCode.DownArrow))
			{
				MoveCursor(1);
			}
			else if (Key(KeyCode.PageUp))
			{
				MoveCursor(-10);
			}
			else if (Key(KeyCode.PageDown))
			{
				MoveCursor(10);
			}
			else if (Key(KeyCode.LeftShift))
			{
				MoveCategory(-1);
			}
			else if (Key(KeyCode.RightShift))
			{
				MoveCategory(1);
			}
			else if (Key(KeyCode.Tab))
			{
				SwitchKey();
			}
			else if (Key(KeyCode.F4) && SubMenu != null)
			{
				string keep = Cur?.RootFileName;
				SubMenu.sortManager.OpenMenu(delegate
				{
					BuildList(listCat, keep);
				});
			}
			else if (Key(KeyCode.F2) && SubMenu != null)
			{
				SubMenu.searchPanel.Open(SearchSong);
			}
		}

		// 버서스 방장: ↑↓ 플레이어 고르기, F6 대전자/관전자, ←→ 레벨 범위(L-SHIFT 최소 / R-SHIFT 최대), F7 판정
		private void InputVersusHost()
		{
			if (!isHost)
			{
				return;
			}
			pcur = Mathf.Clamp(pcur, 0, Mathf.Max(0, players.Count - 1));
			if (Key(KeyCode.UpArrow))
			{
				pcur = (pcur + players.Count - 1) % Mathf.Max(1, players.Count);
				PlaySe(AudioManager.SE.SlideMove);
			}
			else if (Key(KeyCode.DownArrow))
			{
				pcur = (pcur + 1) % Mathf.Max(1, players.Count);
				PlaySe(AudioManager.SE.SlideMove);
			}
			else if (Key(KeyCode.F6) && pcur < players.Count)
			{
				Player p = players[pcur];
				if (!p.fighter && players.Count((Player x) => x.fighter) >= 2)
				{
					// 대전자가 꽉 찼으면 다른 대전자 한 명(커서와 먼 쪽)과 자리 바꾸기
					Player swap = players.Last((Player x) => x.fighter);
					swap.fighter = false;
					swap.ready = false;
				}
				p.fighter = !p.fighter;
				p.ready = false;
				PlaySe(AudioManager.SE.Confirm);
				BroadcastRoster();
			}
			else if (Key(KeyCode.LeftArrow) || Key(KeyCode.RightArrow))
			{
				InitVsRange();
				int dir = Key(KeyCode.LeftArrow) ? (-1) : 1;
				if (vsEditMax)
				{
					vsMax = Mathf.Clamp(vsMax + dir, vsMin, LV_MAX);
				}
				else
				{
					vsMin = Mathf.Clamp(vsMin + dir, 1, vsMax);
				}
				PlaySe(AudioManager.SE.SlideMove);
				BroadcastRoster();
			}
			else if (Key(KeyCode.LeftShift) || Key(KeyCode.RightShift))
			{
				vsEditMax = Key(KeyCode.RightShift);
				PlaySe(AudioManager.SE.SlideMove);
			}
			else if (Key(KeyCode.F7))
			{
				versusHard = !versusHard;
				PlaySe(AudioManager.SE.SlideMove);
				BroadcastRoster();
			}
		}

		private void InputChat()
		{
			Input.imeCompositionMode = IMECompositionMode.On;
			if (Enter())
			{
				SendChat(chatText);
				chatText = "";
				chatting = false;
				Input.imeCompositionMode = IMECompositionMode.Auto;
			}
			else if (Key(KeyCode.Escape))
			{
				chatting = false;
				Input.imeCompositionMode = IMECompositionMode.Auto;
			}
			else
			{
				chatText = EditText(chatText, 60, (char c) => c != '|');
			}
		}
	}
}
