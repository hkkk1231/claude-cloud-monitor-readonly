using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

[assembly: System.Runtime.Versioning.TargetFramework(".NETFramework,Version=v4.8")]

internal static class DashboardConfig {
    internal static readonly string FilePath=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"config.json");
    internal static string InstanceKey {
        get {using(var hash=System.Security.Cryptography.SHA256.Create())return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(AppDomain.CurrentDomain.BaseDirectory.ToLowerInvariant()))).Replace("-","").Substring(0,16);}
    }
    internal static Dictionary<string,object> Load() {
        var value=new JavaScriptSerializer().Deserialize<Dictionary<string,object>>(File.ReadAllText(FilePath));
        Validate(value);return value;
    }
    internal static void Validate(Dictionary<string,object> value) {
        if(value==null)throw new Exception("请先设置连接参数。");
        foreach(string key in new[]{"host","user","identityFile"})
            if(!value.ContainsKey(key) || !(value[key] is string) || String.IsNullOrWhiteSpace((string)value[key]))throw new Exception("请填写服务器、用户名和 SSH 私钥路径。");
        if(!Regex.IsMatch((string)value["host"],@"^[a-zA-Z0-9][a-zA-Z0-9_.-]{0,252}$") ||
           !Regex.IsMatch((string)value["user"],@"^[a-zA-Z0-9_][a-zA-Z0-9_.-]{0,63}$"))throw new Exception("服务器须为 IPv4 地址或域名，SSH 用户名不能包含空格或命令字符。");
        string identity=(string)value["identityFile"];
        if(identity.IndexOfAny(new[]{'\"','\r','\n','\0'})>=0 || !Path.IsPathRooted(identity) || !File.Exists(identity))throw new Exception("请选择本机已有 SSH 私钥文件的绝对路径；软件只保存路径，不复制私钥。");
        int port;
        if(value.ContainsKey("port") && (!(value["port"] is int) || !Int32.TryParse(Convert.ToString(value["port"]),out port) || port<1 || port>65535))throw new Exception("SSH 端口须为 1–65535 的整数。");
    }
    internal static void SelfTest() {
        var valid=new Dictionary<string,object>{{"host","server.example.com"},{"user","monitor"},{"identityFile",Application.ExecutablePath},{"port",22}};
        Validate(valid);
        foreach(var change in new[]{new KeyValuePair<string,object>("host","-oProxyCommand=bad"),new KeyValuePair<string,object>("user","user\ncommand"),new KeyValuePair<string,object>("identityFile","relative-key"),new KeyValuePair<string,object>("port",0),new KeyValuePair<string,object>("port",65536),new KeyValuePair<string,object>("port","22")}) {
            var invalid=new Dictionary<string,object>(valid);invalid[change.Key]=change.Value;bool rejected=false;
            try{Validate(invalid);}catch{rejected=true;}if(!rejected)throw new Exception("Invalid connection configuration accepted");
        }
    }
}

