// 자동 업데이트: 게임을 켜면 GitHub 최신 릴리스(build.txt)를 보고, 더 새 빌드면 WJMAXOnline.dll 을 받아 덮어쓴다.
// 실행 중인 DLL 은 메모리에 올라가 있어서 덮어써도 되고, 다음 실행부터 새 버전이 돈다.
using System;
using System.IO;
using System.Net;
using System.Reflection;
using System.Threading;

namespace WJMAXOnline
{
	public static class Updater
	{
		private const string BASE = "https://github.com/boxblock32/wjmaxonline/releases/latest/download/";

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

		private static void Run(int build)
		{
			Thread.Sleep(3000);
			int latest = int.Parse(System.Text.Encoding.ASCII.GetString(Get(BASE + "build.txt", 64)).Trim());
			if (latest <= build)
			{
				return;
			}
			byte[] dll = Get(BASE + "WJMAXOnline.dll", 16 << 20);
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
			File.Copy(tmp, dst, true);
			string copy = Path.Combine(root, "OnlineMod", "WJMAXOnline.dll");
			if (File.Exists(copy))
			{
				File.Copy(tmp, copy, true);
			}
			File.Delete(tmp);
			ready = latest;
			UnityEngine.Debug.Log("[WJMAXOnline] updated to build " + latest);
		}
	}
}
