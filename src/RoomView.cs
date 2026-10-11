// 방 화면 (디맥 RESPECT V 오픈 매치 방과 같은 배치)
//   왼쪽 위 로고 / 왼쪽 = 곡 정보 패널(방 코드 · KEY 배너 · 자켓 · 난이도 버튼 · 레벨 별 · 기록 · 설정) + 채팅
//   가운데 = 방 머리(방식 · 방 이름 · 인원) + 정렬 + 카테고리 탭 + 곡 목록
//   오른쪽 = PLAYERS 슬롯 + START
// 대전(VERSUS·래더) 화면: 밴픽 → 라운드 공개 → 진행 중(실시간 비교) → 최종 결과
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using WJMAX.UserRecord;

namespace WJMAXOnline
{
	public partial class OnlineMod
	{
		private const float LPX = 54f; // 왼쪽 열

		private const float LPW = 516f;

		private const float CX = 636f; // 가운데 열

		private const float CW = 626f;

		private const float RX = 1306f; // 오른쪽 열

		private const float RW = 526f;

		private const int ROWS = 11; // 곡 목록 줄 수

		private bool Spectating => matchLive && !inMatch;

		private string RoomSig()
		{
			BaseMusicBundle song = RoomSong();
			string reason = isHost ? StartBlockReason() : "";
			string live = Spectating ? string.Join(";", MP.Select((Player p) => p.id + ":" + p.score / 500 + ":" + p.done + p.offline + p.phase)) : "";
			return string.Concat("|", cursor, "|", list.Length, "|", listCat, "|", GameValue.key, "|", BundlePackage.setting.GetSortText(isMain: true), "|", hostSel, songMissing, "|", (song != null) ? ((int)MyModeFor(song)) : (-1), BundlePackage.closet.isPlusMode, BundlePackage.setting.NoteSpeed.ToString("0.0"), "|", matchType, maxPlayers, "|", reason, "|", pcur, vsMin, ":", vsMax, vsEditMax, versusHard, "|", matchLive, live, "|", string.Join(";", players.Select((Player p) => p.id + p.name + p.inRoom + p.ready + p.phase + p.team + p.fighter + p.offline + p.ping / 30 + p.diffMode + p.diffKey + p.plus + p.speed10 + p.tier + p.lp + p.ach + p.level + p.icon + p.plate)));
		}

		private void BuildRoom()
		{
			BaseMusicBundle song = RoomSong();
			// 배경: 지금 곡 자켓을 크게 깔고 어둡게 (디맥 방 배경처럼)
			Image bg = Ui.Cover(body, "bgArt", Vector2.zero, new Vector2(1920f, 1080f), song?.Album ?? AnyJacket());
			bg.color = new Color(1f, 1f, 1f, 0.55f);
			// 배경 자켓이 아주 천천히 커졌다 작아졌다 (멈춘 그림처럼 안 보이게)
			Ui.Every(bg, delegate(float t)
			{
				bg.rectTransform.localScale = Vector3.one * (1.06f + 0.04f * Mathf.Sin(t * 0.15f));
				return false;
			}, 0f);
			Ui.FillImg(body, "bgShade", new Color(0.02f, 0.02f, 0.04f, 0.72f));
			Logo("OPEN MATCH");
			// 왼쪽
			switch (matchType)
			{
			case MT_VERSUS:
				VersusInfoPanel();
				break;
			default:
				SongInfoPanel(song);
				break;
			}
			ChatBox(new Vector2(LPX, 676f), new Vector2(LPW, 262f));
			// 가운데
			CenterHead();
			if (Spectating)
			{
				LivePanel();
			}
			else if (matchType == MT_VERSUS)
			{
				DuelCenter();
			}
			else
			{
				SortRow();
				CategoryTabs();
				SongList();
			}
			// 오른쪽
			PlayersPanel();
			StartButton();
			RoomHotkeys();
		}

		// ---------------- 왼쪽: 곡 정보 ----------------
		private RectTransform LeftPanelFrame()
		{
			RectTransform p = Ui.Node(body, "left", new Vector2(LPX, 88f), new Vector2(LPW, 580f));
			Ui.FillImg(p, "bg", new Color(0f, 0f, 0f, 0.62f));
			Ui.In(p, Ui.At(0.05f), -60f, 0f, 1f, 0.45f);
			// 맨 위 띠: 방 코드 + F1 복사
			RectTransform strip = Ui.Node(p, "strip", Vector2.zero, new Vector2(LPW, 32f));
			Ui.FillImg(strip, "bg", new Color(0f, 0f, 0f, 0.8f));
			Ui.Txt(strip, "code", new Vector2(12f, 0f), new Vector2(380f, 32f), "<color=#FFD54A>ROOM CODE</color>   " + roomCode, 16f, Color.white, extra: false, TextAlignmentOptions.MidlineLeft);
			Ui.HotkeyRightToLeft(strip, 6f, 0f, "F1", "복사", 14f);
			return p;
		}

		private void SongInfoPanel(BaseMusicBundle song)
		{
			RectTransform p = LeftPanelFrame();
			KEY key = GameValue.key;
			// KEY 배너
			RectTransform banner = Ui.Node(p, "banner", new Vector2(0f, 32f), new Vector2(LPW, 84f));
			Ui.FillImg(banner, "bg", (key == KEY.Key6) ? new Color32(120, 60, 200, byte.MaxValue) : new Color32(20, 150, 120, byte.MaxValue));
			Ui.FillImg(banner, "fx", new Color(1f, 1f, 1f, 0.35f), "Music_Choice_Effect");
			Ui.VTxt(banner, "k", new Vector2(18f, 0f), new Vector2(80f, 84f), (key == KEY.Key6) ? "6" : ((key == KEY.Key5) ? "5" : "4"), 72f, Color.white, TextAlignmentOptions.MidlineLeft);
			Ui.VTxt(banner, "kt", new Vector2(80f, 16f), new Vector2(200f, 56f), "BUTTON\n<size=80%>TUNES</size>", 24f, Color.white, TextAlignmentOptions.TopLeft).lineSpacing = -10f;
			if (song == null)
			{
				string root = hostSel.Split('|')[0];
				RectTransform none = Ui.Node(p, "none", new Vector2(0f, 116f), new Vector2(LPW, 456f));
				Ui.Txt(none, "t", new Vector2(24f, 40f), new Vector2(LPW - 48f, 40f), (isHost || root.Length == 0) ? "곡을 고르는 중" : root, 30f, Color.white, extra: true, TextAlignmentOptions.TopLeft).overflowMode = TextOverflowModes.Ellipsis;
				if (!isHost && songMissing)
				{
					TextMeshProUGUI m = Ui.Txt(none, "m", new Vector2(24f, 96f), new Vector2(LPW - 48f, 120f), "방장이 고른 곡이 내 게임에 없어요.\n이번 곡은 관전하면서 실시간 점수를 볼 수 있어요.", 19f, Ui.Gold, extra: false, TextAlignmentOptions.TopLeft);
					m.enableWordWrapping = true;
					m.lineSpacing = 10f;
				}
				return;
			}
			Ui.Txt(banner, "cat", new Vector2(-16f, 0f), new Vector2(240f, 84f), BaseMusicBundle.ToKorea(song.info.musicCategory), 22f, Color.white, extra: true, TextAlignmentOptions.MidlineRight, Ui.TR, Ui.TR);
			// 자켓 + 제목
			const float JY = 116f;
			const float JH = 250f;
			RectTransform jclip = Ui.Node(p, "jclip", new Vector2(0f, JY), new Vector2(LPW, JH));
			jclip.gameObject.AddComponent<RectMask2D>();
			RectTransform jg = Ui.Node(jclip, "jg", Vector2.zero, new Vector2(LPW, JH));
			animJacket = jg;
			animJacketGroup = jg.gameObject.AddComponent<CanvasGroup>();
			Ui.Cover(jg, "jacket", Vector2.zero, new Vector2(LPW, JH), song.Album);
			Image fade = Ui.Img(jg, "jfade", Vector2.zero, new Vector2(LPW, JH), new Color(0f, 0f, 0f, 0.82f));
			fade.sprite = Ui.HFade();
			Ui.Txt(jg, "title", new Vector2(22f, 108f), new Vector2(LPW - 44f, 54f), song.title, 40f, Color.white, extra: true, TextAlignmentOptions.TopLeft).overflowMode = TextOverflowModes.Ellipsis;
			Ui.Txt(jg, "artist", new Vector2(24f, 162f), new Vector2(LPW - 48f, 28f), song.artist ?? "", 19f, Ui.Light, extra: false, TextAlignmentOptions.TopLeft).overflowMode = TextOverflowModes.Ellipsis;
			Ui.VTxt(jg, "bpm", new Vector2(24f, 196f), new Vector2(300f, 26f), "BPM " + song.info.GetBpmString(), 17f, Ui.Light, TextAlignmentOptions.TopLeft);
			System.TimeSpan ts = System.TimeSpan.FromSeconds(song.info.time);
			RectTransform tm = Ui.Node(p, "time", new Vector2(LPW - 78f, JY + JH - 36f), new Vector2(66f, 26f));
			Ui.FillImg(tm, "bg", new Color(0f, 0f, 0f, 0.75f));
			Ui.VTxt(tm, "t", Vector2.zero, new Vector2(66f, 26f), ts.Minutes + ":" + ts.Seconds.ToString("00"), 15f, Color.white, TextAlignmentOptions.Midline);
			// 난이도 버튼 (게임 난이도 버튼 그림)
			MODE mode = MyModeFor(song);
			List<int> modes = Enumerable.Range(0, (int)MODE.COUNT).Where((int m) => song.IsContainsMode(key, (MODE)m)).ToList();
			float bw = Mathf.Min(118f, (LPW - 32f - (modes.Count - 1) * 8f) / Mathf.Max(1, modes.Count));
			for (int i = 0; i < modes.Count; i++)
			{
				int m = modes[i];
				bool on = m == (int)mode;
				Image b = Ui.Img(p, "mode" + m, new Vector2(16f + i * (bw + 8f), JY + JH + 16f), new Vector2(bw, 36f), on ? Color.white : new Color(1f, 1f, 1f, 0.7f), on ? ModeSprite(m) : "mode_unselected", on ? Image.Type.Simple : Image.Type.Sliced);
				Ui.VTxt(b.transform, "t", Vector2.zero, new Vector2(bw, 36f), ModeEng(m), 14f, on ? Color.white : new Color(1f, 1f, 1f, 0.75f), TextAlignmentOptions.Midline);
			}
			// 레벨 별
			float lv = LevelOf(song, key, (int)mode);
			LevelIcons(p, new Vector2(12f, JY + JH + 60f), Mathf.FloorToInt(lv), 400f);
			Ui.VTxt(p, "lv", new Vector2(-16f, JY + JH + 62f), new Vector2(100f, 28f), (lv > 0f) ? ("LV." + Mathf.FloorToInt(lv)) : "", 18f, ModeColor((int)mode), TextAlignmentOptions.TopRight, Ui.TR, Ui.TR);
			// 내 기록
			HighScoreData hs = null;
			try
			{
				hs = BundlePackage.user.GetHighScore(song.RootFileName, mode, BundlePackage.closet.isPlusMode);
			}
			catch (System.Exception)
			{
			}
			int combo = 0;
			try
			{
				song.ChangeMode(key, mode);
				combo = song.GetSheetPack(key)?.combo ?? 0;
			}
			catch (System.Exception)
			{
			}
			string[] lbl = new string[3] { "Record", "Rate", "Combo" };
			string[] val = new string[3]
			{
				(hs != null) ? hs.noteScore.GetRecordScore().ToString("N0") : "0",
				(hs != null) ? (hs.noteScore.rate.ToRateString() + "%") : "0.00%",
				combo.ToString("N0")
			};
			for (int c = 0; c < 3; c++)
			{
				Ui.VTxt(p, "rl" + c, new Vector2(18f + c * 162f, JY + JH + 100f), new Vector2(160f, 20f), lbl[c], 14f, Ui.Gold, TextAlignmentOptions.TopLeft);
				Ui.VTxt(p, "rv" + c, new Vector2(18f + c * 162f, JY + JH + 120f), new Vector2(160f, 30f), val[c], 22f, Color.white, TextAlignmentOptions.TopLeft);
			}
			// 설정 상자 (속도 · 판정 · FC)
			float y = JY + JH + 160f;
			SettingBox(p, new Vector2(16f, y), "SPEED", BundlePackage.setting.NoteSpeed.ToString("0.0"), new Color32(215, 25, 63, byte.MaxValue));
			SettingBox(p, new Vector2(98f, y), "JUDGE", BundlePackage.closet.isPlusMode ? "PLUS" : "NORMAL", BundlePackage.closet.isPlusMode ? new Color32(150, 60, 220, byte.MaxValue) : new Color32(60, 60, 70, byte.MaxValue));
			if (hs != null && hs.isFullCombo)
			{
				Image fc = Ui.Img(p, "fc", new Vector2(180f, y + 8f), new Vector2(84f, 42f), Color.white, "FullCombo");
				fc.preserveAspect = true;
			}
			Ui.Txt(p, "keys", new Vector2(-16f, y + 6f), new Vector2(240f, 50f), "<color=#FFD54A>CTRL</color> 게임 설정   <color=#FFD54A>SPACE</color> 옷장\n<color=#FFD54A>1 / 2</color> 배속", 14f, Ui.Light, extra: false, TextAlignmentOptions.TopRight, Ui.TR, Ui.TR).lineSpacing = 8f;
		}

		// 게임 곡 정보의 레벨 표시와 같은 아이콘: 20렙까지는 20칸(5칸마다 메시→엔젤→왁굳→민수 그림), 21렙부터는 거짓말 그림 10칸
		private static void LevelIcons(RectTransform p, Vector2 pos, int level, float width, float iconSize = 0f)
		{
			bool hard = level > 20;
			int from = hard ? 20 : 0;
			int n = hard ? 10 : 20;
			string[] names = new string[6] { "LevelIcon1_Messi", "LevelIcon2_Angel", "LevelIcon3_Wakgood", "LevelIcon4_Minsu", "LevelIcon5_Lie", "LevelIcon6_Lie" };
			float step = width / n;
			float size = (iconSize > 0f) ? (hard ? iconSize * 1.3f : iconSize) : (hard ? 34f : 26f);
			for (int i = 0; i < n; i++)
			{
				int idx = from + i;
				string sp = names[Mathf.Clamp(idx / 5, 0, 5)] + ((idx < level) ? "_On" : "_Off");
				Image im = Ui.Img(p, "lvi" + i, new Vector2(pos.x + i * step + (step - size) / 2f, pos.y + (30f - size) / 2f), new Vector2(size, size), Color.white, sp);
				im.preserveAspect = true;
			}
		}

		private static void SettingBox(RectTransform p, Vector2 pos, string label, string value, Color c)
		{
			RectTransform b = Ui.Node(p, "set" + label, pos, new Vector2(74f, 58f));
			Ui.FillImg(b, "bg", c);
			Ui.VTxt(b, "l", new Vector2(0f, 4f), new Vector2(74f, 16f), label, 11f, new Color(1f, 1f, 1f, 0.8f), TextAlignmentOptions.Top);
			Ui.VTxt(b, "v", new Vector2(0f, 20f), new Vector2(74f, 34f), value, (value.Length > 4) ? 15f : 24f, Color.white, TextAlignmentOptions.Midline);
		}

		// 버서스 방 왼쪽: 규칙 + 관전자
		private void VersusInfoPanel()
		{
			RectTransform p = LeftPanelFrame();
			RectTransform head = Ui.Node(p, "head", new Vector2(0f, 32f), new Vector2(LPW, 120f));
			Ui.FillImg(head, "bg", new Color32(120, 50, 10, 230));
			Ui.FillImg(head, "fx", new Color(1f, 1f, 1f, 0.25f), "Music_Choice_Effect");
			Ui.VTxt(head, "t", new Vector2(24f, 22f), new Vector2(LPW, 50f), "VERSUS MATCH", 40f, Color.white, TextAlignmentOptions.TopLeft);
			Ui.Txt(head, "s", new Vector2(26f, 74f), new Vector2(LPW, 30f), "대전자 2명 · 관전자 최대 6명", 19f, Ui.Light, extra: false, TextAlignmentOptions.TopLeft);
			TextMeshProUGUI r = Ui.Txt(p, "rule", new Vector2(26f, 176f), new Vector2(LPW - 52f, 260f), "· 래더 매치와 같은 규칙 (LP 변동 없음)\n· 곡 후보 5개 중 대전자가 1곡씩 밴\n· 2판 2선승, 동점이면 하드 판정 타이브레이커\n· 관전자는 실시간 점수를 보며 채팅할 수 있어요\n· 방장: ↑↓ 로 사람 고르고 F6 으로 대전자 지정", 18f, Ui.Light, extra: false, TextAlignmentOptions.TopLeft);
			r.lineSpacing = 14f;
			int spec = players.Count((Player x) => !x.fighter);
			Ui.VTxt(p, "spec", new Vector2(26f, 410f), new Vector2(LPW, 40f), "SPECTATORS  " + spec, 26f, Color.white, TextAlignmentOptions.TopLeft);
			// 관전자 이름 (빈 칸 대신)
			List<Player> sp = players.Where((Player x) => !x.fighter).ToList();
			for (int i = 0; i < sp.Count && i < 5; i++)
			{
				RectTransform row = Ui.Node(p, "sp" + i, new Vector2(26f, 456f + i * 38f), new Vector2(LPW - 52f, 34f));
				Ui.In(row, Mathf.Max(Ui.At(0.2f + i * 0.05f), Ui.Seen("spec" + sp[i].id)), -30f);
				Ui.FillImg(row, "bg", new Color(1f, 1f, 1f, (sp[i].id == myId) ? 0.16f : 0.06f));
				string nm = sp[i].name + ((i == 4 && sp.Count > 5) ? ("  <color=#FFFFFF80>외 " + (sp.Count - 4) + "명</color>") : "");
				Ui.Txt(row, "n", new Vector2(12f, 0f), new Vector2(LPW - 80f, 34f), nm, 17f, Color.white, extra: true, TextAlignmentOptions.MidlineLeft).overflowMode = TextOverflowModes.Ellipsis;
			}
		}