internal sealed class DashboardSetup:Form {
    readonly TextBox host=new TextBox(),user=new TextBox(),identity=new TextBox();
    readonly NumericUpDown port=new NumericUpDown{Minimum=1,Maximum=65535,Value=22};
    internal DashboardSetup() {
        Text="设置连接 · Claude 云端监控";ClientSize=new Size(760,480);FormBorderStyle=FormBorderStyle.FixedDialog;AutoSize=true;AutoSizeMode=AutoSizeMode.GrowAndShrink;
        MaximizeBox=false;MinimizeBox=false;StartPosition=FormStartPosition.CenterScreen;Font=new Font("Microsoft YaHei UI",10);
        var layout=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(20),ColumnCount=3,RowCount=7,AutoSize=true};
        for(int i=0;i<7;i++)layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,140));layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,100));
        var info=new Label{Text="填写你自己的 Linux 服务器。账号和 Claude 登录状态保留在服务器；本机只保存连接参数与私钥路径。",Dock=DockStyle.Fill,AutoSize=true,MaximumSize=new Size(700,0),Margin=new Padding(3,3,3,16)};
        layout.Controls.Add(info,0,0);layout.SetColumnSpan(info,3);
        int row=1;foreach(var pair in new[]{new KeyValuePair<string,Control>("服务器 / 域名",host),new KeyValuePair<string,Control>("SSH 用户名",user),new KeyValuePair<string,Control>("SSH 端口",port),new KeyValuePair<string,Control>("SSH 私钥",identity)}) {
            layout.Controls.Add(new Label{Text=pair.Key,AutoSize=true,Anchor=AnchorStyles.Left},0,row);pair.Value.Dock=DockStyle.Fill;pair.Value.Margin=new Padding(3,3,3,10);layout.Controls.Add(pair.Value,1,row++);
        }
        var browse=new Button{Text="选择…",Dock=DockStyle.Fill};browse.Click+=(s,e)=>{using(var dialog=new OpenFileDialog{Title="选择 SSH 私钥（不要选择 .pub 公钥）",CheckFileExists=true})if(dialog.ShowDialog(this)==DialogResult.OK)identity.Text=dialog.FileName;};layout.Controls.Add(browse,2,4);
        var help=new Label{Text="首次连接：保存后运行“验证SSH连接.cmd”，核对服务器指纹。需要 Windows OpenSSH、.NET Framework 4.8 与 WebView2 Runtime。",AutoSize=true,Dock=DockStyle.Fill,MaximumSize=new Size(700,0),Margin=new Padding(3,10,3,16)};layout.Controls.Add(help,0,5);layout.SetColumnSpan(help,3);
        var buttons=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.RightToLeft,AutoSize=true};var save=new Button{Text="保存",AutoSize=true};var cancel=new Button{Text="取消",AutoSize=true,DialogResult=DialogResult.Cancel};buttons.Controls.Add(save);buttons.Controls.Add(cancel);layout.Controls.Add(buttons,0,6);layout.SetColumnSpan(buttons,3);Controls.Add(layout);AcceptButton=save;CancelButton=cancel;
        try{var value=DashboardConfig.Load();host.Text=(string)value["host"];user.Text=(string)value["user"];identity.Text=(string)value["identityFile"];if(value.ContainsKey("port"))port.Value=Convert.ToInt32(value["port"]);}catch{}
        save.Click+=(s,e)=>{
            try{var value=new Dictionary<string,object>{{"host",host.Text.Trim()},{"user",user.Text.Trim()},{"identityFile",identity.Text.Trim()},{"port",(int)port.Value}};DashboardConfig.Validate(value);
                string temporary=DashboardConfig.FilePath+"."+Guid.NewGuid().ToString("N")+".tmp";File.WriteAllText(temporary,new JavaScriptSerializer().Serialize(value),new UTF8Encoding(false));
                if(File.Exists(DashboardConfig.FilePath))File.Replace(temporary,DashboardConfig.FilePath,null);else File.Move(temporary,DashboardConfig.FilePath);DialogResult=DialogResult.OK;Close();
            }catch(Exception error){MessageBox.Show(this,error.Message,"无法保存连接",MessageBoxButtons.OK,MessageBoxIcon.Information);}
        };
    }
}

