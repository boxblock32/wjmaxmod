// 래더 매치 (AI 상대). 팬게임이라 사람끼리 매칭할 서버가 없으므로, 디맥의 봇 매칭처럼 내 점수 근처의 AI 와 1:1.
// 흐름: 래더 화면 → 매칭 시작(상대 찾는 중...) → MATCH FOUND → 밴픽 → 라운드(내가 곡을 치는 동안 AI 점수를 실시간으로 계산) → 결과·점수 변동
//
// AI 실력 모델 (게임 레벨 숫자 기준, 거짓말 4렙 = 24):
//   실력 K = SkillOf(AI 점수) : 이 레벨 곡을 정확도 94~95% 로 치는 수준
//   곡 레벨 L 과의 차이 d = L - K + 오프셋
//   정확도 = 100 - (0.4 + 22 / (1 + e^(-1.2 (d - 1))))      d=-2 → 99.0%, d=0 → 94.5%, d=2 → 82.7%
//   브레이크 = 포아송(0.28 e^(1.2 d)) + 실수                   그랜드마스터(2000점)가 24렙 → 풀콤 약 80%
//   오프셋: 그날 컨디션 N(0, 0.45) (판마다) + 라운드 흔들림 N(0, 0.35)
//           운 좋음 12% (-0.8), 실수 최대 15% (+1.2, 브레이크 +2~8), 크게 무너짐 최대 5% (+2.5) - 실수는 어려운 곡일수록 잦음, 하드 판정 +1.0
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using WJMAX.UserRecord;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace WJMAXOnline
{
	public partial class OnlineMod
	{
		private const int AI_ID = 99;

		private bool aiMatch; // AI 와 대전 중 (네트워크 없이 내가 방장 역할)

		private int aiRating;

		private float aiForm; // 이번 판 컨디션 (레벨 단위)

		private float aiBanAt;

		private float aiP;

		private static readonly System.Reflection.FieldInfo fJudgeCount = typeof(Record).GetField("judgeCount", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);

		private static readonly System.Reflection.FieldInfo fMaxCount = typeof(Record).GetField("maxCount", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);


		private bool aiPlanned;

		private int aiScore, aiBrk, aiCombo;

		private float aiRate;

		private bool aiClear;

		private string aiEvent = "";

		// 래더 화면
		private int ladState; // 0 대기, 1 상대 찾는 중, 2 찾음

		private float ladAt;

		private float ladFoundAt;

		private Player ladFoe;

		private static readonly string[] AiNames = new string[16]
		{
			"NB RANGER", "NB RIDER", "NB HEARTS", "NB PILOT", "NB SHADOW", "NB BLADE", "NB NOVA", "NB ACE",
			"NB COMET", "NB VIPER", "NB ECHO", "NB FLARE", "NB GHOST", "NB STORM", "NB PULSE", "NB ZERO"
		};

		// 티어별 실력 안내 (점수 → 실력 모델로 계산한 대략적인 수준)
		private static readonly string[] TierSkill = new string[7]
		{
			"5렙을 겨우 클리어",
			"5렙 무난, 6~8렙 클리어",
			"9~11렙 클리어 (엔젤 고렙 · 왁굳 입문)",
			"12~15렙 클리어 (왁굳)",
			"16~19렙 클리어 (민수)",
			"20~23렙 클리어 (민수 고렙 · 거짓말 1~3)",
			"거짓말 4렙(24) 풀콤 80% 이상"
		};

		private static float Gauss(System.Random r, float sd)
		{
			double u1 = 1.0 - r.NextDouble();
			double u2 = r.NextDouble();
			return (float)(Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2)) * sd;
		}

		private static int Poisson(System.Random r, float lambda)
		{
			if (lambda > 30f)
			{
				return Mathf.Max(0, Mathf.RoundToInt(lambda + Gauss(r, Mathf.Sqrt(lambda))));
			}
			double l = Math.Exp(-lambda);
			int k = 0;
			double p = 1.0;
			do
			{
				k++;
				p *= r.NextDouble();
			}
			while (p > l);
			return k - 1;
		}

		private readonly System.Random aiRnd = new System.Random();

		// ---------------- 래더 화면 ----------------
		private void EnterLadder()
		{
			ladState = 0;
			LoadLadderLog();
			SetScr(Scr.Ladder);
			PlaySe(AudioManager.SE.Confirm);
		}

		private void InputLadder()
		{
			if (opOpen)
			{
				InputOnePick();
				return;
			}
			if (ladState == 1)
			{
				if (Key(KeyCode.Escape) || Enter())
				{
					ladState = 0;
					PlaySe(AudioManager.SE.SlideMove);
				}
				return;
			}
			if (ladState == 2)
			{
				return;
			}
			if (Enter())
			{
				ladState = 1;
				ladAt = Time.unscaledTime;
				ladFoundAt = ladAt + UnityEngine.Random.Range(2.5f, 5.5f);
				PlaySe(AudioManager.SE.Confirm);
			}
			else if (Key(KeyCode.Tab))
			{
				KEY next = (GameValue.key == KEY.Key4) ? KEY.Key6 : KEY.Key4;
				if (BundlePackage.musicBundle.GetMusicBundles(FREESTYLE_CATEGORY.ALL, next).Any())
				{
					GameValue.key = next;
					PlaySe(AudioManager.SE.SlideMove);
				}
			}
			else if (Key(KeyCode.F2))
			{
				OpenOnePick();
			}
			else if (Key(KeyCode.F6))
			{
				PlayerPrefs.DeleteKey(OnePickKey);
				Status("ONE PICK 을 지웠어요", Ui.Gray);
				PlaySe(AudioManager.SE.SlideMove);
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
				SetScr(Scr.Lobby);
			}
		}

		// ---------------- ONE PICK (디맥: 버튼별로 자신 있는 곡 1개를 정해 두면 후보 곡에 나올 수 있음) ----------------
		private static string OnePickKey => "WJONLINE_ONEPICK_" + (int)GameValue.key;

		// "파일명:난이도:키" (지금 티어 범위의 채보만)
		private static string OnePick
		{
			get
			{
				string v = PlayerPrefs.GetString(OnePickKey, "");
				if (v.Split(':').Length < 3 || CandBundle(v, out MODE m, out KEY k) == null)
				{
					return "";
				}
				return v;
			}
		}

		// ONE PICK 고르기 창: 지금 티어 범위 곡 목록에서 ↑↓ / ENTER (게임 검색창을 쓰지 않음)
		private bool opOpen;

		private int opSel;

		private readonly List<string> opList = new List<string>();

		private void OpenOnePick()
		{
			opList.Clear();
			opList.AddRange(TierPool(RangeFor(MyRating), GameValue.key, 12).OrderBy((string x) => LevelOf(CandBundle(x, out MODE m, out KEY k), k, (int)m)).ThenBy((string x) => CandBundle(x, out _, out _)?.title));
			if (opList.Count == 0)
			{
				Status("지금 티어 범위에 곡이 없어요", Ui.Gray);
				PlaySe(AudioManager.SE.error);
				return;
			}
			string cur = OnePick;
			opSel = Mathf.Max(0, opList.FindIndex((string x) => cur.Length > 0 && x.Split(':')[0] == cur.Split(':')[0]));
			opOpen = true;
			PlaySe(AudioManager.SE.Confirm);
		}

		private void InputOnePick()
		{
			if (Repeat(KeyCode.UpArrow))
			{
				opSel = (opSel + opList.Count - 1) % opList.Count;
				PlaySe(AudioManager.SE.SlideMove);
			}
			else if (Repeat(KeyCode.DownArrow))
			{
				opSel = (opSel + 1) % opList.Count;
				PlaySe(AudioManager.SE.SlideMove);
			}
			else if (Enter())
			{
				string v = opList[opSel];
				PlayerPrefs.SetString(OnePickKey, v);
				BaseMusicBundle b = CandBundle(v, out MODE m, out KEY k);
				Status("ONE PICK: " + (b?.title ?? "?") + " " + ModeName((int)m) + " Lv." + Mathf.FloorToInt(LevelOf(b, k, (int)m)), Ui.Green);
				opOpen = false;
				PlaySe(AudioManager.SE.Confirm);
			}
			else if (Key(KeyCode.Escape))
			{
				opOpen = false;
				PlaySe(AudioManager.SE.SlideMove);
			}
		}

		private void OnePickBox()
		{
			Ui.FillImg(body, "opdim", new Color(0f, 0f, 0f, 0.75f));
			const int VIS = 9;
			const float RH = 74f;
			float h = 170f + VIS * RH;
			RectTransform box = Ui.Node(body, "opbox", new Vector2(0f, 540f - h / 2f), new Vector2(760f, h), Ui.TC, Ui.TC);
			Ui.FillImg(box, "bg", new Color(0.06f, 0.06f, 0.08f, 0.98f));
			// 왁맥 팝업 모양: 얇은 회색 테두리
			Ui.Img(box, "footer", new Vector2(0f, box.sizeDelta.y - 78f), new Vector2(760f, 78f), new Color32(40, 40, 44, 255));
			Ui.Frame(box, new Color(0.72f, 0.72f, 0.75f, 1f), 2f);
			Ui.VTxt(box, "t", new Vector2(36f, 22f), new Vector2(300f, 40f), "ONE PICK", 30f, Ui.Gold, TextAlignmentOptions.TopLeft);
			Ui.Txt(box, "s", new Vector2(230f, 30f), new Vector2(500f, 30f), KeyName((int)GameValue.key) + " · 지금 티어 범위 곡 " + opList.Count + "개", 16f, Sub, extra: false, TextAlignmentOptions.TopLeft);
			string cur = OnePick;
			int top = Mathf.Clamp(opSel - VIS / 2, 0, Mathf.Max(0, opList.Count - VIS));
			for (int i = top; i < opList.Count && i < top + VIS; i++)
			{
				bool on = i == opSel;
				BaseMusicBundle b = CandBundle(opList[i], out MODE m, out KEY k);
				RectTransform r = Ui.Node(box, "r" + i, new Vector2(30f, 80f + (i - top) * RH), new Vector2(700f, RH - 6f));
				if (on)
				{
					Ui.FillImg(r, "sel", Color.white, "Music_Choice_Effect");
				}
				else
				{
					Ui.FillImg(r, "bg", new Color(1f, 1f, 1f, 0.05f));
				}
				Ui.Cover(r, "jk", new Vector2(6f, 6f), new Vector2(100f, RH - 18f), b?.Album);
				Ui.Txt(r, "n", new Vector2(122f, 0f), new Vector2(380f, RH - 6f), b?.title ?? "?", 21f, Color.white, extra: true, TextAlignmentOptions.MidlineLeft).overflowMode = TextOverflowModes.Ellipsis;
				Image d = Ui.Img(r, "d", new Vector2(540f, (RH - 6f - 34f) / 2f), new Vector2(146f, 34f), Color.white, ModeSprite((int)m));
				Ui.VTxt(d.transform, "t", Vector2.zero, new Vector2(146f, 34f), ModeEng((int)m) + " " + Mathf.FloorToInt(LevelOf(b, k, (int)m)), 16f, Color.white, TextAlignmentOptions.Midline);
				if (cur.Length > 0 && opList[i].Split(':')[0] == cur.Split(':')[0])
				{
					Ui.VTxt(r, "cur", new Vector2(470f, 0f), new Vector2(64f, RH - 6f), "NOW", 14f, Ui.Gold, TextAlignmentOptions.Midline);
				}
			}
			Ui.HotkeyRightToLeft(box, 30f, h - 46f, "ENTER", "정하기", 18f);
			Ui.HotkeyLeftToRight(box, 30f, h - 46f, "ESC", "닫기", 18f);
		}

		// 매칭 연출 진행 (상대 찾는 중 → 찾음 → 밴픽)
		private void LadderTick()
		{
			if (scr != Scr.Ladder)
			{
				return;
			}
			float now = Time.unscaledTime;
			if (ladState == 1 && now >= ladFoundAt)
			{
				ladState = 2;
				ladAt = now;
				MakeFoe();
				PlaySe(AudioManager.SE.PlayMusic);
			}
			else if (ladState == 2 && now - ladAt >= 3f)
			{
				ladState = 0;
				StartAiLadder();
			}
		}

		// 내 점수 근처의 AI 상대
		private void MakeFoe()
		{
			int me = MyRating;
			aiRating = Mathf.Clamp(me + Mathf.RoundToInt(Gauss(aiRnd, 55f)), 1, RATING_MAX);
			aiForm = Gauss(aiRnd, 0.45f);
			ladFoe = new Player
			{
				id = AI_ID,
				name = AiNames[aiRnd.Next(AiNames.Length)],
				level = Mathf.Clamp(10 + aiRating / 25 + aiRnd.Next(-5, 6), 1, 150),
				tier = aiRating,
				ready = true,
				fighter = true,
				diffMode = 0,
				diffKey = (int)GameValue.key
			};
			try
			{
				// AI 아이콘·플레이트: 게임에 있는 아이템 중 무작위
				ItemIcon[] icons = BundlePackage.itemBundle.GetItems<ItemIcon>(Item.TYPE.ICON);
				ItemPlate[] plates = BundlePackage.itemBundle.GetItems<ItemPlate>(Item.TYPE.PLATE);
				if (icons != null && icons.Length > 0)
				{
					ladFoe.icon = CleanId(icons[aiRnd.Next(icons.Length)].ID);
				}
				if (plates != null && plates.Length > 0)
				{
					ladFoe.plate = CleanId(plates[aiRnd.Next(plates.Length)].ID);
				}
			}
			catch (Exception)
			{
			}
		}

		// 네트워크 없이 내가 방장인 1:1 방을 만들고 바로 밴픽
		private void StartAiLadder()
		{
			Disconnect();
			aiMatch = true;
			isHost = true;
			connected = true;
			myId = 0;
			players.Clear();
			Player me = new Player
			{
				id = 0,
				name = myName,
				fighter = true,
				ready = true,
				diffKey = (int)GameValue.key
			};
			SetMyProfile(me);
			players.Add(me);
			players.Add(ladFoe);
			matchType = MT_LADDER;
			maxPlayers = 2;
			roomCode = "";
			chatLog.Clear();
			lastMatch = null;
			bat = new BattleState
			{
				no = bat.no
			};
			BattleBegin();
			if (bat.phase == BAT_BAN)
			{
				SetScr(Scr.Battle);
			}
			else
			{
				EndAiLadder();
			}
		}

		private void EndAiLadder()
		{
			aiMatch = false;
			isHost = false;
			connected = false;
			players.Clear();
			matchIds.Clear();
			matchLive = false;
			myId = -1;
			bat = new BattleState
			{
				no = bat.no
			};
			LoadLadderLog();
			SetScr(Scr.Ladder);
		}

		// AI 밴: 대개 자기에게 가장 어려운(레벨 높은) 곡, 가끔 아무거나
		private int AiBan()
		{
			int n = Mathf.Max(1, bat.cands.Count);
			if (aiRnd.NextDouble() < 0.3)
			{
				return aiRnd.Next(n);
			}
			int best = 0;
			float hi = float.MinValue;
			for (int i = 0; i < bat.cands.Count; i++)
			{
				float lv = LevelOf(CandBundle(bat.cands[i], out MODE m, out KEY k), k, (int)m);
				if (lv > hi)
				{
					hi = lv;
					best = i;
				}
			}
			return best;
		}

		// ---------------- AI 점수 ----------------
		private MODE matchMode;

		// AI 한 곡 결과 (정확도 %, 브레이크 수). 맨 위 설명의 실력 모델
		private static void SimAi(System.Random rnd, int rating, float level, bool hard, float form, out float rate, out int brk, out string ev)
		{
			float d = level - SkillOf(rating) + form + Gauss(rnd, 0.35f);
			int extraBrk = 0;
			double roll = rnd.NextDouble();
			ev = "";
			// 실수·무너짐은 어려운 곡일수록 잦음 (쉬운 곡에서는 거의 없음)
			double sev = 1.0 / (1.0 + Math.Exp(-1.5 * (d + 1.5)));
			if (roll < 0.05 * sev)
			{
				d += 2.5f;
				extraBrk = rnd.Next(8, 20);
				ev = "크게 무너짐";
			}
			else if (roll < 0.20 * sev)
			{
				d += 1.2f;
				extraBrk = rnd.Next(2, 9);
				ev = "실수";
			}
			else if (roll < 0.20 * sev + 0.12)
			{
				d -= 0.8f;
				ev = "운 좋음";
			}
			if (hard)
			{
				d += 1f;
			}
			float loss = 0.4f + 22f / (1f + Mathf.Exp(-1.2f * (d - 1f)));
			brk = Mathf.Max(0, Poisson(rnd, Mathf.Min(3000f, 0.28f * Mathf.Exp(1.2f * d))) + extraBrk);
			rate = Mathf.Clamp(100f - loss + Gauss(rnd, 0.25f) - brk * 0.05f, 30f, (brk == 0) ? 100f : 99.9f);
		}

		// 라운드 시작 때 AI 최종 결과를 정해 둔다
		private void PlanAiRound()
		{
			aiPlanned = true;
			aiP = 0f;
			BaseMusicBundle b = BaseOf(matchRoot, matchKey);
			float level = LevelOf(b, matchKey, (int)matchMode);
			if (level <= 0f)
			{
				level = SkillOf(aiRating);
			}
			SimAi(aiRnd, aiRating, level, matchJudge == 1, aiForm, out aiRate, out aiBrk, out aiEvent);
			aiScore = Mathf.Clamp(Mathf.RoundToInt(aiRate * 10000f) + aiRnd.Next(0, 100), 0, Judgement.MAX_SCORE);
			int combo = 0;
			try
			{
				b?.ChangeMode(matchKey, matchMode);
				combo = b?.GetSheetPack(matchKey)?.combo ?? 1000;
			}
			catch (Exception)
			{
				combo = 1000;
			}
			aiCombo = (aiBrk == 0) ? combo : Mathf.Max(1, combo / (aiBrk + 1) + aiRnd.Next(0, combo / (aiBrk + 1) + 1));
			aiClear = aiRate >= 70f;
		}

		// 곡 진행에 맞춰 AI 점수를 조금씩 올린다 (인게임 SCORE LEAD 가 실시간으로 움직이게)
		private void AiTick()
		{
			if (!aiMatch || !matchLive)
			{
				aiPlanned = false;
				return;
			}
			Player ai = Find(AI_ID);
			if (ai == null || ai.done || !matchIds.Contains(AI_ID))
			{
				return;
			}
			if (!aiPlanned)
			{
				PlanAiRound();
			}
			if (!released)
			{
				return;
			}
			float now = Time.unscaledTime;
			// 디맥 봇처럼 노트 진행에 맞춰 점수가 오른다: 판정된 노트 수 / 전체 노트 수 (곡 시작 전엔 0, 일시정지 중엔 멈춤)
			Record rec = GetRecord();
			Music mu = Singleton<Music>.Instance;
			if (mu != null && mu.isPlaying && mu.currentSample > 0)
			{
				int jc = (rec != null) ? ((fJudgeCount?.GetValue(rec) as int?) ?? 0) : 0;
				int mc = (rec != null) ? ((fMaxCount?.GetValue(rec) as int?) ?? 0) : 0;
				float np = (mc > 0) ? ((float)jc / mc) : ((mu.maxSample > 0) ? ((float)mu.currentSample / mu.maxSample) : 0f);
				aiP = Mathf.Max(aiP, Mathf.Clamp01(np));
			}
			float p = aiP;
			Player me = Me;
			if (p >= 1f || (me != null && me.done))
			{
				string fin = "FIN|" + AI_ID + "|" + aiScore + "|" + aiRate.ToString("0.00", IC) + "|" + (aiClear ? 1 : 0) + "|" + ((aiBrk == 0) ? 1 : 0) + "|" + Mathf.RoundToInt(aiCombo * 0.8f) + "|" + Mathf.RoundToInt(aiCombo * 0.15f) + "|0|0|" + aiBrk + "|" + aiCombo;
				ApplyFinal(fin.Split('|'));
				ai.inRoom = true;
				ai.phase = PH_ROOM;
				aiPlanned = false;
				return;
			}
			ai.phase = PH_PLAYING;
			float wobble = 1f + Mathf.Sin(now * 1.7f) * 0.01f;
			ai.score = Mathf.Clamp(Mathf.RoundToInt(aiScore * p * wobble), 0, aiScore);
			ai.rate = Mathf.Clamp(aiRate + (1f - p) * Mathf.Sin(now * 0.9f) * 1.2f, 0f, 100f);
			ai.brk = Mathf.FloorToInt(aiBrk * p);
			ai.combo = Mathf.RoundToInt(aiCombo * p);
		}

		// ---------------- 래더 기록 (OnlineMod\ladder.txt) ----------------
		// 한 줄 = 날짜 \t W/L \t 변동 \t 결과 점수 \t 상대 \t 상대 점수 \t 세트 점수
		private static string LadderPath => Path.Combine(Path.GetDirectoryName(Application.dataPath), "OnlineMod", "ladder.txt");

		private readonly List<string[]> ladLog = new List<string[]>();

		private void LogLadder(bool win)
		{
			try
			{
				Player foe = Find(AI_ID);
				string line = DateTime.Now.ToString("MM-dd HH:mm") + "\t" + (win ? "W" : "L") + "\t" + batDelta + "\t" + MyRating + "\t" + (foe?.name ?? "?") + "\t" + aiRating + "\t" + bat.pts[MySlot] + ":" + bat.pts[1 - Mathf.Max(0, MySlot)];
				List<string> lines = File.Exists(LadderPath) ? File.ReadAllLines(LadderPath, Encoding.UTF8).ToList() : new List<string>();
				lines.Insert(0, line);
				Directory.CreateDirectory(Path.GetDirectoryName(LadderPath));
				File.WriteAllLines(LadderPath, lines.Take(30), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
			}
			catch (Exception e)
			{
				Debug.LogWarning("[WJMAXOnline] ladder log failed: " + e.Message);
			}
		}

		private void LoadLadderLog()
		{
			ladLog.Clear();
			try
			{
				if (File.Exists(LadderPath))
				{
					ladLog.AddRange(from l in File.ReadAllLines(LadderPath, Encoding.UTF8)
						select l.Split('\t') into f
						where f.Length >= 7
						select f);
				}
			}
			catch (Exception)
			{
			}
		}

		// ---------------- 래더 화면 그리기 ----------------
		private string LadderSig()
		{
			float now = Time.unscaledTime;
			return ladState + "|" + ((ladState == 1) ? ((int)(now - ladAt)).ToString() + Dots : "") + "|" + GameValue.key + "|" + MyRating + "|" + ladLog.Count + "|" + (ladFoe?.name ?? "") + "|" + OnePick + "|" + opOpen + opSel;
		}

		private void BuildLadder()
		{
			Ui.FillImg(body, "shade", new Color(0.04f, 0.02f, 0.07f, 0.74f));
			Logo("LADDER MATCH");
			Player mine = new Player
			{
				id = myId,
				name = myName
			};
			SetMyProfile(mine);
			Ui.In(ProfileCardAt(Ui.Node(body, "profile", new Vector2(-54f, 22f), new Vector2(440f, 88f), Ui.TR, Ui.TR), mine), Ui.At(0.1f), 60f);
			int r = MyRating;
			LadderMyTier(r);
			LadderRecent();
			LadderCenter(r);
			LadderTiers(r);
		}

		// 패널 위쪽에 색 빛 (아래로 사라짐)
		private static void TopGlow(RectTransform p, Color c, float w, float h, float a)
		{
			Image g = Ui.Img(p, "glow", new Vector2(w / 2f, h / 2f), new Vector2(w, h), new Color(c.r, c.g, c.b, a), null, Image.Type.Simple, Ui.TL, Ui.C);
			g.sprite = Ui.Fade();
			g.rectTransform.localEulerAngles = new Vector3(0f, 0f, 180f);
		}

		// 왼쪽 위: 내 티어
		private void LadderMyTier(int r)
		{
			const float W = 520f;
			Color tc = TierColor(r);
			RectTransform l = Ui.Node(body, "me", new Vector2(54f, 130f), new Vector2(W, 476f));
			Ui.FillImg(l, "bg", Glass);
			Ui.In(l, Ui.At(0.05f), -70f, 0f, 1f, 0.6f);
			TopGlow(l, tc, W, 320f, 0.32f);
			Ui.Img(l, "top", Vector2.zero, new Vector2(W, 4f), Color.white, "Music_Choice_Effect");
			Ui.VTxt(l, "my", new Vector2(0f, 22f), new Vector2(W, 22f), "MY TIER", 15f, new Color(1f, 1f, 1f, 0.55f), TextAlignmentOptions.Top).characterSpacing = 6f;
			Ui.In(TierEmblem(l, new Vector2(W / 2f, 136f), 156f, r, label: false), Ui.At(0.25f), 0f, 0f, 0.4f, 0.6f, back: true);
			Ui.In(Ui.VTxt(l, "tier", new Vector2(0f, 228f), new Vector2(W, 44f), TierText(r), 34f, tc, TextAlignmentOptions.Top), Ui.At(0.35f), 0f, 16f);
			TextMeshProUGUI pt = Ui.VTxt(l, "pt", new Vector2(0f, 272f), new Vector2(W, 60f), "", 50f, Color.white, TextAlignmentOptions.Top);
			Ui.Count(pt, 0f, r, Ui.At(0.35f), 0.9f, (float v) => Mathf.RoundToInt(v) + " <size=50%>PT</size>");
			RectTransform bar = Ui.Node(l, "bar", new Vector2(60f, 346f), new Vector2(W - 120f, 10f));
			Ui.Img(bar, "bg", Vector2.zero, new Vector2(W - 120f, 10f), new Color(1f, 1f, 1f, 0.12f));
			Ui.Grow(Ui.Img(bar, "fill", Vector2.zero, new Vector2((W - 120f) * TierProgress(r), 10f), tc).rectTransform, 0f, (W - 120f) * TierProgress(r), Ui.At(0.45f), 0.9f);
			TierDiv(r, out _, out int hi, out _);
			Ui.Txt(l, "next", new Vector2(0f, 362f), new Vector2(W, 24f), (TierGroup(r) == 6) ? "최고 티어 · 점수 제한 없음" : ("다음 단계까지 <color=#FFFFFF>" + (hi - r) + "점</color>"), 15f, new Color(1f, 1f, 1f, 0.6f), extra: false, TextAlignmentOptions.Top);
			int g0 = TierGroup(r);
			if (g0 > 0 && r - TierStart[g0] < 22)
			{
				Ui.Pulse(Chip(l, "risk", new Vector2(W - 168f, 16f), new Vector2(150f, 26f), "DEMOTION RISK", Coral, Color.white, 12f).Find("bg").GetComponent<Image>(), 0.6f, 1f, 1f);
			}
			int games = Wins + Losses;
			string[] sl = new string[3] { "WIN", "LOSE", "RATE" };
			string[] sv = new string[3] { Wins.ToString(), Losses.ToString(), (games > 0) ? ((Wins * 100 / games) + "%") : "-" };
			for (int k = 0; k < 3; k++)
			{
				RectTransform st = Ui.Node(l, "st" + k, new Vector2(24f + k * 162f, 400f), new Vector2(148f, 58f));
				Ui.FillImg(st, "bg", new Color(1f, 1f, 1f, 0.05f));
				Ui.VTxt(st, "l", new Vector2(14f, 0f), new Vector2(60f, 58f), sl[k], 12f, Ui.Gold, TextAlignmentOptions.MidlineLeft);
				Ui.VTxt(st, "v", new Vector2(-14f, 0f), new Vector2(90f, 58f), sv[k], 24f, Color.white, TextAlignmentOptions.MidlineRight, Ui.TR, Ui.TR);
				Ui.In(st, Ui.At(0.4f + k * 0.06f), 0f, 16f);
			}
		}

		// 왼쪽 아래: 최근 대전
		private void LadderRecent()
		{
			const float W = 520f;
			RectTransform rc = Ui.Node(body, "recent", new Vector2(54f, 622f), new Vector2(W, 348f));
			Ui.FillImg(rc, "bg", Glass);
			Ui.In(rc, Ui.At(0.12f), -70f, 0f, 1f, 0.6f);
			Ui.VTxt(rc, "h", new Vector2(24f, 18f), new Vector2(400f, 26f), "RECENT MATCHES", 18f, Ui.Gold, TextAlignmentOptions.TopLeft);
			if (ladLog.Count == 0)
			{
				TextMeshProUGUI nt = Ui.Txt(rc, "none", new Vector2(0f, 60f), new Vector2(W, 260f), "아직 래더 기록이 없어요\n<size=75%><color=#AFAFAF>ENTER 로 첫 매칭을 시작해 보세요</color></size>", 21f, Color.white, extra: true, TextAlignmentOptions.Midline);
				nt.lineSpacing = 18f;
				return;
			}
			for (int i = 0; i < ladLog.Count && i < 5; i++)
			{
				string[] f = ladLog[i];
				bool w = f[1] == "W";
				RectTransform row = Ui.Node(rc, "lg" + i, new Vector2(20f, 56f + i * 56f), new Vector2(W - 40f, 50f));
				Ui.FillImg(row, "bg", new Color(1f, 1f, 1f, 0.04f));
				Chip(row, "r", new Vector2(10f, 12f), new Vector2(60f, 26f), w ? "WIN" : "LOSE", w ? Ui.Gold : new Color(1f, 1f, 1f, 0.12f), w ? new Color(0.1f, 0.08f, 0f) : new Color(1f, 1f, 1f, 0.6f), 13f);
				Ui.Txt(row, "o", new Vector2(84f, 5f), new Vector2(280f, 24f), "vs " + f[4] + "  <color=#AFAFAF>" + f[6] + "</color>", 17f, Color.white, extra: false, TextAlignmentOptions.TopLeft).overflowMode = TextOverflowModes.Ellipsis;
				Ui.Txt(row, "t", new Vector2(84f, 28f), new Vector2(280f, 20f), f[0] + "  ·  상대 " + TierText(Int(f[5], 1, RATING_MAX)), 12f, Sub, extra: false, TextAlignmentOptions.TopLeft);
				int dl = Int(f[2], -99, 99);
				Ui.VTxt(row, "d", new Vector2(-16f, 0f), new Vector2(120f, 50f), ((dl > 0) ? "+" : "") + dl, 22f, (dl > 0) ? Ui.Green : Ui.Red, TextAlignmentOptions.MidlineRight, Ui.TR, Ui.TR);
				Ui.In(row, Ui.At(0.3f + i * 0.05f), -30f, 0f, 1f, 0.45f);
			}
		}

		// 가운데: 랭크 매치 (대기 / 찾는 중 / 찾음)
		private void LadderCenter(int r)
		{
			const float W = 700f;
			RectTransform c = Ui.Node(body, "mid", new Vector2(610f, 130f), new Vector2(W, 840f));
			Ui.FillImg(c, "bg", Glass);
			Ui.In(c, Ui.At(0.1f), 0f, 70f, 1f, 0.6f);
			TopGlow(c, new Color32(150, 90, 255, 255), W, 260f, 0.22f);
			Ui.Img(c, "top", Vector2.zero, new Vector2(W, 4f), Color.white, "Music_Choice_Effect");
			Ui.VTxt(c, "t", new Vector2(0f, 34f), new Vector2(W, 64f), "RANK MATCH", 54f, Color.white, TextAlignmentOptions.Top);
			Ui.Txt(c, "s", new Vector2(0f, 100f), new Vector2(W, 26f), "내 점수 근처의 AI 와 1:1  ·  밴픽  ·  2판 2선승", 18f, new Color(1f, 1f, 1f, 0.7f), extra: false, TextAlignmentOptions.Top);
			if (ladState == 0)
			{
				// 버튼 수 고르기
				KEY[] keys = new KEY[2] { KEY.Key4, KEY.Key6 };
				for (int k = 0; k < 2; k++)
				{
					bool on = GameValue.key == keys[k];
					RectTransform kp = Chip(c, "key" + k, new Vector2(W / 2f - 150f + k * 110f, 148f), new Vector2(100f, 40f), KeyName((int)keys[k]), on ? new Color32(30, 170, 130, byte.MaxValue) : new Color(1f, 1f, 1f, 0.07f), on ? Color.white : new Color(1f, 1f, 1f, 0.5f), 18f);
					if (on)
					{
						Ui.In(kp, Ui.Seen("lkey" + k), 0f, 0f, 1.15f, 0.3f, back: true, alpha: 1f);
					}
				}
				Ui.HotkeyLeftToRight(c, W / 2f + 80f, 152f, "TAB", "변경", 15f);
				LadderOnePick(c, W);
				string[] rules = new string[4] { "곡 후보 5개 중 서로 1곡씩 밴, 남은 곡으로 라운드", "점수가 높으면 1점 · 2판 2선승 (같으면 둘 다 1점)", "동점이면 3라운드부터 하드 판정 타이브레이커", "이기면 +18~22점, 지면 −18~22점 · 도중에 나가면 패배" };
				for (int i = 0; i < rules.Length; i++)
				{
					RectTransform rr = Ui.Node(c, "rule" + i, new Vector2(40f, 380f + i * 52f), new Vector2(W - 80f, 44f));
					Image nb = Ui.Img(rr, "n", new Vector2(0f, 6f), new Vector2(32f, 32f), new Color(1f, 1f, 1f, 0.1f));
					nb.sprite = Ui.Disc();
					Ui.VTxt(nb.transform, "t", Vector2.zero, new Vector2(32f, 32f), (i + 1).ToString(), 15f, Ui.Gold, TextAlignmentOptions.Midline);
					Ui.Txt(rr, "t", new Vector2(48f, 0f), new Vector2(W - 130f, 44f), rules[i], 17f, new Color(1f, 1f, 1f, 0.82f), extra: false, TextAlignmentOptions.MidlineLeft);
					Ui.In(rr, Ui.At(0.35f + i * 0.05f), 24f, 0f);
				}
				// MATCH START: 뒤에서 은은하게 빛나는 큰 버튼
				RectTransform gl = Ui.Node(c, "btnGlow", new Vector2(24f, 676f), new Vector2(W - 48f, 132f));
				Ui.Pulse(Ui.FillImg(gl, "bg", new Color(Yellow.r, Yellow.g, Yellow.b, 0.35f)), 0.05f, 0.4f, 1.6f);
				RectTransform b = Ui.Node(c, "btn", new Vector2(36f, 688f), new Vector2(W - 72f, 108f));
				Ui.FillImg(b, "bg", Yellow);
				Ui.Shine(b, 0.6f, 2.2f);
				TextMeshProUGUI bt = Ui.VTxt(b, "t", new Vector2(0f, 10f), new Vector2(W - 72f, 64f), "MATCH START", 54f, new Color(0.08f, 0.08f, 0.1f), TextAlignmentOptions.Midline);
				bt.fontStyle = FontStyles.Italic;
				Ui.Txt(b, "s", new Vector2(0f, 74f), new Vector2(W - 72f, 22f), "ENTER", 14f, new Color(0f, 0f, 0f, 0.6f), extra: false, TextAlignmentOptions.Midline);
				Ui.In(gl, Ui.At(0.5f), 0f, 30f);
				Ui.In(b, Ui.At(0.5f), 0f, 30f);
				Hotkeys("ENTER", "매칭 시작", "TAB", "4B/6B", "F2", "ONE PICK", "F6", "ONE PICK 지우기", "F11", "기록", "ESC", "로비로");
			}
			else if (ladState == 1)
			{
				// 레이더처럼 퍼지는 고리 + 도는 호 + 경과 시간
				Vector2 rc = new Vector2(W / 2f, 420f);
				for (int k = 0; k < 3; k++)
				{
					Image ring = Ui.Img(c, "wave" + k, rc, new Vector2(220f, 220f), Yellow, null, Image.Type.Simple, Ui.TL, Ui.C);
					ring.rectTransform.anchoredPosition = new Vector2(rc.x, -rc.y);
					ring.sprite = Ui.Ring();
					float off = k / 3f;
					Ui.Every(ring, delegate(float t)
					{
						float ph = Mathf.Repeat(t / 2.1f + off, 1f);
						ring.rectTransform.localScale = Vector3.one * (0.75f + ph * 1.5f);
						ring.color = new Color(Yellow.r, Yellow.g, Yellow.b, 0.45f * (1f - ph));
						return false;
					}, 0f);
				}
				Image core = Ui.Img(c, "core", rc, new Vector2(200f, 200f), new Color(0.05f, 0.05f, 0.08f, 0.95f), null, Image.Type.Simple, Ui.TL, Ui.C);
				core.rectTransform.anchoredPosition = new Vector2(rc.x, -rc.y);
				core.sprite = Ui.Disc();
				Image arc = Ui.Img(c, "arc", rc, new Vector2(200f, 200f), Yellow, null, Image.Type.Simple, Ui.TL, Ui.C);
				arc.rectTransform.anchoredPosition = new Vector2(rc.x, -rc.y);
				arc.sprite = Ui.Ring();
				arc.type = Image.Type.Filled;
				arc.fillMethod = Image.FillMethod.Radial360;
				arc.fillOrigin = 2;
				Ui.Every(arc, delegate(float t)
				{
					arc.rectTransform.localEulerAngles = new Vector3(0f, 0f, -t * 260f);
					arc.fillAmount = 0.18f + 0.12f * Mathf.Sin(t * 3f);
					return false;
				}, 0f);
				int secs = (int)(Time.unscaledTime - ladAt);
				Ui.VTxt(c, "time", new Vector2(0f, rc.y - 28f), new Vector2(W, 56f), "00:" + secs.ToString("00"), 44f, Color.white, TextAlignmentOptions.Midline);
				Ui.Pulse(Ui.Txt(c, "find", new Vector2(0f, 600f), new Vector2(W, 40f), "상대를 찾는 중", 26f, Color.white, extra: true, TextAlignmentOptions.Top), 0.45f, 1f, 1.4f);
				Ui.Txt(c, "find2", new Vector2(0f, 640f), new Vector2(W, 26f), TierText(r) + " 근처의 상대를 찾고 있어요", 16f, new Color(1f, 1f, 1f, 0.55f), extra: false, TextAlignmentOptions.Top);
				RectTransform b = Ui.Node(c, "btn", new Vector2(36f, 700f), new Vector2(W - 72f, 96f));
				Ui.FillImg(b, "bg", new Color(1f, 1f, 1f, 0.08f));
				Ui.VTxt(b, "t", new Vector2(0f, 8f), new Vector2(W - 72f, 56f), "CANCEL", 40f, Color.white, TextAlignmentOptions.Midline);
				Ui.Txt(b, "s", new Vector2(0f, 64f), new Vector2(W - 72f, 22f), "ESC", 14f, Ui.Light, extra: false, TextAlignmentOptions.Midline);
				Hotkeys("ESC", "매칭 취소");
			}
			else if (ladFoe != null)
			{
				Ui.In(Ui.VTxt(c, "found", new Vector2(0f, 170f), new Vector2(W, 64f), "MATCH FOUND", 52f, Ui.Gold, TextAlignmentOptions.Top), Ui.At(0f), 0f, 0f, 1.8f, 0.45f, back: true);
				// 나 vs 상대
				for (int side = 0; side < 2; side++)
				{
					float cx = (side == 0) ? 180f : (W - 180f);
					int rt = (side == 0) ? r : aiRating;
					string nm = (side == 0) ? myName : ladFoe.name;
					RectTransform pc = Ui.Node(c, "fp" + side, new Vector2(cx - 150f, 270f), new Vector2(300f, 340f));
					Ui.FillImg(pc, "bg", new Color(1f, 1f, 1f, 0.05f));
					TopGlow(pc, TierColor(rt), 300f, 200f, 0.3f);
					TierEmblem(pc, new Vector2(150f, 100f), 130f, rt, label: false);
					Ui.Txt(pc, "n", new Vector2(10f, 190f), new Vector2(280f, 34f), nm, 24f, Color.white, extra: true, TextAlignmentOptions.Top).overflowMode = TextOverflowModes.Ellipsis;
					Ui.VTxt(pc, "tier", new Vector2(0f, 234f), new Vector2(300f, 28f), TierText(rt), 22f, TierColor(rt), TextAlignmentOptions.Top);
					Ui.VTxt(pc, "pt", new Vector2(0f, 268f), new Vector2(300f, 24f), rt + " PT", 17f, new Color(1f, 1f, 1f, 0.7f), TextAlignmentOptions.Top);
					Chip(pc, "who", new Vector2(110f, 302f), new Vector2(80f, 24f), (side == 0) ? "YOU" : "AI", (side == 0) ? Coral : new Color(1f, 1f, 1f, 0.12f), Color.white, 12f);
					Ui.In(pc, Ui.At(0.2f), (side == 0) ? -120f : 120f, 0f, 1f, 0.55f);
				}
				RectTransform vs = Ui.Node(c, "vs", new Vector2(W / 2f - 46f, 394f), new Vector2(92f, 92f));
				Image vd = Ui.FillImg(vs, "d", Color.white);
				vd.sprite = Ui.Disc();
				Ui.VTxt(vs, "t", Vector2.zero, new Vector2(92f, 92f), "VS", 36f, new Color(0.08f, 0.08f, 0.1f), TextAlignmentOptions.Midline);
				Ui.In(vs, Ui.At(0.45f), 0f, 0f, 2.6f, 0.35f);
				Ui.Pulse(Ui.Txt(c, "ai", new Vector2(0f, 660f), new Vector2(W, 30f), "곧 밴픽을 시작해요", 20f, Color.white, extra: true, TextAlignmentOptions.Top), 0.45f, 1f, 1.2f);
			}
		}

		// ONE PICK 카드
		private void LadderOnePick(RectTransform c, float W)
		{
			string op = OnePick;
			RectTransform opr = Ui.Node(c, "onepick", new Vector2(36f, 216f), new Vector2(W - 72f, 128f));
			Ui.FillImg(opr, "bg", new Color(1f, 1f, 1f, 0.05f));
			Ui.In(opr, Ui.At(0.3f), 0f, 20f);
			Ui.VTxt(opr, "h", new Vector2(132f, 16f), new Vector2(200f, 20f), "ONE PICK", 15f, Ui.Gold, TextAlignmentOptions.TopLeft);
			if (op.Length > 0)
			{
				BaseMusicBundle ob = CandBundle(op, out MODE om, out KEY ok);
				RectTransform jk = Ui.Node(opr, "jk", new Vector2(14f, 14f), new Vector2(100f, 100f));
				Ui.FillImg(jk, "bg", Glass);
				Ui.Cover(jk, "art", Vector2.zero, new Vector2(100f, 100f), ob?.Album);
				Ui.Txt(opr, "t", new Vector2(132f, 40f), new Vector2(W - 72f - 150f, 34f), ob?.title ?? "?", 24f, Color.white, extra: true, TextAlignmentOptions.TopLeft).overflowMode = TextOverflowModes.Ellipsis;
				Chip(opr, "d", new Vector2(132f, 82f), new Vector2(130f, 30f), ModeEng((int)om) + " " + Mathf.FloorToInt(LevelOf(ob, ok, (int)om)), ModeColor((int)om), new Color(0.08f, 0.08f, 0.1f), 15f);
				Ui.Txt(opr, "k", new Vector2(-18f, 88f), new Vector2(260f, 22f), "<color=#FFD54A>F2</color> 바꾸기   <color=#FFD54A>F6</color> 지우기", 14f, Ui.Light, extra: false, TextAlignmentOptions.TopRight, Ui.TR, Ui.TR);
			}
			else
			{
				RectTransform plus = Ui.Node(opr, "plus", new Vector2(14f, 14f), new Vector2(100f, 100f));
				Ui.FillImg(plus, "bg", new Color(1f, 1f, 1f, 0.06f));
				Ui.Pulse(Ui.VTxt(plus, "t", Vector2.zero, new Vector2(100f, 100f), "+", 54f, Color.white, TextAlignmentOptions.Midline), 0.25f, 0.7f, 1.6f);
				Ui.Txt(opr, "t", new Vector2(132f, 44f), new Vector2(W - 72f - 150f, 30f), "자신 있는 곡을 정하면 곡 후보에 나올 수 있어요", 17f, new Color(1f, 1f, 1f, 0.8f), extra: false, TextAlignmentOptions.TopLeft);
				Ui.Txt(opr, "k", new Vector2(132f, 80f), new Vector2(300f, 24f), "<color=#FFD54A>F2</color> 로 정하기", 15f, Ui.Light, extra: false, TextAlignmentOptions.TopLeft);
			}
		}

		// 오른쪽: 티어표
		private void LadderTiers(int r)
		{
			const float W = 520f;
			RectTransform t = Ui.Node(body, "tiers", new Vector2(1346f, 130f), new Vector2(W, 840f));
			Ui.FillImg(t, "bg", Glass);
			Ui.In(t, Ui.At(0.08f), 70f, 0f, 1f, 0.6f);
			Ui.VTxt(t, "h", new Vector2(24f, 20f), new Vector2(300f, 30f), "TIERS", 22f, Ui.Gold, TextAlignmentOptions.TopLeft);
			Ui.Txt(t, "hs", new Vector2(-24f, 24f), new Vector2(300f, 24f), "점수 구간 · 이 정도 실력", 14f, new Color(1f, 1f, 1f, 0.5f), extra: false, TextAlignmentOptions.TopRight, Ui.TR, Ui.TR);
			int myG = TierGroup(r);
			for (int g = 6; g >= 0; g--)
			{
				int i = 6 - g;
				bool on = g == myG;
				Color c = TierColors[g];
				RectTransform row = Ui.Node(t, "t" + g, new Vector2(16f, 64f + i * 110f), new Vector2(W - 32f, 102f));
				Ui.FillImg(row, "bg", on ? new Color(c.r * 0.5f, c.g * 0.5f, c.b * 0.5f, 0.55f) : new Color(1f, 1f, 1f, 0.04f));
				Ui.In(row, Ui.At(0.15f + i * 0.05f), 40f, 0f, 1f, 0.45f);
				TierEmblem(row, new Vector2(54f, 51f), 70f, (g == 6) ? 2000 : (TierStart[g] + 1), label: false);
				Ui.VTxt(row, "n", new Vector2(104f, 16f), new Vector2(300f, 32f), TierGroups[g], 25f, on ? Color.white : c, TextAlignmentOptions.TopLeft);
				Ui.Txt(row, "k", new Vector2(105f, 54f), new Vector2(W - 160f, 26f), "<color=#FFFFFFC0>" + TierKor[g] + "</color>   " + TierSkill[g], 14f, on ? Color.white : Sub, extra: false, TextAlignmentOptions.TopLeft).overflowMode = TextOverflowModes.Ellipsis;
				string range = (g == 6) ? "2000~" : (Mathf.Max(1, TierStart[g]) + "~" + TierStart[g + 1]);
				Ui.VTxt(row, "r", new Vector2(-18f, 18f), new Vector2(160f, 26f), range, 16f, on ? Color.white : Ui.Light, TextAlignmentOptions.TopRight, Ui.TR, Ui.TR);
				if (on)
				{
					Ui.Frame(row, c, 2f);
					Ui.Shine(row, 0.25f, 3f);
					Chip(row, "you", new Vector2(W - 32f - 76f, 60f), new Vector2(58f, 24f), "YOU", Coral, Color.white, 12f);
				}
			}
		}

		private RectTransform spinner;

		private static void MatchButton(RectTransform c, string big, string small, bool lit)
		{
			RectTransform b = Ui.Node(c, "btn", new Vector2(40f, 700f), new Vector2(540f, 100f));
			Ui.FillImg(b, "bg", lit ? Yellow : new Color(1f, 1f, 1f, 0.12f));
			if (lit)
			{
				Ui.Shine(b, 0.7f, 2f);
			}
			TextMeshProUGUI t = Ui.VTxt(b, "t", new Vector2(0f, 8f), new Vector2(540f, 64f), big, 50f, lit ? Color.black : Color.white, TextAlignmentOptions.Midline);
			t.fontStyle = FontStyles.Italic;
			Ui.Txt(b, "s", new Vector2(0f, 70f), new Vector2(540f, 24f), small, 15f, lit ? new Color(0f, 0f, 0f, 0.7f) : Ui.Light, extra: false, TextAlignmentOptions.Midline);
		}
	}
}
