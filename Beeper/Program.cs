using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using System.Drawing;
using WMPLib;


namespace Beeper {

	public class Program {

		static List<string> SongExtensions = new List<string>() { ".mp3", ".wav", ".m4p", ".m4a", ".wma" };
		static WindowsMediaPlayer Player = new WindowsMediaPlayer();
		static NotifyIcon Icon = null;
		static int Retry = 0;
		static bool Exiting = false;
		const int Mute = 2200;

		static void PlayRandomFromFolder(string path) {
			var dir = new DirectoryInfo(path);
			if (dir.Exists) {
				var infos = dir.GetFileSystemInfos()
					.Where(x => x is DirectoryInfo || SongExtensions.Contains(x.Extension))
					.ToList();
				if (infos.Count > 0) {
					var obj = infos[new Random().Next(infos.Count)];
					if (obj is DirectoryInfo) PlayRandomFromFolder(obj.FullName);
					else PlaySong(obj.FullName);
				} else if (path != main) Play(main);
			}
		}

		static string SongName(string path) {
			return Regex.Replace(Path.GetFileNameWithoutExtension(path).Replace("_", " "), "[0-9]+", "");
		}

		static void Exit() {
			lock (Icon) {
				if (Exiting) return;
				Exiting = true;
			}
			var maxvol = Player.settings.volume;
			var vol = maxvol;
			if (vol > 1) {
				var delay = Mute / vol;
				while (vol > 0) {
					Player.settings.volume--;
					vol--;
					Thread.Sleep(delay);
				}
			}
			Player.controls.stop();
			Player.settings.volume = maxvol;
			Player.close();
			Icon.Visible = false;
			Application.Exit();
		}

		static void PlayStateChange(int NewState) {
			if ((WMPLib.WMPPlayState)NewState == WMPLib.WMPPlayState.wmppsStopped) {
				Exit();
			}
		}


		static void PlaySong(string path = null, IWMPMedia item = null) {
			if (item != null || (File.Exists(path) && SongExtensions.Contains(Path.GetExtension(path)))) {
				try {
					if (item != null) Player.URL = item.sourceURL;
					else Player.URL = new Uri(path).AbsoluteUri;
					Player.PlayStateChange += PlayStateChange;
					Player.controls.play();
					Thread.Sleep(2000);
				} catch {
				}
				if (Player.playState != WMPPlayState.wmppsPlaying) {
					Play(main);
				} else {
					var songName = "";
					if (!string.IsNullOrEmpty(path)) songName = SongName(path);
					if (item == null) item = Player.currentMedia;
					if (item != null) {
						var author = item.getItemInfo("Author");
						var title = item.getItemInfo("Title");
						if (author != null && title != null) songName = author + ", " + title;
					}
					Icon = new NotifyIcon();
					Icon.BalloonTipTitle = "Playing:";
					Icon.BalloonTipText = songName;
					Icon.BalloonTipIcon = ToolTipIcon.Info;
					Icon.Icon = new System.Drawing.Icon(typeof(Program), "StopPlaying.ico");
					Icon.MouseClick += Icon_MouseClick;
					Icon.ShowBalloonTip(7000);
					Icon.Text = songName;
					Icon.Visible = true;
				}
			}
		}

		private static void Icon_MouseClick(object sender, MouseEventArgs e) {
			if (e.Button == MouseButtons.Left) {
				Exit();
			}
		}

		static void Play(string path) {
			if (Retry++ > 20) Exit();

			var playList = Player.playlistCollection.getByName(path);
			IWMPPlaylist list = null;
			if (playList != null && playList.count > 0) list = playList.Item(new Random().Next(playList.count));
			if (list == null || list.count <= 0) list = Player.mediaCollection.getByAuthor(path);
			if (list == null || list.count <= 0) list = Player.mediaCollection.getByGenre(path);
			if (list == null || list.count <= 0) list = Player.mediaCollection.getByAlbum(path);
			if (list != null && list.count > 0) PlaySong(null, list.Item[new Random().Next(list.count)]);
			else {
				if (Directory.Exists(path)) {
					PlayRandomFromFolder(path);
				} else {
					var ext = Path.GetExtension(path);
					if (SongExtensions.Contains(ext) && File.Exists(path)) PlaySong(path);
				}
			}
		}

		static string main;

		public static void Main(string[] args) {

			var showInfo = false;
			string path = null;
			if (args.Length > 0) {
				path = args[0];
				if (path == "-?" || path == "-help") {
					showInfo = true;
					path = null;
				}
			}
			path = path ?? Environment.GetFolderPath(Environment.SpecialFolder.MyMusic);

			main = path;
			Play(path);

			if (showInfo) {
				var form = new AboutBox1();
				form.Show();
			}
			Application.Run();
		}
	}
}