		// ---------------- 왼쪽 아래: 채팅 ----------------
		private void ChatBox(Vector2 pos, Vector2 size)
		{
			RectTransform c = Ui.Node(body, "chat", pos, size);
			Ui.FillImg(c, "bg", new Color(0f, 0f, 0f, 0.62f));
			Ui.In(c, Ui.At(0.12f), -60f, 0f, 1f, 0.45f);
			// 오른쪽 스크롤 막대 장식 (디맥 채팅창)
			Ui.Img(c, "sb", new Vector2(size.x - 16f, 8f), new Vector2(8f, size.y - 60f), new Color(1f, 1f, 1f, 0.12f));
			Ui.Img(c, "sbt", new Vector2(size.x - 16f, size.y - 52f - 40f), new Vector2(8f, 40f), new Color(1f, 1f, 1f, 0.35f));
			float lineH = 25f;
			int max = Mathf.FloorToInt((size.y - 56f) / lineH);
			List<string> lines = chatLog.Skip(Mathf.Max(0, chatLog.Count - max)).ToList();
			for (int i = 0; i < lines.Count; i++)
			{
				string[] l = lines[i].Split(new char[1] { '|' }, 2);
				string text = (l.Length < 2) ? l[0] : ((l[0].Length == 0) ? ("<color=#FFD54A>" + l[1] + "</color>") : ("<color=#7FE7FF>" + l[0] + "</color>  " + l[1]));
				TextMeshProUGUI lt = Ui.Txt(c, "l" + i, new Vector2(14f, size.y - 52f - (lines.Count - i) * lineH), new Vector2(size.x - 40f, lineH), text, 16f, Color.white, extra: false, TextAlignmentOptions.MidlineLeft);
				lt.overflowMode = TextOverflowModes.Ellipsis;
				if (i == lines.Count - 1)
				{
					// 새 채팅은 옆에서 미끄러져 들어옴
					Ui.In(lt, Ui.Seen("chat" + chatLog.Count), -24f, 0f, 1f, 0.3f);
				}
			}
			RectTransform input = Ui.Node(c, "input", new Vector2(8f, size.y - 44f), new Vector2(size.x - 16f, 36f));
			Ui.FillImg(input, "bg", chatting ? new Color(1f, 1f, 1f, 0.14f) : new Color(1f, 1f, 1f, 0.05f));
			if (chatting)
			{
				Ui.Frame(input, Ui.Gold, 2f);
				Ui.Txt(input, "t", new Vector2(10f, 0f), new Vector2(size.x - 40f, 36f), chatText + "<u>" + Input.compositionString + "</u>" + Caret, 17f, Color.white, extra: false, TextAlignmentOptions.MidlineLeft).overflowMode = TextOverflowModes.Ellipsis;
			}
			else
			{
				Ui.Txt(input, "t", new Vector2(10f, 0f), new Vector2(size.x - 40f, 36f), "F3 을 눌러 채팅", 16f, new Color(1f, 1f, 1f, 0.4f), extra: false, TextAlignmentOptions.MidlineLeft);
			}
			Ui.VTxt(input, "enter", new Vector2(-8f, 0f), new Vector2(60f, 36f), "ENTER", 12f, new Color(1f, 1f, 1f, 0.5f), TextAlignmentOptions.MidlineRight, Ui.TR, Ui.TR);
		}

		// ---------------- 가운데 ----------------
		private void CenterHead()
		{
			RectTransform h = Ui.Node(body, "head", new Vector2(CX, 22f), new Vector2(CW, 52f));
			Ui.In(h, Ui.At(0.05f), 0f, -24f);
			Color tc = MatchTypeColor(matchType);
			Image disc = Ui.Img(h, "disc", new Vector2(0f, 4f), new Vector2(44f, 44f), tc);
			disc.sprite = Ui.Disc();
			Ui.VTxt(disc.transform, "l", Vector2.zero, new Vector2(44f, 44f), MatchTypeName(matchType).Substring(0, 1), 26f, Color.white, TextAlignmentOptions.Midline);
			string[] tn = MatchTypeName(matchType).Split(' ');
			Ui.VTxt(h, "tn", new Vector2(52f, 6f), new Vector2(110f, 44f), "<color=" + Ui.Hex(tc) + ">" + tn[0] + "</color>\n" + tn[1], 16f, Color.white, TextAlignmentOptions.TopLeft).lineSpacing = -14f;
			RectTransform bar = Ui.Node(h, "bar", new Vector2(160f, 8f), new Vector2(CW - 160f - 96f, 38f));
			Ui.FillImg(bar, "bg", new Color(0f, 0f, 0f, 0.6f));
			Player host = Find(0);
			Ui.Txt(bar, "t", new Vector2(14f, 0f), new Vector2(CW - 290f, 38f), (host?.name ?? "?") + "'s Room", 20f, Color.white, extra: false, TextAlignmentOptions.MidlineLeft);
			Ui.VTxt(h, "n", new Vector2(0f, 4f), new Vector2(90f, 44f), players.Count + "/" + maxPlayers, 28f, Color.white, TextAlignmentOptions.MidlineRight, Ui.TR, Ui.TR);
		}

		private void SortRow()
		{
			RectTransform r = Ui.Node(body, "sort", new Vector2(CX, 84f), new Vector2(CW, 30f));
			Ui.In(r, Ui.At(0.1f), 0f, -12f);
			if (isHost)
			{
				Ui.HotkeyLeftToRight(r, 0f, -2f, "L.SHIFT", "", 12f);
				Ui.HotkeyRightToLeft(r, 0f, -2f, "R.SHIFT", "", 12f);
				TextMeshProUGUI t = Ui.Txt(r, "t", Vector2.zero, new Vector2(CW, 30f), "<color=#AFAFAF>정렬 :</color>  " + BundlePackage.setting.GetSortText(isMain: true), 17f, Color.white, extra: false, TextAlignmentOptions.Midline);
				float w = Ui.Width(t, t.text);
				Ui.HotkeyLeftToRight(r, CW / 2f + w / 2f + 10f, -2f, "F4", "", 12f);
			}
			else
			{
				Ui.Txt(r, "t", Vector2.zero, new Vector2(CW, 30f), songMissing ? "<color=#FF606A>방장이 고른 곡이 내 게임에 없어요 (관전)</color>" : "<color=#AFAFAF>곡은 방장이 골라요 · 난이도는</color>  ←→", 17f, Color.white, extra: false, TextAlignmentOptions.Midline);
			}
		}

		private void CategoryTabs()
		{
			List<FREESTYLE_CATEGORY> cats = new List<FREESTYLE_CATEGORY>();
			for (FREESTYLE_CATEGORY c = FREESTYLE_CATEGORY.FAVORITY; c < FREESTYLE_CATEGORY.COUNT; c++)
			{
				if (c == listCat || BundlePackage.musicBundle.GetMusicBundles(c, GameValue.key).Any((BaseMusicBundle b) => !b.IsRandom))
				{
					cats.Add(c);
				}
			}
			int at = cats.IndexOf(listCat);
			const int SHOW = 5;
			float tw = CW / SHOW;
			RectTransform bar = Ui.Node(body, "tabs", new Vector2(CX, 122f), new Vector2(CW, 38f));
			Ui.FillImg(bar, "bg", new Color(0f, 0f, 0f, 0.6f));
			Ui.In(bar, Ui.At(0.12f), 0f, -12f);
			int first = Mathf.Clamp(at - SHOW / 2, 0, Mathf.Max(0, cats.Count - SHOW));
			for (int i = 0; i < SHOW && first + i < cats.Count; i++)
			{
				FREESTYLE_CATEGORY c2 = cats[first + i];
				bool on = c2 == listCat;
				RectTransform tab = Ui.Node(bar, "tab" + i, new Vector2(i * tw, 0f), new Vector2(tw, 38f));
				if (on)
				{
					Slant(tab, Color.white);
					Ui.In(tab, Ui.Seen("tab" + c2), 0f, 0f, 0.85f, 0.25f, back: true, alpha: 1f);
				}
				Ui.Txt(tab, "t", Vector2.zero, new Vector2(tw, 38f), CategoryManager.ConvertCategory(c2), 18f, on ? Color.black : new Color32(byte.MaxValue, 213, 74, 200), extra: true, TextAlignmentOptions.Midline);
			}
		}

		private static Color CategoryColor(MUSIC_CATEGORY c)
		{
			return c switch
			{
				MUSIC_CATEGORY.ISEDOL => new Color32(byte.MaxValue, 110, 170, byte.MaxValue),
				MUSIC_CATEGORY.WAKGOOD => new Color32(80, 200, 120, byte.MaxValue),
				MUSIC_CATEGORY.WAKTAVERSE => new Color32(90, 170, byte.MaxValue, byte.MaxValue),
				MUSIC_CATEGORY.MIDGARD => new Color32(170, 120, byte.MaxValue, byte.MaxValue),
				MUSIC_CATEGORY.FANMADE => new Color32(byte.MaxValue, 196, 74, byte.MaxValue),
				MUSIC_CATEGORY.SHORT => new Color32(byte.MaxValue, 130, 70, byte.MaxValue),
				_ => new Color32(120, 220, 230, byte.MaxValue),
			};
		}

		private void SongList()
		{
			const float Y0 = 168f;
			const float RH = 66f;
			const float GAP = 3f;
			RectTransform clip = Ui.Node(body, "listClip", new Vector2(CX, Y0), new Vector2(CW, ROWS * (RH + GAP)));
			clip.gameObject.AddComponent<RectMask2D>();
			RectTransform box = Ui.Node(clip, "list", Vector2.zero, new Vector2(CW, ROWS * (RH + GAP)));
			animList = box;
			if (list.Length == 0)
			{
				Ui.Txt(box, "empty", Vector2.zero, new Vector2(CW, 200f), "곡이 없어요", 22f, Sub, extra: false, TextAlignmentOptions.Midline);
				return;
			}
			bool wrap = list.Length >= ROWS;
			for (int i = 0; i < ROWS; i++)
			{
				int idx = cursor + i - ROWS / 2;
				if (wrap)
				{
					idx = (int)Mathf.Repeat(idx, list.Length);
				}
				else if (idx < 0 || idx >= list.Length)
				{
					continue;
				}
				BaseMusicBundle b = list[idx];
				bool on = idx == cursor && !(songMissing && !isHost);
				RectTransform r = Ui.Node(box, "r" + i, new Vector2(0f, i * (RH + GAP)), new Vector2(CW - 12f, RH));
				Ui.In(r, Ui.At(0.12f + i * 0.03f), 60f, 0f, 1f, 0.4f);
				if (on)
				{
					Ui.FillImg(r, "sel", Color.white, "Music_Choice_Effect");
					Ui.Shine(r, 0.45f);
				}
				else
				{
					Ui.FillImg(r, "bg", new Color(0f, 0f, 0f, 0.55f));
				}
				Ui.Img(r, "stripe", Vector2.zero, new Vector2(6f, RH), CategoryColor(b.info.musicCategory));
				Ui.Cover(r, "jk", new Vector2(12f, 5f), new Vector2(RH - 10f, RH - 10f), b.Album);
				Ui.Txt(r, "t", new Vector2(RH + 14f, 7f), new Vector2(CW - RH - 110f, 32f), b.title, on ? 24f : 22f, Color.white, extra: true, TextAlignmentOptions.TopLeft).overflowMode = TextOverflowModes.Ellipsis;
				Ui.Txt(r, "a", new Vector2(RH + 15f, 40f), new Vector2(CW - RH - 110f, 20f), b.artist ?? "", 14f, on ? Color.white : Sub, extra: false, TextAlignmentOptions.TopLeft).overflowMode = TextOverflowModes.Ellipsis;
				if (b is SoloMusicBundle solo)
				{
					Ui.VTxt(r, "lv", new Vector2(-12f, 0f), new Vector2(120f, RH), ModeEng((int)solo.ApplyMode) + " " + Mathf.FloorToInt(solo.forceLevel), 14f, ModeColor((int)solo.ApplyMode), TextAlignmentOptions.MidlineRight, Ui.TR, Ui.TR);
				}
			}
			// 스크롤 막대
			Ui.Img(box, "sb", new Vector2(CW - 5f, 0f), new Vector2(4f, ROWS * (RH + GAP)), new Color(1f, 1f, 1f, 0.12f));
			float th = Mathf.Max(30f, ROWS * (RH + GAP) * Mathf.Min(1f, (float)ROWS / list.Length));
			Ui.Img(box, "sbt", new Vector2(CW - 5f, (ROWS * (RH + GAP) - th) * cursor / Mathf.Max(1, list.Length - 1)), new Vector2(4f, th), new Color(1f, 1f, 1f, 0.6f));
		}

		// 관전 중: 실시간 점수
		private void LivePanel()
		{
			List<Player> mp = MP;
			RectTransform head = Ui.Node(body, "live", new Vector2(CX, 84f), new Vector2(CW, 76f));
			Ui.FillImg(head, "bg", new Color(0f, 0f, 0f, 0.7f));
			Ui.In(head, Ui.At(0.05f), 0f, -24f);
			RectTransform pill = Ui.Node(head, "pill", new Vector2(14f, 22f), new Vector2(70f, 32f));
			Ui.Pulse(Ui.FillImg(pill, "bg", new Color32(215, 25, 63, byte.MaxValue)), 0.55f, 1f, 1.2f);
			Ui.VTxt(pill, "t", Vector2.zero, new Vector2(70f, 32f), "LIVE", 18f, Color.white, TextAlignmentOptions.Midline);
			Ui.Txt(head, "song", new Vector2(100f, 10f), new Vector2(CW - 120f, 34f), matchTitle, 24f, Color.white, extra: true, TextAlignmentOptions.TopLeft).overflowMode = TextOverflowModes.Ellipsis;
			Ui.Txt(head, "sub", new Vector2(101f, 46f), new Vector2(CW - 120f, 22f), (mp.Any((Player o) => o.id == myId) ? "다른 플레이어가 끝나길 기다리는 중 · 실시간 점수" : "관전 중 · 곡이 끝나면 다음 곡에 참가할 수 있어요"), 15f, Sub, extra: false, TextAlignmentOptions.TopLeft);
			if (IsDuel)
			{
				BuildMatchPanel(body, ingame: false, new Vector2(CX + (CW - 420f) / 2f, 178f));
				Ui.In(body.Find("match"), Ui.At(0.12f), 0f, 50f, 1f, 0.45f);
				return;
			}
			if (matchKind == MT_TEAM)
			{
				BuildTeamPanel(body, ingame: false, new Vector2(CX + (CW - 420f) / 2f, 178f));
				Ui.In(body.Find("team"), Ui.At(0.12f), 0f, 50f, 1f, 0.45f);
				return;
			}
			Player[] order = mp.OrderByDescending((Player p) => p.score).ToArray();
			int top = Mathf.Max(1, order.Length > 0 ? order[0].score : 1);
			for (int k = 0; k < order.Length; k++)
			{
				Player p2 = order[k];
				RectTransform r = Ui.Node(body, "lr" + k, new Vector2(CX, 178 + k * 92), new Vector2(CW, 84f));
				Ui.In(r, Ui.At(0.12f + k * 0.05f), 60f, 0f, 1f, 0.4f);
				Ui.FillImg(r, "bg", new Color(0f, 0f, 0f, 0.6f));
				Ui.Img(r, "bar", Vector2.zero, new Vector2((CW - 4f) * p2.score / Mathf.Max(top, 1), 84f), new Color(1f, 1f, 1f, 0.08f));
				Ui.VTxt(r, "rank", new Vector2(16f, 0f), new Vector2(60f, 84f), (k + 1).ToString(), 36f, RankColor(k), TextAlignmentOptions.MidlineLeft);
				Ui.Txt(r, "name", new Vector2(70f, 12f), new Vector2(280f, 30f), p2.name, 22f, Color.white, extra: true, TextAlignmentOptions.TopLeft).overflowMode = TextOverflowModes.Ellipsis;
				Ui.Txt(r, "st", new Vector2(70f, 48f), new Vector2(300f, 22f), (p2.done ? StateText(p2) : PhaseText(p2)) + "  <color=#FFFFFFA0>" + p2.matchDiff + "</color>", 15f, Color.white, extra: false, TextAlignmentOptions.TopLeft);
				Ui.VTxt(r, "sc", new Vector2(-16f, 10f), new Vector2(260f, 36f), p2.score.ToString("N0"), 28f, Color.white, TextAlignmentOptions.TopRight, Ui.TR, Ui.TR);
				Ui.VTxt(r, "rt", new Vector2(-16f, 50f), new Vector2(260f, 22f), p2.rate.ToString("0.00") + "%  <color=#FF606A>BREAK " + p2.brk + "</color>", 15f, Ui.Light, TextAlignmentOptions.TopRight, Ui.TR, Ui.TR);
			}
		}

