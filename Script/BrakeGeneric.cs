using ORTS.Common;
using ORTS.Scripting.Api;
using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Linq;
using System.Threading.Tasks;
using Event = Orts.Common.Event;
namespace ORTS.Scripting.Script
{
    public abstract class Client
    {
        public abstract void start();
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
            stream = client.GetStream();
            buff = "";
        }
        public override void start()
        {
        }
        public override void WriteLine(string s)
        {
            byte[] b = System.Text.Encoding.UTF8.GetBytes(s + '\n');
            stream.Write(b, 0, b.Length);
        }
        public override string ReadLine()
        {
            while(stream.DataAvailable)
            {
                buff+=(char)stream.ReadByte();
            }
            int ind = buff.IndexOf('\n');
            if(ind >= 0)
            {
                string res = buff.Substring(0, ind);
                buff = buff.Substring(ind+1);
                return res;
            }
            return null;
        }
    }
    public class BrakeGeneric : BrakeController
    {
        enum State
        {
            Release,
            Apply,
            FullBrake,
            Emergency
        }
        State CurrentState=State.Apply;
        float Value=0;
        float applyValue=0;
        float airBrakeValue=0;
        TCPClient client = null;
        public override void Initialize()
        {
        }
        public override void InitializeMoving()
        {
        }
        bool EBPB = false;
        bool dynavail = true;
        bool power = true;
        bool allowConnect = false;
        float speed=0;
        public override float Update(float elapsedSeconds)
        {
            if(TCSEmergencyBraking()) allowConnect = true;
            if(client==null && allowConnect)
            {
                TcpClient c = new TcpClient();
                c.Connect("127.0.0.1", 5090);
                client = new TCPClient(c);
                client.WriteLine("register(train_brake)");
                client.WriteLine("register(emergency_pushb)");
                client.WriteLine("register(power)");
                client.WriteLine("register(speed)");
            }
            if(client!=null)
            {
                string line = client.ReadLine();
                while(line!=null)
                {
                    string val = line.Substring(line.IndexOf('=')+1);
                    if(line.StartsWith("train_brake="))
                    {
                        Value = float.Parse(val.Replace('.', ','));
                    }
                    else if(line.StartsWith("emergency_pushb="))
                    {
                        EBPB = val!="0" && val!="false";
                    }
                    else if(line.StartsWith("power="))
                    {
                        power = val=="1" || val=="true";
                    }
                    else if(line.StartsWith("speed="))
                    {
                        speed = MpS.FromKpH(float.Parse(val.Replace('.', ',')));
                    }
                    line = client.ReadLine();
                }
            }
            if(UpdateValue()==1)
            {
                if(Value>0.9)
                {
                    Value = 1;
                    SetUpdateValue(0);
                }
                else
                {
                    float prev = Value;
                    Value += elapsedSeconds/3;
                    if(Value>=0.9&&prev<0.9)
                    {
                        Value = 0.93f;
                        SetUpdateValue(0);
                    }
                }
            }
            if(UpdateValue()==-1)
            {
                float prev = Value;
                Value -= elapsedSeconds/3;
                if(Value<=0)
                {
                    Value = 0;
                    SetUpdateValue(0);
                }
            }
            if(EBPB || EmergencyBrakingPushButton() || TCSEmergencyBraking() || Value > 0.95f) CurrentState = State.Emergency; 
            else if(TCSFullServiceBraking() || Value > 0.90f) CurrentState = State.FullBrake;
            else if(Value>0.01) CurrentState = State.Apply;
            else CurrentState = State.Release;
            if(Value<0.01) applyValue = 0;
            else applyValue = Math.Min(Math.Max(Value*0.9f, 0), 1);
            airBrakeValue = applyValue;
            SetCurrentValue(Value);
            return CurrentValue();
        }
        public override void UpdatePressure(ref float pressureBar, float elapsedClockSeconds, ref float epPressureBar)
        {
            float releasePressure = Math.Min(MaxPressureBar(), MainReservoirPressureBar());
            float fullServPressure = MaxPressureBar()-FullServReductionBar();
            switch(CurrentState)
            {
                case State.Release:
                    pressureBar = Math.Min(releasePressure, pressureBar+elapsedClockSeconds*ReleaseRateBarpS());
                    break;
                case State.Apply:
                    float target=releasePressure - airBrakeValue*(releasePressure-fullServPressure);
                    if(target<pressureBar) pressureBar = Math.Max(target, pressureBar-elapsedClockSeconds*ApplyRateBarpS());
                    else pressureBar = Math.Min(target, pressureBar+elapsedClockSeconds*ReleaseRateBarpS());
                    break;
                case State.FullBrake:
                    pressureBar = Math.Max(fullServPressure, pressureBar-elapsedClockSeconds*ApplyRateBarpS());
                    break;
                case State.Emergency:
                    pressureBar = Math.Max(0, pressureBar-elapsedClockSeconds*EmergencyRateBarpS());;
                    break;
            }
        }
        public override void UpdateEngineBrakePressure(ref float pressureBar, float elapsedClockSeconds)
        {
            
        }
        public override void HandleEvent(BrakeControllerEvent evt)
        {
            switch (evt)
            {
                case BrakeControllerEvent.StartIncrease:
                    SetUpdateValue(1);
                    break;
                case BrakeControllerEvent.StartDecrease:
                    SetUpdateValue(-1);
                    break;
                case BrakeControllerEvent.StopIncrease:
                case BrakeControllerEvent.StopDecrease:
                    SetUpdateValue(0);
                    break;
            }
        }
        public override void HandleEvent(BrakeControllerEvent evt, float? value)
        {
            if(evt == BrakeControllerEvent.SetCurrentPercent) Value = value.Value/100;
            if(evt == BrakeControllerEvent.SetCurrentValue) Value = value.Value;
            else HandleEvent(evt);
        }
        public override bool IsValid()
        {
            return true;
        }
        public override ControllerState GetState()
        {
            switch(CurrentState)
            {
                case State.Release:
                    return ControllerState.Release;
                case State.Apply:
                    return ControllerState.GSelfLap;
                case State.FullBrake:
                    if(TCSFullServiceBraking()) return ControllerState.TCSFullServ;
                    return ControllerState.FullServ;
                case State.Emergency:
                    if(EmergencyBrakingPushButton()||EBPB) return ControllerState.EBPB;
                    if(TCSEmergencyBraking()) return ControllerState.TCSEmergency;
                    return ControllerState.Emergency;
            }
            return ControllerState.Dummy;
        }
        public override float? GetStateFraction()
        {
            if(CurrentState==State.Apply) return applyValue;
            return null;
        }

    }
}
