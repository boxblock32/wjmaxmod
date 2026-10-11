// 아카데미 (타이틀 ACADEMY 메뉴). 게임에 화면(6_Academy)은 있지만 메뉴를 움직이는 스크립트가 빠져 있어서 모드가 대신 움직인다.
//   수업 [튜토리얼] : 게임의 첫 실행 튜토리얼(싱크 룸)을 다시 보기
//   시험 [배치고사] : 게임의 원래 배치고사 (아래 설명)
//   도서관 [4컷 만화] : 게임에 만화가 없어서 게임 그대로 잠금
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using WJMAX.UserRecord;

namespace WJMAXOnline
{
	public partial class OnlineMod
	{
		private const int MT_PLACE = 4;

		// ---------------- 아카데미 메뉴 ----------------
		private static readonly FieldInfo fTutorialList = typeof(UserRecord).GetField("userTutorialList", F);

		private static readonly FieldInfo fButtonSubmit = typeof(WJButton).GetField("onSubmit", F);

		private AcademyMain academy;

		private bool placePending; // 아카데미에서 배치고사를 골라 프리스타일 씬으로 가는 중

		// 메뉴 이동·강조는 게임 메뉴(WJMenu)가 그대로 한다. 모드는 씬이 열릴 때 화면을 밝히고(게임에 이 처리가 없어서 검은 화면),
		// 수업·시험 버튼의 ENTER 동작만 바꾼다 (게임 원래 동작: 수업은 아무것도 안 함, 시험은 없는 배치고사 씬을 열다 멈춤)
		private void AcademyTick()
		{
			if (academy != null)
			{
				return;
			}
			academy = FindAnyObjectByType<AcademyMain>();
			if (academy == null)
			{
				return;
			}
			AcaSceneButton[] btns = academy.GetComponentsInChildren<AcaSceneButton>(includeInactive: true).OrderBy((AcaSceneButton b) => b.transform.GetSiblingIndex()).ToArray();
			for (int i = 0; i < btns.Length && i < 2; i++)
			{
				int idx = i;
				UnityEngine.Events.UnityEvent ev = new UnityEngine.Events.UnityEvent();
				ev.AddListener(delegate
				{
					AcademySubmit(idx);
				});
				fButtonSubmit.SetValue(btns[i], ev);
				// 수업 버튼은 게임에서 잠금 표시라 ENTER 가 안 먹음 → 잠금 해제
				SV(btns[i], "isLockButton", false);
				(FV(btns[i], "lockObject") as GameObject)?.SetActive(false);
			}
			Singleton<Fader>.Instance.FadeIn();
		}

		private void AcademySubmit(int i)
		{
			global::Popup pop = Singleton<global::Popup>.Instance;
			switch (i)
			{
			case 0:
				PlaySe(AudioManager.SE.Confirm);
				pop?.Show("수업", "튜토리얼을 다시 볼까요?\n처음 게임을 켰을 때 나온 연습 곡이에요.", "예", "아니요", delegate(int r)
				{
					if (r == 0)
					{
						StartTutorial();
					}
				});
				break;
			default:
				PlaySe(AudioManager.SE.Confirm);
				placePending = true;
				GameValue.gameMode = GAMEMODE.FreeStyle;
				Singleton<Fader>.Instance.FadeOut(delegate
				{
					SceneLoader.LoadScene(SCENE.FreeStyle);
				});
				break;
			}
		}

		// 게임은 "첫 실행 튜토리얼을 봤다" 표시가 없을 때만 싱크 룸에서 튜토리얼을 튼다.
		// 표시를 잠깐 지우고 싱크 룸을 열면, 싱크 룸이 시작하면서 표시를 다시 넣고 저장한다 (끝나면 아카데미로 돌아옴)
		private void StartTutorial()
		{
			try
			{
				(fTutorialList?.GetValue(BundlePackage.user) as List<TUTORIAL>)?.Remove(TUTORIAL.FIRST_PLAY);
			}
			catch (Exception e)
			{
				Debug.LogWarning("[WJMAXOnline] tutorial reset failed: " + e.Message);
			}
			Singleton<Fader>.Instance.FadeOut(delegate
			{
				SceneLoader.LoadScene(SCENE.SyncRoom);
			});
		}

		// ---------------- 배치고사 (게임에 남아 있는 원래 데이터·규칙을 그대로 씀) ----------------
		// 원래 게임: 배치고사 전용 씬(시험장)에서 12곡을 차례로 쳐서 시즌 티어를 받는다. 씬 파일과 서버가 빠져 있어서
		// 시험장 화면만 모드가 그리고, 나머지는 게임 코드 그대로:
		//   PlacementData(진행·규칙) : Resources/Etc/PlacemenetBundle 의 36곡 중 단계마다 3곡 중 1곡(4·5·6·8·9·10·12·13·14·16·18·19렙),
		//                             점수 = 12곡 정확도 평균 × 100 (80% 미만은 80%), 재시도 3회, 게임 오버 시 재시도 차감 / 없으면 시험 끝
		//   결과 화면(ResultPlacement) : 정확도 기록 · 재시도 버튼 · 예상 티어 (확인 버튼만 시험장으로 돌아오게 바꿈)
		//   끝나면 프로필 배치 기록 저장 · 업적(USAR0051, 플래티넘 이상 USAR0052) · 보상(ITGE0508) - 원래 마지막 화면과 같은 순서
		// 진행은 서버 대신 OnlineMod\placement.txt 에 저장 (시험장을 나갔다 와도, 게임을 껐다 켜도 이어서)
		private const int PLACE_SEASON = 1;

		private const int ST_GAMEOVER = 4, ST_END = 5, ST_FINISHED = 6; // PLACEMENT_USER_STATE

