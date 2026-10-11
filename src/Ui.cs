using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace WJMAXOnline
{
	// 게임 자체 에셋(paybooc 폰트, Music_Choice 바, HotKey 배지 등)으로 uGUI 를 코드로 조립하는 도구.
	// 좌표는 1920x1080 기준, 기본 기준점은 부모의 왼쪽 위 (y 는 아래로 +).
	public static class Ui
	{
		public static TMP_FontAsset Bold;

		public static TMP_FontAsset Extra;

		public static TMP_FontAsset Vitro; // 게임 영문 장식 글꼴 (난이도 버튼 MESSI/WAKGOOD 등)

		private static Dictionary<string, Sprite> sprites;

		public static readonly Color Gray = new Color32(175, 175, 175, byte.MaxValue);

		public static readonly Color Light = new Color32(230, 230, 230, byte.MaxValue);

		public static readonly Color Dim = new Color32(120, 120, 128, byte.MaxValue);

		public static readonly Color Panel = new Color(0f, 0f, 0f, 0.7f);

		public static readonly Color Gold = new Color32(byte.MaxValue, 213, 74, byte.MaxValue);

		public static readonly Color Green = new Color32(88, 222, 166, byte.MaxValue);

		public static readonly Color Red = new Color32(byte.MaxValue, 96, 106, byte.MaxValue);

		public static readonly Vector2 TL = new Vector2(0f, 1f);

		public static readonly Vector2 TR = new Vector2(1f, 1f);

		public static readonly Vector2 BL = new Vector2(0f, 0f);

		public static readonly Vector2 C = new Vector2(0.5f, 0.5f);

		public static readonly Vector2 TC = new Vector2(0.5f, 1f);

		public static void LoadAssets()
		{
			foreach (TMP_FontAsset f in Resources.FindObjectsOfTypeAll<TMP_FontAsset>())
			{
				if (f.name == "paybooc Bold SDF" && Bold == null)
				{
					Bold = f;
				}
				if (f.name == "paybooc Extra SDF" && Extra == null)
				{
					Extra = f;
				}
				if (f.name == "VITRO CORE SDF" && Vitro == null)
				{
					Vitro = f;
				}
			}
			Bold = Bold ?? TMP_Settings.defaultFontAsset;
			AddFallback(Bold);
			AddFallback(Extra);
			Extra = Extra ?? Bold;
			Vitro = Vitro ?? Bold;
			sprites = new Dictionary<string, Sprite>();
			foreach (Sprite s in Resources.FindObjectsOfTypeAll<Sprite>())
			{
				if (!sprites.ContainsKey(s.name))
				{
					sprites[s.name] = s;
				}
			}
		}

		// 게임 폰트 에셋은 게임에서 쓰는 글자만 들어 있어서(정적 아틀라스) 빠진 글자는 동적 폰트로 대체
		private static void AddFallback(TMP_FontAsset f)
		{
			TMP_FontAsset dyn = null;
			foreach (TMP_FontAsset a in Resources.FindObjectsOfTypeAll<TMP_FontAsset>())
			{
				if (a.atlasPopulationMode == AtlasPopulationMode.Dynamic && a.sourceFontFile != null)
				{
					dyn = a;
				}
			}
			if (f != null && dyn != null && f != dyn)
			{
				if (f.fallbackFontAssetTable == null)
				{
					f.fallbackFontAssetTable = new List<TMP_FontAsset>();
				}
				if (!f.fallbackFontAssetTable.Contains(dyn))
				{
					f.fallbackFontAssetTable.Add(dyn);
				}
			}
		}

		private static float rescanAt = -10f;

		// 게임 스프라이트는 곡 선택 화면 등이 로드된 뒤에야 메모리에 올라온다: 못 찾으면 (1초에 한 번) 다시 훑는다
		public static Sprite Sp(string name)
		{
			if (sprites != null && sprites.TryGetValue(name, out var s) && s != null)
			{
				return s;
			}
			if (Time.unscaledTime - rescanAt < 1f)
			{
				return null;
			}
			rescanAt = Time.unscaledTime;
			sprites = sprites ?? new Dictionary<string, Sprite>();
			foreach (Sprite x in Resources.FindObjectsOfTypeAll<Sprite>())
			{
				if (x != null && (!sprites.TryGetValue(x.name, out var old) || old == null))
				{
					sprites[x.name] = x;
				}
			}
			return (sprites.TryGetValue(name, out s) && s != null) ? s : null;
		}

		public static RectTransform Node(Transform parent, string name, Vector2 pos, Vector2 size, Vector2? anchor = null, Vector2? pivot = null)
		{
			GameObject go = new GameObject(name, typeof(RectTransform));
			go.layer = 5;
			RectTransform rt = (RectTransform)go.transform;
			rt.SetParent(parent, worldPositionStays: false);
			Vector2 a = anchor ?? TL;
			rt.anchorMin = a;
			rt.anchorMax = a;
			rt.pivot = pivot ?? a;
			rt.sizeDelta = size;
			rt.anchoredPosition = new Vector2(pos.x, 0f - pos.y);
			return rt;
		}

		public static RectTransform Fill(Transform parent, string name, float inset = 0f)
		{
			RectTransform rt = Node(parent, name, Vector2.zero, Vector2.zero);
			rt.anchorMin = Vector2.zero;
			rt.anchorMax = Vector2.one;
			rt.pivot = C;
			rt.offsetMin = new Vector2(inset, inset);
			rt.offsetMax = new Vector2(0f - inset, 0f - inset);
			return rt;
		}

		public static Image Img(RectTransform rt, Color color, string sprite = null, Image.Type type = Image.Type.Simple)
		{
			Image img = rt.gameObject.AddComponent<Image>();
			img.color = color;
			img.raycastTarget = false;
			if (sprite != null)
			{
				img.sprite = Sp(sprite);
				img.type = type;
				// 스프라이트를 못 찾았을 때 흰 상자로 덮여 글자가 안 보이는 일이 없게 어두운 보라로 대체
				if (img.sprite == null && color == Color.white)
				{
					img.color = new Color32(92, 56, 150, 255);
				}
			}
			return img;
		}

		public static Image Img(Transform parent, string name, Vector2 pos, Vector2 size, Color color, string sprite = null, Image.Type type = Image.Type.Simple, Vector2? anchor = null, Vector2? pivot = null)
		{
			return Img(Node(parent, name, pos, size, anchor, pivot), color, sprite, type);
		}

		public static Image FillImg(Transform parent, string name, Color color, string sprite = null, Image.Type type = Image.Type.Simple, float inset = 0f)
		{
			if ((name == "bg" || name == "sel") && (sprite == null || sprite == "Music_Choice_Effect"))
			{
				RoundClip(parent as RectTransform);
			}
			Image img = Img(Fill(parent, name, inset), color, sprite, type);
			if (name == "sel")
			{
				SelIn(img);
			}
			else if (name == "bg" && parent.GetComponent<Mask>() != null && ((RectTransform)parent).rect.width >= 120f && ((RectTransform)parent).rect.height >= 60f && color.a < 0.97f && color.maxColorComponent < 0.3f)
			{
				// 어두운 유리 패널: 아주 옅은 테두리로 윤곽을 잡아 줌
				RoundFrame((RectTransform)parent, new Color(1f, 1f, 1f, 0.1f), 1f, "edge");
			}
			else if (name == "bg" && sprite == null && color.a > 0.8f)
			{
				// 진한 색 판에는 위쪽이 살짝 밝은 광택 (납작한 색 상자처럼 안 보이게)
				Color.RGBToHSV(color, out _, out float sat, out float val);
				if (sat > 0.25f && val > 0.3f)
				{
					Image gl = Img(Fill(parent, "gloss"), new Color(1f, 1f, 1f, 0.28f));
					gl.sprite = Fade();
					gl.rectTransform.localEulerAngles = new Vector3(0f, 0f, 180f);
					Image sh = Img(Fill(parent, "shade"), new Color(0f, 0f, 0f, 0.18f));
					sh.sprite = Fade();
				}
			}
			return img;
		}

		// ---------------- 둥근 모서리 ----------------
		private static Sprite round;

		private static Sprite roundRing;

		private static Sprite RoundSprite(bool ring)
		{
			const int N = 64;
			const float R = 20f;
			Texture2D t = new Texture2D(N, N, TextureFormat.RGBA32, mipChain: false);
			t.wrapMode = TextureWrapMode.Clamp;
			for (int y = 0; y < N; y++)
			{
				for (int x = 0; x < N; x++)
				{
					// 둥근 사각형까지의 거리 (안쪽 음수)
					float qx = Mathf.Max(Mathf.Abs(x + 0.5f - N / 2f) - (N / 2f - R), 0f);
					float qy = Mathf.Max(Mathf.Abs(y + 0.5f - N / 2f) - (N / 2f - R), 0f);
					float d = Mathf.Sqrt(qx * qx + qy * qy) - R;
					float edge = Mathf.Max(Mathf.Abs(x + 0.5f - N / 2f), Mathf.Abs(y + 0.5f - N / 2f)) - N / 2f;
					d = Mathf.Max(d, edge);
					float a = Mathf.Clamp01(0.5f - d);
					if (ring)
					{
						a *= Mathf.Clamp01(d + 4.5f);
					}
					t.SetPixel(x, y, new Color(1f, 1f, 1f, a));
				}
			}
			t.Apply();
			return Sprite.Create(t, new Rect(0f, 0f, N, N), new Vector2(0.5f, 0.5f), 100f, 0u, SpriteMeshType.FullRect, new Vector4(24f, 24f, 24f, 24f));
		}

		private static float Radius(RectTransform rt)
		{
			Vector2 s = rt.rect.size;
			return Mathf.Clamp(Mathf.Min(s.x, s.y) * 0.14f, 5f, 14f);
		}

		// 패널 모양을 둥글게 자름 (안의 모든 그림·글자가 둥근 모서리 안으로). 얇은 줄·화면 전체·이어 붙인 조각은 제외
		public static void RoundClip(RectTransform p)
		{
			if (p == null || p.GetComponent<Mask>() != null || p.GetComponent<Graphic>() != null)
			{
				return;
			}
			Vector2 s = p.rect.size;
			string n = p.name;
			string pn = (p.parent != null) ? p.parent.name : "";
			if (s.x < 24f || s.y < 24f || s.x >= 1800f || n == "strip" || n == "banner" || n == "head" || n == "keys" || pn == "match" || pn == "team" || pn == "multi")
			{
				return;
			}
			Image m = p.gameObject.AddComponent<Image>();
			m.sprite = round ?? (round = RoundSprite(ring: false));
			m.type = Image.Type.Sliced;
			m.pixelsPerUnitMultiplier = 20f / Radius(p);
			m.raycastTarget = false;
			p.gameObject.AddComponent<Mask>().showMaskGraphic = false;
		}

		// 둥근 테두리 (둥글게 자른 패널용)
		private static void RoundFrame(RectTransform p, Color c, float t, string name = "frame")
		{
			Image f = Img(Fill(p, name), c);
			f.sprite = roundRing ?? (roundRing = RoundSprite(ring: true));
			f.type = Image.Type.Sliced;
			f.pixelsPerUnitMultiplier = 20f / Radius(p);
		}

		// 선택 표시: 지난번 선택이 있던 쪽에서 쓸려 들어오며 나타남 (목록마다 따로 기억)
		private static readonly Dictionary<string, float> selY = new Dictionary<string, float>();

		private static void SelIn(Image sel)
		{
			RectTransform row = sel.rectTransform.parent as RectTransform;
			if (row == null || row.parent == null)
			{
				return;
			}
			string key = Path(row.parent);
			float y = row.position.y;
			if (selY.TryGetValue(key, out var prev) && Mathf.Abs(prev - y) > 1f)
			{
				float dir = Mathf.Sign(prev - y);
				RectTransform s = sel.rectTransform;
				float h = row.rect.height;
				Color c = sel.color;
				Every(sel, delegate(float t)
				{
					float k = Mathf.Clamp01(t / 0.22f);
					float e = OutQuart(k);
					s.offsetMin = new Vector2(0f, dir * h * 0.6f * (1f - e));
					s.offsetMax = new Vector2(0f, dir * h * 0.6f * (1f - e));
					sel.color = new Color(c.r, c.g, c.b, c.a * Mathf.Clamp01(0.25f + e));
					return k >= 1f;
				}, Time.unscaledTime);
			}
			selY[key] = y;
		}

		private static string Path(Transform t)
		{
			string s = t.name;
			for (Transform p = t.parent; p != null; p = p.parent)
			{
				s = p.name + "/" + s;
			}
			return s;
		}

		public static TextMeshProUGUI Txt(Transform parent, string name, Vector2 pos, Vector2 size, string text, float fontSize, Color color, bool extra = false, TextAlignmentOptions align = TextAlignmentOptions.Left, Vector2? anchor = null, Vector2? pivot = null)
		{
			TextMeshProUGUI t = Node(parent, name, pos, size, anchor, pivot).gameObject.AddComponent<TextMeshProUGUI>();
			// paybooc Extra 아틀라스는 영문이 비어 있어서, 한글이 없는 글자는 Bold 를 굵게 써서 대신한다
			bool hangul = false;
			foreach (char ch in text)
			{
				hangul |= ch >= '가' && ch <= '힣';
			}
			t.font = (extra && hangul) ? Extra : Bold;
			if (extra && !hangul)
			{
				t.fontStyle = FontStyles.Bold;
			}
			t.fontSize = fontSize;
			t.color = color;
			t.alignment = align;
			t.text = text;
			t.raycastTarget = false;
			t.enableWordWrapping = false;
			t.overflowMode = TextOverflowModes.Overflow;
			return t;
		}

		// 영문·숫자 장식 글꼴 (VITRO CORE: 한글 없음)
		public static TextMeshProUGUI VTxt(Transform parent, string name, Vector2 pos, Vector2 size, string text, float fontSize, Color color, TextAlignmentOptions align = TextAlignmentOptions.Left, Vector2? anchor = null, Vector2? pivot = null)
		{
			TextMeshProUGUI t = Txt(parent, name, pos, size, text, fontSize, color, extra: false, align, anchor, pivot);
			bool hangul = false;
			foreach (char ch in text)
			{
				hangul |= ch >= '가' && ch <= '힣';
			}
			// VITRO 에는 한글이 없음: 한글이 섞이면 굵은 기본 글꼴로
			t.font = hangul ? Extra : Vitro;
			t.fontStyle = FontStyles.Normal;
			return t;
		}

		// 자켓처럼 칸을 꽉 채우고 넘치는 부분은 잘라냄
		public static Image Cover(Transform parent, string name, Vector2 pos, Vector2 size, Sprite sp)
		{
			RectTransform box = Node(parent, name, pos, size);
			box.gameObject.AddComponent<RectMask2D>();
			Image img = Img(box, "img", Vector2.zero, size, (sp == null) ? new Color(0.1f, 0.1f, 0.12f, 1f) : Color.white, null, Image.Type.Simple, C, C);
			if (sp != null)
			{
				img.sprite = sp;
				float r = sp.rect.width / Mathf.Max(1f, sp.rect.height);
				img.rectTransform.sizeDelta = (r > size.x / size.y) ? new Vector2(size.y * r, size.y) : new Vector2(size.x, size.x / r);
			}
			return img;
		}

		// 테두리 (두께 t)
		public static void Frame(Transform parent, Color c, float t = 3f)
		{
			if (parent.GetComponent<Mask>() != null)
			{
				RoundFrame((RectTransform)parent, c, t);
				return;
			}
			RectTransform f = Fill(parent, "frame");
			Image a = Img(f, "t", Vector2.zero, new Vector2(0f, t), c);
			a.rectTransform.anchorMax = new Vector2(1f, 1f);
			a.rectTransform.anchorMin = new Vector2(0f, 1f);
			a.rectTransform.offsetMin = new Vector2(0f, -t);
			a.rectTransform.offsetMax = Vector2.zero;
			Image b = Img(f, "b", Vector2.zero, new Vector2(0f, t), c, null, Image.Type.Simple, BL, BL);
			b.rectTransform.anchorMax = new Vector2(1f, 0f);
			b.rectTransform.offsetMin = Vector2.zero;
			b.rectTransform.offsetMax = new Vector2(0f, t);
			Image l = Img(f, "l", Vector2.zero, new Vector2(t, 0f), c, null, Image.Type.Simple, BL, BL);
			l.rectTransform.anchorMax = new Vector2(0f, 1f);
			l.rectTransform.offsetMin = Vector2.zero;
			l.rectTransform.offsetMax = new Vector2(t, 0f);
			Image r = Img(f, "r", Vector2.zero, new Vector2(t, 0f), c, null, Image.Type.Simple, new Vector2(1f, 0f), new Vector2(1f, 0f));
			r.rectTransform.anchorMin = new Vector2(1f, 0f);
			r.rectTransform.offsetMin = new Vector2(-t, 0f);
			r.rectTransform.offsetMax = Vector2.zero;
		}

		private static Sprite hfade;

		// 왼쪽이 진하고 오른쪽으로 사라지는 가로 그라데이션 (자켓 위 글자 받침)
		public static Sprite HFade()
		{
			if (hfade == null)
			{
				Texture2D t = new Texture2D(64, 1, TextureFormat.RGBA32, mipChain: false);
				t.wrapMode = TextureWrapMode.Clamp;
				for (int x = 0; x < 64; x++)
				{
					t.SetPixel(x, 0, new Color(1f, 1f, 1f, Mathf.SmoothStep(1f, 0f, x / 63f)));
				}
				t.Apply();
				hfade = Sprite.Create(t, new Rect(0f, 0f, 64f, 1f), new Vector2(0.5f, 0.5f));
			}
			return hfade;
		}

		public static string Hex(Color c)
		{
			return "#" + ColorUtility.ToHtmlStringRGBA(c);
		}

		public static float Width(TMP_Text t, string s)
		{
			return t.GetPreferredValues(s).x;
		}

		// 게임 하단과 같은 키 안내 (HotKey 배지 + 설명). 오른쪽 끝 x 에서 왼쪽으로 쌓고 다음 x 를 반환
		public static float HotkeyRightToLeft(Transform parent, float right, float y, string key, string label, float labelSize = 24f)
		{
			TextMeshProUGUI lt = Txt(parent, "hk_" + key, new Vector2(0f - right, y), new Vector2(300f, 34f), label, labelSize, Light, extra: false, TextAlignmentOptions.MidlineRight, TR, TR);
			float lw = Width(lt, label) + 4f;
			lt.rectTransform.sizeDelta = new Vector2(lw, 34f);
			float bx = right + lw + 12f;
			Badge(parent, key, new Vector2(0f - bx, y + 2f), TR, TR);
			return bx + BadgeWidth(key) + 40f;
		}

		public static float HotkeyLeftToRight(Transform parent, float x, float y, string key, string label, float labelSize = 24f)
		{
			float bw = BadgeWidth(key);
			Badge(parent, key, new Vector2(x, y + 2f), TL, TL);
			TextMeshProUGUI lt = Txt(parent, "hk_" + key, new Vector2(x + bw + 12f, y), new Vector2(300f, 34f), label, labelSize, Light, extra: false, TextAlignmentOptions.MidlineLeft);
			return x + bw + 12f + Width(lt, label) + 40f;
		}

		private static bool IsArrow(string key)
		{
			if (!(key == "←→"))
			{
				return key == "↑↓";
			}
			return true;
		}

		private static float BadgeWidth(string key)
		{
			if (IsArrow(key))
			{
				return 58f;
			}
			return Mathf.Max(48f, key.Length * 11f + 26f);
		}

		// 화살표는 게임 하단 안내와 같은 arrow_lr 스프라이트 (↑↓는 90도 회전)
		private static void Badge(Transform parent, string key, Vector2 pos, Vector2 anchor, Vector2 pivot)
		{
			if (IsArrow(key))
			{
				RectTransform holder = Node(parent, "key_" + key, pos, new Vector2(58f, 30f), anchor, pivot);
				Image a = Img(holder, "arrow", Vector2.zero, new Vector2(58f, 30f), Color.white, "arrow_lr", Image.Type.Simple, C, C);
				a.preserveAspect = true;
				if (key == "↑↓")
				{
					// 세로로 돌리면 높이가 넘치므로 작게
					a.rectTransform.sizeDelta = new Vector2(40f, 22f);
					a.rectTransform.localEulerAngles = new Vector3(0f, 0f, 90f);
				}
				return;
			}
			Image b = Img(parent, "key_" + key, pos, new Vector2(BadgeWidth(key), 30f), Color.white, "HotKey", Image.Type.Sliced, anchor, pivot);
			Txt(b.transform, "t", Vector2.zero, Vector2.zero, key, 14f, Gray, extra: false, TextAlignmentOptions.Midline).rectTransform.Stretch();
		}

		// 상태 표시용 작은 점 (폰트에 ● 글자가 없음)
		public static void Dot(Transform parent, Vector2 pos, Color c)
		{
			Img(parent, "dot", pos, new Vector2(12f, 12f), c);
		}

		private static Sprite fade;

		// 아래쪽이 진해지는 세로 그라데이션 (키 안내 바닥)
		public static Sprite Fade()
		{
			if (fade == null)
			{
				Texture2D t = new Texture2D(1, 64, TextureFormat.RGBA32, mipChain: false);
				t.wrapMode = TextureWrapMode.Clamp;
				for (int y = 0; y < 64; y++)
				{
					t.SetPixel(0, y, new Color(1f, 1f, 1f, Mathf.SmoothStep(0.85f, 0f, y / 63f)));
				}
				t.Apply();
				fade = Sprite.Create(t, new Rect(0f, 0f, 1f, 64f), new Vector2(0.5f, 0.5f));
			}
			return fade;
		}

		private static Sprite ring;

		private static Sprite disc;

		// 원형 게이지(SCORE LEAD)용 고리 / 원판 (게임에 없는 모양이라 직접 그림)
		public static Sprite Ring()
		{
			return ring ?? (ring = Circle(0.8f));
		}

		public static Sprite Disc()
		{
			return disc ?? (disc = Circle(0f));
		}

		private static Sprite Circle(float inner)
		{
			const int N = 256;
			Texture2D t = new Texture2D(N, N, TextureFormat.RGBA32, mipChain: false);
			t.wrapMode = TextureWrapMode.Clamp;
			for (int y = 0; y < N; y++)
			{
				for (int x = 0; x < N; x++)
				{
					float d = new Vector2(x - N / 2f + 0.5f, y - N / 2f + 0.5f).magnitude / (N / 2f);
					float a = Mathf.Clamp01((1f - d) * N / 2f) * ((inner > 0f) ? Mathf.Clamp01((d - inner) * N / 2f) : 1f);
					t.SetPixel(x, y, new Color(1f, 1f, 1f, a));
				}
			}
			t.Apply();
			return Sprite.Create(t, new Rect(0f, 0f, N, N), new Vector2(0.5f, 0.5f));
		}

		private static Sprite hex;

		private static Sprite hexRing;

		// 육각형 (티어 엠블럼)
		public static Sprite Hex()
		{
			return hex ?? (hex = Hexagon(0f));
		}

		public static Sprite HexRing()
		{
			return hexRing ?? (hexRing = Hexagon(0.86f));
		}

		private static Sprite Hexagon(float inner)
		{
			const int N = 256;
			Texture2D t = new Texture2D(N, N, TextureFormat.RGBA32, mipChain: false);
			t.wrapMode = TextureWrapMode.Clamp;
			for (int y = 0; y < N; y++)
			{
				for (int x = 0; x < N; x++)
				{
					float px = Mathf.Abs(x - N / 2f + 0.5f) / (N / 2f);
					float py = Mathf.Abs(y - N / 2f + 0.5f) / (N / 2f);
					// 꼭짓점이 위아래인 육각형까지의 거리 (1 = 가장자리)
					float d = Mathf.Max(px * 0.8660254f + py * 0.5f, py) / 0.98f;
					float a = Mathf.Clamp01((1f - d) * N / 2f) * ((inner > 0f) ? Mathf.Clamp01((d - inner) * N / 2f) : 1f);
					t.SetPixel(x, y, new Color(1f, 1f, 1f, a));
				}
			}
			t.Apply();
			return Sprite.Create(t, new Rect(0f, 0f, N, N), new Vector2(0.5f, 0.5f));
		}

		// ---------------- 움직임 ----------------
		// 화면은 상태가 바뀔 때마다 다시 조립되므로, 연출 시각은 "지금 화면 단계가 시작된 시각(Epoch)" 이나
		// "그 일을 처음 본 시각(Seen)" 기준으로 잡는다 → 다시 조립돼도 이어서 움직이고, 끝난 건 그대로 있다.
		public static float Epoch;

		private static readonly Dictionary<string, Vector2> seen = new Dictionary<string, Vector2>(); // (처음 본 시각, 마지막으로 본 조립 번호)

		private static int gen;

		public static void NewEpoch()
		{
			Epoch = Time.unscaledTime;
			seen.Clear();
		}

		// 화면을 새로 조립할 때마다 호출: 한 번 빠졌다가 다시 나온 것(팝업 다시 열기 등)은 새로 등장
		public static void Build()
		{
			gen++;
		}

		public static float At(float delay)
		{
			return Epoch + delay;
		}

		public static float Seen(string key)
		{
			if (!seen.TryGetValue(key, out var v) || v.y < gen - 1)
			{
				v.x = Time.unscaledTime;
			}
			v.y = gen;
			seen[key] = v;
			return v.x;
		}

		// 매 프레임 f(t0 부터 지난 시간). true 를 돌려주면 끝
		public static Tick Every(Component c, System.Func<float, bool> f, float t0)
		{
			Tick k = c.gameObject.AddComponent<Tick>();
			k.t0 = t0;
			k.f = f;
			if (f(Time.unscaledTime - t0))
			{
				Object.Destroy(k);
			}
			return k;
		}

		public static float OutCubic(float k)
		{
			return 1f - (1f - k) * (1f - k) * (1f - k);
		}

		public static float OutQuart(float k)
		{
			float x = 1f - k;
			return 1f - x * x * x * x;
		}

		public static float OutBack(float k)
		{
			float x = k - 1f;
			return 1f + 2.70158f * x * x * x + 1.70158f * x * x;
		}

		// 등장: at 부터 dur 동안 (dx, dy) 떨어진 곳에서 미끄러져 들어오고, scale → 1, 투명 → 보임
		public static T In<T>(T c, float at, float dx = 0f, float dy = 0f, float scale = 1f, float dur = 0.5f, bool back = false, float alpha = 0f) where T : Component
		{
			if (c == null)
			{
				return c;
			}
			RectTransform rt = (RectTransform)c.transform;
			if (scale != 1f)
			{
				// 가운데를 기준으로 커지게 (보이는 위치는 그대로)
				Vector2 d = C - rt.pivot;
				rt.pivot = C;
				rt.anchoredPosition += new Vector2(d.x * rt.rect.width, d.y * rt.rect.height);
			}
			Vector2 p = rt.anchoredPosition;
			Vector3 s = rt.localScale;
			CanvasGroup g = null;
			float a1 = 1f;
			if (alpha < 1f)
			{
				g = rt.GetComponent<CanvasGroup>() ?? rt.gameObject.AddComponent<CanvasGroup>();
				a1 = g.alpha;
			}
			Every(c, delegate(float t)
			{
				float k = Mathf.Clamp01(t / (dur * 1.2f));
				float e = back ? OutBack(k) : OutQuart(k);
				rt.anchoredPosition = p + new Vector2(dx, 0f - dy) * (1f - e);
				rt.localScale = s * Mathf.LerpUnclamped(scale, 1f, e);
				if (g != null)
				{
					g.alpha = a1 * Mathf.Lerp(alpha, 1f, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(k * 1.8f)));
				}
				return k >= 1f;
			}, at);
			return c;
		}

		// 숨 쉬듯 알파가 lo~hi 를 오감 (시각 기준이라 다시 조립돼도 박자가 이어짐)
		public static void Pulse(Graphic gr, float lo, float hi, float period = 1.6f)
		{
			Color c = gr.color;
			Every(gr, delegate(float t)
			{
				gr.color = new Color(c.r, c.g, c.b, Mathf.Lerp(lo, hi, 0.5f + 0.5f * Mathf.Sin(t * 6.2831855f / period)));
				return false;
			}, 0f);
		}

		private static Sprite band;

		// 가운데가 밝고 양옆으로 사라지는 띠 (반짝임)
		private static Sprite Band()
		{
			if (band == null)
			{
				Texture2D t = new Texture2D(64, 1, TextureFormat.RGBA32, mipChain: false);
				t.wrapMode = TextureWrapMode.Clamp;
				for (int x = 0; x < 64; x++)
				{
					t.SetPixel(x, 0, new Color(1f, 1f, 1f, Mathf.Sin(x / 63f * Mathf.PI)));
				}
				t.Apply();
				band = Sprite.Create(t, new Rect(0f, 0f, 64f, 1f), new Vector2(0.5f, 0.5f));
			}
			return band;
		}

		// 선택된 칸 위로 비스듬한 빛이 period 마다 왼쪽→오른쪽으로 지나감 (디맥 선택 효과)
		public static void Shine(RectTransform parent, float alpha = 0.35f, float period = 2.4f)
		{
			if (parent.GetComponent<RectMask2D>() == null && parent.GetComponent<Mask>() == null)
			{
				parent.gameObject.AddComponent<RectMask2D>();
			}
			float w = parent.rect.width;
			float h = parent.rect.height;
			float sw = Mathf.Clamp(h * 0.9f, 50f, 160f);
			Image s = Img(parent, "shine", Vector2.zero, new Vector2(sw, h * 2.2f), new Color(1f, 1f, 1f, alpha), null, Image.Type.Simple, TL, C);
			s.sprite = Band();
			s.rectTransform.localEulerAngles = new Vector3(0f, 0f, -20f);
			Every(s, delegate(float t)
			{
				float k = Mathf.Repeat(t, period) / 0.75f;
				s.enabled = k < 1f;
				s.rectTransform.anchoredPosition = new Vector2(Mathf.Lerp(-sw, w + sw, OutCubic(Mathf.Clamp01(k))), -h / 2f);
				return false;
			}, 0f);
		}

		// 숫자가 from → to 로 올라감
		public static void Count(TMP_Text t, float from, float to, float at, float dur, System.Func<float, string> fmt)
		{
			Every(t, delegate(float x)
			{
				float k = Mathf.Clamp01(x / dur);
				t.text = fmt(Mathf.Lerp(from, to, OutCubic(k)));
				return k >= 1f;
			}, at);
		}

		// 막대가 왼쪽에서부터 차오름 (너비 from → to)
		public static void Grow(RectTransform rt, float from, float to, float at, float dur = 0.7f)
		{
			float h = rt.sizeDelta.y;
			Every(rt, delegate(float x)
			{
				float k = Mathf.Clamp01(x / dur);
				rt.sizeDelta = new Vector2(Mathf.Lerp(from, to, OutCubic(k)), h);
				return k >= 1f;
			}, at);
		}

		public static void Stretch(this RectTransform rt)
		{
			rt.anchorMin = Vector2.zero;
			rt.anchorMax = Vector2.one;
			rt.offsetMin = Vector2.zero;
			rt.offsetMax = Vector2.zero;
		}

		public static void Clear(Transform t)
		{
			for (int i = t.childCount - 1; i >= 0; i--)
			{
				Object.Destroy(t.GetChild(i).gameObject);
			}
		}
	}

	// 매 프레임 f(지난 시간) 호출, true 면 스스로 빠짐 (Ui.Every)
	public class Tick : MonoBehaviour
	{
		public System.Func<float, bool> f;

		public float t0;

		private void LateUpdate()
		{
			if (f == null || f(Time.unscaledTime - t0))
			{
				Destroy(this);
			}
		}
	}
}
