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
  if(args.Length==3&&args[0]=="--resume-replay"){try{using(var previous=Process.GetProcessById(Int32.Parse(args[2])))if(!previous.WaitForExit(30000))return;}catch(ArgumentException){}try{SteamLaunch.Start(args[1]);}catch(Exception e){MessageBox.Show(e.Message,"리플레이 다시 시작");}return;}
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
  AddButton("리플레이 · 자동 녹화",()=>{using(var form=new ReplayWindow(path.Text))form.ShowDialog(this);return Task.FromResult(0);});
  AddButton("업데이트 내역 보기",()=>{using(var form=new Form{Text="업데이트 내역 · v"+CurrentVersion,ClientSize=new Size(760,520),MinimumSize=new Size(560,360),StartPosition=FormStartPosition.CenterParent,Font=Font}){var text=new TextBox{Dock=DockStyle.Fill,Multiline=true,ReadOnly=true,ScrollBars=ScrollBars.Vertical,BackColor=BackColor,ForeColor=ForeColor};using(var stream=typeof(Launcher).Assembly.GetManifestResourceStream("CHANGELOG.md"))using(var reader=new StreamReader(stream))text.Text=reader.ReadToEnd().Replace("\r\n","\n").Replace("\n","\r\n");text.SelectionStart=0;text.SelectionLength=0;form.Controls.Add(text);form.ShowDialog(this);}return Task.FromResult(0);});
  Controls.Add(new Label{Text="진영 전체 유닛 + 하단 고정 생산 HUD(최대 64개), 설명창 자동 배치. Steam 실행 시 이전 패치 자동 갱신.\n새 경기에서 반영. 싱글플레이: Enter → money(자원 9999) / speed(생산·건설 100배) / pop(최대 인구) → Enter",Location=new Point(25,287),Size=new Size(710,52),Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right,ForeColor=Color.Silver});
  log.SetBounds(25,350,710,115);log.Multiline=true;log.ReadOnly=true;log.ScrollBars=ScrollBars.Vertical;log.Anchor=AnchorStyles.Top|AnchorStyles.Bottom|AnchorStyles.Left|AnchorStyles.Right;log.BackColor=Color.FromArgb(13,17,23);log.ForeColor=Color.LightGray;Controls.Add(log);
  path.Text=File.Exists(config)?File.ReadAllText(config):@"C:\Program Files (x86)\Steam\steamapps\common\Crossfire Legion (Demo)";
  path.Leave+=(s,e)=>{Save();RefreshStatus();};RefreshStatus();Write("런처 준비 완료 · v"+CurrentVersion);
  Shown+=async(s,e)=>await CheckStartupUpdate();
 }
 async Task CheckStartupUpdate(){
  try{
   Write("GitHub 최신 버전 자동 확인 중…");string version=await Updater.CheckLatest(CurrentVersion);
   if(IsDisposed||Disposing)return;
   if(version==null){Write("최신 버전입니다 · v"+CurrentVersion);return;}
   if(!actions.Enabled||OwnedForms.Length>0){Write("새 버전 v"+version+"이 있습니다. 런처 업데이트 버튼으로 설치하세요.");return;}
   if(MessageBox.Show(this,"새 버전 v"+version+"이 있습니다.\n현재 버전: v"+CurrentVersion+"\n\n지금 다운로드하고 새 런처로 다시 시작할까요?","런처 업데이트",MessageBoxButtons.YesNo,MessageBoxIcon.Question,MessageBoxDefaultButton.Button2)!=DialogResult.Yes){Write("업데이트를 나중으로 미뤘습니다 · 새 버전 v"+version);return;}
   actions.Enabled=false;path.Enabled=false;
   try{Write("v"+version+" 다운로드 및 무결성 검사 중…");string next=await Updater.UpdateTo(root,CurrentVersion,version);if(IsDisposed||Disposing)return;Process.Start(new ProcessStartInfo(next,"\""+root.TrimEnd('\\')+"\""){WorkingDirectory=Path.GetDirectoryName(next)});Close();}
   finally{if(!IsDisposed){actions.Enabled=true;path.Enabled=true;}}
  }catch(Exception e){if(!IsDisposed&&!Disposing)Write("자동 업데이트 확인/설치 실패: "+e.Message+" · 런처 업데이트 버튼으로 다시 시도할 수 있습니다.");}
 }
 void AddButton(string text,Func<Task> action){var b=new Button{Text=text,Size=new Size(168,38),FlatStyle=FlatStyle.Flat,BackColor=Color.FromArgb(40,54,69),ForeColor=Color.White};b.Click+=async(s,e)=>{actions.Enabled=false;path.Enabled=false;try{await action();Write(text+" 완료");}catch(Exception ex){Write("실패: "+ex.Message);MessageBox.Show(this,ex.Message,text);}finally{actions.Enabled=true;path.Enabled=true;RefreshStatus();}};actions.Controls.Add(b);}
 void Save(){Directory.CreateDirectory(Path.GetDirectoryName(config));File.WriteAllText(config,path.Text);}
 void RefreshStatus(){try{status.Text=Patcher.Status(path.Text);}catch(Exception e){status.Text="확인 실패: "+e.Message;}}
 void Write(string text){log.AppendText(DateTime.Now.ToString("HH:mm:ss")+"  "+text+Environment.NewLine);}
}
sealed class ReplayWindow:Form {
 readonly string game;readonly ListBox list=new ListBox();readonly TextBox detail=new TextBox();readonly Label state=new Label();readonly FlowLayoutPanel buttons=new FlowLayoutPanel();readonly Timer timer=new Timer();
 public ReplayWindow(string selectedGame){
  game=selectedGame;Text="경기 자동 녹화 · 관전자 리플레이";ClientSize=new Size(900,610);MinimumSize=new Size(820,600);StartPosition=FormStartPosition.CenterParent;Font=new Font("맑은 고딕",10);
  state.SetBounds(18,12,860,52);state.Text="실제 경기의 네이티브 명령 기록을 사용합니다. 관전자·재생은 재녹화하지 않습니다.\n기록 종료와 전체 재생 검증은 다릅니다. 호환성 정보가 없는 과거 파일은 재현을 보장하지 않습니다.";Controls.Add(state);
  list.SetBounds(18,70,860,225);list.Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right;list.SelectedIndexChanged+=(s,e)=>ShowDetails();Controls.Add(list);
  detail.SetBounds(18,305,860,150);detail.Multiline=true;detail.ReadOnly=true;detail.ScrollBars=ScrollBars.Vertical;detail.Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right;Controls.Add(detail);
  buttons.SetBounds(18,505,860,90);Controls.Add(buttons);
  Button("새로고침",()=>{RefreshList();return Task.FromResult(0);});
  Button("관전자 재생",async()=>{var entry=Selected();if(Patcher.NeedsUpgrade(game)||!File.Exists(Path.Combine(game,"Crossfire_Legion_Data/Managed/CflRuntime.dll")))throw new Exception("게임을 종료한 상태에서 런처의 모든 유닛 패치 적용으로 리플레이 패치를 설치하세요.");string request=await Task.Run(()=>ReplayLibrary.Request(game,entry));try{SteamLaunch.Start(game);}catch{File.Delete(request);throw;}detail.AppendText("\r\n실행 요청됨 · 실제 로딩/재생 결과는 리플레이 진단 로그에서 확인하세요.");});
  Button("가져오기",async()=>{using(var d=new OpenFileDialog{Filter="리플레이|*.rply;*.cflreplay.zip"})if(d.ShowDialog(this)==DialogResult.OK){string file=d.FileName;await Task.Run(()=>ReplayLibrary.Import(file));RefreshList();}});
  Button("내보내기",async()=>{var entry=Selected();using(var d=new SaveFileDialog{Filter="리플레이 묶음|*.cflreplay.zip",FileName=entry.Name+".cflreplay.zip",OverwritePrompt=false})if(d.ShowDialog(this)==DialogResult.OK){string file=d.FileName;if(File.Exists(file))throw new IOException("기존 파일을 덮어쓰지 않습니다. 다른 이름을 선택하세요.");await Task.Run(()=>ReplayLibrary.Export(entry,file));}});
  Button("기록 폴더",()=>{Open(ReplayProtocol.NativeFolder);return Task.FromResult(0);});Button("리플레이 진단",()=>{Open(ReplayProtocol.Home);return Task.FromResult(0);});
  RefreshList();timer.Interval=3000;timer.Tick+=(s,e)=>RefreshList();timer.Start();FormClosed+=(s,e)=>timer.Dispose();
 }
 ReplayEntry Selected(){var entry=list.SelectedItem as ReplayEntry;if(entry==null)throw new Exception("리플레이를 선택하세요.");return entry;}
 void Open(string path){Directory.CreateDirectory(path);Process.Start(new ProcessStartInfo(path){UseShellExecute=true});}
 void Button(string text,Func<Task> action){var b=new Button{Text=text,Width=130,Height=34};b.Click+=async(s,e)=>{buttons.Enabled=false;timer.Stop();try{await action();}catch(Exception ex){MessageBox.Show(this,ex.Message,"리플레이");}finally{buttons.Enabled=true;timer.Start();}};buttons.Controls.Add(b);}
 void RefreshList(){try{string selected=list.SelectedItem is ReplayEntry?((ReplayEntry)list.SelectedItem).File:null;list.BeginUpdate();list.Items.Clear();foreach(var entry in ReplayLibrary.List()){int index=list.Items.Add(entry);if(entry.File==selected)list.SelectedIndex=index;}if(list.SelectedIndex<0&&list.Items.Count>0)list.SelectedIndex=0;list.EndUpdate();var live=ReplayProtocol.Read(Path.Combine(ReplayProtocol.Home,"status.ini"));if(live.Count>0)state.Text="자동 녹화·재생 진단 (기록 종료와 전체 재생 검증은 구분됩니다)\n"+ReplayProtocol.Value(live,"State");}catch(Exception e){detail.Text=e.Message;}}
 void ShowDetails(){var entry=list.SelectedItem as ReplayEntry;if(entry==null)return;double frame,rate;string duration="";if(Double.TryParse(ReplayProtocol.Value(entry.Metadata,"LastFrame"),out frame)&&Double.TryParse(ReplayProtocol.Value(entry.Metadata,"TicksPerSecond"),System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out rate)&&rate>0)duration=" · 경기 길이 "+TimeSpan.FromSeconds(frame/rate).ToString(@"hh\:mm\:ss");detail.Text="파일: "+entry.File+"\r\n상태: "+entry.State+" · "+entry.Bytes.ToString("N0")+" bytes"+duration+"\r\n플레이어: "+entry.Players+"\r\n맵 ID: "+entry.Map+" · 모드: "+entry.Mode+" · 게임 빌드: "+entry.Build+"\r\n"+(entry.Error??"재생: 게임 기본 관전자 카메라·미니맵·관점·시야·일시정지·배속 UI를 사용합니다. 시간 탐색은 제공하지 않습니다.");}
}
