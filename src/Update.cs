// 자동 업데이트: 게임을 켜면 GitHub 최신 릴리스(build.txt)를 보고, 더 새 빌드면 WJMAXOnline.dll 을 받아 덮어쓴다.
// 실행 중인 DLL 은 메모리에 올라가 있어서 덮어써도 되고, 다음 실행부터 새 버전이 돈다.
// 보안: build.txt 둘째 줄은 "WJMAXOnline|빌드|DLL의 SHA256" 에 대한 RSA 서명. 서명 키(signkey.xml)는 만든 사람 PC 에만 있어서
// GitHub 계정이 털려도 서명 없는 DLL 은 절대 받지 않는다. 빌드 번호도 서명에 들어 있어서 옛 버전으로 되돌리기도 막힌다.
using System;
using System.IO;
using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace WJMAXOnline
{
	public static class Updater
	{
		private const string BASE = "https://github.com/boxblock32/wjmaxmod/releases/latest/download/";

		private const string PUBKEY = "<RSAKeyValue><Modulus>7H+QpysAk52vSYfgQ5rtSLT48dkUeT0Fjawh9hcNUFczSk4ao5kbZVZTiqpnE4UFRf4v9f7ASJzqc0BeRTwvpwhoeQ+WsbekiJKY1Q3a/FgLYy3y5cAMqheT1t0zZCXPTtyZEeirIVDiNxNXz7zhMJc/Jy01Bx03UvJhy9W9K3Mgp9v48dMqps0wiKL45YbtryPGi2LcZN8v5NlQhnuW2EKtej1WrjG7WV1/f5a8/jzCBb2wWT7rC0ApQELG3RAWkXtGp5+Hugt2ZE3vgxKUGMYsdxkTrNbRASHX2WB2I/2T6sJKULjcHg+gifZ0gzbsXahmI5oGwd1B+j0TYxW9MtwerDgLEZqNKJTPK0mGviEvqAubQU/46PoUeehZJpAdPO+g4jGBPePedB1RSv0KA+7GLadqPlbL78WttBMdGKDjDeKycxwoGtRfkKhCm6acA+2PFeMA9OqJukGAbYGqopkpwKh7Z8dBubp3eFS8mpqAkMFHK8OjuudBRLnRADy9</Modulus><Exponent>AQAB</Exponent></RSAKeyValue>";

		// 받기 끝나면 새 빌드 번호 (화면 표시용)
		public static volatile int ready;

		public static void Start(int build)
		{
#if RELEASE
			new Thread(() =>
			{
				try
				{
					Run(build);
				}
				catch (Exception e)
				{
					UnityEngine.Debug.Log("[WJMAXOnline] update check failed: " + e.Message);
				}
			}) { IsBackground = true }.Start();
#endif
		}

		private static byte[] Get(string url, int max)
		{
			HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);
			req.UserAgent = "WJMAXOnline";
			req.Timeout = 15000;
			req.ReadWriteTimeout = 15000;
			using WebResponse resp = req.GetResponse();
			using Stream s = resp.GetResponseStream();
			using MemoryStream ms = new MemoryStream();
			byte[] buf = new byte[65536];
			int n;
			while ((n = s.Read(buf, 0, buf.Length)) > 0)
			{
				ms.Write(buf, 0, n);
				if (ms.Length > max)
				{
					throw new Exception("too big");
				}
			}
			return ms.ToArray();
		}

		public static bool Verify(int build, byte[] dll, byte[] sig)
		{
			using SHA256 sha = SHA256.Create();
			byte[] msg = Encoding.ASCII.GetBytes("WJMAXOnline|" + build + "|" + BitConverter.ToString(sha.ComputeHash(dll)).Replace("-", ""));
			using RSACryptoServiceProvider rsa = new RSACryptoServiceProvider();
			rsa.FromXmlString(PUBKEY);
			return rsa.VerifyHash(sha.ComputeHash(msg), CryptoConfig.MapNameToOID("SHA256"), sig);
		}

		private static void Run(int build)
		{
			Thread.Sleep(3000);
			string[] info = Encoding.ASCII.GetString(Get(BASE + "build.txt", 4096)).Split(new char[2] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
			int latest = int.Parse(info[0].Trim());
			if (latest <= build)
			{
				return;
			}
			byte[] dll = Get(BASE + "WJMAXOnline.dll", 16 << 20);
			if (!Verify(latest, dll, Convert.FromBase64String(info[1].Trim())))
			{
				throw new Exception("bad signature");
			}
			string root = Path.GetDirectoryName(UnityEngine.Application.dataPath);
			string dst = Path.Combine(UnityEngine.Application.dataPath, "Managed", "WJMAXOnline.dll");
			string tmp = dst + ".new";
			File.WriteAllBytes(tmp, dll);
			// 받은 게 진짜 이 모드 DLL 인지 확인 (깨진 파일로 덮어써서 게임이 안 켜지는 일 막기)
			if (AssemblyName.GetAssemblyName(tmp).Name != "WJMAXOnline")
			{
				File.Delete(tmp);
				throw new Exception("not WJMAXOnline");
			}
			string copy = Path.Combine(root, "OnlineMod", "WJMAXOnline.dll");
			if (File.Exists(copy))
			{
				File.Copy(tmp, copy, true);
			}
			// 한 번에 바꿔치기 (복사 도중 게임이 꺼져도 반쪽짜리 DLL 이 남지 않게)
			try
			{
				File.Replace(tmp, dst, null);
			}
			catch (Exception)
			{
				File.Copy(tmp, dst, true);
				File.Delete(tmp);
			}
			ready = latest;
			UnityEngine.Debug.Log("[WJMAXOnline] updated to build " + latest);
		}
	}
}