		// 버서스·래더 방 가운데: 대전자 카드 + 설정
		private void DuelCenter()
		{
			const bool lad = false;
			List<Player> f = players.Where((Player p) => p.fighter).Take(2).ToList();
			RectTransform head = Ui.Node(body, "dh", new Vector2(CX, 84f), new Vector2(CW, 44f));
			Ui.FillImg(head, "bg", new Color(0f, 0f, 0f, 0.6f));
			Ui.VTxt(head, "t", Vector2.zero, new Vector2(CW, 44f), lad ? "LADDER MATCH  ·  1 vs 1" : "VERSUS MATCH  ·  FIGHTERS", 22f, Color.white, TextAlignmentOptions.Midline);
			for (int i = 0; i < 2; i++)
			{
				Player p = (i < f.Count) ? f[i] : null;
				RectTransform card = Ui.Node(body, "fc" + i, new Vector2(CX + i * (CW / 2f + 6f), 144f), new Vector2(CW / 2f - 6f, 330f));
				Ui.FillImg(card, "bg", new Color(0f, 0f, 0f, 0.7f));
				Ui.In(card, Ui.At(0.1f + i * 0.08f), (i == 0) ? -70f : 70f, 0f, 1f, 0.45f);
				Ui.Img(card, "top", Vector2.zero, new Vector2(CW / 2f - 6f, 6f), (i == 0) ? Blue : Orange);
				if (p == null)
				{
					Ui.Txt(card, "w", Vector2.zero, new Vector2(CW / 2f - 6f, 330f), "상대를 기다리는 중" + Dots, 20f, Sub, extra: false, TextAlignmentOptions.Midline);
					continue;
				}
				Image icon = Ui.Img(card, "icon", new Vector2((CW / 2f - 6f) / 2f - 60f, 26f), new Vector2(120f, 120f), Color.white);
				icon.sprite = IconSprite(p.icon);
				icon.preserveAspect = true;
				if (icon.sprite == null)
				{
					icon.color = new Color(1f, 1f, 1f, 0.12f);
				}
				Ui.Txt(card, "name", new Vector2(0f, 156f), new Vector2(CW / 2f - 6f, 36f), p.name, 26f, Color.white, extra: true, TextAlignmentOptions.Top).overflowMode = TextOverflowModes.Ellipsis;
				Ui.VTxt(card, "lv", new Vector2(0f, 194f), new Vector2(CW / 2f - 6f, 26f), "LV " + p.level, 20f, Ui.Light, TextAlignmentOptions.Top);
				Ui.VTxt(card, "tier", new Vector2(0f, 230f), new Vector2(CW / 2f - 6f, 36f), TierText(p.tier), 28f, TierColor(p.tier), TextAlignmentOptions.Top);
				Ui.VTxt(card, "lp", new Vector2(0f, 268f), new Vector2(CW / 2f - 6f, 26f), p.tier + " PT", 18f, Ui.Light, TextAlignmentOptions.Top);
				Ui.VTxt(card, "rd", new Vector2(0f, 298f), new Vector2(CW / 2f - 6f, 26f), (p.id == 0) ? "HOST" : (p.ready ? "READY" : "-"), 18f, p.ready ? Ui.Gold : Sub, TextAlignmentOptions.Top);
			}
			RectTransform vs = Ui.Node(body, "vs", new Vector2(CX + CW / 2f - 44f, 270f), new Vector2(88f, 88f));
			Image vd = Ui.FillImg(vs, "d", Color.white);
			vd.sprite = Ui.Disc();
			Ui.VTxt(vs, "t", Vector2.zero, new Vector2(88f, 88f), "VS", 34f, Color.black, TextAlignmentOptions.Midline);
			Ui.In(vs, Ui.At(0.3f), 0f, 0f, 0.2f, 0.45f, back: true);
			// 설정
			InitVsRange();
			string lo = "Lv " + vsMin;
			string hi = "Lv " + vsMax;
			if (isHost)
			{
				// 지금 바꾸는 쪽을 노랗게 + 화살표
				if (vsEditMax)
				{
					hi = "<color=#FFD54A>◀ " + hi + " ▶</color>";
				}
				else
				{
					lo = "<color=#FFD54A>◀ " + lo + " ▶</color>";
				}
			}
			string[,] rows = new string[3, 2]
			{
				{ "레벨 범위", lad ? "두 사람 티어에 맞춰 자동" : (lo + "  ~  " + hi + "  <size=75%><color=#FFFFFF90>(" + VersusSongCount() + "곡)</color></size>") },
				{ "판정", lad ? "일반 (3라운드부터 하드)" : (versusHard ? "하드 (모든 라운드)" : "일반 (3라운드부터 하드)") },
				{ "버튼", KeyName((int)GameValue.key) }
			};
			for (int i = 0; i < 3; i++)
			{
				RectTransform r = Ui.Node(body, "set" + i, new Vector2(CX, 490 + i * 62), new Vector2(CW, 54f));
				Ui.In(r, Ui.At(0.2f + i * 0.05f), 0f, 24f);
				Ui.FillImg(r, "bg", new Color(0f, 0f, 0f, 0.6f));
				Ui.Txt(r, "l", new Vector2(20f, 0f), new Vector2(200f, 54f), rows[i, 0], 20f, Ui.Gold, extra: false, TextAlignmentOptions.MidlineLeft);
				Ui.Txt(r, "v", new Vector2(-20f, 0f), new Vector2(420f, 54f), rows[i, 1], 21f, Color.white, extra: true, TextAlignmentOptions.MidlineRight, Ui.TR, Ui.TR);
			}
			string help = lad ? "두 사람이 모두 준비하면 방장이 START. 곡 후보는 두 사람 티어 평균에 맞춰요." : (isHost ? "SHIFT 최소/최대   ←→ 레벨   F7 판정   ↑↓ 사람   F6 대전자 지정" : "방장이 대전자와 곡 레벨 범위를 정해요. 대전자는 ENTER 로 준비.");
			Ui.Txt(body, "help", new Vector2(CX, 690f), new Vector2(CW, 60f), help, 17f, Sub, extra: false, TextAlignmentOptions.Top).enableWordWrapping = true;
			if (bat.no > 0 && bat.hist.Count > 0 && bat.phase == BAT_NONE)
			{
				Player w = Find(bat.winner);
				Ui.Txt(body, "last", new Vector2(CX, 760f), new Vector2(CW, 30f), "지난 대전  " + ((bat.winner == -1) ? "무승부" : ((w?.name ?? "?") + " 승리")) + "   " + bat.pts[0] + " : " + bat.pts[1], 18f, Ui.Light, extra: false, TextAlignmentOptions.Top);
			}
		}

		// ---------------- 오른쪽: PLAYERS ----------------
		private void PlayersPanel()
		{
			Ui.In(Ui.VTxt(body, "ph", new Vector2(RX, 88f), new Vector2(RW, 60f), "PLAYERS", 50f, new Color(1f, 1f, 1f, 0.5f), TextAlignmentOptions.TopLeft), Ui.At(0.05f), 50f);
			float top = 160f;
			float avail = 820f - top;
			int n = Mathf.Max(maxPlayers, players.Count);
			float sh = Mathf.Min(116f, avail / n);
			for (int i = 0; i < n; i++)
			{
				PlayerSlot(i, (i < players.Count) ? players[i] : null, new Vector2(RX, top + i * sh), new Vector2(RW, sh - 8f));
			}
		}

		private void PlayerSlot(int i, Player p, Vector2 pos, Vector2 size)
		{
			RectTransform c = Ui.Node(body, "slot" + i, pos, size);
			// 처음엔 차례로, 새로 들어온 사람은 들어온 순간 오른쪽에서 미끄러져 들어옴
			Ui.In(c, (p == null) ? Ui.At(0.1f + i * 0.04f) : Mathf.Max(Ui.At(0.1f + i * 0.04f), Ui.Seen("slot" + p.id)), 80f, 0f, 1f, 0.45f);
			float h = size.y;
			const float PL = 356f;
			if (p == null)
			{
				Ui.Img(c, "e1", Vector2.zero, new Vector2(PL, h), new Color(1f, 1f, 1f, 0.07f));
				Ui.Img(c, "e2", new Vector2(PL + 8f, 0f), new Vector2(RW - PL - 8f, h / 2f - 3f), new Color(1f, 1f, 1f, 0.07f));
				Ui.Img(c, "e3", new Vector2(PL + 8f, h / 2f + 3f), new Vector2(RW - PL - 8f, h / 2f - 3f), new Color(1f, 1f, 1f, 0.07f));
				if (i == players.Count)
				{
					Ui.Txt(c, "w", Vector2.zero, new Vector2(PL, h), "참가 대기 중" + Dots, 16f, new Color(1f, 1f, 1f, 0.3f), extra: false, TextAlignmentOptions.Midline);
				}
				return;
			}
			bool me = p.id == myId;
			bool spec = (matchType == MT_VERSUS && !p.fighter) || (!battleMode && p.diffMode < 0);
			// 플레이트
			RectTransform pl = Ui.Node(c, "plate", Vector2.zero, new Vector2(PL, h));
			Image plate = Ui.FillImg(pl, "plate", Color.white);
			plate.sprite = PlateSprite(p.plate);
			plate.preserveAspect = false;
			if (plate.sprite == null)
			{
				plate.color = new Color(0.15f, 0.15f, 0.18f, 0.95f);
			}
			Ui.FillImg(pl, "shade", new Color(0f, 0f, 0f, spec ? 0.65f : 0.35f));
			if (teamMode)
			{
				Ui.Img(pl, "team", Vector2.zero, new Vector2(8f, h), TeamColor(p.team));
				Ui.VTxt(pl, "tm", new Vector2(-8f, 4f), new Vector2(60f, 18f), "TEAM " + (char)(65 + p.team), 11f, TeamColor(p.team), TextAlignmentOptions.TopRight, Ui.TR, Ui.TR);
			}
			float ic = h - 12f;
			Image icon = Ui.Img(pl, "icon", new Vector2(teamMode ? 14f : 6f, 6f), new Vector2(ic, ic), Color.white);
			icon.sprite = IconSprite(p.icon);
			icon.preserveAspect = true;
			if (icon.sprite == null)
			{
				icon.color = new Color(1f, 1f, 1f, 0.12f);
			}
			float tx = (teamMode ? 14f : 6f) + ic + 10f;
			bool small = h < 80f;
			Ui.Txt(pl, "name", new Vector2(tx, small ? 4f : 8f), new Vector2(PL - tx - 70f, 28f), p.name, small ? 17f : 20f, Color.white, extra: true, TextAlignmentOptions.TopLeft).overflowMode = TextOverflowModes.Ellipsis;
			Ui.VTxt(pl, "lv", new Vector2(tx, small ? 26f : 34f), new Vector2(160f, 34f), "<size=55%>LV</size> " + p.level, small ? 22f : 28f, Color.white, TextAlignmentOptions.TopLeft);
			if (!small || h >= 70f)
			{
				Ui.VTxt(pl, "tier", new Vector2(tx, h - 20f), new Vector2(160f, 16f), TierText(p.tier), 11f, TierColor(p.tier), TextAlignmentOptions.TopLeft);
			}
			if (p.id == 0)
			{
				RectTransform ht = Ui.Node(pl, "host", new Vector2(-6f, h - 24f), new Vector2(54f, 18f), new Vector2(1f, 1f), new Vector2(1f, 1f));
				ht.anchoredPosition = new Vector2(-6f, -(h - 24f));
				Ui.FillImg(ht, "bg", Color.white);
				Ui.VTxt(ht, "t", Vector2.zero, new Vector2(54f, 18f), "HOST", 11f, Color.black, TextAlignmentOptions.Midline);
			}
			if (matchType == MT_VERSUS)
			{
				RectTransform ft = Ui.Node(pl, "fighter", new Vector2(-6f, 6f), new Vector2(70f, 18f), Ui.TR, Ui.TR);
				Ui.FillImg(ft, "bg", p.fighter ? Orange : new Color(1f, 1f, 1f, 0.25f));
				Ui.VTxt(ft, "t", Vector2.zero, new Vector2(70f, 18f), p.fighter ? "FIGHTER" : "SPECTATE", 10f, Color.white, TextAlignmentOptions.Midline);
			}
			// 오른쪽 위: 버튼 + 난이도
			float rw = RW - PL - 8f;
			RectTransform kt = Ui.Node(c, "key", new Vector2(PL + 8f, 0f), new Vector2(44f, h / 2f - 3f));
			Ui.FillImg(kt, "bg", new Color32(30, 160, 90, byte.MaxValue));
			Ui.VTxt(kt, "t", Vector2.zero, new Vector2(44f, h / 2f - 3f), KeyName(p.diffKey), 14f, Color.white, TextAlignmentOptions.Midline);
			RectTransform dt = Ui.Node(c, "diff", new Vector2(PL + 56f, 0f), new Vector2(rw - 48f, h / 2f - 3f));
			string dtext;
			Color dcol;
			if (battleMode)
			{
				dtext = (matchType == MT_VERSUS && !p.fighter) ? "SPECTATE" : TierText(p.tier);
				dcol = (matchType == MT_VERSUS && !p.fighter) ? new Color(1f, 1f, 1f, 0.2f) : TierColor(p.tier);
			}
			else if (p.diffMode < 0)
			{
				dtext = "SPECTATE";
				dcol = new Color(1f, 1f, 1f, 0.2f);
			}
			else
			{
				float lv = LevelOf(RoomSong(), (KEY)p.diffKey, p.diffMode);
				dtext = ModeEng(p.diffMode) + (p.plus ? "+" : "") + ((lv > 0f) ? (" " + Mathf.FloorToInt(lv)) : "");
				dcol = ModeColor(p.diffMode);
			}
			Ui.FillImg(dt, "bg", dcol);
			Ui.VTxt(dt, "t", Vector2.zero, new Vector2(rw - 48f, h / 2f - 3f), dtext, 13f, Color.black, TextAlignmentOptions.Midline);
			// 오른쪽 아래: 상태
			RectTransform sb = Ui.Node(c, "state", new Vector2(PL + 8f, h / 2f + 3f), new Vector2(rw, h / 2f - 3f));
			Ui.FillImg(sb, "bg", new Color(0f, 0f, 0f, 0.55f));
			string st;
			Color sc = Color.white;
			string phase = PhaseText(p);
			if (phase.Length > 0)
			{
				st = phase;
				sc = p.offline ? Ui.Red : Ui.Gold;
				if (p.phase == PH_PLAYING && !p.done && matchIds.Contains(p.id))
				{
					st = p.score.ToString("N0");
				}
			}
			else if (spec)
			{
				st = "관전";
				sc = Sub;
			}
			else if (p.id == 0)
			{
				st = "HOST";
				sc = Ui.Light;
			}
			else if (p.ready)
			{
				st = "READY";
				sc = Yellow;
			}
			else
			{
				st = "준비 중";
				sc = Sub;
			}
			TextMeshProUGUI stt = Ui.VTxt(sb, "t", Vector2.zero, new Vector2(rw, h / 2f - 3f), st, (st == "READY") ? 18f : 14f, sc, TextAlignmentOptions.Midline);
			// 준비·상태가 바뀌면 글자가 톡 튐
			Ui.In(stt, Ui.Seen("st" + p.id + "|" + ((phase.Length > 0) ? ("ph" + p.phase) : st)), 0f, 0f, 1.5f, 0.3f, back: true, alpha: 1f);
			if (p.speed10 > 0 && h >= 60f)
			{
				Ui.VTxt(sb, "spd", new Vector2(4f, 0f), new Vector2(60f, 14f), "x" + (p.speed10 / 10f).ToString("0.0"), 9f, new Color(1f, 1f, 1f, 0.6f), TextAlignmentOptions.TopLeft);
			}
			if (!me && p.ping > 0 && h >= 70f)
			{
				Ui.VTxt(sb, "ping", new Vector2(-4f, 0f), new Vector2(60f, 14f), p.ping + "ms", 9f, (p.ping < 80) ? Ui.Green : ((p.ping < 160) ? Ui.Gold : Ui.Red), TextAlignmentOptions.TopRight, Ui.TR, Ui.TR);
			}
			if (me)
			{
				Ui.Frame(c, Yellow, 3f);
				foreach (Image fi in c.Find("frame").GetComponentsInChildren<Image>())
				{
					Ui.Pulse(fi, 0.5f, 1f, 1.6f);
				}
				RectTransform you = Ui.Node(c, "you", new Vector2(-38f, h / 2f - 12f), new Vector2(34f, 24f));
				Ui.FillImg(you, "bg", new Color32(215, 25, 63, byte.MaxValue));
				Ui.VTxt(you, "t", Vector2.zero, new Vector2(34f, 24f), "YOU", 10f, Color.white, TextAlignmentOptions.Midline);
			}
			if (isHost && matchType == MT_VERSUS && i == pcur)
			{
				Ui.Frame(c, Color.white, 2f);
				Ui.VTxt(c, "cur", new Vector2(-62f, h / 2f - 12f), new Vector2(20f, 24f), "▶", 18f, Color.white, TextAlignmentOptions.Midline);
			}
		}

		// START (디맥 노란 버튼). 손님은 READY, 관전자는 관전 중
		private void StartButton()
		{
			RectTransform b = Ui.Node(body, "start", new Vector2(RX, 836f), new Vector2(RW, 100f));
			Player me = Me;
			string big;
			string small = "ENTER";
			bool lit;
			string reason = null;
			if (isHost)
			{
				reason = StartBlockReason();
				big = "START";
				lit = reason == null;
			}
			else if (me != null && ((matchType == MT_VERSUS && !me.fighter) || (!battleMode && me.diffMode < 0)))
			{
				big = "SPECTATE";
				small = "관전 중";
				lit = false;
			}
			else
			{
				big = "READY";
				lit = me == null || !me.ready;
				small = (me != null && me.ready) ? "준비 완료 · ENTER 로 취소" : "ENTER";
			}
			Ui.FillImg(b, "bg", lit ? Yellow : new Color(Yellow.r, Yellow.g, Yellow.b, 0.25f));
			Ui.In(b, Ui.At(0.2f), 0f, 50f, 1f, 0.45f);
			if (lit)
			{
				Ui.Shine(b, 0.7f, 2f);
			}
			Ui.Frame(b, new Color(0f, 0f, 0f, 0.5f), 3f);
			TextMeshProUGUI t = Ui.VTxt(b, "t", new Vector2(0f, 8f), new Vector2(RW, 64f), big, 56f, lit ? Color.black : new Color(1f, 1f, 1f, 0.8f), TextAlignmentOptions.Midline);
			t.fontStyle = FontStyles.Italic;
			Ui.Txt(b, "s", new Vector2(0f, 70f), new Vector2(RW, 24f), small, 15f, lit ? new Color(0f, 0f, 0f, 0.7f) : Ui.Light, extra: false, TextAlignmentOptions.Midline);
			if (reason != null)
			{
				Ui.Txt(body, "reason", new Vector2(RX, 802f), new Vector2(RW, 28f), reason, 16f, Ui.Gold, extra: false, TextAlignmentOptions.MidlineRight).overflowMode = TextOverflowModes.Ellipsis;
			}
			else if (!isHost && me != null && me.ready)
			{
				Ui.Txt(body, "reason", new Vector2(RX, 802f), new Vector2(RW, 28f), "방장이 시작하길 기다리는 중" + Dots, 16f, Ui.Light, extra: false, TextAlignmentOptions.MidlineRight);
			}
		}