		private static string PlacePath => System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Application.dataPath), "OnlineMod", "placement.txt");

		private PlacementData pd;

		private bool placeActive; // 시험장에 있음 (곡 진행 중 포함)

		private bool plPlaying; // 곡을 시작함 (돌아올 때까지)

		private bool plGotResult; // 이번 곡이 결과 화면을 거쳤음 (= 정확도 기록됨)

		private int plRetryAt; // 곡 시작 때 재시도 수 (게임 오버 화면이 이미 깎았는지 보려고)

		private int plPhase; // 1 시험장 / 2 최종 결과 / 3 응시 완료(이미 친 시즌)

		private int plSel; // 목록에서 보고 있는 단계

		private float plAt;

		private bool plRewarded;

		private bool plQuit; // 시험장 나가기 확인

		private string plFlash = "";

		private float plFlashAt;

		private PlaceRecord plDone; // 이미 친 시즌 기록

		private static FieldInfo FieldOf(Type t, string n)
		{
			for (; t != null; t = t.BaseType)
			{
				FieldInfo f = t.GetField(n, F | BindingFlags.DeclaredOnly);
				if (f != null)
				{
					return f;
				}
			}
			return null;
		}

		private static object FV(object o, string n)
		{
			return (o == null) ? null : FieldOf(o.GetType(), n)?.GetValue(o);
		}

		private static void SV(object o, string n, object v)
		{
			if (o != null)
			{
				FieldOf(o.GetType(), n)?.SetValue(o, v);
			}
		}

		private Array PlStages => FV(pd, "stages") as Array;

		private int PlIndex => (int)FV(pd, "stageIndex");

		private int PlState => Convert.ToInt32(FV(pd, "state"));

		private int PlTier => Convert.ToInt32(FV(pd, "tier"));

		private object PlBundle => FV(pd, "bundle");

		// 원래 티어 표: 0 UNKNOWN, IRON III(8000) … LEGENO(10000)
		private object TierInfoOf(int tier)
		{
			foreach (object t in (FV(PlBundle, "tierInfos") as Array) ?? Array.Empty<object>())
			{
				if (Convert.ToInt32(FV(t, "tier")) == tier)
				{
					return t;
				}
			}
			return null;
		}

		private void EnterPlacement()
		{
			Disconnect();
			players.Clear();
			placeActive = true;
			plQuit = false;
			plBoard = false;
			plFlash = "";
			plRewarded = false;
			GameValue.key = KEY.Key4;
			// 원래 시험장처럼: 4B · 일반 판정 · 패턴 섞기 없음 (나갈 때 옷장 설정을 다시 불러옴)
			BundlePackage.closet.SetPlacement();
			pd = Singleton<PlacementData>.Instance;
			if (pd == null)
			{
				GameObject go = new GameObject("PlacementData");
				pd = go.AddComponent<PlacementData>();
				LoadPlaceProgress();
			}
			// 이미 이번 시즌을 친 사람: 결과만 보여 줌 (원래 게임도 시즌당 한 번)
			plDone = null;
			try
			{
				PlaceRecord r = BundlePackage.user.GetPlacementRecord(PLACE_SEASON);
				if (r != null && (int)r.tier != 0)
				{
					plDone = r;
				}
			}
			catch (Exception)
			{
			}
			if (plDone != null)
			{
				plPhase = 3;
				plAt = Time.unscaledTime;
				SetScr(Scr.Place);
				return;
			}
			// 시험 곡이 다 있는지 (원래: "시험에 필요한 음악이 존재하지 않습니다")
			foreach (object st in PlStages)
			{
				if (BaseOf((string)FV(st, "rootFileName"), KEY.Key4) == null)
				{
					plPhase = 1;
					SetScr(Scr.Place);
					Status("시험장 접근 불가: 시험에 필요한 음악(" + FV(st, "title") + ")이 없어요. 타이틀-옵션-유효성 검사를 해 주세요", Ui.Red, 10f);
					return;
				}
			}
			if (PlIndex < 0)
			{
				pd.NextStage();
			}
			else
			{
				pd.UpdateBundle();
			}
			plSel = PlIndex;
			plPhase = (PlState == ST_FINISHED || PlState == ST_END) ? 2 : 1;
			Flash("시험장 입장 허가!");
			SetScr(Scr.Place);
		}

		private void Flash(string s)
		{
			plFlash = s;
			plFlashAt = Time.unscaledTime;
		}

		private void LeavePlacement()
		{
			SavePlaceProgress();
			placeActive = false;
			plPlaying = false;
			online = false;
			SetScr(Scr.None);
			try
			{
				BundlePackage.closet.load?.Invoke();
			}
			catch (Exception)
			{
			}
			GameValue.gameMode = GAMEMODE.Academy;
			Singleton<Fader>.Instance.FadeOut(delegate
			{
				SceneLoader.LoadScene(SCENE.Academy);
			});
		}

		// 지금 단계 곡 시작 (원래 시험장 Update 의 ENTER 와 같음: 게임 곡 선택 화면의 PlayMusic)
		private void PlaceStart()
		{
			BaseMusicBundle b = FV(pd, "currentBundle") as BaseMusicBundle;
			if (b == null || lister == null || !ListerReady)
			{
				return;
			}
			GameValue.key = KEY.Key4;
			GameValue.gameMode = GAMEMODE.Placement;
			GameValue.currentBundle = b;
			GameValue.isPlayRandomMusic = false;
			GameValue.isPlayHiddenMusic = false;
			plPlaying = true;
			plGotResult = false;
			plRetryAt = pd.RetryCount;
			fIsPlayGame.SetValue(lister, true);
			if (fPlayBackImage.GetValue(lister) is Image pbi)
			{
				pbi.sprite = b.Album;
				pbi.gameObject.SetActive(value: true);
			}
			CInput.CloseWindow(base.gameObject);
			focused = false;
			PlaySe(AudioManager.SE.PlayMusic);
			mPlayMusic.Invoke(lister, new object[1] { b });
		}

		// 씬이 바뀔 때 (Core.OnSceneChanged 에서 부름)
		private void PlaceSceneChanged(SCENE from, SCENE to)
		{
			if (!placeActive || !plPlaying)
			{
				return;
			}
			if (to == SCENE.Game)
			{
				// 결과 화면의 재시도로 다시 치는 경우도 새 곡처럼 (재시도는 그때 이미 깎임)
				plGotResult = false;
				plRetryAt = pd.RetryCount;
			}
			else if (to == SCENE.Result)
			{
				plGotResult = true;
			}
			else if (to == SCENE.FreeStyle)
			{
				plPlaying = false;
				GameValue.gameMode = GAMEMODE.FreeStyle;
				PlaceAfterSong();
			}
		}

		private void PlaceAfterSong()
		{
			if (plGotResult)
			{
				// 결과 화면(ResultPlacement)이 이번 단계 정확도를 기록했음
				if (pd.IsLastStage)
				{
					FinishPlacement();
				}
				else
				{
					pd.NextStage();
					plSel = PlIndex;
					Flash("NEXT MUSIC");
				}
			}
			else
			{
				// 결과 없이 돌아옴 (중도 포기 · 게임 오버): 원래 규칙대로 재시도 1회 차감, 남은 재시도가 없으면 시험 끝.
				// 게임 오버 화면은 게임이 직접 PlacementData.GameOver 를 부르므로 그때는 또 깎지 않음
				if (pd.RetryCount == plRetryAt && PlState != ST_END)
				{
					pd.GameOver();
				}
				if (PlState == ST_END)
				{
					FinishPlacement();
				}
				else
				{
					pd.UpdateBundle();
					Flash("재시도 -1");
					Status("문이 닫혀있습니다. 시험 도중 나갈 수 없습니다. 끝까지 화이팅!", Ui.Gold, 6f);
				}
			}
			SavePlaceProgress();
		}

		private void FinishPlacement()
		{
			SV(pd, "state", Enum.ToObject(FV(pd, "state").GetType(), ST_FINISHED));
			SV(pd, "point", (float)pd.FinalPoint);
			pd.UpdateTier();
			plPhase = 2;
			plAt = Time.unscaledTime;
			plRewarded = false;
			PlaySe(AudioManager.SE.Confirm);
#if !DEV
			// 원래 마지막 화면처럼 프로필에 이번 시즌 배치 기록 (게임이 프로필 티어로 보여 줌)
			try
			{
				UserRecord u = BundlePackage.user;
				if (u.GetPlacementRecord(PLACE_SEASON) == null)
				{
					u.StartPlacement(PLACE_SEASON);
				}
				PlaceRecord r = u.LastPlaceRecord;
				r.season = PLACE_SEASON;
				r.tier = (TIER)PlTier;
				r.key = KEY.Key4;
				r.point = pd.FinalPoint;
				u.save?.Invoke();
			}
			catch (Exception e)
			{
				Debug.LogWarning("[WJMAXOnline] placement record failed: " + e.Message);
			}
#endif
			try
			{
				System.IO.File.Delete(PlacePath);
			}
			catch (Exception)
			{
			}
		}

		private void PlaceTick()
		{
			if (!placeActive)
			{
				return;
			}
			SCENE scene = SceneLoader.Current;
			// 결과 화면 확인 · 게임 오버 · ESC 는 원래 시험장 씬(빌드에 없음)을 열다 멈춤 → 시험장 화면(프리스타일)으로
			if (scene == SCENE.Placement)
			{
				SceneLoader.LoadScene(SCENE.FreeStyle);
				return;
			}
			if (scr != Scr.Place || scene != SCENE.FreeStyle)
			{
				return;
			}
			// 최종 결과: 원래처럼 잠시 뒤 업적 · 보상
			if (plPhase == 2 && !plRewarded && Time.unscaledTime - plAt > 3.2f)
			{
				plRewarded = true;
#if !DEV
				try
				{
					Singleton<Archivement>.Instance?.OnCheck("USAR0051");
					if (PlTier >= 13)
					{
						Singleton<Archivement>.Instance?.OnCheck("USAR0052");
					}
					ItemPopup ip = Singleton<ItemPopup>.Instance;
					if (ip != null)
					{
						ip.Regested("ITGE0508", "배치고사 시즌1 플레이 보상");
						ip.StartShowing();
					}
				}
				catch (Exception e)
				{
					Debug.LogWarning("[WJMAXOnline] placement reward failed: " + e.Message);
				}
#endif
			}
		}

		// 배경 영상: 보고 있는 단계 곡 미리보기 (원래 시험장의 stageVideo · stageAudio). 잠긴 곡·결과 화면은 배치고사 영상
		private string PlaceBgUrl
		{
			get
			{
				BaseMusicBundle b = (plPhase == 1 && pd != null && PlaceUnlocked(plSel)) ? PlaceStageBundle(plSel) : null;
				return (b != null && !string.IsNullOrEmpty(b.previewUrl)) ? b.previewUrl : System.IO.Path.Combine(Paths.MAIN_VIDEO_PATH, "Placement.mp4");
			}
		}

		private BaseMusicBundle PlaceStageBundle(int i)
		{
			Array st = PlStages;
			if (st == null || i < 0 || i >= st.Length)
			{
				return null;
			}
			return BaseOf((string)FV(st.GetValue(i), "rootFileName"), KEY.Key4);
		}

		private bool PlaceUnlocked(int i)
		{
			return i <= PlIndex;
		}

		private void InputPlace()
		{
			if (plBoard)
			{
				InputBoard();
				return;
			}
			if (Key(KeyCode.Tab) && plPhase != 0)
			{
				OpenBoard();
				return;
			}
			if (plQuit)
			{
				if (Enter())
				{
					PlaySe(AudioManager.SE.Confirm);
					plQuit = false;
					LeavePlacement();
				}
				else if (Key(KeyCode.Escape))
				{
					plQuit = false;
					PlaySe(AudioManager.SE.SlideMove);
				}
				return;
			}
			if (plPhase == 2 || plPhase == 3)
			{
				if (Time.unscaledTime - plAt > 3.5f && (Enter() || Key(KeyCode.Escape)))
				{
					PlaySe(AudioManager.SE.Confirm);
					LeavePlacement();
				}
				return;
			}
			int n = PlStages?.Length ?? 0;
			if (Repeat(KeyCode.UpArrow) && plSel > 0)
			{
				plSel--;
				PlaySe(AudioManager.SE.SlideMove);
			}
			else if (Repeat(KeyCode.DownArrow) && plSel < n - 1)
			{
				plSel++;
				PlaySe(AudioManager.SE.SlideMove);
			}
			else if (Enter() && Time.unscaledTime - scrTime > 1f) // 아카데미에서 고른 ENTER 로 바로 시작하지 않게
			{
				plSel = PlIndex;
				PlaceStart();
			}
			else if (Key(KeyCode.Escape))
			{
				plQuit = true;
				PlaySe(AudioManager.SE.SlideMove);
			}
		}

		// ---------------- 진행 저장 (서버 대신) ----------------
		// 1줄: stageIndex|retryCount|state, 이후 12줄: rootFileName \t title \t mode \t level \t rate
		private void SavePlaceProgress()
		{
			if (pd == null || plPhase != 1)
			{
				return;
			}
			try
			{
				List<string> lines = new List<string> { PlIndex + "|" + pd.RetryCount + "|" + PlState };
				foreach (object st in PlStages)
				{
					lines.Add(FV(st, "rootFileName") + "\t" + FV(st, "title") + "\t" + Convert.ToInt32(FV(st, "mode")) + "\t" + FV(st, "level") + "\t" + ((float)FV(st, "rate")).ToString(IC));
				}
				System.IO.File.WriteAllLines(PlacePath, lines);
			}
			catch (Exception e)
			{
				Debug.LogWarning("[WJMAXOnline] placement save failed: " + e.Message);
			}
		}

		private void LoadPlaceProgress()
		{
			try
			{
				if (!System.IO.File.Exists(PlacePath))
				{
					return;
				}
				string[] lines = System.IO.File.ReadAllLines(PlacePath);
				Array st = PlStages;
				if (lines.Length != st.Length + 1)
				{
					return;
				}
				string[] h = lines[0].Split('|');
				for (int i = 0; i < st.Length; i++)
				{
					string[] f = lines[i + 1].Split('\t');
					object s = st.GetValue(i);
					SV(s, "rootFileName", f[0]);
					SV(s, "title", f[1]);
					SV(s, "mode", Enum.ToObject(FV(s, "mode").GetType(), Int(f[2], 0, 99)));
					SV(s, "level", Int(f[3], 0, 99));
					SV(s, "rate", Num(f[4], 0f, 100f));
					st.SetValue(s, i);
				}
				SV(pd, "stageIndex", Int(h[0], -1, st.Length - 1));
				SV(pd, "retryCount", Int(h[1], 0, 3));
				SV(pd, "state", Enum.ToObject(FV(pd, "state").GetType(), Int(h[2], 0, 6)));
				SV(pd, "point", (float)pd.FinalPoint);
				pd.UpdateTier();
			}
			catch (Exception e)
			{
				Debug.LogWarning("[WJMAXOnline] placement load failed: " + e.Message);
			}
		}

		// ---------------- 시험장 화면 ----------------
		private string PlaceSig()
		{
			return plPhase + "|" + plSel + "|" + (plBoard ? ("B" + plCat + ":" + plTop) : "") + "|" + (pd == null ? "" : (PlIndex + ":" + pd.RetryCount + ":" + pd.FinalPoint)) + "|" + plQuit + "|" + plFlash + ((Time.unscaledTime - plFlashAt < 1.8f) ? "1" : "0");
		}

		// 원래 티어 그림 (틀 + 아이콘)
		private static RectTransform TierArt(Transform p, string name, Vector2 pos, float size, object info)
		{
			RectTransform r = Ui.Node(p, name, pos, new Vector2(size, size));
			Sprite frame = FV(info, "frame") as Sprite;
			Sprite icon = FV(info, "sprite") as Sprite;
			if (frame != null)
			{
				Image f = Ui.FillImg(r, "frame", Color.white);
				f.sprite = frame;
				f.preserveAspect = true;
			}
			if (icon != null)
			{
				Image ic = Ui.FillImg(r, "icon", Color.white);
				ic.sprite = icon;
				ic.preserveAspect = true;
			}
			return r;
		}

		private void BuildPlace()
		{
			Ui.FillImg(body, "shade", new Color(0.02f, 0.02f, 0.05f, 0.45f));
			Logo("PLACEMENT TEST", "ACADEMY");
			Ui.In(Ui.VTxt(body, "season", new Vector2(-54f, 30f), new Vector2(400f, 40f), "SEASON " + PLACE_SEASON, 30f, Ui.Gold, TextAlignmentOptions.TopRight, Ui.TR, Ui.TR), Ui.At(0.05f), 30f);
			if (plBoard)
			{
				BuildBoard();
				return;
			}
			if (plPhase == 3)
			{
				PlaceFinal(plDone.point, (int)plDone.tier, done: true);
				Hotkeys("ENTER", "아카데미로", "TAB", "리더보드");
				return;
			}
			if (pd == null || PlStages == null)
			{
				return;
			}
			PlaceList();
			PlaceTierPanel();
			PlaceStageCard();
			if (plPhase == 2)
			{
				PlaceFinal(pd.FinalPoint, PlTier, done: false);
				Hotkeys("ENTER", "아카데미로", "TAB", "리더보드");
			}
			else if (plQuit)
			{
				ConfirmBox("시험장 나가기", "시험장을 나갑니까?\n<size=80%>(응시기간 동안 다시 입장할 수 있습니다.)</size>", "네");
			}
			else
			{
				Hotkeys("ENTER", "시험 시작", "↑↓", "시험 곡 보기", "TAB", "리더보드", "ESC", "시험장 나가기");
			}
			if (plFlash.Length > 0 && Time.unscaledTime - plFlashAt < 1.8f)
			{
				RectTransform fb = Ui.Node(body, "flash", new Vector2(0f, 470f), new Vector2(1920f, 120f));
				Ui.FillImg(fb, "bg", new Color(0f, 0f, 0f, 0.75f));
				Ui.In(Ui.VTxt(fb, "t", Vector2.zero, new Vector2(1920f, 120f), plFlash, 60f, Ui.Gold, TextAlignmentOptions.Midline), Ui.Seen("fl" + plFlashAt), 0f, 0f, 1.8f, 0.4f, back: true);
				Ui.In(fb, Ui.Seen("flb" + plFlashAt), -300f, 0f, 1f, 0.35f);
			}
		}

		// 왼쪽: 시험 곡 12개 (원래 PlaceMusicList: 친 곡 · 지금 곡 · 잠긴 곡)
		private void PlaceList()
		{
			const float W = 640f;
			const float RH = 66f;
			Array st = PlStages;
			int cur = PlIndex;
			RectTransform l = Ui.Node(body, "list", new Vector2(54f, 122f), new Vector2(W, 12f * RH + 20f));
			Ui.FillImg(l, "bg", Glass);
			Ui.In(l, Ui.At(0.05f), -70f, 0f, 1f, 0.6f);
			for (int i = 0; i < st.Length; i++)
			{
				object s = st.GetValue(i);
				bool done = i < cur || (i == cur && plPhase == 2);
				bool now = i == cur && plPhase != 2;
				bool lockd = i > cur;
				bool sel = i == plSel && plPhase == 1;
				RectTransform r = Ui.Node(l, "s" + i, new Vector2(10f, 10f + i * RH), new Vector2(W - 20f, RH - 6f));
				if (sel)
				{
					Ui.FillImg(r, "sel", Color.white, "Music_Choice_Effect");
				}
				else
				{
					Ui.FillImg(r, "bg", new Color(1f, 1f, 1f, now ? 0.1f : 0.04f));
				}
				if (now)
				{
					Ui.Frame(r, Ui.Gold, 2f);
				}
				Ui.VTxt(r, "no", new Vector2(12f, 0f), new Vector2(60f, RH - 6f), (i + 1).ToString("00"), 20f, now ? Ui.Gold : new Color(1f, 1f, 1f, 0.55f), TextAlignmentOptions.MidlineLeft);
				int mode = Convert.ToInt32(FV(s, "mode"));
				int lv = (int)FV(s, "level");
				if (lockd)
				{
					RectTransform lk = Ui.Node(r, "lk", new Vector2(66f, 4f), new Vector2(52f, 52f));
					Ui.FillImg(lk, "bg", new Color(1f, 1f, 1f, 0.06f));
					Ui.VTxt(lk, "q", Vector2.zero, new Vector2(52f, 52f), "?", 24f, new Color(1f, 1f, 1f, 0.3f), TextAlignmentOptions.Midline);
					Ui.Txt(r, "t", new Vector2(130f, 0f), new Vector2(280f, RH - 6f), "LOCKED", 16f, new Color(1f, 1f, 1f, 0.3f), extra: false, TextAlignmentOptions.MidlineLeft).characterSpacing = 4f;
				}
				else
				{
					BaseMusicBundle b = PlaceStageBundle(i);
					Ui.Cover(r, "jk", new Vector2(66f, 4f), new Vector2(52f, 52f), b?.Album);
					Ui.Txt(r, "t", new Vector2(130f, 0f), new Vector2(280f, RH - 6f), (string)FV(s, "title"), 19f, Color.white, extra: true, TextAlignmentOptions.MidlineLeft).overflowMode = TextOverflowModes.Ellipsis;
				}
				// 난이도는 잠겨 있어도 보임 (원래 목록도 레벨은 공개)
				Image d = Ui.Img(r, "d", new Vector2(420f, 13f), new Vector2(110f, 34f), Color.white, ModeSprite(mode));
				Ui.VTxt(d.transform, "t", Vector2.zero, new Vector2(110f, 34f), ModeEng(mode) + " " + lv, 14f, Color.white, TextAlignmentOptions.Midline);
				if (done)
				{
					float rate = (float)FV(s, "rate");
					Ui.VTxt(r, "r", new Vector2(-12f, 0f), new Vector2(90f, RH - 6f), rate.ToString("0.00", IC), 17f, Color.white, TextAlignmentOptions.MidlineRight, Ui.TR, Ui.TR);
				}
				else if (now)
				{
					Ui.Pulse(Ui.VTxt(r, "now", new Vector2(-12f, 0f), new Vector2(90f, RH - 6f), "NOW", 16f, Ui.Gold, TextAlignmentOptions.MidlineRight, Ui.TR, Ui.TR), 0.4f, 1f, 1f);
				}
				Ui.In(r, Ui.At(0.1f + i * 0.025f), -30f, 0f, 1f, 0.4f);
			}
		}

		// 오른쪽 위: 예상 티어 (원래 PlaceTierPreview: 지금 티어 · 점수 게이지 · 다음 티어)
		private void PlaceTierPanel()
		{
			const float W = 600f;
			int point = pd.FinalPoint;
			pd.UpdateTier();
			object cur = pd.CurrentTierInfo;
			object next = pd.NextTierInfo;
			RectTransform t = Ui.Node(body, "tier", new Vector2(-54f, 122f), new Vector2(W, 430f), Ui.TR, Ui.TR);
			Ui.FillImg(t, "bg", Glass);
			Ui.In(t, Ui.At(0.08f), 70f, 0f, 1f, 0.6f);
			Ui.VTxt(t, "h", new Vector2(24f, 18f), new Vector2(300f, 26f), "EXPECTED TIER", 18f, Ui.Gold, TextAlignmentOptions.TopLeft);
			Ui.In(TierArt(t, "art", new Vector2(24f, 56f), 220f, cur), Ui.At(0.2f), 0f, 0f, 0.5f, 0.6f, back: true);
			Ui.VTxt(t, "n", new Vector2(268f, 80f), new Vector2(310f, 50f), (string)FV(cur, "name") ?? "", 36f, Color.white, TextAlignmentOptions.TopLeft);
			TextMeshProUGUI pt = Ui.VTxt(t, "pt", new Vector2(268f, 138f), new Vector2(310f, 50f), "", 40f, Ui.Gold, TextAlignmentOptions.TopLeft);
			Ui.Count(pt, Mathf.Max(8000, point - 300), point, Ui.At(0.3f), 0.8f, (float v) => Mathf.RoundToInt(v) + " <size=55%>PT</size>");
			int lo = Convert.ToInt32(FV(cur, "point"));
			int hi = Convert.ToInt32(FV(next, "point"));
			float fill = (hi > lo) ? Mathf.Clamp01((float)(point - lo) / (hi - lo)) : 1f;
			RectTransform bar = Ui.Node(t, "bar", new Vector2(268f, 206f), new Vector2(300f, 12f));
			Ui.FillImg(bar, "bg", new Color(1f, 1f, 1f, 0.12f));
			Ui.Grow(Ui.Img(bar, "fill", Vector2.zero, new Vector2(300f * fill, 12f), Ui.Gold).rectTransform, 0f, 300f * fill, Ui.At(0.4f), 0.8f);
			if (next != cur)
			{
				Ui.Txt(t, "nx", new Vector2(268f, 226f), new Vector2(310f, 24f), "다음  " + FV(next, "name") + "  (" + hi + ")", 15f, Ui.Light, extra: false, TextAlignmentOptions.TopLeft);
			}
			// 재시도
			Ui.Img(t, "div", new Vector2(24f, 300f), new Vector2(W - 48f, 2f), new Color(1f, 1f, 1f, 0.1f));
			int rc = pd.RetryCount;
			Ui.VTxt(t, "rh", new Vector2(24f, 322f), new Vector2(200f, 26f), "RETRY", 18f, Ui.Gold, TextAlignmentOptions.TopLeft);
			Ui.Txt(t, "rt", new Vector2(24f, 356f), new Vector2(300f, 40f), "재시도 (" + rc + "회)", 26f, Color.white, extra: true, TextAlignmentOptions.TopLeft);
			for (int i = 0; i < 3; i++)
			{
				Image pip = Ui.Img(t, "pip" + i, new Vector2(W - 170f + i * 50f, 340f), new Vector2(36f, 36f), (i < rc) ? Ui.Gold : new Color(1f, 1f, 1f, 0.12f));
				pip.sprite = Ui.Disc();
			}
			Ui.Txt(t, "rs", new Vector2(24f, 396f), new Vector2(W - 48f, 24f), "결과 화면에서 다시 치거나, 게임 오버 때 자동으로 써요", 13f, Sub, extra: false, TextAlignmentOptions.TopLeft);
		}

		// 오른쪽 아래: 보고 있는 단계 곡
		private void PlaceStageCard()
		{
			const float W = 600f;
			int i = Mathf.Clamp(plSel, 0, PlStages.Length - 1);
			object s = PlStages.GetValue(i);
			bool lockd = !PlaceUnlocked(i);
			BaseMusicBundle b = PlaceStageBundle(i);
			int mode = Convert.ToInt32(FV(s, "mode"));
			RectTransform c = Ui.Node(body, "card", new Vector2(-54f, 572f), new Vector2(W, 392f), Ui.TR, Ui.TR);
			Ui.FillImg(c, "bg", Glass);
			Ui.In(c, Ui.At(0.12f), 70f, 0f, 1f, 0.6f);
			Ui.VTxt(c, "h", new Vector2(24f, 18f), new Vector2(400f, 26f), "STAGE " + (i + 1) + " / " + PlStages.Length, 18f, Ui.Gold, TextAlignmentOptions.TopLeft);
			RectTransform jk = Ui.Node(c, "jk", new Vector2(24f, 60f), new Vector2(200f, 200f));
			Ui.FillImg(jk, "bg", new Color(1f, 1f, 1f, 0.05f));
			if (lockd)
			{
				Ui.VTxt(jk, "q", Vector2.zero, new Vector2(200f, 200f), "?", 90f, new Color(1f, 1f, 1f, 0.25f), TextAlignmentOptions.Midline);
			}
			else
			{
				Ui.Cover(jk, "art", Vector2.zero, new Vector2(200f, 200f), b?.Album);
			}
			Ui.Txt(c, "t", new Vector2(244f, 64f), new Vector2(W - 268f, 80f), lockd ? "? ? ?" : (string)FV(s, "title"), 28f, Color.white, extra: true, TextAlignmentOptions.TopLeft).overflowMode = TextOverflowModes.Ellipsis;
			Ui.Txt(c, "a", new Vector2(244f, 148f), new Vector2(W - 268f, 26f), lockd ? "" : (b?.artist ?? ""), 16f, Sub, extra: false, TextAlignmentOptions.TopLeft).overflowMode = TextOverflowModes.Ellipsis;
			Image d = Ui.Img(c, "d", new Vector2(244f, 196f), new Vector2(150f, 40f), Color.white, ModeSprite(mode));
			Ui.VTxt(d.transform, "t", Vector2.zero, new Vector2(150f, 40f), ModeEng(mode) + " " + FV(s, "level"), 18f, Color.white, TextAlignmentOptions.Midline);
			Ui.VTxt(c, "k", new Vector2(404f, 196f), new Vector2(120f, 40f), "4B", 22f, Color.white, TextAlignmentOptions.MidlineLeft);
			string hint = (i == PlIndex && plPhase == 1) ? "ENTER 로 시험 시작" : (i < PlIndex) ? ("기록 " + ((float)FV(s, "rate")).ToString("0.00", IC) + "%") : (lockd ? "앞 곡을 치면 열려요" : "");
			bool go = i == PlIndex && plPhase == 1;
			RectTransform bt = Ui.Node(c, "btn", new Vector2(24f, 290f), new Vector2(W - 48f, 76f));
			Ui.FillImg(bt, "bg", go ? Yellow : new Color(1f, 1f, 1f, 0.08f));
			if (go)
			{
				Ui.Shine(bt, 0.6f, 2.2f);
			}
			Ui.VTxt(bt, "t", Vector2.zero, new Vector2(W - 48f, 76f), hint, 26f, go ? new Color(0.08f, 0.08f, 0.1f) : Color.white, TextAlignmentOptions.Midline);
		}

		// 최종 결과 (원래 PlaceFinalPanel: 티어 · 점수 → 기록 저장 · 업적 · 보상). 시즌 1 다른 응시자 기록과 비교한 순위도
		private void PlaceFinal(int point, int tier, bool done)
		{
			if (pd == null)
			{
				pd = Singleton<PlacementData>.Instance ?? new GameObject("PlacementData").AddComponent<PlacementData>();
			}
			object info = TierInfoOf(tier) ?? pd.CurrentTierInfo;
			Image dim = Ui.FillImg(body, "fdim", new Color(0f, 0f, 0f, 0.82f));
			RectTransform box = Ui.Node(body, "final", new Vector2(0f, 110f), new Vector2(980f, 820f), Ui.TC, Ui.TC);
			PopIn(dim, box, "plfinal");
			Ui.FillImg(box, "bg", new Color(0.05f, 0.05f, 0.08f, 0.98f));
			Ui.Frame(box, new Color(1f, 1f, 1f, 0.3f), 2f);
			Ui.VTxt(box, "h", new Vector2(0f, 30f), new Vector2(980f, 34f), done ? "시험 응시 완료" : "PLACEMENT RESULT", 28f, Ui.Gold, TextAlignmentOptions.Top);
			Ui.In(TierArt(box, "art", new Vector2(340f, 80f), 300f, info), Ui.At(0.3f), 0f, 0f, 0.2f, 0.8f, back: true);
			Ui.In(Ui.VTxt(box, "n", new Vector2(0f, 396f), new Vector2(980f, 64f), (string)FV(info, "name") ?? "", 54f, Color.white, TextAlignmentOptions.Top), Ui.At(0.9f), 0f, 20f);
			TextMeshProUGUI pt = Ui.VTxt(box, "pt", new Vector2(0f, 466f), new Vector2(980f, 50f), "", 40f, Ui.Gold, TextAlignmentOptions.Top);
			Ui.Count(pt, 8000f, point, Ui.At(0.5f), 1.4f, (float v) => Mathf.RoundToInt(v) + " <size=55%>PT</size>");
			// 시즌 1 응시자 기록(게임에 들어 있는 9천여 명)과 비교
			Array sd = FV(PlBundle, "seasonData") as Array;
			if (sd != null && sd.Length > 0)
			{
				int above = 0;
				foreach (object e in sd)
				{
					if (Convert.ToInt32(FV(e, "point")) > point)
					{
						above++;
					}
				}
				int rank = above + 1;
				float top = 100f * rank / (sd.Length + 1);
				Ui.In(Ui.Txt(box, "rank", new Vector2(0f, 530f), new Vector2(980f, 34f), "시즌 1 응시자 " + sd.Length.ToString("#,0") + "명 중  <color=#FFD54A>" + rank.ToString("#,0") + "위</color>  (상위 " + top.ToString((top < 10f) ? "0.0" : "0") + "%)", 22f, Color.white, extra: true, TextAlignmentOptions.Top), Ui.At(1.3f), 0f, 16f);
			}
			if (!done && PlStages != null)
			{
				// 곡별 정확도 (12칸)
				for (int i = 0; i < PlStages.Length; i++)
				{
					object s = PlStages.GetValue(i);
					RectTransform c = Ui.Node(box, "c" + i, new Vector2(50f + (i % 6) * 147f, 590f + (i / 6) * 70f), new Vector2(137f, 60f));
					Ui.FillImg(c, "bg", new Color(1f, 1f, 1f, 0.05f));
					Ui.VTxt(c, "n", new Vector2(0f, 6f), new Vector2(137f, 20f), (i + 1).ToString("00") + "  Lv." + FV(s, "level"), 13f, Sub, TextAlignmentOptions.Top);
					Ui.VTxt(c, "r", new Vector2(0f, 28f), new Vector2(137f, 28f), ((float)FV(s, "rate")).ToString("0.00", IC), 19f, Color.white, TextAlignmentOptions.Top);
					Ui.In(c, Ui.At(1.5f + i * 0.04f), 0f, 12f);
				}
			}
			else
			{
				Ui.Txt(box, "msg", new Vector2(0f, 620f), new Vector2(980f, 80f), "더 이상 시험을 칠 수 없습니다.\n다음 시즌을 이용해주세요.", 22f, Ui.Light, extra: true, TextAlignmentOptions.Top);
			}
			Ui.In(Ui.Txt(box, "go", new Vector2(0f, 770f), new Vector2(980f, 30f), "ENTER  아카데미로", 18f, Sub, extra: false, TextAlignmentOptions.Top), Ui.At(3.5f), 0f, 10f);
		}

		// ---------------- 리더보드 (원래 PlaceLeaderboard: 시즌 응시자 기록 · 티어 탭 · 내 점수 · 티어 분포) ----------------
		// 기록은 게임에 들어 있는 시즌 1 응시자 데이터(PlacementBundle.seasonData) 그대로. 내 기록은 따로 아래 줄에
		private static readonly string[] BoardCats = { "ALL", "LEGENO", "GRAND\nMASTER", "MASTER", "DIAMOND", "PLATINUM", "GOLD", "SILVER", "BRONZE", "IRON" };

		private const int BOARD_ROWS = 11;

		private bool plBoard;

		private int plCat; // TIER_CATEGORY

		private int plTop; // 보고 있는 첫 줄

		private object[] plBoardAll; // index 순

		private object[] plBoardRows; // 지금 탭

		// TIER_CATEGORY → TIER 범위 (원래 GetTierCategoryMinMax)
		private static bool InCat(int cat, int tier)
		{
			return cat switch
			{
				0 => true,
				1 => tier == 21,
				2 => tier == 20,
				3 => tier == 19,
				_ => tier >= 1 && (9 - cat) * 3 + 1 <= tier && tier <= (9 - cat) * 3 + 3,
			};
		}

		// 내 점수: 응시 완료 기록 · 방금 끝난 시험. 아니면 미응시(-1)
		private int MyBoardPoint => (plPhase == 3 && plDone != null) ? plDone.point : ((plPhase == 2 && pd != null) ? pd.FinalPoint : (-1));

		private int MyBoardTier => (plPhase == 3 && plDone != null) ? (int)plDone.tier : ((plPhase == 2 && pd != null) ? PlTier : 0);

		private void OpenBoard()
		{
			if (plBoardAll == null)
			{
				plBoardAll = ((FV(PlBundle, "seasonData") as Array) ?? Array.Empty<object>()).Cast<object>().OrderBy((object e) => Convert.ToInt32(FV(e, "index"))).ToArray();
			}
			plBoard = true;
			SetBoardCat(0);
			PlaySe(AudioManager.SE.Confirm);
		}

		private void SetBoardCat(int c)
		{
			plCat = c;
			plTop = 0;
			plBoardRows = plBoardAll.Where((object e) => InCat(c, Convert.ToInt32(FV(e, "tier")))).ToArray();
		}

		private void InputBoard()
		{
			int max = Mathf.Max(0, plBoardRows.Length - BOARD_ROWS);
			if (Repeat(KeyCode.LeftArrow) && plCat > 0)
			{
				SetBoardCat(plCat - 1);
				PlaySe(AudioManager.SE.SlideMove);
			}
			else if (Repeat(KeyCode.RightArrow) && plCat < BoardCats.Length - 1)
			{
				SetBoardCat(plCat + 1);
				PlaySe(AudioManager.SE.SlideMove);
			}
			else if (Repeat(KeyCode.UpArrow) && plTop > 0)
			{
				plTop--;
			}
			else if (Repeat(KeyCode.DownArrow) && plTop < max)
			{
				plTop++;
			}
			else if (Repeat(KeyCode.PageUp))
			{
				plTop = Mathf.Max(0, plTop - BOARD_ROWS);
				PlaySe(AudioManager.SE.SlideMove);
			}
			else if (Repeat(KeyCode.PageDown))
			{
				plTop = Mathf.Min(max, plTop + BOARD_ROWS);
				PlaySe(AudioManager.SE.SlideMove);
			}
			else if (Key(KeyCode.Home) && MyBoardPoint >= 0)
			{
				// 내 위치로: 나보다 점수 높은 사람 수
				int p = MyBoardPoint;
				plTop = Mathf.Clamp(plBoardRows.Count((object e) => Convert.ToInt32(FV(e, "point")) > p) - BOARD_ROWS / 2, 0, max);
				PlaySe(AudioManager.SE.SlideMove);
			}
			else if (Key(KeyCode.Escape) || Key(KeyCode.Tab))
			{
				plBoard = false;
				PlaySe(AudioManager.SE.SlideMove);
			}
		}

		private void BoardRow(Transform p, string name, float y, float w, string rank, string user, string uid, int tier, int point, Color bg, Color fg)
		{
			RectTransform r = Ui.Node(p, name, new Vector2(10f, y), new Vector2(w - 20f, 58f));
			Ui.FillImg(r, "bg", bg);
			Ui.VTxt(r, "i", new Vector2(14f, 0f), new Vector2(110f, 58f), rank, 20f, fg, TextAlignmentOptions.MidlineLeft);
			Ui.Txt(r, "n", new Vector2(130f, 0f), new Vector2(w - 640f, 58f), user + ((uid.Length > 0) ? ("  <color=#AAAAAA><size=60%>#" + uid + "</size></color>") : ""), 20f, Color.white, extra: true, TextAlignmentOptions.MidlineLeft).overflowMode = TextOverflowModes.Ellipsis;
			object ti = TierInfoOf(tier);
			if (FV(ti, "sprite") is Sprite sp)
			{
				Image ic = Ui.Img(r, "ti", new Vector2(w - 500f, 5f), new Vector2(48f, 48f), Color.white, null);
				ic.sprite = sp;
				ic.preserveAspect = true;
			}
			Ui.Txt(r, "tn", new Vector2(w - 444f, 0f), new Vector2(220f, 58f), (string)FV(ti, "name") ?? "-", 18f, Color.white, extra: false, TextAlignmentOptions.MidlineLeft);
			Ui.VTxt(r, "pt", new Vector2(-14f, 0f), new Vector2(180f, 58f), (point < 0) ? "-" : point.ToString("N0"), 22f, Ui.Gold, TextAlignmentOptions.MidlineRight, Ui.TR, Ui.TR);
		}

		private void BuildBoard()
		{
			const float W = 1180f;
			Hotkeys("←→", "티어", "↑↓", "이동", "PgUp/Dn", "페이지", "HOME", "내 위치", "ESC", "닫기");
			// 탭
			RectTransform bar = Ui.Node(body, "tabs", new Vector2(54f, 122f), new Vector2(W, 54f));
			Ui.FillImg(bar, "bg", new Color(0f, 0f, 0f, 0.6f));
			float tw = W / BoardCats.Length;
			for (int i = 0; i < BoardCats.Length; i++)
			{
				bool on = i == plCat;
				RectTransform tab = Ui.Node(bar, "tab" + i, new Vector2(i * tw, 0f), new Vector2(tw, 54f));
				if (on)
				{
					Slant(tab, Color.white);
					Ui.In(tab, Ui.Seen("btab" + i), 0f, 0f, 0.85f, 0.25f, back: true, alpha: 1f);
				}
				Ui.Txt(tab, "t", Vector2.zero, new Vector2(tw, 54f), BoardCats[i], (BoardCats[i].Length > 8) ? 14f : 17f, on ? Color.black : new Color32(byte.MaxValue, 213, 74, 200), extra: true, TextAlignmentOptions.Midline).lineSpacing = -20f;
			}
			// 목록
			RectTransform l = Ui.Node(body, "board", new Vector2(54f, 190f), new Vector2(W, BOARD_ROWS * 62f + 20f));
			Ui.FillImg(l, "bg", Glass);
			if (plBoardRows.Length == 0)
			{
				Ui.Txt(l, "e", new Vector2(0f, 300f), new Vector2(W, 40f), "이 티어에는 응시자가 없어요", 22f, Sub, extra: false, TextAlignmentOptions.Top);
			}
			string myUid = FV(pd, "uid") as string;
			for (int k = 0; k < BOARD_ROWS && plTop + k < plBoardRows.Length; k++)
			{
				object e = plBoardRows[plTop + k];
				string uid = FV(e, "uid") as string ?? "";
				bool me = !string.IsNullOrEmpty(myUid) && uid == myUid;
				Color bg = me ? new Color(1f, 0.84f, 0.29f, 0.25f) : new Color(1f, 1f, 1f, ((plTop + k) % 2 == 0) ? 0.06f : 0.02f);
				BoardRow(l, "r" + k, 10f + k * 62f, W, Convert.ToInt32(FV(e, "index")).ToString("N0"), FV(e, "userName") as string ?? "", uid, Convert.ToInt32(FV(e, "tier")), Convert.ToInt32(FV(e, "point")), bg, Color.white);
			}
			if (plBoardRows.Length > BOARD_ROWS)
			{
				Ui.VTxt(l, "pg", new Vector2(-14f, -34f), new Vector2(400f, 26f), (plTop + 1).ToString("N0") + " - " + Mathf.Min(plBoardRows.Length, plTop + BOARD_ROWS).ToString("N0") + " / " + plBoardRows.Length.ToString("N0"), 15f, Sub, TextAlignmentOptions.TopRight, Ui.TR, Ui.TR);
			}
			// 내 점수
			int mp = MyBoardPoint;
			int all = plBoardAll.Length;
			int rank = (mp < 0) ? 0 : (plBoardAll.Count((object e) => Convert.ToInt32(FV(e, "point")) > mp) + 1);
			float top = (mp < 0) ? 0f : (100f * rank / (all + 1));
			RectTransform my = Ui.Node(body, "my", new Vector2(54f, 190f + BOARD_ROWS * 62f + 32f), new Vector2(W, 96f));
			Ui.FillImg(my, "bg", Glass);
			Ui.Frame(my, Ui.Gold, 2f);
			Ui.Txt(my, "h", new Vector2(24f, 6f), new Vector2(600f, 24f), (mp < 0) ? "내 점수 (미응시)" : ("내 점수 (상위 " + top.ToString("N2", IC) + "%)"), 15f, Ui.Gold, extra: false, TextAlignmentOptions.TopLeft);
			BoardRow(my, "me", 34f, W, (mp < 0) ? "-" : rank.ToString("N0"), myName, myUid ?? "", MyBoardTier, mp, new Color(0f, 0f, 0f, 0f), Ui.Gold);
			// 티어 분포 (원래 judgeCircles: 티어 묶음별 비율)
			const float RW = 600f;
			RectTransform d = Ui.Node(body, "dist", new Vector2(-54f, 122f), new Vector2(RW, 870f), Ui.TR, Ui.TR);
			Ui.FillImg(d, "bg", Glass);
			Ui.VTxt(d, "h", new Vector2(24f, 18f), new Vector2(400f, 26f), "TIER DISTRIBUTION", 18f, Ui.Gold, TextAlignmentOptions.TopLeft);
			Ui.Txt(d, "n", new Vector2(24f, 50f), new Vector2(RW - 48f, 24f), "시즌 " + PLACE_SEASON + " 응시자 " + all.ToString("N0") + "명", 15f, Sub, extra: false, TextAlignmentOptions.TopLeft);
			for (int c = 1; c < BoardCats.Length; c++)
			{
				int cc = c;
				int n = plBoardAll.Count((object e) => InCat(cc, Convert.ToInt32(FV(e, "tier"))));
				float pct = (all == 0) ? 0f : (100f * n / all);
				float y = 96f + (c - 1) * 84f;
				bool on = c == plCat || (mp >= 0 && InCat(c, MyBoardTier));
				Ui.Txt(d, "c" + c, new Vector2(24f, y), new Vector2(300f, 30f), BoardCats[c].Replace("\n", " ") + ((mp >= 0 && InCat(c, MyBoardTier)) ? "  <size=75%><color=#FFD54A>◀ 나</color></size>" : ""), 18f, on ? Color.white : Ui.Light, extra: true, TextAlignmentOptions.TopLeft);
				Ui.VTxt(d, "p" + c, new Vector2(-24f, y), new Vector2(200f, 30f), pct.ToString("N2", IC) + "%", 18f, Color.white, TextAlignmentOptions.TopRight, Ui.TR, Ui.TR);
				RectTransform gb = Ui.Node(d, "g" + c, new Vector2(24f, y + 38f), new Vector2(RW - 48f, 14f));
				Ui.FillImg(gb, "bg", new Color(1f, 1f, 1f, 0.1f));
				float fw = (RW - 48f) * Mathf.Clamp01(pct / 100f);
				Ui.Grow(Ui.Img(gb, "f", Vector2.zero, new Vector2(fw, 14f), on ? Ui.Gold : new Color(1f, 1f, 1f, 0.55f)).rectTransform, 0f, fw, Ui.At(0.1f + c * 0.04f), 0.6f);
			}
		}
	}
}
