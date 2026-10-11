// 1:1 대전 (디맥 RESPECT V 래더 매치 방식). 오픈 매치 VERSUS(친구끼리)와 래더 매치(AI 상대)가 같이 쓴다.
// 1) START → 점수(티어)에 맞는 레벨의 곡 후보 5개를 무작위로 제시
// 2) 15초 동안 대전자가 1곡씩 제외 (같은 곡이면 1곡 더 무작위 제외) → 남은 3곡을 무작위 순서로
// 3) 라운드마다 곡 공개(준비 시간) 후 시작. 점수가 높으면 1점, 같으면 둘 다 1점
// 4) 2판 2선승: 2라운드 이후 점수가 다르면 끝, 같으면 타이브레이커(하드 판정 = 게임의 PLUS 판정)
// 5) 래더만 승패로 점수 ±18~22 (점수는 각자 PC 에 저장). VERSUS 는 점수 변동 없음
// ponytail: 맥스 판정·승급전·시즌은 없음. 5라운드까지 동점이면 정확도 합으로 가림
using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace WJMAXOnline
{
	public partial class OnlineMod
	{
		private const int BAT_NONE = 0;

		private const int BAT_BAN = 1;

		private const int BAT_REVEAL = 2;

		private const int BAT_PLAY = 3;

		private const int BAT_END = 4;

		private const float BAN_TIME = 15f;

		private const float REVEAL_TIME = 15f; // 디맥: 라운드마다 정비 시간 15초 (곡 추첨 연출 포함)

		private const int MAX_ROUNDS = 5;

		private class BattleState
		{
			public int phase;

			public float until;

			public int no; // 대전 번호 (LP 를 한 번만 반영하려고)

			public int kind = MT_VERSUS;

			public int round;

			public int tier;

			public bool hard; // VERSUS: 모든 라운드 하드 판정

			public readonly List<string> cands = new List<string>(); // "파일명:난이도:키"

			public readonly int[] ban = new int[2] { -1, -1 };

			public readonly List<int> order = new List<int>(); // 라운드별 후보 번호

			public readonly int[] pts = new int[2];

			public readonly int[] ids = new int[2] { -1, -1 };

			public readonly List<string> hist = new List<string>(); // 라운드별 "점수0:점수1"

			public int winner = -2; // -1 무승부

			public bool scored;
		}

		private BattleState bat = new BattleState();

		private int batApplied = -1;

		private int batSeen = -1;

		private int batDelta;

		private int batBefore; // 래더: 판 시작 전 점수

		private int banSel;

		// VERSUS 곡 레벨 범위 (방장이 L/R SHIFT 로 최소/최대 고르고 ←→ 로 조절). 처음엔 내 게임의 랜덤 선곡 레벨 범위
		private int vsMin = -1, vsMax = -1;

		private bool vsEditMax;

		private const int LV_MAX = 30;

		private void InitVsRange()
		{
			if (vsMin > 0)
			{
				return;
			}
			try
			{
				RandomLevelRange r = BundlePackage.closet.RandomLevel;
				vsMin = Mathf.Clamp(Mathf.FloorToInt(r.Min), 1, LV_MAX);
				vsMax = Mathf.Clamp(Mathf.FloorToInt(r.Max), vsMin, LV_MAX);
			}
			catch (Exception)
			{
				LevelRange r = RangeFor(MyRating);
				vsMin = Mathf.Clamp(Mathf.FloorToInt(r.Min), 1, LV_MAX);
				vsMax = Mathf.Clamp(Mathf.FloorToInt(r.Max), vsMin, LV_MAX);
			}
		}

		// 레벨 n 은 n.0 ~ n.99 (게임은 레벨을 내림해서 보여 줌)
		private LevelRange VersusRange
		{
			get
			{
				InitVsRange();
				return new LevelRange(vsMin, vsMax + 0.99f);
			}
		}

		// 지금 버튼에서 범위 안 곡 수 (방 화면 표시용, 범위·버튼이 바뀔 때만 다시 셈)
		private string vsCountKey;

		private int vsCount;

		private int VersusSongCount()
		{
			LevelRange r = VersusRange;
			string k = vsMin + ":" + vsMax + ":" + GameValue.key;
			if (k != vsCountKey)
			{
				vsCountKey = k;
				vsCount = BundlePackage.musicBundle.GetMusicBundles(FREESTYLE_CATEGORY.ALL, GameValue.key).Where((BaseMusicBundle b) => !b.IsRandom && !(b is SoloMusicBundle) && b.IsWithinLevel(GameValue.key, r)).Select((BaseMusicBundle b) => b.RootFileName).Distinct().Count();
			}
			return vsCount;
		}

		private static readonly int[] GroupMid = new int[7] { 150, 450, 750, 1050, 1350, 1750, 2200 };


		private bool versusHard;

		private int MySlot => Array.IndexOf(bat.ids, myId);

		private bool HardRound(int round)
		{
			return round >= 3 || (bat.kind == MT_VERSUS && bat.hard);
		}

		// ---------------- 래더 점수 / 티어 ----------------
		// 1~300 IRON, 300~600 BRONZE, 600~900 SILVER, 900~1200 GOLD (100점마다 III → II → I)
		// 1200~1500 DIAMOND (60점마다 V → I), 1500~2000 MASTER (100점마다 V → I), 2000~ GRAND MASTER (상한 없음)
		private static readonly string[] TierGroups = new string[7] { "IRON", "BRONZE", "SILVER", "GOLD", "DIAMOND", "MASTER", "GRAND MASTER" };

		private static readonly string[] TierKor = new string[7] { "아이언", "브론즈", "실버", "골드", "다이아몬드", "마스터", "그랜드마스터" };


		private static readonly int[] TierStart = new int[8] { 0, 300, 600, 900, 1200, 1500, 2000, 99999 };

		private static readonly Color[] TierColors = new Color[7]
		{
			new Color32(150, 150, 160, byte.MaxValue),
			new Color32(205, 127, 70, byte.MaxValue),
			new Color32(200, 210, 225, byte.MaxValue),
			new Color32(byte.MaxValue, 205, 70, byte.MaxValue),
			new Color32(110, 200, byte.MaxValue, byte.MaxValue),
			new Color32(220, 110, byte.MaxValue, byte.MaxValue),
			new Color32(byte.MaxValue, 80, 90, byte.MaxValue)
		};

		private const int RATING_MAX = 9999;

		private static int TierGroup(int r)
		{
			for (int g = 6; g > 0; g--)
			{
				if (r >= TierStart[g])
				{
					return g;
				}
			}
			return 0;
		}

		// 지금 단계(디비전)의 시작·끝 점수와 번호 (번호가 작을수록 높음: III → I, V → I)
		private static void TierDiv(int r, out int lo, out int hi, out int div)
		{
			int g = TierGroup(r);
			if (g == 6)
			{
				lo = 2000;
				hi = 2000;
				div = 0;
				return;
			}
			int w = (g == 4) ? 60 : 100;
			int n = (TierStart[g + 1] - TierStart[g]) / w;
			int idx = Mathf.Clamp((r - TierStart[g]) / w, 0, n - 1);
			lo = TierStart[g] + idx * w;
			hi = lo + w;
			div = n - idx;
		}

		private static string Roman(int n)
		{
			return n switch
			{
				1 => "I",
				2 => "II",
				3 => "III",
				4 => "IV",
				5 => "V",
				_ => "",
			};
		}

		private static string TierText(int r)
		{
			TierDiv(r, out _, out _, out int div);
			int g = TierGroup(r);
			return (g == 6) ? TierGroups[6] : (TierGroups[g] + " " + Roman(div));
		}

		private static Color TierColor(int r)
		{
			return TierColors[TierGroup(r)];
		}

		// 지금 단계 안에서 얼마나 왔는지 (0~1)
		private static float TierProgress(int r)
		{
			TierDiv(r, out int lo, out int hi, out _);
			return (hi <= lo) ? 1f : Mathf.Clamp01((float)(r - lo) / (hi - lo));
		}

		// 점수 → 실력: 이 레벨 곡을 정확도 94~95% 쯤으로 치는 수준 (게임 레벨 숫자, 거짓말 4렙 = 24)
		// 기준: 150점(아이언) = 5렙을 82% 쯤으로 겨우 클리어, 2000점(그랜드마스터) = 24렙(거짓말 4) 풀콤 확률 80%
		private static float SkillOf(int r)
		{
			return 3f + (r - 150) * 0.0119f;
		}

		// 대전 곡 후보 레벨: 실력보다 조금 쉬운 곡 ~ 살짝 어려운 곡
		private static LevelRange RangeFor(int r)
		{
			float k = SkillOf(r);
			return new LevelRange(Mathf.Max(1f, k - 2.5f), Mathf.Max(2f, k + 0.5f));
		}

		// 티어 엠블럼: 육각형(티어 색) + 안쪽 어두운 육각형 + 단계 숫자, 아래에 티어 이름
		private static RectTransform TierEmblem(Transform parent, Vector2 center, float size, int r, bool label = true)
		{
			Color c = TierColor(r);
			int g = TierGroup(r);
			RectTransform e = Ui.Node(parent, "emblem", center - new Vector2(size / 2f, size / 2f), new Vector2(size, size));
			Image glow = Ui.Img(e, "glow", new Vector2(-size * 0.12f, -size * 0.12f), new Vector2(size * 1.24f, size * 1.24f), new Color(c.r, c.g, c.b, 0.18f));
			glow.sprite = Ui.Hex();
			Ui.Pulse(glow, 0.08f, 0.32f, 2.2f);
			Image outer = Ui.FillImg(e, "outer", c);
			outer.sprite = Ui.Hex();
			Image inner = Ui.Img(e, "inner", new Vector2(size * 0.1f, size * 0.1f), new Vector2(size * 0.8f, size * 0.8f), new Color(0.07f, 0.06f, 0.1f, 1f));
			inner.sprite = Ui.Hex();
			Image shine = Ui.Img(e, "shine", new Vector2(size * 0.1f, size * 0.1f), new Vector2(size * 0.8f, size * 0.8f), new Color(c.r, c.g, c.b, 0.35f), "Music_Choice_Effect");
			shine.sprite = Ui.HexRing();
			Image ring = Ui.Img(e, "ring", new Vector2(size * 0.18f, size * 0.18f), new Vector2(size * 0.64f, size * 0.64f), new Color(c.r, c.g, c.b, 0.6f));
			ring.sprite = Ui.HexRing();
			TierDiv(r, out _, out _, out int div);
			string mark = (g == 6) ? "GM" : Roman(div);
			Ui.VTxt(e, "m", Vector2.zero, new Vector2(size, size), mark, size * ((mark.Length > 3) ? 0.2f : 0.26f), Color.white, TextAlignmentOptions.Midline);
			if (label)
			{
				Ui.VTxt(e, "n", new Vector2(-size, size * 1.02f), new Vector2(size * 3f, size * 0.3f), TierText(r), size * 0.17f, c, TextAlignmentOptions.Top);
			}
			return e;
		}

		private static int MyRating
		{
			get
			{
				return Mathf.Clamp(PlayerPrefs.GetInt("WJONLINE_RATING", 100), 1, RATING_MAX);
			}
			set
			{
				PlayerPrefs.SetInt("WJONLINE_RATING", Mathf.Clamp(value, 1, RATING_MAX));
			}
		}

		private static int Wins
		{
			get
			{
				return PlayerPrefs.GetInt("WJONLINE_WIN", 0);
			}
			set
			{
				PlayerPrefs.SetInt("WJONLINE_WIN", value);
			}
		}

		private static int Losses
		{
			get
			{
				return PlayerPrefs.GetInt("WJONLINE_LOSE", 0);
			}
			set
			{
				PlayerPrefs.SetInt("WJONLINE_LOSE", value);
			}
		}

		// 한 판 점수 변동: 18~22점. 나보다 센 상대를 이기면 많이(22), 약한 상대에게 지면 많이(-22) (Elo 기대 승률 비례)
		private static int RatingDelta(bool win, int me, int opp)
		{
			float e = 1f / (1f + Mathf.Pow(10f, (opp - me) / 400f));
			return win ? Mathf.RoundToInt(18f + 4f * (1f - e)) : (-Mathf.RoundToInt(18f + 4f * e));
		}

		private static int ApplyRating(int delta)
		{
			MyRating += delta;
			if (delta > 0)
			{
				Wins++;
			}
			else if (delta < 0)
			{
				Losses++;
			}
			PlayerPrefs.Save();
			return delta;
		}

		// ---------------- 방장: 진행 ----------------
		private void BattleBegin()
		{
			List<Player> two = aiMatch ? players.ToList() : players.Where((Player p) => p.fighter).ToList();
			if (two.Count != 2)
			{
				return;
			}
			int tier = MyRating;
			KEY key = GameValue.key;
			List<string> cands = PickCandidates(aiMatch ? RangeFor(tier) : VersusRange, key, 5, new HashSet<string>());
			// 래더 ONE PICK: 절반 확률로 후보에 들어감
			string op = aiMatch ? OnePick : "";
			if (op.Length > 0 && cands.Count == 5 && UnityEngine.Random.value < 0.5f && !cands.Any((string x) => x.Split(':')[0] == op.Split(':')[0]))
			{
				cands[UnityEngine.Random.Range(0, 5)] = op;
			}
			if (cands.Count < 5)
			{
				Status("곡이 부족해서 대전을 시작할 수 없어요", Ui.Red);
				PlaySe(AudioManager.SE.error);
				return;
			}
			int no = bat.no + 1;
			batApplied = -1; // 새 대전: 점수 반영 표시 초기화 (방을 옮기면 번호가 다시 1부터라서)
			bat = new BattleState
			{
				no = no,
				kind = matchType,
				tier = tier,
				hard = versusHard,
				phase = BAT_BAN,
				until = Time.unscaledTime + BAN_TIME
			};
			bat.cands.AddRange(cands);
			bat.ids[0] = two[0].id;
			bat.ids[1] = two[1].id;
			aiBanAt = Time.unscaledTime + UnityEngine.Random.Range(3f, 9f);
			foreach (Player p in players)
			{
				p.ready = false;
			}
			PlaySe(AudioManager.SE.Confirm);
			BroadcastRoster();
			BroadcastBattle();
		}

		// 티어 레벨 범위의 곡에서 무작위 (모자라면 범위를 넓힘)
		// 티어 범위 채보 목록 "파일명:난이도:키". 범위 곡이 min 개보다 적으면 1렙씩 넓힌다
		private static List<string> TierPool(LevelRange range, KEY key, int min, HashSet<string> exclude = null)
		{
			BaseMusicBundle[] all = BundlePackage.musicBundle.GetMusicBundles(FREESTYLE_CATEGORY.ALL, key).Where((BaseMusicBundle b) => !b.IsRandom && !(b is SoloMusicBundle) && (exclude == null || !exclude.Contains(b.RootFileName)) && b.RootFileName.IndexOfAny(new char[3] { ':', ';', '|' }) < 0).ToArray();
			List<BaseMusicBundle> pool = new List<BaseMusicBundle>();
			for (int widen = 0; widen < 8 && pool.Count < min; widen++)
			{
				LevelRange r = new LevelRange(Mathf.Max(1f, range.Min - widen), range.Max + widen);
				pool = all.Where((BaseMusicBundle b) => b.IsWithinLevel(key, r)).ToList();
				if (pool.Count >= min || widen == 7)
				{
					range = r;
				}
			}
			if (pool.Count == 0)
			{
				pool = all.ToList();
			}
			System.Random rnd = new System.Random(Guid.NewGuid().GetHashCode());
			List<string> res = new List<string>();
			foreach (BaseMusicBundle b in pool)
			{
				SheetPack[] packs = b.GetSheetPacksWithinLevel(key, range).Where((SheetPack x) => x.mode != MODE.Lie || range.Max >= 21f).ToArray();
				MODE mode = (packs.Length > 0) ? packs[rnd.Next(packs.Length)].mode : b.ChangeMode(key, MODE.Wakgood);
				res.Add(b.RootFileName + ":" + (int)mode + ":" + (int)key);
			}
			return res;
		}

		// 최근에 나온 곡은 한동안 빼서 같은 곡만 계속 나오지 않게
		private const string RECENT_KEY = "WJONLINE_RECENT";

		private static List<string> RecentRoots => PlayerPrefs.GetString(RECENT_KEY, "").Split('|').Where((string x) => x.Length > 0).ToList();

		private static void AddRecent(IEnumerable<string> cands)
		{
			List<string> r = cands.Select((string c) => c.Split(':')[0]).Concat(RecentRoots).Distinct().Take(25).ToList();
			PlayerPrefs.SetString(RECENT_KEY, string.Join("|", r));
		}

		private static List<string> PickCandidates(LevelRange range, KEY key, int count, HashSet<string> exclude)
		{
			List<string> recent = RecentRoots;
			List<string> pool = TierPool(range, key, count + 12, exclude);
			// 범위 곡이 충분하면 최근 곡을 빼고, 모자라면 오래된 것부터 다시 허용
			for (int keep = recent.Count; keep >= 0; keep -= 5)
			{
				HashSet<string> rx = new HashSet<string>(recent.Take(Mathf.Max(0, keep)));
				List<string> p = pool.Where((string c) => !rx.Contains(c.Split(':')[0])).ToList();
				if (p.Count >= count)
				{
					pool = p;
					break;
				}
			}
			System.Random rnd = new System.Random(Guid.NewGuid().GetHashCode());
			List<string> res = pool.OrderBy((string _) => rnd.Next()).Take(count).ToList();
			AddRecent(res);
			return res;
		}

		private void BattleBan(int id, int idx)
		{
			int slot = Array.IndexOf(bat.ids, id);
			if (bat.phase == BAT_BAN && slot >= 0 && bat.ban[slot] < 0 && idx >= 0 && idx < bat.cands.Count)
			{
				bat.ban[slot] = idx;
				BroadcastBattle();
			}
		}

		private void BattleTick()
		{
			if (!isHost || !connected || bat.phase == BAT_NONE)
			{
				return;
			}
			float now = Time.unscaledTime;
			if (bat.phase != BAT_END && bat.ids.Any((int id) => Find(id) == null))
			{
				// 대전자가 나감: 남은 사람 승리 (나간 사람은 자기 PC 에서 패배 처리)
				int stay = bat.ids.FirstOrDefault((int id) => Find(id) != null);
				bat.winner = (Find(stay) != null) ? stay : (-1);
				bat.hist.Add("기권");
				EndBattle();
				return;
			}
			switch (bat.phase)
			{
			case BAT_BAN:
				if (aiMatch && bat.ban[1] < 0 && now >= aiBanAt)
				{
					bat.ban[1] = AiBan();
					BroadcastBattle();
				}
				if ((bat.ban[0] >= 0 && bat.ban[1] >= 0) || now >= bat.until)
				{
					ResolveBans();
				}
				break;
			case BAT_REVEAL:
			{
				// 준비 시간이 지나고 두 대전자 모두 방에 있으면 시작
				List<Player> f = bat.ids.Select(Find).ToList();
				if (now >= bat.until && f.All((Player p) => p != null && p.inRoom && !p.offline) && !matchLive && ListerReady)
				{
					string[] c = bat.cands[bat.order[bat.round - 1]].Split(':');
					bat.phase = BAT_PLAY;
					bat.scored = false;
					BroadcastBattle();
					BroadcastPlay(c[0], (KEY)Int(c[2], 0, (int)KEY.Count - 1), forced: true, (MODE)Int(c[1], 0, (int)MODE.COUNT - 1), HardRound(bat.round) ? 1 : 0, bat.ids.ToList());
				}
				break;
			}
			case BAT_PLAY:
			{
				List<Player> f2 = bat.ids.Select(Find).ToList();
				if (!bat.scored && f2.All((Player p) => p != null && (p.offline || (p.done && p.inRoom))))
				{
					ScoreRound();
				}
				break;
			}
			case BAT_END:
				if (now >= bat.until)
				{
					bat.phase = BAT_NONE;
					BroadcastBattle();
				}
				break;
			}
		}

		private void ResolveBans()
		{
			System.Random rnd = new System.Random();
			for (int i = 0; i < 2; i++)
			{
				if (bat.ban[i] < 0)
				{
					bat.ban[i] = rnd.Next(bat.cands.Count);
				}
			}
			HashSet<int> removed = new HashSet<int> { bat.ban[0], bat.ban[1] };
			if (removed.Count == 1)
			{
				List<int> rest = Enumerable.Range(0, bat.cands.Count).Where((int i) => !removed.Contains(i)).ToList();
				removed.Add(rest[rnd.Next(rest.Count)]);
			}
			bat.order.Clear();
			bat.order.AddRange(from i in Enumerable.Range(0, bat.cands.Count)
				where !removed.Contains(i)
				orderby rnd.Next()
				select i);
			bat.round = 1;
			bat.phase = BAT_REVEAL;
			bat.until = Time.unscaledTime + REVEAL_TIME;
			BroadcastBattle();
		}

		private void ScoreRound()
		{
			bat.scored = true;
			Player a = Find(bat.ids[0]);
			Player b = Find(bat.ids[1]);
			int sa = (a != null && !a.offline) ? a.score : 0;
			int sb = (b != null && !b.offline) ? b.score : 0;
			if (sa >= sb)
			{
				bat.pts[0]++;
			}
			if (sb >= sa)
			{
				bat.pts[1]++;
			}
			bat.hist.Add(sa + ":" + sb + ":" + (a?.rate ?? 0f).ToString("0.00", IC) + ":" + (b?.rate ?? 0f).ToString("0.00", IC));
			bool decided = bat.round >= 2 && bat.pts[0] != bat.pts[1];
			if (decided || bat.round >= MAX_ROUNDS)
			{
				if (bat.pts[0] != bat.pts[1])
				{
					bat.winner = (bat.pts[0] > bat.pts[1]) ? bat.ids[0] : bat.ids[1];
				}
				else
				{
					// 끝까지 동점: 정확도 합
					float ra = bat.hist.Sum((string h) => Num(h.Split(':')[2], 0f, 100f));
					float rb = bat.hist.Sum((string h) => Num(h.Split(':')[3], 0f, 100f));
					bat.winner = (ra > rb) ? bat.ids[0] : ((rb > ra) ? bat.ids[1] : (-1));
				}
				EndBattle();
				return;
			}
			bat.round++;
			if (bat.round > bat.order.Count)
			{
				// 타이브레이커 곡이 모자라면 새 곡을 무작위로 추가
				HashSet<string> used = new HashSet<string>(bat.cands.Select((string c) => c.Split(':')[0]));
				List<string> more = PickCandidates((bat.kind == MT_VERSUS) ? VersusRange : RangeFor(bat.tier), GameValue.key, 1, used);
				if (more.Count == 0)
				{
					more.Add(bat.cands[bat.order[0]]);
				}
				bat.cands.Add(more[0]);
				bat.order.Add(bat.cands.Count - 1);
			}
			bat.phase = BAT_REVEAL;
			bat.until = Time.unscaledTime + REVEAL_TIME;
			BroadcastBattle();
		}

		private void EndBattle()
		{
			bat.phase = BAT_END;
			bat.until = Time.unscaledTime + 30f;
			BroadcastBattle();
		}

		private void BroadcastBattle()
		{
			string line = BattleLine();
			ApplyBattle(line.Split('|'));
			net?.Broadcast(line);
		}

		private string BattleLine()
		{
			return "LAD|" + bat.phase + "|" + Mathf.CeilToInt(Mathf.Max(0f, bat.until - Time.unscaledTime)) + "|" + bat.no + "|" + bat.round + "|" + bat.pts[0] + "|" + bat.pts[1] + "|" + bat.ban[0] + "|" + bat.ban[1] + "|" + string.Join(",", bat.order) + "|" + bat.ids[0] + "|" + bat.ids[1] + "|" + bat.winner + "|" + string.Join(";", bat.hist) + "|" + string.Join(";", bat.cands) + "|" + (bat.hard ? 1 : 0) + "|" + bat.tier + "|" + bat.kind;
		}

		// 방장·손님 공통: 대전 상태 반영
		private void ApplyBattle(string[] p)
		{
			if (p.Length < 18)
			{
				return;
			}
			int phase = Int(p[1], 0, BAT_END);
			int no = Int(p[3], 0, int.MaxValue);
			if (no != bat.no || (phase == BAT_BAN && bat.phase != BAT_BAN))
			{
				banSel = 0;
				if (!isHost && phase == BAT_BAN)
				{
					batApplied = -1;
				}
			}
			if (!isHost)
			{
				bat.phase = phase;
				bat.until = Time.unscaledTime + Int(p[2], 0, 60);
				bat.no = no;
				bat.round = Int(p[4], 0, MAX_ROUNDS);
				bat.pts[0] = Int(p[5], 0, 9);
				bat.pts[1] = Int(p[6], 0, 9);
				bat.ban[0] = Int(p[7], -1, 9);
				bat.ban[1] = Int(p[8], -1, 9);
				bat.order.Clear();
				bat.order.AddRange(from x in p[9].Split(',')
					where x.Length > 0
					select Int(x, 0, 9));
				bat.ids[0] = Int(p[10], -1, int.MaxValue);
				bat.ids[1] = Int(p[11], -1, int.MaxValue);
				bat.winner = Int(p[12], -2, int.MaxValue);
				bat.hist.Clear();
				bat.hist.AddRange(from x in p[13].Split(';')
					where x.Length > 0
					select x);
				bat.cands.Clear();
				bat.cands.AddRange(from x in p[14].Split(';')
					where x.Split(':').Length >= 3
					select x);
				bat.hard = p[15] == "1";
				bat.tier = Int(p[16], 0, RATING_MAX);
				bat.kind = Int(p[17], MT_VERSUS, MT_LADDER);
			}
			// 대전 결과: 래더(AI 대전)만 점수 변동
			if (bat.phase == BAT_END && batApplied != bat.no && MySlot >= 0)
			{
				batApplied = bat.no;
				bool win = bat.winner == myId;
				batDelta = 0;
				if (bat.kind == MT_LADDER && bat.winner != -1)
				{
					batBefore = MyRating;
					batDelta = ApplyRating(RatingDelta(win, MyRating, aiRating));
					LogLadder(win);
					Player me = Me;
					if (me != null)
					{
						SetMyProfile(me);
					}
				}
				PlaySe(win ? AudioManager.SE.Confirm : AudioManager.SE.SlideMove);
			}
			BattleScreen();
		}

		// 대전이 진행 중이면 대전 화면, 끝나고 결과를 봤으면 방 화면
		private void BattleScreen()
		{
			if (!online || !connected || inMatch)
			{
				return;
			}
			bool want = bat.phase == BAT_BAN || bat.phase == BAT_REVEAL || bat.phase == BAT_PLAY || (bat.phase == BAT_END && batSeen != bat.no);
			if (want && scr == Scr.Room)
			{
				SetScr(Scr.Battle);
			}
			else if (!want && scr == Scr.Battle)
			{
				SetScr(Scr.Room);
			}
		}

		// 후보 곡: 내 게임의 곡 (없으면 null)
		private static BaseMusicBundle CandBundle(string cand, out MODE mode, out KEY key)
		{
			string[] c = cand.Split(':');
			mode = (MODE)Int(c[1], 0, (int)MODE.COUNT - 1);
			key = (KEY)Int(c[2], 0, (int)KEY.Count - 1);
			return BaseOf(c[0], key);
		}

		private void InputBattle()
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
			if (!aiMatch && Key(KeyCode.F3))
			{
				chatting = true;
				chatText = "";
				PlaySe(AudioManager.SE.SlideMove);
				return;
			}
			int slot = MySlot;
			if (bat.phase == BAT_BAN && slot >= 0 && bat.ban[slot] < 0)
			{
				int n = Mathf.Max(1, bat.cands.Count);
				if (Key(KeyCode.LeftArrow) || Key(KeyCode.UpArrow))
				{
					banSel = (banSel + n - 1) % n;
					PlaySe(AudioManager.SE.SlideMove);
				}
				else if (Key(KeyCode.RightArrow) || Key(KeyCode.DownArrow))
				{
					banSel = (banSel + 1) % n;
					PlaySe(AudioManager.SE.SlideMove);
				}
				else if (Enter())
				{
					PlaySe(AudioManager.SE.Confirm);
					bat.ban[slot] = banSel; // 내 화면에는 바로 표시 (방장이 확정해서 다시 보내줌)
					if (isHost)
					{
						BroadcastBattle();
					}
					else
					{
						net?.server?.Send("BAN|" + myId + "|" + banSel);
					}
				}
			}
			else if (bat.phase == BAT_END && Enter())
			{
				PlaySe(AudioManager.SE.Confirm);
				batSeen = bat.no;
				if (aiMatch)
				{
					EndAiLadder();
				}
				else
				{
					SetScr(Scr.Room);
				}
				return;
			}
			if (Key(KeyCode.Escape))
			{
				PlaySe(AudioManager.SE.Confirm);
				confirmLeave = true;
			}
		}

		// 대전 중에 내가 나가면 패배 처리 (래더)
		private void BattleForfeit()
		{
			if (bat.kind == MT_LADDER && bat.phase != BAT_NONE && bat.phase != BAT_END && MySlot >= 0 && batApplied != bat.no)
			{
				batApplied = bat.no;
				batBefore = MyRating;
				batDelta = ApplyRating(RatingDelta(win: false, MyRating, aiRating));
				LogLadder(win: false);
			}
			bat = new BattleState
			{
				no = bat.no
			};
		}
	}
}
