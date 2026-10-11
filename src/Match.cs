// 곡 진행: 시작 알림(PLAY) → 각자 게임 곡 선택 화면으로 곡 시작 → 모두 로딩될 때까지 카운트다운 멈춤 → 동시 출발(GO)
// → 점수 실시간 공유(SC) → 끝(FIN) → 게임 결과 화면(+온라인 순위) → 방.
// 곡이 없는 사람·버서스 관전자는 이번 곡에 들어가지 않고 방에서 실시간 점수를 본다. 진행 중인 방에 들어온 사람도 관전.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using WJMAX.UserRecord;

namespace WJMAXOnline
{
	public partial class OnlineMod
	{
		private const float HOLD_DELAY = 3f;

		private const float HOLD_TIMEOUT = 60f;

		private static readonly MethodInfo mPlayMusic = typeof(MusicSelectRoot).GetMethod("PlayMusic", F);

		private static readonly FieldInfo fRecord = typeof(Judgement).GetField("record", F);

		private static readonly FieldInfo fHp = typeof(NoteGenerator).GetField("hp", F);

		private static readonly FieldInfo fMaxHp = typeof(NoteGenerator).GetField("maxHp", F);

		private static readonly MethodInfo mApplySpeed = typeof(Generator).GetMethod("OnApplyChangeSpeed", F);

		private static readonly FieldInfo fLockSpeed = typeof(Generator).GetField("isLockChangeSpeed", F);

		private static readonly FieldInfo fMenuWindow = typeof(GameOver).GetField("menuWindow", F);

		private static readonly FieldInfo fPlayMode = typeof(Closet).GetField("playMode", F);

		private bool inMatch; // 내가 곡을 플레이하는 중 (곡 선택 화면 출발 ~ 방 복귀)

		private bool matchLive; // 방에서 누군가 곡을 플레이하는 중 (방장이 정하고 ROSTER 로 알림)

		private readonly HashSet<int> matchIds = new HashSet<int>(); // 이번 곡 참가자

		private bool released = true;

		private bool sentFinal;

		private Generator heldGen;

		private float holdStart;

		private float liveSince;

		private float scoreTimer;

		private string lastSc;

		private float lastScAt;


		private string pendingPlay;

		private int loadedCount;

		private int totalCount;

		private string lastFin;

		private bool paused;

		private int pauseSel;

		private bool gameOver;

		private bool leaving;

		private int roomRound; // 이 방에서 몇 번째 곡인지 (인게임 ROUND 표시)

		// 이번 곡 정보 (HUD·결과·기록)
		private string matchTitle = "";

		private string matchArtist = "";

		private Sprite matchJacket;

		private string matchRoot = "";

		private KEY matchKey;

		private int matchKind; // 곡 시작 때의 대전 방식

		private int matchJudge; // 0 일반, 1 하드(플러스)

		private PLAYMODE? savedPlayMode;

		private FREESTYLE_CATEGORY? savedCategory;

		private List<Player> MP => players.Where((Player p) => matchIds.Contains(p.id)).ToList();

		// 지난 판 결과 (참가자 정보 복사본: 나간 사람도 남게)
		private class MatchResult
		{
			public string title, artist, root;

			public Sprite jacket;

			public KEY key;

			public int kind, round, judge;

			public List<Player> ps;
		}

		private MatchResult lastMatch;

		private bool resultTaken;

		// ---------------- 시작 ----------------
		// 방장 (FREE / TEAM): 지금 곡으로 시작. 곡이 있는 사람만 참가
		private void StartSongMatch()
		{
			BaseMusicBundle b = RoomSong();
			if (b == null)
			{
				return;
			}
			List<int> ids = Participants.Select((Player p) => p.id).ToList();
			if (!ids.Contains(myId))
			{
				ids.Insert(0, myId);
			}
			BroadcastPlay(b.RootFileName, GameValue.key, forced: false, MODE.NONE, -1, ids);
		}

