// WJMAX 온라인 모드. 원본 Assembly-CSharp.dll 은 수정하지 않고, RuntimeInitializeOnLoads.json 에 등록되어 게임 시작 시 Init() 이 호출된다.
// 화면 흐름 (디맥 RESPECT V 온라인과 같은 구성):
//   타이틀 ONLINE → 로비 (OPEN MATCH / LADDER MATCH 카드) → 방 만들기 · 코드로 참가
//   → 오픈 매치 방 (FREE / TEAM / VERSUS) 또는 래더 방 → (VERSUS·래더) 밴픽 → 라운드 공개 → 곡 → 게임 결과(+온라인 순위) → 방
// 방 화면은 게임 곡 선택 화면을 고쳐 쓰지 않고 전부 직접 그린다. 게임 곡 선택 화면은 뒤에서 곡 시작·게임 설정·옷장에만 쓴다.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;
using WJMAX.Title;

namespace WJMAXOnline
{
	public partial class OnlineMod : MonoBehaviour
	{
		private const string VERSION = "10";

		// 자동 업데이트 빌드 번호 (release.bat 이 올림). VERSION 은 통신 형식이 바뀔 때만 올린다
		private const int BUILD = 4;

		private const BindingFlags F = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

		private static readonly CultureInfo IC = CultureInfo.InvariantCulture;

		private static readonly FieldInfo fTitleInfoMgr = typeof(TitleManager).GetField("titleInfoManager", F);

		private static readonly FieldInfo fInfos = typeof(TitleInfoManager).GetField("infos", F);

		private static readonly FieldInfo fMenuIndex = typeof(TitleManager).GetField("currentMenuIndex", F);

		private static readonly FieldInfo fInputDelay = typeof(CInput).GetField("inputDelay", BindingFlags.Static | BindingFlags.NonPublic);

		private enum Scr
		{
			None,
			Lobby, // OPEN MATCH / LADDER MATCH 고르기 (+ 방 만들기·참가 팝업)
			Room, // 방 (오픈 매치 / 래더)
			Battle, // 밴픽 · 라운드 공개 · 최종 결과 (VERSUS, 래더)
			Ladder, // 래더 매치 화면 (내 티어 · AI 매칭 · 티어표)
			Result, // 지난 판 결과
			History, // 최근 대전 기록
			Place // 아카데미 배치고사
		}

		// 대전 방식
		private const int MT_FREE = 0;

		private const int MT_TEAM = 1;

		private const int MT_VERSUS = 2;

		private const int MT_LADDER = 3;

		private class Player
		{
			public int id;

			public string name;

			public bool inRoom = true;

			public bool ready;

			public int phase;

			public bool loaded;

			public bool done;

			public bool cleared;

			public bool fullCombo;

			public int score;

			public int combo;

			public float rate;

			public float hp = 1f;

			public int brk; // 곡 진행 중 BREAK(MISS) 수

			public int team; // 0 A, 1 B, 2 C (TEAM MATCH)

			public bool fighter; // VERSUS 대전자

			public bool offline; // 연결이 끊겨 재접속을 기다리는 중

			public float offlineSince;

			public string token = ""; // 재접속 확인용 (방장만 보관)

			public int ping;

			// 프로필 (PROF)
			public int level, mastery, clears, ach, achTotal, tier, lp;

			public string tag = "", icon = "", plate = "";

			// 판정 상세 (FIN)
			public int perfect, great, good, bad, miss, maxCombo;

			// 방에서 고른 난이도 (DF): -1 = 방장 곡이 내 게임에 없음 → 관전
			public int diffMode = -1, diffKey;

			public bool plus;

			public int speed10; // 배속 x10 (디맥처럼 방에서 서로의 속도가 보임)

			public string matchDiff = ""; // 곡 시작 때의 난이도 글자

			public Player Copy()
			{
				return (Player)MemberwiseClone();
			}
		}

		// ---- 상태 ----
		private bool online;

		private Scr scr;

		private float scrTime;

		private Net net;

		private bool isHost;

		private bool connected;

		private bool connecting;

		private int myId = -1;

		private int nextId = 1;

		private int maxPlayers = 8;

		private int matchType;

		private string roomCode = "";

		private string reachNote = "";

		private string reachResult; // 백그라운드 스레드 → 메인

		private readonly List<Player> players = new List<Player>();

		private string myName = "";

		private string status = "";

		private Color statusColor = Color.white;

		private float statusUntil;

		private MainMusicLister lister;

		private bool focused;

		// ---- 타이틀 ----
		private TitleManager titleManager;

		private int onlineTitleIndex = -1;

		private int lastTitleIndex = -1;

		private SCENE lastScene;