		private void RoomHotkeys()
		{
			if (chatting)
			{
				Hotkeys("ENTER", "보내기", "ESC", "취소");
				return;
			}
			List<string> k = new List<string>();
			if (isHost && matchType != MT_VERSUS)
			{
				k.AddRange(new string[10] { "ENTER", "시작", "↑↓", "곡", "←→", "난이도", "TAB", "4B/6B", "F2", "검색" });
			}
			else if (isHost)
			{
				k.AddRange(new string[2] { "ENTER", "시작" });
			}
			else
			{
				k.AddRange(new string[2] { "ENTER", "준비" });
				if (!battleMode)
				{
					k.AddRange(new string[2] { "←→", "난이도" });
				}
			}
			if (teamMode)
			{
				k.AddRange(new string[2] { "F9", "팀" });
			}
			if (isHost)
			{
				k.AddRange(new string[4] { "F12", "방식", "F8", "강퇴" });
			}
			k.AddRange(new string[8] { "F3", "채팅", "F11", "결과·기록", "SPACE", "옷장", "ESC", "나가기" });
			Hotkeys(k.ToArray());
		}

		// ---------------- 대전 화면 ----------------
		private string BattleSig()
		{
			int secs = Mathf.CeilToInt(Mathf.Max(0f, bat.until - Time.unscaledTime));
			string live = (bat.phase == BAT_PLAY) ? string.Join(";", MP.Select((Player p) => p.id + ":" + p.score / 500 + ":" + p.rate.ToString("0.0") + p.brk + p.done + p.phase)) : "";
			return string.Concat("|", bat.phase, "|", secs, "|", bat.no, "|", bat.round, "|", bat.pts[0], bat.pts[1], "|", bat.ban[0], bat.ban[1], "|", banSel, "|", string.Join(",", bat.order), "|", string.Join(";", bat.hist), "|", bat.cands.Count, "|", bat.winner, batDelta, "|", (bat.phase == BAT_PLAY || bat.phase == BAT_REVEAL) ? Dots : "", "|", live, "|", RouletteSig(), "|", string.Join(";", players.Select((Player p) => p.id + p.name + p.inRoom + p.offline + p.phase + p.tier)));
		}

		private void BuildBattle()
		{
			Player a = Find(bat.ids[0]);
			Player b = Find(bat.ids[1]);
			int secs = Mathf.CeilToInt(Mathf.Max(0f, bat.until - Time.unscaledTime));
			int slot = MySlot;
			// 배경: 이번 라운드 곡 자켓
			// (밴픽·곡 추첨 중엔 곡이 드러나지 않게 테마 영상만)
			bool hidden = bat.phase == BAT_BAN || bat.phase == BAT_END || Roulette(out _, out _);
			if (!hidden && bat.round > 0 && bat.round <= bat.order.Count)
			{
				Sprite art = CandBundle(bat.cands[bat.order[bat.round - 1]], out _, out _)?.Album;
				Image bg = Ui.Cover(body, "bgArt", Vector2.zero, new Vector2(1920f, 1080f), art ?? AnyJacket());
				bg.color = new Color(1f, 1f, 1f, (bat.phase == BAT_REVEAL) ? 0.75f : 0.45f);
				// 배경 자켓이 아주 천천히 커졌다 작아졌다 (멈춘 그림처럼 안 보이게)
				Ui.Every(bg, delegate(float t)
				{
					bg.rectTransform.localScale = Vector3.one * (1.06f + 0.04f * Mathf.Sin(t * 0.15f));
					return false;
				}, 0f);
			}
			Ui.FillImg(body, "bgShade", new Color(0.02f, 0.02f, 0.04f, (bat.phase == BAT_REVEAL) ? (hidden ? 0.72f : 0.55f) : (hidden ? 0.7f : 0.8f)));
			// 디맥: 밴 화면·라운드 화면엔 위쪽 대전 점수판이 없다 (밴 = 로고 + 내 프로필, 라운드 = 가운데 곡 세로줄 + 양옆 티어)
			bool plain = bat.phase == BAT_BAN || bat.phase == BAT_REVEAL || bat.phase == BAT_END;
			if (bat.phase != BAT_REVEAL && bat.phase != BAT_END)
			{
				Logo(MatchTypeName(bat.kind));
			}
			if (bat.phase == BAT_BAN)
			{
				Player mine = Me;
				if (mine != null)
				{
					ProfileCard(Ui.Node(body, "profile", new Vector2(-54f, 22f), new Vector2(440f, 88f), Ui.TR, Ui.TR), mine);
				}
			}
			if (!plain)
			{
				// 위: 두 대전자 + 점수
				Ui.In(DuelPlate(a, 0, new Vector2(360f, 24f)), Ui.At(0.05f), -80f, 0f, 1f, 0.45f);
				Ui.In(DuelPlate(b, 1, new Vector2(1920f - 360f - 440f, 24f)), Ui.At(0.05f), 80f, 0f, 1f, 0.45f);
				Ui.In(Ui.VTxt(body, "pts", new Vector2(0f, 16f), new Vector2(1920f, 90f), bat.pts[0] + "<size=60%>  :  </size>" + bat.pts[1], 72f, Color.white, TextAlignmentOptions.Top), Ui.At(0.15f), 0f, 0f, 1.5f, 0.4f, back: true);
			}
			string phase = bat.phase switch
			{
				BAT_BAN => "BAN PICK",
				BAT_REVEAL => "ROUND " + bat.round,
				BAT_PLAY => "ROUND " + bat.round + "  ·  PLAYING",
				BAT_END => "MATCH END",
				_ => "",
			};
			if (!plain)
			{
				Ui.VTxt(body, "phase", new Vector2(0f, 104f), new Vector2(1920f, 30f), phase, 22f, Ui.Gold, TextAlignmentOptions.Top);
			}
			switch (bat.phase)
			{
			case BAT_BAN:
				BanPhase(slot, secs);
				break;
			case BAT_REVEAL:
				RevealPhase(secs);
				break;
			case BAT_PLAY:
				BuildMatchPanel(body, ingame: false, new Vector2(750f, 170f));
				Ui.In(body.Find("match"), Ui.At(0.15f), 0f, 50f, 1f, 0.45f);
				RoundHistory(new Vector2(1250f, 200f));
				Ui.Pulse(Ui.Txt(body, "wait", new Vector2(0f, 930f), new Vector2(1920f, 36f), (slot >= 0) ? "상대가 끝나길 기다리는 중" : "관전 중 · 실시간 점수", 22f, Color.white, extra: true, TextAlignmentOptions.Top), 0.4f, 1f, 1.6f);
				break;
			case BAT_END:
				EndPhase(slot);
				break;
			}
			// AI 래더는 채팅 상대가 없으니 채팅 칸·키를 숨긴다
			if (!aiMatch)
			{
				ChatBox(new Vector2(LPX, 868f), new Vector2(560f, 124f));
			}
			if (chatting)
			{
				Hotkeys("ENTER", "보내기", "ESC", "취소");
			}
			else if (bat.phase == BAT_BAN && slot >= 0 && bat.ban[slot] < 0)
			{
				Hotkeys(aiMatch ? new string[6] { "ENTER", "이 곡 밴", "←→", "선택", "ESC", "기권" } : new string[8] { "ENTER", "이 곡 밴", "←→", "선택", "F3", "채팅", "ESC", "나가기" });
			}
			else if (bat.phase == BAT_END)
			{
				Hotkeys(aiMatch ? new string[2] { "ENTER", "확인" } : new string[4] { "ENTER", "방으로", "F3", "채팅" });
			}
			else
			{
				Hotkeys(aiMatch ? new string[2] { "ESC", "기권" } : new string[4] { "F3", "채팅", "ESC", "나가기" });
			}
		}

		private RectTransform DuelPlate(Player p, int side, Vector2 pos)
		{
			RectTransform card = Ui.Node(body, "duel" + side, pos, new Vector2(440f, 88f));
			if (p == null)
			{
				Ui.FillImg(card, "bg", new Color(0f, 0f, 0f, 0.6f));
				Ui.Txt(card, "t", Vector2.zero, new Vector2(440f, 88f), "나감", 22f, Sub, extra: false, TextAlignmentOptions.Midline);
				return card;
			}
			ProfileCard(card, p);
			Ui.Img(card, "side", (side == 0) ? Vector2.zero : new Vector2(434f, 0f), new Vector2(6f, 88f), (side == 0) ? Blue : Orange);
			if (p.id == myId)
			{
				Ui.Frame(card, Yellow, 3f);
			}
			return card;
		}

		// 밴 화면: 위 원형 타이머 + BAN PICK 제목 + 안내 + 두 사람 상태, 가운데 곡 카드 5장(유리 카드), 아래 BAN 버튼
		private static readonly Color Glass = new Color(0.07f, 0.07f, 0.1f, 0.84f);

		private static readonly Color Coral = new Color32(byte.MaxValue, 92, 96, byte.MaxValue);

		// 둥근 작은 꼬리표
		private static RectTransform Chip(Transform p, string name, Vector2 pos, Vector2 size, string text, Color bg, Color fg, float fontSize)
		{
			RectTransform c = Ui.Node(p, name, pos, size);
			Ui.FillImg(c, "bg", bg);
			Ui.Txt(c, "t", Vector2.zero, size, text, fontSize, fg, extra: true, TextAlignmentOptions.Midline).overflowMode = TextOverflowModes.Ellipsis;
			return c;
		}

		private void BanPhase(int slot, float secs)
		{
			// 위아래 어둡게 (가운데 카드에 시선)
			Image vt = Ui.Img(body, "vigT", Vector2.zero, new Vector2(1920f, 260f), new Color(0f, 0f, 0f, 0.55f));
			vt.sprite = Ui.Fade();
			vt.rectTransform.localEulerAngles = new Vector3(0f, 0f, 180f);
			vt.rectTransform.pivot = Ui.C;
			vt.rectTransform.anchoredPosition = new Vector2(960f, -130f);
			Ui.Img(body, "vigB", new Vector2(0f, 760f), new Vector2(1920f, 320f), new Color(0f, 0f, 0f, 0.6f)).sprite = Ui.Fade();
			// 원형 타이머
			const float R = 64f;
			Vector2 tc = new Vector2(960f, 118f);
			RectTransform tm = Ui.Node(body, "timer", tc - new Vector2(R, R), new Vector2(R * 2f, R * 2f));
			Ui.Img(tm, "disc", Vector2.zero, new Vector2(R * 2f, R * 2f), Glass).sprite = Ui.Disc();
			Ui.Img(tm, "track", Vector2.zero, new Vector2(R * 2f, R * 2f), new Color(1f, 1f, 1f, 0.12f)).sprite = Ui.Ring();
			Image arc = Ui.Img(tm, "arc", Vector2.zero, new Vector2(R * 2f, R * 2f), Coral);
			arc.sprite = Ui.Ring();
			arc.type = Image.Type.Filled;
			arc.fillMethod = Image.FillMethod.Radial360;
			arc.fillOrigin = 2;
			arc.fillClockwise = false;
			Ui.Every(arc, delegate
			{
				float left = Mathf.Clamp01((bat.until - Time.unscaledTime) / BAN_TIME);
				arc.fillAmount = left;
				arc.color = Color.Lerp(Ui.Red, Coral, Mathf.Clamp01((left * BAN_TIME - 3f) / 3f));
				return false;
			}, 0f);
			Ui.In(Ui.VTxt(tm, "s", new Vector2(0f, 26f), new Vector2(R * 2f, 56f), secs.ToString(), 46f, (secs <= 5f) ? Ui.Red : Color.white, TextAlignmentOptions.Top), Ui.Seen("bsec" + secs), 0f, 0f, (secs <= 5f) ? 1.35f : 1.15f, 0.3f, back: true, alpha: 1f);
			Ui.VTxt(tm, "u", new Vector2(0f, 80f), new Vector2(R * 2f, 20f), KeyName((int)GameValue.key) + " TUNES", 12f, new Color(1f, 1f, 1f, 0.55f), TextAlignmentOptions.Top);
			Ui.In(tm, Ui.At(0.05f), 0f, 0f, 0.5f, 0.5f, back: true);
			// 제목 + 안내
			Ui.In(Ui.VTxt(body, "title", new Vector2(0f, 196f), new Vector2(1920f, 60f), "BAN PICK", 50f, Color.white, TextAlignmentOptions.Top), Ui.At(0.1f), 0f, -20f);
			bool can = slot >= 0 && bat.ban[slot] < 0;
			int opp = (slot >= 0) ? (1 - slot) : (-1);
			string msg = (slot < 0) ? "대전자들이 이번 매치에서 뺄 곡을 고르고 있어요" : (can ? "이번 매치에서 뺄 곡을 하나 골라주세요" : ((bat.ban[opp] >= 0) ? "두 사람 모두 골랐어요" : "상대가 고르는 중이에요"));
			Ui.In(Ui.Txt(body, "how", new Vector2(0f, 256f), new Vector2(1920f, 30f), msg, 21f, new Color(1f, 1f, 1f, 0.78f), extra: false, TextAlignmentOptions.Top), Ui.Seen("how" + msg), 0f, 10f, 1f, 0.35f);
			// 두 대전자 상태
			for (int k = 0; k < 2; k++)
			{
				Player fp = Find(bat.ids[k]);
				bool done = bat.ban[k] >= 0;
				string nm = (fp == null) ? "?" : ((fp.id == myId) ? "나" : fp.name);
				RectTransform st = Chip(body, "st" + k, new Vector2(960f - 214f + k * 224f, 296f), new Vector2(204f, 30f), nm + "   " + (done ? "<color=#58DEA6>선택 완료</color>" : ("<color=#FFFFFFA0>고르는 중" + Dots + "</color>")), done ? new Color(0.35f, 0.87f, 0.65f, 0.16f) : new Color(1f, 1f, 1f, 0.08f), Color.white, 15f);
				Ui.In(st, Mathf.Max(Ui.At(0.2f), Ui.Seen("bst" + k + done)), 0f, 0f, done ? 1.15f : 1f, 0.3f, back: true);
			}
			// 곡 카드
			const float CW = 272f;
			const float GAP = 34f;
			int n = bat.cands.Count;
			float x0 = (1920f - (n * CW + (n - 1) * GAP)) / 2f;
			for (int i = 0; i < n; i++)
			{
				bool cur = can && i == banSel;
				bool mine = slot >= 0 && bat.ban[slot] == i;
				BanCard(i, new Vector2(x0 + i * (CW + GAP), 360f), CW, cur, mine, can && !cur);
			}
			// BAN 버튼
			RectTransform ok = Ui.Node(body, "ok", new Vector2(960f - 170f, 836f), new Vector2(340f, 66f));
			Ui.FillImg(ok, "bg", can ? Coral : Glass);
			if (can)
			{
				Ui.Shine(ok, 0.5f, 2.2f);
				Ui.VTxt(ok, "t", new Vector2(0f, 8f), new Vector2(340f, 34f), "BAN", 30f, Color.white, TextAlignmentOptions.Top);
				Ui.Txt(ok, "k", new Vector2(0f, 42f), new Vector2(340f, 18f), "ENTER", 12f, new Color(1f, 1f, 1f, 0.8f), extra: false, TextAlignmentOptions.Top);
			}
			else
			{
				Ui.Txt(ok, "t", Vector2.zero, new Vector2(340f, 66f), (slot >= 0 && bat.ban[slot] >= 0) ? "선택 완료" : "관전 중", 22f, Sub, extra: true, TextAlignmentOptions.Midline);
			}
			Ui.In(ok, Ui.At(0.5f), 0f, 30f);
		}

		private void BanCard(int i, Vector2 pos, float w, bool cursor, bool banned, bool dim)
		{
			const float H = 430f;
			RectTransform c = Ui.Node(body, "ban" + i, pos, new Vector2(w, H));
			Ui.In(c, Ui.At(0.2f + i * 0.07f), 0f, 80f, 1f, 0.55f);
			// 고른 카드는 살짝 떠오름 (안쪽 카드만 움직여서 등장 연출과 안 겹침)
			RectTransform card = Ui.Node(c, "card", cursor ? new Vector2(0f, -16f) : Vector2.zero, new Vector2(w, H));
			if (cursor)
			{
				Ui.In(card, Ui.Seen("bcur" + i), 0f, 16f, 1f, 0.3f, back: false, alpha: 1f);
			}
			Ui.FillImg(card, "bg", Glass);
			BaseMusicBundle b = CandBundle(bat.cands[i], out MODE mode, out KEY key);
			float lv = LevelOf(b, key, (int)mode);
			Ui.Cover(card, "art", Vector2.zero, new Vector2(w, w), b?.Album);
			Image jf = Ui.Img(card, "artFade", new Vector2(0f, w - 90f), new Vector2(w, 90f), new Color(0.07f, 0.07f, 0.1f, 0.95f));
			jf.sprite = Ui.Fade();
			if (b != null)
			{
				string cat = "";
				try
				{
					cat = BaseMusicBundle.ToKorea(b.info.musicCategory);
				}
				catch (System.Exception)
				{
				}
				Color cc = CategoryColor(b.info.musicCategory);
				Chip(card, "cat", new Vector2(10f, 10f), new Vector2(Mathf.Max(64f, cat.Length * 17f + 22f), 26f), cat, new Color(cc.r * 0.55f, cc.g * 0.55f, cc.b * 0.55f, 0.92f), Color.white, 13f);
			}
			Chip(card, "key", new Vector2(w - 54f, 10f), new Vector2(44f, 26f), KeyName((int)key), new Color(0f, 0f, 0f, 0.6f), Color.white, 13f);
			TextMeshProUGUI tt = Ui.Txt(card, "title", new Vector2(12f, w + 8f), new Vector2(w - 24f, 32f), b?.title ?? "내 게임에 없는 곡", 22f, (b == null) ? Ui.Red : Color.white, extra: true, TextAlignmentOptions.Top);
			tt.enableAutoSizing = true;
			tt.fontSizeMin = 15f;
			tt.fontSizeMax = 22f;
			tt.overflowMode = TextOverflowModes.Ellipsis;
			Ui.Txt(card, "artist", new Vector2(12f, w + 40f), new Vector2(w - 24f, 22f), b?.artist ?? "", 14f, Sub, extra: false, TextAlignmentOptions.Top).overflowMode = TextOverflowModes.Ellipsis;
			// 난이도 알약 (게임 색)
			Color mc = ModeColor((int)mode);
			Chip(card, "diff", new Vector2((w - 150f) / 2f, w + 72f), new Vector2(150f, 32f), ModeEng((int)mode) + ((lv > 0f) ? ("  " + Mathf.FloorToInt(lv)) : ""), mc, new Color(0.08f, 0.08f, 0.1f), 16f);
			string best = "-";
			try
			{
				if (b != null)
				{
					float r = BundlePackage.user.GetHighScore(b.RootFileName, mode, isPlusMode: false).noteScore.rate;
					best = (r > 0f) ? (r.ToString("0.00") + "%") : "-";
				}
			}
			catch (System.Exception)
			{
			}
			Ui.Txt(card, "best", new Vector2(0f, w + 116f), new Vector2(w, 22f), "MY BEST   <color=#FFD54A>" + best + "</color>", 14f, new Color(1f, 1f, 1f, 0.6f), extra: false, TextAlignmentOptions.Top);
			if (cursor)
			{
				Ui.Img(card, "tint", Vector2.zero, new Vector2(w, w), new Color(Coral.r, Coral.g, Coral.b, 0.16f));
				Ui.Frame(card, Coral, 3f);
				Ui.Pulse(card.Find("frame").GetComponent<Image>(), 0.55f, 1f, 1.1f);
			}
			else if (dim)
			{
				Ui.FillImg(card, "dimmed", new Color(0.02f, 0.02f, 0.03f, 0.42f));
			}
			if (banned)
			{
				Ui.FillImg(card, "dimmed", new Color(0.02f, 0.02f, 0.03f, 0.62f));
				RectTransform stamp = Ui.Node(card, "stamp", new Vector2(w / 2f, w / 2f), new Vector2(180f, 60f));
				stamp.pivot = Ui.C;
				stamp.anchoredPosition = new Vector2(w / 2f, -w / 2f);
				stamp.localEulerAngles = new Vector3(0f, 0f, 12f);
				Ui.FillImg(stamp, "bg", Coral);
				Ui.VTxt(stamp, "t", Vector2.zero, new Vector2(180f, 60f), "BAN", 34f, Color.white, TextAlignmentOptions.Midline);
				Ui.In(stamp, Ui.Seen("banned" + i), 0f, 0f, 2.2f, 0.35f);
			}
		}