		private void BroadcastPlay(string root, KEY key, bool forced, MODE mode, int judge, List<int> ids)
		{
			string line = "PLAY|" + root + "|" + (int)key + "|" + (forced ? 1 : 0) + "|" + (int)mode + "|" + judge + "|" + string.Join(",", ids);
			BeginLive(ids);
			net?.Broadcast(line);
			ReceivePlay(line);
			BroadcastRoster();
		}

		// 방장: 진행 중 표시 (참가자는 로딩 대기 대상)
		private void BeginLive(List<int> ids)
		{
			matchIds.Clear();
			foreach (int id in ids)
			{
				matchIds.Add(id);
			}
			matchLive = true;
			liveSince = Time.unscaledTime;
			released = false;
			loadedCount = 0;
			totalCount = ids.Count;
			ResetMatchScores();
			foreach (Player p in MP)
			{
				p.inRoom = false;
				p.ready = false;
				p.loaded = p.id == AI_ID; // AI 는 로딩이 없음
				p.phase = PH_LOADING;
			}
		}

		private void ResetMatchScores()
		{
			resultTaken = false;
			foreach (Player p in MP)
			{
				p.done = false;
				p.cleared = false;
				p.fullCombo = false;
				p.score = 0;
				p.combo = 0;
				p.rate = 0f;
				p.hp = 1f;
				p.perfect = p.great = p.good = p.bad = p.miss = p.maxCombo = p.brk = 0;
			}
		}

		// PLAY 받음: 내가 참가자면 곡 시작 예약, 아니면 관전
		private void ReceivePlay(string line)
		{
			string[] p = line.Split('|');
			if (p.Length < 7)
			{
				return;
			}
			HashSet<int> ids = new HashSet<int>(p[6].Split(',').Select((string s) => Int(s, -1, int.MaxValue)));
			if (!isHost)
			{
				matchIds.Clear();
				matchIds.UnionWith(ids);
				ResetMatchScores();
				released = false;
			}
			SetMatchInfo(p);
			if (ids.Contains(myId))
			{
				pendingPlay = line;
				if (Me != null)
				{
					Me.ready = false;
				}
			}
		}

		private void SetMatchInfo(string[] p)
		{
			KEY key = (KEY)Int(p[2], 0, (int)KEY.Count - 1);
			BaseMusicBundle b = BaseOf(CleanRoot(p[1]), key);
			matchRoot = CleanRoot(p[1]);
			matchKey = key;
			matchTitle = b?.title ?? matchRoot;
			matchArtist = b?.artist ?? "";
			matchJacket = b?.Album;
			matchKind = matchType;
			matchJudge = Int(p[5], -1, 1);
			matchMode = (MODE)Int(p[4], 0, (int)MODE.COUNT - 1);
			roomRound++;
			bool forced = p[3] == "1";
			foreach (Player pl in MP)
			{
				pl.matchDiff = forced ? MatchDiffText((int)matchMode, matchJudge == 1) : MatchDiffText(pl.diffMode, pl.plus);
			}
		}

