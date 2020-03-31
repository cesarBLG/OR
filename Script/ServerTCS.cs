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
using Orts.Simulation.Physics;
using Orts.Simulation.Signalling;
namespace ORTS.Scripting.Script
{
    public class ServerTCS : TrainControlSystem
    {
        public Client c=null;
        HashSet<Parameter> parameters = new HashSet<Parameter>();
        List<InteractiveTCS> tcs = new List<InteractiveTCS>();
        Parameter GetParameter(string parameter)
        {
            Parameter compared = new Parameter(parameter);
            Parameter p = null;
            foreach(Parameter p1 in parameters)
            {
                if(compared.Equals(p1))
                {
                    p = p1;
                    break;
                }
            }
            if (p==null)
            {
                p = compared;
                if(parameter == "speed")
                {
                    p.GetValue = () => MpS.ToKpH(SpeedMpS()).ToString().Replace(',','.');
                }
                else if(parameter == "cruise_speed")
                {
                    //p.GetValue = () => MpS.ToKpH(cruise_speed).ToString().Replace(',','.');
                    p.SetValue = (string val) => cruise_speed=MpS.FromKpH(float.Parse(val.Replace('.',',')));
                }
                else if(parameter == "distance")
                {
                    p.GetValue = () => DistanceM().ToString().Replace(',','.');
                }
                else if(parameter == "train_length")
                {
                    p.GetValue = () => TrainLengthM().ToString().Replace(',','.');
                }
                else if(parameter.StartsWith("next_signal_aspect("))
                {
                    int par1 = parameter.IndexOf('(');
                    int par2 = parameter.LastIndexOf(')');
                    int signum = int.Parse(parameter.Substring(par1+1, par2-par1-1));
                    p.GetValue = () => NextSignalAspect(signum).ToString();
                }
                else if(parameter=="throttle")
                {
                    p.SetValue = (string val) => {
                        float value = float.Parse(val.Replace('.',','));
                        userThrottle = value;
                    };
                }
                else if(parameter=="dynamic_brake")
                {
                    p.SetValue = (string val) => {
                        float value = float.Parse(val.Replace('.',','));
                        userDynamic = value;
                    };
                }
                else if(parameter=="direction")
                {
                    p.SetValue = (string val) =>
                    {
                        if(val=="1") direction = Direction.Forward;
                        else if(val=="-1") direction = Direction.Reverse;
                        else direction = Direction.N;
                    };
                }
                /*else if(parameter=="train_brake")
                {
                    //p.SetValue = (string val) => {ATF_brake = float.Parse(val.Replace('.',','));};
                    p.GetValue = () => ATF_brake.ToString().Replace(',','.');
                }*/
                else if(parameter=="headlight")
                {
                    p.SetValue = (string val) => 
                    {
                        if(val=="3") SignalEvent(Event._HeadlightOn);
                        else if(val=="2") SignalEvent(Event._HeadlightDim);
                        else SignalEvent(Event._HeadlightOff);
                    };
                }
                else if(parameter=="wipers")
                {
                    p.SetValue = (string val) => 
                    {
                        if(val=="3"||val=="1") SignalEvent(Event.WiperOn);
                        else SignalEvent(Event.WiperOff);
                    };
                }
                else if(parameter=="sander")
                {
                    p.SetValue = (string val) => 
                    {
                        if(val=="1" || val == "true") SignalEventToTrain(Event.SanderOn);
                        else SignalEventToTrain(Event.SanderOff);
                    };
                }
                else if(parameter=="horn")
                {
                    p.SetValue = (string val) => 
                    {
                        if(val=="1" || val == "true") SetHorn(true);
                        else SetHorn(false);
                    };
                }
                else if(parameter=="bell")
                {
                    p.SetValue = (string val) => setKey(0x42, val != "1" && val != "true");
                }
                else if(parameter=="simulator_time")
                {
                    p.GetValue = () => ClockTime().ToString().Replace(',','.');
                }
                else
                {
                    bool assigned = false;
                    foreach(InteractiveTCS i in tcs)
                    {
                        if(i.HandleParameter(p))
                        {
                            assigned = true;
                            break;
                        }
                    }
                    if(!assigned) return null;
                }
                parameters.Add(p);
                /*if(p.SetValue != null)
                {
                    c.WriteLine("register(" + p.name + ")");
                    p.registerPetitionSent.Add(c);
                }*/
            }
            return p;
        }
        public override void Initialize()
        {
            Activated = false;
            tcs.Add(new HM(this));
            tcs.Add(new ASFADigital(this));
            tcs.Add(new ETCS(this));
            foreach(InteractiveTCS i in tcs)
            {
                i.Activated = true;
                i.Initialize();
            }
            NextRepeaterSignalAspect = () => NextRepeaterSignalItem<Aspect>(ref SignalAspect, Train.TrainObjectItem.TRAINOBJECTTYPE.SIGNAL);
            NextRepeaterSignalDistanceM = () => NextRepeaterSignalItem<float>(ref SignalDistance, Train.TrainObjectItem.TRAINOBJECTTYPE.SIGNAL);
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
        public override void Update()
        {
            if(!IsTrainControlEnabled())
            {
                c = null;
                return;
            }

            foreach(InteractiveTCS i in tcs)
            {
                i.Update();
            }
            if(c==null)
            {
                TcpClient cl = new TcpClient();
                cl.Connect("127.0.0.1", 5090);
                c = new TCPClient(cl);
                c.WriteLine("register(throttle)");
                c.WriteLine("register(cruise_speed)");
                c.WriteLine("register(dynamic_brake)");
                c.WriteLine("register(direction)");
                c.WriteLine("register(horn)");
                c.WriteLine("register(bell)");
                c.WriteLine("register(wipers)");
                c.WriteLine("register(sander)");
                c.WriteLine("register(headlight)");
                c.WriteLine("register(hm_pressed)");
                c.WriteLine("request_module(asfa_digital)");
                c.WriteLine("register(asfa_emergency)");
                c.WriteLine("register(asfa_target_speed)");
                c.WriteLine("register(asfa_target_state)");
                c.WriteLine("register(asfa_last_info)");
                c.WriteLine("register(asfa_secuencia_aa)");
                c.WriteLine("register(asfa_control_desvio)");
                c.WriteLine("register(asfa_indicador_lvi)");
                c.WriteLine("register(asfa_indicador_pndesp)");
                c.WriteLine("register(asfa_indicador_pnprot)");
                c.WriteLine("register(asfa_indicador_frenado)");
                c.WriteLine("register(asfa_ilumpuls_anpar)");
                c.WriteLine("register(asfa_ilumpuls_anpre)");
                c.WriteLine("register(asfa_ilumpuls_prepar)");
                c.WriteLine("register(asfa_ilumpuls_vlcond)");
                c.WriteLine("register(asfa_ilumpuls_modo)");
                c.WriteLine("register(asfa_ilumpuls_rearme)");
                c.WriteLine("register(asfa_ilumpuls_rebase)");
                c.WriteLine("register(asfa_ilumpuls_aumento)");
                c.WriteLine("register(asfa_ilumpuls_alarma)");
                c.WriteLine("register(asfa_ilumpuls_ocultacion)");
                c.WriteLine("register(asfa_ilumpuls_lvi)");
                c.WriteLine("register(asfa_ilumpuls_pn)");
                c.WriteLine("register(etcs_emergency)");
                c.WriteLine("register(etcs_fullbrake)");
            }
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
                    String fun = s.Substring(0, div);
                    String param = s.Substring(div+1, fin-div-1);
                    Parameter p = GetParameter(param);
                    if(p!=null && p.GetValue!=null)
                    {
                        if(fun == "register")
                        {
                            Register r = new DiscreteRegister(false);
                            p.registers[r] = c;
                        }
                        else c.WriteLine(p.name + '=' + p.GetValue());
                    }
                }
                else if(s.Contains('='))
                {
                    int pos = s.IndexOf('=');
                    string param = s.Substring(0, pos);
                    string val = s.Substring(pos+1);
                    if(param != "connected") 
                    {
                        Parameter p = GetParameter(param);
                        if(p!=null && p.SetValue!=null) p.SetValue(val);
                    }
                }
                s = c.ReadLine();
            }
            foreach(Parameter p in parameters)
            {
                p.Send();
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
            SetEmergencyBrake(Emergency||IsDirectionNeutral());
            SetFullBrake(FullBrake);
            SetTractionAuthorization(!DoesBrakeCutPower() || BrakeCutsPowerAtBrakeCylinderPressureBar() > LocomotiveBrakeCylinderPressureBar());
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
		Aspect SignalAspect;
        float SignalDistance;
		public Func<Aspect> NextRepeaterSignalAspect;
        public Func<float> NextRepeaterSignalDistanceM;
		T NextRepeaterSignalItem<T>(ref T retval, Train.TrainObjectItem.TRAINOBJECTTYPE type)
        {
            if (Locomotive().Train.ValidRoute[0] != null && Locomotive().Train.PresentPosition[0].RouteListIndex >= 0)
            {
                TrackCircuitSignalItem nextSignal = Locomotive().Train.signalRef.Find_Next_Object_InRoute(Locomotive().Train.ValidRoute[0],
                    Locomotive().Train.PresentPosition[0].RouteListIndex, Locomotive().Train.PresentPosition[0].TCOffset,
                            400, Orts.Formats.Msts.MstsSignalFunction.REPEATER, Locomotive().Train.routedForward);

                if (nextSignal.SignalState == ObjectItemInfo.ObjectItemFindState.Object)
                {
                    Aspect distanceSignalAspect = (Aspect)Locomotive().Train.signalRef.TranslateToTCSAspect(nextSignal.SignalRef.this_sig_lr(Orts.Formats.Msts.MstsSignalFunction.REPEATER));
                    SignalAspect = distanceSignalAspect;
                    SignalDistance = nextSignal.SignalLocation;
                    return retval;
                }
            }

            SignalAspect = Aspect.None;
            SignalDistance = float.MaxValue;
            return retval;
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
        public List<Client> registerPetitionSent;
        public readonly string name;
        public Parameter(string name)
        {
            registers = new Dictionary<Register, Client>();
            registerPetitionSent = new List<Client>();
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
        public abstract bool HandleParameter(Parameter p);
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
        public override void HandleEvent(TCSEvent evt, string message)
        {
            switch(evt)
            {
                case TCSEvent.AlerterPressed:
                    Pressed = true;
                    break;
                case TCSEvent.AlerterReleased:
                    Pressed = false;
                    break;
            }
        }
        public override void Initialize()
        {
            HMReleasedAlertDelayS = 2.5f;
            HMReleasedEmergencyDelayS = 5f;
            HMPressedAlertDelayS = 32.5f;
            HMPressedEmergencyDelayS = 35f;
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
                Emergency = false;
                if (tcs.AlerterSound()) SetVigilanceAlarm(false);
                SetVigilanceAlarmDisplay(false);
                //SetVigilanceEmergencyDisplay(false);
                return;
            }
            if (Pressed && (!HMPressedAlertTimer.Started || !HMPressedEmergencyTimer.Started))
            {
                HMReleasedAlertTimer.Stop();
                HMReleasedEmergencyTimer.Stop();
                HMPressedAlertTimer.Start();
                HMPressedEmergencyTimer.Start();
                if (!tcs.AlerterSound()) SetVigilanceAlarm(false);
                SetVigilanceAlarmDisplay(false);
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
            if (Emergency && tcs.SpeedMpS() < 1.5f)
            {
                Emergency = false;
                SetVigilanceEmergencyDisplay(false);
            }
        }
        public override bool HandleParameter(Parameter p)
        {
            if(p.name=="hm_pressed")
            {
                p.SetValue = (string val) => {Pressed = val=="1" || val=="true";};
                return true;
            }
            return false;
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
            Client c = ((ServerTCS)tcs).c;
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
                string grad="00010101"+"01"+"0000001001110"+"01";
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
                c.WriteLine("etcs_telegram="+tel1);
                c.WriteLine("etcs_telegram="+tel2);
                c.WriteLine("etcs_telegram="+tel3);
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
        public override bool HandleParameter(Parameter p)
        {
            if(p.name=="etcs_emergency")
            {
                p.SetValue = (string val) => {Emergency = val=="1" || val=="true";};
                return true;
            }
            else if(p.name=="etcs_fullbrake")
            {
                p.SetValue = (string val) => {FullBrake = val=="1" || val=="true";};
                return true;
            }
            return false;
        }
        string format_etcs_speed(float speedmps)
        {
            int val = (int)Math.Round(speedmps*3.6)/5;
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
            int val = (int)distm;
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
            L11
        }
        Aspect BalizaAspect;
        Aspect BalizaNextAspect;
        float LVIstart = 0;
        Freq lvi1 = Freq.L11;
        Freq lvi2 = Freq.L11;
        public ASFA(ServerTCS tcs) : base(tcs)
        {

        }
        public override void Update()
        {
            UpdateSignalPassed();
            UpdateDistanciaPrevia();
            UpdatePostPassed();
            UpdateAnuncioLTVPassed();
        }
        public Freq Baliza()
        {
            //if (tcs.IsDirectionReverse())
            //{
            //    if (SignalPassed) return FrecASFA.L8;
            //    else if (PreviaPassed) return FrecASFA.L7;
            //    else return FrecASFA.FP;
            //}
            //else
            /*{
                if (tcs.NextSignalDistanceM(PreviaSignalNumber) > PreviaDistance - 10 && tcs.NextSignalDistanceM(PreviaSignalNumber) < PreviaDistance)
                {
                    BalizaAspect = tcs.NextSignalAspect(PreviaSignalNumber);
                    switch (BalizaAspect)
                    {
                        case Aspect.Stop:
                        case Aspect.StopAndProceed:
                        case Aspect.Restricted:
                        case Aspect.Permission:
                            return Freq.L7;
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
                if (tcs.NextSignalDistanceM(0) < 5 && tcs.NextSignalDistanceM(0) > 0.01f)
                {
                    BalizaAspect = tcs.NextSignalAspect(0);
                    switch (BalizaAspect)
                    {
                        case Aspect.Stop:
                        case Aspect.StopAndProceed:
                        case Aspect.Restricted:
                        case Aspect.Permission:
                            return Freq.L8;
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
                    BalizaNextAspect = tcs.NextSignalAspect(0);
                }
                if(LVIstart==0)
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
                    
                }
                if (LVIstart != 0)
                {
                    if (tcs.DistanceM() - LVIstart < 3) return lvi1;
                    if (tcs.DistanceM() - LVIstart < 6) return Freq.FP;
                    if (tcs.DistanceM() - LVIstart < 9) return lvi2;
                    if (tcs.DistanceM() - LVIstart < 12) return Freq.FP;
                    if (tcs.DistanceM() - LVIstart < 15) return Freq.L9;
                    LVIstart = 0;
                }
            }
            return Freq.FP;*/
            float dist = tcs.NextRepeaterSignalDistanceM();
            if (dist<5 && dist>0.01f)
            {
                switch(tcs.NextRepeaterSignalAspect())
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
            return Freq.FP;
        }
        public override bool HandleParameter(Parameter p)
        {
            if(p.name=="asfa_emergency")
            {
                p.SetValue = (string val) => {Emergency = val!="0" && val!="false";};
                return true;   
            }
            else if(p.name == "asfa_baliza")
            {
                p.GetValue = () => Baliza().ToString();
                return true;
            }
            return false;
        }
        bool SignalPassed = false;
        float PreviousSignalDistanceM = 0;
        bool PreviaPassed = false;
        bool IntermediateDist = false;
        int PreviaSignalNumber=0;
        bool IsPN;
        bool LineaConvencional = true;
        float PreviaDistance = 300;
        Aspect CurrentSignalAspect = Aspect.Stop;
        float PreviousPostDistanceM = 0f;
        bool IntermediateLTVDist = false;
        bool PostPassed = false;
        bool AnuncioLTVPassed = false;
        float AnuncioDistance = 1500f;
		protected void UpdateSignalPassed()
        {
            SignalPassed = (tcs.NextSignalDistanceM(0) > PreviousSignalDistanceM+20)&&(tcs.SpeedMpS()>0.1f);
            PreviousSignalDistanceM = tcs.NextSignalDistanceM(0);
            if (SignalPassed && tcs.NextSignalAspect(0) == Aspect.None) SignalPassed = false;
        }
        protected void UpdatePreviaPassed()
		{
			if(PreviaPassed) IntermediateDist = true;
            if (SignalPassed && (CurrentSignalAspect != Aspect.Clear_2 || tcs.CurrentSignalSpeedLimitMpS() < MpS.FromKpH(150f) || tcs.CurrentSignalSpeedLimitMpS() > MpS.FromKpH(165f) || !IsPN ) && (CurrentSignalAspect != Aspect.Approach_1 || tcs.CurrentSignalSpeedLimitMpS() < MpS.FromKpH(25f) || tcs.CurrentSignalSpeedLimitMpS() > MpS.FromKpH(35f) || !IsPN)) IntermediateDist = false;
			PreviaPassed = tcs.NextSignalDistanceM(PreviaSignalNumber)<PreviaDistance && tcs.NextSignalDistanceM(PreviaSignalNumber)>PreviaDistance-10 && !IntermediateDist&&tcs.SpeedMpS()>0.1f;
            if (tcs.NextSignalAspect(1) == Aspect.None && tcs.NextSignalAspect(0) == Aspect.Stop )
            {
                PreviaPassed = false;
            }
        }
        protected void UpdateDistanciaPrevia()
        {
            if (SignalPassed)
            {
                if ((tcs.NextSignalAspect(0) == Aspect.Clear_2 && tcs.NextSignalSpeedLimitMpS(0) < MpS.FromKpH(165f) && tcs.NextSignalSpeedLimitMpS(0) > MpS.FromKpH(155f)) || (tcs.NextSignalAspect(0) == Aspect.Approach_1 && tcs.NextSignalSpeedLimitMpS(0) < MpS.FromKpH(35f) && tcs.NextSignalSpeedLimitMpS(0) > MpS.FromKpH(25f)))
                {
                    PreviaSignalNumber = 1;
                    IsPN = true;
                }
                else PreviaSignalNumber = 0;
                if ((tcs.CurrentSignalSpeedLimitMpS() > 165f || tcs.CurrentSignalSpeedLimitMpS() < 155f || CurrentSignalAspect != Aspect.Clear_2) && ((tcs.CurrentSignalSpeedLimitMpS() > 35f || tcs.CurrentSignalSpeedLimitMpS() < 25f || CurrentSignalAspect != Aspect.Approach_1)))
                {
                    if (tcs.NextSignalDistanceM(PreviaSignalNumber) < 100f)
                    {
                        PreviaDistance = 0f;
                    }
                    else if (tcs.NextSignalDistanceM(PreviaSignalNumber) < 400f)
                    {
                        PreviaDistance = 50f;
                    }
                    else if (tcs.NextSignalDistanceM(PreviaSignalNumber) < 700f)
                    {
                        PreviaDistance = 100f;
                    }
                    else
                    {
                        PreviaDistance = 300f;
                    }
                }
                if (!LineaConvencional)
                {
                    PreviaSignalNumber = 0;
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
            else CurrentSignalAspect = tcs.NextSignalAspect(0);
            if ((tcs.NextSignalAspect(0) == Aspect.Clear_2 && tcs.NextSignalSpeedLimitMpS(0) < MpS.FromKpH(165f) && tcs.NextSignalSpeedLimitMpS(0) > MpS.FromKpH(150f)) || (tcs.NextSignalAspect(0) == Aspect.Approach_1 && tcs.NextSignalSpeedLimitMpS(0) < MpS.FromKpH(35f) && tcs.NextSignalSpeedLimitMpS(0) > MpS.FromKpH(25f))) IsPN = true;
            if (PreviaDistance != 0f) UpdatePreviaPassed();
            else PreviaPassed = false;
        }
		protected void UpdatePostPassed()
        {
            PostPassed = (tcs.NextPostDistanceM(0) > PreviousPostDistanceM+20)&&(tcs.SpeedMpS()>0);
            PreviousPostDistanceM = tcs.NextPostDistanceM(0);
        }	
		protected void UpdateAnuncioLTVPassed()
		{
			if(AnuncioLTVPassed) IntermediateLTVDist = true;
			if(PostPassed) IntermediateLTVDist = false;
			AnuncioLTVPassed = tcs.NextPostDistanceM(0)<AnuncioDistance && tcs.NextPostDistanceM(0)>AnuncioDistance-10f && !IntermediateLTVDist && (tcs.CurrentPostSpeedLimitMpS()-tcs.NextPostSpeedLimitMpS(0))>=MpS.FromKpH(40f);
		}
    }
    class ASFAclasico : ASFA
    {
        public ASFAclasico(ServerTCS tcs) : base(tcs)
        {
        }
        public override void Initialize()
        {
            //ToDo: send DIV data
        }
        public override void Update()
        {
            base.Update();
        }
        public override void HandleEvent(TCSEvent ev, string message)
        {

        }
        public override void SetEmergency(bool emergency) {}
        public override bool HandleParameter(Parameter p)
        {
            return base.HandleParameter(p);
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
        public ASFADigital(ServerTCS tcs) : base(tcs)
        {
        }
        public override void Initialize()
        {
            //ToDo: send DIV data
            tcs.SetCustomizedTCSControlString("Modo ASFA");
            tcs.SetCustomizedTCSControlString("Rearme freno");
            tcs.SetCustomizedTCSControlString("Rebase autorizado");
            tcs.SetCustomizedTCSControlString("Aumento vel. ASFA");
            tcs.SetCustomizedTCSControlString("Rec. alarma ASFA");
            tcs.SetCustomizedTCSControlString("Ocultacion info ASFA");
            tcs.SetCustomizedTCSControlString("Rec. limitacion vel. ASFA");
            tcs.SetCustomizedTCSControlString("Rec. paso a nivel");
            tcs.SetCustomizedTCSControlString("Rec. anuncio parada");
            tcs.SetCustomizedTCSControlString("Rec. anuncio precaucion");
            tcs.SetCustomizedTCSControlString("Rec. preanuncio o condicional");
        }
        public override void Update()
        {
            base.Update();
            //tcs.SetNextSignalAspect(UltimaInfo);
            tcs.SetCabDisplayControl(15, UltimaInfo);
            tcs.SetCabDisplayControl(16, IndicadorPNdesp != 0 ? 2 : (IndicadorPNprot != 0 ? 1 : 0));
            tcs.SetCabDisplayControl(17, controldesv ? 2 : (secAA ? 5 : 0));
            tcs.SetCabDisplayControl(18, IndicadorLVI);
            tcs.SetCabDisplayControl(20, TargetState == 0 ? 2 : (TargetState == 1 ? 0 : 1));
            tcs.SetCabDisplayControl(21, IlumModo ? 1 : 0);
            tcs.SetCabDisplayControl(22, IlumRearme ? 1 : 0);
            tcs.SetCabDisplayControl(23, IlumRebase ? 1 : 0);
            tcs.SetCabDisplayControl(24, IlumAumento ? 1 : 0);
            tcs.SetCabDisplayControl(25, IlumAlarma ? 1 : 0);
            tcs.SetCabDisplayControl(26, IlumOcult ? 1 : 0);
            tcs.SetCabDisplayControl(27, IlumLVI ? 1 : 0);
            tcs.SetCabDisplayControl(28, IlumPN ? 1 : 0);
            tcs.SetCabDisplayControl(29, IlumAnpar ? 1 : 0);
            tcs.SetCabDisplayControl(30, IlumAnpre ? 1 : 0);
            tcs.SetCabDisplayControl(31, IlumVLcond ? (IlumPrepar ? 3 : 2) : (IlumPrepar ? 1 : 0));
            tcs.SetCabDisplayControl(11, Emergency ? 1 : (IndicadorFrenado!=0 ? (IndicadorFrenado + 1) : 0));
            tcs.SetOverspeedWarningDisplay(IndicadorFrenado != 0 ? true : false);
            tcs.SetPenaltyApplicationDisplay(Emergency);
        }
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
        public override void HandleEvent(TCSEvent ev, string message)
        {
            if(ev == TCSEvent.GenericTCSButtonPressed || ev == TCSEvent.GenericTCSButtonReleased)
            {
                int num = int.Parse(message);
                bool pressed = ev == TCSEvent.GenericTCSButtonPressed;
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
        public override bool HandleParameter(Parameter p)
        {
            if(p.name=="asfa_sound_trigger")
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
            else if(p.name=="asfa_target_speed")
            {
                p.SetValue = (string val) => tcs.SetNextSpeedLimitMpS(MpS.FromKpH(float.Parse(val)));
                return true;
            }
            else if(p.name=="asfa_target_state")
            {
                p.SetValue = (string val) => TargetState = int.Parse(val);
                return true;
            }
            else if(p.name=="asfa_last_info")
            {
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
                return true;
            }
            else if(p.name=="asfa_control_desvio")
            {
                p.SetValue = (string val) => controldesv = val=="1";
                return true;
            }
            else if(p.name=="asfa_secuencia_aa")
            {
                p.SetValue = (string val) => secAA = val=="1";
                return true;
            }
            else if(p.name=="asfa_indicador_lvi")
            {
                p.SetValue = (string val) => IndicadorLVI = int.Parse(val);
                return true;
            }
            else if(p.name=="asfa_indicador_pndesp")
            {
                p.SetValue = (string val) => IndicadorPNdesp = int.Parse(val);
                return true;
            }
            else if(p.name=="asfa_indicador_pnprot")
            {
                p.SetValue = (string val) => IndicadorPNprot = int.Parse(val);
                return true;
            }
            else if(p.name=="asfa_indicador_frenado")
            {
                p.SetValue = (string val) => IndicadorFrenado = int.Parse(val);
                return true;
            }
            else if(p.name=="asfa_pulsador_anpar")
            {
                p.GetValue = () => Anun ? "1" : "0";
                return true;
            }
            else if(p.name=="asfa_pulsador_anpre")
            {
                p.GetValue = () => Prec ? "1" : "0";
                return true;
            }
            else if(p.name=="asfa_pulsador_prepar")
            {
                p.GetValue = () => Prean ? "1" : "0";
                return true;
            }
            else if(p.name=="asfa_pulsador_modo")
            {
                p.GetValue = () => Modo ? "1" : "0";
                return true;
            }
            else if(p.name=="asfa_pulsador_rearme")
            {
                p.GetValue = () => Rearme ? "1" : "0";
                return true;
            }
            else if(p.name=="asfa_pulsador_rebase")
            {
                p.GetValue = () => Rebase ? "1" : "0";
                return true;
            }
            else if(p.name=="asfa_pulsador_aumento")
            {
                p.GetValue = () => Aumento ? "1" : "0";
                return true;
            }
            else if(p.name=="asfa_pulsador_alarma")
            {
                p.GetValue = () => Alarma ? "1" : "0";
                return true;
            }
            else if(p.name=="asfa_pulsador_ocultacion")
            {
                p.GetValue = () => Ocultacion ? "1" : "0";
                return true;
            }
            else if(p.name=="asfa_pulsador_lvi")
            {
                p.GetValue = () => LTV ? "1" : "0";
                return true;
            }
            else if(p.name=="asfa_pulsador_pn")
            {
                p.GetValue = () => PN ? "1" : "0";
                return true;
            }
            else if(p.name=="asfa_ilumpuls_anpar")
            {
                p.SetValue = (string val) => IlumAnpar = val== "1";
                return true;
            }
            else if(p.name=="asfa_ilumpuls_anpre")
            {
                p.SetValue = (string val) => IlumAnpre = val== "1";
                return true;
            }
            else if(p.name=="asfa_ilumpuls_prepar")
            {
                p.SetValue = (string val) => IlumPrepar = val== "1";
                return true;
            }
            else if(p.name=="asfa_ilumpuls_vlcond")
            {
                p.SetValue = (string val) => IlumVLcond = val== "1";
                return true;
            }
            else if(p.name=="asfa_ilumpuls_modo")
            {
                p.SetValue = (string val) => IlumModo = val== "1";
                return true;
            }
            else if(p.name=="asfa_ilumpuls_rearme")
            {
                p.SetValue = (string val) => IlumRearme = val== "1";
                return true;
            }
            else if(p.name=="asfa_ilumpuls_rebase")
            {
                p.SetValue = (string val) => IlumRebase = val== "1";
                return true;
            }
            else if(p.name=="asfa_ilumpuls_aumento")
            {
                p.SetValue = (string val) => IlumAumento = val== "1";
                return true;
            }
            else if(p.name=="asfa_ilumpuls_alarma")
            {
                p.SetValue = (string val) => IlumAlarma = val== "1";
                return true;
            }
            else if(p.name=="asfa_ilumpuls_ocultacion")
            {
                p.SetValue = (string val) => IlumOcult = val== "1";
                return true;
            }
            else if(p.name=="asfa_ilumpuls_lvi")
            {
                p.SetValue = (string val) => IlumLVI = val== "1";
                return true;
            }
            else if(p.name=="asfa_ilumpuls_pn")
            {
                p.SetValue = (string val) => IlumPN = val== "1";
                return true;
            }
            else return base.HandleParameter(p);
        }
    }
}