		// ---------------- 곡 추첨 룰렛 (라운드 곡 공개 전) ----------------
		private const float ROULETTE_TIME = 3.6f;

		private const float ROULETTE_HOLD = 2.4f; // 멈춘 곡을 캐러셀에서 보여 주는 시간

		private string rouletteKey = "";

		private float rouletteAt;

		private int rouletteLast = -1;

		// 남은 곡들 사이를 빠르게 돌다가 점점 느려지며 이번 라운드 곡에 멈춘다
		private bool Roulette(out List<int> pool, out int hi)
		{
			pool = bat.order.Skip(Mathf.Max(0, bat.round - 1)).Distinct().ToList();
			if (pool.Count < 2)
			{
				pool = Enumerable.Range(0, bat.cands.Count).ToList();
			}
			hi = -1;
			if (bat.phase != BAT_REVEAL || bat.round < 1 || bat.round > bat.order.Count)
			{
				return false;
			}
			string key = bat.no + ":" + bat.round;
			if (rouletteKey != key)
			{
				rouletteKey = key;
				rouletteAt = Time.unscaledTime;
				rouletteLast = -1;
			}
			int target = pool.IndexOf(bat.order[bat.round - 1]);
			int n = pool.Count;
			int steps = n * 4 + Mathf.Max(0, target);
			float x = Mathf.Clamp01((Time.unscaledTime - rouletteAt) / ROULETTE_TIME);
			int k = Mathf.Min(steps, Mathf.FloorToInt(steps * (1f - Mathf.Pow(1f - x, 3f))));
			hi = k % n;
			return Time.unscaledTime - rouletteAt < ROULETTE_TIME + ROULETTE_HOLD;
		}

		// 캐러셀은 매 프레임 스스로 움직이므로 다시 조립할 필요 없음 (끝난 뒤 번쩍임만)
		private string RouletteSig()
		{
			bool on = Roulette(out _, out _);
			float pop = Time.unscaledTime - rouletteAt - ROULETTE_TIME - ROULETTE_HOLD;
			return on + ":" + ((pop >= 0f && pop < 0.6f) ? Mathf.FloorToInt(pop * 30f) : -1);
		}

		// 곡 추첨: 화면 가운데 큰 자켓 줄이 촤라라락 넘어가다 이번 라운드 곡에 멈춤. 가운데 곡의 제목·정보가 아래에 뜸
		private void Carousel(List<int> pool)
		{
			int n = pool.Count;
			int target = Mathf.Max(0, pool.IndexOf(bat.order[bat.round - 1]));
			int steps = n * 4 + target;
			const float SP = 300f;
			const float JS = 290f;
			const float CY = 480f;
			Image band = Ui.Img(body, "band", new Vector2(0f, CY - 250f), new Vector2(1920f, 580f), new Color(0f, 0f, 0f, 0.4f));
			Ui.In(band, Ui.At(0f), 0f, 0f, 1f, 0.4f);
			Ui.In(Ui.VTxt(body, "rnd", new Vector2(0f, 110f), new Vector2(1920f, 70f), "ROUND " + bat.round, 58f, Color.white, TextAlignmentOptions.Top), Ui.At(0f), 0f, -24f);
			TextMeshProUGUI sub = Ui.Txt(body, "sel", new Vector2(0f, 180f), new Vector2(1920f, 30f), "이번 라운드 곡을 고르는 중", 20f, new Color(1f, 1f, 1f, 0.7f), extra: false, TextAlignmentOptions.Top);
			Ui.In(sub, Ui.At(0.1f));
			RectTransform strip = Ui.Node(body, "strip", Vector2.zero, new Vector2(1920f, 1080f));
			int[] order = new int[9] { 4, -4, 3, -3, 2, -2, 1, -1, 0 };
			RectTransform[] slots = new RectTransform[9];
			Image[] arts = new Image[9];
			CanvasGroup[] groups = new CanvasGroup[9];
			int[] shown = new int[9];
			foreach (int j in order)
			{
				RectTransform sl = Ui.Node(strip, "s" + j, new Vector2(960f, CY), new Vector2(JS, JS), Ui.TL, Ui.C);
				Ui.FillImg(sl, "bg", Glass);
				arts[j + 4] = Ui.Cover(sl, "art", Vector2.zero, new Vector2(JS, JS), null);
				slots[j + 4] = sl;
				groups[j + 4] = sl.gameObject.AddComponent<CanvasGroup>();
				shown[j + 4] = -1;
			}
			// 가운데 금색 틀 (고정) + 멈출 때 번쩍임
			RectTransform cf = Ui.Node(body, "cframe", new Vector2(960f, CY), new Vector2(JS + 18f, JS + 18f), Ui.TL, Ui.C);
			Ui.FillImg(cf, "bg", new Color(0f, 0f, 0f, 0f));
			Ui.Frame(cf, Ui.Gold, 4f);
			Ui.Pulse(cf.Find("frame").GetComponent<Image>(), 0.5f, 1f, 0.8f);
			Image flash = Ui.FillImg(cf, "flash", new Color(1f, 1f, 1f, 0f));
			TextMeshProUGUI title = Ui.Txt(body, "title", new Vector2(160f, CY + 162f), new Vector2(1600f, 64f), "", 38f, Color.white, extra: true, TextAlignmentOptions.Top);
			title.overflowMode = TextOverflowModes.Ellipsis;
			TextMeshProUGUI artist = Ui.Txt(body, "artist", new Vector2(160f, CY + 226f), new Vector2(1600f, 28f), "", 19f, Sub, extra: false, TextAlignmentOptions.Top);
			artist.overflowMode = TextOverflowModes.Ellipsis;
			TextMeshProUGUI info = Ui.Txt(body, "info", new Vector2(160f, CY + 262f), new Vector2(1600f, 30f), "", 20f, Color.white, extra: true, TextAlignmentOptions.Top);
			foreach (Component c in new Component[3] { title, artist, info })
			{
				Ui.In(c, Ui.At(0.2f), 0f, 16f);
			}
						// 멈추면 가운데 아래에 뜨는 곡 정보 칩 (난이도 · 키 · BPM) + 금색 고리가 퍼짐
			BaseMusicBundle tb = CandBundle(bat.cands[pool[target]], out MODE tmode, out KEY tkey);
			float tlv = LevelOf(tb, tkey, (int)tmode);
			RectTransform pick = Ui.Node(body, "pick", new Vector2(960f - 291f, CY + 266f), new Vector2(582f, 48f));
			CanvasGroup pickG = pick.gameObject.AddComponent<CanvasGroup>();
			pickG.alpha = 0f;
			Chip(pick, "mode", Vector2.zero, new Vector2(214f, 48f), ModeEng((int)tmode) + ((tlv > 0f) ? ("  " + Mathf.FloorToInt(tlv)) : ""), ModeColor((int)tmode), Color.white, 24f);
			Chip(pick, "key", new Vector2(228f, 0f), new Vector2(140f, 48f), KeyName((int)tkey), new Color(1f, 1f, 1f, 0.14f), Color.white, 24f);
			Chip(pick, "bpm", new Vector2(382f, 0f), new Vector2(200f, 48f), "BPM " + ((tb != null) ? tb.info.GetBpmString() : "?"), new Color(1f, 1f, 1f, 0.14f), Color.white, 22f);
			RectTransform burst = Ui.Node(body, "burst", new Vector2(960f, CY), new Vector2(JS + 18f, JS + 18f), Ui.TL, Ui.C);
			Ui.FillImg(burst, "bg", new Color(0f, 0f, 0f, 0f));
			Ui.Frame(burst, Ui.Gold, 6f);
			CanvasGroup burstG = burst.gameObject.AddComponent<CanvasGroup>();
			burstG.alpha = 0f;
			int shownInfo = -1;
			float landAt = rouletteAt + ROULETTE_TIME;
			Ui.Every(strip, delegate
			{
				float now = Time.unscaledTime;
				float x = Mathf.Clamp01((now - rouletteAt) / ROULETTE_TIME);
				float pos = steps * (1f - Mathf.Pow(1f - x, 3f));
				int bi = Mathf.FloorToInt(pos);
				float fr = pos - bi;
				float land = Mathf.Clamp01((now - landAt) / 0.4f);
				for (int j = -4; j <= 4; j++)
				{
					int k = j + 4;
					int item = (int)Mathf.Repeat(bi + j, n);
					float dx = (j - fr) * SP;
					float d = Mathf.Abs(dx) / SP;
					float sc = (d < 1f) ? Mathf.Lerp(1f, 0.66f, d) : Mathf.Max(0.4f, 0.66f - (d - 1f) * 0.08f);
					if (Mathf.Abs(dx) < SP * 0.5f && now >= landAt)
					{
						sc *= 1f + 0.1f * Mathf.Sin(Mathf.PI * land);
					}
					slots[k].anchoredPosition = new Vector2(960f + dx * (1f - Mathf.Min(d, 3f) * 0.06f), -CY);
					slots[k].localScale = Vector3.one * sc;
					groups[k].alpha = Mathf.Clamp01(1.15f - d * 0.3f) * ((now >= landAt && d > 0.5f) ? Mathf.Lerp(1f, 0.45f, land) : 1f);
					if (shown[k] != item)
					{
						shown[k] = item;
						Sprite sp = CandBundle(bat.cands[pool[item]], out _, out _)?.Album;
						Image a = arts[k];
						a.sprite = sp;
						a.color = (sp == null) ? new Color(0.1f, 0.1f, 0.12f, 1f) : Color.white;
						if (sp != null)
						{
							float r = sp.rect.width / Mathf.Max(1f, sp.rect.height);
							a.rectTransform.sizeDelta = (r > 1f) ? new Vector2(JS * r, JS) : new Vector2(JS, JS / r);
						}
					}
				}
				int ci = (int)Mathf.Repeat(Mathf.RoundToInt(pos), n);
				if (ci != shownInfo)
				{
					shownInfo = ci;
					BaseMusicBundle b = CandBundle(bat.cands[pool[ci]], out MODE mode, out KEY key);
					float lv = LevelOf(b, key, (int)mode);
					title.text = b?.title ?? "?";
					artist.text = b?.artist ?? "";
					info.text = "<color=" + Ui.Hex(ModeColor((int)mode)) + ">" + ModeEng((int)mode) + ((lv > 0f) ? (" " + Mathf.FloorToInt(lv)) : "") + "</color><color=#FFFFFF60>   ·   </color>" + KeyName((int)key) + ((b != null) ? ("<color=#FFFFFF60>   ·   </color>BPM " + b.info.GetBpmString()) : "");
					if (x < 1f && rouletteLast != ci)
					{
						rouletteLast = ci;
						PlaySe(AudioManager.SE.SlideMove);
					}
				}
				if (now >= landAt)
				{
										title.color = Color.Lerp(Color.white, Ui.Gold, land);
					title.fontSize = Mathf.Lerp(38f, 46f, Ui.OutCubic(land));
					info.alpha = 1f - land;
					float pk = Mathf.Clamp01((now - landAt - 0.15f) / 0.45f);
					pickG.alpha = pk;
					pick.anchoredPosition = new Vector2(960f - 291f, -(CY + 266f) - 14f * (1f - Ui.OutBack(pk)));
					float bk = Mathf.Clamp01((now - landAt) / 0.7f);
					burst.localScale = Vector3.one * Mathf.Lerp(1f, 1.45f, Ui.OutCubic(bk));
					burstG.alpha = 1f - bk;
					flash.color = new Color(1f, 1f, 1f, 0.75f * (1f - Mathf.Clamp01((now - landAt) / 0.5f)));
					sub.text = "이번 라운드 곡";
				}
				return false;
			}, 0f);
		}

		// 디맥 라운드 화면: 위 GET READY + 카운트, 가운데 라운드 곡 세로줄(지난 라운드는 WIN/LOSE 점수, 다음 곡은 ?), 뒤에 큰 ROUND N, 양옆 티어·프로필
		private void RevealPhase(float secs)
		{
			bool hard = HardRound(bat.round);
			bool spin = Roulette(out List<int> pool, out int hi);
			int me = Mathf.Max(0, MySlot);
			if (spin)
			{
				Carousel(pool);
				return;
			}
			// 위: 남은 시간 막대 + GET READY
			float left = Mathf.Clamp01((bat.until - Time.unscaledTime) / REVEAL_TIME);
			Image tbar = Ui.Img(body, "bar", new Vector2(960f - 480f * left, 0f), new Vector2(960f * left, 8f), new Color32(byte.MaxValue, 96, 24, byte.MaxValue));
			Ui.Every(tbar, delegate
			{
				float l = Mathf.Clamp01((bat.until - Time.unscaledTime) / REVEAL_TIME);
				tbar.rectTransform.anchoredPosition = new Vector2(960f - 480f * l, 0f);
				tbar.rectTransform.sizeDelta = new Vector2(960f * l, 8f);
				return false;
			}, 0f);
			Ui.In(Ui.VTxt(body, "ready", new Vector2(0f, 20f), new Vector2(1920f, 40f), spin ? "SELECTING" : "GET READY", 30f, new Color32(byte.MaxValue, 110, 40, byte.MaxValue), TextAlignmentOptions.Top), Ui.Seen("ready" + spin), 0f, -24f, 1f, 0.35f);
			Ui.In(Ui.VTxt(body, "cnt", new Vector2(0f, 56f), new Vector2(1920f, 50f), secs.ToString(), 40f, new Color(1f, 1f, 1f, 0.6f), TextAlignmentOptions.Top), Ui.Seen("rcnt" + secs), 0f, 0f, 1.3f, 0.3f, back: true, alpha: 1f);
			// 큰 ROUND N (곡 줄 뒤): 크게 나타나며 내려앉음
			Ui.In(Ui.VTxt(body, "bigRound", new Vector2(0f, 420f), new Vector2(1920f, 180f), "ROUND " + bat.round, 140f, new Color32(byte.MaxValue, 60, 60, 190), TextAlignmentOptions.Top), Ui.At(0.05f), 0f, 0f, 1.6f, 0.6f);
			if (hard)
			{
				RectTransform warn = Ui.Node(body, "hard", new Vector2(0f, 600f), new Vector2(560f, 50f), Ui.TC, Ui.TC);
				Ui.Pulse(Ui.FillImg(warn, "bg", new Color32(215, 25, 63, 235)), 0.65f, 1f, 0.9f);
				Ui.In(warn, Ui.At(0.6f), 0f, 0f, 2f, 0.35f, back: true);
				Ui.VTxt(warn, "t", Vector2.zero, new Vector2(560f, 50f), "HARD JUDGEMENT", 30f, Color.white, TextAlignmentOptions.Midline);
			}
			// 양옆: 티어 엠블럼 + 티어 + 프로필 (왼쪽 = 나)
			for (int side = 0; side < 2; side++)
			{
				int sl = (side == 0) ? me : (1 - me);
				Player p = Find(bat.ids[sl]);
				float cx = (side == 0) ? 430f : 1490f;
				int rt = (p != null && p.id == AI_ID) ? aiRating : ((p?.tier > 0) ? p.tier : bat.tier);
				Ui.In(TierEmblem(body, new Vector2(cx, 470f), 250f, rt, label: false), Ui.At(0.15f), (side == 0) ? -220f : 220f, 0f, 0.6f, 0.55f, back: true);
				Ui.In(Ui.VTxt(body, "tier" + side, new Vector2(cx - 300f, 690f), new Vector2(600f, 44f), TierText(rt), 34f, Color.white, TextAlignmentOptions.Top), Ui.At(0.3f), 0f, 24f);
				if (p != null)
				{
					RectTransform card = Ui.Node(body, "pc" + side, new Vector2(cx - 220f, 770f), new Vector2(440f, 88f));
					ProfileCard(card, p);
					Ui.In(card, Ui.At(0.35f), 0f, 36f);
				}
			}
			// 가운데 세로줄: 지금 라운드를 가운데에 두고 앞뒤 한 칸씩
			int total = Mathf.Max(3, bat.order.Count);
			int first = Mathf.Clamp(bat.round - 2, 0, Mathf.Max(0, total - 3));
			for (int k = 0; k < 3 && first + k < total; k++)
			{
				int r = first + k;
				float y = 130f + k * 300f;
				RectTransform cell;
				if (r < bat.round - 1)
				{
					cell = RoundSongCell(bat.order[r], y, r, false, false);
				}
				else if (r == bat.round - 1)
				{
					cell = RoundSongCell(bat.order[r], y, r, false, true);
				}
				else
				{
					cell = RoundUnknownCell(y);
				}
				Ui.In(cell, Ui.At(0.1f + k * 0.08f), 0f, -40f, 1f, 0.45f);
			}
		}