		// 곡 선택 화면이 준비되면 실제로 곡 시작
		private void ExecutePlay()
		{
			string[] p = pendingPlay.Split('|');
			pendingPlay = null;
			KEY key = (KEY)Int(p[2], 0, (int)KEY.Count - 1);
			BaseMusicBundle bundle = BaseOf(CleanRoot(p[1]), key);
			if (bundle == null)
			{
				// 내 게임에 없는 곡 (래더 후보 등): 이번 곡은 0점 처리
				Status("이 곡이 내 게임에 없어서 이번 곡은 0점이에요: " + p[1], Ui.Red, 8f);
				lastFin = "FIN|" + myId + "|0|0.00|0|0|0|0|0|0|0|0";
				Send(lastFin);
				ApplyFinal(lastFin.Split('|'));
				Send("ROOM|" + myId);
				return;
			}
			bool forced = p[3] == "1";
			int judge = Int(p[5], -1, 1);
			GameValue.key = key;
			MODE mode = forced ? (MODE)Int(p[4], 0, (int)MODE.COUNT - 1) : MyModeFor(bundle);
			bundle.ChangeMode(key, mode);
			// 판정: 대전은 정해진 판정(하드 = 플러스), 그 외는 각자 설정. 오토 플레이는 온라인에서 끔
			Closet closet = BundlePackage.closet;
			PLAYMODE pm = (PLAYMODE)fPlayMode.GetValue(closet);
			PLAYMODE want = (judge == 1) ? PLAYMODE.PLUS : ((judge == 0 || pm == PLAYMODE.AUTO) ? PLAYMODE.NORMAL : pm);
			if (want != pm)
			{
				savedPlayMode = pm;
				fPlayMode.SetValue(closet, want);
			}
			// 온라인 플레이는 퀘스트에 세지 않음: 게임 규칙상 "즐겨찾기 카테고리" 플레이는 퀘스트 카운트 제외
			if (savedCategory == null && GameValue.lastPlayed != null)
			{
				savedCategory = GameValue.lastPlayed.category;
				GameValue.lastPlayed.category = FREESTYLE_CATEGORY.FAVORITY;
			}
			fIsPlayGame.SetValue(lister, true);
			// 로딩 화면 썸네일이 다른 곡(게임 목록에서 고른 곡/랜덤)으로 뜨지 않게
			GameValue.currentBundle = bundle;
			GameValue.isPlayRandomMusic = false;
			GameValue.isPlayHiddenMusic = false;
			loadingBundle = bundle;
			StartMatch(bundle);
			CInput.CloseWindow(base.gameObject);
			focused = false;
			mPlayMusic.Invoke(lister, new object[1] { bundle });
			// 게임의 곡 시작(OnPlayMusic)처럼 화면 전환 그림을 이 곡 자켓으로. 안 하면 예전 곡 그림이 뜬다
			if (fPlayBackImage.GetValue(lister) is Image pbi)
			{
				pbi.sprite = bundle.Album;
				pbi.gameObject.SetActive(value: true);
			}
		}