internal sealed class DashboardReader {
    readonly Dictionary<string,object> config;
    readonly string root = AppDomain.CurrentDomain.BaseDirectory;
    public readonly string Host;
    public DashboardReader() {
        config = DashboardConfig.Load();
        Host = Convert.ToString(config["host"]);
        foreach(string name in new[]{"host","user"})
            if(!Regex.IsMatch(Convert.ToString(config[name]),@"^[a-zA-Z0-9_.-]+$"))throw new Exception("连接配置无效");
    }
    public async Task<Dictionary<string,object>> Read(string kind,CancellationToken token) {
        if(kind!="quota" && kind!="performance")throw new ArgumentException("只支持监控查询");
        string script=Convert.ToBase64String(File.ReadAllBytes(Path.Combine(root,kind=="quota"?"quota_probe.py":"performance_probe.py")));
        string identity=Convert.ToString(config["identityFile"]);
        if(identity.Contains("\"") || !File.Exists(identity))throw new Exception("请检查 SSH 密钥路径");
        string remote="python3 -c \"import base64;exec(base64.b64decode('"+script+"'))\"";
        string args="-F NUL -p "+(config.ContainsKey("port")?config["port"]:22)+" -i \""+identity+"\" -o IdentitiesOnly=yes -o StrictHostKeyChecking=yes -o ClearAllForwardings=yes -o PermitLocalCommand=no -o BatchMode=yes -o ConnectTimeout=7 -o ConnectionAttempts=1 -o ServerAliveInterval=5 -o ServerAliveCountMax=1 "+config["user"]+"@"+Host+" \""+remote.Replace("\"","\\\"")+"\"";
        var info=new ProcessStartInfo("ssh.exe",args){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true,RedirectStandardInput=true,StandardOutputEncoding=Encoding.UTF8,StandardErrorEncoding=Encoding.UTF8};
        using(var process=new Process{StartInfo=info}) {
            token.ThrowIfCancellationRequested();process.Start();process.StandardInput.Close();
            using(token.Register(()=>{try{if(!process.HasExited)process.Kill();}catch{}})) {
                Task<string> output=process.StandardOutput.ReadToEndAsync(),error=process.StandardError.ReadToEndAsync();
                bool finished=await Task.Run(()=>process.WaitForExit(kind=="quota"?35000:12000));
                token.ThrowIfCancellationRequested();
                if(!finished){try{process.Kill();}catch{}throw new Exception("连接超时，请检查服务器与网络");}
                string raw=await output,err=await error;
                if(process.ExitCode!=0)throw new Exception(err.Contains("Permission denied")?"SSH 身份验证失败，请运行验证SSH连接.cmd":err.Contains("Host key verification failed")?"服务器身份未确认，请运行验证SSH连接.cmd并核对指纹":"暂时无法连接服务器，请检查连接设置或运行验证SSH连接.cmd");
                var data=new JavaScriptSerializer().Deserialize<Dictionary<string,object>>(raw);
                if(data==null || !data.ContainsKey("ok") || !Object.Equals(data["ok"],true))throw new Exception(kind=="quota"?"账号数据暂时不可用，稍后自动重试":"性能数据暂时不可用，稍后自动重试");
                return data;
            }
        }
    }
}

