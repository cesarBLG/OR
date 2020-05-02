using ORTS.Common;
using ORTS.Scripting.Api;
using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.IO.Ports;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Linq;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using Orts.Common;
using Event = Orts.Common.Event;
namespace ORTS.Scripting.Script
{
    public class ServerTCS : TrainControlSystem
    {
        public Client c=null;
        HashSet<Parameter> parameters = new HashSet<Parameter>();
        List<InteractiveTCS> tcs = new List<InteractiveTCS>();
        public void RequestModule(string module)
        {
            if (c != null) c.WriteLine("request_module("+module+")");
        }
        public void Register(string parameter)
        {
            if (c != null) c.WriteLine("register("+parameter+")");
        }
        public void RemoveParameter(string parameter)
        {
            if (c != null) c.WriteLine("unregister("+parameter+")");
        }
        public void SendParameter(string parameter, string value)
        {
            if (c != null) c.WriteLine(parameter+"="+value);
        }
        Parameter GetParameter(string parameter)
        {
            Parameter compared = new Parameter(parameter);
            Parameter p = null;
            parameters.TryGetValue(compared, out p);
            return p;
        }
        float LocomotiveOverspeedMpS;
        public override void Initialize()
        {
            Activated = false;
            tcs.Add(new HM(this));
            if(GetBoolParameter("ASFA","Digital",false)) tcs.Add(new ASFADigital(this));
            else tcs.Add(new ASFAclasico(this));
            tcs.Add(new ETCS(this));
            
            LocomotiveOverspeedMpS = MpS.FromKpH(GetIntParameter("General", "Sobrevelocidad", 500));
            
            foreach(InteractiveTCS i in tcs)
            {
                i.Activated = true;
                i.Initialize();
                parameters.UnionWith(i.GetParameters());
            }
            
            Parameter p = null;
            p = new Parameter("speed");
            p.GetValue = () => MpS.ToKpH(SpeedMpS()).ToString().Replace(',','.');
            parameters.Add(p);
            
            p = new Parameter("distance");
            p.GetValue = () => DistanceM().ToString().Replace(',','.');
            parameters.Add(p);
            
            p = new Parameter("cruise_speed");
            p.SetValue = (string val) => cruise_speed=MpS.FromKpH(float.Parse(val.Replace('.',',')));
            parameters.Add(p);
            
            p = new Parameter("train_length");
            p.GetValue = () => TrainLengthM().ToString().Replace(',','.');
            parameters.Add(p);
            
            p = new Parameter("controller::throttle");
            p.SetValue = (string val) => {
                float value = float.Parse(val.Replace('.',','));
                userThrottle = value;
            };
            parameters.Add(p);
            
            p = new Parameter("controller::brake::dynamic");
            p.SetValue = (string val) => {
                float value = float.Parse(val.Replace('.',','));
                userDynamic = value;
            };
            parameters.Add(p);
            
            p = new Parameter("controller::direction");
            p.SetValue = (string val) =>
            {
                if(val=="1") direction = Direction.Forward;
                else if(val=="-1") direction = Direction.Reverse;
                else direction = Direction.N;
            };
            parameters.Add(p);
            
            p = new Parameter("controller::headlight");
            p.SetValue = (string val) => 
            {
                if(val=="3") SignalEvent(Event._HeadlightOn);
                else if(val=="2") SignalEvent(Event._HeadlightDim);
                else SignalEvent(Event._HeadlightOff);
            };
            parameters.Add(p);
            
            p = new Parameter("controller::wipers");
             p.SetValue = (string val) => 
            {
                if(val=="3"||val=="1") SignalEvent(Event.WiperOn);
                else SignalEvent(Event.WiperOff);
            };
            parameters.Add(p);
            
            p = new Parameter("controller::sander");
            p.SetValue = (string val) => 
            {
                if(val=="1" || val == "true") SignalEventToTrain(Event.SanderOn);
                else SignalEventToTrain(Event.SanderOff);
            };
            parameters.Add(p);
            
            p = new Parameter("controller::horn");
            p.SetValue = (string val) => 
            {
                if(val=="1" || val == "true") SetHorn(true);
                else SetHorn(false);
            };
            parameters.Add(p);
            
            p = new Parameter("controller::bell");
            p.SetValue = (string val) => setKey(0x42, val != "1" && val != "true");
            parameters.Add(p);
            
            p = new Parameter("simulator_time");
            p.GetValue = () => Locomotive().Simulator.ClockTime.ToString().Replace(',','.');
            parameters.Add(p);
        }
        float prevDynamic=0;
        float prevThrottle=0;
        float userThrottle=0;
        void setThrottle(float thr)
        {
            if(prevThrottle==0 && thr>0) VK = 0xBC;
            if(prevThrottle!=thr && thr == 0) SetThrottleController(0);
            prevThrottle = thr;
        }
        float userDynamic=0;
        void setDynamicBrake(float dyn)
        {
            if(prevDynamic==0 && dyn>0) VK = 0xBE;
            if(prevDynamic!=dyn && dyn == 0) SetDynamicBrakeController(0);
            prevDynamic = dyn;
        }
        float ATFval=0;
        bool ServerAvailable = true;
        public override void Update()
        {
            if(!IsTrainControlEnabled())
            {
                if (c!=null)
                {
                    c.Stop();
                    c = null;
                }
                return;
            }

            foreach(InteractiveTCS i in tcs)
            {
                i.Update();
            }
            if(c==null && ServerAvailable)
            {
                try{
                    TcpClient cl = new TcpClient();
                    cl.Connect("127.0.0.1", 5090);
                    c = new TCPClient(cl);
                } catch(Exception e)
                {
                    ServerAvailable = false;
                    c = null;
                }
                c.WriteLine("register(controller::throttle)");
                c.WriteLine("register(cruise_speed)");
                c.WriteLine("register(controller::brake::dynamic)");
                c.WriteLine("register(controller::direction)");
                c.WriteLine("register(controller::horn)");
                c.WriteLine("register(controller::bell)");
                c.WriteLine("register(controller::wipers)");
                c.WriteLine("register(controller::sander)");
                c.WriteLine("register(controller::headlight)");
                c.WriteLine("register(hm::pressed)");
                c.WriteLine("register(+::emergency)");
                c.WriteLine("register(+::fullbrake)");
                foreach(InteractiveTCS i in tcs)
                {
                    if (i is ASFADigital)
                    {
                        (i as ASFADigital).Conex();
                    }
                }
            }
            if (c != null)
            {
                String s = c.ReadLine();
                while(s!=null)
                {
                    if(s.StartsWith("register(") || s.StartsWith("get("))
                    {
                        int div = s.IndexOf('(');
                        int fin = s.LastIndexOf(')');
                        if(div<=0 || fin<=0)
                        {
                            s = c.ReadLine();
                            continue;
                        }
                        string fun = s.Substring(0, div);
                        string param = s.Substring(div+1, fin-div-1);
                        foreach (Parameter p in parameters)
                        {
                            if(p!=null && p.GetValue!=null && p.Matches(param))
                            {
                                if(fun == "register")
                                {
                                    Register r = new DiscreteRegister(false);
                                    p.registers[r] = c;
                                }
                                else c.WriteLine(p.name + '=' + p.GetValue());
                            }
                        }
                    }
                    else if(s.Contains('='))
                    {
                        int pos = s.IndexOf('=');
                        string param = s.Substring(0, pos);
                        string val = s.Substring(pos+1);
                        if(param != "connected") 
                        {
                            Parameter ps = GetParameter(param);
                            if(ps != null && ps.SetValue!=null) ps.SetValue(val);
                        }
                    }
                    s = c.ReadLine();
                }
                foreach(Parameter p in parameters)
                {
                    p.Send();
                }
            }
            
            bool Emergency = false;
            bool FullBrake = false;
            foreach(InteractiveTCS i in tcs)
            {
                if(i.Emergency) Emergency = true;
                if(i.FullBrake) FullBrake = true;
            }
            if(cruise_speed>MpS.FromKpH(15) && !IsDirectionNeutral() && userDynamic == 0)
            {
                ATFon = true;
                ATF(cruise_speed, ref ATFval);
                if (userThrottle==0) setThrottle(Math.Max(0,ATFval));
                else setThrottle(Math.Max(0,Math.Min(ATFval,userThrottle)));
                setDynamicBrake(Math.Max(-ATFval,0));
            }
            else
            {
                ATFval = 0;
                ATFon = false;
                setThrottle(userThrottle);
                setDynamicBrake(userDynamic);
            }
            SetDirection();
            if(VK==0)
            {
                if(prevThrottle!=0) SetThrottleController(prevThrottle);
                if(prevDynamic!=0) SetDynamicBrakeController(prevDynamic);
            }
            else
            {
                SetThrottleController(0);
                SetDynamicBrakeController(0);
            }
            pressKey();
            SetEmergencyBrake(Emergency/*||IsDirectionNeutral()*/);
            SetFullBrake(FullBrake);
            SetTractionAuthorization(!DoesBrakeCutPower() || BrakeCutsPowerAtBrakeCylinderPressureBar() > LocomotiveBrakeCylinderPressureBar());
            SetOverspeedWarningDisplay(SpeedMpS()>LocomotiveOverspeedMpS);
        }
        Direction direction=Direction.N;
        [DllImport("user32.dll")]
        public static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, uint dwExtraInfo);
        [DllImport("user32.dll")]
        public static extern int SetForegroundWindow(IntPtr hWnd);
        int VK=0;
        bool vkpressed=false;
        void pressKey()
        {
            if(VK!=0)
            {
                setKey(VK, vkpressed);
                if(vkpressed)
                { 
                    VK = 0;
                    vkpressed = false;
                }
                else vkpressed = true;
            }
        }
        Process p=null;
        void setKey(int VK, bool release)
        {
            if(p==null)
            {
                var pr = Process.GetProcessesByName("RunActivity");
                p = pr[0];
            }
            SetForegroundWindow(p.MainWindowHandle);
            keybd_event((byte)VK, 0, release ? (uint)0x0002 : (uint)0, 0);
        }
        Direction prevDirection;
        void SetDirection()
        {
            if(direction==prevDirection && CurrentDirection()!=prevDirection)
            {
                direction = CurrentDirection();
            }
            if(direction!=CurrentDirection()&&VK==0)
            {
                string key;
                if(direction==Direction.Forward||(direction==Direction.N && IsDirectionReverse()))
                {
                    key = "W";
                    VK = 0x57;
                }
                else
                {
                    key = "S";
                    VK = 0x53;
                }
            }
            prevDirection = CurrentDirection();
        }
        public override void SetEmergency(bool emergency){}
        public override void HandleEvent(TCSEvent evt, string message) 
        {
            foreach(InteractiveTCS e in tcs)
            {
                e.HandleEvent(evt, message);
            }
        }
        bool ATFon = false;
        float cruise_speed=0;
        double LastTime=0;
        double LastError=0;
        double i_error=0;
        float ATF_brake=0;
        double p_coef = 2;
        double i_coef = 0.001;
        double d_coef = 0.4;
        protected void ATF(double limit, ref float value)
        {
            if(VK!=0) return;
            limit = limit - MpS.FromKpH(1);
            double error = limit-SpeedMpS();
            double dt = ClockTime()-LastTime;
            if (dt < 0.0001f) return;
            if(Math.Abs(error)<1)
            {
                i_error += (error+LastError)*dt/2;
            }
            else i_error = 0;
            double d_error = (error-LastError)/dt;
            double p_out = p_coef*error;
            double i_out = i_coef*i_error;
            double d_out = d_coef*d_error;
            double diff = d_out+p_out+i_out;
            value = Math.Max(Math.Min((float)diff,1),-1);
            LastTime = ClockTime();
            LastError = error;
        }
    }
    public abstract class Client
    {
        public virtual void Start()
        {
            WriteLine("connected=true");
        }
        public abstract void WriteLine(string s);
        public abstract string ReadLine();
        public abstract void Stop();
    }
    public class TCPClient : Client
    {
        TcpClient client;
        NetworkStream stream;
        string buff;
        public TCPClient(TcpClient c)
        {
            client = c;
            buff = "";
            stream = client.GetStream();
        }
        public override void Start()
        {
            base.Start();
        }
        public override void Stop()
        {
            client.Close();
        }
        public override void WriteLine(string s)
        {
            byte[] b = System.Text.Encoding.UTF8.GetBytes(s + '\n');
            stream.Write(b, 0, b.Length);
        }
        public override string ReadLine()
        {
            while (stream.DataAvailable)
            {
                buff += (char)stream.ReadByte();
            }
            int ind = buff.IndexOf('\n');
            if (ind >= 0)
            {
                string res = buff.Substring(0, ind);
                buff = buff.Substring(ind + 1);
                return res;
            }
            return null;
        }
    }
    public abstract class Register
    {
        public Func<string, bool> HasToSend;
        public Action<string> Sent;
    }
    public class DiscreteRegister : Register
    {
        string prev = "";
        public DiscreteRegister(bool repeat)
        {
            HasToSend = (string val) => repeat || val != prev;
            Sent = (string val) => prev = val;
        }
    }
    public class NumericRegister : Register
    {
        float prev = 0;
        public NumericRegister(float margin)
        {
            HasToSend = (string val) => Math.Abs(float.Parse(val.Replace('.', ',')) - prev) > margin;
            Sent = (string val) => prev = float.Parse(val.Replace('.', ','));
        }
    }
    public class Parameter
    {
        public Dictionary<Register, Client> registers;
        public readonly string name;
        public Parameter(string name)
        {
            registers = new Dictionary<Register, Client>();
            this.name = name;
        }
        public Func<string> GetValue;
        public Action<string> SetValue;
        public void Send()
        {
            if(GetValue == null) return;
            string val = GetValue();
            if (val == null) return;
            foreach (Register r in registers.Keys)
            {
                if (r.HasToSend(val))
                {
                    registers[r].WriteLine(name + '=' + val);
                    if(r.Sent!=null) r.Sent(val);
                }
            }
        }
        public bool Matches(string topic)
        {
            string[] t1 = topic.Split(new string[]{"::"}, StringSplitOptions.None);
            string[] t2 = name.Split(new string[]{"::"}, StringSplitOptions.None);
            for (int i=0; i<t1.Length && i<t2.Length; i++)
            {
                if (t1[i] == "*")
                    return true;
                if (t1[i] != "+" && t1[i] != t2[i])
                    return false;
            }
            return t1.Length == t2.Length;
        }
        public override bool Equals(object obj)
        {
            if (obj is Parameter) return name.Equals((obj as Parameter).name);
            return name.Equals(obj);
        }
        public override int GetHashCode()
        {
            return name.GetHashCode();
        }
    }
    public abstract class InteractiveTCS : TrainControlSystem
    {
        public ServerTCS tcs;
        public bool Emergency = false;
        public bool FullBrake = false;
        public abstract List<Parameter> GetParameters();
        public InteractiveTCS(ServerTCS tcs)
        {
            this.tcs = tcs;
        }
    }
    public class HM : InteractiveTCS
    {
        float HMReleasedAlertDelayS;
        float HMReleasedEmergencyDelayS;
        float HMPressedAlertDelayS;
        float HMPressedEmergencyDelayS;

        public bool Pressed = false;
        Timer HMPressedAlertTimer;
        Timer HMPressedEmergencyTimer;
        Timer HMReleasedAlertTimer;
        Timer HMReleasedEmergencyTimer;

        public HM(ServerTCS tcs) : base(tcs)
        {
            SetVigilanceAlarm = (value) => { if (Activated) tcs.SetVigilanceAlarm(value); };
            SetVigilanceAlarmDisplay = (value) => { if (Activated) tcs.SetVigilanceAlarmDisplay(value); };
            SetVigilanceEmergencyDisplay = (value) => { if (Activated) tcs.SetVigilanceAlarm(value); };
        }
        public override void SetEmergency(bool emergency)
        {
            throw new NotImplementedException();
        }
        bool InvertResetButton = false;
        bool ResetAtStandstill = false;
        bool ResetWhenPressed = false;
        public override void HandleEvent(TCSEvent evt, string message)
        {
            switch(evt)
            {
                case TCSEvent.AlerterPressed:
                    Pressed = !InvertResetButton;
                    break;
                case TCSEvent.AlerterReleased:
                    Pressed = InvertResetButton;
                    break;
            }
        }
        public override void Initialize()
        {
            InvertResetButton = tcs.GetBoolParameter("HM","InvertirBoton",true);
            HMReleasedAlertDelayS = tcs.GetFloatParameter("HM","AvisoLevantado",2.5f);
            HMReleasedEmergencyDelayS = tcs.GetFloatParameter("HM","UrgenciaLevantado", 5);
            HMPressedAlertDelayS = tcs.GetFloatParameter("HM","AvisoPisado", 32.5f);
            HMPressedEmergencyDelayS = tcs.GetFloatParameter("HM","UrgenciaPisado", 35);
            ResetWhenPressed = tcs.GetBoolParameter("HM","RearmarAlPulsar", false);
            ResetAtStandstill = tcs.GetBoolParameter("HM","RearmarEnParado", false);
            HMPressedAlertTimer = new Timer(tcs);
            HMPressedAlertTimer.Setup(HMPressedAlertDelayS);
            HMPressedEmergencyTimer = new Timer(tcs);
            HMPressedEmergencyTimer.Setup(HMPressedEmergencyDelayS);
            HMReleasedAlertTimer = new Timer(tcs);
            HMReleasedAlertTimer.Setup(HMReleasedAlertDelayS);
            HMReleasedEmergencyTimer = new Timer(tcs);
            HMReleasedEmergencyTimer.Setup(HMReleasedEmergencyDelayS);
        }
        public override void Update()
        {
            if (!Activated || !tcs.IsAlerterEnabled() || tcs.IsDirectionNeutral())
            {
                HMReleasedAlertTimer.Stop();
                HMReleasedEmergencyTimer.Stop();
                HMPressedAlertTimer.Stop();
                HMPressedEmergencyTimer.Stop();
                if (Emergency)
                {
                    Emergency = false;
                    SetVigilanceEmergencyDisplay(false);
                }
                if (tcs.AlerterSound()) SetVigilanceAlarm(false);
                SetVigilanceAlarmDisplay(false);
                tcs.SetCabDisplayControl(31, 0);
                return;
            }
            tcs.SetCabDisplayControl(31, 1);
            if (Pressed && (!HMPressedAlertTimer.Started || !HMPressedEmergencyTimer.Started))
            {
                HMReleasedAlertTimer.Stop();
                HMReleasedEmergencyTimer.Stop();
                HMPressedAlertTimer.Start();
                HMPressedEmergencyTimer.Start();
                if (tcs.AlerterSound()) SetVigilanceAlarm(false);
                SetVigilanceAlarmDisplay(false);
                if (Emergency && ResetWhenPressed)
                {
                    Emergency = false;
                    SetVigilanceEmergencyDisplay(false);
                }
            }
            if (Pressed && HMPressedAlertTimer.RemainingValue < 2.5f)
            {
                SetVigilanceAlarmDisplay(true);
            }
            if (!Pressed && (!HMReleasedAlertTimer.Started || !HMReleasedEmergencyTimer.Started))
            {
                HMReleasedAlertTimer.Start();
                HMReleasedEmergencyTimer.Start();
                HMPressedAlertTimer.Stop();
                HMPressedEmergencyTimer.Stop();
                if (tcs.AlerterSound()) SetVigilanceAlarm(false);
                SetVigilanceAlarmDisplay(true);
                if (Emergency && ResetWhenPressed)
                {
                    Emergency = false;
                    SetVigilanceEmergencyDisplay(false);
                }
            }
            if (HMReleasedAlertTimer.Triggered || HMPressedAlertTimer.Triggered)
            {
                if (!tcs.AlerterSound()) SetVigilanceAlarm(true);
                SetVigilanceAlarmDisplay(true);
            }
            else
            {
                if (tcs.AlerterSound()) SetVigilanceAlarm(false);
            }
            if (!Emergency && (HMPressedEmergencyTimer.Triggered || HMReleasedEmergencyTimer.Triggered))
            {
                Emergency = true;
                if (tcs.AlerterSound()) SetVigilanceAlarm(false);
                SetVigilanceAlarmDisplay(false);
                SetVigilanceEmergencyDisplay(true);
            }
            if (Emergency && tcs.SpeedMpS() < 1.5f && ResetAtStandstill)
            {
                Emergency = false;
                SetVigilanceEmergencyDisplay(false);
            }
        }
        public override List<Parameter> GetParameters()
        {
            List<Parameter> l = new List<Parameter>();
            
            Parameter p;
            
            p = new Parameter("hm::pressed");
            p.SetValue = (string val) => {Pressed = val=="1" || val=="true";};
            l.Add(p);
            
            return l;
        }
    }
    public class ETCS : InteractiveTCS
    {
        public ETCS(ServerTCS tcs) : base(tcs)
        {
        }
        public override void SetEmergency(bool emergency)
        {
            throw new NotImplementedException();
        }
        public override void HandleEvent(TCSEvent evt, string message)
        {
        }
        public override void Initialize()
        {
        }
        public override void Update()
        {
            UpdateSignalPassed();
            UpdateInfillPassed();
            if(SignalPassed)
            {
                string ma="00001100"+"01";
                float maend=0;
                if(PreviousSignalAspect==Aspect.Stop)
                {
                    ma += "0000001011111"+"01";
                    ma += "0000000"+"0000000"+"0000000000"+"00000";
                    ma += "000000000000000"+"0"+"0"+"1"+"000000011001000"+"1111111"+"0";
                }
                else if(PreviousSignalAspect==Aspect.Clear_1 || PreviousSignalAspect==Aspect.Clear_2 || PreviousSignalAspect==Aspect.Approach_2)
                {
                    ma += "0000001011111"+"01";
                    ma += "0101000"+"0000000"+"0000000000"+"00000";
                    ma += format_etcs_distance(tcs.NextSignalDistanceM(1))+"0"+"0"+"1"+"000000011001000"+"1111111"+"0";
                }
                else
                {
                    ma += "0000001011111"+"01";
                    ma += "0101000"+"0000000"+"0000000000"+"00000";
                    ma += format_etcs_distance(tcs.NextSignalDistanceM(0))+"0"+"0"+"1"+"000000011001000"+"1111111"+"0";
                }
                string ssp="00011011"+"01"+"0000000000000"+"01";
                ssp += "000000000000000"+format_etcs_speed(tcs.CurrentPostSpeedLimitMpS())+"0"+"00000";
                ssp += "00000";
                int niter=0;
                float sspend = tcs.NextSignalDistanceM(1)+1000;
                float dist=0;
                for(int i=0; i<10; i++)
                {
                    float prevdist = dist;
                    dist = tcs.NextPostDistanceM(i);
                    if (dist > sspend || dist==prevdist)
                        break;
                    ssp+=format_etcs_distance(dist-prevdist)+format_etcs_speed(tcs.NextPostSpeedLimitMpS(i))+"0"+"00000";
                    dist=tcs.NextPostDistanceM(i);
                    niter=i+1;
                }
                niter++;
                ssp += format_etcs_distance(sspend)+"1111111"+"0"+"00000";
                string sspsize = Convert.ToString(ssp.Length,2);
                int i0 = 23-sspsize.Length;
                char[] ssparr = ssp.ToCharArray();
                for (int i=i0; i<23; i++)
                {
                    ssparr[i] = sspsize[i-i0];
                }
                string n_iter = Convert.ToString(niter,2);
                i0 = 58-n_iter.Length;
                for (int i=i0; i<58; i++)
                {
                    ssparr[i] = n_iter[i-i0];
                }
                ssp = new string(ssparr);
                string grad="00010101"+"01"+"0000001001110"+"10";
                grad += "000000000000000"+"0"+"00000000";
                grad += "00001";
                grad += "111111111111111"+"0"+"11111111";
                string link="00000101"+"01"+"0000001000101"+"01"+format_etcs_distance(tcs.NextSignalDistanceM(0))+"0"+"00000000000001"+"1"+"00"+"001100"+"00000";
                string tel1 = "1"+"0100001"+"0"+"000"+"010"+"00"+"11111111"+"0000000000"+"00000000000001"+"1";
                tel1 += grad + ssp + "11111111";
                string tel2 = "1"+"0100001"+"0"+"001"+"010"+"00"+"11111111"+"0000000000"+"00000000000001"+"1";
                tel2 += ma + "11111111";
                string tel3 = "1"+"0100001"+"0"+"010"+"010"+"00"+"11111111"+"0000000000"+"00000000000001"+"1";
                tel3 += link + "11111111";
                System.Console.WriteLine(tel1);
                tcs.SendParameter("etcs::telegram",tel1);
                tcs.SendParameter("etcs::telegram",tel2);
                tcs.SendParameter("etcs::telegram",tel3);
            }
            if(InfillPassed)
            {
                /*string val;
                int num=1;
                if(PreviousSignalAspect==Aspect.Clear_1 || PreviousSignalAspect==Aspect.Clear_2 || PreviousSignalAspect==Aspect.Approach_2) num=2;
                float start = NextSignalDistanceM(0);
                val = "etcs_ma_infill="+start.ToString().Replace(',','.');
                val += ","+(NextSignalDistanceM(num)-start).ToString().Replace(',','.');
                c.WriteLine(val);*/
            }
        }
        bool SignalPassed = false;
        float PreviousSignalDistanceM = 0;
        Aspect PreviousSignalAspect;
		protected void UpdateSignalPassed()
        {
            SignalPassed = (tcs.NextSignalDistanceM(0) > PreviousSignalDistanceM+20)&&(tcs.SpeedMpS()>0.1f);
            PreviousSignalDistanceM = tcs.NextSignalDistanceM(0);
            if (SignalPassed && tcs.NextSignalAspect(0) == Aspect.None) SignalPassed = false;
            if (!SignalPassed) PreviousSignalAspect = tcs.NextSignalAspect(0);
        }
        bool InfillPassed=false;
        bool InfillReset=false;
        protected void UpdateInfillPassed()
        {
            InfillPassed = false;
            if (tcs.NextSignalDistanceM(0) < 300)
            {
                if (!InfillReset)
                {
                    InfillReset = true;
                    InfillPassed = true;
                }
            }
            if (SignalPassed) InfillReset = false;
        }
        public override List<Parameter> GetParameters()
        {
            List<Parameter> l = new List<Parameter>();
            
            Parameter p;
            
            p = new Parameter("etcs::emergency");
            p.SetValue = (string val) => {Emergency = val!="0" && val!="false";};
            l.Add(p);
            
            p = new Parameter("etcs::fullbrake");
            p.SetValue = (string val) => {FullBrake = val=="1" || val=="true";};
            l.Add(p);

            return l;
        }
        string format_etcs_speed(float speedmps)
        {
            int val = Math.Min((int)Math.Round(speedmps*3.6)/5, 120);
            string spd = Convert.ToString(val,2);
            string speed="";
            for (int i=0; i<7-spd.Length; i++)
            {
                speed += "0";
            }
            speed += spd;
            return speed;
        }
        string format_etcs_distance(float distm)
        {
            int val = (int)Math.Min(distm, 32767);
            string d = Convert.ToString(val,2);
            string dist="";
            for (int i=0; i<15-d.Length; i++)
            {
                dist += "0";
            }
            dist += d;
            return dist;
        }
    }
    public abstract class ASFA : InteractiveTCS
    {
        public enum Freq
        {
            FP,
            L1,
            L2,
            L3,
            L4,
            L5,
            L6,
            L7,
            L8,
            L9,
            L10,
            L11,
            AL
        }
        float LVIstart = 0;
        Freq lvi1 = Freq.L11;
        Freq lvi2 = Freq.L11;
        bool LineaEquipadaComprobado = false;
        bool LineaEquipada = false;
        public ASFA(ServerTCS tcs) : base(tcs)
        {
        }
        public override void Update()
        {
            if(!LineaEquipadaComprobado)
            {
                LineaEquipada = tcs.Locomotive().Train.signalRef.ORTSSignalTypes.IndexOf("ASFA") > 0;
                LineaEquipadaComprobado = true;
            }
            UpdateSignalPassed();
            UpdateDistanciaPrevia();
        }
        Random rnd = new Random();
        
        Freq GetBalizaAspect()
        {
            if (!LineaEquipada)
            {
                for(int i=0; i<4; i++)
                {
                    if(tcs.NextPostDistanceM(i)<=1500 && tcs.NextPostDistanceM(i)>=1495 && tcs.CurrentPostSpeedLimitMpS() - tcs.NextPostSpeedLimitMpS(i)>=MpS.FromKpH(40))
                    {
                        return Freq.L1;
                    }
                }
                float dprevia = tcs.NextSignalDistanceM(0)-PreviaDistance+10;
                switch (tcs.NextSignalAspect(0))
                {
                    case Aspect.Stop:
                    case Aspect.StopAndProceed:
                    case Aspect.Restricted:
                    case Aspect.Permission:
                        return dprevia>0 ? Freq.L7 : Freq.L8;
                    case Aspect.Approach_1:
                    case Aspect.Approach_2:
                    case Aspect.Approach_3:
                        return Freq.L1;
                    case Aspect.Clear_1:
                        return Freq.L2;
                    case Aspect.Clear_2:
                        return Freq.L3;
                    default:
                        return Freq.FP;
                }
            }
            else
            {
                string name = tcs.NextGenericSignalMainHeadSignalType("ASFA");
                if (name == "asfa_baliza_l10")
                    return Freq.L10;
                if (name == "asfa_baliza_l11")
                    return Freq.L11;
                switch(tcs.NextGenericSignalAspect("ASFA"))
                {
                    case Aspect.Permission:
                    case Aspect.Stop:
                        return Freq.L8;
                    case Aspect.StopAndProceed:
                        return Freq.L7;
                    case Aspect.Restricted:
                        return Freq.L4;
                    case Aspect.Approach_1:
                        return Freq.L1;
                    case Aspect.Approach_2:
                        return Freq.L5;
                    case Aspect.Approach_3:
                        return Freq.L6;
                    case Aspect.Clear_1:
                        return Freq.L2;
                    case Aspect.Clear_2:
                        return Freq.L3;
                    default:
                        return Freq.FP;
                }
            }
        }
        float GetBalizaDistance()
        {
            if (!LineaEquipada)
            {
                for(int i=0; i<4; i++)
                {
                    if(tcs.NextPostDistanceM(i)<=1500 && tcs.NextPostDistanceM(i)>=1495 && tcs.CurrentPostSpeedLimitMpS() - tcs.NextPostSpeedLimitMpS(i)>=MpS.FromKpH(40))
                    {
                        return tcs.NextPostDistanceM(i)-1495;
                    }
                }
                float dprevia = tcs.NextSignalDistanceM(0)-PreviaDistance+10;
                if (dprevia>0)
                    return dprevia;
                
                return tcs.NextSignalDistanceM(0);
                /*if(LVIstart==0)
                {
                    for(int i=0; i<4; i++)
                    {
                        if(tcs.NextPostDistanceM(i)>1500 || tcs.NextPostDistanceM(i)<1495) continue;
                        if(tcs.CurrentPostSpeedLimitMpS()-tcs.NextPostSpeedLimitMpS(i)>=MpS.FromKpH(40))
                        {
                            LVIstart = tcs.DistanceM();
                            float speed = MpS.ToKpH(tcs.NextPostSpeedLimitMpS(i));
                            if (speed < 50) lvi1 = lvi2 = Freq.L11;
                            else if (speed < 80)
                            {
                                lvi1 = Freq.L11;
                                lvi2 = Freq.L10;
                            }
                            else if (speed < 120)
                            {
                                lvi1 = Freq.L10;
                                lvi2 = Freq.L11;
                            }
                            else lvi1 = lvi2 = Freq.L10;
                        }
                    }
                }*/
            }
            else
            {
                return tcs.NextGenericSignalDistanceM("ASFA");
            }
        }
        
        float fail;
        float fail_odometer;
        
        Freq prevBalizaAspect = Freq.FP;
        float prevBalizaDistance;
        public Freq Baliza()
        {
            int random = 1;
            int random_max = 1000;
            if (random == 2) random_max = 500;
            else if (random == 3) random_max = 100;
                
            if (random > 0 && tcs.DistanceM()-fail_odometer > 1000) {
                if (rnd.Next(1,random_max) == 5)
                    fail = tcs.ClockTime();
                fail_odometer = tcs.DistanceM();
            }
            if (tcs.ClockTime()-fail<0.5f)
                return Freq.AL;
            
            float dist = GetBalizaDistance();
            if (prevBalizaAspect != Freq.FP && prevBalizaDistance + 3 < dist)
            {
                Freq f = prevBalizaAspect;
                prevBalizaAspect = Freq.FP;
                return f;
            }
            if (dist<5)
            {
                prevBalizaDistance = dist;
                prevBalizaAspect = GetBalizaAspect();
            }
            else
            {
                prevBalizaAspect = Freq.FP;
            }
            if (dist<0.3)
            {
                return prevBalizaAspect;
            }
            return Freq.FP;
        }
        public override List<Parameter> GetParameters()
        {
            List<Parameter> l = new List<Parameter>();
            
            Parameter p;
            
            p = new Parameter("asfa::emergency");
            p.SetValue = (string val) => {Emergency = val!="0" && val!="false";};
            l.Add(p);
            
            p = new Parameter("asfa::frecuencia");
            p.GetValue = () => Baliza().ToString();
            l.Add(p);
            
            return l;
        }
        bool SignalPassed = false;
        float PreviousSignalDistanceM = 0;
        bool PreviaPassed = false;
        bool LineaConvencional = true;
        float PreviaDistance = 300;
        float AnuncioDistance = 1500f;
        protected void UpdateSignalPassed()
        {
            SignalPassed = (tcs.NextSignalDistanceM(0) > PreviousSignalDistanceM+20)&&(tcs.SpeedMpS()>0.1f);
            PreviousSignalDistanceM = tcs.NextSignalDistanceM(0);
            if (SignalPassed && tcs.NextSignalAspect(0) == Aspect.None) SignalPassed = false;
        }
        protected void UpdateDistanciaPrevia()
        {
            if (SignalPassed)
            {
                if ((tcs.NextSignalAspect(0) == Aspect.Clear_2 && tcs.NextSignalSpeedLimitMpS(0) < MpS.FromKpH(165f) && tcs.NextSignalSpeedLimitMpS(0) > MpS.FromKpH(155f)) || (tcs.NextSignalAspect(0) == Aspect.Approach_1 && tcs.NextSignalSpeedLimitMpS(0) < MpS.FromKpH(35f) && tcs.NextSignalSpeedLimitMpS(0) > MpS.FromKpH(25f)))
                {
                    PreviaDistance = 0;
                }
                else
                {
                    if (LineaConvencional)
                    {
                        if (tcs.NextSignalDistanceM(0) < 100f)
                        {
                            PreviaDistance = 0f;
                        }
                        else if (tcs.NextSignalDistanceM(0) < 400f)
                        {
                            PreviaDistance = 50f;
                        }
                        else if (tcs.NextSignalDistanceM(0) < 700f)
                        {
                            PreviaDistance = 100f;
                        }
                        else
                        {
                            PreviaDistance = 300f;
                        }
                    }
                    else
                    {
                        if (tcs.NextSignalDistanceM(0) < 100f)
                        {
                            PreviaDistance = 0f;
                        }
                        else if (tcs.NextSignalDistanceM(0) < 700f)
                        {
                            PreviaDistance = 100f;
                        }
                        else if (tcs.NextSignalDistanceM(0) < 1000f)
                        {
                            PreviaDistance = 300f;
                        }
                        else
                        {
                            PreviaDistance = 500f;
                        }
                    }
                }
            }
        }
    }
    class ASFAclasico : ASFA
    {
        bool Encendido = false;
        bool Urgencia = false;
        bool RebaseAuto = false;
        bool Eficacia = false;
        bool ASFA200 = true;
        bool RecL2;
        bool Connected = false;
        int TipoTren;
        ulong RECStarted = 0;
        ulong RojoStarted = 0;
        ulong AlarmaStarted = 0;
        ulong RebaseStarted = 0;
        ulong CondStarted = 0;
        int Velocidad = 0;
        ulong Previous;
        ulong LastPConex;
        ulong BuzzEnd = 0;
        ulong poweroff = 0;
        
        const int PConex = 2;
        const int PREC = 3;
        const int PAlarma = 4;
        const int PRearme = 5;
        const int PRebase = 6;
        const int LuzFrenar = 12;
        const int LuzL2 = 13;
        const int LuzRojo = 14;
        const int LuzVL = 15;
        const int LuzCV = 16;
        const int LuzEficacia = 17;
        const int LuzREC = 18;
        const int LuzAlarma = 19;
        const int LuzRearme = 20;
        const int LuzRebase = 21;
        
        Freq prev_freq;
        Freq freq;
        
        void buzz(ulong time)
        {
            if (time == 500) tcs.TriggerSoundInfo1();
            else tcs.TriggerSoundPenalty1();
            BuzzEnd = millis() + time;
        }
        void nobuzz()
        {
            tcs.TriggerSoundPenalty2();
        }
        ulong millis()
        {
            return (ulong)(tcs.ClockTime()*1000);
        }
        int HIGH = 1;
        int LOW = 0;
        int[] estados_luces = new int[12];
        int[] estados_botones = new int[12];
        void digitalWrite(int pin, int value)
        {
            estados_luces[pin-12] = value;
            tcs.SetCabDisplayControl(pin, value);
            if(estados_luces[LuzRojo-12]==1) tcs.SetNextSignalAspect(Aspect.Stop);
            else if(estados_luces[LuzFrenar-12]==1) tcs.SetNextSignalAspect(Aspect.Approach_1);
            else if(estados_luces[LuzVL-12]==1) tcs.SetNextSignalAspect(Aspect.Clear_1);
            else tcs.SetNextSignalAspect(Aspect.Clear_2);
        }
        int digitalRead(int pin)
        {
            return 1-estados_botones[pin];
        }
        public ASFAclasico(ServerTCS tcs) : base(tcs)
        {
        }
        public override void Initialize()
        {
            estados_botones[PConex] = 1;
            tcs.SetCustomizedTCSControlString("Genérico ASFA 1");
            tcs.SetCustomizedTCSControlString("Genérico ASFA 2");
            tcs.SetCustomizedTCSControlString("Conexión ASFA");
            tcs.SetCustomizedTCSControlString("REC ASFA");
            tcs.SetCustomizedTCSControlString("Alarma ASFA");
            tcs.SetCustomizedTCSControlString("Rearme ASFA");
            tcs.SetCustomizedTCSControlString("Rebase ASFA");
        }
        public override void Update()
        {
            base.Update();
            Velocidad = (int)MpS.ToKpH(tcs.SpeedMpS());
            freq = Baliza();
            if(digitalRead(PConex)==LOW && !Encendido) start();
            if(digitalRead(PConex)==HIGH&&digitalRead(PRebase)==HIGH)
            {
                if(Encendido && poweroff == 0) poweroff = millis();
            }
            else poweroff = 0;
            if(poweroff != 0 && poweroff+2000<millis()) shutdown();
            if(Encendido)
            {
                if(RebaseStarted==0&&digitalRead(PRebase)==LOW)
                {
                    RebaseStarted = millis();
                    RebaseAuto = true;
                    digitalWrite(LuzRebase, HIGH);
                }
                if(digitalRead(PRebase)==HIGH)
                {
                    RebaseStarted = 0;
                    digitalWrite(LuzRebase, LOW);
                }
                if(RebaseStarted+10000<millis())
                {
                    RebaseAuto = false;
                    digitalWrite(LuzRebase, LOW);
                }
                if(BuzzEnd!=0 && BuzzEnd<millis())
                {
                    BuzzEnd = 0;
                    nobuzz();
                }
                if(prev_freq!=freq)
                {
                    if(ASFA200 && freq != Freq.FP)
                    {
                        CondStarted = 0;
                        RecL2 = false;
                    }
                    switch(freq)
                    {
                        case Freq.L1:
                            buzz(3000);
                            RECStarted = millis();
                            break;
                        case Freq.L2:
                            if(ASFA200)
                            {
                                buzz(3000);
                                CondStarted = millis();
                                digitalWrite(LuzREC, HIGH);
                            }
                            else buzz(500);
                            break;
                        case Freq.L3:
                            buzz(500);
                            break;
                        case Freq.L7:
                        {
                            int Vmax = 60;
                            if(TipoTren == 110) Vmax = 60;
                            if(TipoTren == 90) Vmax = 50;
                            if(TipoTren == 70) Vmax = 35;
                            if(Velocidad>Vmax)
                            {
                            Urgencia = true;
                            buzz(5000);
                            digitalWrite(LuzRojo, HIGH);
                            }
                            else
                            {
                            buzz(3000);
                            RojoStarted = millis();
                            }
                        }
                        break;
                        case Freq.L8:
                            if(!RebaseAuto)
                            {
                                Urgencia = true;
                                buzz(5000);
                                digitalWrite(LuzRojo, HIGH);
                            }
                            else
                            {
                                buzz(3000);
                                RojoStarted = millis();
                            }
                            break;
                        case Freq.FP:
                            break;
                        default:
                            Eficacia = false;
                            if(AlarmaStarted==0)
                            {
                                buzz(3000);
                                AlarmaStarted = millis();
                                digitalWrite(LuzAlarma, HIGH);
                            }
                            break;
                    }
                    prev_freq = freq;
                }
                Eficacia = freq==Freq.FP;  
                digitalWrite(LuzEficacia, Eficacia ? 1 : 0);
                if(AlarmaStarted!=0)
                {
                    if(digitalRead(PAlarma)==LOW&&Eficacia)
                    {
                        nobuzz();
                        AlarmaStarted = 0;
                        digitalWrite(LuzAlarma, LOW);
                    }
                    else if(AlarmaStarted+3000<millis()) Urgencia = true;
                }
                if(RECStarted != 0)
                {
                    digitalWrite(LuzREC, HIGH);
                    digitalWrite(LuzFrenar, HIGH);
                    if(Velocidad>160 && ASFA200) Urgencia = true;
                    if(digitalRead(PREC)==LOW)
                    {
                        nobuzz();
                        digitalWrite(LuzREC, LOW);
                        digitalWrite(LuzFrenar, LOW);
                        RECStarted = 0;
                    }
                    else if(RECStarted+3000<millis())
                    {
                        Urgencia = true;
                        digitalWrite(LuzREC, LOW);
                        digitalWrite(LuzFrenar, LOW);
                        RECStarted = 0;
                    }
                }
                if(RojoStarted!=0)
                {
                    digitalWrite(LuzRojo, HIGH);
                    if(RojoStarted+10000<millis())
                    {
                        digitalWrite(LuzRojo, LOW);
                        RojoStarted = 0;
                    }
                }
                if(CondStarted!=0)
                {
                    if(digitalRead(PREC)==LOW && !RecL2)
                    {
                        nobuzz();
                        RecL2 = true;
                        digitalWrite(LuzREC, LOW);
                    }
                    if(!RecL2 && CondStarted + 3000 < millis())
                    {
                        Urgencia = true;
                        digitalWrite(LuzREC, LOW);
                    }
                    if(Velocidad>180 && CondStarted + 18000 < millis()) Urgencia = true;
                    if(Velocidad>160 && CondStarted + 30000 < millis()) Urgencia = true;
                    digitalWrite(LuzL2, (int)((millis() - CondStarted) / 500 % 2));
                }
                else digitalWrite(LuzL2, LOW);
                if(Urgencia&&Velocidad<5)
                { 
                    digitalWrite(LuzRojo, LOW);
                    if(AlarmaStarted==0)
                    {
                        digitalWrite(LuzRearme, HIGH);
                        if(digitalRead(PRearme)==LOW) Urgencia = false;
                    }
                }
                else digitalWrite(LuzRearme, LOW);
            }
            Emergency = Urgencia;
            Previous = millis();
        }
        void start()
        {   
            //Urgencia = false;
            Encendido = true;
            buzz(500);
            LastPConex = millis();
        }
        void shutdown()
        {
            freq = Freq.FP;
            RECStarted = RojoStarted = AlarmaStarted = RebaseStarted = CondStarted = 0;
            //Urgencia = false;
            Eficacia = false;
            nobuzz();
            digitalWrite(LuzREC, LOW);
            digitalWrite(LuzFrenar, LOW);
            digitalWrite(LuzRojo, LOW);
            digitalWrite(LuzAlarma, LOW);
            digitalWrite(LuzEficacia, LOW);
            digitalWrite(LuzL2, LOW);
            digitalWrite(LuzRebase, LOW);
            digitalWrite(LuzRearme, LOW);
            digitalWrite(LuzVL, LOW);
            digitalWrite(LuzCV, LOW);
            Encendido = false;
            poweroff = 0;
        }
        double LastPressed;
        int count=0;
        public override void HandleEvent(TCSEvent ev, string message)
        {
            if(ev == TCSEvent.GenericTCSButtonPressed || ev == TCSEvent.GenericTCSButtonReleased)
            {
                int num = int.Parse(message);
                bool pressed = ev == TCSEvent.GenericTCSButtonPressed;
                if (num == 0)
                {
                    estados_botones[PREC] = pressed ? 1 : 0;
                    estados_botones[PRebase] = pressed ? 1 : 0;
                    if (pressed)
                    {
                        if(LastPressed + 1 > tcs.ClockTime()) count++;
                        else count = 1;
                        if (count == 4) estados_botones[PConex] = 1-estados_botones[PConex];
                        LastPressed = tcs.ClockTime();
                    }
                }
                else if (num == 1)
                {
                    estados_botones[PAlarma] = pressed ? 1 : 0;
                    estados_botones[PRearme] = pressed ? 1 : 0;
                }
                else
                {
                    estados_botones[num] = pressed ? 1 : 0;
                }
            }
        }
        public override void SetEmergency(bool emergency) {}
        public override List<Parameter> GetParameters()
        {
            return new List<Parameter>();
        }
    }
    class ASFAclasicoExterno : ASFA
    {
        public ASFAclasicoExterno(ServerTCS tcs) : base(tcs)
        {
        }
        public override void Initialize()
        {
        }
        public override void Update()
        {
            base.Update();
        }
        public override void HandleEvent(TCSEvent ev, string message)
        {

        }
        public override void SetEmergency(bool emergency) {}
        public override List<Parameter> GetParameters()
        {
            return base.GetParameters();
        }
    }
    class ASFADigital : ASFA
    {
        //Combinador general
        public bool Connected;
        public bool FE;
        //Transición a LZB/ERTMS
        public bool AKT = false; //Inhibir freno de urgencia
        public bool CON = true; //Conexión de ASFA
        int UltimaInfo=7;
        bool controldesv=false;
        bool secAA=false;
        int TargetState=0;
        int IndicadorLVI=0;
        int IndicadorPNdesp=0;
        int IndicadorPNprot=0;
        int IndicadorFrenado=0;
        bool Anun = false;
        bool Prec = false;
        bool Prean = false;
        bool Modo = false;
        bool Rearme = false;
        bool Rebase = false;
        bool Aumento = false;
        bool Alarma = false;
        bool Ocultacion = false;
        bool LTV = false;
        bool PN = false;
        bool IlumAnpar=false;
        bool IlumAnpre=false;
        bool IlumPrepar=false;
        bool IlumVLcond=false;
        bool IlumModo=false;
        bool IlumRearme=false;
        bool IlumRebase=false;
        bool IlumAumento=false;
        bool IlumAlarma=false;
        bool IlumOcult=false;
        bool IlumLVI=false;
        bool IlumPN=false;
        public ASFADigital(ServerTCS tcs) : base(tcs)
        {
        }
        public void Conex()
        {
            UltimaInfo=7;
            controldesv=false;
            secAA=false;
            TargetState=0;
            IndicadorLVI=0;
            IndicadorPNdesp=0;
            IndicadorPNprot=0;
            IndicadorFrenado=0;
            Anun = false;
            Prec = false;
            Prean = false;
            Modo = false;
            Rearme = false;
            Rebase = false;
            Aumento = false;
            Alarma = false;
            Ocultacion = false;
            LTV = false;
            PN = false;
            IlumAnpar=false;
            IlumAnpre=false;
            IlumPrepar=false;
            IlumVLcond=false;
            IlumModo=false;
            IlumRearme=false;
            IlumRebase=false;
            IlumAumento=false;
            IlumAlarma=false;
            IlumOcult=false;
            IlumLVI=false;
            IlumPN=false;
            Connected = true;
            tcs.SetCabDisplayControl(11, 1);
            
            tcs.RequestModule("asfa::digital");
            tcs.Register("asfa::indicador::*");
            tcs.Register("asfa::pulsador::ilum::*");
            
            tcs.SendParameter("asfa::pulsador::conex", "1");
            
            tcs.SendParameter("asfa::selector_tipo",tcs.GetIntParameter("ASFA", "TipoTren", -1).ToString());
            string DIV = tcs.GetStringParameter("ASFA", "DIV", "020001162E47558A26023132333402300000C8780000957102690295000288039803E8643C328C00000000000000000000000000000000000000000000000000");
            string hex = "0123456789ABCDEF";
            System.Text.StringBuilder b = new System.Text.StringBuilder(DIV);
            int Vmax = Math.Min(tcs.GetIntParameter("ASFA","VmaxVehiculo",0),200);
            if (Vmax == 0) Vmax = Math.Min(tcs.GetIntParameter("General","TrainMaxSpeed",0),200);
            if (Vmax > 0)
            {
                b[36] = hex[Vmax/16];
                b[37] = hex[Vmax%16];
            }
            int div15 = Convert.ToInt32(DIV.Substring(30,2),16);
            int div17 = Convert.ToInt32(DIV.Substring(34,2),16);
            int modoCONV = tcs.GetIntParameter("ASFA","ModoCONV",-1);
            int modoAV = tcs.GetIntParameter("ASFA","ModoAV",-1);
            int modoRAM = tcs.GetIntParameter("ASFA","ModoRAM",-1);
            int modoBTS = tcs.GetIntParameter("ASFA","ModoBTS",-1);
            if (modoCONV != -1) div15 = (div15&(255-16))|(modoCONV*16);
            if (modoAV != -1) div15 = (div15&(255-32))|(modoAV*32);
            if (modoRAM != -1) div17 = (div17&(255-4))|(modoRAM*4);
            if (modoBTS != -1) div17 = (div17&(255-8))|(modoBTS*8);
            b[30] = hex[div15/16];
            b[31] = hex[div15%16];
            b[34] = hex[div17/16];
            b[35] = hex[div17%16];
            DIV = b.ToString();
            
            tcs.SendParameter("asfa::div",DIV);
        }
        void Desconex()
        {
            Connected = false;
            Emergency = false;
            tcs.SetCabDisplayControl(11, 0);
            
            //tcs.Unregister("asfa::*");
            
            tcs.SendParameter("asfa::pulsador::conex", "0");
        }
        public override void Initialize()
        {
            //ToDo: send DIV data
            tcs.SetCustomizedTCSControlString("Rec. anuncio parada");
            tcs.SetCustomizedTCSControlString("Rec. anuncio precaucion");
            tcs.SetCustomizedTCSControlString("Rec. preanuncio o condicional");
            tcs.SetCustomizedTCSControlString("Modo ASFA");
            tcs.SetCustomizedTCSControlString("Rearme freno");
            tcs.SetCustomizedTCSControlString("Rebase autorizado");
            tcs.SetCustomizedTCSControlString("Aumento vel. ASFA");
            tcs.SetCustomizedTCSControlString("Rec. alarma ASFA");
            tcs.SetCustomizedTCSControlString("Ocultacion info ASFA");
            tcs.SetCustomizedTCSControlString("Rec. limitacion vel. ASFA");
            tcs.SetCustomizedTCSControlString("Rec. paso a nivel");
            tcs.SetCustomizedTCSControlString("Conexión ASFA");
        }
        public override void Update()
        {
            base.Update();
            tcs.SetCabDisplayControl(0, IlumAnpar ? 1 : 0);
            tcs.SetCabDisplayControl(1, IlumAnpre ? 1 : 0);
            tcs.SetCabDisplayControl(2, IlumVLcond ? (IlumPrepar ? 3 : 2) : (IlumPrepar ? 1 : 0));
            tcs.SetCabDisplayControl(3, IlumModo ? 1 : 0);
            tcs.SetCabDisplayControl(4, IlumRearme ? 1 : 0);
            tcs.SetCabDisplayControl(5, IlumRebase ? 1 : 0);
            tcs.SetCabDisplayControl(6, IlumAumento ? 1 : 0);
            tcs.SetCabDisplayControl(7, IlumAlarma ? 1 : 0);
            tcs.SetCabDisplayControl(8, IlumOcult ? 1 : 0);
            tcs.SetCabDisplayControl(9, IlumLVI ? 1 : 0);
            tcs.SetCabDisplayControl(10, IlumPN ? 1 : 0);
            tcs.SetCabDisplayControl(15, UltimaInfo);
            tcs.SetCabDisplayControl(16, IndicadorPNdesp != 0 ? 2 : (IndicadorPNprot != 0 ? 1 : 0));
            tcs.SetCabDisplayControl(17, controldesv ? 2 : (secAA ? 5 : 0));
            tcs.SetCabDisplayControl(18, IndicadorLVI);
            tcs.SetCabDisplayControl(20, TargetState == 0 ? 2 : (TargetState == 1 ? 0 : 1));
            tcs.SetCabDisplayControl(21, Emergency ? 3 : IndicadorFrenado);
            tcs.SetNextSignalAspect((Aspect)(8-UltimaInfo));
            //tcs.SetOverspeedWarningDisplay(IndicadorFrenado != 0 ? true : false);
            //tcs.SetPenaltyApplicationDisplay(Emergency);
        }
        public override void HandleEvent(TCSEvent ev, string message)
        {
            if(ev == TCSEvent.GenericTCSButtonPressed || ev == TCSEvent.GenericTCSButtonReleased)
            {
                int num = int.Parse(message);
                bool pressed = ev == TCSEvent.GenericTCSButtonPressed;
                if (num == 11 && pressed)
                {
                    if (Connected) Desconex();
                    else Conex();
                    return;
                }
                if(num==0) Anun = pressed;
                else if(num==1) Prec = pressed;
                else if(num==2) Prean = pressed;
                else if(num==3) Modo = pressed;
                else if(num==4) Rearme = pressed;
                else if(num==5) Rebase = pressed;
                else if(num==6) Aumento = pressed;
                else if(num==7) Alarma = pressed;
                else if(num==8) Ocultacion = pressed;
                else if(num==9) LTV = pressed;
                else if(num==10) PN = pressed;
            }
        }
        public override void SetEmergency(bool emergency) {}
        public override List<Parameter> GetParameters()
        {
            List<Parameter> l = base.GetParameters();
            /*if(p.name=="asfa_sound_trigger")
            {
                p.SetValue = (string val) => 
                {
                    int num = int.Parse(val);
                    if(num == 0) tcs.TriggerSoundInfo1();
                    if(num == 1) tcs.TriggerSoundPenalty1();
                    if(num == 2) tcs.TriggerSoundAlert1();
                    if(num == 3) tcs.TriggerSoundAlert2();
                    if(num == 9) tcs.TriggerSoundSystemDeactivate();
                };
                return true;
            }
            else */
            Parameter p = null;
            
            p = new Parameter("asfa::indicador::v_control");
            p.SetValue = (string val) => tcs.SetNextSpeedLimitMpS(MpS.FromKpH(float.Parse(val)));
            l.Add(p);
            
            p = new Parameter("asfa::indicador::estado_vcontrol");
            p.SetValue = (string val) => TargetState = int.Parse(val);
            l.Add(p);
            
            
            p = new Parameter("asfa::indicador::ultima_info");
            p.SetValue = (string val) => {
                int num = int.Parse(val);
                switch(num)
                {
                    case 2:
                        UltimaInfo = 0;
                        break;
                    case 3:
                        UltimaInfo = 1;
                        break;
                    case 4:
                        UltimaInfo = 5;
                        break;
                    case 5:
                        UltimaInfo = 4;
                        break;
                    case 6:
                        UltimaInfo = 2;
                        break;
                    case 7:
                        UltimaInfo = 3;
                        break;
                    case 8:
                        UltimaInfo = 6;
                        break;
                    default:
                        UltimaInfo = 7;
                        break;
                }
            };
            l.Add(p);
            
            p = new Parameter("asfa::indicador::control_desvio");
            p.SetValue = (string val) => controldesv = val=="1";
            l.Add(p);
            
            p = new Parameter("asfa::indicador::secuencia_aa");
            p.SetValue = (string val) => secAA = val=="1";
            l.Add(p);
            
            p = new Parameter("asfa::indicador::lvi");
            p.SetValue = (string val) => IndicadorLVI = int.Parse(val);
            l.Add(p);
            
            p = new Parameter("asfa::indicador::pndesp");
            p.SetValue = (string val) => IndicadorPNdesp = int.Parse(val);
            l.Add(p);
            
            p = new Parameter("asfa::indicador::pnprot");
            p.SetValue = (string val) => IndicadorPNprot = int.Parse(val);
            l.Add(p);
            
            p = new Parameter("asfa::indicador::frenado");
            p.SetValue = (string val) => IndicadorFrenado = int.Parse(val);
            l.Add(p);
            
            p = new Parameter("asfa::pulsador::anpar");
            p.GetValue = () => Anun ? "1" : "0";
            l.Add(p);
            
            p = new Parameter("asfa::pulsador::anpre");
            p.GetValue = () => Prec ? "1" : "0";
            l.Add(p);
            
            p = new Parameter("asfa::pulsador::prepar");
            p.GetValue = () => Prean ? "1" : "0";
            l.Add(p);
            
            p = new Parameter("asfa::pulsador::modo");
            p.GetValue = () => Modo ? "1" : "0";
            l.Add(p);
            
            p = new Parameter("asfa::pulsador::rearme");
            p.GetValue = () => Rearme ? "1" : "0";
            l.Add(p);
            
            p = new Parameter("asfa::pulsador::rebase");
            p.GetValue = () => Rebase ? "1" : "0";
            l.Add(p);
            
            p = new Parameter("asfa::pulsador::aumento");
            p.GetValue = () => Aumento ? "1" : "0";
            l.Add(p);
            
            p = new Parameter("asfa::pulsador::alarma");
            p.GetValue = () => Alarma ? "1" : "0";
            l.Add(p);
            
            p = new Parameter("asfa::pulsador::ocultacion");
            p.GetValue = () => Ocultacion ? "1" : "0";
            l.Add(p);
            
            p = new Parameter("asfa::pulsador::lvi");
            p.GetValue = () => LTV ? "1" : "0";
            l.Add(p);
            
            p = new Parameter("asfa::pulsador::pn");
            p.GetValue = () => PN ? "1" : "0";
            l.Add(p);
            
            p = new Parameter("asfa::pulsador::ilum::anpar");
            p.SetValue = (string val) => IlumAnpar = val== "1";
            l.Add(p);
            
            p = new Parameter("asfa::pulsador::ilum::anpre");
            p.SetValue = (string val) => IlumAnpre = val== "1";
            l.Add(p);
            
            p = new Parameter("asfa::pulsador::ilum::prepar");
            p.SetValue = (string val) => IlumPrepar = val== "1";
            l.Add(p);
            
            p = new Parameter("asfa::pulsador::ilum::vlcond");
            p.SetValue = (string val) => IlumVLcond = val== "1";
            l.Add(p);
            
            p = new Parameter("asfa::pulsador::ilum::modo");
            p.SetValue = (string val) => IlumModo = val== "1";
            l.Add(p);
            
            p = new Parameter("asfa::pulsador::ilum::rearme");
            p.SetValue = (string val) => IlumRearme = val== "1";
            l.Add(p);
            
            p = new Parameter("asfa::pulsador::ilum::rebase");
            p.SetValue = (string val) => IlumRebase = val== "1";
            l.Add(p);
            
            p = new Parameter("asfa::pulsador::ilum::aumento");
            p.SetValue = (string val) => IlumAumento = val== "1";
            l.Add(p);
            
            p = new Parameter("asfa::pulsador::ilum::alarma");
            p.SetValue = (string val) => IlumAlarma = val== "1";
            l.Add(p);
            
            p = new Parameter("asfa::pulsador::ilum::ocultacion");
            p.SetValue = (string val) => IlumOcult = val== "1";
            l.Add(p);
            
            p = new Parameter("asfa::pulsador::ilum::lvi");
            p.SetValue = (string val) => IlumLVI = val== "1";
            l.Add(p);
            
            p = new Parameter("asfa::pulsador::ilum::pn");
            p.SetValue = (string val) => IlumPN = val== "1";
            l.Add(p);
            
            return l;
        }
    }
}