		private static readonly FieldInfo fPlayBackImage = typeof(MusicSelectRoot).GetField("playBackImage", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

		private BaseMusicBundle loadingBundle;

		private static readonly FieldInfo fLoadBg = typeof(LoadingPanel).GetField("backgroundImage", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

		private static readonly FieldInfo fLoadTitle = typeof(LoadingPanel).GetField("titleText", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

		private static readonly FieldInfo fLoadArtist = typeof(LoadingPanel).GetField("artistText", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

		// 곡 로딩 화면: 대전 곡 자켓·제목으로 고정 (게임에 들어가면 끝)
		private void FixLoadingPanel(SCENE scene)
		{
			if (loadingBundle == null)
			{
				return;
			}
			if (!inMatch || (scene == SCENE.Game && Generator.Instance != null && Singleton<Music>.Instance != null && Singleton<Music>.Instance.isPlaying))
			{
				loadingBundle = null;
				return;
			}
			try
			{
				foreach (LoadingPanel lp in FindObjectsByType<LoadingPanel>(FindObjectsSortMode.None))
				{
					if (fLoadBg?.GetValue(lp) is Image img && img.sprite != loadingBundle.Album && loadingBundle.Album != null)
					{
						img.sprite = loadingBundle.Album;
					}
					if (fLoadTitle?.GetValue(lp) is TMP_Text t && t.text != loadingBundle.title)
					{
						t.text = loadingBundle.title;
					}
					if (fLoadArtist?.GetValue(lp) is TMP_Text a && a.text != loadingBundle.artist)
					{
						a.text = loadingBundle.artist;
					}
				}
			}
			catch (Exception)
			{
			}
		}

		private void StartMatch(BaseMusicBundle b)
		{
			inMatch = true;
			sentFinal = false;
			paused = false;
			gameOver = false;
			leaving = false;
			heldGen = null;
			lastFin = null;
			historySaved = false;
			Player me = Me;
			if (me != null)
			{
				me.diffMode = (int)b.ApplyMode;
				me.diffKey = (int)GameValue.key;
				me.plus = BundlePackage.closet.isPlusMode;
				me.matchDiff = MatchDiffText(me.diffMode, me.plus);
				string df = "DF|" + myId + "|" + me.diffMode + "|" + me.diffKey + "|" + (me.plus ? 1 : 0) + "|" + Mathf.RoundToInt(BundlePackage.setting.NoteSpeed * 10f);
				sentDiff = "";
				Send(df);
				me.inRoom = false;
			}
			SetScr((matchType == MT_PLACE) ? Scr.Place : Scr.Room);
		}

		// "왁굳+ Lv.13" (이번 곡 기준)
		private string MatchDiffText(int mode, bool plus)
		{
			if (mode < 0)
			{
				return "관전";
			}
			float lv = LevelOf(BaseOf(matchRoot, matchKey), matchKey, mode);
			return ModeName(mode) + (plus ? "+" : "") + ((lv > 0f) ? (" Lv." + Mathf.FloorToInt(lv)) : "");
		}

		// 곡이 끝나 방으로 돌아옴
		private void BackToRoom()
		{
			Player me = Me;
			if (me != null)
			{
				me.inRoom = true;
			}
			if (isHost)
			{
				BroadcastRoster();
			}
			else
			{
				net?.server?.Send("ROOM|" + myId);
			}
			sentDiff = "";
			SetScr((matchType == MT_PLACE) ? Scr.Place : (connected ? ((bat.phase != BAT_NONE) ? Scr.Battle : Scr.Room) : Scr.Lobby));
		}

		private void RestoreAfterMatch()
		{
			if (savedPlayMode.HasValue)
			{
				fPlayMode.SetValue(BundlePackage.closet, savedPlayMode.Value);
				savedPlayMode = null;
			}
			if (savedCategory.HasValue && GameValue.lastPlayed != null)
			{
				GameValue.lastPlayed.category = savedCategory.Value;
				GameValue.Save();
			}
			savedCategory = null;
		}

		// 방장: 참가자 로딩 집계 → 모두 되면 출발
		private void SetLoaded(int id)
		{
			Player pl = Find(id);
			if (pl != null)
			{
				pl.loaded = true;
			}
			List<Player> mp = MP;
			loadedCount = mp.Count((Player p) => p.loaded);
			// 끊긴 사람, 곡에 안 들어오고 방으로 돌아온 사람은 기다리지 않는다
			totalCount = mp.Count((Player p) => p.loaded || (!p.offline && !p.inRoom));
			net?.Broadcast("LD|" + loadedCount + "|" + totalCount + "|" + string.Join(",", mp.Where((Player p) => p.loaded).Select((Player p) => p.id)));
			if (!released && loadedCount >= totalCount)
			{
				net?.Broadcast("GO");
				released = true;
			}
		}

		// 방장: 진행 끝 판정 / 로딩 시간 초과
		private void MatchTick()
		{
			// 모두 끝나면(또는 끊기면) 결과 저장
			List<Player> mp = MP;
			if (!resultTaken && mp.Count > 0 && (lastFin != null || !inMatch) && mp.All((Player p) => p.done || p.offline) && matchTitle.Length > 0)
			{
				TakeResult();
			}
			if (!isHost || !matchLive)
			{
				return;
			}
			if (!released && Time.unscaledTime - liveSince > HOLD_TIMEOUT + 10f)
			{
				net?.Broadcast("GO");
				released = true;
			}
			if (mp.All((Player p) => p.offline || (p.done && p.inRoom)) || mp.Count == 0)
			{
				matchLive = false;
				BroadcastRoster();
			}
		}

		private void TakeResult()
		{
			resultTaken = true;
			lastMatch = new MatchResult
			{
				title = matchTitle,
				artist = matchArtist,
				root = matchRoot,
				jacket = matchJacket,
				key = matchKey,
				kind = matchKind,
				round = (bat.round > 0 && matchKind >= MT_VERSUS) ? bat.round : roomRound,
				judge = matchJudge,
				ps = MP.Select((Player p) => p.Copy()).ToList()
			};
			if (lastFin != null && matchKind != MT_PLACE)
			{
				SaveHistory(lastMatch);
			}
		}

		// ---------------- 위치(상태) 공유 ----------------
		private const int PH_ROOM = 0;

		private const int PH_LOADING = 1;

		private const int PH_PLAYING = 2;

		private const int PH_GAME_RESULT = 3;

		private const int PH_MAX = 3;

		private int sentPhase = -1;

		private int MyPhase()
		{
			SCENE scene = SceneLoader.Current;
			if (scene == SCENE.Game)
			{
				return released ? PH_PLAYING : PH_LOADING;
			}
			if (scene == SCENE.Result)
			{
				return PH_GAME_RESULT;
			}
			return inMatch ? PH_LOADING : PH_ROOM;
		}

		private void SyncPhase()
		{
			Player me = Me;
			if (me == null || !connected)
			{
				sentPhase = -1;
				return;
			}
			int ph = MyPhase();
			me.phase = ph;
			if (ph == sentPhase)
			{
				return;
			}
			sentPhase = ph;
			if (isHost)
			{
				BroadcastRoster();
			}
			else
			{
				net?.server?.Send("PH|" + myId + "|" + ph);
			}
		}

		private string PhaseText(Player p)
		{
			if (p.offline)
			{
				return "연결 끊김";
			}
			switch (p.phase)
			{
			case PH_LOADING:
				return "로딩 중";
			case PH_PLAYING:
				return p.done ? (p.cleared ? "CLEAR" : "FAILED") : "PLAYING";
			case PH_GAME_RESULT:
				return "결과 확인 중";
			default:
				return "";
			}
		}

		// ---------------- 점수 ----------------
		private void ApplyScore(string[] p)
		{
			Player pl = (p.Length >= 6) ? Find(Int(p[1], -1, int.MaxValue)) : null;
			if (pl != null && !pl.done)
			{
				pl.score = Int(p[2], 0, Judgement.MAX_SCORE);
				pl.combo = Int(p[3], 0, Judgement.MAX_COMBO);
				pl.rate = Num(p[4], 0f, 100f);
				pl.hp = Num(p[5], 0f, 1f);
				pl.brk = (p.Length > 6) ? Int(p[6], 0, Judgement.MAX_COMBO) : pl.brk;
			}
		}

		private void ApplyFinal(string[] p)
		{
			Player pl = (p.Length >= 12) ? Find(Int(p[1], -1, int.MaxValue)) : null;
			if (pl == null)
			{
				return;
			}
			pl.score = Int(p[2], 0, Judgement.MAX_SCORE);
			pl.rate = Num(p[3], 0f, 100f);
			pl.cleared = p[4] == "1";
			pl.fullCombo = p[5] == "1";
			pl.perfect = Int(p[6], 0, Judgement.MAX_COMBO);
			pl.great = Int(p[7], 0, Judgement.MAX_COMBO);
			pl.good = Int(p[8], 0, Judgement.MAX_COMBO);
			pl.bad = Int(p[9], 0, Judgement.MAX_COMBO);
			pl.miss = Int(p[10], 0, Judgement.MAX_COMBO);
			pl.maxCombo = Int(p[11], 0, Judgement.MAX_COMBO);
			pl.brk = pl.miss;
			pl.done = true;
		}

		// ---------------- 게임 중 ----------------
		private void UpdateGame()
		{
			Generator gen = Generator.Instance;
			Music music = Singleton<Music>.Instance;
			if (gen == null || music == null)
			{
				return;
			}
			if (heldGen == null && !released)
			{
				heldGen = gen;
				holdStart = Time.time;
				Judgement j = Singleton<Judgement>.Instance;
				if (j != null)
				{
					j.onEndJudge += OnEndJudge;
				}
			}
			// 동시 출발: 모두 로딩될 때까지 시작 카운트다운을 3초에서 멈춤
			if (!released && gen == heldGen)
			{
				if (music.startDelayTime < HOLD_DELAY)
				{
					music.startDelayTime = HOLD_DELAY;
					Player me = Me;
					if (me != null && !me.loaded)
					{
						me.loaded = true;
						if (isHost)
						{
							SetLoaded(myId);
						}
						else
						{
							net?.server?.Send("LOADED|" + myId);
						}
					}
				}
				if (!connected || Time.time - holdStart > HOLD_TIMEOUT)
				{
					if (isHost)
					{
						net?.Broadcast("GO");
					}
					released = true;
				}
			}
			GameKeys(gen);
			if (!sentFinal && (scoreTimer -= Time.unscaledDeltaTime) <= 0f)
			{
				scoreTimer = 0.15f;
				Record r = GetRecord();
				if (r != null)
				{
					string sc = "SC|" + myId + "|" + r.noteScore.gameScore + "|" + r.Combo + "|" + r.noteScore.rate.ToString("0.00", IC) + "|" + GetHp().ToString("0.000", IC) + "|" + r.noteScore.miss;
					// 변화 없으면(쉬는 구간) 보내지 않음. 연결 확인용으로 1초에 한 번은 보냄
					if (sc != lastSc || Time.unscaledTime - lastScAt > 1f)
					{
						lastSc = sc;
						lastScAt = Time.unscaledTime;
						ApplyScore(sc.Split('|'));
						Send(sc);
					}
				}
			}
		}

		// 온라인 곡 진행 중 조작: 게임 원래 키(F5 재시작, ESC 옵션, 1/2 속도)는 막고 여기서 대신 처리한다.
		// 판정 키는 게임이 Input 을 직접 읽으므로 영향 없음.
		private void GameKeys(Generator gen)
		{
			if (!gameOver && released && GetHp() <= 0f)
			{
				gameOver = true;
				paused = false;
				if (!sentFinal)
				{
					SendFinal(cleared: false, fc: false);
				}
			}
			if (gameOver)
			{
				GameOver go = FindAnyObjectByType<GameOver>();
				if (go != null && fMenuWindow.GetValue(go) is GameObject menu && menu.activeSelf)
				{
					menu.SetActive(value: false);
				}
				if (Enter())
				{
					PlaySe(AudioManager.SE.Confirm);
					LeaveGame(gen, quit: false);
				}
				return;
			}
			if (Key(KeyCode.Escape))
			{
				PlaySe(AudioManager.SE.Confirm);
				if (paused)
				{
					Resume(gen);
				}
				else
				{
					paused = true;
					pauseSel = 0;
					gen.OnOpenOption();
				}
				return;
			}
			if (paused)
			{
				if (Key(KeyCode.UpArrow) || Key(KeyCode.DownArrow))
				{
					pauseSel = 1 - pauseSel;
					PlaySe(AudioManager.SE.SlideMove);
				}
				else if (Enter())
				{
					PlaySe(AudioManager.SE.Confirm);
					if (pauseSel == 0)
					{
						Resume(gen);
					}
					else
					{
						LeaveGame(gen, quit: true);
					}
				}
				return;
			}
			int speed = (Key(KeyCode.Alpha1) ? (-1) : 0) + (Key(KeyCode.Alpha2) ? 1 : 0);
			if (speed != 0)
			{
				if ((bool)fLockSpeed.GetValue(gen))
				{
					PlaySe(AudioManager.SE.Locked);
				}
				else
				{
					BundlePackage.setting.NoteSpeed = Mathf.Clamp(BundlePackage.setting.NoteSpeed + 0.1f * (float)speed, Setting.MIN_USER_SPEED, 8f);
					mApplySpeed.Invoke(gen, null);
					PlaySe(AudioManager.SE.SlideMove);
				}
			}
		}

		private void Resume(Generator gen)
		{
			paused = false;
			gen.OnCloseOption("");
		}

		private void LeaveGame(Generator gen, bool quit)
		{
			paused = false;
			if (!sentFinal)
			{
				SendFinal(cleared: false, fc: false);
			}
			leaving = true;
			if (quit)
			{
				gen.OnCloseOption("goMain");
				return;
			}
			Singleton<Fader>.Instance.FadeOut(delegate
			{
				SceneLoader.LoadScene(SCENE.FreeStyle);
			});
		}

		private void LateUpdate()
		{
			// 게임 원래 키 입력 차단 (CInput 은 inputDelay 동안 키를 무시함)
			if (SceneLoader.Current == SCENE.Game && inMatch && !leaving)
			{
				fInputDelay.SetValue(null, Time.time + 0.5f);
			}
		}

		private float GetHp()
		{
			if (Generator.Instance is NoteGenerator ng)
			{
				float max = (float)fMaxHp.GetValue(ng);
				if (max > 0f)
				{
					return Mathf.Clamp01((float)fHp.GetValue(ng) / max);
				}
			}
			return 1f;
		}

		private void OnEndJudge()
		{
			if (inMatch && !sentFinal)
			{
				Record r = GetRecord();
				SendFinal(cleared: true, r != null && r.noteScore.miss <= 0);
			}
		}

		private Record GetRecord()
		{
			Judgement j = Singleton<Judgement>.Instance;
			return (j == null) ? null : (fRecord.GetValue(j) as Record);
		}

		private void SendFinal(bool cleared, bool fc)
		{
			sentFinal = true;
			Record r = GetRecord();
			RecordNote n = (r != null) ? r.noteScore : default(RecordNote);
			lastFin = "FIN|" + myId + "|" + n.gameScore + "|" + n.rate.ToString("0.00", IC) + "|" + (cleared ? 1 : 0) + "|" + (fc ? 1 : 0) + "|" + n.perfect + "|" + n.great + "|" + n.good + "|" + n.bad + "|" + n.miss + "|" + n.highCombo;
			ApplyFinal(lastFin.Split('|'));
			Send(lastFin);
		}

		// ---------------- 팀 ----------------
		private static readonly Color[] TeamColors = new Color[3]
		{
			new Color32(byte.MaxValue, 84, 104, byte.MaxValue),
			new Color32(70, 150, byte.MaxValue, byte.MaxValue),
			new Color32(byte.MaxValue, 196, 50, byte.MaxValue)
		};

		private static Color TeamColor(int t)
		{
			return TeamColors[Mathf.Clamp(t, 0, 2)];
		}

		private static string TeamName(int t)
		{
			return "TEAM " + (char)('A' + Mathf.Clamp(t, 0, 2));
		}

		// 팀 점수 = 팀원 점수 평균 (디맥 팀 매치)
		private static int TeamScore(List<Player> ps, int t)
		{
			List<Player> m = ps.Where((Player p) => p.team == t).ToList();
			return (m.Count == 0) ? (-1) : (int)m.Average((Player p) => p.score);
		}

		// 이긴 팀 (-1 = 무승부)
		private static int TeamWinner(List<Player> ps)
		{
			int[] s = new int[3]
			{
				TeamScore(ps, 0),
				TeamScore(ps, 1),
				TeamScore(ps, 2)
			};
			int best = s.Max();
			return (s.Count((int x) => x == best) > 1) ? (-1) : Array.IndexOf(s, best);
		}

		// 디맥 오픈 매치 결과처럼 TOP SCORE / TOP RATE / TOP COMBO
		private static string TopBadges(List<Player> ps, Player p)
		{
			List<Player> mp = ps.Where((Player o) => o.done).ToList();
			if (mp.Count < 2 || !p.done)
			{
				return "";
			}
			List<string> t = new List<string>();
			if (p.score > 0 && p.score == mp.Max((Player o) => o.score))
			{
				t.Add("TOP SCORE");
			}
			if (p.rate > 0f && p.rate >= mp.Max((Player o) => o.rate))
			{
				t.Add("TOP RATE");
			}
			if (p.maxCombo > 0 && p.maxCombo == mp.Max((Player o) => o.maxCombo))
			{
				t.Add("TOP COMBO");
			}
			return string.Join("  ", t);
		}

		// ---------------- 나감 알림 (곡 진행 중 위쪽) ----------------
		private string leftNotice = "";

		private float leftUntil;

		private void NoticeLeft(string name)
		{
			if (inMatch)
			{
				leftNotice = name + " 님이 나갔어요";
				leftUntil = Time.unscaledTime + 6f;
			}
		}
	}
}