		private bool teamMode => matchType == MT_TEAM;

		private bool battleMode => matchType == MT_VERSUS || matchType == MT_LADDER;

		private Player Me => players.FirstOrDefault((Player p) => p.id == myId);

		private Player Find(int id)
		{
			return players.FirstOrDefault((Player p) => p.id == id);
		}

		public static void Init()
		{
			GameObject go = new GameObject("WJMAX_Online");
			DontDestroyOnLoad(go);
			go.AddComponent<OnlineMod>();
			Debug.Log("[WJMAXOnline] loaded v" + VERSION + " build " + BUILD);
			Updater.Start(BUILD);
		}

		private void Awake()
		{
			SceneManager.sceneLoaded += OnSceneLoaded;
			portText = PlayerPrefs.GetString("WJONLINE_PORT", "7777");
			codeText = PlayerPrefs.GetString("WJONLINE_CODE", "");
		}

		private void OnApplicationQuit()
		{
			Disconnect();
			RestoreAfterMatch();
		}

		// 타이틀의 "ONLINE" 메뉴 잠금 해제 → 프리스타일 씬을 깔고 그 위에 온라인 화면
		private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
		{
			if (SceneLoader.Current != SCENE.Title)
			{
				return;
			}
			try
			{
				titleManager = FindAnyObjectByType<TitleManager>();
				TitleInfo[] infos = (titleManager == null) ? null : (fInfos.GetValue(fTitleInfoMgr.GetValue(titleManager)) as TitleInfo[]);
				for (int i = 0; infos != null && i < infos.Length; i++)
				{
					if (infos[i].gameMode == GAMEMODE.Academy)
					{
						infos[i].isLock = false;
					}
					if (infos[i].gameMode == GAMEMODE.Online || i == onlineTitleIndex)
					{
						infos[i].isLock = false;
						infos[i].gameMode = GAMEMODE.FreeStyle;
						infos[i].scene = SCENE.FreeStyle;
						onlineTitleIndex = i;
					}
				}
			}
			catch (Exception e)
			{
				Debug.LogWarning("[WJMAXOnline] title patch failed: " + e);
			}
		}

		private void Update()
		{
			try
			{
				Tick();
			}
			catch (Exception e)
			{
				// 한 프레임 오류로 온라인 전체가 멈추지 않게 (같은 오류는 한 번만 기록)
				string msg = e.GetType().Name + ": " + e.Message;
				if (msg != lastError)
				{
					lastError = msg;
					Debug.LogWarning("[WJMAXOnline] " + e);
#if DEV
					System.IO.File.AppendAllText(System.IO.Path.Combine(DevDir, "log.txt"), "ERR " + e + "\n");
#endif
				}
			}
		}

		private string lastError;

		private void Tick()
		{
#if DEV
			DevUpdate();
#endif
			PumpNet();
			UpdateConnection();
			if (reachResult != null)
			{
				string[] r = reachResult.Split('|');
				reachResult = null;
				int.TryParse(portText, out var port);
				roomCode = RoomCode.Encode(r[0], port);
				reachNote = r[1];
				AddChat("|" + (reachNote.Contains("같은 공유기") ? "밖에서는 접속할 수 없어요: " : "접속 상태: ") + reachNote);
			}
			SCENE scene = SceneLoader.Current;
			if (scene != lastScene)
			{
				OnSceneChanged(lastScene, scene);
				lastScene = scene;
			}
			if (scene == SCENE.Title && titleManager != null)
			{
				lastTitleIndex = (int)fMenuIndex.GetValue(titleManager);
			}
			if (scene == SCENE.FreeStyle)
			{
				FreeStyleTick();
			}
			if (scene == SCENE.Academy)
			{
				AcademyTick();
			}
			if (scene == SCENE.Game && inMatch)
			{
				UpdateGame();
			}
			// 결과 화면의 F5(재시작) 차단: 결과 화면 입력은 코루틴이라 Update 다음에 읽힌다
			if (scene == SCENE.Result && online && Input.GetKeyDown(KeyCode.F5))
			{
				fInputDelay.SetValue(null, Time.time + 0.3f);
			}
			FixLoadingPanel(scene);
			LadderTick();
			PlaceTick();
			AiTick();
			BattleTick();
			MatchTick();
			SyncPhase();
			UpdateView(scene);
		}