internal sealed class DashboardForm:Form {
    readonly WebView2 view=new WebView2{Dock=DockStyle.Fill,DefaultBackgroundColor=Color.FromArgb(244,246,245)};
    readonly JavaScriptSerializer json=new JavaScriptSerializer();
    readonly CancellationTokenSource stopping=new CancellationTokenSource();
    readonly System.Windows.Forms.Timer timer=new System.Windows.Forms.Timer{Interval=1000};
    readonly Dictionary<string,bool> busy=new Dictionary<string,bool>{{"quota",false},{"performance",false}};
    readonly Dictionary<string,DateTime> next=new Dictionary<string,DateTime>{{"quota",DateTime.MinValue},{"performance",DateTime.MinValue}};
    readonly DashboardReader reader=new DashboardReader();
    readonly string root=AppDomain.CurrentDomain.BaseDirectory;
    readonly string testMode;
    bool ready,paused;
    public DashboardForm(string testMode) {
        this.testMode=testMode;Text="Claude 云端监控";ClientSize=new Size(1280,940);MinimumSize=new Size(720,600);
        AutoScaleMode=AutoScaleMode.Dpi;StartPosition=FormStartPosition.CenterScreen;BackColor=Color.FromArgb(244,246,245);
        Icon=Icon.ExtractAssociatedIcon(Application.ExecutablePath);Controls.Add(view);
        Shown+=async(s,e)=>await Initialize();
        timer.Tick+=async(s,e)=>{
            if(!ready || paused || WindowState==FormWindowState.Minimized)return;
            await Task.WhenAll(Poll("quota",false),Poll("performance",false));
        };
        FormClosing+=(s,e)=>{ready=false;timer.Stop();stopping.Cancel();};
        FormClosed+=(s,e)=>{timer.Dispose();view.Dispose();};
    }
    async Task Initialize() {
        try {
            var env=await CoreWebView2Environment.CreateAsync(null,Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"ClaudeMonitor","DashboardRuntime",DashboardConfig.InstanceKey));
            if(stopping.IsCancellationRequested)return;
            await view.EnsureCoreWebView2Async(env);
            var core=view.CoreWebView2;
            core.SetVirtualHostNameToFolderMapping("dashboard.claude-monitor.invalid",Path.Combine(root,"dashboard"),CoreWebView2HostResourceAccessKind.DenyCors);
            core.Settings.AreDefaultContextMenusEnabled=false;core.Settings.AreDevToolsEnabled=false;core.Settings.IsStatusBarEnabled=false;
            core.Settings.IsWebMessageEnabled=true;core.Settings.AreBrowserAcceleratorKeysEnabled=false;
            core.NavigationStarting+=(s,e)=>{if(e.Uri!="https://dashboard.claude-monitor.invalid/index.html")e.Cancel=true;};
            core.NewWindowRequested+=(s,e)=>e.Handled=true;
            core.PermissionRequested+=(s,e)=>e.State=CoreWebView2PermissionState.Deny;
            core.WebMessageReceived+=async(s,e)=>{
                if(e.Source!="https://dashboard.claude-monitor.invalid/index.html")return;
                string action;try{action=e.TryGetWebMessageAsString();}catch{return;}
                if(action=="ready" && !ready){ready=true;Post(new{type="config",host=reader.Host});
                    if(testMode!=null){await CheckUi();return;}
                    timer.Start();await Task.WhenAll(Poll("quota",true),Poll("performance",true));
                }else if(action=="refresh" && testMode==null){await Task.WhenAll(Poll("quota",true),Poll("performance",true));}
                else if(action=="pause"){paused=true;Post(new{type="paused",value=true});}
                else if(action=="resume"){paused=false;Post(new{type="paused",value=false});if(testMode==null)await Task.WhenAll(Poll("quota",true),Poll("performance",true));}
            };
            core.Navigate("https://dashboard.claude-monitor.invalid/index.html");
        }catch(Exception error){if(testMode!=null){File.WriteAllText(Path.Combine(root,"dashboard-ui-error.txt"),error.ToString());Environment.ExitCode=1;}else if(!stopping.IsCancellationRequested)MessageBox.Show(this,"监控主页加载失败，请检查 WebView2 Runtime 和本地资源文件。","Claude 云端监控");Close();}
    }
    void Post(object message){if(ready && !stopping.IsCancellationRequested)view.CoreWebView2.PostWebMessageAsJson(json.Serialize(message));}
    async Task Poll(string kind,bool force) {
        if(busy[kind] || stopping.IsCancellationRequested || (!force && DateTime.UtcNow<next[kind]))return;
        // Manual refresh is also coalesced; it never bypasses upstream quota backoff.
        busy[kind]=true;Post(new{type="loading",kind=kind});
        try{var data=await reader.Read(kind,stopping.Token);Post(new{type="data",kind=kind,data=data});}
        catch(OperationCanceledException){}
        catch(Exception error){Post(new{type="error",kind=kind,message=error.Message});}
        finally{busy[kind]=false;next[kind]=DateTime.UtcNow.AddSeconds(kind=="quota"?60:10);Post(new{type="settled",kind=kind});}
    }
    async Task CheckUi() {
        try{
            await view.CoreWebView2.ExecuteScriptAsync(File.ReadAllText(Path.Combine(root,"dashboard_fixture.js")));
            if(testMode=="live"){
                await Task.WhenAll(Poll("quota",true),Poll("performance",true));
            }else await view.CoreWebView2.ExecuteScriptAsync("window.dashboardTest.seed("+json.Serialize(testMode)+")");
            await Task.Delay(750);
            string directory=Path.Combine(root,"dashboard-check");Directory.CreateDirectory(directory);
            foreach(int width in new[]{1280,900,720}){
                ClientSize=new Size(width,width==1280?940:860);await Task.Delay(350);
                string audit=await view.CoreWebView2.ExecuteScriptAsync("JSON.stringify(window.dashboardTest.audit())");
                File.WriteAllText(Path.Combine(directory,testMode+"-"+width+".json"),json.Deserialize<string>(audit),Encoding.UTF8);
                using(var stream=File.Create(Path.Combine(directory,testMode+"-"+width+".png")))await view.CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png,stream);
                await view.CoreWebView2.ExecuteScriptAsync("window.scrollTo(0,document.body.scrollHeight)");await Task.Delay(100);
                using(var stream=File.Create(Path.Combine(directory,testMode+"-"+width+"-bottom.png")))await view.CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png,stream);
                await view.CoreWebView2.ExecuteScriptAsync("window.scrollTo(0,0)");
            }
        }catch(Exception error){File.WriteAllText(Path.Combine(root,"dashboard-ui-error.txt"),error.ToString());Environment.ExitCode=1;}
        finally{Close();}
    }
}

