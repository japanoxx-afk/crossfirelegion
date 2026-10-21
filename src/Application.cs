using System;
using System.IO;
using System.Diagnostics;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
[assembly:System.Reflection.AssemblyVersion("0.1.0.0")]
class Launcher:Form {
 const string CurrentVersion="0.1.0";
 string root,config;
 TextBox path=new TextBox();Label status=new Label();TextBox log=new TextBox();FlowLayoutPanel actions=new FlowLayoutPanel();
 [STAThread] static void Main(string[] args){
  Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
  if(args.Length>0 && args[0]=="--self-test"){Environment.Exit(SelfTest.Run(args.Length>1?args[1]:null));return;}
  Application.Run(new Launcher(args.Length>0?args[0]:Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"..",".."))));
 }
 Launcher(string installRoot){
  root=Path.GetFullPath(installRoot);config=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"CrossfireLegionLauncher","game-path.txt");
  Text="Crossfire Legion · All Units Launcher "+CurrentVersion;ClientSize=new Size(760,490);MinimumSize=new Size(700,500);StartPosition=FormStartPosition.CenterScreen;
  BackColor=Color.FromArgb(22,27,35);ForeColor=Color.WhiteSmoke;Font=new Font("맑은 고딕",10);
  var title=new Label{Text="CROSSFIRE LEGION",Font=new Font("Segoe UI",23,FontStyle.Bold),Location=new Point(24,20),AutoSize=true};Controls.Add(title);
  Controls.Add(new Label{Text="ALL UNITS  /  모든 유닛 생산",Location=new Point(27,68),AutoSize=true,ForeColor=Color.LightSkyBlue});
  path.SetBounds(25,112,600,28);path.Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right;Controls.Add(path);
  var browse=new Button{Text="찾아보기",Location=new Point(635,110),Size=new Size(100,32),Anchor=AnchorStyles.Top|AnchorStyles.Right};browse.Click+=(s,e)=>{using(var d=new FolderBrowserDialog()){d.SelectedPath=path.Text;if(d.ShowDialog()==DialogResult.OK){path.Text=d.SelectedPath;Save();RefreshStatus();}}};Controls.Add(browse);
  status.SetBounds(25,152,710,30);status.Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right;Controls.Add(status);
  actions.SetBounds(25,194,710,88);actions.Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right;Controls.Add(actions);
  AddButton("모든 유닛 패치 적용",()=>{string game=path.Text;return Task.Run(()=>Patcher.Apply(game));});
  AddButton("원본 복원",()=>{string game=path.Text;return Task.Run(()=>Patcher.Restore(game));});
  AddButton("Steam으로 게임 실행",async()=>{Save();string game=path.Text;SteamLaunch.Prepare(game);if(Patcher.NeedsUpgrade(game)){Write("이전 게임 패치를 최신 HUD/로딩 패치로 갱신 중…");await Task.Run(()=>Patcher.Apply(game));}Write(SteamLaunch.Start(game));});
  AddButton("런처 업데이트",async()=>{Write("GitHub 최신 버전 확인 중…");string next=await Updater.Update(root,CurrentVersion);if(next==null){Write("이미 최신 버전입니다 · 현재 v"+CurrentVersion);return;}Process.Start(new ProcessStartInfo(next,"\""+root.TrimEnd('\\')+"\""){WorkingDirectory=Path.GetDirectoryName(next)});Close();});
  AddButton("진단 로그 폴더 열기",()=>{string folder=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),"AppData","LocalLow","Blackbird Interactive","Crossfire Legion","Logs");Directory.CreateDirectory(folder);Process.Start(new ProcessStartInfo(folder){UseShellExecute=true});Write(folder);return Task.FromResult(0);});
  Controls.Add(new Label{Text="진영 전체 유닛 + 하단 고정 생산 HUD(최대 64개), 설명창 자동 배치. Steam 실행 시 이전 패치 자동 갱신.\n새 경기에서 반영. 싱글플레이: Enter → money(자원 9999) / speed(생산·건설 100배) / pop(최대 인구) → Enter",Location=new Point(25,287),Size=new Size(710,52),Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right,ForeColor=Color.Silver});
  log.SetBounds(25,350,710,115);log.Multiline=true;log.ReadOnly=true;log.ScrollBars=ScrollBars.Vertical;log.Anchor=AnchorStyles.Top|AnchorStyles.Bottom|AnchorStyles.Left|AnchorStyles.Right;log.BackColor=Color.FromArgb(13,17,23);log.ForeColor=Color.LightGray;Controls.Add(log);
  path.Text=File.Exists(config)?File.ReadAllText(config):@"C:\Program Files (x86)\Steam\steamapps\common\Crossfire Legion (Demo)";
  path.Leave+=(s,e)=>{Save();RefreshStatus();};RefreshStatus();Write("런처 준비 완료 · v"+CurrentVersion);
 }
 void AddButton(string text,Func<Task> action){var b=new Button{Text=text,Size=new Size(168,38),FlatStyle=FlatStyle.Flat,BackColor=Color.FromArgb(40,54,69),ForeColor=Color.White};b.Click+=async(s,e)=>{actions.Enabled=false;path.Enabled=false;try{await action();Write(text+" 완료");}catch(Exception ex){Write("실패: "+ex.Message);MessageBox.Show(this,ex.Message,text);}finally{actions.Enabled=true;path.Enabled=true;RefreshStatus();}};actions.Controls.Add(b);}
 void Save(){Directory.CreateDirectory(Path.GetDirectoryName(config));File.WriteAllText(config,path.Text);}
 void RefreshStatus(){try{status.Text=Patcher.Status(path.Text);}catch(Exception e){status.Text="확인 실패: "+e.Message;}}
 void Write(string text){log.AppendText(DateTime.Now.ToString("HH:mm:ss")+"  "+text+Environment.NewLine);}
}
