// 온라인 화면 공통: 캔버스, 배경(영상·음악), 로비, 방 만들기/참가 팝업, 지난 결과, 기록, 인게임 HUD.
// 상태 시그니처가 바뀔 때만 다시 조립한다. 좌표는 1920x1080 기준 왼쪽 위.
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace WJMAXOnline
{
	public partial class OnlineMod
	{
		private Canvas canvas;

		private RectTransform screen; // 전체 화면 (로비·방·대전·결과·기록)

		private RectTransform videoLayer; // 로비 배경 영상

		private RectTransform body;

		private CanvasGroup bodyGroup;

		private RectTransform hud;

		private VideoPlayer bgVideo;

		private AudioSource bgm;

		private string screenSig;

		private string hudSig;

		private CanvasGroup noteGroup;

		private static readonly string[] Ranks = new string[8] { "1st", "2nd", "3rd", "4th", "5th", "6th", "7th", "8th" };

		private static readonly Color Sub = new Color32(175, 175, 175, byte.MaxValue);

		private static readonly Color Blue = new Color32(46, 155, byte.MaxValue, byte.MaxValue);

		private static readonly Color Orange = new Color32(byte.MaxValue, 138, 30, byte.MaxValue);

		private static readonly Color Yellow = new Color32(byte.MaxValue, 214, 0, byte.MaxValue);

		private bool ScreenOn(SCENE scene)
		{
			return online && scr != Scr.None && scene == SCENE.FreeStyle && !inMatch && !ListerPlaying;
		}

		private void UpdateView(SCENE scene)
		{
			bool show = ScreenOn(scene);
			bool inGame = inMatch && scene == SCENE.Game;
			// 게임 결과 화면에는 온라인 순위를 같이 띄운다 (입력은 게임이 그대로 받음)
			bool showHud = inGame || (online && scene == SCENE.Result && matchIds.Contains(myId) && MP.Count > 0);
#if DEV
			showHud |= devRank;
#endif
			// 화면이 떠 있는 동안 게임 입력 차단 (게임 공식 창과 같은 방식: 창 목록에 올림)
			// 온라인 곡 진행 중에도 잡아둬서 창 전환 시 게임이 자동 일시정지(옵션창)하지 않게 한다.
			bool wantFocus = show || (inGame && !leaving);
			if (wantFocus != focused)
			{
				focused = wantFocus;
				if (wantFocus)
				{
					CInput.FocusWindow(base.gameObject);
				}
				else
				{
					CInput.CloseWindow(base.gameObject);
					CInput.SetInputDelay();
					Input.imeCompositionMode = IMECompositionMode.Auto;
				}
			}
			// 게임 곡 선택 화면은 온라인 중에 감춘다 (곡 시작·게임 설정·옷장만 뒤에서 씀)
			Transform content = (lister == null) ? null : lister.transform.Find("ContentMask");
			if (content != null && content.gameObject.activeSelf == online)
			{
				content.gameObject.SetActive(!online);
			}
			// 게임 창(게임 설정·옷장·정렬·검색)이 위에 뜨면 온라인 화면은 잠시 내린다
			bool screenVisible = show && !GameWindowOpen;
			Media(screenVisible);
			if (!screenVisible && !showHud)
			{
				if (canvas != null)
				{
					canvas.gameObject.SetActive(value: false);
				}
				return;
			}
			if (canvas == null)
			{
				BuildCanvas();
			}
			canvas.gameObject.SetActive(value: true);
			screen.gameObject.SetActive(screenVisible);
			hud.gameObject.SetActive(showHud);
			if (screenVisible)
			{
				// 화면 전환 시 살짝 떠오르는 연출
				float t = Mathf.Clamp01((Time.unscaledTime - scrTime) / 0.25f);
				bodyGroup.alpha = Mathf.SmoothStep(0f, 1f, t);
				body.anchoredPosition = new Vector2(0f, -14f * (1f - Mathf.SmoothStep(0f, 1f, t)));
				if (Time.unscaledTime > statusUntil)
				{
					status = "";
				}
				string sig = ScreenSig();
				if (sig != screenSig)
				{
					screenSig = sig;
					RebuildScreen();
				}
				AnimateScreen();
			}
			if (showHud)
			{
				string sig2 = HudSig();
				if (sig2 != hudSig)
				{
					int cut = sig2.LastIndexOf("|#");
					bool scoreOnly = hudSig != null && cut > 0 && hudSig.Length > cut && string.CompareOrdinal(sig2, 0, hudSig, 0, cut + 2) == 0;
					if (!scoreOnly || Time.unscaledTime - hudBuiltAt >= 0.2f)
					{
						hudSig = sig2;
						hudBuiltAt = Time.unscaledTime;
						RebuildHud();
					}
				}
				// "OO 님이 나갔어요" 알림은 끝날 때 서서히 사라짐
				if (noteGroup != null)
				{
					noteGroup.alpha = Mathf.Clamp01((leftUntil - Time.unscaledTime) / 1.2f);
				}
			}
		}

		private static readonly FieldInfo fPreviewVideo = typeof(MusicSelectRoot).GetField("previewVideo", F);

		private static readonly FieldInfo fPreviewAudio = typeof(MainMusicLister).GetField("previewAudio", F);

		private bool listerMuted;

		// 로비·기록: 타이틀 메인화면 영상 + 테마 배경음 (곡 미리듣기는 멈춤). 방·대전: 고른 곡 미리듣기
		private void Media(bool show)
		{
			VideoPlayer pv = (lister == null) ? null : (fPreviewVideo.GetValue(lister) as VideoPlayer);
			AudioSource pa = (lister == null) ? null : (fPreviewAudio.GetValue(lister) as AudioSource);
			bool lobbyMedia = show && (scr == Scr.Lobby || scr == Scr.Ladder || scr == Scr.History || (scr == Scr.Result && !connected) || (scr == Scr.Battle && (bat.phase == BAT_BAN || bat.phase == BAT_END || Roulette(out _, out _))));
			if (videoLayer != null)
			{
				videoLayer.gameObject.SetActive(lobbyMedia);
			}
			if (lobbyMedia && bgVideo != null)
			{
				if (string.IsNullOrEmpty(bgVideo.url))
				{
					bgVideo.url = ThemeVideo();
					bgVideo.Play();
				}
				else if (!bgVideo.isPlaying)
				{
					bgVideo.Play();
				}
				// 테마 BGM(곡)은 틀지 않는다: 온라인 화면에서 엉뚱한 곡이 배경음으로 나오는 문제
				if (pv != null && pv.isPlaying)
				{
					pv.Pause();
				}
				if (pa != null && pa.isPlaying)
				{
					pa.Pause();
				}
				listerMuted = pv != null;
				return;
			}
			if (bgm != null && bgm.isPlaying)
			{
				bgm.Pause();
			}
			if (bgVideo != null && bgVideo.isPlaying)
			{
				bgVideo.Pause();
			}
			if (listerMuted && pv != null && !ListerPlaying)
			{
				// 멈춰 둔 예전 미리듣기(엉뚱한 곡)를 이어 틀지 않고, 지금 곡 미리듣기를 새로 튼다
				listerMuted = false;
				previewRoot = "#replay";
			}
		}

		private string ThemeVideo()
		{
			string prefix = "Default";
			try
			{
				prefix = BundlePackage.itemBundle.GetItem<ItemTheme>(BundlePackage.closet.GetEquipID(Item.TYPE.THEME)).themePrefix;
			}
			catch (System.Exception)
			{
			}
			string path = System.IO.Path.Combine(Paths.MAIN_VIDEO_PATH, "Title_" + prefix + "_" + Mathf.Max(onlineTitleIndex, 0) + ".mp4");
			if (!System.IO.File.Exists(path))
			{
				path = System.IO.Path.Combine(Paths.MAIN_VIDEO_PATH, "Title_Default_Logo.mp4");
			}
			return path;
		}

		private static AudioClip ThemeBgm()
		{
			try
			{
				return BundlePackage.itemBundle.GetItem<ItemTheme>(BundlePackage.closet.GetEquipID(Item.TYPE.THEME)).bgmClip;
			}
			catch (System.Exception)
			{
				return null;
			}
		}

		private bool CaretOn => (int)(Time.unscaledTime * 2.2f) % 2 == 0;

		private string Caret => CaretOn ? "<color=#FFFFFF>|</color>" : "<color=#FFFFFF00>|</color>";

		private string Dots => new string('.', (int)(Time.unscaledTime * 2f) % 4);

		private string ScreenSig()
		{
			string common = scr + "|" + keyHelp + "|" + status + "|" + popup + "|" + confirmLeave + "|" + kickOpen + kickSel + "|" + chatting + "|" + (chatting ? (chatText + Input.compositionString + CaretOn) : "") + "|" + chatLog.Count + "|" + roomCode + "|" + connecting + "|" + (connecting ? Dots : "");
			switch (scr)
			{
			case Scr.Lobby:
				return common + LobbySig();
			case Scr.Room:
				return common + RoomSig();
			case Scr.Battle:
				return common + BattleSig();
			case Scr.Ladder:
				return common + LadderSig();
			case Scr.Result:
				return common + "|" + (lastMatch?.title ?? "") + resultTaken;
			default:
				return common + "|" + histSel + "|" + history.Count;
			}
		}

		private void BuildCanvas()
		{
			Ui.LoadAssets();
			GameObject go = new GameObject("OnlineCanvas");
			go.transform.SetParent(base.transform, worldPositionStays: false);
			canvas = go.AddComponent<Canvas>();
			canvas.renderMode = RenderMode.ScreenSpaceOverlay;
			canvas.sortingOrder = 500;
			CanvasScaler cs = go.AddComponent<CanvasScaler>();
			cs.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
			cs.referenceResolution = new Vector2(1920f, 1080f);
			cs.matchWidthOrHeight = 1f;
			screen = Ui.Fill(go.transform, "Screen");
			Ui.FillImg(screen, "base", new Color32(10, 10, 14, byte.MaxValue));
			// 로비 배경: 타이틀 메인화면의 ONLINE 메뉴 영상 (영상 → RenderTexture → RawImage)
			videoLayer = Ui.Fill(screen, "video");
			RawImage raw = videoLayer.gameObject.AddComponent<RawImage>();
			raw.raycastTarget = false;
			raw.texture = new RenderTexture(1920, 1080, 0);
			bgVideo = go.AddComponent<VideoPlayer>();
			bgVideo.playOnAwake = false;
			bgVideo.isLooping = true;
			bgVideo.renderMode = VideoRenderMode.RenderTexture;
			bgVideo.targetTexture = (RenderTexture)raw.texture;
			bgVideo.audioOutputMode = VideoAudioOutputMode.None;
			bgm = go.AddComponent<AudioSource>();
			bgm.playOnAwake = false;
			bgm.loop = true;
			body = Ui.Fill(screen, "body");
			bodyGroup = body.gameObject.AddComponent<CanvasGroup>();
			hud = Ui.Fill(go.transform, "Hud");
		}

		private string epochKey;

		private void RebuildScreen()
		{
			// 화면 단계가 바뀌면 등장 연출을 처음부터 (팝업·채팅 같은 건 단계로 치지 않음)
			string ek = scr + "|" + Spectating + "|" + ((scr == Scr.Battle) ? (bat.no + ":" + bat.phase + ":" + bat.round + ":" + Roulette(out _, out _)) : "") + "|" + ((scr == Scr.Ladder) ? ladState.ToString() : "") + "|" + ((scr == Scr.Room) ? matchType.ToString() : "");
			if (ek != epochKey)
			{
				epochKey = ek;
				Ui.NewEpoch();
			}
			Ui.Build();
			Ui.Clear(body);
			animList = null;
			animJacket = null;
			animJacketGroup = null;
			spinner = null;
			switch (scr)
			{
			case Scr.Lobby:
				BuildLobby();
				break;
			case Scr.Room:
				BuildRoom();
				break;
			case Scr.Battle:
				BuildBattle();
				break;
			case Scr.Ladder:
				BuildLadder();
				if (opOpen)
				{
					OnePickBox();
				}
				break;
			case Scr.Result:
				BuildResult();
				break;
			case Scr.History:
				BuildHistory();
				break;
			}
			StatusPill();
			if (kickOpen)
			{
				KickBox();
			}
			if (confirmLeave)
			{
				ConfirmBox(aiMatch ? "래더 포기" : "방 나가기", (bat.kind == MT_LADDER && bat.phase != BAT_NONE && bat.phase != BAT_END && MySlot >= 0) ? "지금 나가면 래더 매치는 패배로 처리돼요.\n정말 나갈까요?" : "방에서 나가 로비로 돌아갈까요?", "나가기");
			}
		}

		// ---------------- 공통 부품 ----------------
		private void StatusPill()
		{
			if (status.Length == 0)
			{
				return;
			}
			TextMeshProUGUI t = Ui.Txt(body, "status", Vector2.zero, new Vector2(1400f, 46f), status, 21f, Color.white, extra: false, TextAlignmentOptions.Midline, Ui.TC, Ui.TC);
			float w = Ui.Width(t, status) + 70f;
			RectTransform pill = Ui.Node(body, "statusBg", new Vector2(0f, 936f), new Vector2(w, 46f), Ui.TC, Ui.TC);
			Ui.FillImg(pill, "bg", new Color(0f, 0f, 0f, 0.9f));
			Ui.Img(pill, "accent", Vector2.zero, new Vector2(5f, 46f), statusColor);
			t.rectTransform.SetParent(pill, worldPositionStays: false);
			t.rectTransform.anchoredPosition = new Vector2(3f, 0f);
			t.rectTransform.sizeDelta = new Vector2(w, 46f);
			Ui.In(pill, Ui.Seen("status" + status), 0f, 24f, 1f, 0.3f);
		}

		// 팝업: 어둡게 깔리고 상자가 살짝 커지며 나타남
		private void PopIn(Image dim, RectTransform box, string key)
		{
			float at = Ui.Seen("pop" + key);
			Ui.In(dim, at, 0f, 0f, 1f, 0.2f);
			Ui.In(box, at, 0f, 0f, 0.9f, 0.3f, back: true);
		}

		private void ConfirmBox(string title, string msg, string ok)
		{
			Image dim = Ui.FillImg(body, "dim", new Color(0f, 0f, 0f, 0.7f));
			RectTransform box = Ui.Node(body, "confirm", new Vector2(0f, 380f), new Vector2(680f, 300f), Ui.TC, Ui.TC);
			PopIn(dim, box, "confirm");
			Ui.FillImg(box, "bg", new Color(0.06f, 0.06f, 0.08f, 0.97f));
			// 왁맥 팝업 모양: 얇은 회색 테두리
			Ui.Frame(box, new Color(0.72f, 0.72f, 0.75f, 1f), 2f);
			Ui.Txt(box, "t", new Vector2(40f, 30f), new Vector2(600f, 44f), title, 32f, Color.white, extra: true, TextAlignmentOptions.TopLeft);
			TextMeshProUGUI m = Ui.Txt(box, "m", new Vector2(40f, 92f), new Vector2(600f, 100f), msg, 21f, Ui.Light, extra: false, TextAlignmentOptions.TopLeft);
			m.enableWordWrapping = true;
			m.lineSpacing = 10f;
			Ui.HotkeyRightToLeft(box, 40f, 236f, "ENTER", ok, 20f);
			Ui.HotkeyLeftToRight(box, 40f, 236f, "ESC", "취소", 20f);
		}

		// 방장: 내보내기 목록
		private void KickBox()
		{
			List<Player> others = players.Where((Player p) => p.id != myId).ToList();
			Image dim = Ui.FillImg(body, "dim", new Color(0f, 0f, 0f, 0.7f));
			float h = 170f + others.Count * 70f;
			RectTransform box = Ui.Node(body, "kick", new Vector2(0f, 540f - h / 2f), new Vector2(640f, h), Ui.TC, Ui.TC);
			PopIn(dim, box, "kick");
			Ui.FillImg(box, "bg", new Color(0.06f, 0.06f, 0.08f, 0.97f));
			// 왁맥 팝업 모양: 얇은 회색 테두리
			Ui.Img(box, "footer", new Vector2(0f, h - 78f), new Vector2(640f, 78f), new Color32(40, 40, 44, 255));
			Ui.Frame(box, new Color(0.72f, 0.72f, 0.75f, 1f), 2f);
			Ui.VTxt(box, "t", new Vector2(36f, 24f), new Vector2(560f, 40f), "KICK", 30f, Color.white, TextAlignmentOptions.TopLeft);
			Ui.Txt(box, "s", new Vector2(120f, 30f), new Vector2(480f, 30f), "내보낼 사람을 고르세요 (이 방에는 다시 못 들어와요)", 16f, Sub, extra: false, TextAlignmentOptions.TopLeft);
			for (int i = 0; i < others.Count; i++)
			{
				bool on = i == kickSel;
				RectTransform r = Ui.Node(box, "p" + i, new Vector2(30f, 80 + i * 70), new Vector2(580f, 62f));
				if (on)
				{
					Ui.FillImg(r, "sel", Color.white, "Music_Choice_Effect");
					Ui.Shine(r);
				}
				else
				{
					Ui.FillImg(r, "bg", new Color(1f, 1f, 1f, 0.06f));
				}
				Image ic = Ui.Img(r, "icon", new Vector2(8f, 6f), new Vector2(50f, 50f), Color.white);
				ic.sprite = IconSprite(others[i].icon);
				ic.preserveAspect = true;
				Ui.Txt(r, "n", new Vector2(72f, 0f), new Vector2(300f, 62f), others[i].name, 22f, Color.white, extra: true, TextAlignmentOptions.MidlineLeft);
				Ui.VTxt(r, "lv", new Vector2(-16f, 0f), new Vector2(200f, 62f), "LV " + others[i].level + "   " + others[i].ping + "ms", 16f, on ? Color.white : Sub, TextAlignmentOptions.MidlineRight, Ui.TR, Ui.TR);
			}
			Ui.HotkeyRightToLeft(box, 30f, h - 50f, "ENTER", "내보내기", 18f);
			Ui.HotkeyLeftToRight(box, 30f, h - 50f, "ESC", "닫기", 18f);
		}

		// 화면 아래 키 안내 (오른쪽부터)
		private bool keyHelp;

		private bool hotkeysMore;

		// 아래 키 바: 앞쪽 3개 + 마지막(ESC) 만 보여주고, 나머지는 F10 키 안내 창에
		private void Hotkeys(params string[] pairs)
		{
			List<string> show = pairs.ToList();
			bool more = pairs.Length > 8;
			hotkeysMore = more;
			if (more)
			{
				show = pairs.Take(6).Concat(pairs.Skip(pairs.Length - 2)).Concat(new string[2] { "F10", "키 안내" }).ToList();
			}

			// 왁맥 메인 화면과 같은 아래 키 바: 화면 너비 어두운 띠, 왼쪽부터 키 나열
			RectTransform bar = Ui.Node(body, "keys", new Vector2(0f, 1000f), new Vector2(1920f, 80f));
			Ui.FillImg(bar, "bg", new Color(0.12f, 0.12f, 0.14f, 0.82f));
			Ui.In(bar, Ui.At(0.1f), 0f, 50f, 1f, 0.35f, back: false, alpha: 1f);
			float x = 40f;
			for (int i = 0; i < show.Count; i += 2)
			{
				x = Ui.HotkeyLeftToRight(bar, x, 23f, show[i], show[i + 1], 19f);
			}
			if (!keyHelp || !more)
			{
				return;
			}
			int n = pairs.Length / 2;
			float h = 170f + n * 50f;
			Image khdim = Ui.FillImg(body, "khdim", new Color(0f, 0f, 0f, 0.7f));
			RectTransform box = Ui.Node(body, "keyhelp", new Vector2(0f, 540f - h / 2f), new Vector2(560f, h), Ui.TC, Ui.TC);
			PopIn(khdim, box, "keyhelp");
			Ui.FillImg(box, "bg", new Color(0.06f, 0.06f, 0.08f, 0.97f));
			// 왁맥 팝업 모양: 얇은 회색 테두리
			Ui.Img(box, "footer", new Vector2(0f, h - 78f), new Vector2(560f, 78f), new Color32(40, 40, 44, 255));
			Ui.Frame(box, new Color(0.72f, 0.72f, 0.75f, 1f), 2f);
			Ui.VTxt(box, "t", new Vector2(36f, 24f), new Vector2(480f, 40f), "KEY GUIDE", 28f, Color.white, TextAlignmentOptions.TopLeft);
			for (int i = 0; i < n; i++)
			{
				RectTransform r = Ui.Node(box, "k" + i, new Vector2(36f, 80 + i * 50), new Vector2(488f, 44f));
				Ui.FillImg(r, "bg", new Color(1f, 1f, 1f, (i % 2 == 0) ? 0.06f : 0.03f));
				Ui.VTxt(r, "key", new Vector2(16f, 0f), new Vector2(150f, 44f), pairs[i * 2], 18f, Ui.Gold, TextAlignmentOptions.MidlineLeft);
				Ui.Txt(r, "d", new Vector2(170f, 0f), new Vector2(300f, 44f), pairs[i * 2 + 1], 19f, Color.white, extra: false, TextAlignmentOptions.MidlineLeft);
			}
			Ui.Txt(box, "close", new Vector2(0f, h - 40f), new Vector2(560f, 30f), "F10 / ESC 로 닫기", 15f, Sub, extra: false, TextAlignmentOptions.Top);
		}

		// 디맥 왼쪽 위 로고: 원 + ONLINE / 부제
		private void Logo(string sub)
		{
			Image ring = Ui.Img(body, "logoRing", new Vector2(54f, 22f), new Vector2(46f, 46f), Color.white);
			ring.sprite = Ui.Ring();
			Ui.In(ring, Ui.At(0f), 0f, 0f, 0.3f, 0.45f, back: true);
			Ui.In(Ui.VTxt(body, "logo1", new Vector2(112f, 16f), new Vector2(400f, 34f), "ONLINE", 30f, Color.white, TextAlignmentOptions.TopLeft), Ui.At(0.05f), -30f);
			Ui.In(Ui.VTxt(body, "logo2", new Vector2(112f, 48f), new Vector2(400f, 30f), sub, 24f, new Color(1f, 1f, 1f, 0.55f), TextAlignmentOptions.TopLeft), Ui.At(0.1f), -30f);
		}

		// 흰 사선 탭 (게임 카테고리 선택 탭 스프라이트)
		private static Image Slant(RectTransform rt, Color c)
		{
			return Ui.FillImg(rt, "slant", c, "Main_Category_Middle", Image.Type.Sliced);
		}

		private static Color RankColor(int rank)
		{
			return rank switch
			{
				0 => Ui.Gold,
				1 => new Color32(208, 214, 224, byte.MaxValue),
				2 => new Color32(224, 160, 112, byte.MaxValue),
				_ => Ui.Gray,
			};
		}

		private static Color ModeColor(int m)
		{
			return m switch
			{
				0 => new Color32(88, 222, 166, byte.MaxValue),
				1 => new Color32(byte.MaxValue, 140, 190, byte.MaxValue),
				2 => new Color32(byte.MaxValue, 196, 74, byte.MaxValue),
				3 => new Color32(byte.MaxValue, 110, 96, byte.MaxValue),
				4 => new Color32(170, 120, byte.MaxValue, byte.MaxValue),
				_ => new Color32(90, 90, 96, byte.MaxValue),
			};
		}

		// 게임 난이도 버튼과 같은 영문 이름
		private static string ModeEng(int m)
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

		private static string ModeSprite(int m)
		{
			return m switch
			{
				0 => "mode_messi_filled",
				1 => "mode_angel_filled",
				2 => "mode_wakgood_filled",
				3 => "mode_minsu_filled",
				_ => "mode_lie_filled_0",
			};
		}

		private static string MatchTypeName(int t)
		{
			return t switch
			{
				MT_TEAM => "TEAM MATCH",
				MT_VERSUS => "VERSUS MATCH",
				MT_LADDER => "LADDER MATCH",
				_ => "FREE MATCH",
			};
		}

		private static Color MatchTypeColor(int t)
		{
			return t switch
			{
				MT_TEAM => new Color32(70, 150, byte.MaxValue, byte.MaxValue),
				MT_VERSUS => new Color32(byte.MaxValue, 120, 40, byte.MaxValue),
				MT_LADDER => new Color32(200, 110, byte.MaxValue, byte.MaxValue),
				_ => new Color32(60, 200, 110, byte.MaxValue),
			};
		}

		private static RectTransform ProfileCardAt(RectTransform card, Player p)
		{
			ProfileCard(card, p);
			return card;
		}

		// 게임 오른쪽 위 프로필과 같은 카드: 플레이트 그림 + 아이콘 + 레벨 + 이름 + [칭호]
		private static void ProfileCard(RectTransform card, Player p)
		{
			Image plate = Ui.FillImg(card, "plate", Color.white);
			plate.sprite = PlateSprite(p.plate);
			if (plate.sprite == null)
			{
				plate.color = new Color(0f, 0f, 0f, 0.45f);
			}
			Ui.FillImg(card, "shade", new Color(0f, 0f, 0f, 0.35f));
			float h = card.sizeDelta.y;
			Image icon = Ui.Img(card, "icon", new Vector2(8f, 8f), new Vector2(h - 16f, h - 16f), Color.white);
			icon.sprite = IconSprite(p.icon);
			icon.preserveAspect = true;
			if (icon.sprite == null)
			{
				icon.color = new Color(1f, 1f, 1f, 0.12f);
			}
			Ui.Txt(card, "name", new Vector2(h + 4f, 12f), new Vector2(card.sizeDelta.x - h - 150f, 34f), p.name, 26f, Color.white, extra: true, TextAlignmentOptions.TopLeft).overflowMode = TextOverflowModes.Ellipsis;
			Ui.VTxt(card, "lv", new Vector2(h + 4f, 48f), new Vector2(200f, 30f), "<size=60%>LV</size> " + p.level, 24f, Color.white, TextAlignmentOptions.TopLeft);
			Ui.VTxt(card, "tier", new Vector2(-12f, 14f), new Vector2(150f, 22f), TierText(p.tier), 15f, TierColor(p.tier), TextAlignmentOptions.TopRight, Ui.TR, Ui.TR);
			if (p.achTotal > 0)
			{
				Ui.Txt(card, "ach", new Vector2(-12f, 50f), new Vector2(150f, 22f), "업적 " + p.ach + "<color=#AFAFAF>/" + p.achTotal + "</color>", 15f, Color.white, extra: false, TextAlignmentOptions.TopRight, Ui.TR, Ui.TR);
			}
		}

		// ---------------- 로비 (디맥 온라인 메뉴: OPEN MATCH / LADDER MATCH) ----------------
		private enum Popup
		{
			None,
			Create,
			Join,
			Browse
		}

		private Popup popup;

		private int lobbyCard; // 0 OPEN MATCH, 1 LADDER MATCH

		private int lobbyRow; // 0 방 만들기, 1 방 찾기, 2 코드로 참가

		private int popRow;

		private int createType = MT_FREE;

		private int createMax = 8;

		private bool createPublic
		{
			get
			{
				return PlayerPrefs.GetInt("WJONLINE_PUBLIC", 1) == 1;
			}
			set
			{
				PlayerPrefs.SetInt("WJONLINE_PUBLIC", value ? 1 : 0);
			}
		}

		// 방 찾기: 공개 방 목록 (백그라운드로 받아 옴)
		private volatile string[] roomsRaw;

		private volatile string roomsErr;

		private volatile bool roomsLoading;

		private float roomsAt = -99f;

		private int roomsVer;

		private int browseSel;

		private void FetchRooms()
		{
			string url = Relay.Url();
			roomsAt = Time.unscaledTime;
			if (url == null)
			{
				roomsErr = "중계 서버(OnlineMod\\relay.txt)가 없어서 방 찾기를 쓸 수 없어요";
				roomsVer++;
				return;
			}
			if (roomsLoading)
			{
				return;
			}
			roomsLoading = true;
			roomsVer++;
			new System.Threading.Thread((System.Threading.ThreadStart)delegate
			{
				try
				{
					roomsRaw = Relay.FetchRooms(url);
					roomsErr = null;
				}
				catch (System.Exception e)
				{
					roomsErr = "목록을 불러오지 못했어요 (" + Ws.Why(e) + ")";
				}
				roomsLoading = false;
			})
			{
				IsBackground = true
			}.Start();
		}

		private struct RoomRow
		{
			public string code, name;

			public int type, n, max;

			public bool playing, sameVer;
		}

		// 같은 버전 · 대기 중 · 자리 있는 방이 위로
		private List<RoomRow> Rooms()
		{
			List<RoomRow> list = new List<RoomRow>();
			foreach (string line in roomsRaw ?? new string[0])
			{
				string[] p = line.Split('|');
				if (p.Length < 7 || !Relay.IsCode(p[0]))
				{
					continue;
				}
				list.Add(new RoomRow
				{
					code = p[0],
					sameVer = p[1] == VERSION,
					type = Int(p[2], 0, 2),
					n = Int(p[3], 0, 99),
					max = Int(p[4], 2, 8),
					playing = p[5] == "1",
					name = CleanName(p[6])
				});
			}
			return list.OrderBy((RoomRow r) => !r.sameVer).ThenBy((RoomRow r) => r.n >= r.max).ThenBy((RoomRow r) => r.playing).ThenByDescending((RoomRow r) => r.n).ToList();
		}


		private string LobbySig()
		{
			return lobbyCard + "|" + lobbyRow + "|" + popRow + "|" + createType + "|" + createMax + "|" + createPublic + "|" + codeText + "|" + ((popup != Popup.None && popup != Popup.Browse) ? CaretOn.ToString() : "") + "|" + MyRating + "|" + ((popup == Popup.Browse) ? (browseSel + ":" + roomsVer + ":" + roomsLoading + ":" + roomsErr + ":" + string.Join(";", roomsRaw ?? new string[0])) : "");
		}

		private void InputLobby()
		{
			if (popup == Popup.Create)
			{
				InputCreatePopup();
				return;
			}
			if (popup == Popup.Join)
			{
				InputJoinPopup();
				return;
			}
			if (popup == Popup.Browse)
			{
				InputBrowsePopup();
				return;
			}
			if (Key(KeyCode.LeftArrow) || Key(KeyCode.RightArrow))
			{
				lobbyCard = 1 - lobbyCard;
				PlaySe(AudioManager.SE.SlideMove);
			}
			else if ((Key(KeyCode.UpArrow) || Key(KeyCode.DownArrow)) && lobbyCard == 0)
			{
				lobbyRow = (lobbyRow + (Key(KeyCode.UpArrow) ? 2 : 1)) % 3;
				PlaySe(AudioManager.SE.SlideMove);
			}
			else if (Enter())
			{
				status = "";
				if (lobbyCard == 1)
				{
					EnterLadder();
					return;
				}
				PlaySe(AudioManager.SE.Confirm);
				popRow = 0;
				createType = MT_FREE;
				popup = (lobbyRow == 0) ? Popup.Create : ((lobbyRow == 1) ? Popup.Browse : Popup.Join);
				if (popup == Popup.Browse)
				{
					browseSel = 0;
					roomsRaw = null;
					FetchRooms();
				}
			}
			else if (Key(KeyCode.F11))
			{
				PlaySe(AudioManager.SE.Confirm);
				LoadHistory();
				SetScr(Scr.History);
			}
			else if (Key(KeyCode.Escape))
			{
				PlaySe(AudioManager.SE.Confirm);
				LeaveOnline();
			}
		}

		private const int CreateRows = 4;

		private void InputCreatePopup()
		{
			int rows = CreateRows;
			if (Key(KeyCode.UpArrow))
			{
				popRow = (popRow + rows - 1) % rows;
				PlaySe(AudioManager.SE.SlideMove);
			}
			else if (Key(KeyCode.DownArrow))
			{
				popRow = (popRow + 1) % rows;
				PlaySe(AudioManager.SE.SlideMove);
			}
			int field = popRow; // 0 방식, 1 인원, 2 공개, 3 만들기
			bool l = Key(KeyCode.LeftArrow);
			bool r = Key(KeyCode.RightArrow);
			if (field == 0 && (l || r))
			{
				createType = (createType + (l ? 2 : 1)) % 3;
				PlaySe(AudioManager.SE.SlideMove);
			}
			else if (field == 1 && (l || r))
			{
				createMax = Mathf.Clamp(createMax + (l ? (-1) : 1), 2, 8);
				PlaySe(AudioManager.SE.SlideMove);
			}
			else if (field == 2 && (l || r))
			{
				createPublic = !createPublic;
				PlaySe(AudioManager.SE.SlideMove);
			}
			if (Enter())
			{
				PlaySe(AudioManager.SE.Confirm);
				if (field == 3)
				{
					StartHost(createType, createMax);
				}
				else
				{
					popRow = rows - 1;
				}
			}
			else if (Key(KeyCode.Escape))
			{
				PlaySe(AudioManager.SE.SlideMove);
				popup = Popup.None;
			}
		}

		private void InputBrowsePopup()
		{
			List<RoomRow> rooms = Rooms();
			if (!roomsLoading && Time.unscaledTime - roomsAt > 5f)
			{
				FetchRooms();
			}
			if (Key(KeyCode.UpArrow) && rooms.Count > 0)
			{
				browseSel = (browseSel + rooms.Count - 1) % rooms.Count;
				PlaySe(AudioManager.SE.SlideMove);
			}
			else if (Key(KeyCode.DownArrow) && rooms.Count > 0)
			{
				browseSel = (browseSel + 1) % rooms.Count;
				PlaySe(AudioManager.SE.SlideMove);
			}
			else if (Key(KeyCode.F5))
			{
				PlaySe(AudioManager.SE.SlideMove);
				FetchRooms();
			}
			else if (Enter() && rooms.Count > 0)
			{
				RoomRow r = rooms[Mathf.Clamp(browseSel, 0, rooms.Count - 1)];
				if (!r.sameVer)
				{
					Status("이 방은 온라인 모드 버전이 달라요", Ui.Red);
					PlaySe(AudioManager.SE.error);
					return;
				}
				PlaySe(AudioManager.SE.Confirm);
				codeText = r.code;
				popup = Popup.Join;
				StartJoin();
			}
			else if (Key(KeyCode.Escape))
			{
				PlaySe(AudioManager.SE.SlideMove);
				popup = Popup.None;
			}
		}

		private void InputJoinPopup()
		{
			if (connecting)
			{
				if (Key(KeyCode.Escape))
				{
					Disconnect();
					Status("접속을 취소했어요", Ui.Gray);
				}
				return;
			}
			if ((Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) && Key(KeyCode.V))
			{
				codeText = RoomCode.Clean(GUIUtility.systemCopyBuffer ?? "");
				codeText = codeText.Substring(0, Mathf.Min(RoomCode.LENGTH, codeText.Length));
				PlaySe(AudioManager.SE.SlideMove);
			}
			else
			{
				codeText = EditText(RoomCode.Clean(codeText), RoomCode.LENGTH, char.IsLetterOrDigit).ToUpperInvariant();
			}
			if (Enter())
			{
				PlaySe(AudioManager.SE.Confirm);
				StartJoin();
			}
			else if (Key(KeyCode.Escape))
			{
				PlaySe(AudioManager.SE.SlideMove);
				popup = Popup.None;
			}
		}

		private void BuildLobby()
		{
			Ui.FillImg(body, "shade", new Color(0f, 0f, 0f, 0.5f));
			Logo("SELECT MODE");
			Player mine = new Player
			{
				id = myId,
				name = myName
			};
			SetMyProfile(mine);
			Ui.In(ProfileCardAt(Ui.Node(body, "profile", new Vector2(-54f, 22f), new Vector2(440f, 88f), Ui.TR, Ui.TR), mine), Ui.At(0.1f), 60f);
			if (Updater.ready > 0)
			{
				Ui.Txt(body, "update", new Vector2(-54f, 118f), new Vector2(700f, 30f), "새 버전 받음! 게임을 다시 켜면 적용돼요", 19f, new Color(1f, 0.84f, 0.29f), extra: false, TextAlignmentOptions.TopRight, Ui.TR, Ui.TR);
			}
			for (int c = 0; c < 2; c++)
			{
				LobbyCard(c, new Vector2((c == 0) ? 250f : 1000f, 170f));
			}
			if (popup == Popup.Create)
			{
				BuildCreatePopup();
			}
			else if (popup == Popup.Join)
			{
				BuildJoinPopup();
			}
			else if (popup == Popup.Browse)
			{
				BuildBrowsePopup();
			}
			else
			{
				Hotkeys("ENTER", "결정", "↑↓", "방 만들기·찾기·참가", "←→", "모드", "F11", "기록", "ESC", "타이틀로");
			}
		}

		private void LobbyCard(int c, Vector2 pos)
		{
			bool on = lobbyCard == c;
			const float W = 670f;
			const float H = 800f;
			RectTransform card = Ui.Node(body, "card" + c, pos, new Vector2(W, H));
			if (!on)
			{
				card.gameObject.AddComponent<CanvasGroup>().alpha = 0.55f;
			}
			Ui.FillImg(card, "bg", new Color(0f, 0f, 0f, 0.78f));
			// 위: 그림 영역
			if (c == 0)
			{
				Ui.Cover(card, "art", Vector2.zero, new Vector2(W, 300f), AnyJacket());
				Image g = Ui.Img(card, "grad", Vector2.zero, new Vector2(W, 300f), new Color(0f, 0f, 0f, 0.85f));
				g.sprite = Ui.HFade();
			}
			else
			{
				RectTransform art = Ui.Node(card, "art", Vector2.zero, new Vector2(W, 300f));
				Ui.FillImg(art, "bg", new Color32(40, 18, 60, byte.MaxValue));
				Ui.FillImg(art, "fx", new Color(1f, 1f, 1f, 0.25f), "Music_Choice_Effect");
				int t = MyRating;
				TierEmblem(art, new Vector2(W / 2f - 150f, 120f), 120f, t, label: false);
				Ui.VTxt(art, "tier", new Vector2(W / 2f - 60f, 80f), new Vector2(330f, 50f), TierText(t), (TierGroup(t) == 6) ? 30f : 38f, TierColor(t), TextAlignmentOptions.TopLeft);
				Ui.VTxt(art, "lp", new Vector2(W / 2f - 60f, 132f), new Vector2(330f, 40f), t + " PT", 30f, Color.white, TextAlignmentOptions.TopLeft);
				RectTransform bar = Ui.Node(art, "bar", new Vector2(W / 2f - 200f, 222f), new Vector2(400f, 10f));
				Ui.FillImg(bar, "bg", new Color(1f, 1f, 1f, 0.15f));
				Ui.Img(bar, "fill", Vector2.zero, new Vector2(400f * TierProgress(t), 10f), TierColor(t));
				Ui.Txt(art, "wl", new Vector2(0f, 248f), new Vector2(W, 30f), Wins + "승  " + Losses + "패", 20f, Ui.Light, extra: false, TextAlignmentOptions.Top);
			}
			Ui.Img(card, "line", new Vector2(0f, 300f), new Vector2(W, 4f), Color.white, "Music_Choice_Effect");
			Ui.VTxt(card, "title", new Vector2(40f, 330f), new Vector2(W - 80f, 70f), (c == 0) ? "OPEN MATCH" : "LADDER MATCH", 54f, Color.white, TextAlignmentOptions.TopLeft);
			Ui.Txt(card, "sub", new Vector2(42f, 402f), new Vector2(W - 80f, 30f), (c == 0) ? "오픈 매치 · 최대 8명" : "래더 매치 · AI 와 1:1 랭크전", 21f, (c == 0) ? Ui.Green : MatchTypeColor(MT_LADDER), extra: false, TextAlignmentOptions.TopLeft);
			string desc = (c == 0) ? "방장이 고른 곡을 다 같이 플레이해요. 난이도는 각자.\nFREE · TEAM(팀 평균 점수) · VERSUS(1:1 + 관전)\n곡이 없는 사람은 관전, 진행 중에도 입장 가능" : "내 점수 근처의 AI 와 매칭돼요. 곡 5개 중 1곡씩 밴,\n2판 2선승, 동점이면 하드 판정 타이브레이커.\n이기면 +18~22점, IRON 부터 GRAND MASTER 까지.";
			TextMeshProUGUI d = Ui.Txt(card, "desc", new Vector2(42f, 448f), new Vector2(W - 84f, 120f), desc, 19f, Ui.Light, extra: false, TextAlignmentOptions.TopLeft);
			d.lineSpacing = 14f;
			string[] rows = (c == 0) ? new string[3] { "방 만들기", "방 찾기", "코드로 참가" } : new string[1] { "래더 입장" };
			for (int i = 0; i < rows.Length; i++)
			{
				bool sel = on && (c == 1 || lobbyRow == i);
				RectTransform r = Ui.Node(card, "row" + i, new Vector2(30f, 590 + i * 68), new Vector2(W - 60f, 58f));
				if (sel)
				{
					Slant(r, Color.white);
					Ui.Shine(r, 0.6f);
					Ui.In(r, Ui.Seen("lrow" + c + i), 18f, 0f, 1f, 0.25f, back: false, alpha: 0.4f);
				}
				else
				{
					Ui.FillImg(r, "bg", new Color(1f, 1f, 1f, 0.07f));
				}
				Ui.Txt(r, "t", Vector2.zero, new Vector2(W - 60f, 58f), rows[i] + (sel ? "  ▶" : ""), 24f, sel ? Color.black : Color.white, extra: true, TextAlignmentOptions.Midline);
			}
			if (on)
			{
				Ui.Frame(card, Ui.Gold, 4f);
				foreach (Image fi in card.Find("frame").GetComponentsInChildren<Image>())
				{
					Ui.Pulse(fi, 0.45f, 1f, 1.4f);
				}
			}
			// 처음엔 아래에서 차례로 올라옴
			Ui.In(card, Ui.At(0.08f + c * 0.1f), 0f, 70f, 1f, 0.5f);
		}

		private RectTransform PopupBox(string title, string sub, float h, float w = 820f)
		{
			Image dim = Ui.FillImg(body, "dim", new Color(0f, 0f, 0f, 0.72f));
			RectTransform box = Ui.Node(body, "popup", new Vector2(0f, 540f - h / 2f), new Vector2(w, h), Ui.TC, Ui.TC);
			PopIn(dim, box, title);
			Ui.FillImg(box, "bg", new Color(0.05f, 0.05f, 0.07f, 0.97f));
			// 왁맥 팝업 모양: 얇은 회색 테두리
			Ui.Frame(box, new Color(0.72f, 0.72f, 0.75f, 1f), 2f);
			Ui.VTxt(box, "t", new Vector2(44f, 30f), new Vector2(700f, 50f), title, 36f, Color.white, TextAlignmentOptions.TopLeft);
			Ui.Txt(box, "s", new Vector2(46f, 78f), new Vector2(700f, 26f), sub, 18f, Sub, extra: false, TextAlignmentOptions.TopLeft);
			return box;
		}

		private static string Arrows(string v, bool on)
		{
			return on ? ("<color=#FFFFFF90>◀</color>   " + v + "   <color=#FFFFFF90>▶</color>") : v;
		}

		private void BuildCreatePopup()
		{
			RectTransform box = PopupBox("OPEN MATCH", "오픈 매치 방 만들기", 560f);
			string[,] rows = new string[3, 2]
			{
				{ "대전 방식", MatchTypeName(createType) },
				{ "최대 인원", createMax + "명" },
				{ "공개 설정", createPublic ? "<color=#5BE08A>공개</color>" : "<color=#FF8A8A>비공개</color>" }
			};
			for (int i = 0; i < 3; i++)
			{
				bool sel = popRow == i;
				RectTransform r = Ui.Node(box, "row" + i, new Vector2(40f, 130 + i * 84), new Vector2(740f, 72f));
				if (sel)
				{
					Ui.FillImg(r, "sel", Color.white, "Music_Choice_Effect");
				}
				else
				{
					Ui.FillImg(r, "bg", new Color(1f, 1f, 1f, 0.06f));
				}
				Ui.Txt(r, "l", new Vector2(30f, 0f), new Vector2(300f, 72f), rows[i, 0], 24f, Color.white, extra: false, TextAlignmentOptions.MidlineLeft);
				string v = Arrows(rows[i, 1], sel);
				Ui.Txt(r, "v", new Vector2(-30f, 0f), new Vector2(420f, 72f), v, 25f, Color.white, extra: false, TextAlignmentOptions.MidlineRight, Ui.TR, Ui.TR);
			}
			string help = createType switch
			{
				MT_TEAM => "팀 A·B·C 로 나눠 팀원 점수 평균으로 겨뤄요.",
				MT_VERSUS => "대전자 2명이 밴픽 2판 2선승, 나머지는 관전해요.",
				_ => "방장이 고른 곡을 다 같이, 각자 점수로 순위를 매겨요.",
			};
			if (popRow == 2)
			{
				help = createPublic ? "방 찾기 목록에 보여서 누구나 들어올 수 있어요." : "목록에 안 보여요. 방 코드를 아는 사람만 들어와요.";
			}
			Ui.Txt(box, "help", new Vector2(44f, 384f), new Vector2(740f, 26f), help + ((popRow <= 1) ? "  (방에서 F12 로 바꿀 수 있어요)" : ""), 17f, Sub, extra: false, TextAlignmentOptions.TopLeft);
			RectTransform b = Ui.Node(box, "btn", new Vector2(160f, 440f), new Vector2(500f, 70f));
			Slant(b, (popRow == 3) ? Color.white : new Color(1f, 1f, 1f, 0.35f));
			Ui.Txt(b, "t", Vector2.zero, new Vector2(500f, 70f), "방 만들기  ▶", 27f, Color.black, extra: true, TextAlignmentOptions.Midline);
			Hotkeys("ENTER", "결정", "←→", "변경", "↑↓", "이동", "ESC", "닫기");
		}

		// 방 찾기: 공개 방 목록 (↑↓ 고르기, ENTER 참가, F5 새로고침, 5초마다 자동 새로고침)
		private void BuildBrowsePopup()
		{
			const float W = 1100f;
			const float RH = 70f;
			const int VIS = 7;
			RectTransform box = PopupBox("FIND ROOM", "공개된 오픈 매치 방", 780f, W);
			List<RoomRow> rooms = Rooms();
			browseSel = Mathf.Clamp(browseSel, 0, Mathf.Max(0, rooms.Count - 1));
			Ui.Txt(box, "cnt", new Vector2(-44f, 40f), new Vector2(400f, 30f), (roomsLoading ? "불러오는 중" + Dots + "   " : "") + rooms.Count + "개 방", 20f, Sub, extra: false, TextAlignmentOptions.TopRight, Ui.TR, Ui.TR);
			// 머리줄
			float y = 126f;
			string[] heads = new string[4] { "방식", "방", "인원", "상태" };
			float[] hx = new float[4] { 64f, 260f, 760f, 900f };
			for (int i = 0; i < 4; i++)
			{
				Ui.Txt(box, "h" + i, new Vector2(hx[i], y), new Vector2(200f, 24f), heads[i], 16f, Sub, extra: false, TextAlignmentOptions.TopLeft);
			}
			y += 32f;
			if (rooms.Count == 0)
			{
				string msg = roomsErr ?? (roomsLoading && roomsRaw == null ? "불러오는 중" + Dots : "지금 열려 있는 공개 방이 없어요.\n직접 방을 만들어 보세요!");
				Ui.Txt(box, "empty", new Vector2(0f, y + 150f), new Vector2(W, 80f), msg, 22f, (roomsErr != null) ? Ui.Red : Ui.Light, extra: false, TextAlignmentOptions.Top);
			}
			int first = Mathf.Clamp(browseSel - VIS / 2, 0, Mathf.Max(0, rooms.Count - VIS));
			for (int k = 0; k < VIS && first + k < rooms.Count; k++)
			{
				int i = first + k;
				RoomRow rm = rooms[i];
				bool sel = i == browseSel;
				bool full = rm.n >= rm.max;
				RectTransform r = Ui.Node(box, "room" + k, new Vector2(40f, y + k * (RH + 8f)), new Vector2(W - 80f, RH));
				if (sel)
				{
					Ui.FillImg(r, "sel", Color.white, "Music_Choice_Effect");
				}
				else
				{
					Ui.FillImg(r, "bg", new Color(1f, 1f, 1f, 0.06f));
				}
				if (!rm.sameVer || full)
				{
					r.gameObject.AddComponent<CanvasGroup>().alpha = 0.45f;
				}
				Chip(r, "type", new Vector2(18f, 17f), new Vector2(160f, 36f), MatchTypeName(rm.type), MatchTypeColor(rm.type), Color.white, 18f);
				Ui.Txt(r, "name", new Vector2(220f, 0f), new Vector2(470f, RH), rm.name + "'s Room" + (rm.sameVer ? "" : "  <size=70%><color=#FF8A8A>버전 다름</color></size>"), 24f, Color.white, extra: true, TextAlignmentOptions.MidlineLeft).overflowMode = TextOverflowModes.Ellipsis;
				Ui.VTxt(r, "n", new Vector2(700f, 0f), new Vector2(120f, RH), rm.n + " / " + rm.max, 26f, full ? Ui.Red : Color.white, TextAlignmentOptions.MidlineLeft);
				Chip(r, "st", new Vector2(850f, 17f), new Vector2(150f, 36f), full ? "가득 참" : (rm.playing ? "게임 중" : "대기 중"), full ? new Color(0.5f, 0.12f, 0.16f, 1f) : (rm.playing ? new Color(0.55f, 0.38f, 0.05f, 1f) : new Color(0.1f, 0.45f, 0.25f, 1f)), Color.white, 18f);
				Ui.In(r, Ui.Seen("room" + rm.code), 0f, 14f, 1f, 0.3f);
			}
			if (rooms.Count > VIS)
			{
				Ui.Txt(box, "more", new Vector2(0f, y + VIS * (RH + 8f) + 4f), new Vector2(W, 24f), (first + 1) + "–" + Mathf.Min(rooms.Count, first + VIS) + " / " + rooms.Count, 16f, Sub, extra: false, TextAlignmentOptions.Top);
			}
			Ui.Txt(box, "tip", new Vector2(0f, 735f), new Vector2(W, 26f), "게임 중인 방에도 들어가면 관전하다가 다음 곡부터 함께해요.", 17f, Sub, extra: false, TextAlignmentOptions.Top);
			Hotkeys("ENTER", "참가", "↑↓", "고르기", "F5", "새로고침", "ESC", "닫기");
		}

		private void BuildJoinPopup()
		{
			RectTransform box = PopupBox("JOIN ROOM", "방장에게 받은 10자리 방 코드 (대소문자 구분 없음)", 470f);
			RectTransform r = Ui.Node(box, "code", new Vector2(40f, 130f), new Vector2(740f, 150f));
			Ui.FillImg(r, "bg", new Color(1f, 1f, 1f, 0.06f));
			Ui.Frame(r, new Color(1f, 1f, 1f, 0.4f), 2f);
			string shown = RoomCode.Format(RoomCode.Clean(codeText));
			string ghost = "XXXXX-XXXXX".Substring(Mathf.Min(11, shown.Length));
			Ui.VTxt(r, "t", Vector2.zero, new Vector2(740f, 150f), shown + (connecting ? "" : Caret) + "<color=#FFFFFF30>" + ghost + "</color>", 64f, Color.white, TextAlignmentOptions.Midline).characterSpacing = 6f;
			if (connecting)
			{
				Ui.Txt(box, "conn", new Vector2(0f, 320f), new Vector2(820f, 40f), "방에 접속하는 중" + Dots, 24f, Ui.Gold, extra: false, TextAlignmentOptions.Midline);
				Hotkeys("ESC", "취소");
				return;
			}
			RectTransform b = Ui.Node(box, "btn", new Vector2(160f, 320f), new Vector2(500f, 70f));
			Slant(b, (RoomCode.Clean(codeText).Length == RoomCode.LENGTH) ? Color.white : new Color(1f, 1f, 1f, 0.35f));
			Ui.Txt(b, "t", Vector2.zero, new Vector2(500f, 70f), "참가하기  ▶", 27f, Color.black, extra: true, TextAlignmentOptions.Midline);
			Hotkeys("ENTER", "참가", "CTRL+V", "붙여넣기", "BS", "지우기", "ESC", "닫기");
		}

		private static Sprite AnyJacket()
		{
			try
			{
				string id = GameValue.lastPlayed?.id;
				return BaseOf(id, GameValue.key)?.Album ?? BundlePackage.musicBundle.GetMusicBundles(FREESTYLE_CATEGORY.ALL, GameValue.key).FirstOrDefault((BaseMusicBundle b) => !b.IsRandom)?.Album;
			}
			catch (System.Exception)
			{
				return null;
			}
		}

		// ---------------- 공통: 왼쪽 정보 패널 + 오른쪽 목록 (결과·기록) ----------------
		private const float PX = 70f;

		private const float PW = 500f;

		private const float LX = 700f;

		private const float LW = 1110f;

		private const float TOP = 150f;

		private RectTransform InfoPanel(float height)
		{
			RectTransform p = Ui.Node(body, "info", new Vector2(PX, TOP), new Vector2(PW, height));
			Ui.FillImg(p, "bg", new Color(0f, 0f, 0f, 0.7f));
			return p;
		}

		private static void PanelHead(RectTransform p, string title, string sub)
		{
			RectTransform h = Ui.Node(p, "head", Vector2.zero, new Vector2(PW, 75f));
			Ui.FillImg(h, "bg", new Color(0f, 0f, 0f, 0.55f));
			Ui.Img(h, "accent", Vector2.zero, new Vector2(PW, 4f), Color.white, "Music_Choice_Effect");
			Ui.VTxt(h, "t", new Vector2(26f, 14f), new Vector2(460f, 34f), title, 28f, Color.white, TextAlignmentOptions.TopLeft);
			Ui.Txt(h, "s", new Vector2(27f, 48f), new Vector2(460f, 20f), sub, 15f, Sub, extra: false, TextAlignmentOptions.TopLeft);
		}

		private static TextMeshProUGUI Label(Transform p, float x, float y, string s, TextAlignmentOptions a = TextAlignmentOptions.TopLeft, float w = 450f)
		{
			return Ui.Txt(p, "lbl", new Vector2(x, y), new Vector2(w, 22f), s, 17f, Ui.Gold, extra: false, a);
		}

		private RectTransform Row(string name, float y, float h, bool selected, float x = LX, float w = LW)
		{
			RectTransform r = Ui.Node(body, name, new Vector2(x, y), new Vector2(w, h));
			if (selected)
			{
				Ui.FillImg(r, "sel", Color.white, "Music_Choice_Effect");
			}
			else
			{
				Ui.FillImg(r, "bg", new Color(0f, 0f, 0f, 0.65f));
			}
			return r;
		}

		private static void Tag(Transform p, Vector2 pos, float w, string s, Color c, bool filled = false)
		{
			RectTransform t = Ui.Node(p, "tag", pos, new Vector2(w, 28f));
			if (filled)
			{
				Ui.FillImg(t, "bg", c);
			}
			else
			{
				Ui.FillImg(t, "line", c, "PopupFrame", Image.Type.Sliced).pixelsPerUnitMultiplier = 6f;
			}
			Ui.Txt(t, "t", Vector2.zero, new Vector2(w, 28f), s, 15f, filled ? Color.black : c, extra: false, TextAlignmentOptions.Midline);
		}

		private static string StateText(Player p)
		{
			if (p.offline && !p.done)
			{
				return "<color=#FF606A>연결 끊김</color>";
			}
			if (!p.done)
			{
				return "<color=#FFD54A>플레이 중</color>";
			}
			return p.fullCombo ? "<color=#58DEA6>FULL COMBO</color>" : (p.cleared ? "CLEAR" : "<color=#FF606A>FAILED</color>");
		}

		// ---------------- 지난 결과 ----------------
		private void InputResult()
		{
			if (Enter() || Key(KeyCode.Escape) || Key(KeyCode.F11))
			{
				PlaySe(AudioManager.SE.Confirm);
				SetScr(connected ? Scr.Room : Scr.Lobby);
			}
		}

		private void BuildResult()
		{
			MatchResult m = lastMatch;
			if (m?.jacket != null)
			{
				// 배경: 곡 자켓을 크게 깔고 어둡게, 아주 천천히 움직임 (방 화면과 같은 느낌)
				Image bg = Ui.Cover(body, "bgArt", Vector2.zero, new Vector2(1920f, 1080f), m.jacket);
				bg.color = new Color(1f, 1f, 1f, 0.5f);
				Ui.Every(bg, delegate(float t)
				{
					bg.rectTransform.localScale = Vector3.one * (1.06f + 0.04f * Mathf.Sin(t * 0.15f));
					return false;
				}, 0f);
			}
			Ui.FillImg(body, "shade", new Color(0.02f, 0.02f, 0.04f, 0.8f));
			Logo("LAST RESULT");
			if (m == null)
			{
				Hotkeys("ENTER", "돌아가기");
				return;
			}
			bool team = m.kind == MT_TEAM;
			Player[] order = m.ps.OrderByDescending((Player p) => p.score).ToArray();
			Player mine = m.ps.FirstOrDefault((Player p) => p.id == myId);
			RectTransform p0 = InfoPanel(team ? 760f : 640f);
			Ui.In(p0, Ui.At(0.05f), -60f, 0f, 1f, 0.45f);
			Ui.Cover(p0, "jacket", Vector2.zero, new Vector2(PW, 281f), m.jacket);
			Ui.Txt(p0, "title", new Vector2(26f, 300f), new Vector2(450f, 40f), m.title, 30f, Color.white, extra: true, TextAlignmentOptions.TopLeft).overflowMode = TextOverflowModes.Ellipsis;
			Tag(p0, new Vector2(26f, 348f), 160f, MatchTypeName(m.kind), MatchTypeColor(m.kind), filled: true);
			Tag(p0, new Vector2(196f, 348f), 107f, "ROUND " + m.round, Ui.Light);
			Tag(p0, new Vector2(313f, 348f), 80f, m.ps.Count + "명", Ui.Light);
			Ui.Img(p0, "sep", new Vector2(26f, 400f), new Vector2(448f, 1f), new Color(1f, 1f, 1f, 0.15f));
			if (team)
			{
				int w = TeamWinner(m.ps);
				Label(p0, 0f, 420f, "TEAM RESULT", TextAlignmentOptions.Top, PW);
				Ui.In(Ui.VTxt(p0, "team", new Vector2(0f, 446f), new Vector2(PW, 70f), (w < 0) ? "DRAW" : (TeamName(w) + " WIN"), 50f, (w < 0) ? Color.white : TeamColor(w), TextAlignmentOptions.Top), Ui.At(0.55f), 0f, 0f, 1.8f, 0.4f, back: true);
				if (w >= 0)
				{
					// 이긴 팀 색 빛줄기
					Image wg = Ui.Img(p0, "wglow", new Vector2(PW / 2f, 480f), new Vector2(PW, 80f), TeamColor(w), null, Image.Type.Simple, Ui.TL, Ui.C);
					wg.sprite = Ui.Fade();
					wg.rectTransform.localEulerAngles = new Vector3(0f, 0f, 180f);
					wg.rectTransform.SetSiblingIndex(1);
					Ui.Pulse(wg, 0.1f, 0.35f, 2f);
				}
				for (int t = 0; t < 3; t++)
				{
					int s = TeamScore(m.ps, t);
					TextMeshProUGUI tt = Ui.Txt(p0, "ts" + t, new Vector2(PW / 3f * t, 540f), new Vector2(PW / 3f, 60f), "", 26f, TeamColor(t), extra: true, TextAlignmentOptions.Top);
					string tn = TeamName(t);
					if (s < 0)
					{
						tt.text = "<size=60%>" + tn + "</size>\n-";
					}
					else
					{
						Ui.Count(tt, 0f, s, Ui.At(0.3f + t * 0.08f), 0.8f, (float v) => "<size=60%>" + tn + "</size>\n" + ((int)v).ToString("N0"));
					}
				}
			}
			if (mine != null)
			{
				int myRank = order.Count((Player o) => o.score > mine.score);
				float y0 = team ? 630f : 420f;
				Label(p0, 0f, y0, "MY RANK", TextAlignmentOptions.Top, PW);
				Ui.In(Ui.VTxt(p0, "rank", new Vector2(0f, y0 + 26f), new Vector2(PW, 80f), Ranks[Mathf.Clamp(myRank, 0, 7)], team ? 52f : 72f, RankColor(myRank), TextAlignmentOptions.Top), Ui.At(0.7f), 0f, 0f, 1.6f, 0.4f, back: true);
				if (!team)
				{
					Label(p0, 0f, 540f, "SCORE", TextAlignmentOptions.Top, PW / 2f);
					Label(p0, PW / 2f, 540f, "RATE", TextAlignmentOptions.Top, PW / 2f);
					Ui.VTxt(p0, "ms", new Vector2(0f, 566f), new Vector2(PW / 2f, 40f), mine.score.ToString("N0"), 30f, Ui.Gold, TextAlignmentOptions.Top);
					Ui.VTxt(p0, "mr", new Vector2(PW / 2f, 566f), new Vector2(PW / 2f, 40f), mine.rate.ToString("0.00") + "%", 30f, Ui.Gold, TextAlignmentOptions.Top);
				}
			}
			float rowH = (order.Length > 5) ? 96f : 138f;
			for (int k = 0; k < order.Length; k++)
			{
				Player p2 = order[k];
				int rank = order.Count((Player o) => o.score > p2.score);
				bool me = p2.id == myId;
				RectTransform r = Row("row" + k, TOP + k * (rowH + 10f), rowH, me);
				Ui.In(r, Ui.At(0.12f + k * 0.06f), 90f, 0f, 1f, 0.45f);
				if (me)
				{
					Ui.Shine(r, 0.3f, 3f);
				}
				if (team)
				{
					Ui.Img(r, "team", Vector2.zero, new Vector2(10f, rowH), TeamColor(p2.team));
				}
				Ui.VTxt(r, "rank", new Vector2(30f, 10f), new Vector2(120f, 60f), Ranks[Mathf.Clamp(rank, 0, 7)], 40f, me ? Color.white : RankColor(rank), TextAlignmentOptions.TopLeft);
				Image ri = Ui.Img(r, "icon", new Vector2(150f, 10f), new Vector2(64f, 64f), Color.white);
				ri.sprite = IconSprite(p2.icon);
				ri.preserveAspect = true;
				if (ri.sprite == null)
				{
					ri.color = new Color(1f, 1f, 1f, 0.12f);
				}
				Ui.Txt(r, "name", new Vector2(230f, 10f), new Vector2(380f, 40f), p2.name, 28f, Color.white, extra: true, TextAlignmentOptions.TopLeft).overflowMode = TextOverflowModes.Ellipsis;
				string state = StateText(p2);
				Ui.Txt(r, "state", new Vector2(231f, 52f), new Vector2(400f, 22f), (me ? System.Text.RegularExpressions.Regex.Replace(state, "</?color[^>]*>", "") : state) + "   <color=#FFFFFFB0>" + p2.matchDiff + "</color>", 16f, Color.white, extra: false, TextAlignmentOptions.TopLeft);
				string tops = TopBadges(m.ps, p2);
				if (tops.Length > 0)
				{
					Ui.VTxt(r, "tops", new Vector2(560f, 12f), new Vector2(300f, 24f), tops, 14f, Ui.Gold, TextAlignmentOptions.TopLeft);
				}
				int sc = p2.score;
				Ui.Count(Ui.VTxt(r, "score", new Vector2(600f, 8f), new Vector2(480f, 50f), "", 38f, Color.white, TextAlignmentOptions.TopRight), 0f, sc, Ui.At(0.2f + k * 0.06f), 0.8f, (float v) => ((int)v).ToString("N0"));
				Ui.VTxt(r, "rate", new Vector2(600f, 56f), new Vector2(480f, 24f), p2.rate.ToString("0.00") + "%", 18f, me ? Color.white : Ui.Light, TextAlignmentOptions.TopRight);
				if (rowH > 100f)
				{
					RectTransform jb = Ui.Node(r, "judge", new Vector2(0f, 96f), new Vector2(LW, 42f));
					Ui.FillImg(jb, "bg", new Color(0f, 0f, 0f, 0.35f));
					string[] jl = new string[6] { "PERFECT", "GREAT", "GOOD", "BAD", "MISS", "MAX COMBO" };
					int[] jv = new int[6] { p2.perfect, p2.great, p2.good, p2.bad, p2.miss, p2.maxCombo };
					string[] jc = new string[6] { "#7FE7FF", "#58DEA6", "#FFD54A", "#FFA060", "#FF606A", "#FFFFFF" };
					for (int j = 0; j < 6; j++)
					{
						Ui.Txt(jb, "j" + j, new Vector2(30f + j * 178, 0f), new Vector2(178f, 42f), "<color=" + jc[j] + "><size=80%>" + jl[j] + "</size></color>  " + jv[j].ToString("N0"), 17f, Color.white, extra: false, TextAlignmentOptions.MidlineLeft);
					}
				}
			}
			Hotkeys("ENTER", "돌아가기");
		}

		// ---------------- 인게임 HUD ----------------
		private string HudSig()
		{
			return string.Concat(SceneLoader.Current, string.Join("", MP.Select((Player p) => p.loaded ? "1" : "0")), (Time.unscaledTime < leftUntil) ? leftNotice : "", paused, pauseSel, gameOver, connected, reconnecting, "|", released, "|", loadedCount, "/", totalCount, "|", (!released) ? Dots : "", "|", bat.pts[0], bat.pts[1], bat.round, "|#", string.Join(";", MP.Select((Player p) => p.id + ":" + (IsDuel ? "" : (p.score + ":" + p.combo + ":" + p.rate + ":" + p.brk)) + ":" + p.done + p.offline + p.diffMode)));
		}

		private SCENE hudScene;

		private float hudBuiltAt;


		private float hudAt;

		private void RebuildHud()
		{
			Ui.Build();
			if (SceneLoader.Current != hudScene)
			{
				hudScene = SceneLoader.Current;
				hudAt = Time.unscaledTime;
			}
			Ui.Clear(hud);
#if DEV
			if (devRank)
			{
				BuildGameResultRanking();
				return;
			}
#endif
			if (SceneLoader.Current == SCENE.Result)
			{
				BuildGameResultRanking();
				return;
			}
			if (!released && heldGen != null)
			{
				// 디맥처럼 누가 로딩 중인지 보여준다
				List<Player> mpl = MP;
				RectTransform wait = Ui.Node(hud, "wait", new Vector2(0f, 260f), new Vector2(760f, 84f + mpl.Count * 46f), Ui.TC, Ui.TC);
				Ui.FillImg(wait, "bg", new Color(0f, 0f, 0f, 0.85f));
				Ui.Img(wait, "accent", Vector2.zero, new Vector2(760f, 4f), Color.white, "Music_Choice_Effect");
				Ui.Txt(wait, "t", new Vector2(0f, 6f), new Vector2(760f, 70f), "다른 플레이어를 기다리는 중" + Dots + "   <color=#58DEA6>" + loadedCount + " / " + Mathf.Max(totalCount, 1) + "</color>", 26f, Color.white, extra: true, TextAlignmentOptions.Midline);
				for (int i = 0; i < mpl.Count; i++)
				{
					Player lp = mpl[i];
					bool ok = lp.loaded || lp.id == AI_ID;
					RectTransform r = Ui.Node(wait, "l" + i, new Vector2(40f, 76 + i * 46), new Vector2(680f, 40f));
					Ui.FillImg(r, "bg", new Color(1f, 1f, 1f, (lp.id == myId) ? 0.12f : 0.05f));
					Ui.Txt(r, "n", new Vector2(16f, 0f), new Vector2(400f, 40f), lp.name, 19f, Color.white, extra: true, TextAlignmentOptions.MidlineLeft);
					Ui.VTxt(r, "s", new Vector2(-16f, 0f), new Vector2(240f, 40f), lp.offline ? "OFFLINE" : (ok ? "READY" : ("LOADING" + Dots)), 16f, lp.offline ? Ui.Red : (ok ? Ui.Green : Ui.Gold), TextAlignmentOptions.MidlineRight, Ui.TR, Ui.TR);
				}
			}
			if (!connected)
			{
				RectTransform lost = Ui.Node(hud, "lost", new Vector2(0f, 24f), new Vector2(390f, 44f), Ui.TC, Ui.TC);
				Ui.FillImg(lost, "bg", new Color(0.35f, 0.05f, 0.08f, 0.85f));
				Ui.Txt(lost, "t", new Vector2(16f, 0f), new Vector2(370f, 44f), reconnecting ? ("연결이 끊겨 다시 접속하는 중" + Dots) : "방과 연결이 끊겼어요", 18f, Color.white, extra: false, TextAlignmentOptions.MidlineLeft);
			}
			// 다른 플레이어 연결 끊김 / 나감 알림 (위쪽 가운데)
			List<string> notes = MP.Where((Player o) => o.id != myId && o.offline && !o.done).Select((Player o) => o.name + " 님의 연결이 끊겼어요 · 재접속 기다리는 중").ToList();
			if (Time.unscaledTime < leftUntil)
			{
				notes.Add(leftNotice);
			}
			noteGroup = null;
			for (int n = 0; n < notes.Count; n++)
			{
				RectTransform note = Ui.Node(hud, "note" + n, new Vector2(0f, (connected ? 24f : 76f) + n * 50f), new Vector2(560f, 44f), Ui.TC, Ui.TC);
				Ui.In(note, Ui.Seen("note" + notes[n]), 0f, -30f, 1f, 0.3f, back: false, alpha: 1f);
				if (notes[n] == leftNotice && Time.unscaledTime < leftUntil)
				{
					noteGroup = note.gameObject.AddComponent<CanvasGroup>();
				}
				Ui.FillImg(note, "bg", new Color(0.35f, 0.05f, 0.08f, 0.88f));
				Ui.Img(note, "accent", Vector2.zero, new Vector2(5f, 44f), Ui.Red);
				Ui.Txt(note, "t", Vector2.zero, new Vector2(560f, 44f), notes[n], 18f, Color.white, extra: false, TextAlignmentOptions.Midline);
			}
			if (paused)
			{
				BuildPausePanel();
			}
			if (gameOver)
			{
				RectTransform go = Ui.Node(hud, "gameover", new Vector2(0f, 690f), new Vector2(600f, 70f), Ui.TC, Ui.TC);
				Slant(go, Color.white);
				Ui.Txt(go, "t", Vector2.zero, new Vector2(600f, 70f), (matchKind >= MT_VERSUS) ? "대전으로 돌아가기  ▶" : "방으로 돌아가기  ▶", 28f, Color.black, extra: true, TextAlignmentOptions.Midline);
				Ui.Txt(hud, "goSub", new Vector2(0f, 776f), new Vector2(900f, 30f), (matchKind >= MT_VERSUS) ? "ENTER  ·  게임 오버 시점 점수로 이번 라운드가 끝나요" : "ENTER  ·  다른 사람의 결과는 방에서 F11 로 볼 수 있어요", 19f, Ui.Light, extra: false, TextAlignmentOptions.Midline, Ui.TC, Ui.TC);
				return; // GAME OVER 글자를 가리지 않게 대전 패널은 숨김
			}
			BuildHudPanel();
		}

		// 온라인 일시정지: 내 곡만 멈추고 다른 플레이어는 계속 진행된다
		private void BuildPausePanel()
		{
			Ui.FillImg(hud, "dim", new Color(0f, 0f, 0f, 0.6f));
			RectTransform panel = Ui.Node(hud, "pause", new Vector2(0f, 300f), new Vector2(700f, 420f), Ui.TC, Ui.TC);
			Ui.FillImg(panel, "bg", new Color(0f, 0f, 0f, 0.88f));
			// 왁맥 팝업 모양: 얇은 회색 테두리
			Ui.Frame(panel, new Color(0.72f, 0.72f, 0.75f, 1f), 2f);
			Ui.VTxt(panel, "h", new Vector2(40f, 34f), new Vector2(620f, 50f), "PAUSE", 40f, Color.white, TextAlignmentOptions.TopLeft);
			Ui.Txt(panel, "n", new Vector2(42f, 92f), new Vector2(620f, 30f), "온라인 대전 중에는 다른 플레이어의 곡이 멈추지 않아요.", 18f, Sub, extra: false, TextAlignmentOptions.TopLeft);
			string[] items = new string[2] { "계속하기", "방으로 돌아가기 (기권)" };
			for (int i = 0; i < 2; i++)
			{
				RectTransform row = Ui.Node(panel, "row" + i, new Vector2(40f, 150 + i * 90), new Vector2(620f, 78f));
				if (i == pauseSel)
				{
					Ui.FillImg(row, "sel", Color.white, "Music_Choice_Effect");
				}
				else
				{
					Ui.FillImg(row, "bg", new Color(1f, 1f, 1f, 0.06f));
				}
				Ui.Txt(row, "t", new Vector2(30f, 0f), new Vector2(580f, 78f), items[i], 26f, Color.white, extra: false, TextAlignmentOptions.MidlineLeft);
			}
			Ui.HotkeyRightToLeft(panel, 40f, 362f, "ENTER", "선택", 18f);
			Ui.HotkeyLeftToRight(panel, 40f, 362f, "ESC", "계속하기", 18f);
		}

		// 게임 결과 화면 오른쪽 빈 공간(판정 목록과 캐릭터 사이)에 온라인 순위 / 라운드 결과
		private void BuildGameResultRanking()
		{
			List<Player> mp = MP;
			// 게임 결과 화면의 SCORE 숫자와 겹치지 않게: 오른쪽 끝, 내용 높이만큼만
			float ph = 62f + ((matchKind == MT_TEAM) ? 76f : 0f) + ((matchKind >= MT_VERSUS) ? 60f : 0f) + mp.Count * (((mp.Count > 4) ? 60f : 84f) + 6f) + 10f;
			// y 190: 게임의 "보너스 적용" 글씨(y 160) 아래
			RectTransform p = Ui.Node(hud, "ranking", new Vector2(1500f, 190f), new Vector2(400f, ph));
			Ui.FillImg(p, "bg", new Color(0f, 0f, 0f, 0.6f));
			Ui.Img(p, "top", Vector2.zero, new Vector2(400f, 4f), Color.white, "Music_Choice_Effect");
			Ui.In(p, hudAt + 0.4f, 120f, 0f, 1f, 0.5f);
			bool battle = matchKind >= MT_VERSUS;
			Ui.VTxt(p, "h", new Vector2(20f, 14f), new Vector2(360f, 30f), battle ? ("ROUND " + bat.round) : "ONLINE RANKING", 22f, Color.white, TextAlignmentOptions.TopLeft);
			if (battle)
			{
				// 라운드 점수는 방으로 돌아가야 매겨지므로 (ScoreRound) 이번 판 결과를 미리 더해서 보여준다. 내 쪽이 왼쪽
				int[] pts = { bat.pts[0], bat.pts[1] };
				Player pa = Find(bat.ids[0]);
				Player pb = Find(bat.ids[1]);
				if (!bat.scored && pa != null && pb != null && (pa.done || pa.offline) && (pb.done || pb.offline))
				{
					int sa = pa.offline ? 0 : pa.score;
					int sb = pb.offline ? 0 : pb.score;
					pts[0] += (sa >= sb) ? 1 : 0;
					pts[1] += (sb >= sa) ? 1 : 0;
				}
				int ms = Mathf.Max(0, MySlot);
				Ui.VTxt(p, "pts", new Vector2(-20f, 16f), new Vector2(200f, 30f), "MATCH " + pts[ms] + " : " + pts[1 - ms], 18f, Ui.Gold, TextAlignmentOptions.TopRight, Ui.TR, Ui.TR);
			}
			Ui.Img(p, "l", new Vector2(20f, 50f), new Vector2(360f, 1f), new Color(1f, 1f, 1f, 0.3f));
			bool all = mp.All((Player o) => o.done || o.offline);
			float y = 62f;
			if (matchKind == MT_TEAM)
			{
				int w = TeamWinner(mp);
				TextMeshProUGUI twt = Ui.VTxt(p, "tw", new Vector2(20f, y), new Vector2(360f, 34f), all ? ((w < 0) ? "DRAW" : (TeamName(w) + " WIN")) : ("집계 중" + Dots), 24f, (all && w >= 0) ? TeamColor(w) : Color.white, TextAlignmentOptions.TopLeft);
				if (all)
				{
					Ui.In(twt, Mathf.Max(hudAt + 0.7f, Ui.Seen("hudteamwin")), 0f, 0f, 1.7f, 0.4f, back: true);
				}
				string ts = string.Join("   ", from t in Enumerable.Range(0, 3)
					where TeamScore(mp, t) >= 0
					select "<color=" + Ui.Hex(TeamColor(t)) + ">" + (char)(65 + t) + " " + TeamScore(mp, t).ToString("N0") + "</color>");
				Ui.Txt(p, "ts", new Vector2(20f, y + 36f), new Vector2(360f, 24f), ts, 16f, Color.white, extra: false, TextAlignmentOptions.TopLeft);
				y += 76f;
			}
			if (battle && all && mp.Count == 2)
			{
				Player me = mp.FirstOrDefault((Player o) => o.id == myId);
				Player op = mp.FirstOrDefault((Player o) => o.id != myId);
				if (me != null && op != null)
				{
					string res = (me.score > op.score) ? "WIN" : ((me.score < op.score) ? "LOSE" : "DRAW");
						Ui.In(Ui.VTxt(p, "res", new Vector2(20f, y), new Vector2(360f, 50f), res, 44f, (res == "WIN") ? Ui.Gold : ((res == "LOSE") ? Ui.Gray : Color.white), TextAlignmentOptions.TopLeft), Mathf.Max(hudAt + 0.7f, Ui.Seen("hudres")), 0f, 0f, 1.7f, 0.4f, back: true);
					// 디맥처럼 점수 차이 (+는 이긴 쪽)
					int diff = me.score - op.score;
					Ui.VTxt(p, "diff", new Vector2(-20f, y + 10f), new Vector2(200f, 34f), ((diff >= 0) ? "+" : "-") + Mathf.Abs(diff).ToString("N0"), 26f, (diff >= 0) ? Color.white : Ui.Red, TextAlignmentOptions.TopRight, Ui.TR, Ui.TR);
					y += 60f;
				}
			}
			Player[] order = mp.OrderByDescending((Player o) => o.score).ToArray();
			float rh = (order.Length > 4) ? 60f : 84f;
			for (int k = 0; k < order.Length; k++)
			{
				Player p2 = order[k];
				int rank = order.Count((Player o) => o.score > p2.score);
				bool me2 = p2.id == myId;
				RectTransform r = Ui.Node(p, "r" + k, new Vector2(10f, y + k * (rh + 6f)), new Vector2(380f, rh));
				Ui.In(r, hudAt + 0.55f + k * 0.07f, 60f, 0f, 1f, 0.4f);
				if (me2)
				{
					Ui.FillImg(r, "sel", Color.white, "Music_Choice_Effect");
					Ui.Shine(r, 0.3f, 3f);
				}
				if (matchKind == MT_TEAM)
				{
					Ui.Img(r, "team", Vector2.zero, new Vector2(5f, rh), TeamColor(p2.team));
				}
				Ui.VTxt(r, "rank", new Vector2(14f, 0f), new Vector2(50f, rh), (rank + 1).ToString(), 30f, me2 ? Color.white : RankColor(rank), TextAlignmentOptions.MidlineLeft);
				Ui.Txt(r, "name", new Vector2(56f, 8f), new Vector2(180f, 28f), p2.name, 20f, Color.white, extra: true, TextAlignmentOptions.TopLeft).overflowMode = TextOverflowModes.Ellipsis;
				string tops = TopBadges(mp, p2);
				Ui.Txt(r, "state", new Vector2(56f, rh - 30f), new Vector2(200f, 22f), (tops.Length > 0) ? ("<color=#FFD54A>" + tops.Replace("  TOP ", " / ") + "</color>") : (StateText(p2) + "  <color=#FFFFFFA0>" + p2.matchDiff + "</color>"), 12f, Color.white, extra: false, TextAlignmentOptions.TopLeft).overflowMode = TextOverflowModes.Ellipsis;
				Ui.VTxt(r, "score", new Vector2(190f, 6f), new Vector2(176f, 32f), p2.score.ToString("N0"), 22f, Color.white, TextAlignmentOptions.TopRight);
				Ui.VTxt(r, "rate", new Vector2(190f, rh - 28f), new Vector2(176f, 22f), p2.rate.ToString("0.00") + "%", 14f, me2 ? Color.white : Ui.Light, TextAlignmentOptions.TopRight);
			}
		}
	}
}