		private void FreeStyleTick()
		{
			if (lister == null)
			{
				lister = FindAnyObjectByType<MainMusicLister>();
				if (lister != null && online)
				{
					PrepareLister();
				}
			}
			if (!online)
			{
				if (Key(KeyCode.F7) && !CInput.isShowing && lister != null)
				{
					EnterOnline();
				}
				return;
			}
			RoomTick();
			if (pendingPlay != null && ListerReady && !GameWindowOpen)
			{
				ExecutePlay();
			}
			// 내 창이 맨 위일 때만 온라인 키를 받는다 (게임 설정·옷장 등 게임 창이 떠 있으면 그 창이 받음)
			if (focused && CInput.IsFocus(base.gameObject) && !Fader.isFading)
			{
				// F10: 전체 키 안내 (아래 키 바에는 자주 쓰는 키만 보여준다)
				if (!chatting && (hotkeysMore || keyHelp) && Key(KeyCode.F10))
				{
					keyHelp = !keyHelp;
					PlaySe(AudioManager.SE.SlideMove);
					return;
				}
				if (keyHelp && !hotkeysMore)
				{
					keyHelp = false;
				}
				if (keyHelp)
				{
					if (Key(KeyCode.Escape) || Key(KeyCode.Return))
					{
						keyHelp = false;
						PlaySe(AudioManager.SE.SlideMove);
					}
					return;
				}
				switch (scr)
				{
				case Scr.Lobby:
					InputLobby();
					break;
				case Scr.Room:
					InputRoom();
					break;
				case Scr.Battle:
					InputBattle();
					break;
				case Scr.Ladder:
					InputLadder();
					break;
				case Scr.Result:
					InputResult();
					break;
				case Scr.History:
					InputHistory();
					break;
				case Scr.Place:
					InputPlace();
					break;
				}
			}
		}

		private void OnSceneChanged(SCENE from, SCENE to)
		{
			PlaceSceneChanged(from, to);
			if (from == SCENE.Game && inMatch && !sentFinal)
			{
				SendFinal(cleared: false, fc: false);
			}
			if (to == SCENE.FreeStyle)
			{
				lister = null;
				if (from == SCENE.Title && onlineTitleIndex >= 0 && lastTitleIndex == onlineTitleIndex)
				{
					EnterOnline();
				}
				else if (placePending)
				{
					placePending = false;
					EnterOnline();
					EnterPlacement();
				}
				else if (online && inMatch)
				{
					inMatch = false;
					BackToRoom();
				}
			}
			if (to == SCENE.Title && online)
			{
				Disconnect();
				online = false;
				SetScr(Scr.None);
			}
			if (from == SCENE.Game)
			{
				RestoreAfterMatch();
				fInputDelay.SetValue(null, 0f);
				paused = false;
			}
		}

		private void EnterOnline()
		{
			online = true;
			myName = CleanName(BundlePackage.user?.UserName);
			LoadHistory();
			// 온라인 중에는 창을 바꿔도 게임이 멈추지 않게 (멈추면 방 전체가 기다리게 됨)
			Application.runInBackground = true;
			lobbyCard = 0;
			lobbyRow = 0;
			popup = Popup.None;
			if (lister != null)
			{
				PrepareLister();
			}
			SetScr(Scr.Lobby);
			PlaySe(AudioManager.SE.Confirm);
		}

		private void LeaveOnline()
		{
			Disconnect();
			online = false;
			SetScr(Scr.None);
			Singleton<Fader>.Instance.FadeOut(delegate
			{
				SceneLoader.LoadScene(SCENE.Title);
			});
		}

		private void SetScr(Scr s)
		{
			if (scr != s)
			{
				scr = s;
				scrTime = Time.unscaledTime;
				histSel = 0;
				chatting = false;
				confirmLeave = false;
			}
		}

		private void Status(string msg, Color c, float seconds = 4f)
		{
			status = msg;
			statusColor = c;
			statusUntil = Time.unscaledTime + seconds;
		}

		// ---------------- 게임 곡 선택 화면 (뒤에 깔린 것) ----------------
		private static readonly FieldInfo fIsPlayGame = typeof(MusicSelectRoot).GetField("isPlayGame", F);

		private static readonly FieldInfo fIsInput = typeof(MainMusicLister).GetField("isInput", F);

		private static readonly FieldInfo fSubMenu = typeof(MainMusicLister).GetField("subMenu", F);

		private bool ListerReady => lister != null && (bool)fIsInput.GetValue(lister) && !(bool)fIsPlayGame.GetValue(lister);

		private bool ListerPlaying => lister != null && (bool)fIsPlayGame.GetValue(lister);

		private MainSubMenu SubMenu => (lister == null) ? null : (fSubMenu.GetValue(lister) as MainSubMenu);

		// 게임 창(게임 설정, 옷장, 정렬, 검색, 팝업 등)이 내 창 위에 떠 있음
		private bool GameWindowOpen => CInput.windowList.Any((GameObject w) => w != null && w != base.gameObject && w.activeInHierarchy) || Option.isOpenOption;