		private RectTransform RoundSongCell(int cand, float y, int round, bool spinning, bool fresh)
		{
			const float JS = 180f;
			float x = 960f - JS / 2f;
			BaseMusicBundle b = CandBundle(bat.cands[cand], out MODE mode, out KEY key);
			float lv = LevelOf(b, key, (int)mode);
			RectTransform c = Ui.Node(body, "rc" + round, new Vector2(x, y), new Vector2(JS, 290f));
			// 제목 꼬리표 + BPM
			RectTransform tag = Ui.Node(c, "tag", new Vector2(-60f, 0f), new Vector2(JS + 120f, 30f));
			Ui.FillImg(tag, "bg", new Color(0.05f, 0.05f, 0.06f, 0.85f));
			Ui.Txt(tag, "t", Vector2.zero, new Vector2(JS + 120f, 30f), spinning ? "? ? ?" : (b?.title ?? "?"), 18f, Color.white, extra: true, TextAlignmentOptions.Midline).overflowMode = TextOverflowModes.Ellipsis;
			Ui.VTxt(c, "bpm", new Vector2(0f, 30f), new Vector2(JS, 20f), spinning ? "" : ((b != null) ? (b.info.GetBpmString() + " BPM") : ""), 13f, new Color(1f, 1f, 1f, 0.75f), TextAlignmentOptions.Midline);
			RectTransform jk = Ui.Node(c, "jk", new Vector2(0f, 52f), new Vector2(JS, JS + 34f));
			Ui.Cover(jk, "art", Vector2.zero, new Vector2(JS, JS), b?.Album);
			Ui.Img(jk, "keyBg", new Vector2(0f, JS), new Vector2(52f, 34f), new Color(0.12f, 0.12f, 0.14f, 1f));
			Ui.VTxt(jk, "key", new Vector2(0f, JS), new Vector2(52f, 34f), KeyName((int)key), 17f, Color.white, TextAlignmentOptions.Midline);
			Image d = Ui.Img(jk, "diff", new Vector2(52f, JS), new Vector2(JS - 52f, 34f), Color.white, ModeSprite((int)mode));
			Ui.VTxt(d.transform, "t", Vector2.zero, new Vector2(JS - 52f, 34f), ModeEng((int)mode) + ((lv > 0f) ? (" " + Mathf.FloorToInt(lv)) : ""), 15f, Color.white, TextAlignmentOptions.Midline);
			if (b != null)
			{
				LevelIcons(c, new Vector2(0f, 52f + JS + 38f), Mathf.FloorToInt(lv), JS, 12f);
			}
			if (spinning)
			{
				Ui.Frame(jk, Yellow, 4f);
			}
			else if (fresh)
			{
				// 멈춘 직후 0.5초 번쩍
				float pop = Mathf.Clamp01((Time.unscaledTime - rouletteAt - ROULETTE_TIME - ROULETTE_HOLD) / 0.5f);
				if (pop < 1f)
				{
					Ui.Img(jk, "flash", Vector2.zero, new Vector2(JS, JS + 34f), new Color(1f, 1f, 1f, 0.7f * (1f - pop)));
				}
				Ui.Frame(jk, Yellow, 3f);
			}
			// 지난 라운드: 양옆 WIN / LOSE 점수 (왼쪽 = 나)
			if (round < bat.hist.Count)
			{
				string[] h = bat.hist[round].Split(':');
				if (h.Length >= 2)
				{
					int me = Mathf.Max(0, MySlot);
					int sm = Int(h[me], 0, int.MaxValue);
					int so = Int(h[1 - me], 0, int.MaxValue);
					ScoreBox(c, new Vector2(-176f, 52f + 40f), sm, sm >= so, true);
					ScoreBox(c, new Vector2(JS + 6f, 52f + 40f), so, so >= sm, false);
				}
			}
			return c;
		}

		private static void ScoreBox(RectTransform c, Vector2 pos, int score, bool win, bool left)
		{
			RectTransform b = Ui.Node(c, left ? "sbL" : "sbR", pos, new Vector2(170f, 74f));
			Ui.FillImg(b, "bg", new Color(0.04f, 0.04f, 0.05f, 0.92f));
			if (win)
			{
				Ui.Img(b, "edge", left ? Vector2.zero : new Vector2(166f, 0f), new Vector2(4f, 74f), Ui.Gold);
			}
			Ui.VTxt(b, "l", new Vector2(left ? 14f : -14f, 8f), new Vector2(140f, 22f), win ? "WIN" : "LOSE", 16f, win ? Ui.Gold : new Color32(byte.MaxValue, 70, 70, byte.MaxValue), left ? TextAlignmentOptions.TopLeft : TextAlignmentOptions.TopRight, left ? Ui.TL : Ui.TR, left ? Ui.TL : Ui.TR);
			Ui.VTxt(b, "v", new Vector2(left ? 14f : -14f, 34f), new Vector2(150f, 32f), score.ToString(), 26f, Color.white, left ? TextAlignmentOptions.TopLeft : TextAlignmentOptions.TopRight, left ? Ui.TL : Ui.TR, left ? Ui.TL : Ui.TR);
		}

		// 아직 공개 안 된 라운드: ? 자켓
		private RectTransform RoundUnknownCell(float y)
		{
			const float JS = 180f;
			RectTransform c = Ui.Node(body, "unk" + (int)y, new Vector2(960f - JS / 2f, y + 52f), new Vector2(JS, JS));
			Ui.FillImg(c, "bg", new Color(0.05f, 0.05f, 0.06f, 0.92f));
			Ui.Frame(c, new Color(1f, 1f, 1f, 0.15f), 2f);
			Ui.Pulse(Ui.VTxt(c, "q", Vector2.zero, new Vector2(JS, JS), "?", 110f, Yellow, TextAlignmentOptions.Midline), 0.35f, 1f, 1.4f);
			return c;
		}

		// 지난 라운드 결과 목록
		private void RoundHistory(Vector2 pos)
		{
			if (bat.hist.Count == 0)
			{
				return;
			}
			Ui.VTxt(body, "rh", pos, new Vector2(560f, 30f), "ROUNDS", 22f, Ui.Gold, TextAlignmentOptions.TopLeft);
			for (int i = 0; i < bat.hist.Count; i++)
			{
				string[] h = bat.hist[i].Split(':');
				RectTransform r = Ui.Node(body, "rh" + i, pos + new Vector2(0f, 40 + i * 62), new Vector2(560f, 56f));
				Ui.In(r, Ui.At(0.2f + i * 0.06f), 60f, 0f, 1f, 0.4f);
				Ui.FillImg(r, "bg", new Color(0f, 0f, 0f, 0.65f));
				Ui.VTxt(r, "n", new Vector2(14f, 0f), new Vector2(60f, 56f), "R" + (i + 1), 22f, Color.white, TextAlignmentOptions.MidlineLeft);
				if (h.Length < 2)
				{
					Ui.Txt(r, "x", new Vector2(80f, 0f), new Vector2(460f, 56f), bat.hist[i], 18f, Ui.Light, extra: false, TextAlignmentOptions.MidlineLeft);
					continue;
				}
				int sa = Int(h[0], 0, int.MaxValue);
				int sb = Int(h[1], 0, int.MaxValue);
				Ui.VTxt(r, "a", new Vector2(80f, 0f), new Vector2(200f, 56f), sa.ToString("N0"), 22f, (sa >= sb) ? Blue : Sub, TextAlignmentOptions.MidlineLeft);
				Ui.VTxt(r, "b", new Vector2(-14f, 0f), new Vector2(200f, 56f), sb.ToString("N0"), 22f, (sb >= sa) ? Orange : Sub, TextAlignmentOptions.MidlineRight, Ui.TR, Ui.TR);
				if (i < bat.order.Count)
				{
					BaseMusicBundle cb = CandBundle(bat.cands[bat.order[i]], out _, out _);
					Ui.Txt(r, "song", new Vector2(250f, 0f), new Vector2(110f, 56f), cb?.title ?? "", 13f, Sub, extra: false, TextAlignmentOptions.Midline).overflowMode = TextOverflowModes.Ellipsis;
				}
			}
		}

		// 매치 결과: 위 RESULT, 가운데 라운드 줄(내 점수 | 곡 | 상대 점수) + 최종 스코어, 양옆 플레이어 카드(이긴 쪽 금빛)
		private void EndPhase(int slot)
		{
			int me = Mathf.Max(0, slot);
			Ui.In(Ui.VTxt(body, "result", new Vector2(0f, 30f), new Vector2(1920f, 90f), "RESULT", 76f, Color.white, TextAlignmentOptions.Top), Ui.At(0f), 0f, -30f, 1.15f, 0.6f);
			Image line = Ui.Img(body, "line", new Vector2(960f, 124f), new Vector2(260f, 4f), Color.white, "Music_Choice_Effect", Image.Type.Simple, Ui.TL, Ui.C);
			Ui.Every(line, delegate(float t)
			{
				float k = Mathf.Clamp01(t / 0.6f);
				line.rectTransform.localScale = new Vector3(Ui.OutQuart(k), 1f, 1f);
				return k >= 1f;
			}, Ui.At(0.15f));
			Ui.In(Ui.VTxt(body, "sub", new Vector2(0f, 138f), new Vector2(1920f, 30f), MatchTypeName(bat.kind), 20f, new Color(1f, 1f, 1f, 0.6f), TextAlignmentOptions.Top), Ui.At(0.2f));
			// 가운데 라운드 줄
			int rounds = Mathf.Min(3, Mathf.Min(bat.hist.Count, bat.order.Count));
			float y = 196f;
			for (int k = 0; k < rounds; k++)
			{
				RoundRow(k, y, me);
				y += 162f;
			}
			// 최종 스코어
			bool draw = bat.winner == -1;
			bool iWin = bat.winner >= 0 && bat.winner == bat.ids[me];
			int pm = bat.pts[me];
			int po = bat.pts[1 - me];
			TextMeshProUGUI fs = Ui.VTxt(body, "final", new Vector2(0f, y + 6f), new Vector2(1920f, 110f), "<color=" + Ui.Hex(draw ? Color.white : (iWin ? Ui.Gold : new Color(1f, 1f, 1f, 0.55f))) + ">" + pm + "</color><size=60%><color=#FFFFFF80>  :  </color></size><color=" + Ui.Hex(draw ? Color.white : (!iWin ? Ui.Gold : new Color(1f, 1f, 1f, 0.55f))) + ">" + po + "</color>", 96f, Color.white, TextAlignmentOptions.Top);
			Ui.In(fs, Ui.At(0.9f), 0f, 0f, 1.6f, 0.45f, back: true);
			if (draw)
			{
				Ui.In(Ui.VTxt(body, "draw", new Vector2(0f, y + 118f), new Vector2(1920f, 36f), "DRAW", 30f, Color.white, TextAlignmentOptions.Top), Ui.At(1.1f), 0f, 0f, 1.6f, 0.4f, back: true);
			}
			// 양옆 플레이어 카드 (왼쪽 = 나)
			for (int side = 0; side < 2; side++)
			{
				int sl = (side == 0) ? me : (1 - me);
				SideCard(side, sl, slot);
			}
		}

		private void RoundRow(int k, float y, int me)
		{
			const float W = 760f;
			const float H = 150f;
			RectTransform r = Ui.Node(body, "rr" + k, new Vector2(580f, y), new Vector2(W, H));
			Ui.In(r, Ui.At(0.3f + k * 0.12f), 0f, 40f, 1f, 0.55f);
			Ui.FillImg(r, "bg", Glass);
			string[] h = bat.hist[k].Split(':');
			int sm = (h.Length >= 2) ? Int(h[me], 0, int.MaxValue) : 0;
			int so = (h.Length >= 2) ? Int(h[1 - me], 0, int.MaxValue) : 0;
			BaseMusicBundle b = CandBundle(bat.cands[bat.order[k]], out MODE mode, out KEY key);
			float lv = LevelOf(b, key, (int)mode);
			Ui.VTxt(r, "rn", new Vector2(18f, 12f), new Vector2(200f, 20f), "ROUND " + (k + 1), 14f, new Color(1f, 1f, 1f, 0.5f), TextAlignmentOptions.TopLeft);
			if (HardRound(k + 1))
			{
				Chip(r, "hard", new Vector2(W - 78f, 10f), new Vector2(64f, 22f), "HARD", Coral, Color.white, 12f);
			}
			// 가운데: 자켓 + 제목 + 난이도
			RectTransform jk = Ui.Node(r, "jk", new Vector2(W / 2f - 50f, 14f), new Vector2(100f, 100f));
			Ui.FillImg(jk, "bg", Glass);
			Ui.Cover(jk, "art", Vector2.zero, new Vector2(100f, 100f), b?.Album);
			Ui.Txt(r, "t", new Vector2(W / 2f - 140f, 118f), new Vector2(280f, 24f), (b?.title ?? "?") + "  <color=" + Ui.Hex(ModeColor((int)mode)) + "><size=85%>" + ModeEng((int)mode) + ((lv > 0f) ? (" " + Mathf.FloorToInt(lv)) : "") + "</size></color>", 16f, Color.white, extra: true, TextAlignmentOptions.Top).overflowMode = TextOverflowModes.Ellipsis;
			// 양옆 점수
			for (int s2 = 0; s2 < 2; s2++)
			{
				int sc = (s2 == 0) ? sm : so;
				bool win = sc >= ((s2 == 0) ? so : sm);
				bool left = s2 == 0;
				float x = left ? 30f : (W / 2f + 80f);
				TextMeshProUGUI st = Ui.VTxt(r, "s" + s2, new Vector2(x, 44f), new Vector2(W / 2f - 110f, 50f), "", 38f, win ? Color.white : new Color(1f, 1f, 1f, 0.45f), left ? TextAlignmentOptions.TopRight : TextAlignmentOptions.TopLeft);
				Ui.Count(st, 0f, sc, Ui.At(0.45f + k * 0.12f), 0.8f, (float v) => ((int)v).ToString("N0"));
				RectTransform ch = Chip(r, "w" + s2, new Vector2(left ? (W / 2f - 80f - 70f) : (W / 2f + 80f), 98f), new Vector2(70f, 24f), win ? "WIN" : "LOSE", win ? Ui.Gold : new Color(1f, 1f, 1f, 0.1f), win ? new Color(0.1f, 0.08f, 0f) : new Color(1f, 1f, 1f, 0.5f), 13f);
				Ui.In(ch, Ui.At(1f + k * 0.12f), 0f, 0f, 1.6f, 0.3f, back: true);
			}
		}