internal static class DashboardProgram {
    [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr handle);
    [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr handle,int command);
    [STAThread] static void Main(string[] args){
        try{
            if(args.Length>0 && args[0]=="--self-test"){DashboardConfig.SelfTest();return;}
            if(args.Length>0 && args[0]=="--config-check"){try{DashboardConfig.Load();}catch{Environment.ExitCode=1;}return;}
            if(args.Length==2 && args[0]=="--setup-ui-check") {
                Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
                using(var setup=new DashboardSetup()){setup.Shown+=(s,e)=>{using(var bitmap=new Bitmap(setup.Width,setup.Height)){setup.DrawToBitmap(bitmap,new Rectangle(Point.Empty,setup.Size));bitmap.Save(args[1]);}setup.Close();};Application.Run(setup);}return;
            }
            if(args.Length>0 && args[0]=="--configure") {Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);using(var setup=new DashboardSetup())if(setup.ShowDialog()!=DialogResult.OK)Environment.ExitCode=1;return;}
            if(args.Length>0 && args[0]=="--check"){
                var reader=new DashboardReader();var result=new Dictionary<string,object>();
                Task.Run(async()=>{await Task.WhenAll(Check(reader,"quota",result),Check(reader,"performance",result));}).GetAwaiter().GetResult();
                File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"dashboard-live-check.json"),new JavaScriptSerializer().Serialize(result),Encoding.UTF8);return;
            }
            bool test=args.Length>0 && args[0]=="--ui-check";
            if(args.Length>0 && !test){Environment.ExitCode=2;return;}
            bool created;
            using(var mutex=new Mutex(true,"Local\\ClaudeMonitor.Dashboard.v2."+DashboardConfig.InstanceKey+(test?".Test":""),out created)){
                if(!created){foreach(var p in Process.GetProcessesByName(Process.GetCurrentProcess().ProcessName))if(p.Id!=Process.GetCurrentProcess().Id && p.MainWindowHandle!=IntPtr.Zero){ShowWindow(p.MainWindowHandle,9);SetForegroundWindow(p.MainWindowHandle);}return;}
                Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
                try{DashboardConfig.Load();}catch{using(var setup=new DashboardSetup())if(setup.ShowDialog()!=DialogResult.OK)return;}
                Application.Run(new DashboardForm(test?(args.Length>1?args[1]:"normal"):null));
            }
        }catch(Exception){Environment.ExitCode=1;MessageBox.Show("启动失败，请检查监控目录内的配置和程序依赖。","Claude 云端监控");}
    }
    static async Task Check(DashboardReader reader,string kind,Dictionary<string,object> result){
        object data;try{data=await reader.Read(kind,CancellationToken.None);}catch(Exception error){data=new{ok=false,error=error.Message};Environment.ExitCode=1;}
        lock(result)result[kind]=data;
    }
}
