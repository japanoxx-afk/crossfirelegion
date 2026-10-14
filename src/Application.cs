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
  AddButton("Steam으로 게임 실행",()=>{Save();SteamLaunch.Start(path.Text);Write("Steam에 실행을 요청했습니다. Steam 로그인 및 온라인 상태를 확인하세요.");return Task.FromResult(0);});
  AddButton("런처 업데이트",async()=>{Write("GitHub 최신 버전 확인 중…");string next=await Updater.Update(root,CurrentVersion);if(next==null){Write("이미 최신 버전입니다.");return;}Process.Start(new ProcessStartInfo(next,"\""+root.TrimEnd('\\')+"\""){WorkingDirectory=Path.GetDirectoryName(next)});Close();});
  Controls.Add(new Label{Text="덱 화면은 9칸을 유지합니다. 패치 후 해당 진영의 생산·업그레이드 필터가 해제됩니다.\n오프라인·사용자 지정 테스트용. 실제 경기 및 생산 메뉴 표시는 게임 내 확인이 필요합니다.",Location=new Point(25,287),Size=new Size(710,52),Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right,ForeColor=Color.Silver});
  log.SetBounds(25,350,710,115);log.Multiline=true;log.ReadOnly=true;log.ScrollBars=ScrollBars.Vertical;log.Anchor=AnchorStyles.Top|AnchorStyles.Bottom|AnchorStyles.Left|AnchorStyles.Right;log.BackColor=Color.FromArgb(13,17,23);log.ForeColor=Color.LightGray;Controls.Add(log);
  path.Text=File.Exists(config)?File.ReadAllText(config):@"C:\Program Files (x86)\Steam\steamapps\common\Crossfire Legion (Demo)";
  path.Leave+=(s,e)=>{Save();RefreshStatus();};RefreshStatus();Write("런처 준비 완료 · v"+CurrentVersion);
 }
 void AddButton(string text,Func<Task> action){var b=new Button{Text=text,Size=new Size(168,38),FlatStyle=FlatStyle.Flat,BackColor=Color.FromArgb(40,54,69),ForeColor=Color.White};b.Click+=async(s,e)=>{actions.Enabled=false;path.Enabled=false;try{await action();Write(text+" 완료");}catch(Exception ex){Write("실패: "+ex.Message);MessageBox.Show(this,ex.Message,text);}finally{actions.Enabled=true;path.Enabled=true;RefreshStatus();}};actions.Controls.Add(b);}
 void Save(){Directory.CreateDirectory(Path.GetDirectoryName(config));File.WriteAllText(config,path.Text);}
 void RefreshStatus(){try{status.Text=Patcher.Status(path.Text);}catch(Exception e){status.Text="확인 실패: "+e.Message;}}
 void Write(string text){log.AppendText(DateTime.Now.ToString("HH:mm:ss")+"  "+text+Environment.NewLine);}
}