		private void SideCard(int side, int sl, int slot)
		{
			const float W = 440f;
			const float H = 650f;
			Player p = Find(bat.ids[sl]);
			bool win = bat.winner >= 0 && bat.winner == bat.ids[sl];
			bool lose = bat.winner >= 0 && !win;
			int rt = (p != null && p.id == AI_ID) ? aiRating : ((p != null && p.id == myId && bat.kind == MT_LADDER) ? MyRating : ((p?.tier > 0) ? p.tier : bat.tier));
			RectTransform c = Ui.Node(body, "side" + side, new Vector2((side == 0) ? 70f : (1920f - 70f - W), 190f), new Vector2(W, H));
			if (lose)
			{
				c.gameObject.AddComponent<CanvasGroup>().alpha = 0.72f;
			}
			Ui.In(c, Ui.At(0.35f), (side == 0) ? -90f : 90f, 0f, 1f, 0.6f);
			Ui.FillImg(c, "bg", Glass);
			Color tcol = TierColor(rt);
			Image glow = Ui.Img(c, "glow", Vector2.zero, new Vector2(W, 320f), new Color(tcol.r, tcol.g, tcol.b, win ? 0.32f : 0.16f));
			glow.sprite = Ui.Fade();
			glow.rectTransform.localEulerAngles = new Vector3(0f, 0f, 180f);
			glow.rectTransform.pivot = Ui.C;
			glow.rectTransform.anchoredPosition = new Vector2(W / 2f, -160f);
			// 위 머리글: WINNER / LOSE / DRAW
			string head = win ? "WINNER" : (lose ? "LOSE" : "DRAW");
			TextMeshProUGUI ht = Ui.VTxt(c, "head", new Vector2(0f, 22f), new Vector2(W, 40f), head, win ? 34f : 26f, win ? Ui.Gold : new Color(1f, 1f, 1f, 0.6f), TextAlignmentOptions.Top);
			ht.characterSpacing = 8f;
			Ui.In(ht, Ui.At(1.2f), 0f, 0f, win ? 2f : 1.3f, 0.4f, back: win);
			Ui.In(TierEmblem(c, new Vector2(W / 2f, 190f), 190f, rt, label: false), Ui.At(0.6f), 0f, 0f, 0.3f, 0.6f, back: true);
			Ui.In(Ui.VTxt(c, "tier", new Vector2(0f, 300f), new Vector2(W, 40f), TierText(rt), 30f, tcol, TextAlignmentOptions.Top), Ui.At(0.75f), 0f, 16f);
			TextMeshProUGUI pt = Ui.VTxt(c, "pt", new Vector2(0f, 342f), new Vector2(W, 30f), rt + " PT", 22f, new Color(1f, 1f, 1f, 0.8f), TextAlignmentOptions.Top);
			if (!(side == 0 && slot >= 0 && bat.kind == MT_LADDER))
			{
				// 이 사람의 라운드 승수 · 총점
				int total = 0;
				foreach (string hs in bat.hist)
				{
					string[] h = hs.Split(':');
					if (h.Length >= 2)
					{
						total += Int(h[sl], 0, int.MaxValue);
					}
				}
				string[] sl2 = new string[2] { "ROUNDS WON", "TOTAL SCORE" };
				string[] sv = new string[2] { bat.pts[sl].ToString(), total.ToString("N0") };
				for (int k = 0; k < 2; k++)
				{
					RectTransform st = Ui.Node(c, "stat" + k, new Vector2(40f, 410f + k * 64f), new Vector2(W - 80f, 54f));
					Ui.FillImg(st, "bg", new Color(1f, 1f, 1f, 0.05f));
					Ui.VTxt(st, "l", new Vector2(18f, 0f), new Vector2(200f, 54f), sl2[k], 14f, new Color(1f, 1f, 1f, 0.55f), TextAlignmentOptions.MidlineLeft);
					Ui.VTxt(st, "v", new Vector2(-18f, 0f), new Vector2(200f, 54f), sv[k], 26f, (k == 0 && win) ? Ui.Gold : Color.white, TextAlignmentOptions.MidlineRight, Ui.TR, Ui.TR);
					Ui.In(st, Ui.At(0.9f + k * 0.08f), 0f, 20f);
				}
			}
			// 래더: 내 점수 변동 (숫자가 올라가고 막대가 차오름)
			if (side == 0 && slot >= 0 && bat.kind == MT_LADDER)
			{
				int r = MyRating;
				int before = (batBefore > 0) ? batBefore : (r - batDelta);
				int delta = batDelta;
				Ui.Count(pt, before, r, Ui.At(1.4f), 0.9f, (float v) => Mathf.RoundToInt(v) + " PT");
				RectTransform dc = Chip(c, "delta", new Vector2(W / 2f - 70f, 382f), new Vector2(140f, 40f), "", (delta >= 0) ? new Color(0.35f, 0.87f, 0.65f, 0.2f) : new Color(1f, 0.38f, 0.42f, 0.2f), (delta >= 0) ? Ui.Green : Ui.Red, 24f);
				TextMeshProUGUI dt = dc.Find("t").GetComponent<TextMeshProUGUI>();
				Ui.Count(dt, 0f, delta, Ui.At(1.4f), 0.9f, (float v) => ((delta >= 0) ? "+" : "") + Mathf.RoundToInt(v) + " PT");
				Ui.In(dc, Ui.At(1.3f), 0f, 0f, 1.4f, 0.4f, back: true);
				RectTransform bar = Ui.Node(c, "bar", new Vector2(60f, 440f), new Vector2(W - 120f, 8f));
				Ui.Img(bar, "bg", Vector2.zero, new Vector2(W - 120f, 8f), new Color(1f, 1f, 1f, 0.12f));
				Image fill = Ui.Img(bar, "fill", Vector2.zero, new Vector2((W - 120f) * TierProgress(r), 8f), tcol);
				bool same = TierText(before) == TierText(r);
				Ui.Grow(fill.rectTransform, same ? ((W - 120f) * TierProgress(before)) : 0f, (W - 120f) * TierProgress(r), Ui.At(1.4f), 0.9f);
				if (!same)
				{
					bool up = r > before;
					Ui.In(Chip(c, "promo", new Vector2(W / 2f - 150f, 462f), new Vector2(300f, 32f), (up ? "승급  " : "강등  ") + TierText(before) + " → " + TierText(r), up ? new Color(1f, 0.84f, 0.29f, 0.22f) : new Color(1f, 0.38f, 0.42f, 0.22f), up ? Ui.Gold : Ui.Red, 15f), Ui.At(2.2f), 0f, 0f, 1.5f, 0.4f, back: true);
				}
			}
			if (p != null)
			{
				RectTransform pc = Ui.Node(c, "pc", new Vector2(0f, H - 88f), new Vector2(W, 88f));
				ProfileCard(pc, p);
			}
			if (win)
			{
				Ui.Frame(c, Ui.Gold, 3f);
				Ui.Pulse(c.Find("frame").GetComponent<Image>(), 0.45f, 1f, 1.6f);
				Ui.Img(c, "topline", Vector2.zero, new Vector2(W, 5f), Color.white, "Music_Choice_Effect");
				Image flash = Ui.FillImg(c, "flash", new Color(1f, 1f, 1f, 0f));
				Ui.Every(flash, delegate(float t)
				{
					float k = Mathf.Clamp01(t / 0.6f);
					flash.color = new Color(1f, 0.95f, 0.8f, (t < 0f) ? 0f : (0.45f * (1f - k)));
					return k >= 1f;
				}, Ui.At(1.2f));
				Ui.Shine(c, 0.18f, 3.2f);
			}
		}

		// 곡 후보 카드: 자켓 + 제목 + 아티스트 + 난이도/레벨 + 내 최고 정확도
		private RectTransform CandCard(int i, Vector2 pos, Vector2 size, bool selected)
		{
			RectTransform c = Ui.Node(body, "cand" + i + "_" + (int)pos.x, pos, size);
			Ui.FillImg(c, "bg", new Color(0.05f, 0.05f, 0.07f, 0.95f));
			if (i < 0 || i >= bat.cands.Count)
			{
				return c;
			}
			BaseMusicBundle b = CandBundle(bat.cands[i], out MODE mode, out KEY key);
			float jh = size.x * 9f / 16f;
			Ui.Cover(c, "jacket", Vector2.zero, new Vector2(size.x, jh), b?.Album);
			float fs = (size.x >= 400f) ? 1.3f : 1f;
			Ui.Txt(c, "title", new Vector2(18f, jh + 14f), new Vector2(size.x - 36f, 44f * fs), b?.title ?? "내 게임에 없는 곡", 24f * fs, (b == null) ? Ui.Red : Color.white, extra: true, TextAlignmentOptions.TopLeft).overflowMode = TextOverflowModes.Ellipsis;
			Ui.Txt(c, "artist", new Vector2(19f, jh + 14f + 40f * fs), new Vector2(size.x - 36f, 24f), b?.artist ?? "", 15f * fs, Sub, extra: false, TextAlignmentOptions.TopLeft).overflowMode = TextOverflowModes.Ellipsis;
			float lv = LevelOf(b, key, (int)mode);
			// 큰 카드(라운드 공개): 레벨 아이콘 + BPM · 콤보
			if (size.y >= 500f && b != null)
			{
				LevelIcons(c, new Vector2(14f, jh + 110f), Mathf.FloorToInt(lv), size.x - 28f);
				int combo = 0;
				try
				{
					b.ChangeMode(key, mode);
					combo = b.GetSheetPack(key)?.combo ?? 0;
				}
				catch (System.Exception)
				{
				}
				System.TimeSpan ts = System.TimeSpan.FromSeconds(b.info.time);
				Ui.VTxt(c, "info", new Vector2(18f, jh + 156f), new Vector2(size.x - 36f, 28f), "BPM " + b.info.GetBpmString() + "   COMBO " + combo.ToString("N0") + "   TIME " + ts.Minutes + ":" + ts.Seconds.ToString("00"), 17f, Ui.Light, TextAlignmentOptions.TopLeft);
			}
			else if (b != null)
			{
				System.TimeSpan ts2 = System.TimeSpan.FromSeconds(b.info.time);
				Ui.VTxt(c, "info", new Vector2(18f, jh + 100f), new Vector2(size.x - 36f, 24f), "BPM " + b.info.GetBpmString() + "   TIME " + ts2.Minutes + ":" + ts2.Seconds.ToString("00"), 15f, Ui.Light, TextAlignmentOptions.TopLeft);
			}
			Image d = Ui.Img(c, "diff", new Vector2(18f, size.y - 100f), new Vector2(size.x - 36f, 40f), Color.white, ModeSprite((int)mode));
			Ui.VTxt(d.transform, "t", Vector2.zero, new Vector2(size.x - 36f, 40f), KeyName((int)key) + "  " + ModeEng((int)mode) + ((lv > 0f) ? ("  LV." + Mathf.FloorToInt(lv)) : ""), 17f * fs, Color.white, TextAlignmentOptions.Midline);
			string best = "-";
			try
			{
				if (b != null)
				{
					float r = BundlePackage.user.GetHighScore(b.RootFileName, mode, isPlusMode: false).noteScore.rate;
					best = (r > 0f) ? (r.ToString("0.00") + "%") : "-";
				}
			}
			catch (System.Exception)
			{
			}
			Ui.VTxt(c, "best", new Vector2(18f, size.y - 48f), new Vector2(size.x - 36f, 30f), "MY BEST  " + best, 15f * fs, Ui.Gold, TextAlignmentOptions.MidlineLeft);
			if (selected)
			{
				Ui.Frame(c, Yellow, 4f);
			}
			return c;
		}

		// ---------------- 1:1 비교 패널 (디맥 래더 인게임 패널과 같은 구성) ----------------
		// ROUND / 곡(자켓·제목·레벨) / 버튼·난이도 띠 / SCORE LEAD 원형 미터(위 화살표, 파랑=왼쪽 사람, 주황=오른쪽) / 이름 / RATE / BREAK
		private void BuildMatchPanel(RectTransform parent, bool ingame, Vector2? at = null)
		{
			const float W = 420f;
			Vector2 pos = at ?? HudPos(W);
			RectTransform pnl = Ui.Node(parent, "match", pos, new Vector2(W, 900f));
			List<Player> mp = MP;
			bool battle = matchKind >= MT_VERSUS;
			float y = 0f;
			// ROUND
			RectTransform hd = Ui.Node(pnl, "round", new Vector2(0f, y), new Vector2(W, 56f));
			Ui.FillImg(hd, "bg", new Color32(215, 25, 63, 240));
			string head = battle ? ("ROUND " + bat.round) : "1 VS 1";
			Ui.VTxt(hd, "t", Vector2.zero, new Vector2(W, 56f), head, 34f, Color.white, TextAlignmentOptions.Midline);
			if (battle)
			{
				Ui.VTxt(hd, "pts", new Vector2(-14f, 0f), new Vector2(90f, 56f), bat.pts[0] + "-" + bat.pts[1], 20f, new Color(1f, 1f, 1f, 0.85f), TextAlignmentOptions.MidlineRight, Ui.TR, Ui.TR);
			}
			y += 56f;
			// 곡: 자켓 + 제목 + 아티스트 + 레벨 아이콘
			Player me = mp.FirstOrDefault((Player p) => p.id == myId);
			int mode = (me != null) ? me.diffMode : (int)matchMode;
			float lv = LevelOf(BaseOf(matchRoot, matchKey), matchKey, mode);
			RectTransform song = Ui.Node(pnl, "song", new Vector2(0f, y), new Vector2(W, 96f));
			Ui.FillImg(song, "bg", new Color(0.04f, 0.04f, 0.06f, 0.92f));
			Ui.Cover(song, "jacket", new Vector2(8f, 8f), new Vector2(80f, 80f), matchJacket);
			Ui.Txt(song, "title", new Vector2(98f, 6f), new Vector2(W - 108f, 30f), matchTitle, 22f, Color.white, extra: true, TextAlignmentOptions.TopLeft).overflowMode = TextOverflowModes.Ellipsis;
			Ui.Txt(song, "artist", new Vector2(99f, 36f), new Vector2(W - 108f, 20f), matchArtist, 14f, Sub, extra: false, TextAlignmentOptions.TopLeft).overflowMode = TextOverflowModes.Ellipsis;
			LevelIcons(song, new Vector2(94f, 60f), Mathf.FloorToInt(lv), W - 108f);
			y += 96f;
			// 버튼 · 난이도 띠
			RectTransform strip = Ui.Node(pnl, "strip", new Vector2(0f, y), new Vector2(W, 34f));
			Ui.FillImg(strip, "bg", new Color32(30, 170, 90, 245));
			Ui.FillImg(strip, "fx", new Color(1f, 1f, 1f, 0.25f), "Music_Choice_Effect");
			Ui.VTxt(strip, "k", new Vector2(12f, 0f), new Vector2(200f, 34f), KeyName((int)matchKey) + " <size=60%>TUNES</size>", 22f, Color.white, TextAlignmentOptions.MidlineLeft);
			RectTransform mt = Ui.Node(strip, "mode", new Vector2(-8f, 5f), new Vector2(124f, 24f), Ui.TR, Ui.TR);
			Ui.FillImg(mt, "bg", new Color(0f, 0f, 0f, 0.55f));
			Ui.Frame(mt, ModeColor(mode), 2f);
			Ui.VTxt(mt, "t", Vector2.zero, new Vector2(124f, 24f), ModeEng(mode) + ((me != null && me.plus) || (battle && matchJudge == 1) ? "+" : ""), 13f, ModeColor(mode), TextAlignmentOptions.Midline);
			y += 34f;
			DuelSide ds = DuelNow();
			string lName = ds.lName, rName = ds.rName;
			int lScore = ds.lScore, rScore = ds.rScore, lBrk = ds.lBrk, rBrk = ds.rBrk;
			float lRate = ds.lRate, rRate = ds.rRate;
			// SCORE LEAD 원형 미터: 위 화살표에서 파랑은 왼쪽으로, 주황은 오른쪽으로. 점수 비율만큼 아래 경계가 움직인다
			const float R = 136f;
			RectTransform ringArea = Ui.Node(pnl, "ring", new Vector2(0f, y), new Vector2(W, 320f));
			Ui.FillImg(ringArea, "bg", new Color(0.04f, 0.04f, 0.06f, 0.88f));
			Vector2 c = new Vector2(W / 2f, 160f);
			float share = (lScore + rScore > 0) ? ((float)lScore / (float)(lScore + rScore)) : 0.5f;
			Image back = Ui.Img(ringArea, "back", c - new Vector2(R, R), new Vector2(R * 2f, R * 2f), Orange);
			back.sprite = Ui.Ring();
			Image front = Ui.Img(ringArea, "front", c - new Vector2(R, R), new Vector2(R * 2f, R * 2f), Blue);
			front.sprite = Ui.Ring();
			front.type = Image.Type.Filled;
			front.fillMethod = Image.FillMethod.Radial360;
			front.fillOrigin = 2;
			front.fillClockwise = false;
			front.fillAmount = share;
			// 두 색 사이 틈 (위 고정, 아래는 비율 위치)
			RingGap(ringArea, c, R, 0f);
			RectTransform gap = RingGap(ringArea, c, R, share * 360f);
			Image disc = Ui.Img(ringArea, "disc", c - new Vector2(R - 26f, R - 26f), new Vector2((R - 26f) * 2f, (R - 26f) * 2f), new Color(0.95f, 0.95f, 0.96f, 1f));
			disc.sprite = Ui.Disc();
			Ui.VTxt(ringArea, "lbl", new Vector2(0f, c.y - 44f), new Vector2(W, 22f), "SCORE LEAD", 15f, new Color(0.45f, 0.45f, 0.5f), TextAlignmentOptions.Midline);
			// 화살표: 링 바깥에서 파랑/주황 경계를 가리킨다 (점수 비율에 따라 링을 따라 움직임)
			float ang = share * 360f;
			float rad = (90f + ang) * Mathf.Deg2Rad;
			Vector2 ap = c + new Vector2(Mathf.Cos(rad), -Mathf.Sin(rad)) * (R + 12f);
			Image tri = Ui.Img(ringArea, "arrow", Vector2.zero, new Vector2(30f, 16f), Color.white, "Triangle");
			tri.rectTransform.pivot = Ui.C;
			tri.rectTransform.anchoredPosition = new Vector2(ap.x, -ap.y);
			tri.rectTransform.localEulerAngles = new Vector3(0f, 0f, ang);
			int lead = lScore - rScore;
			TMP_Text leadT = Ui.VTxt(ringArea, "lead", new Vector2(0f, c.y - 22f), new Vector2(W, 64f), Mathf.Abs(lead).ToString("N0"), (Mathf.Abs(lead) >= 100000) ? 40f : 54f, (lead > 0) ? Blue : ((lead < 0) ? Orange : new Color(0.2f, 0.2f, 0.24f)), TextAlignmentOptions.Midline);
			y += 320f;
			// 이름 (가운데 사선으로 나뉜 파랑 / 주황)
			RectTransform names = Ui.Node(pnl, "names", new Vector2(0f, y), new Vector2(W, 44f));
			names.gameObject.AddComponent<RectMask2D>();
			Ui.Img(names, "l", Vector2.zero, new Vector2(W, 44f), new Color32(18, 70, 120, 245));
			Image rb = Ui.Img(names, "r", new Vector2(W / 2f + 10f, -20f), new Vector2(W, 90f), new Color32(120, 62, 16, 245));
			rb.rectTransform.localEulerAngles = new Vector3(0f, 0f, -18f);
			Ui.Txt(names, "lt", new Vector2(10f, 0f), new Vector2(W / 2f - 24f, 44f), lName, 19f, Color.white, extra: true, TextAlignmentOptions.Midline).overflowMode = TextOverflowModes.Ellipsis;
			Ui.Txt(names, "rt", new Vector2(W / 2f + 14f, 0f), new Vector2(W / 2f - 24f, 44f), rName, 19f, Color.white, extra: true, TextAlignmentOptions.Midline).overflowMode = TextOverflowModes.Ellipsis;
			y += 44f;
			VsRow(pnl, ref y, "RATE", Yellow, lRate.ToString("0.00") + "%", rRate.ToString("0.00") + "%", W);
			VsRow(pnl, ref y, "BREAK", new Color32(215, 25, 63, byte.MaxValue), lBrk.ToString("N0"), rBrk.ToString("N0"), W);
			if (!ingame)
			{
				VsRow(pnl, ref y, "SCORE", Color.white, lScore.ToString("N0"), rScore.ToString("N0"), W);
				return;
			}
			// 인게임: 점수가 바뀌어도 패널을 다시 그리지 않는다. 매 프레임 실제 점수를 읽어 표시값이 부드럽게 따라간다
			if (ldAt != hudAt)
			{
				ldAt = hudAt;
				ldShare = share;
				ldLead = lead;
				ldL = lRate;
				ldR = rRate;
				ldShareV = ldLeadV = 0f;
			}
			TMP_Text lrT = pnl.Find("vsRATE/l").GetComponent<TMP_Text>();
			TMP_Text rrT = pnl.Find("vsRATE/r").GetComponent<TMP_Text>();
			TMP_Text lbT = pnl.Find("vsBREAK/l").GetComponent<TMP_Text>();
			TMP_Text rbT = pnl.Find("vsBREAK/r").GetComponent<TMP_Text>();
			TMP_Text lnT = pnl.Find("names/lt").GetComponent<TMP_Text>();
			TMP_Text rnT = pnl.Find("names/rt").GetComponent<TMP_Text>();
			float last = 0f;
			Ui.Every(ringArea, delegate(float x)
			{
				float dt = Mathf.Max(0.0001f, x - last);
				last = x;
				DuelSide n = DuelNow();
				float tShare = (n.lScore + n.rScore > 0) ? ((float)n.lScore / (float)(n.lScore + n.rScore)) : 0.5f;
				float tLead = n.lScore - n.rScore;
				// 스프링처럼 감속하며 따라감 (화살표·링·숫자가 같이 미끄러짐)
				ldShare = Mathf.SmoothDamp(ldShare, tShare, ref ldShareV, 0.28f, float.PositiveInfinity, dt);
				ldLead = Mathf.SmoothDamp(ldLead, tLead, ref ldLeadV, 0.22f, float.PositiveInfinity, dt);
				float k = 1f - Mathf.Exp(-dt * 8f);
				ldL = Mathf.Lerp(ldL, n.lRate, k);
				ldR = Mathf.Lerp(ldR, n.rRate, k);
				front.fillAmount = ldShare;
				float a = ldShare * 360f;
				PlaceOnRing(gap, c, R - 13f, a);
				PlaceOnRing(tri.rectTransform, c, R + 12f, a);
				int ld = Mathf.RoundToInt(ldLead);
				leadT.text = Mathf.Abs(ld).ToString("N0");
				leadT.fontSize = (Mathf.Abs(ld) >= 100000) ? 40f : 54f;
				// 앞서는 쪽 색으로, 0 근처에선 회색으로 자연스럽게 섞임
				float t = Mathf.Clamp01(Mathf.Abs(ldLead) / 3000f);
				leadT.color = Color.Lerp(new Color(0.2f, 0.2f, 0.24f), (ldLead >= 0f) ? Blue : Orange, t);
				lrT.text = ldL.ToString("0.00") + "%";
				rrT.text = ldR.ToString("0.00") + "%";
				lbT.text = n.lBrk.ToString("N0");
				rbT.text = n.rBrk.ToString("N0");
				lnT.text = n.lName;
				rnT.text = n.rName;
				return false;
			}, Time.unscaledTime);
		}

