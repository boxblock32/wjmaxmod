// 최근 대전 기록 (OnlineMod\history.txt, 최신이 위, 50개까지)
// 한 줄 = 날짜 \t 곡 \t 방식 \t 팀결과(이긴팀|A|B|C) \t 플레이어(이름|점수|정확도|상태|팀|나|난이도)...
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using TMPro;
using UnityEngine;

namespace WJMAXOnline
{
	public partial class OnlineMod
	{
		private static string HistoryPath => Path.Combine(Path.GetDirectoryName(Application.dataPath), "OnlineMod", "history.txt");

		private readonly List<string[]> history = new List<string[]>();

		private bool historySaved;

		private int histSel;

		private void SaveHistory(MatchResult m)
		{
			if (historySaved || m.ps.Count == 0)
			{
				return;
			}
			historySaved = true;
			try
			{
				string team = (m.kind == MT_TEAM) ? (TeamWinner(m.ps) + "|" + TeamScore(m.ps, 0) + "|" + TeamScore(m.ps, 1) + "|" + TeamScore(m.ps, 2)) : "";
				string line = DateTime.Now.ToString("yyyy-MM-dd HH:mm") + "\t" + NoTab(m.title) + "\t" + MatchTypeName(m.kind) + ((m.kind >= MT_VERSUS) ? (" R" + m.round) : "") + "\t" + team + "\t" + string.Join("\t", m.ps.OrderByDescending((Player p) => p.score).Select((Player p) => p.name + "|" + p.score + "|" + p.rate.ToString("0.00", IC) + "|" + StateCode(p) + "|" + p.team + "|" + ((p.id == myId) ? 1 : 0) + "|" + p.matchDiff.Replace("|", " ")));
				List<string> lines = File.Exists(HistoryPath) ? File.ReadAllLines(HistoryPath, Encoding.UTF8).ToList() : new List<string>();
				lines.Insert(0, line);
				Directory.CreateDirectory(Path.GetDirectoryName(HistoryPath));
				File.WriteAllLines(HistoryPath, lines.Take(50), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
				LoadHistory();
			}
			catch (Exception e)
			{
				Debug.LogWarning("[WJMAXOnline] history save failed: " + e.Message);
			}
		}

		private static string NoTab(string s)
		{
			return (s ?? "").Replace('\t', ' ').Replace('\n', ' ').Replace('\r', ' ');
		}

		private static string StateCode(Player p)
		{
			if (!p.done)
			{
				return "P";
			}
			if (p.fullCombo)
			{
				return "F";
			}
			return p.cleared ? "C" : "X";
		}

		private void LoadHistory()
		{
			history.Clear();
			try
			{
				if (File.Exists(HistoryPath))
				{
					history.AddRange(from l in File.ReadAllLines(HistoryPath, Encoding.UTF8)
						select l.Split('\t') into f
						where f.Length >= 5
						select f);
				}
			}
			catch (Exception e)
			{
				Debug.LogWarning("[WJMAXOnline] history load failed: " + e.Message);
			}
		}

		private void InputHistory()
		{
			if (history.Count > 0)
			{
				if (Repeat(KeyCode.UpArrow))
				{
					histSel = (histSel + history.Count - 1) % history.Count;
					PlaySe(AudioManager.SE.SlideMove);
				}
				else if (Repeat(KeyCode.DownArrow))
				{
					histSel = (histSel + 1) % history.Count;
					PlaySe(AudioManager.SE.SlideMove);
				}
			}
			if (Key(KeyCode.Escape) || Enter() || Key(KeyCode.F11))
			{
				PlaySe(AudioManager.SE.Confirm);
				SetScr(connected ? Scr.Room : Scr.Lobby);
			}
		}

		// 기록 한 줄에서 내 결과 (개인전 순위 또는 팀전 승패)
		private void HistResult(string[] f, out string big, out Color bigColor)
		{
			string[][] ps = f.Skip(4).Select((string x) => x.Split('|')).Where((string[] x) => x.Length >= 6).ToArray();
			int myIdx = Array.FindIndex(ps, (string[] x) => x[5] == "1");
			string[] team = f[3].Split('|');
			if (team.Length >= 3)
			{
				int w = Int(team[0], -1, 2);
				int myTeam = (myIdx >= 0) ? Int(ps[myIdx][4], 0, 2) : (-1);
				big = (w < 0) ? "DRAW" : ((w == myTeam) ? "WIN" : "LOSE");
				bigColor = (w < 0) ? Color.white : ((w == myTeam) ? Ui.Gold : Ui.Gray);
			}
			else
			{
				big = (myIdx >= 0) ? Ranks[Mathf.Clamp(myIdx, 0, 7)] : "-";
				bigColor = RankColor(Mathf.Max(myIdx, 0));
			}
		}

		private void BuildHistory()
		{
			Ui.FillImg(body, "shade", new Color(0f, 0f, 0f, 0.55f));
			Logo("RECORDS");
			Hotkeys("ESC", connected ? "방으로" : "로비로", "↑↓", "이동");
			RectTransform p = InfoPanel((history.Count == 0) ? 220f : 760f);
			if (history.Count == 0)
			{
				PanelHead(p, "RECORDS", "최근 대전 기록");
				TextMeshProUGUI e = Ui.Txt(p, "e", new Vector2(26f, 100f), new Vector2(450f, 100f), "아직 기록이 없어요.\n친구와 한 곡 플레이하면 여기에 남아요.", 20f, Ui.Light, extra: false, TextAlignmentOptions.TopLeft);
				e.lineSpacing = 12f;
				return;
			}
			histSel = Mathf.Clamp(histSel, 0, history.Count - 1);
			string[] f = history[histSel];
			string[][] ps = f.Skip(4).Select((string x) => x.Split('|')).Where((string[] x) => x.Length >= 6).ToArray();
			bool isTeam = f[3].Split('|').Length >= 3;
			PanelHead(p, "RECORDS", (histSel + 1) + " / " + history.Count);
			Ui.Txt(p, "title", new Vector2(26f, 96f), new Vector2(450f, 40f), f[1], 30f, Color.white, extra: true, TextAlignmentOptions.TopLeft).overflowMode = TextOverflowModes.Ellipsis;
			Tag(p, new Vector2(26f, 144f), 200f, f[2], Color.white, filled: true);
			Ui.Txt(p, "date", new Vector2(26f, 184f), new Vector2(450f, 22f), f[0], 16f, Sub, extra: false, TextAlignmentOptions.TopLeft);
			HistResult(f, out string big, out Color bigColor);
			Label(p, 0f, 230f, isTeam ? "TEAM RESULT" : "MY RANK", TextAlignmentOptions.Top, PW);
			Ui.VTxt(p, "big", new Vector2(0f, 256f), new Vector2(PW, 80f), big, 60f, bigColor, TextAlignmentOptions.Top);
			Ui.Img(p, "sep", new Vector2(26f, 350f), new Vector2(448f, 1f), new Color(1f, 1f, 1f, 0.15f));
			for (int k = 0; k < ps.Length && k < 6; k++)
			{
				string[] x = ps[k];
				float y = 366 + k * 64;
				bool me = x[5] == "1";
				Color nameColor = isTeam ? TeamColor(Int(x[4], 0, 2)) : (me ? Ui.Gold : Color.white);
				Ui.VTxt(p, "r" + k, new Vector2(26f, y), new Vector2(60f, 30f), Ranks[k], 20f, RankColor(k), TextAlignmentOptions.TopLeft);
				Ui.Txt(p, "n" + k, new Vector2(96f, y), new Vector2(220f, 30f), x[0], 21f, nameColor, extra: true, TextAlignmentOptions.TopLeft).overflowMode = TextOverflowModes.Ellipsis;
				Ui.VTxt(p, "s" + k, new Vector2(300f, y), new Vector2(174f, 30f), Int(x[1], 0, int.MaxValue).ToString("N0"), 21f, Color.white, TextAlignmentOptions.TopRight);
				string st = (x[3] == "F") ? "FULL COMBO" : ((x[3] == "C") ? "CLEAR" : ((x[3] == "X") ? "FAILED" : "-"));
				Ui.Txt(p, "d" + k, new Vector2(96f, y + 30f), new Vector2(378f, 22f), st + "  ·  " + x[2] + "%" + ((x.Length > 6 && x[6].Length > 0) ? ("  ·  " + x[6]) : ""), 14f, Sub, extra: false, TextAlignmentOptions.TopLeft);
			}
			const int PAGE = 8;
			int first = Mathf.Clamp(histSel - PAGE / 2, 0, Mathf.Max(0, history.Count - PAGE));
			for (int k = 0; k < PAGE && first + k < history.Count; k++)
			{
				string[] h = history[first + k];
				bool s = first + k == histSel;
				RectTransform r = Row("h" + k, TOP + k * 92, 82f, s);
				HistResult(h, out string b2, out Color c2);
				Ui.VTxt(r, "big", new Vector2(30f, 0f), new Vector2(130f, 82f), b2, 30f, s ? Color.white : c2, TextAlignmentOptions.MidlineLeft);
				Ui.Txt(r, "title", new Vector2(170f, 12f), new Vector2(640f, 34f), h[1], 26f, Color.white, extra: false, TextAlignmentOptions.TopLeft).overflowMode = TextOverflowModes.Ellipsis;
				Ui.Txt(r, "sub", new Vector2(171f, 48f), new Vector2(640f, 20f), h[2] + "  ·  " + (h.Length - 4) + "명", 15f, s ? Color.white : Sub, extra: false, TextAlignmentOptions.TopLeft);
				string[] mine = h.Skip(4).Select((string x) => x.Split('|')).FirstOrDefault((string[] x) => x.Length >= 6 && x[5] == "1");
				if (mine != null)
				{
					Ui.VTxt(r, "score", new Vector2(660f, 10f), new Vector2(420f, 34f), Int(mine[1], 0, int.MaxValue).ToString("N0") + "  <size=70%>" + mine[2] + "%</size>", 26f, Color.white, TextAlignmentOptions.TopRight);
				}
				Ui.Txt(r, "date", new Vector2(660f, 50f), new Vector2(420f, 22f), h[0], 15f, s ? Color.white : Sub, extra: false, TextAlignmentOptions.TopRight);
			}
		}
	}
}