		// 온라인 중: 오른쪽 위 메뉴 아이콘 숨김, 퀘스트 자동 열기 막기
		private void PrepareLister()
		{
			foreach (FlagIcon f in FindObjectsByType<FlagIcon>(FindObjectsInactive.Include, FindObjectsSortMode.None))
			{
				string n = f.gameObject.name;
				if (n == "KeyInfo" || n == "KeyArchive" || n == "KeyShop" || n == "KeyQuest")
				{
					f.gameObject.SetActive(value: false);
				}
			}
			QuestManager q = FindAnyObjectByType<QuestManager>(FindObjectsInactive.Include);
			if (q != null)
			{
				q.LockOpenForce();
			}
		}

		// ---------------- 키보드 입력 ----------------
		private static bool Key(KeyCode k)
		{
#if DEV
			if (DevKey(k))
			{
				return true;
			}
#endif
			return Input.GetKeyDown(k);
		}

		private static bool Enter()
		{
			return Key(KeyCode.Return) || Key(KeyCode.KeypadEnter);
		}

		private KeyCode repeatKey;

		private float repeatAt;

		// 누르고 있으면 반복 (목록 이동)
		private bool Repeat(KeyCode k)
		{
			if (Key(k))
			{
				repeatKey = k;
				repeatAt = Time.unscaledTime + 0.32f;
				return true;
			}
			if (repeatKey == k && !Input.GetKey(k))
			{
				repeatKey = KeyCode.None;
			}
			if (repeatKey == k && Time.unscaledTime >= repeatAt)
			{
				repeatAt = Time.unscaledTime + 0.05f;
				return true;
			}
			return false;
		}

		private static string Typed()
		{
#if DEV
			return Input.inputString + DevTyped();
#else
			return Input.inputString;
#endif
		}

		private static string EditText(string v, int max, Func<char, bool> ok)
		{
			foreach (char c in Typed())
			{
				if (c == '\b')
				{
					if (v.Length > 0)
					{
						v = v.Substring(0, v.Length - 1);
					}
				}
				else if (!char.IsControl(c) && v.Length < max && ok(c))
				{
					v += c;
				}
			}
			return v;
		}

		// ---------------- 공통 ----------------
		// 받은 숫자는 범위를 강제 (NaN·무한대·음수 등으로 화면이 깨지지 않게)
		private static int Int(string s, int min, int max)
		{
			if (!int.TryParse(s, NumberStyles.Integer, IC, out var v))
			{
				v = min;
			}
			return Mathf.Clamp(v, min, max);
		}

		private static float Num(string s, float min, float max)
		{
			if (!float.TryParse(s, NumberStyles.Float, IC, out var v) || float.IsNaN(v) || float.IsInfinity(v))
			{
				v = min;
			}
			return Mathf.Clamp(v, min, max);
		}

		// 닉네임: 구분자와 TMP 서식 태그(<...>) 제거, 길이 제한
		private static string CleanName(string s)
		{
			s = new string((s ?? "").Where((char ch) => ch != '|' && ch != ',' && ch != ';' && ch != ':' && ch != '<' && ch != '>' && !char.IsControl(ch)).ToArray()).Trim();
			if (s.Length > 12)
			{
				s = s.Substring(0, 12);
			}
			return (s.Length != 0) ? s : "플레이어";
		}

		private static string CleanCode(string s)
		{
			s = new string((s ?? "").Where((char ch) => (ch >= 'A' && ch <= 'Z') || (ch >= '0' && ch <= '9') || ch == '-').ToArray());
			return (s.Length <= 16) ? s : s.Substring(0, 16);
		}

		private static string CleanText(string s, int max)
		{
			s = new string((s ?? "").Where((char c) => c != '|' && c != '<' && c != '>' && c != ';' && !char.IsControl(c)).ToArray()).Trim();
			return (s.Length > max) ? s.Substring(0, max) : s;
		}

		private static string CleanRoot(string s)
		{
			s = new string((s ?? "").Where((char c) => c != '|' && c != ':' && c != ';' && !char.IsControl(c)).ToArray());
			return (s.Length > 100) ? s.Substring(0, 100) : s;
		}

		private static string CleanId(string s)
		{
			s = new string((s ?? "").Where((char c) => char.IsLetterOrDigit(c) || c == '_' || c == '-' || c == '.' || c == ' ').ToArray());
			return (s.Length > 48) ? s.Substring(0, 48) : s;
		}

		private static void PlaySe(AudioManager.SE se)
		{
			AudioManager a = Singleton<AudioManager>.Instance;
			if (a != null)
			{
				a.PlaySe(se);
			}
		}
	}
}
