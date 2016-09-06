using System;
using System.Collections.Generic;
using System.IO.Ports;
using ORTS.Common;
using ORTS.Scripting.Api;
namespace ORTS.Scripting.Script
{
    public struct Battery
    {
        public float ChargeAh;
        const float MaxChargeAh = 190;
        public float VoltageV;
        public float ChargePercent { get { return ChargeAh / MaxChargeAh * 100; } set { ChargeAh = MaxChargeAh * value / 100; } }
        public Electricity Get(float CurrentA, float TimeS)
        {
            ChargeAh -= CurrentA * TimeS / 3600;
            VoltageV = Math.Max(55 + 21.25f * ((ChargePercent - 20) / 100), 0);
            return new Electricity(VoltageV, CurrentA);
        }
        public void Add(Electricity e, float TimeS)
        {
            VoltageV = e.Voltage;
            ChargeAh = Math.Max(Math.Min(ChargeAh + e.Current * TimeS / 3600, MaxChargeAh), ChargeAh);
        }
    }
    public struct Electricity
    {
        public float Voltage;
        public float Current;
        public Electricity(float Voltage, float Current)
        {
            this.Voltage = Voltage;
            this.Current = Current;
        }
    }
    public abstract class Convertidor
    {
        public PowerSupplyState State;
        public abstract void Update(float Time);
    }
    public class Convertidor_3000Vcc_380Vca3 : Convertidor
    {
        public Func<Electricity> Input;
        public Func<float, float, Electricity[]> Output;
        public override void Update(float Time)
        {
            if (Input().Voltage > 2700) State = PowerSupplyState.PowerOn;
            else State = PowerSupplyState.PowerOff;
        }
        public Electricity[] Get(float CurrentA, float TimeS)
        {
            return Output(TimeS, CurrentA);
        }
        public Convertidor_3000Vcc_380Vca3(Func<Electricity> Input)
        {
            Output = (float Time, float Current) =>
            {
                if (State != PowerSupplyState.PowerOn) return new Electricity[] { new Electricity(0,0), new Electricity(0, 0), new Electricity(0, 0)};
                Time = Time % 0.02f;
                double Angle = Time * 50 * 2 * Math.PI;
                Electricity Fase1 = new Electricity((float)Math.Sin(Angle) * 380, Current * (float)Math.Sin(Angle) * 380 / Input().Voltage);
                Electricity Fase2 = new Electricity((float)Math.Sin(Angle + 2 * Math.PI / 3) * 380, Current * (float)Math.Sin(Angle + 2 * Math.PI / 3) * 380 / Input().Voltage);
                Electricity Fase3 = new Electricity((float)Math.Sin(Angle + 4 * Math.PI / 3) * 380, Current * (float)Math.Sin(Angle + 4 * Math.PI / 3) * 380 / Input().Voltage);
                return new Electricity[] { Fase1, Fase2, Fase3 };
            };
            this.Input = Input;
        }
    }
    public class Cargador_Bateria : Convertidor
    {
        static IIRFilter VFilter;
        protected float LastTime;
        protected float LastChargeTime;
        public Convertidor_3000Vcc_380Vca3 Input;
        public Electricity Get(float CurrentA, float Time)
        {
            Electricity input = new Electricity(0, 0);
            for (int i = 0; i < 3; i++)
            {
                Electricity e = Input.Get(CurrentA / 2.64f / 3, Time)[i];
                input.Voltage += Math.Abs(e.Voltage / 2.64f / 3); //Transformador y puente de diodos
                input.Current += Math.Abs(e.Current * 2.64f * 3);
            }
            input.Voltage = VFilter.Filter(input.Voltage, Time - LastTime); //Condensador
            input.Voltage = Math.Min(72, input.Voltage); //Zener
            LastTime = Time;
            return input;
        }
        public override void Update(float Time)
        {
            throw new NotImplementedException();
        }
        public void Charge(ref Battery bat, float Time)
        {
            bat.Add(Get(4, Time), Time);
            LastChargeTime = Time;
        }
        public Cargador_Bateria(Convertidor_3000Vcc_380Vca3 Input)
        {
            VFilter = new IIRFilter(IIRFilter.FilterTypes.Butterworth, 1, IIRFilter.HzToRad(0.7f), 0.001f);
            this.Input = Input;
        }
    }
    public class Toma_corriente_447 : ElectricPowerSupply
    {
        IIRFilter VoltageFilter;
        float RealLineVoltageV;
        Timer Rele_minima;
        bool Contactor_bateria = false;
        Battery Battery;
        Convertidor_3000Vcc_380Vca3 Convertidor_Estático;
        Cargador_Bateria Cargador_Batería;
        public override void Initialize()
        {
            VoltageFilter = new IIRFilter(IIRFilter.FilterTypes.Butterworth, 1, IIRFilter.HzToRad(0.7f), 0.001f);
            Battery.ChargePercent = 100;
            Battery.VoltageV = 72;
            Rele_minima = new Timer(this);
            Rele_minima.Setup(2f * 60f + 30);
            Convertidor_Estático = new Convertidor_3000Vcc_380Vca3(() => new Electricity(CurrentCircuitBreakerState() == CircuitBreakerState.Closed && Contactor_bateria ? RealLineVoltageV : 0, Acceleration / 0.7f * 315 + 15));
            Cargador_Batería = new Cargador_Bateria(Convertidor_Estático);
        }
        float SpeedMpS;
        float Acceleration = 0;
        float PreviousSpeed = 0;
        float PreviousDistance = 0;
        float elapsedTime = 0;
        PantographState PreviousState;
        public override void Update(float elapsedClockSeconds)
        {
            Convertidor_Estático.Update(ClockTime());
            elapsedTime += elapsedClockSeconds;
            if (elapsedTime>0.5)
            {
                SpeedMpS = (DistanceM() - PreviousDistance) / elapsedTime;
                Acceleration = Math.Max(Math.Min((SpeedMpS - PreviousSpeed) / elapsedTime, 0.7f), -0.7f);
                PreviousSpeed = SpeedMpS;
                PreviousDistance = DistanceM();
                elapsedTime = 0;
            }
            RealLineVoltageV = LineVoltageV() - Acceleration / 0.7f * 90f;
            if (CurrentPantographState() == PantographState.Down)
            {
                if(!Rele_minima.Triggered) Contactor_bateria = true;
                if(!Rele_minima.Started) Rele_minima.Start();
            }
            if ((CurrentPantographState() == PantographState.Up && PreviousState != PantographState.Up) || Battery.VoltageV >= 69) Rele_minima.Stop();
            PreviousState = CurrentPantographState();
            if (CurrentPantographState() == PantographState.Up && Convertidor_Estático.State != PowerSupplyState.PowerOn && !Rele_minima.Started) Rele_minima.Start();
            if (Rele_minima.Triggered) Contactor_bateria = false;
            if (CurrentCircuitBreakerState() == CircuitBreakerState.Open || RealLineVoltageV < 2700 || RealLineVoltageV > 3300 || Convertidor_Estático.State != PowerSupplyState.PowerOn)
            {
                SetCurrentState(PowerSupplyState.PowerOff);
            }
            else
            {
                SetCurrentState(PowerSupplyState.PowerOn);
            }
            if (Contactor_bateria && Battery.VoltageV > 56) SetCurrentAuxiliaryState(PowerSupplyState.PowerOn);
            else SetCurrentAuxiliaryState(PowerSupplyState.PowerOff);
            if (Convertidor_Estático.State != PowerSupplyState.PowerOn && CurrentAuxiliaryState()==PowerSupplyState.PowerOn && Contactor_bateria) Battery.Get(1800f, elapsedClockSeconds);
            if (Convertidor_Estático.State == PowerSupplyState.PowerOn && Contactor_bateria) Cargador_Batería.Charge(ref Battery, ClockTime());
            SetFilterVoltageV(CurrentCircuitBreakerState() == CircuitBreakerState.Closed ? RealLineVoltageV : Battery.VoltageV);
        }
    }
}