		private float ldAt = -1f, ldShare = 0.5f, ldLead, ldL, ldR, ldShareV, ldLeadV;

		private struct DuelSide
		{
			public string lName, rName;

			public int lScore, rScore, lBrk, rBrk;

			public float lRate, rRate;
		}

		// 비교 대상: 대전 = 두 대전자(나는 왼쪽), 팀전 = 우리 팀 vs 가장 센 상대 팀, 개인전 = 나 vs 상대 (관전이면 1등 vs 2등)
		private DuelSide DuelNow()
		{
			List<Player> mp = MP;
			Player me = mp.FirstOrDefault((Player p) => p.id == myId);
			bool battle = matchKind >= MT_VERSUS;
			DuelSide d = new DuelSide
			{
				lName = "",
				rName = ""
			};
			if (matchKind == MT_TEAM && mp.Select((Player p) => p.team).Distinct().Count() > 1 && mp.Count > 2)
			{
				int[] teams = (from t in Enumerable.Range(0, 3)
					where mp.Any((Player p) => p.team == t)
					select t).ToArray();
				int lt = (me != null) ? me.team : teams[0];
				int rt = teams.Where((int t) => t != lt).OrderByDescending((int t) => TeamScore(mp, t)).DefaultIfEmpty(lt).First();
				List<Player> lm = mp.Where((Player p) => p.team == lt).ToList();
				List<Player> rm = mp.Where((Player p) => p.team == rt).ToList();
				d.lName = TeamName(lt);
				d.rName = TeamName(rt);
				d.lScore = Mathf.Max(0, TeamScore(mp, lt));
				d.rScore = Mathf.Max(0, TeamScore(mp, rt));
				d.lRate = (lm.Count > 0) ? lm.Average((Player p) => p.rate) : 0f;
				d.rRate = (rm.Count > 0) ? rm.Average((Player p) => p.rate) : 0f;
				d.lBrk = lm.Sum((Player p) => p.brk);
				d.rBrk = rm.Sum((Player p) => p.brk);
			}
			else
			{
				Player l;
				Player r;
				if (battle)
				{
					l = Find(bat.ids[0]);
					r = Find(bat.ids[1]);
				}
				else
				{
					Player[] order = mp.OrderByDescending((Player p) => p.score).ToArray();
					l = order.FirstOrDefault();
					r = order.Skip(1).FirstOrDefault();
				}
				if (r != null && r.id == myId)
				{
					Player x = l;
					l = r;
					r = x;
				}
				d.lName = l?.name ?? "";
				d.rName = r?.name ?? "";
				d.lScore = l?.score ?? 0;
				d.rScore = r?.score ?? 0;
				d.lRate = l?.rate ?? 0f;
				d.rRate = r?.rate ?? 0f;
				d.lBrk = l?.brk ?? 0;
				d.rBrk = r?.brk ?? 0;
			}
			return d;
		}

		// 링 위의 각도(위에서 시계 반대 방향) 위치에 놓고 그 방향으로 돌림
		private static void PlaceOnRing(RectTransform rt, Vector2 c, float r, float angle)
		{
			float rad = (90f + angle) * Mathf.Deg2Rad;
			Vector2 p = c + new Vector2(Mathf.Cos(rad), -Mathf.Sin(rad)) * r;
			rt.anchoredPosition = new Vector2(p.x, -p.y);
			rt.localEulerAngles = new Vector3(0f, 0f, angle);
		}

		// 원형 미터의 틈 (각도: 위에서 시계 반대 방향)
		private static RectTransform RingGap(RectTransform area, Vector2 c, float r, float angle)
		{
			RectTransform g = Ui.Node(area, "gap" + (int)angle, Vector2.zero, new Vector2(8f, 34f));
			g.pivot = Ui.C;
			PlaceOnRing(g, c, r - 13f, angle);
			Ui.FillImg(g, "bg", new Color(0.04f, 0.04f, 0.06f, 1f));
			return g;
		}

		// RATE / BREAK 줄: 가운데 비스듬한 색 꼬리표
		private static void VsRow(RectTransform pnl, ref float y, string label, Color lc, string l, string r, float W)
		{
			RectTransform row = Ui.Node(pnl, "vs" + label, new Vector2(0f, y), new Vector2(W, 46f));
			Ui.FillImg(row, "bg", new Color(0.04f, 0.04f, 0.06f, 0.88f));
			Ui.Img(row, "line", new Vector2(10f, 45f), new Vector2(W - 20f, 1f), new Color(1f, 1f, 1f, 0.08f));
			Color vc = (label == "BREAK") ? new Color32(byte.MaxValue, 70, 90, byte.MaxValue) : ((label == "RATE") ? Yellow : Color.white);
			Ui.VTxt(row, "l", Vector2.zero, new Vector2(W / 2f - 52f, 46f), l, 21f, vc, TextAlignmentOptions.Midline);
			Ui.VTxt(row, "r", new Vector2(W / 2f + 52f, 0f), new Vector2(W / 2f - 52f, 46f), r, 21f, vc, TextAlignmentOptions.Midline);
			RectTransform b = Ui.Node(row, "lbl", new Vector2(W / 2f - 48f, 12f), new Vector2(96f, 22f));
			Ui.FillImg(b, "bg", lc, "Main_Category_Middle", Image.Type.Sliced);
			Ui.VTxt(b, "t", Vector2.zero, new Vector2(96f, 22f), label, 12f, (label == "BREAK") ? Color.white : Color.black, TextAlignmentOptions.Midline);
			y += 46f;
		}

		// 1:1 인가 (대전, 또는 참가자가 2명) → SCORE LEAD 패널. 아니면 다인전/팀전 패널
		private bool IsDuel => matchKind >= MT_VERSUS || MP.Count == 2;

		// 인게임 패널 고르기 (디맥: 1:1 = 점수 차 미터, 다인전 = 모두의 정확도·브레이크, 팀전 = 팀 점수)
		private void BuildHudPanel()
		{
			if (IsDuel)
			{
				BuildMatchPanel(hud, ingame: true);
			}
			else if (matchKind == MT_TEAM)
			{
				BuildTeamPanel(hud, ingame: true);
			}
			else
			{
				BuildMultiPanel(hud, ingame: true);
			}
		}

		// 기어 반대편 (기어가 왼쪽이면 오른쪽)
		private static Vector2 HudPos(float w)
		{
			return new Vector2((BundlePackage.setting.GearPosition == 0) ? (1920f - 36f - w) : 36f, 36f);
		}

		// 곡 머리 (방식 + 곡)
		private float PanelHead(RectTransform pnl, float W, string title, Color c)
		{
			RectTransform hd = Ui.Node(pnl, "head", Vector2.zero, new Vector2(W, 48f));
			Ui.FillImg(hd, "bg", c);
			Ui.VTxt(hd, "t", Vector2.zero, new Vector2(W, 48f), title, 24f, Color.white, TextAlignmentOptions.Midline);
			RectTransform song = Ui.Node(pnl, "song", new Vector2(0f, 48f), new Vector2(W, 72f));
			Ui.FillImg(song, "bg", new Color(0f, 0f, 0f, 0.8f));
			Ui.Cover(song, "jacket", new Vector2(8f, 8f), new Vector2(56f, 56f), matchJacket);
			Ui.Txt(song, "title", new Vector2(74f, 8f), new Vector2(W - 84f, 30f), matchTitle, 21f, Color.white, extra: true, TextAlignmentOptions.TopLeft).overflowMode = TextOverflowModes.Ellipsis;
			Ui.Txt(song, "artist", new Vector2(75f, 40f), new Vector2(W - 84f, 20f), matchArtist, 14f, Sub, extra: false, TextAlignmentOptions.TopLeft).overflowMode = TextOverflowModes.Ellipsis;
			return 120f;
		}

		// 다인전 (오픈 매치 3명 이상): 모두의 순위 · 패턴 · 정확도 · 브레이크 · 점수
		private void BuildMultiPanel(RectTransform parent, bool ingame, Vector2? at = null)
		{
			const float W = 420f;
			List<Player> mp = MP;
			RectTransform pnl = Ui.Node(parent, "multi", at ?? HudPos(W), new Vector2(W, 900f));
			float y = PanelHead(pnl, W, "OPEN MATCH  <size=70%>" + mp.Count + " PLAYERS</size>", MatchTypeColor(MT_FREE) * new Color(0.75f, 0.75f, 0.75f, 0.95f));
			Player[] order = mp.OrderByDescending((Player p) => p.score).ToArray();
			float rh = (order.Length > 5) ? 74f : 86f;
			for (int k = 0; k < order.Length; k++)
			{
				Player p2 = order[k];
				bool isMe = p2.id == myId;
				int rank = order.Count((Player o) => o.score > p2.score);
				RectTransform row = Ui.Node(pnl, "r" + k, new Vector2(0f, y + 4f + k * (rh + 4f)), new Vector2(W, rh));
				if (isMe)
				{
					Ui.FillImg(row, "sel", Color.white, "Music_Choice_Effect");
				}
				else
				{
					Ui.FillImg(row, "bg", new Color(0f, 0f, 0f, 0.72f));
				}
				Ui.VTxt(row, "rank", new Vector2(12f, 0f), new Vector2(40f, rh), (rank + 1).ToString(), 30f, isMe ? Color.white : RankColor(rank), TextAlignmentOptions.MidlineLeft);
				Ui.Txt(row, "name", new Vector2(50f, 8f), new Vector2(190f, 28f), p2.name, 19f, Color.white, extra: true, TextAlignmentOptions.TopLeft).overflowMode = TextOverflowModes.Ellipsis;
				string sub = (p2.offline && !p2.done) ? "<color=#FF606A>연결 끊김</color>" : (p2.done ? StateText(p2) : p2.matchDiff);
				Ui.Txt(row, "diff", new Vector2(51f, 36f), new Vector2(190f, 20f), sub, 13f, ModeColor(p2.diffMode), extra: false, TextAlignmentOptions.TopLeft).overflowMode = TextOverflowModes.Ellipsis;
				Ui.VTxt(row, "score", new Vector2(-12f, 6f), new Vector2(170f, 30f), p2.score.ToString("N0"), 22f, Color.white, TextAlignmentOptions.TopRight, Ui.TR, Ui.TR);
				Ui.VTxt(row, "rb", new Vector2(-12f, rh - 30f), new Vector2(200f, 22f), "<color=#FFD54A>" + p2.rate.ToString("0.00") + "%</color>   <color=#FF606A>BREAK " + p2.brk + "</color>", 14f, Color.white, TextAlignmentOptions.TopRight, Ui.TR, Ui.TR);
			}
		}

		// 팀전: 팀별 평균 점수 + 팀원
		private void BuildTeamPanel(RectTransform parent, bool ingame, Vector2? at = null)
		{
			const float W = 420f;
			List<Player> mp = MP;
			RectTransform pnl = Ui.Node(parent, "team", at ?? HudPos(W), new Vector2(W, 900f));
			float y = PanelHead(pnl, W, "TEAM MATCH  <size=70%>평균 점수</size>", MatchTypeColor(MT_TEAM) * new Color(0.75f, 0.75f, 0.75f, 0.95f));
			int[] teams = (from t in Enumerable.Range(0, 3)
				where mp.Any((Player p) => p.team == t)
				orderby TeamScore(mp, t) descending
				select t).ToArray();
			Player me = mp.FirstOrDefault((Player p) => p.id == myId);
			int top = (teams.Length > 0) ? Mathf.Max(1, TeamScore(mp, teams[0])) : 1;
			foreach (int t in teams)
			{
				List<Player> m = mp.Where((Player p) => p.team == t).OrderByDescending((Player p) => p.score).ToList();
				int ts = Mathf.Max(0, TeamScore(mp, t));
				bool mine = me != null && me.team == t;
				RectTransform hb = Ui.Node(pnl, "t" + t, new Vector2(0f, y + 6f), new Vector2(W, 58f));
				Ui.FillImg(hb, "bg", new Color(0f, 0f, 0f, 0.8f));
				Ui.Img(hb, "bar", Vector2.zero, new Vector2(W * ts / top, 58f), TeamColor(t) * new Color(1f, 1f, 1f, 0.35f));
				Ui.Img(hb, "c", Vector2.zero, new Vector2(8f, 58f), TeamColor(t));
				Ui.VTxt(hb, "n", new Vector2(18f, 0f), new Vector2(200f, 58f), TeamName(t) + (mine ? "  <size=70%>MY TEAM</size>" : ""), 22f, TeamColor(t), TextAlignmentOptions.MidlineLeft);
				Ui.VTxt(hb, "s", new Vector2(-12f, 0f), new Vector2(200f, 58f), ts.ToString("N0"), 26f, Color.white, TextAlignmentOptions.MidlineRight, Ui.TR, Ui.TR);
				y += 64f;
				foreach (Player p2 in m)
				{
					bool isMe = p2.id == myId;
					RectTransform row = Ui.Node(pnl, "m" + p2.id, new Vector2(16f, y), new Vector2(W - 16f, 34f));
					Ui.FillImg(row, "bg", isMe ? new Color(1f, 1f, 1f, 0.2f) : new Color(0f, 0f, 0f, 0.6f));
					Ui.Txt(row, "n", new Vector2(10f, 0f), new Vector2(150f, 34f), p2.name, 15f, Color.white, extra: true, TextAlignmentOptions.MidlineLeft).overflowMode = TextOverflowModes.Ellipsis;
					Ui.VTxt(row, "rb", new Vector2(150f, 0f), new Vector2(160f, 34f), "<color=#FFD54A>" + p2.rate.ToString("0.00") + "%</color>  <color=#FF606A><size=85%>BREAK</size> " + p2.brk + "</color>", 12f, Color.white, TextAlignmentOptions.MidlineLeft);
					Ui.VTxt(row, "s", new Vector2(-10f, 0f), new Vector2(120f, 34f), p2.score.ToString("N0"), 15f, Color.white, TextAlignmentOptions.MidlineRight, Ui.TR, Ui.TR);
					y += 36f;
				}
				y += 6f;
			}
		}

		// ---------------- 곡 바꿀 때 애니메이션 ----------------
		private RectTransform animList;

		private RectTransform animJacket;

		private CanvasGroup animJacketGroup;

		private float songAnimAt = -10f;

		private int songAnimDir;

		private string animRoot = "";

		private int animCursor;

		// 곡이 바뀐 순간 기록 (방장 = 커서 이동 방향, 손님 = 방장 곡 따라감)
		private void NoteSongChange()
		{
			string root = (Cur?.RootFileName ?? "") + cursor;
			if (root == animRoot)
			{
				return;
			}
			int d = cursor - animCursor;
			if (list.Length > 0 && Mathf.Abs(d) > list.Length / 2)
			{
				d = -(int)Mathf.Sign(d);
			}
			songAnimDir = (d == 0) ? 0 : (int)Mathf.Sign(d);
			songAnimAt = (animRoot.Length > 0) ? Time.unscaledTime : -10f;
			animRoot = root;
			animCursor = cursor;
		}

		// 목록은 한 칸 밀렸다가 제자리로, 자켓·제목은 옆에서 미끄러지며 나타남 (게임 곡 선택과 비슷하게)
		private void AnimateScreen()
		{
			if (scr == Scr.Ladder && spinner != null)
			{
				spinner.localEulerAngles = new Vector3(0f, 0f, -Time.unscaledTime * 240f);
			}
			if (scr != Scr.Room)
			{
				return;
			}
			float t = Mathf.Clamp01((Time.unscaledTime - songAnimAt) / 0.16f);
			float e = 1f - (1f - t) * (1f - t) * (1f - t);
			if (animList != null)
			{
				animList.anchoredPosition = new Vector2(0f, -songAnimDir * 69f * (1f - e));
			}
			float t2 = Mathf.Clamp01((Time.unscaledTime - songAnimAt) / 0.28f);
			float e2 = 1f - (1f - t2) * (1f - t2) * (1f - t2);
			if (animJacket != null)
			{
				animJacket.anchoredPosition = new Vector2(60f * (1f - e2), 0f);
			}
			if (animJacketGroup != null)
			{
				animJacketGroup.alpha = Mathf.Lerp(0.15f, 1f, e2);
			}
		}

	}
}
