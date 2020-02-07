//Scripts de sistemas de seguridad Españoles
//César Benito Lamata
//Versión 3.0
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
namespace ORTS.Scripting.Script
{
    public class TCS_Spain : TrainControlSystem
    {
        public enum CCS
        {
            ASFA,
            LZB,
            EBICAB,
            ETCS,
            EXT
        }
        public CCS ActiveCCS;

        enum Tipo_ASFA
        {
            Basico,
            ASFA200,
            Refuerzo,
            Digital,
        }
        Tipo_ASFA TipoASFA;

        bool ASFAInstalled;
        bool ASFADigitalInstalled;
        bool ETCSInstalled;
        bool ATFInstalled;
        bool ASFAActivated;
        bool LZBInstalled;
        bool LZBActivated;
        bool HMInhibited = false;

        public float TrainMaxSpeed;

        float VelocidadPrefijada = 0;

        public int SerieTren;

        //ETCS
        public int ETCSInstalledLevel;

        //ATF
        float LastMpS;
        float LastTime;
        float ATFAcceleration = 0;
        float ATFThrottle = 0;
        float ATFBrake = 0;
        bool ATFActivated = false;
        float ATFSpeed = 0;

        bool ExternalEmergencyBraking;

        float PreviousSignalDistanceM = 0f;
        float PreviousPostDistanceM = 0f;
        public bool SignalPassed = false;
        public bool PreviaPassed = false;
        bool IntermediateDist = false;
        public bool IntermediateLTVDist = false;
        bool IntermediatePreLTVDist = false;
        public bool PostPassed = false;
        public bool AnuncioLTVPassed = false;
        public bool PreanuncioLTVPassed = false;
        public float PreviaDistance = 300f;
        public int PreviaSignalNumber;
        public float AnuncioDistance = 1500f;
        Aspect CurrentSignalAspect;

        public bool ETCSPressed;
        /*bool ETCSPrPressed
        {
            get
            {
                if (ETCSButtonPressed)
                {
                    //ETCSButtonPressed = false;
                    return true;
                }
                else return false;
            }
            set { ETCSButtonPressed = value; }
        }*/

        public TCS_Spain() { }
        public ETCS ETCS;
        public ASFA ASFA;
        public LZB LZB;
        public HM HM;
        public Serial serial;
        public HMSerial hmserial;
        public override void Initialize()
        {
            ASFAInstalled = GetBoolParameter("General", "ASFA", true);
            ETCSInstalled = GetBoolParameter("General", "ETCS", false);
            HMInhibited = GetBoolParameter("General", "HMInhibited", true);
            ATFActivated = ATFInstalled = GetBoolParameter("General", "ATO", false) || GetBoolParameter("General", "ATF", false);
            LZBActivated = LZBInstalled = GetBoolParameter("General", "LZB", false);
            ASFADigitalInstalled = GetBoolParameter("ASFA", "Digital", false);
            ETCSInstalledLevel = GetIntParameter("ETCS", "Level", 0);
            if (ASFAInstalled)
            {
                ActiveCCS = CCS.ASFA;
                if (ASFADigitalInstalled)
                {
                    TipoASFA = Tipo_ASFA.Digital;
                    ASFA = new ASFADigital(this);
                }
                else
                {
                    if (TrainMaxSpeed <= MpS.FromKpH(160f))
                    {
                        TipoASFA = Tipo_ASFA.Basico;
                        ASFA = new ASFAOriginal(this);
                    }
                    else
                    {
                        TipoASFA = Tipo_ASFA.ASFA200;
                        ASFA = new ASFA200(this);
                    }
                }
            }
            if (LZBInstalled)
            {
                ActiveCCS = CCS.LZB;
                LZB = new LZB(this);
            }
            if (ETCSInstalled)
            {
                ActiveCCS = CCS.ETCS;
                ETCS = new ETCS(this);
            }
            if (!HMInhibited)
            {
                HM = new HM(this);
                HM.Initialize();
            }
            TrainMaxSpeed = MpS.FromKpH(GetFloatParameter("General", "MaxSpeed", 380f));
            SerieTren = GetIntParameter("General", "Serie", 440);

            Activated = true;
            PreviousSignalDistanceM = 0f;
            PreviousPostDistanceM = 0f;

            if (ASFA != null)
            {
                if (ASFA is ASFAclasico) (ASFA as ASFAclasico).Initialize();
                else (ASFA as ASFADigital).Conex();
            }
            if (ETCS != null) ETCS.Initialize();
            if (LZB != null) LZB.Initialize();
            serial = new Serial(115200, this, "COM5");
            if(HM!=null) hmserial = new HMSerial(115200, this, "COM6");
        }
        public bool LineaConvencional;
        public override void Update()
        {
            LineaConvencional = true;
            if (CurrentPostSpeedLimitMpS() > MpS.FromKpH(200)) LineaConvencional = false;
            for (int i = 0; i < 5; i++)
            {
                if (NextPostSpeedLimitMpS(i) > MpS.FromKpH(200)) LineaConvencional = false;
            }
            UpdateSignalPassed();
            UpdateDistanciaPrevia();
            UpdatePostPassed();
            UpdateAnuncioLTVPassed();
            if (HM != null)
            {
                HM.Activated = (ASFA == null || !ASFA.Connected()) && (ETCS == null || !ETCS.Activated) && !ATFActivated;
                HM.Update();
                hmserial.poll();
            }
            else if (AlerterSound()) SetVigilanceAlarm(false);
            if(IsTrainControlEnabled()) serial.poll();
            if (!IsTrainControlEnabled())
            {
                if (ASFA is ASFADigital) (ASFA as ASFADigital).Connected = false;
                else (ASFA as ASFAclasico).Activated = false;
                if (ETCS != null) ETCS.CurrentMode = ETCS.Mode.NL;
            }
            if (serial.Port != null && TipoASFA != Tipo_ASFA.Digital) ActiveCCS = CCS.EXT;
            if (ASFAInstalled && (ActiveCCS != CCS.EXT || ASFA is ASFADigital))
            {
                ASFA.Update();
            }
            if (LZBInstalled)
            {
                LZB.Update();
                if (LZB.LZBSupervising)
                {
                    ActiveCCS = CCS.LZB;
                    if (ASFAInstalled)
                    {
                        if (ASFA is ASFADigital)
                        {
                            ((ASFADigital)ASFA).AKT = true;
                            ((ASFADigital)ASFA).CON = false;
                        }
                    }
                }
                else if (ASFAInstalled)
                {
                    ActiveCCS = CCS.ASFA;
                    ((ASFADigital)ASFA).AKT = false;
                    ((ASFADigital)ASFA).CON = true;
                }
            }
            if (ETCSInstalled) ETCS.Update();
            if (ATFInstalled)
            {
                if (ATFActivated) ATFActivated = false;
                ATFSpeed = float.MaxValue;
#if _OR_PERS
                VelocidadPrefijada = Locomotive.ThrottleController.CurrentValue * TrainMaxSpeed;
#endif
                if (LZB != null && LZB.LZBSupervising && (BrakePipePressureBar() > 4.8 || LZB.LZBEmergencyBrake || ATFFullBrake))
                {
                    ATFActivated = true;
                    ATFSpeed = LZB.LZBMaxSpeed;
                    if (LZB.LZBTargetDistance < 15 && SpeedMpS() < 4 && LZB.LZBTargetSpeed == 0) ATFSpeed = 0;
                }
                if (ETCS != null && ETCS.CurrentMode == ETCS.Mode.FS && ETCS.Vperm > ETCS.Vrelease && SpeedMpS() > 0.1 && (BrakePipePressureBar() > 4.8 || ETCS.EmergencyBraking || ETCS.ServiceBrake || ATFFullBrake))
                {
                    ATFActivated = true;
                    ATFSpeed = ETCS.Vperm > ETCS.Vtarget ? ETCS.Vperm - 1 : ETCS.Vperm;
                }
                if (VelocidadPrefijada > 1)
                {
                    ATFActivated = true;
                    ATFSpeed = Math.Min(VelocidadPrefijada, ATFSpeed);
                    if (ActiveCCS != CCS.ETCS && ActiveCCS != CCS.LZB) SetCurrentSpeedLimitMpS(VelocidadPrefijada);
                }
                if (ATFActivated && VelocidadPrefijada < 1 && (ETCS == null || ETCS.CurrentMode != ETCS.Mode.FS || SpeedMpS() < 0.1 || ETCS.Vrelease > ETCS.Vperm || (BrakePipePressureBar() < 4.8 && !ETCS.EmergencyBraking && !ETCS.ServiceBrake && !ATFFullBrake)) && (LZB == null || !LZB.LZBSupervising /*|| SpeedMpS() < 1 */|| (BrakePipePressureBar() < 4.8 && !LZB.LZBEmergencyBrake && !ATFFullBrake)))
                {
                    ATFActivated = false;
                    ATFFullBrake = false;
                    SetThrottleController(0);
#if _OR_PERS
                    Locomotive.DynamicBrakeIntervention = -1;
                    Locomotive.ThrottleIntervention = 0;
                    Locomotive.TrainBrakeIntervention = 0;
#endif
                }
                if (ATFActivated) ATF(ATFSpeed);
            }
            SetPenaltyApplicationDisplay((ASFA != null && ASFA.Urgencia()) || (ETCS != null && (ETCS.EmergencyBraking || ETCS.ServiceBrake)) || (LZB != null && LZB.LZBOE));
            SetFullBrake((ETCS != null && ETCS.ServiceBrake && !ETCS.EmergencyBraking) || ATFFullBrake);
            SetEmergencyBrake((ASFA != null && ASFA.Urgencia()) || (ETCS != null && ETCS.EmergencyBraking) || (HM != null && HM.HMEmergencyBraking) || (LZB != null && LZB.LZBEmergencyBrake));
            SetTractionAuthorization(((ASFA == null || !ASFA.Urgencia()) && (ETCS == null || (!ETCS.TCO && !ETCS.EmergencyBraking && !ETCS.ServiceBrake)) && (HM == null || !HM.HMEmergencyBraking) && (LZB == null || !LZB.LZBEmergencyBrake)) && (LocomotiveBrakeCylinderPressureBar() < BrakeCutsPowerAtBrakeCylinderPressureBar() || !DoesBrakeCutPower()));
            bool ETCSNeutralZone = false;
            bool ETCSLowerPantographs = false;
            SetPowerAuthorization(!ETCSNeutralZone);
            if (ETCSLowerPantographs) SetPantographsDown();
        }
        public class OnBoardMA
        {
            public int Q_DIR;
            public int L_PACKET;
            public int Q_SCALE;
            public float V_MAIN;
            public float V_LOA;
            public Timer T_LOA;
            public int N_ITER;
            public OdoMeter[] L_SECTION;
            public bool[] Q_SECTIONTIMER;
            public Timer[] T_SECTIONTIMER;
            public OdoMeter[] D_SECTIONTIMERSTOPLOC;
            public OdoMeter L_ENDSECTION;
            public bool Q_ENDTIMER;
            public Timer T_ENDTIMER;
            public OdoMeter D_ENDTIMERSTARTLOC;
            public bool Q_DANGERPOINT;
            public OdoMeter D_DP;
            public float V_RELEASEDP;
            public bool Q_OVERLAP;
            public OdoMeter D_STARTOL;
            public Timer T_OL;
            public OdoMeter D_OL;
            public float V_RELEASEOL;
            public OnBoardMA(MovementAuthority MA, AbstractScriptClass asc)
            {
                Q_DIR = MA.Q_DIR;
                L_PACKET = MA.L_PACKET;
                Q_SCALE = MA.Q_SCALE;
                V_MAIN = MpS.FromKpH(MA.V_MAIN * 5);
                V_LOA = MpS.FromKpH(MA.V_LOA * 5);
                T_LOA = new Timer(asc);
                T_LOA.Setup(MA.T_LOA);
                N_ITER = MA.L_SECTION.Length;
                L_SECTION = new OdoMeter[N_ITER];
                Q_SECTIONTIMER = new bool[N_ITER];
                T_SECTIONTIMER = new Timer[N_ITER];
                D_SECTIONTIMERSTOPLOC = new OdoMeter[N_ITER];
                for (int i = 0; i < N_ITER; i++)
                {
                    L_SECTION[i] = new OdoMeter(asc);
                    L_SECTION[i].Setup((float)(MA.L_SECTION[i] * Math.Pow(10, Q_SCALE - 1)));
                    T_SECTIONTIMER[i] = new Timer(asc);
                    T_SECTIONTIMER[i].Setup(MA.T_SECTIONTIMER[i]);
                    D_SECTIONTIMERSTOPLOC[i] = new OdoMeter(asc);
                    D_SECTIONTIMERSTOPLOC[i].Setup((float)(MA.D_SECTIONTIMERSTOPLOC[i] * Math.Pow(10, Q_SCALE - 1)));
                }
                L_ENDSECTION = new OdoMeter(asc);
                L_ENDSECTION.Setup((float)(MA.L_ENDSECTION * Math.Pow(10, Q_SCALE - 1)));
                Q_ENDTIMER = Convert.ToBoolean(MA.Q_ENDTIMER);
                T_ENDTIMER = new Timer(asc);
                T_ENDTIMER.Setup(MA.T_ENDTIMER);
                D_ENDTIMERSTARTLOC = new OdoMeter(asc);
                D_ENDTIMERSTARTLOC.Setup((float)(MA.D_ENDTIMERSTARTLOC * Math.Pow(10, Q_SCALE - 1)));
                Q_DANGERPOINT = Convert.ToBoolean(MA.Q_DANGERPOINT);
                D_DP = new OdoMeter(asc);
                D_DP.Setup((float)(MA.D_DP * Math.Pow(10, Q_SCALE - 1)));
                V_RELEASEDP = MpS.FromKpH(MA.V_RELEASEDP * 5);
                Q_OVERLAP = Convert.ToBoolean(MA.Q_OVERLAP);
                D_STARTOL = new OdoMeter(asc);
                D_STARTOL.Setup((float)(MA.D_STARTOL * Math.Pow(10, Q_SCALE - 1)));
                T_OL = new Timer(asc);
                T_OL.Setup(MA.T_OL);
                D_OL = new OdoMeter(asc);
                D_OL.Setup((float)(MA.D_OL * Math.Pow(10, Q_SCALE - 1)));
                V_RELEASEOL = MpS.FromKpH(MA.V_RELEASEOL * 5);
            }
            public void Update()
            {
                N_ITER = L_SECTION.Length;
                if (N_ITER > 0)
                {
                    if (!L_SECTION[0].Started)
                    {
                        L_SECTION[0].Start();
                        T_SECTIONTIMER[0].Start();
                        D_SECTIONTIMERSTOPLOC[0].Start();
                    }
                    for (int i = 0; i < N_ITER - 1; i++)
                    {
                        if (L_SECTION[i].Triggered && !L_SECTION[i + 1].Started)
                        {
                            L_SECTION[i + 1].Start();
                            T_SECTIONTIMER[i + 1].Start();
                            D_SECTIONTIMERSTOPLOC[i + 1].Start();
                        }
                        if (D_SECTIONTIMERSTOPLOC[i].Triggered) T_SECTIONTIMER[i].Stop();
                    }
                }
                else V_MAIN = 0;
            }
            public void ExtendInfill(MovementAuthority MA, float Distance, AbstractScriptClass asc)
            {
                Q_DIR = MA.Q_DIR;
                L_PACKET = MA.L_PACKET;
                Q_SCALE = MA.Q_SCALE;
                V_MAIN = MpS.FromKpH(MA.V_MAIN * 5);
                V_LOA = MpS.FromKpH(MA.V_LOA * 5);
                T_LOA = new Timer(asc);
                T_LOA.Setup(MA.T_LOA);
                float Dist = 0;
                for (int i = 0; i < L_SECTION.Length; i++)
                {
                    if ((L_SECTION[i].Started ? L_SECTION[i].RemainingValue : L_SECTION[i].AlarmValue) + Dist >= Distance || i + 1 == L_SECTION.Length)
                    {
                        N_ITER = i + MA.L_SECTION.Length + 1;
                        var l = new OdoMeter[N_ITER];
                        var q = new bool[N_ITER];
                        var t = new Timer[N_ITER];
                        var d = new OdoMeter[N_ITER];
                        for(int a=0; a<i; a++)
                        {
                            l[a] = L_SECTION[a];
                            t[a] = T_SECTIONTIMER[a];
                            d[a] = D_SECTIONTIMERSTOPLOC[a];
                        }
                        l[i] = new OdoMeter(asc);
                        l[i].Setup(Distance - Dist);
                        d[i] = D_SECTIONTIMERSTOPLOC[i];
                        t[i] = T_SECTIONTIMER[i];
                        for(int a=0; a + i + 1<l.Length; a++)
                        {
                            l[a + i + 1] = new OdoMeter(asc);
                            l[a + i + 1].Setup((float)(MA.L_SECTION[a] * Math.Pow(10, Q_SCALE - 1)));
                            t[a + i + 1] = new Timer(asc);
                            t[a + i + 1].Setup(MA.T_SECTIONTIMER[a]);
                            d[a + i + 1] = new OdoMeter(asc);
                            d[a + i + 1].Setup((float)(MA.D_SECTIONTIMERSTOPLOC[a] * Math.Pow(10, Q_SCALE - 1)));
                        }
                        L_SECTION = l;
                        T_SECTIONTIMER = t;
                        Q_SECTIONTIMER = q;
                        D_SECTIONTIMERSTOPLOC = d;
                        break;
                    }
                    else Dist += L_SECTION[i].Started ? L_SECTION[i].RemainingValue : L_SECTION[i].AlarmValue;
                }
                L_ENDSECTION = new OdoMeter(asc);
                L_ENDSECTION.Setup((float)(MA.L_ENDSECTION * Math.Pow(10, Q_SCALE - 1)));
                Q_ENDTIMER = Convert.ToBoolean(MA.Q_ENDTIMER);
                T_ENDTIMER = new Timer(asc);
                T_ENDTIMER.Setup(MA.T_ENDTIMER);
                D_ENDTIMERSTARTLOC = new OdoMeter(asc);
                D_ENDTIMERSTARTLOC.Setup((float)(MA.D_ENDTIMERSTARTLOC * Math.Pow(10, Q_SCALE - 1)));
                Q_DANGERPOINT = Convert.ToBoolean(MA.Q_DANGERPOINT);
                D_DP = new OdoMeter(asc);
                D_DP.Setup((float)(MA.D_DP * Math.Pow(10, Q_SCALE - 1)));
                V_RELEASEDP = MpS.FromKpH(MA.V_RELEASEDP * 5);
                Q_OVERLAP = Convert.ToBoolean(MA.Q_OVERLAP);
                D_STARTOL = new OdoMeter(asc);
                D_STARTOL.Setup((float)(MA.D_STARTOL * Math.Pow(10, Q_SCALE - 1)));
                T_OL = new Timer(asc);
                T_OL.Setup(MA.T_OL);
                D_OL = new OdoMeter(asc);
                D_OL.Setup((float)(MA.D_OL * Math.Pow(10, Q_SCALE - 1)));
                V_RELEASEOL = MpS.FromKpH(MA.V_RELEASEOL * 5);
            }
        }
        public class StaticSpeedProfile
        {
            public int Q_DIR;
            public int L_PACKET;
            public int Q_SCALE;
            public int N_ITER;
            public OdoMeter[] D_STATIC;
            public float[] V_STATIC;
            public bool[] Q_FRONT;
            public int[] Q_DIFF;
            public int[] NC_CDDIFF;
            public int[] NC_DIFF;
            public int[] V_DIFF;
            public StaticSpeedProfile(InternationalStaticSpeedProfile SSP, TrainControlSystem tcs)
            {
                Q_DIR = SSP.Q_DIR;
                L_PACKET = SSP.L_PACKET;
                Q_SCALE = SSP.Q_SCALE;
                N_ITER = SSP.D_STATIC.Length;
                D_STATIC = new OdoMeter[N_ITER];
                V_STATIC = new float[N_ITER];
                Q_FRONT = new bool[N_ITER];
                for (int i = 0; i < N_ITER; i++)
                {
                    D_STATIC[i] = new OdoMeter(tcs);
                    D_STATIC[i].Setup((float)(SSP.D_STATIC[i] * Math.Pow(10, Q_SCALE - 1)));
                    D_STATIC[i].Start();
                    V_STATIC[i] = MpS.FromKpH(SSP.V_STATIC[i] * 5);
                    Q_FRONT[i] = SSP.Q_FRONT[i] == 1 ? true : false;
                }
            }
            public void ExtendInfill(InternationalStaticSpeedProfile SSP, TrainControlSystem tcs)
            {
                int i;
                for(i=0; i < N_ITER; i++)
                {
                    if (V_STATIC[i] == SSP.V_STATIC[0] && D_STATIC[i + 1].RemainingValue == SSP.D_STATIC[1]) break;
                }
                var olddstatic = D_STATIC;
                var oldvstatic = V_STATIC;
                var oldqfront = Q_FRONT;
                N_ITER = SSP.D_STATIC.Length + i;
                V_STATIC = new float[N_ITER];
                D_STATIC = new OdoMeter[N_ITER];
                Q_FRONT = new bool[N_ITER];
                for (int a = 0; a < N_ITER; a++)
                {
                    if(a<i)
                    {
                        D_STATIC[a] = olddstatic[i];
                        V_STATIC[a] = oldvstatic[i];
                        Q_FRONT[a] = oldqfront[i];
                    }
                    else
                    {
                        D_STATIC[a] = new OdoMeter(tcs);
                        D_STATIC[a].Setup((float)(SSP.D_STATIC[a-i] * Math.Pow(10, Q_SCALE - 1)));
                        D_STATIC[a].Start();
                        V_STATIC[a] = MpS.FromKpH(SSP.V_STATIC[a-i] * 5);
                        Q_FRONT[a] = Convert.ToBoolean(SSP.Q_FRONT[a-i]);

                    }
                }
            }
        }
        bool ATFFullBrake;
        double LastError = 0;
        double i_error = 0;
        protected void ATF(double limit)
		{
            limit = limit - MpS.FromKpH(1);
            double error = limit-SpeedMpS();
            double dt = ClockTime()-LastTime;
            double p_out = 3*error;
            i_error += (error+LastError)*dt;
            double d_error = (error-LastError)/dt;
            double i_out = 0*i_error;
            double d_out = 0.5*d_error;
            double diff = d_out+p_out+i_out;
            ATFThrottle = diff>0 ? Math.Min((float)diff,100) : 0;
            ATFBrake = diff<0 ? -(float)diff : 0;
            SetThrottleController(ATFThrottle);
            try
            {
                ATFFullBrake = (diff<-1 && d_out<0.5);
                SetDynamicBrakeController(Math.Min(ATFBrake, 1));
            }
            catch (Exception)
            {
                if (ATFBrake >= 1) SetEmergencyBrake(true);
                else if (ATFBrake > 0.3) ATFFullBrake = true;
            }
            LastTime = ClockTime();
            LastError = error;
		}
		public override void SetEmergency(bool emergency)
        {
            ExternalEmergencyBraking = emergency;
        }
        bool Pressed;
		public override void HandleEvent(TCSEvent evt, string message)
   		{
            if (evt == TCSEvent.AlerterPressed) Pressed = true;
            if (evt == TCSEvent.AlerterReleased) Pressed = false;
            if (ASFA is ASFAclasico) (ASFA as ASFAclasico).HandleEvent(evt, message);
            if (ETCS != null) ETCS.HandleEvent(evt, message);
            if (LZB != null) LZB.HandleEvent(evt, message);
            if (HM != null) HM.HandleEvent(evt, message);
        }
		protected void UpdateSignalPassed()
        {
            SignalPassed = (NextSignalDistanceM(0) > PreviousSignalDistanceM+20)&&(SpeedMpS()>0.1f);
            PreviousSignalDistanceM = NextSignalDistanceM(0);
            if (SignalPassed && NextSignalAspect(0) == Aspect.None) SignalPassed = false;
            UpdateSignalBalisePassed();
        }
        public bool SBalisePassed;
        bool IDist2;
        protected void UpdateSignalBalisePassed()
        {
            if (SBalisePassed) IDist2 = true;
            if (SignalPassed) IDist2 = false;
            SBalisePassed = NextSignalDistanceM(0) < 10 && !SignalPassed && !IDist2 && SpeedMpS() > 0.1f;
        }
        protected void UpdatePreviaPassed()
		{
			if(PreviaPassed) IntermediateDist = true;
            if (SignalPassed && (CurrentSignalAspect != Aspect.Clear_2 || CurrentSignalSpeedLimitMpS() < MpS.FromKpH(155f) || CurrentSignalSpeedLimitMpS() > MpS.FromKpH(165f) || !IsPN ) && (CurrentSignalAspect != Aspect.Approach_1 || CurrentSignalSpeedLimitMpS() < MpS.FromKpH(25f) || CurrentSignalSpeedLimitMpS() > MpS.FromKpH(35f) || !IsPN)) IntermediateDist = false;
			PreviaPassed = NextSignalDistanceM(PreviaSignalNumber)<PreviaDistance && NextSignalDistanceM(PreviaSignalNumber)>PreviaDistance-10 && !IntermediateDist&&SpeedMpS()>0.1f;
            if (NextSignalAspect(1) == Aspect.None && NextSignalAspect(0) == Aspect.Stop )
            {
                PreviaPassed = false;
            }
            if (NextSignalAspect(0) == Aspect.None || NextSignalAspect(1) == Aspect.None) TriggerSoundAlert1();
        }
        public bool IsLTV;
        public bool IsPN;
        protected void UpdateDistanciaPrevia()
        {
            if (SignalPassed)
            {
                if ((NextSignalAspect(0) == Aspect.Clear_2 && NextSignalSpeedLimitMpS(0) < MpS.FromKpH(165f) && NextSignalSpeedLimitMpS(0) > MpS.FromKpH(155f)) || (NextSignalAspect(0) == Aspect.Approach_1 && NextSignalSpeedLimitMpS(0) < MpS.FromKpH(35f) && NextSignalSpeedLimitMpS(0) > MpS.FromKpH(25f)))
                {
                    PreviaSignalNumber = 1;
                    IsPN = true;
                }
                else PreviaSignalNumber = 0;
                if ((CurrentSignalSpeedLimitMpS() > 165f || CurrentSignalSpeedLimitMpS() < 155f || CurrentSignalAspect != Aspect.Clear_2) && ((CurrentSignalSpeedLimitMpS() > 35f || CurrentSignalSpeedLimitMpS() < 25f || CurrentSignalAspect != Aspect.Approach_1)))
                {
                    if (NextSignalDistanceM(PreviaSignalNumber) < 100f)
                    {
                        PreviaDistance = 0f;
                    }
                    else if (NextSignalDistanceM(PreviaSignalNumber) < 400f)
                    {
                        PreviaDistance = 50f;
                    }
                    else if (NextSignalDistanceM(PreviaSignalNumber) < 700f)
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
                    if (NextSignalDistanceM(0) < 100f)
                    {
                        PreviaDistance = 0f;
                    }
                    else if (NextSignalDistanceM(0) < 700f)
                    {
                        PreviaDistance = 100f;
                    }
                    else if (NextSignalDistanceM(0) < 1000f)
                    {
                        PreviaDistance = 300f;
                    }
                    else
                    {
                        PreviaDistance = 500f;
                    }
                }
            }
            else CurrentSignalAspect = NextSignalAspect(0);
            if ((NextSignalAspect(0) == Aspect.Clear_2 && NextSignalSpeedLimitMpS(0) < MpS.FromKpH(165f) && NextSignalSpeedLimitMpS(0) > MpS.FromKpH(155f)) || (NextSignalAspect(0) == Aspect.Approach_1 && NextSignalSpeedLimitMpS(0) < MpS.FromKpH(35f) && NextSignalSpeedLimitMpS(0) > MpS.FromKpH(25f))) IsPN = true;
            if (!SignalPassed && (ASFA==null/* || !ASFA.Rec.Started*/) && IsPN && !((NextSignalAspect(0) == Aspect.Clear_2 && NextSignalSpeedLimitMpS(0) < MpS.FromKpH(165f) && NextSignalSpeedLimitMpS(0) > MpS.FromKpH(155f)) || (NextSignalAspect(0) == Aspect.Approach_1 && NextSignalSpeedLimitMpS(0) < MpS.FromKpH(35f) && NextSignalSpeedLimitMpS(0) > MpS.FromKpH(25f)))) IsPN = false;
            if (PreviaDistance != 0f) UpdatePreviaPassed();
            else PreviaPassed = false;
        }
		protected void UpdatePostPassed()
        {
            PostPassed = (NextPostDistanceM(0) > PreviousPostDistanceM+20)&&(SpeedMpS()>0);
            PreviousPostDistanceM = NextPostDistanceM(0);
        }	
		protected void UpdateAnuncioLTVPassed()
		{
			if(AnuncioLTVPassed) IntermediateLTVDist = true;
			if(PostPassed) IntermediateLTVDist = false;
			AnuncioLTVPassed = NextPostDistanceM(0)<AnuncioDistance && NextPostDistanceM(0)>AnuncioDistance-10f && !IntermediateLTVDist && (CurrentPostSpeedLimitMpS()-NextPostSpeedLimitMpS(0))>=MpS.FromKpH(40f);
			//AnuncioLTVPassed = AnuncioLTVPassed || (NextPostDistanceM(1)<AnuncioDistance && NextPostDistanceM(1)>AnuncioDistance-10f && !IntermediateLTVDist && (CurrentPostSpeedLimitMpS()-NextPostSpeedLimitMpS(1))>=MpS.FromKpH(40f));
		}
		protected void UpdatePreanuncioLTVPassed()
		{
			if(PreanuncioLTVPassed) IntermediatePreLTVDist = true;
			if(PostPassed) IntermediatePreLTVDist = false;
			PreanuncioLTVPassed = NextPostDistanceM(0)<AnuncioDistance+1500f && NextPostDistanceM(0)>AnuncioDistance+1490f && (CurrentPostSpeedLimitMpS()-NextPostSpeedLimitMpS(0))>MpS.FromKpH(40f) && CurrentPostSpeedLimitMpS()>=MpS.FromKpH(160f) && !IntermediatePreLTVDist;
			PreanuncioLTVPassed = PreanuncioLTVPassed || (NextPostDistanceM(1)<AnuncioDistance+1500f && NextPostDistanceM(1)>AnuncioDistance+1490f && (CurrentPostSpeedLimitMpS()-NextPostSpeedLimitMpS(1))>MpS.FromKpH(40f) && CurrentPostSpeedLimitMpS()>=MpS.FromKpH(160f) && !IntermediatePreLTVDist);
		}
	}
    public class Packet
    {
        public int NID_PACKET;
        public Packet(int nid_packet)
        {
            NID_PACKET = nid_packet;
        }
    }
	public class EurobaliseTelegram
	{
		public int Q_UPDOWN;
		public int M_VERSION;
		public int Q_MEDIA;
		public int N_PIG;
		public int N_TOTAL;
		public int M_DUP;
		public int M_MCOUNT;
		public int NID_C;
		public int NID_BG;
		public int Q_LINK;
		public Packet[] packet;
		public EurobaliseTelegram(int NID_BG, int Q_LINK, params Packet[] packets)
        {
            this.NID_BG = NID_BG;
            this.Q_LINK = Q_LINK;
			packet = new Packet[packets.Length+1];
			for(int i=0; i<packets.Length; i++)
			{
				packet[i]=packets[i];
			}
			packet[packet.Length-1]=new Packet(255);
		}
    }
    public class EuroradioMessage
    {
        public int NID_MESSAGE;
        public int L_MESSAGE;
        public int T_TRAIN;
        public EuroradioMessage(int NID_MESSAGE)
        {
            this.NID_MESSAGE = NID_MESSAGE;
        }
    }
    public class TrainToTrackEuroradioMessage : EuroradioMessage
    {
        public int NID_ENGINE;
        public TrainToTrackEuroradioMessage(int NID_MESSAGE) : base(NID_MESSAGE) { }
    }
    public class TrackToTrainEuroradioMessage : EuroradioMessage
    {
        public int M_ACK;
        public int NID_LRBG;
        public TrackToTrainEuroradioMessage(int NID_MESSAGE) : base(NID_MESSAGE) { }
    }
    public class RadioMA : TrackToTrainEuroradioMessage
    {
        public L2MA MA;
        public Packet[] OptionalPackets;
        public RadioMA(int T_TRAIN, int M_ACK, int NID_LRBG, L2MA MA, params Packet[] OptionalPackets) : base(3)
        {
            this.T_TRAIN = T_TRAIN;
            this.M_ACK = M_ACK;
            this.NID_LRBG = NID_LRBG;
            this.MA = MA;
            this.OptionalPackets = OptionalPackets;
        }
    }
    public class UnconditionalEmergencyStop : TrackToTrainEuroradioMessage
    {
        public UnconditionalEmergencyStop() : base(16){}
    }
    public class MARequest : TrainToTrackEuroradioMessage
    {
        public int Q_MARQSTREASON;
        Packet pck = new Packet(1);
        public MARequest() : base(137)
        {

        }
    }
    public abstract class MovementAuthority : Packet
    {
        public int Q_DIR;
        public int L_PACKET;
        public int Q_SCALE;
        public int V_MAIN;
        public int V_LOA;
        public int T_LOA;
        public int N_ITER;
        public int[] L_SECTION;
        public int[] Q_SECTIONTIMER;
        public int[] T_SECTIONTIMER;
        public int[] D_SECTIONTIMERSTOPLOC;
        public int L_ENDSECTION;
        public int Q_ENDTIMER;
        public int T_ENDTIMER;
        public int D_ENDTIMERSTARTLOC;
        public int Q_DANGERPOINT;
        public int D_DP;
        public int V_RELEASEDP;
        public int Q_OVERLAP;
        public int D_STARTOL;
        public int T_OL;
        public int D_OL;
        public int V_RELEASEOL;
        public MovementAuthority(int nid) : base(nid) { }
    }
    public class L1MA : MovementAuthority
	{
		public L1MA(int q_dir, int l_packet, int q_scale, int v_main, int v_loa, int t_loa, int n_iter, int[] l_section, int[] q_sectiontimer, int[] t_sectiontimer, int[] d_sectiontimerstoploc, int l_endsection, int q_endtimer, int t_endtimer, int d_endtimerstartloc, int q_dangerpoint, int d_dp, int v_releasedp, int q_overlap, int d_startol, int t_ol, int d_ol, int v_releaseol) : base(12)
		{
			Q_DIR=q_dir;
			L_PACKET=l_packet;
			Q_SCALE=q_scale;
			V_MAIN=v_main;
			V_LOA=v_loa;
			T_LOA=t_loa;
			N_ITER=n_iter;
			L_SECTION=l_section;
			Q_SECTIONTIMER=q_sectiontimer;
			T_SECTIONTIMER=t_sectiontimer;
			D_SECTIONTIMERSTOPLOC=d_sectiontimerstoploc;
			L_ENDSECTION=l_endsection;
			Q_ENDTIMER=q_endtimer;
			T_ENDTIMER=t_endtimer;
			D_ENDTIMERSTARTLOC=d_endtimerstartloc;
			Q_DANGERPOINT=q_dangerpoint;
			D_DP=d_dp;
			V_RELEASEDP=v_releasedp;
			Q_OVERLAP=q_overlap;
			D_STARTOL=d_startol;
			T_OL=t_ol;
			D_OL=d_ol;
			V_RELEASEOL=v_releaseol;
			if(V_MAIN==0||L_SECTION.Length==1) N_ITER=0;
		}
	}
	public class L2MA : MovementAuthority
	{
        public L2MA(int q_dir, int l_packet, int q_scale, int v_main, int v_loa, int t_loa, int n_iter, int[] l_section, int[] q_sectiontimer, int[] t_sectiontimer, int[] d_sectiontimerstoploc, int l_endsection, int q_endtimer, int t_endtimer, int d_endtimerstartloc, int q_dangerpoint, int d_dp, int v_releasedp, int q_overlap, int d_startol, int t_ol, int d_ol, int v_releaseol) : base(15)
		{
			Q_DIR=q_dir;
			L_PACKET=l_packet;
			Q_SCALE=q_scale;
			V_MAIN=v_main;
			V_LOA=v_loa;
			T_LOA=t_loa;
			N_ITER=n_iter;
			L_SECTION=l_section;
			Q_SECTIONTIMER=q_sectiontimer;
			T_SECTIONTIMER=t_sectiontimer;
			D_SECTIONTIMERSTOPLOC=d_sectiontimerstoploc;
			L_ENDSECTION=l_endsection;
			Q_ENDTIMER=q_endtimer;
			T_ENDTIMER=t_endtimer;
			D_ENDTIMERSTARTLOC=d_endtimerstartloc;
			Q_DANGERPOINT=q_dangerpoint;
			D_DP=d_dp;
			V_RELEASEDP=v_releasedp;
			Q_OVERLAP=q_overlap;
			D_STARTOL=d_startol;
			T_OL=t_ol;
			D_OL=d_ol;
			V_RELEASEOL=v_releaseol;
			if(V_MAIN==0||L_SECTION.Length==1) N_ITER=0;
		}
	}
	public class InternationalStaticSpeedProfile : Packet
	{
        public int Q_DIR;
        public int L_PACKET;
        public int Q_SCALE;
        public int[] D_STATIC;
        public int[] V_STATIC;
        public int[] Q_FRONT;
        public int[] N_ITER;
        public int[] Q_DIFF;
        public int[] NC_CDDIFF;
        public int[] NC_DIFF;
        public int[] V_DIFF;
		public InternationalStaticSpeedProfile( int q_dir, int l_packet, int q_scale, int[] d_static, int[] v_static, int[] q_front, int[] n_iter, int[] q_diff, int[] nc_cddif, int[] nc_diff, int[] v_diff) : base(27)
		{
            NID_PACKET = 27;
			Q_DIR=q_dir;
			L_PACKET=l_packet;
			Q_SCALE=q_scale;
			D_STATIC=d_static;
			V_STATIC=v_static;
			Q_FRONT=q_front;
			N_ITER=n_iter;
			Q_DIFF=q_diff;
			NC_CDDIFF=nc_cddif;
			NC_DIFF=nc_diff;
			V_DIFF=v_diff;
		} 
	}
	public class TemporarySpeedRestriction : Packet
	{
        public int Q_DIR;
        public int L_PACKET;
        public int Q_SCALE;
        public int NID_TSR;
        public int D_TSR;
        public int L_TSR;
        public int Q_FRONT;
        public int V_TSR;
        public TemporarySpeedRestriction(int q_dir, int l_packet, int q_scale, int nid_tsr, int d_tsr, int l_tsr, int q_front, int v_tsr) : base(65)
        {
            Q_DIR = q_dir;
            L_PACKET = l_packet;
            Q_SCALE = q_scale;
            NID_TSR = nid_tsr;
            D_TSR = d_tsr;
            L_TSR = l_tsr;
            Q_FRONT = q_front;
            V_TSR = v_tsr;
        }
	}
	public class TemporarySpeedRestrictionRevocation : Packet
	{
        public int Q_DIR;
        public int L_PACKET;
        public int NID_TSR;
		public TemporarySpeedRestrictionRevocation(int q_dir, int l_packet, int nid_tsr) : base(66)
        {
            Q_DIR = q_dir;
            L_PACKET = l_packet;
            NID_TSR = nid_tsr;
        }
	}
	public class ModeProfile : Packet
	{
        public int Q_DIR;
        public int L_PACKET;
        public int Q_SCALE;
        public int N_ITER;
        public int[] D_MAMODE;
        public int[] M_MAMODE;
        public int[] V_MAMODE;
        public int[] L_MAMODE;
        public int[] L_ACKMAMODE;
        public int[] Q_MAMODE;
		public ModeProfile(int q_dir, int l_packet, int q_scale, int n_iter, int[] d_mamode, int[] m_mamode, int[] v_mamode, int[] l_mamode, int[] l_ackmamode, int[] q_mamode) : base(80)
		{
            Q_DIR = q_dir;
            L_PACKET = l_packet;
            Q_SCALE = q_scale;
			N_ITER=n_iter;
			D_MAMODE=d_mamode;
			M_MAMODE=m_mamode;
			V_MAMODE=v_mamode;
			L_MAMODE=l_mamode;
			L_ACKMAMODE=l_ackmamode;
			Q_MAMODE=q_mamode;
		}
	}
    public class MARequestParameters : Packet
    {
        public int Q_DIR;
        public int L_PACKET;
        public int T_MAR;
        public int T_TIMEOUTREQST;
        public int T_CYCRQST;
        public MARequestParameters(int Q_DIR, int L_PACKET, int T_MAR, int T_TIMEOUTREQST,int T_CYCRQST) : base(57)
        {
            this.Q_DIR = Q_DIR;
            this.L_PACKET = L_PACKET;
            this.T_MAR = T_MAR;
            this.T_TIMEOUTREQST = T_TIMEOUTREQST;
            this.T_CYCRQST = T_CYCRQST;
        }
    }
    public class InfillLocationReference : Packet
    {
        public int Q_DIR;
        public int L_PACKET;
        public int Q_NEWCOUNTRY;
        public int NID_C;
        public int NID_BG;
        public InfillLocationReference(int Q_DIR, int L_PACKET, int Q_NEWCOUNTRY, int NID_C, int NID_BG) : base(136)
        {
            this.Q_DIR = Q_DIR;
            this.L_PACKET = L_PACKET;
            this.Q_NEWCOUNTRY = Q_NEWCOUNTRY;
            this.NID_C = NID_C;
            this.NID_BG = NID_BG;
        }
    }
    public class Linking : Packet
    {
        public int Q_DIR;
        public int L_PACKET;
        public int Q_SCALE;
        public int D_LINK;
        public int Q_NEWCOUNTRY;
        public int NID_C;
        public int NID_BG;
        public int Q_LINKORIENTATION;
        public int Q_LINKREACTION;
        public int Q_LOCACC;
        public Linking(int Q_DIR, int L_PACKET, int Q_SCALE, int D_LINK, int Q_NEWCOUNTRY, int NID_C, int NID_BG, int Q_LINKORIENTATION, int Q_LINKREACTION, int Q_LOCACC) : base(5)
        {
            this.Q_DIR = Q_DIR;
            this.L_PACKET = L_PACKET;
            this.Q_SCALE = Q_SCALE;
            this.D_LINK = D_LINK;
            this.Q_NEWCOUNTRY = Q_NEWCOUNTRY;
            this.NID_C = NID_C;
            this.NID_BG = NID_BG;
            this.Q_LINKORIENTATION = Q_LINKORIENTATION;
            this.Q_LINKREACTION = Q_LINKREACTION;
            this.Q_LOCACC = Q_LOCACC;
        }
    }
    public class LZB : TrainControlSystem
    {
        Aspect LZBLastAspect;
        Aspect LZBPreviousAspect;
        bool LZBLiberar;
        bool LZBRebasar;
        bool LZBAnularParada;
        public bool LZBSupervising;
        public bool LZBEmergencyBrake;
        public bool LZBOE;
        int LZBPar = -1;
        float LZBMaxDistance = 4000;
        float LZBLT;
        float LZBPFT;
        float LZBVMT;
        LZBTrain LZBTR = null;
        bool PruebaFuncional;
        float LZBLastDistance;
        float LZBLastSigDistance;
        float LZBLastTime;
        float LZBSpeedLimit = 0;
        float LZBSupervisionLimit = 0;
        public float LZBMaxSpeed = 0;
        public float LZBTargetSpeed = 0;
        public float LZBTargetDistance = 0;
        float LZBDeceleration = 0.8f;
        LZBCenter LZBCenter;
        bool LZBAhorroEnergia = false;
        bool LZBEnd;
        bool LZBVOn;
        bool LZBV;
        Timer LZBRecTimer;
        Timer LZBCurveTimer;
        Timer LZBVTimer;

        TCS_Spain tcs;

        bool Pressed;
        public LZB(TrainControlSystem tcss)
        {
            tcs = tcss as TCS_Spain;
            // AbstractScriptClass
            ClockTime = () => tcs.ClockTime();
            DistanceM = () => tcs.DistanceM();

            // TrainControlSystem
            IsTrainControlEnabled = tcs.IsTrainControlEnabled;
            IsDirectionReverse = tcs.IsDirectionReverse;
            IsBrakeEmergency = tcs.IsBrakeEmergency;
            IsBrakeFullService = tcs.IsBrakeFullService;
            PowerAuthorization = tcs.PowerAuthorization;
            TrainLengthM = tcs.TrainLengthM;
            SpeedMpS = tcs.SpeedMpS;
            BrakePipePressureBar = tcs.BrakePipePressureBar;
            CurrentSignalSpeedLimitMpS = tcs.CurrentSignalSpeedLimitMpS;
            CurrentPostSpeedLimitMpS = tcs.CurrentPostSpeedLimitMpS;
            IsAlerterEnabled = tcs.IsAlerterEnabled;
            AlerterSound = tcs.AlerterSound;
            SetHorn = (value) => { };
            SetFullBrake = (value) =>
            {
            };
            SetEmergencyBrake = (value) =>
            {
                LZBEmergencyBrake = true;
            };
            SetThrottleController = (value) => { };
            SetDynamicBrakeController = (value) => { };
            SetVigilanceAlarmDisplay = (value) => { if (tcs.ActiveCCS == TCS_Spain.CCS.LZB) tcs.SetVigilanceAlarmDisplay(value); };
            SetVigilanceEmergencyDisplay = (value) => { if (tcs.ActiveCCS == TCS_Spain.CCS.LZB) tcs.SetVigilanceEmergencyDisplay(value); };
            SetOverspeedWarningDisplay = (value) => { if (tcs.ActiveCCS == TCS_Spain.CCS.LZB) tcs.SetOverspeedWarningDisplay(value); };
            SetPenaltyApplicationDisplay = (value) => { if (tcs.ActiveCCS == TCS_Spain.CCS.LZB) tcs.SetPenaltyApplicationDisplay(value); };
            SetMonitoringStatus = (value) => { if (tcs.ActiveCCS == TCS_Spain.CCS.LZB) tcs.SetMonitoringStatus(value); };
            SetCurrentSpeedLimitMpS = (value) => { if (tcs.ActiveCCS == TCS_Spain.CCS.LZB) tcs.SetCurrentSpeedLimitMpS(value); };
            SetNextSpeedLimitMpS = (value) => { if (tcs.ActiveCCS == TCS_Spain.CCS.LZB) tcs.SetNextSpeedLimitMpS(value); };
            SetInterventionSpeedLimitMpS = (value) => { };
            SetNextSignalAspect = (value) => { if (tcs.ActiveCCS == TCS_Spain.CCS.LZB) tcs.SetNextSignalAspect(value); };
            TriggerSoundAlert1 = () => { if (tcs.ActiveCCS == TCS_Spain.CCS.LZB) tcs.TriggerSoundAlert1(); };
            TriggerSoundAlert2 = () => { if (tcs.ActiveCCS == TCS_Spain.CCS.LZB) tcs.TriggerSoundAlert2(); };
            TriggerSoundInfo1 = () => { if (tcs.ActiveCCS == TCS_Spain.CCS.LZB) tcs.TriggerSoundInfo1(); };
            TriggerSoundInfo2 = () => { if (tcs.ActiveCCS == TCS_Spain.CCS.LZB) tcs.TriggerSoundInfo2(); };
            TriggerSoundPenalty1 = () => { if (tcs.ActiveCCS == TCS_Spain.CCS.LZB) tcs.TriggerSoundPenalty1(); };
            TriggerSoundPenalty2 = () => { if (tcs.ActiveCCS == TCS_Spain.CCS.LZB) tcs.TriggerSoundPenalty2(); };
            TriggerSoundWarning1 = () => { if (tcs.ActiveCCS == TCS_Spain.CCS.LZB) tcs.TriggerSoundWarning1(); };
            TriggerSoundWarning2 = () => { if (tcs.ActiveCCS == TCS_Spain.CCS.LZB) tcs.TriggerSoundWarning2(); };
            TriggerSoundSystemActivate = () => { if (tcs.ActiveCCS == TCS_Spain.CCS.LZB) tcs.TriggerSoundSystemActivate(); };
            TriggerSoundSystemDeactivate = () => { if (tcs.ActiveCCS == TCS_Spain.CCS.LZB) tcs.TriggerSoundSystemDeactivate(); };
            TrainSpeedLimitMpS = tcs.TrainSpeedLimitMpS;
            NextSignalSpeedLimitMpS = tcs.NextSignalSpeedLimitMpS;
            NextSignalAspect = tcs.NextSignalAspect;
            NextSignalDistanceM = tcs.NextSignalDistanceM;
            NextPostSpeedLimitMpS = tcs.NextPostSpeedLimitMpS;
            NextPostDistanceM = tcs.NextPostDistanceM;

            SpeedCurve = tcs.SpeedCurve;
            DistanceCurve = tcs.DistanceCurve;
            Deceleration = tcs.Deceleration;
            SetPantographsDown = () =>
            {

            };
            SetPowerAuthorization = (value) =>
            {

            };
            GetBoolParameter = tcs.GetBoolParameter;
            GetIntParameter = tcs.GetIntParameter;
            GetFloatParameter = tcs.GetFloatParameter;
            GetStringParameter = tcs.GetStringParameter;
        }
        public override void Initialize()
        {
            LZBCenter = new LZBCenter(!GetBoolParameter("LZB", "Canton_Movil", false));
            LZBAlertarLiberar();
            LZBSupervising = true;
            LZBRecTimer = new Timer(tcs);
            LZBRecTimer.Setup(8);
            LZBCurveTimer = new Timer(tcs);
            LZBVTimer = new Timer(tcs);
            LZBVTimer.Setup(0.5f);

            PressedTimer = new Timer(tcs);
            PressedTimer.Setup(3);
            ButtonsTimer = new Timer(tcs);
            ButtonsTimer.Setup(0.3f);
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
        Timer PressedTimer;
        int TimesPressed;
        Timer ButtonsTimer;
        public void Botones()
        {
            LZBLiberar = Pressed;
            if (Pressed && !PressedTimer.Started)
            {
                TimesPressed++;
                ButtonsTimer.Stop();
            }
            if (TimesPressed != 0 && !Pressed && !ButtonsTimer.Started) ButtonsTimer.Start();
            if (ButtonsTimer.Triggered) TimesPressed = 0;
            LZBAnularParada = Pressed && TimesPressed == 3 && tcs.SerieTren == 446;
            if (Pressed && !PressedTimer.Started)
            {
                PressedTimer.Start();
            }
            if (!Pressed)
            {
                PressedTimer.Stop();
                LZBRebasar = false;
            }
            if (PressedTimer.Triggered)
            {
                LZBRebasar = true;
                PressedTimer.Stop();
            }
        }
        public override void Update()
        {
            Botones();
            LZBEmergencyBrake = false;
            /*if ((DateTime.Now.DayOfWeek == DayOfWeek.Saturday || DateTime.Now.DayOfWeek == DayOfWeek.Sunday) && tcs.SerieTren == 446)
            {
                LZBAhorroEnergia = true;
            }
            else */LZBAhorroEnergia = false;
            if (((LZBLiberar && !LZBOE && tcs.SerieTren == 446) || (tcs.SignalPassed && !LZBOE && tcs.SerieTren != 446)) && !LZBSupervising)
            {
                LZBSupervising = true;
                LZBTR = null;
            }
            if (LZBRebasar)
            {
                LZBSupervising = LZBOE;
                LZBOE = false;
                LZBEmergencyBrake = false;
                if (tcs.SerieTren == 446) LZBSupervising = false;
                else
                {
                    LZBTargetSpeed = LZBMaxSpeed = LZBSpeedLimit = MpS.FromKpH(40);
                    SetNextSpeedLimitMpS(LZBTargetSpeed);
                }
            }
            if (LZBSupervising && (NextSignalDistanceM(0) > 0 || DistanceM() > 0))
            {
                SetVigilanceAlarmDisplay(true);
                int i;
                for (i = 0; i < 5 && NextSignalAspect(i) != Aspect.Stop; i++) ;
                if (NextSignalAspect(i) == Aspect.Stop && NextSignalDistanceM(i) > 0 && !LZBCenter.SobreEnclavamientos)
                {
                    LZBCenter.Com(new LZBTrain(MpS.FromKpH(300), 4, 41, 0, (Math.Abs(LZBLastSigDistance - NextSignalDistanceM(i) - (DistanceM() - LZBLastDistance))) / (ClockTime() - LZBLastTime), new LZBPosition(DistanceM() + NextSignalDistanceM(i), false), 2, this));
                }
                if (LZBTR == null)
                {
                    LZBAlertarLiberar();
                    LZBTR = new LZBTrain(LZBVMT, 7, LZBPFT, LZBLT, SpeedMpS(), new LZBPosition(DistanceM(), false), 1, this);
#if _OR_PERS
                    foreach (var stop in Locomotive.Train.StationStops)
                    {
                        LZBTR.Stops.Add(stop.DistanceToTrainM + DistanceM());
                    }
#else
                    if (tcs.SerieTren == 446)
                    {
                        float a = 75 - LZBLT;
                        LZBTR.Stops.Add(3360 - 37.5f + LZBLT / 2f + a);
                        LZBTR.Stops.Add(6771 - 37.5f + LZBLT / 2f + a);
                        LZBTR.Stops.Add(12342 - 37.5f + LZBLT / 2f + a);
                        LZBTR.Stops.Add(15654 - 37.5f + LZBLT / 2f + a);
                        LZBTR.Stops.Add(18837 - 37.5f + LZBLT / 2f + a);
                        LZBTR.Stops.Add(21413 - 37.5f + LZBLT / 2f + a);
                        LZBTR.Stops.Add(24447 - 37.5f + LZBLT / 2f + a);
                        LZBTR.Stops.Add(27059 - 37.5f + LZBLT / 2f + a);
                        LZBTR.Stops.Add(28398 - 37.5f + LZBLT / 2f + a);
                        LZBTR.Stops.Add(34349 + a);
                    }
#endif
                    LZBTR = LZBCenter.Com(LZBTR);
                }
                else if (LZBTR.Position.PK + LZBTR.Profile.TargetDistance <= DistanceM() && LZBTR.Profile.TargetSpeed < 0.1f) LZBOE = true;
                LZBTR.Speed = SpeedMpS();
                LZBTR.Position.PK = DistanceM();
                var tr = LZBCenter.Com(LZBTR);
                if (tr == null)
                {
                    SetVigilanceEmergencyDisplay(true);
                    if (tcs.SerieTren == 446)
                    {
                        if (SpeedCurve(LZBTargetDistance - 350, LZBTargetSpeed, 0, 0, LZBPFT / 250) < LZBSpeedLimit)
                        {
                            //Curva de frenado
                        }
                        else
                        {
                            //Sin curva de frenado
                        }
                    }
                    else
                    {
                        if (Math.Abs(LZBTR.NextTrain.Position.PK - LZBTR.Position.PK) < NextSignalDistanceM(0))
                        {
                            //Bloqueo parcial
                        }
                        else
                        {
                            //Bloqueo total
                        }
                    }
                }
                else
                {
                    LZBTR = tr;
                    SetVigilanceEmergencyDisplay(false);
                }
                LZBTargetSpeed = LZBTR.Profile.TargetSpeed;
                LZBTargetDistance = LZBTR.Profile.TargetDistance;
                LZBSpeedLimit = LZBTR.Profile.SpeedLimit;
                LZBSupervisionLimit = SpeedCurve(LZBTargetDistance, LZBTargetSpeed, 0, 0, LZBPFT / 100);
                LZBV = (LZBTargetDistance < 350 || SpeedCurve(LZBTargetDistance - 350, LZBTargetSpeed, 0, 0, LZBPFT / 250) <= LZBSpeedLimit) && LZBTargetSpeed < LZBSpeedLimit && SpeedMpS() > LZBTargetSpeed;
                if (LZBTR.Profile.End && LZBTR.Profile.TargetDistance < 1700)
                {
                    if (!LZBEnd)
                    {
                        LZBRecTimer.Setup(10);
                        LZBRecTimer.Start();
                        TriggerSoundInfo1();
                    }
                    LZBEnd = true;
                    if (LZBRecTimer.Triggered)
                    {
                        LZBCurveTimer.Setup((LZBSpeedLimit - 3.75f) / 1.25f);
                        LZBCurveTimer.Start();
                        LZBRecTimer.Stop();
                        LZBEmergencyBrake = true;
                    }
                    if (LZBCurveTimer.Started)
                    {
                        LZBSpeedLimit = LZBSpeedLimit - 3.75f - 1.25f * (int)(LZBCurveTimer.AlarmValue - LZBCurveTimer.RemainingValue);
                    }
                    if (LZBLiberar)
                    {
                        LZBRecTimer.Stop();
                        LZBCurveTimer.Stop();
                    }
                    if (LZBTR.Profile.TargetDistance < 10)
                    {
                        LZBSupervising = false;
                        LZBEnd = false;
                    }
                }
                LZBSupervisionLimit = Math.Min(LZBSpeedLimit * 1.05f + 1, LZBSupervisionLimit);
                LZBMaxSpeed = LZBAhorroEnergia ? LZBSpeedLimit * 0.8f : LZBSpeedLimit;
                if (LZBOE)
                {
                    LZBMaxSpeed = LZBSpeedLimit = LZBTargetDistance = LZBTargetSpeed = 0;
                }
                if (LZBAnularParada && LZBTR.Stops.Count != 0) LZBTR.Stops.RemoveAt(0);
                if (LZBTargetDistance > LZBMaxDistance) SetNextSignalAspect(Aspect.Clear_2);
                else SetNextSignalAspect(Aspect.Stop);
                if (LZBSupervisionLimit < SpeedMpS() || LZBOE) LZBEmergencyBrake = true;
                if (LZBTargetDistance < LZBMaxDistance)
                {
                    SetNextSpeedLimitMpS(LZBTargetSpeed);
                    SetCurrentSpeedLimitMpS(MpS.FromKpH(LZBTargetDistance));
                }
                else
                {
                    SetNextSpeedLimitMpS(LZBSpeedLimit);
                    SetCurrentSpeedLimitMpS(MpS.FromKpH(LZBMaxDistance));
                }
                if (SpeedMpS() > LZBSpeedLimit)
                {
                    if (LZBVTimer.Triggered || !LZBVTimer.Started)
                    {
                        LZBVOn = !LZBVOn;
                        LZBVTimer.Start();
                    }
                }
                else
                {
                    LZBVTimer.Stop();
                    LZBVOn = LZBV && LZBSpeedLimit - SpeedMpS() < MpS.FromKpH(30);
                }
                if (LZBAhorroEnergia) SetNextSignalAspect(Aspect.Clear_1);
                else SetNextSignalAspect(Aspect.Clear_2);
                SetOverspeedWarningDisplay(LZBVOn);
                LZBPreviousAspect = NextSignalAspect(i);
                LZBLastSigDistance = NextSignalDistanceM(i);
                LZBLastDistance = DistanceM();
                LZBLastTime = ClockTime();
            }
            else
            {
                SetVigilanceAlarmDisplay(false);
                SetCurrentSpeedLimitMpS(0);
                SetNextSpeedLimitMpS(0);
            }
        }
        public void LZBAlertarLiberar()
        {
            LZBVMT = tcs.TrainMaxSpeed;
            if (LZBVMT < MpS.FromKpH(101)) LZBMaxDistance = 2000;
            else if (LZBVMT < MpS.FromKpH(161)) LZBMaxDistance = 4000;
            else if (LZBVMT < MpS.FromKpH(201)) LZBMaxDistance = 9900;
            else LZBMaxDistance = 12000;
            LZBLT = TrainLengthM();
            if (tcs.SerieTren == 446)
            {
                int LZBDec = 0;
                switch (LZBDec)
                {
                    case 0:
                        LZBPFT = 124;
                        break;
                    case 1:
                        LZBPFT = 103;
                        break;
                    case 2:
                        LZBPFT = 96;
                        break;
                    case 3:
                        LZBPFT = 89;
                        break;
                    case 4:
                        LZBPFT = 82;
                        break;
                    case 7:
                        LZBPFT = 60;
                        LZBVMT = MpS.FromKpH(30);
                        break;
                }
            }
            else LZBPFT = 75;
        }
    }
    public class LZBCenter
    {
        public bool SobreEnclavamientos;
        List<LZBTrain> Trains;
        public LZBCenter(bool CONV)
        {
            SobreEnclavamientos = CONV;
            Trains = new List<LZBTrain>();
        }
        public LZBTrain Com(LZBTrain tr)
        {
            try
            {
                Trains.RemoveAll(x => x.LastTime < x.tcs.ClockTime() - 3f);
                if (Trains.Contains(tr))
                {
                    Trains.RemoveAll(x => x.Number == tr.Number);
                }
                tr.LastTime = tr.tcs.ClockTime();
                Trains.Add(tr);
                float TargetDistance = float.MaxValue;
                float Speed = float.MaxValue;
                float TargetSpeed = Speed;
                bool End = false;
                foreach (var Train in Trains)
                {
                    if (Train.Number == tr.Number || Train.Position.SI != tr.Position.SI || (tr.Position.SI && tr.Position.PK < Train.Position.PK) || (!tr.Position.SI && tr.Position.PK > Train.Position.PK)) continue;
                    if (tr.NextTrain == null) tr.NextTrain = Train;
                    if (Math.Abs(Train.Position.PK - tr.Position.PK) < Math.Abs(tr.NextTrain.Position.PK - tr.Position.PK)) tr.NextTrain = Train;
                    float TD = Math.Abs(Train.Position.PK - tr.Position.PK) + DistanceCurve(Train.Speed, 0, 0, 0, Train.PFT) - Train.LT;
                    float TS = 0;
                    float SL = Math.Max(SpeedCurve(TD, TS, 0, 0, tr.PFT / 250), TS);
                    if (SL < Speed)
                    {
                        TargetDistance = TD;
                        TargetSpeed = TS;
                        Speed = SL;
                        End = false;
                    }
                }
                {
                    int sig;
                    for (sig = 0; sig < 5 && tr.tcs.NextSignalAspect(sig) != Aspect.Stop && tr.tcs.NextSignalAspect(sig) != Aspect.Restricted && tr.tcs.NextSignalAspect(sig) != Aspect.StopAndProceed && tr.tcs.NextSignalAspect(sig) != Aspect.Permission; sig++) ;
                    float TD = tr.tcs.NextSignalDistanceM(sig);
                    float TS = tr.tcs.NextSignalAspect(sig) == Aspect.Restricted ? MpS.FromKpH(30) : 0;
                    float SL = Math.Max(SpeedCurve(TD, TS, 0, 0, tr.PFT / 250), TS);
                    if (SL < Speed)
                    {
                        End = tr.tcs.NextSignalAspect(sig) == Aspect.Restricted;
                        if (End || SobreEnclavamientos)
                        {
                            TargetDistance = TD;
                            TargetSpeed = TS;
                            Speed = SL;
                        }
                    }
                }
                for (int i = 0; i < 5; i++)
                {
                    if ((tr.tcs.NextPostSpeedLimitMpS(i) >= tr.VMT && (i == 0 && tr.tcs.CurrentPostSpeedLimitMpS() >= tr.VMT) || (i != 0 && tr.tcs.NextPostSpeedLimitMpS(i - 1) >= tr.VMT)) || (i == 0 && tr.tcs.NextPostSpeedLimitMpS(i) == tr.tcs.CurrentPostSpeedLimitMpS()) || (i != 0 && tr.tcs.NextPostSpeedLimitMpS(i) == tr.tcs.NextPostSpeedLimitMpS(i - 1))) continue;
                    float TD = tr.tcs.NextPostDistanceM(i);
                    float TS = tr.tcs.NextPostSpeedLimitMpS(i);
                    float SL = Math.Max(TS, SpeedCurve(TD, TS, 0, 0, tr.PFT / 250));
                    if (SL < Speed)
                    {
                        TargetDistance = TD;
                        TargetSpeed = TS;
                        Speed = SL;
                        End = false;
                    }
                }
                /*if(tr.tcs.SignalPassed)
                {
                    if (tr.tcs.NextSignalDistanceM(0) < 1000 && !tr.Stops.Contains(tr.Position.PK + (tr.tcs.NextSignalDistanceM(0) - 125 + tr.VMT / 2) * (tr.Position.SI ? -1 : 1))) tr.Stops.Add(tr.Position.PK + (tr.tcs.NextSignalDistanceM(0) - 125 + tr.LT / 2) * (tr.Position.SI ? -1 : 1));
                    if (tr.tcs.NextSignalDistanceM(1) - tr.tcs.NextSignalDistanceM(0) < 1000 && !tr.Stops.Contains(tr.Position.PK + (tr.tcs.NextSignalDistanceM(1) - 125 + tr.VMT / 2) * (tr.Position.SI ? -1 : 1))) tr.Stops.Add(tr.Position.PK + (tr.tcs.NextSignalDistanceM(1) - 125 + tr.LT / 2) * (tr.Position.SI ? -1 : 1));
                    if (tr.tcs.NextSignalDistanceM(2) - tr.tcs.NextSignalDistanceM(1) < 1000 && !tr.Stops.Contains(tr.Position.PK + (tr.tcs.NextSignalDistanceM(2) - 125 + tr.VMT / 2) * (tr.Position.SI ? -1 : 1))) tr.Stops.Add(tr.Position.PK + (tr.tcs.NextSignalDistanceM(2) - 125 + tr.LT / 2) * (tr.Position.SI ? -1 : 1));
                }*/
                for (int i = 0; i < tr.Stops.Count; i++)
                {
                    var Stop = tr.Stops[i];
                    float TD = Math.Abs(Stop - tr.Position.PK);
                    float TS = 0;
                    float SL = Math.Max(TS, SpeedCurve(TD, TS, 0, 0, tr.PFT / 250));
                    if (TD < 15 && tr.Speed < 0.1f || (Stop < tr.Position.PK - 1 && !tr.Position.SI) || (Stop > tr.Position.PK + 1 && tr.Position.SI))
                    {
                        tr.Stops.RemoveAt(i);
                        continue;
                    }
                    if (SL < Speed)
                    {
                        TargetDistance = TD;
                        TargetSpeed = TS;
                        Speed = SL;
                        End = false;
                    }
                }
                Speed = Math.Min(Speed, Math.Min(tr.tcs.CurrentPostSpeedLimitMpS(), tr.VMT));
                TargetSpeed = Math.Min(TargetSpeed, Speed);
                tr.Profile = new LZBProfile(Speed, TargetDistance, TargetSpeed, End);
                return tr;
            }
            catch(Exception)
            {
                return null;
            }
        }
        public float SpeedCurve(float targetDistanceM, float targetSpeedMpS, float slope, float delayS, float decelerationMpS2)
        {
            if (targetSpeedMpS < 0)
                targetSpeedMpS = 0;

            decelerationMpS2 -= 9.80665f * slope;

            float squareSpeedComponent = targetSpeedMpS * targetSpeedMpS
                + (delayS * delayS) * decelerationMpS2 * decelerationMpS2
                + 2f * targetDistanceM * decelerationMpS2;

            float speedComponent = delayS * decelerationMpS2;

            return (float)Math.Sqrt(squareSpeedComponent) - speedComponent;
        }
        private static float DistanceCurve(float currentSpeedMpS, float targetSpeedMpS, float slope, float delayS, float decelerationMpS2)
        {
            if (targetSpeedMpS < 0)
                targetSpeedMpS = 0;

            float brakingDistanceM = (currentSpeedMpS * currentSpeedMpS - targetSpeedMpS * targetSpeedMpS)
                / (2 * (decelerationMpS2 - 9.80665f * slope));

            float delayDistanceM = delayS * currentSpeedMpS;

            return brakingDistanceM + delayDistanceM;
        }
    }
    public class LZBTrain
    {
        public float VMT;
        public float TF;
        public float PFT;
        public float LT;
        public float Speed;
        public int Number;
        public LZBPosition Position;
        public TrainControlSystem tcs;
        public LZBProfile Profile;
        public List<float> Stops;
        public LZBTrain NextTrain;
        public float LastTime;
        public LZBTrain(float VMT, float TF, float PFT, float LT, float Speed, LZBPosition Position, int Number, TrainControlSystem tcs)
        {
            this.VMT = VMT;
            this.TF = TF;
            this.PFT = PFT;
            this.LT = LT;
            this.Speed = Speed;
            this.Position = Position;
            this.Number = Number;
            this.tcs = tcs;
            Stops = new List<float>();
        }
        // override object.Equals
        public override bool Equals(object obj)
        {
            if (obj == null || GetType() != obj.GetType())
            {
                return false;
            }
            else return (obj as LZBTrain).Number == Number;
        }
        public override int GetHashCode()
        {
            return Number;
        }
    }
    public struct LZBPosition
    {
        public float PK;
        public bool SI;
        public LZBPosition(float PK, bool SI)
        {
            this.PK = PK;
            this.SI = SI;
        }
    }
    public struct LZBProfile
    {
        public float SpeedLimit;
        public float TargetDistance;
        public float TargetSpeed;
        public bool End;
        public LZBProfile(float SL, float TD, float TS, bool End)
        {
            SpeedLimit = SL;
            TargetDistance = TD;
            TargetSpeed = TS;
            this.End = End;
        }
    }
    public interface ASFA
    {
        bool Urgencia();
        bool Connected();
        void Update();
    }
    public abstract class ASFAclasico : TrainControlSystem, ASFA
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
            L9
        }
        public bool Urgencia;
        public bool Eficacia;
        protected bool Alarma;
        protected bool RebaseAuto;
        public Timer TiempoEncendido;
        public Timer Rec;
        public float TipoTren = 100f / 3.6f;
        protected readonly TCS_Spain tcs;
        protected Aspect BalizaAspect;
        Aspect BalizaNextAspect;

        protected Timer PressedTimer;
        protected Timer ButtonsTimer;
        protected Timer OverrideTimer;

        public bool Pressed = false;
        public bool RearmeFrenoPressed;
        public bool RebasePressed = false;
        public bool ConexPressed;

        public int ConexTimesPressed;


        public abstract void BotonesASFA();
        protected abstract void SetVelocidades();

        public override void Update()
        {
            BotonesASFA();
            if (ConexPressed)
            {
                if (Eficacia) tcs.ActiveCCS = TCS_Spain.CCS.ASFA;
                if(!Activated) TriggerSoundInfo1();
                Activated = true;
                Eficacia = tcs.ActiveCCS == TCS_Spain.CCS.ASFA;
                ConexTimesPressed = 0;
            }
            else if (Activated)
            {
                Activated = false;
                ConexTimesPressed = 0;
            }
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
        public ASFAclasico(TrainControlSystem TCS)
        {
            tcs = TCS as TCS_Spain;
            // AbstractScriptClass
            ClockTime = tcs.ClockTime;
            DistanceM = tcs.DistanceM;

            // TrainControlSystem
            IsTrainControlEnabled = tcs.IsTrainControlEnabled;
            IsDirectionReverse = tcs.IsDirectionReverse;
            IsBrakeEmergency = tcs.IsBrakeEmergency;
            IsBrakeFullService = tcs.IsBrakeFullService;
            PowerAuthorization = tcs.PowerAuthorization;
            TrainLengthM = tcs.TrainLengthM;
            SpeedMpS = tcs.SpeedMpS;
            BrakePipePressureBar = tcs.BrakePipePressureBar;
            CurrentSignalSpeedLimitMpS = tcs.CurrentSignalSpeedLimitMpS;
            CurrentPostSpeedLimitMpS = tcs.CurrentPostSpeedLimitMpS;
            IsAlerterEnabled = tcs.IsAlerterEnabled;
            AlerterSound = tcs.AlerterSound;
            SetHorn = (value) => { };
            SetFullBrake = (value) =>
            {
                return;
            };
            SetEmergencyBrake = (value) =>
            {
                Urgencia = true;
            };
            SetThrottleController = (value) => { };
            SetDynamicBrakeController = (value) => { };
            SetVigilanceAlarmDisplay = (value) => { if (tcs.ActiveCCS == TCS_Spain.CCS.ASFA) tcs.SetVigilanceAlarmDisplay(value); };
            SetVigilanceEmergencyDisplay = (value) => { if (tcs.ActiveCCS == TCS_Spain.CCS.ASFA) tcs.SetVigilanceEmergencyDisplay(value); };
            SetOverspeedWarningDisplay = (value) => { if (tcs.ActiveCCS == TCS_Spain.CCS.ASFA) tcs.SetOverspeedWarningDisplay(value); };
            SetPenaltyApplicationDisplay = (value) => { if (tcs.ActiveCCS == TCS_Spain.CCS.ASFA) tcs.SetPenaltyApplicationDisplay(value); };
            SetMonitoringStatus = (value) => { };
            SetCurrentSpeedLimitMpS = (value) => { if (Activated && tcs.ActiveCCS == TCS_Spain.CCS.ASFA) tcs.SetCurrentSpeedLimitMpS(0); };
            SetNextSpeedLimitMpS = (value) => { if (tcs.ActiveCCS == TCS_Spain.CCS.ASFA) tcs.SetNextSpeedLimitMpS(value); };
            SetInterventionSpeedLimitMpS = (value) => { };
            SetNextSignalAspect = (value) => { if (tcs.ActiveCCS == TCS_Spain.CCS.ASFA) tcs.SetNextSignalAspect(value); };
            TriggerSoundAlert1 = () => { if (tcs.ActiveCCS == TCS_Spain.CCS.ASFA) tcs.TriggerSoundAlert1(); };
            TriggerSoundAlert2 = () => { if (tcs.ActiveCCS == TCS_Spain.CCS.ASFA) tcs.TriggerSoundAlert2(); };
            TriggerSoundInfo1 = () => { if (tcs.ActiveCCS == TCS_Spain.CCS.ASFA) tcs.TriggerSoundInfo1(); };
            TriggerSoundInfo2 = () => { if (tcs.ActiveCCS == TCS_Spain.CCS.ASFA) tcs.TriggerSoundInfo2(); };
            TriggerSoundPenalty1 = () => { if (tcs.ActiveCCS == TCS_Spain.CCS.ASFA) tcs.TriggerSoundPenalty1(); };
            TriggerSoundPenalty2 = () => { if (tcs.ActiveCCS == TCS_Spain.CCS.ASFA) tcs.TriggerSoundPenalty2(); };
            TriggerSoundWarning1 = () => { if (tcs.ActiveCCS == TCS_Spain.CCS.ASFA) tcs.TriggerSoundWarning1(); };
            TriggerSoundWarning2 = () => { if (tcs.ActiveCCS == TCS_Spain.CCS.ASFA) tcs.TriggerSoundWarning2(); };
            TriggerSoundSystemActivate = () => { if (tcs.ActiveCCS == TCS_Spain.CCS.ASFA) tcs.TriggerSoundSystemActivate(); };
            TriggerSoundSystemDeactivate = () => { if (tcs.ActiveCCS == TCS_Spain.CCS.ASFA) tcs.TriggerSoundSystemDeactivate(); };
            TrainSpeedLimitMpS = tcs.TrainSpeedLimitMpS;
            NextSignalSpeedLimitMpS = tcs.NextSignalSpeedLimitMpS;
            NextSignalAspect = tcs.NextSignalAspect;
            NextSignalDistanceM = tcs.NextSignalDistanceM;
            NextPostSpeedLimitMpS = tcs.NextPostSpeedLimitMpS;
            NextPostDistanceM = tcs.NextPostDistanceM;

            SpeedCurve = tcs.SpeedCurve;
            DistanceCurve = tcs.DistanceCurve;
            Deceleration = tcs.Deceleration;
            SetPantographsDown = () =>
            {
                
            };
            SetPowerAuthorization = (value) =>
            {
                
            };
            GetBoolParameter = tcs.GetBoolParameter;
            GetIntParameter = tcs.GetIntParameter;
            GetFloatParameter = tcs.GetFloatParameter;
            GetStringParameter = tcs.GetStringParameter;
        }
        public override void Initialize()
        {
            PressedTimer = new Timer(tcs);
            PressedTimer.Setup(3f);
            ButtonsTimer = new Timer(tcs);
            ButtonsTimer.Setup(0.5f);
            OverrideTimer = new Timer(tcs);
            OverrideTimer.Setup(10f);

            TiempoEncendido = new Timer(tcs);
            TiempoEncendido.Setup(10f);

            Rec = new Timer(tcs);
            Rec.Setup(3f);

            ConexPressed = true;

            SetVelocidades();
        }
        public Freq Baliza()
        {
            if (IsDirectionReverse())
            {
                if (tcs.SignalPassed) return Freq.L8;
                else if (tcs.PreviaPassed) return Freq.L7;
                else return Freq.FP;
            }
            else
            {
                if (tcs.PreviaPassed)
                {
                    BalizaAspect = NextSignalAspect(tcs.PreviaSignalNumber);
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
                if (tcs.SignalPassed)
                {
                    BalizaAspect = BalizaNextAspect;
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
                    BalizaNextAspect = NextSignalAspect(0);
                }
                if (tcs.AnuncioLTVPassed) return Freq.L1;
                if (tcs.PreanuncioLTVPassed) return Freq.L2;
            }
            return Freq.FP;
        }

        bool ASFA.Urgencia()
        {
            return Urgencia;
        }

        bool ASFA.Connected()
        {
            return Activated;
        }
    }
    public class ASFAOriginal : ASFAclasico
    {
        bool ASFAFrenarOn = false;
        bool ASFARecOn = false;
        bool ASFARebaseOn = false;
        bool ASFAParadaOn = false;
        bool ASFAVLOn = false;
        
        Timer ASFAPressedTimer;
        Timer ASFAButtonsTimer;
        Timer ASFAOverrideTimer;

        Timer RojoEncendido;

        Aspect ASFAUltimaInfo;

        Freq FrecBaliza;
        public ASFAOriginal(TrainControlSystem tcs) : base(tcs)
        {
        }
        public override void BotonesASFA()
        {
            if (Pressed && !PressedTimer.Started)
            {
                ConexTimesPressed++;
                ButtonsTimer.Stop();
            }
            if (ConexTimesPressed != 0 && !Pressed && !ButtonsTimer.Started) ButtonsTimer.Start();
            if (ButtonsTimer.Triggered) ConexTimesPressed = 0;
            if (Pressed && ConexTimesPressed == 4)
            {
                if (ConexPressed) ConexPressed = false;
                else ConexPressed = true;
            }
            RearmeFrenoPressed = Pressed;
            if (Pressed && !PressedTimer.Started) PressedTimer.Start();
            if (!Pressed) PressedTimer.Stop();
            if(PressedTimer.Triggered)
            {
                RebasePressed = !RebasePressed;
                PressedTimer.Stop();
            }
        }
        protected override void SetVelocidades()
        {
            TipoTren = Math.Min(tcs.TrainMaxSpeed, TipoTren);
            if (TipoTren >= MpS.FromKpH(110f)) TipoTren = MpS.FromKpH(160);
            else if (TipoTren >= MpS.FromKpH(80f)) TipoTren = MpS.FromKpH(100);
            else TipoTren = MpS.FromKpH(70f);
        }
        public override void HandleEvent(TCSEvent evt, string message)
        {
            base.HandleEvent(evt, message);
        }
        public override void Initialize()
        {
            base.Initialize();
            RojoEncendido = new Timer(tcs);
            RojoEncendido.Setup(10f);
            
            ASFAPressedTimer = new Timer(tcs);
            ASFAPressedTimer.Setup(3f);
            ASFAButtonsTimer = new Timer(tcs);
            ASFAButtonsTimer.Setup(0.5f);
            ASFAOverrideTimer = new Timer(tcs);
            ASFAOverrideTimer.Setup(10f);
        }
        public override void SetEmergency(bool emergency)
        {
            return;
        }
        public override void Update()
        {
            base.Update();
            if (Eficacia)
            {
                if (RebasePressed && !ASFAOverrideTimer.Started)
                {
                    ASFAOverrideTimer.Setup(10f);
                    ASFAOverrideTimer.Start();
                    RebaseAuto = true;
                    SetNextSignalAspect(Aspect.StopAndProceed);
                    ASFARebaseOn = true;
                }
                FrecBaliza = Baliza();
                if (FrecBaliza != Freq.FP)
                {
                    RojoEncendido.Stop();
                    switch (FrecBaliza)
                    {
                        case Freq.L1:
                            Rec.Start();
                            ASFAUltimaInfo = Aspect.Approach_1;
                            break;
                        case Freq.L2:
                        case Freq.L3:
                            TriggerSoundInfo1();
                            ASFAUltimaInfo = Aspect.Clear_2;
                            break;
                        case Freq.L7:
                            TriggerSoundInfo2();
                            float MaxSpeed;
                            if (TipoTren >= MpS.FromKpH(110f)) MaxSpeed = MpS.FromKpH(60f);
                            else if (TipoTren >= MpS.FromKpH(80f)) MaxSpeed = MpS.FromKpH(50f);
                            else MaxSpeed = MpS.FromKpH(35f);
                            if (SpeedMpS() < MaxSpeed)
                            {
                                RojoEncendido.Start();
                                TriggerSoundPenalty1();
                            }
                            else
                            {
                                TriggerSoundInfo2();
                                Urgencia = true;
                            }
                            ASFAUltimaInfo = Aspect.Stop;
                            break;
                        case Freq.L8:
                            ASFAUltimaInfo = Aspect.Stop;
                            if (!RebaseAuto)
                            {
                                Urgencia = true;
                                TriggerSoundInfo2();
                            }
                            else
                            {
                                RojoEncendido.Start();
                                TriggerSoundInfo2();
                            }
                            RebaseAuto = false;
                            break;
                        default:
                            Rec.Start();
                            ASFAUltimaInfo = Aspect.Stop;
                            break;
                    }
                    SetNextSignalAspect(ASFAUltimaInfo);
                }
                if (RojoEncendido.Triggered)
                {
                    SetNextSignalAspect(Aspect.None);
                    RojoEncendido.Stop();
                }
                if (ASFAOverrideTimer.Triggered)
                {
                    RebaseAuto = false;
                    RebasePressed = false;
                }
                if (!RebasePressed && ASFAOverrideTimer.Started)
                {
                    ASFAOverrideTimer.Stop();
                    SetNextSignalAspect(Aspect.None);
                    ASFARebaseOn = false;
                }
                if (Rec.Started && !Rec.Triggered)
                {
                    TriggerSoundPenalty1();
                    SetVigilanceAlarmDisplay(true);
                    ASFARecOn = true;
                }
                if (Rec.Triggered)
                {
                    Urgencia = true;
                    TriggerSoundPenalty2();
                    Rec.Stop();
                    SetVigilanceAlarmDisplay(false);
                    ASFARecOn = false;
                }
                if (Pressed && Rec.Started)
                {
                    SetVigilanceAlarmDisplay(false);
                    ASFARecOn = false;
                    Rec.Stop();
                    TriggerSoundPenalty2();
                    SetNextSignalAspect(Aspect.None);
                }
                ASFAFrenarOn = ASFAUltimaInfo == Aspect.Approach_1 && Rec.Started;
                ASFAParadaOn = RojoEncendido.Started;
                if (Urgencia && RearmeFrenoPressed && SpeedMpS() < 1.5f) Urgencia = false;
            }
            else Urgencia = false;
        }
    }
    public class ASFA200 : ASFAclasico
    {
        bool FrenarOn = false;
        bool RecOn = false;
        bool RebaseOn = false;
        bool ParadaOn = false;
        bool VLOn = false;

        Timer ASFAPressedTimer;
        Timer ASFAButtonsTimer;
        Timer ASFAOverrideTimer;

        Timer VLParpadeo1;
        Timer VLParpadeo2;
        Timer RojoEncendido;
        Timer CurveL2Timer;

        Aspect UltimaInfo;

        float MaxSpeed;

        Freq FrecBaliza;
        public override void HandleEvent(TCSEvent evt, string message)
        {
            base.HandleEvent(evt, message);
        }
        public override void SetEmergency(bool emergency)
        {
        }
        protected override void SetVelocidades()
        {
            TipoTren = MpS.FromKpH(200);
        }
        public ASFA200(TrainControlSystem tcs) : base(tcs)
        {

        }
        public override void BotonesASFA()
        {
            if (Pressed && !PressedTimer.Started)
            {
                ConexTimesPressed++;
                ButtonsTimer.Stop();
            }
            if (ConexTimesPressed != 0 && !Pressed && !ButtonsTimer.Started) ButtonsTimer.Start();
            if (ButtonsTimer.Triggered) ConexTimesPressed = 0;
            if (Pressed && ConexTimesPressed == 4)
            {
                if (ConexPressed) ConexPressed = false;
                else ConexPressed = true;
            }
            RearmeFrenoPressed = Pressed;
            if (Pressed && !PressedTimer.Started) PressedTimer.Start();
            if (!Pressed) PressedTimer.Stop();
            if (PressedTimer.Triggered)
            {
                RebasePressed = !RebasePressed;
                PressedTimer.Stop();
            }
        }
        public override void Initialize()
        {
            base.Initialize();
            RojoEncendido = new Timer(tcs);
            RojoEncendido.Setup(10f);
            VLParpadeo1 = new Timer(tcs);
            VLParpadeo1.Setup(0.5f);
            VLParpadeo2 = new Timer(tcs);
            VLParpadeo2.Setup(0.5f);

            CurveL2Timer = new Timer(tcs);
            CurveL2Timer.Setup(29f);
            
            ASFAPressedTimer = new Timer(tcs);
            ASFAPressedTimer.Setup(3f);
            ASFAButtonsTimer = new Timer(tcs);
            ASFAButtonsTimer.Setup(0.5f);
            ASFAOverrideTimer = new Timer(tcs);
            ASFAOverrideTimer.Setup(10f);
        }
        public override void Update()
        {
            base.Update();
            if (Eficacia)
            {
                if (MaxSpeed == 0) MaxSpeed = float.MaxValue;
                if (RebasePressed)
                {
                    ASFAOverrideTimer.Setup(10f);
                    ASFAOverrideTimer.Start();
                    RebaseAuto = true;
                    SetNextSignalAspect(Aspect.StopAndProceed);
                    RebaseOn = true;
                }
                FrecBaliza = Baliza();
                if (FrecBaliza != Freq.FP)
                {
                    MaxSpeed = float.MaxValue;
                    VLParpadeo1.Stop();
                    VLParpadeo2.Stop();
                    RojoEncendido.Stop();
                    CurveL2Timer.Stop();
                    switch (FrecBaliza)
                    {
                        case Freq.L1:
                            MaxSpeed = MpS.FromKpH(160f);
                            Rec.Start();
                            UltimaInfo = Aspect.Approach_1;
                            break;
                        case Freq.L2:
                            Rec.Start();
                            CurveL2Timer.Start();
                            UltimaInfo = Aspect.Clear_1;
                            VLParpadeo1.Start();
                            break;
                        case Freq.L3:
                            TriggerSoundInfo1();
                            UltimaInfo = Aspect.Clear_2;
                            break;
                        case Freq.L7:
                            if (TipoTren >= MpS.FromKpH(110f)) MaxSpeed = MpS.FromKpH(60f);
                            else if (TipoTren >= MpS.FromKpH(80f)) MaxSpeed = MpS.FromKpH(50f);
                            else MaxSpeed = MpS.FromKpH(35f);
                            if (SpeedMpS() < MaxSpeed)
                            {
                                RojoEncendido.Start();
                                TriggerSoundPenalty1();
                            }
                            else
                            {
                                TriggerSoundInfo2();
                                Urgencia = true;
                            }
                            MaxSpeed = float.MaxValue;
                            UltimaInfo = Aspect.Stop;
                            break;
                        case Freq.L8:
                            UltimaInfo = Aspect.Stop;
                            if (!RebaseAuto)
                            {
                                Urgencia = true;
                                TriggerSoundInfo2();
                            }
                            else
                            {
                                RojoEncendido.Start();
                                TriggerSoundInfo2();
                            }
                            RebaseAuto = false;
                            break;
                        default:
                            Rec.Start();
                            UltimaInfo = Aspect.Stop;
                            break;
                    }
                    SetNextSignalAspect(UltimaInfo);
                }
                if (CurveL2Timer.Started && CurveL2Timer.RemainingValue < 11f)
                {
                    if (CurveL2Timer.Triggered) MaxSpeed = MpS.FromKpH(160f);
                    else MaxSpeed = MpS.FromKpH(180f);
                }
                if (VLParpadeo1.Started)
                {
                    if (VLParpadeo1.Triggered)
                    {
                        VLParpadeo1.Stop();
                        VLParpadeo2.Start();
                    }
                    else SetNextSignalAspect(Aspect.Clear_1);
                }
                if (VLParpadeo2.Started)
                {
                    if (VLParpadeo2.Triggered)
                    {
                        VLParpadeo2.Stop();
                        VLParpadeo1.Start();
                    }
                    else SetNextSignalAspect(Aspect.None);
                }
                if (RojoEncendido.Triggered)
                {
                    SetNextSignalAspect(Aspect.None);
                    RojoEncendido.Stop();
                }
                if (ASFAOverrideTimer.Triggered)
                {
                    RebaseAuto = false;
                    RebasePressed = false;
                }
                if (SpeedMpS() > MaxSpeed) Urgencia = true;
                if (Rec.Started && !Rec.Triggered)
                {
                    SetVigilanceAlarmDisplay(true);
                    RecOn = true;
                    TriggerSoundPenalty1();
                }
                if (Rec.Triggered)
                {
                    Urgencia = true;
                    TriggerSoundPenalty2();
                    Rec.Stop();
                    SetVigilanceAlarmDisplay(false);
                    RecOn = false;
                }
                if (Pressed && Rec.Started)
                {
                    Rec.Stop();
                    TriggerSoundPenalty2();
                    SetVigilanceAlarmDisplay(false);
                    RecOn = false;
                    SetNextSignalAspect(Aspect.None);
                    MaxSpeed = float.MaxValue;
                }
                FrenarOn = UltimaInfo == Aspect.Approach_1 && Rec.Started;
                ParadaOn = RojoEncendido.Started;
                VLOn = VLParpadeo1.Started && !VLParpadeo1.Triggered;
                if (Urgencia && RearmeFrenoPressed && SpeedMpS() < 1.5f) Urgencia = false;
            }
            else Urgencia = false;
        }
    }
    public class ETCS : TrainControlSystem
    {
        public enum Level
        {
            L0,
            NTC,
            L1,
            L2,
            L3
        }
        public Level CurrentLevel = Level.L0;

        public enum Mode
        {
            FS,
            LS,
            OS,
            SR,
            SH,
            UN,
            PS,
            SL,
            SB,
            TR,
            PT,
            SF,
            IS,
            NP,
            NL,
            SN,
            RV
        }
        bool Pressed;
        public Mode CurrentMode = Mode.SB;
        EurobaliseTelegram EBD;
        TrackToTrainEuroradioMessage ERM;
        TrainToTrackEuroradioMessage TRBCM;
        List<TemporarySpeedRestriction> TSRs = new List<TemporarySpeedRestriction>();
        SpeedProfile SRsp;
        SpeedProfile MAsp;
        SpeedProfile OVsp;
        List<SpeedProfile> MPsp;
        List<SpeedProfile> SSPsp;
        List<SpeedProfile> TSRsp;
        MovementAuthority MA;
        ModeProfile MP;
        InternationalStaticSpeedProfile ISSP;
        bool OverrideEoA;
        bool AcknowledgeEoA;
        float dEstFront;
        float dMinFront;
        float dMaxFront;
        float dOrA = 0;
        float dUrA = 0;
        class BaliseGroup
        {
            public float Pos;
            public float Acc;
            public Func<float> Dist;
            public int NID;
            public BaliseGroup(float pos, float accuracy, int NID_BG, TrainControlSystem tcs)
            {
                Pos = pos;
                Acc = accuracy;
                NID = NID_BG;
                float x = tcs.DistanceM();
                Dist = () => tcs.DistanceM() - x;
            }
        }
        class Train_Position
        {
            public void SetLRBG(BaliseGroup bg)
            {
                PrevLRBG = LRBG;
                LRBG = bg;
                foreach (var lrbg in LRBGs)
                {
                    lrbg.Dist = () => LRBG.Dist() + (LRBG.Pos - lrbg.Pos);
                }
                LRBGs.Add(LRBG);
            }
            public BaliseGroup LRBG;
            public BaliseGroup PrevLRBG;
            public List<BaliseGroup> LRBGs = new List<BaliseGroup>();
            public float DoubtUnder { get { return LRBG.Dist() * 0.004f + LRBG.Acc; } }
            public float DoubtOver { get { return LRBG.Dist() * 0.004f + LRBG.Acc; } }
        }
        static string[,] ETCSFixedText = {
            {"Entering FS", "Entrada en FS"},
            {"Train TRIP", "Modo TRIP"},
            {"Unauthorized passing of EoA/LoA", "EoA o LoA rebasado"},
            {"Balise read error", "Datos de eurobaliza no consistentes" },
            {"Acknowledge SR mode", "Reconocer modo SR"},
            {"Acknowledge OS mode", "Reconocer modo OS"},
            {"Acknowledge UN mode", "Reconocer modo UN" },
            {"Emergency brake test: going", "Test de freno de emergencia: en curso" },
            {"Emergency brake test: completed", "Test de freno de emergencia: completado" },
            {"Service brake applied", "Freno de servicio aplicado" },
            {"Standstill supervision", "Standstill supervision" },
            {"Reverse movement protection", "Reverse movement protection"},
            {"Roll away protection", "Roll away protection" }
        };
        class ETCSMessage
        {
            public string Text;
            public int id = -1;
            public float Time;
            public Func<bool> Revoke;
            public int Priority;
            public bool Acknowledgement;
            public bool Acknowledged;
            public bool Displayed;
            public ETCSMessage(string text, float time, Func<bool> revoke, int priority, bool ack)
            {
                Text = text;
                id = -1;
                for (int i = 0; i < ETCSFixedText.Length / 2; i++)
                {
                    if (Text == ETCSFixedText[i, 1])
                    {
                        id = i;
                        break;
                    }
                }
                Time = time;
                Revoke = revoke;
                Priority = priority;
                Acknowledgement = ack;
                Acknowledged = false;
                Displayed = false;
            }
            public override bool Equals(object obj)
            {
                var a = obj as ETCSMessage;
                if (a != null)
                {
                    if (id == a.id && (Text == a.Text || id > -1)) return true;
                    else return false;
                }
                else return base.Equals(obj);
            }
            public override int GetHashCode()
            {
                return base.GetHashCode();
            }
        }
        List<ETCSMessage> Messages;
        List<ETCSMessage> DispMsg;
        List<ETCSMessage> AckMsg = new List<ETCSMessage>();
        float LastAck = -1;
        protected void ViewMessages()
        {
            Messages.RemoveAll(x => x.Revoke());
            AckMsg.RemoveAll(x => !Messages.Contains(x));
            DispMsg = new List<ETCSMessage>();
            DispMsg.Clear();
            if (Messages.Count == 0) return;
            foreach (var m in Messages)
            {
                if (m.Acknowledgement)
                {
                    if (!AckMsg.Contains(m)) AckMsg.Add(m);
                }
                else
                {
                    DispMsg.Add(m);
                }
            }
            if (AckMsg.Count > 0)
            {
                var A = AckMsg[0];
                if (LastAck + 1 <= ClockTime() || A.Displayed)
                {
                    DispMsg.Add(A);
                    if (!A.Displayed)
                    {
                        if (A.id == 1) TriggerSoundPenalty1();
                        else TriggerSoundInfo1();
                        A.Displayed = true;
                    }
                    LastAck = ClockTime();
                }
                Message(Orts.Simulation.ConfirmLevel.None, A.id > -1 ? ETCSFixedText[A.id, 0] : A.Text);
                if (Pressed && A.Displayed)
                {
                    A.Acknowledged = true;
                    if (A.Revoke()) A.Revoke = () => true;
                    A.Acknowledgement = false;
                    Pressed = false;
                    AckMsg.Remove(A);
                }
                return;
            }
            else
            {
                DispMsg.Sort(delegate (ETCSMessage x, ETCSMessage y)
                {
                    if (x == y) return 0;
                    else if (x == null) return -1;
                    else if (y == null) return 1;
                    else if (x.Priority < y.Priority) return 1;
                    else if (x.Priority > y.Priority) return -1;
                    else if (x.Priority == y.Priority)
                    {
                        if (x.Time > y.Time) return 1;
                        else if (x.Time < y.Time) return -1;
                        else return 0;
                    }
                    else return 0;
                });
                foreach (var m in DispMsg)
                {
                    if (m.Revoke()) m.Revoke = () => true;
                    if (!m.Displayed)
                    {
                        if (m.Priority == 1) TriggerSoundInfo1();
                        m.Displayed = true;
                    }
                    Message(Orts.Simulation.ConfirmLevel.None, m.id > -1 ? ETCSFixedText[m.id, 0] : m.Text);
                }
            }
        }
        Train_Position TrainPosition;
        Linking link;
        protected void Odometry()
        {
            if (TrainPosition == null)
            {
                TrainPosition = new Train_Position();
                TrainPosition.SetLRBG(new BaliseGroup(0, NationalValues.Q_NVLOCACC, 0, this));
            }
            dEstFront = TrainPosition.LRBG.Dist();
            dMinFront = dEstFront - TrainPosition.DoubtUnder;
            dMaxFront = dEstFront + TrainPosition.DoubtOver;
        }
        TCS_Spain tcs;
        public ETCS(TrainControlSystem tcss)
        {
            tcs = (TCS_Spain)tcss;
            // AbstractScriptClass
            ClockTime = () => tcs.ClockTime();
            DistanceM = () => tcs.DistanceM();

            // TrainControlSystem
            IsTrainControlEnabled = tcs.IsTrainControlEnabled;
            IsDirectionReverse = tcs.IsDirectionReverse;
            IsBrakeEmergency = tcs.IsBrakeEmergency;
            IsBrakeFullService = tcs.IsBrakeFullService;
            PowerAuthorization = tcs.PowerAuthorization;
            TrainLengthM = tcs.TrainLengthM;
            SpeedMpS = tcs.SpeedMpS;
            BrakePipePressureBar = tcs.BrakePipePressureBar;
            CurrentSignalSpeedLimitMpS = tcs.CurrentSignalSpeedLimitMpS;
            CurrentPostSpeedLimitMpS = tcs.CurrentPostSpeedLimitMpS;
            IsAlerterEnabled = tcs.IsAlerterEnabled;
            AlerterSound = tcs.AlerterSound;
            SetHorn = (value) => { };
            SetFullBrake = (value) =>
            {
                ServiceBrake = true;
            };
            SetEmergencyBrake = (value) =>
            {
                EmergencyBraking = true;
            };
            SetThrottleController = (value) => { };
            SetDynamicBrakeController = (value) => { };
            SetVigilanceAlarmDisplay = (value) => { if (tcs.ActiveCCS == TCS_Spain.CCS.ETCS) tcs.SetVigilanceAlarmDisplay(value); };
            SetVigilanceEmergencyDisplay = (value) => { if (tcs.ActiveCCS == TCS_Spain.CCS.ETCS) tcs.SetVigilanceEmergencyDisplay(value); };
            SetOverspeedWarningDisplay = (value) => { if (tcs.ActiveCCS == TCS_Spain.CCS.ETCS) tcs.SetOverspeedWarningDisplay(value); };
            SetPenaltyApplicationDisplay = (value) => { if (tcs.ActiveCCS == TCS_Spain.CCS.ETCS) tcs.SetPenaltyApplicationDisplay(value); };
            SetMonitoringStatus = (value) => { if (tcs.ActiveCCS == TCS_Spain.CCS.ETCS) tcs.SetMonitoringStatus(value); };
            SetCurrentSpeedLimitMpS = (value) => { if (tcs.ActiveCCS == TCS_Spain.CCS.ETCS) tcs.SetCurrentSpeedLimitMpS(value); };
            SetNextSpeedLimitMpS = (value) => { if (tcs.ActiveCCS == TCS_Spain.CCS.ETCS) tcs.SetNextSpeedLimitMpS(value); };
            SetInterventionSpeedLimitMpS = (value) => { };
            SetNextSignalAspect = (value) => { if (tcs.ActiveCCS == TCS_Spain.CCS.ETCS) tcs.SetNextSignalAspect(value); };
            TriggerSoundAlert1 = () => { if (tcs.ActiveCCS == TCS_Spain.CCS.ETCS) tcs.TriggerSoundAlert1(); };
            TriggerSoundAlert2 = () => { if (tcs.ActiveCCS == TCS_Spain.CCS.ETCS) tcs.TriggerSoundAlert2(); };
            TriggerSoundInfo1 = () => { if (tcs.ActiveCCS == TCS_Spain.CCS.ETCS) tcs.TriggerSoundInfo1(); };
            TriggerSoundInfo2 = () => { if (tcs.ActiveCCS == TCS_Spain.CCS.ETCS) tcs.TriggerSoundInfo2(); };
            TriggerSoundPenalty1 = () => { if (tcs.ActiveCCS == TCS_Spain.CCS.ETCS) tcs.TriggerSoundPenalty1(); };
            TriggerSoundPenalty2 = () => { if (tcs.ActiveCCS == TCS_Spain.CCS.ETCS) tcs.TriggerSoundPenalty2(); };
            TriggerSoundWarning1 = () => { if (tcs.ActiveCCS == TCS_Spain.CCS.ETCS) tcs.TriggerSoundWarning1(); };
            TriggerSoundWarning2 = () => { if (tcs.ActiveCCS == TCS_Spain.CCS.ETCS) tcs.TriggerSoundWarning2(); };
            TriggerSoundSystemActivate = () => { if (tcs.ActiveCCS == TCS_Spain.CCS.ETCS) tcs.TriggerSoundSystemActivate(); };
            TriggerSoundSystemDeactivate = () => { if (tcs.ActiveCCS == TCS_Spain.CCS.ETCS) tcs.TriggerSoundSystemDeactivate(); };
            TrainSpeedLimitMpS = tcs.TrainSpeedLimitMpS;
            NextSignalSpeedLimitMpS = tcs.NextSignalSpeedLimitMpS;
            NextSignalAspect = tcs.NextSignalAspect;
            NextSignalDistanceM = tcs.NextSignalDistanceM;
            NextPostSpeedLimitMpS = tcs.NextPostSpeedLimitMpS;
            NextPostDistanceM = tcs.NextPostDistanceM;

            SpeedCurve = tcs.SpeedCurve;
            DistanceCurve = tcs.DistanceCurve;
            Deceleration = tcs.Deceleration;
            SetPantographsDown = () =>
            {

            };
            SetPowerAuthorization = (value) =>
            {

            };
            GetBoolParameter = tcs.GetBoolParameter;
            GetIntParameter = tcs.GetIntParameter;
            GetFloatParameter = tcs.GetFloatParameter;
            GetStringParameter = tcs.GetStringParameter;
            Message = (a, b) => tcs.Message(a, "ETCS: " + b);
        }
        public override void Initialize()
        {
            switch (tcs.ETCSInstalledLevel)
            {
                case 0:
                    CurrentLevel = Level.L0;
                    break;
                case 1:
                    CurrentLevel = Level.L1;
                    break;
                case 2:
                    CurrentLevel = Level.L2;
                    break;
                case 3:
                    CurrentLevel = Level.L3;
                    break;
                case 4:
                    CurrentLevel = Level.NTC;
                    break;
            }
            MPsp = new List<SpeedProfile>();
            TSRsp = new List<SpeedProfile>();
            SSPsp = new List<SpeedProfile>();
            SpdProf = new List<SpeedProfile>();
        }
        public override void HandleEvent(TCSEvent evt, string message)
        {
            switch (evt)
            {
                case TCSEvent.AlerterReleased:
                    Pressed = true;
                    break;
                case TCSEvent.AlerterPressed:
                    TriggerSoundInfo2();
                    break;
            }
        }
        public override void SetEmergency(bool emergency)
        {
        }
        bool Start;
        void SetNV()
        {
            if (tcs.LineaConvencional)
            {
                NationalValues.D_NVOVTRP = 80;
                NationalValues.V_NVSTFF = MpS.FromKpH(100);
                NationalValues.T_NVOVTRP = 20;
                NationalValues.V_NVREL = MpS.FromKpH(20);
                NationalValues.D_NVPOTRP = 50;
            }
            else
            {
                NationalValues.T_NVOVTRP = 80;
                NationalValues.V_NVSTFF = MpS.FromKpH(100);
                NationalValues.V_NVSHUNT = MpS.FromKpH(50);
                NationalValues.V_NVONSIGHT = MpS.FromKpH(50);
                NationalValues.T_NVOVTRP = 20;
                NationalValues.V_NVREL = MpS.FromKpH(35);
                NationalValues.D_NVROLL = 5;
                NationalValues.D_NVPOTRP = 50;
            }
        }
        float MAEnd;
        Aspect ETCSAspect;
        Aspect ETCSPreviousAspect;
        protected TrackToTrainEuroradioMessage RBC(TrainToTrackEuroradioMessage ERM)
        {
            L2MA MA;
            ModeProfile MP = null;
            int N_ITER = 0;
            int[] L_SECTION;
            int[] Q_SECTIONTIMER;
            int[] T_SECTIONTIMER;
            int[] D_SECTIONTIMERSTOPLOC;
            int NumPost;
            int iter;
            int Q_SCALE = 1;
            int V_MAIN = 120;
            if (ERM.NID_MESSAGE == 1)
            {
                var a = (MARequest)ERM;
            }
            if (tcs.SignalPassed)
            {
                if (ETCSAspect == Aspect.Stop) return new UnconditionalEmergencyStop();
                switch (ETCSAspect)
                {
                    case Aspect.Approach_1:
                    case Aspect.Approach_3:
                        if (tcs.IsPN && CurrentSignalSpeedLimitMpS() < MpS.FromKpH(35f) && CurrentSignalSpeedLimitMpS() > MpS.FromKpH(25f)) { }
                        else N_ITER = 1;
                        break;
                    case Aspect.Clear_1:
                    case Aspect.Approach_2:
                        N_ITER = 2;
                        break;
                    case Aspect.Clear_2:
                        if (CurrentPostSpeedLimitMpS() < MpS.FromKpH(165) && CurrentPostSpeedLimitMpS() > MpS.FromKpH(155) && tcs.IsPN) { }
                        else if (NextSignalAspect(0) == Aspect.Clear_1) N_ITER = 3;
                        else N_ITER = 2;
                        break;
                    case Aspect.StopAndProceed:
                    case Aspect.Permission:
                    case Aspect.Restricted:
                        N_ITER = 1;
                        V_MAIN = 6;
                        break;
                    case Aspect.Stop:
                    default:
                        V_MAIN = 0;
                        N_ITER = 0;
                        break;
                }
                for (int i = 0; i < N_ITER; i++)
                {
                    if ((NextSignalAspect(i) == Aspect.Approach_1 && NextSignalSpeedLimitMpS(i) < MpS.FromKpH(35f) && NextSignalSpeedLimitMpS(i) > MpS.FromKpH(25f)) || (NextSignalAspect(i) == Aspect.Clear_2 && NextSignalSpeedLimitMpS(i).AlmostEqual(MpS.FromKpH(160), 2))) N_ITER++;
                }
                MAEnd = NextSignalDistanceM(N_ITER - 1);
                if (ETCSAspect == Aspect.StopAndProceed || ETCSAspect == Aspect.Permission)
                {
                    MP = new ModeProfile(0, 0, 1, 1, new int[] { 0 }, new int[] { 0 }, new int[] { 6 }, new int[] { (int)MAEnd }, new int[] { 20 }, new int[] { 0 });
                }
                if (ETCSAspect == Aspect.Restricted)
                {
                    MP = new ModeProfile(0, 0, 1, 1, new int[] { 0 }, new int[] { 1 }, new int[] { 6 }, new int[] { (int)MAEnd }, new int[] { 20 }, new int[] { 0 });
                }
                L_SECTION = new int[N_ITER];
                Q_SECTIONTIMER = new int[N_ITER];
                T_SECTIONTIMER = new int[N_ITER];
                D_SECTIONTIMERSTOPLOC = new int[N_ITER];
                for (int i = 0; i < N_ITER; i++)
                {
                    if (i == 0)
                    {
                        L_SECTION[i] = (int)NextSignalDistanceM(i);
                        D_SECTIONTIMERSTOPLOC[i] = (int)(NextSignalDistanceM(i));
                    }
                    else
                    {
                        L_SECTION[i] = (int)(NextSignalDistanceM(i) - NextSignalDistanceM(i - 1));
                        D_SECTIONTIMERSTOPLOC[i] = (int)(NextSignalDistanceM(i) - NextSignalDistanceM(i - 1));
                    }
                    Q_SECTIONTIMER[i] = 1;
                    T_SECTIONTIMER[i] = 10 * 60;
                }
                MA = new L2MA(0, 0, Q_SCALE, V_MAIN, 0, 0, N_ITER, L_SECTION, Q_SECTIONTIMER, T_SECTIONTIMER, D_SECTIONTIMERSTOPLOC, 15, 0, 0, 0, 1, 10, 6, 0, 0, 0, 0, 0);
                NumPost = 2;
                if (NextPostDistanceM(0) <= MAEnd) NumPost = 3;
                if (NextPostDistanceM(1) <= MAEnd) NumPost = 4;
                if (NextPostDistanceM(2) <= MAEnd) NumPost = 5;
                if (NextPostDistanceM(3) <= MAEnd) NumPost = 6;
                if (NextPostDistanceM(4) <= MAEnd) NumPost = 7;
                if (NextPostDistanceM(5) <= MAEnd) NumPost = 8;
                if (NextPostDistanceM(6) <= MAEnd) NumPost = 9;
                if (NextPostDistanceM(7) <= MAEnd) NumPost = 10;
                if (NextPostDistanceM(0) < 0)
                {
                    NumPost = 2;
                }
                int[] Dist = new int[NumPost];
                int[] Spd = new int[NumPost];
                int[] Front = new int[NumPost];
                iter = 0;
                for (iter = 0; iter < NumPost - 1; iter++)
                {
                    Dist[iter] = (int)NextPostDistanceM(iter);
                    if (iter == 0) Spd[iter] = (int)(MpS.ToKpH(CurrentPostSpeedLimitMpS()) / 5);
                    else Spd[iter] = (int)(MpS.ToKpH(NextPostSpeedLimitMpS(iter - 1)) / 5);
                    if (Spd[iter] > MpS.ToKpH(NextPostSpeedLimitMpS(iter)) / 5) Front[iter] = 1;
                    else Front[iter] = 0;
                }
                Spd[NumPost - 1] = 127;
                Dist[NumPost - 1] = (int)MAEnd;
                Front[NumPost - 1] = 0;
                Packet Speed = new InternationalStaticSpeedProfile(2, 0, 1, Dist, Spd, Front, new int[1] { NumPost }, new int[NumPost], null, null, new int[NumPost]);
                if (MP != null) return new RadioMA(1, 0, 0, MA, Speed, MP);
                return new RadioMA(1, 0, 0, MA, Speed);
            }
            else if (ETCSPreviousAspect != Aspect.Stop && ETCSPreviousAspect != Aspect.StopAndProceed && ETCSPreviousAspect != Aspect.Restricted && (ETCSAspect != NextSignalAspect(0) || ((int)ClockTime() % 10 == 0)))
            {
                for (N_ITER = 1; NextSignalAspect(N_ITER - 1) != Aspect.Stop && NextSignalAspect(N_ITER - 1) != Aspect.StopAndProceed && NextSignalAspect(N_ITER - 1) != Aspect.Restricted && N_ITER < 6; N_ITER++) ;
                MAEnd = NextSignalDistanceM(N_ITER - 1);
                L_SECTION = new int[N_ITER];
                Q_SECTIONTIMER = new int[N_ITER];
                T_SECTIONTIMER = new int[N_ITER];
                D_SECTIONTIMERSTOPLOC = new int[N_ITER];
                for (int i = 0; i < N_ITER; i++)
                {
                    if (i == 0)
                    {
                        L_SECTION[i] = (int)NextSignalDistanceM(i);
                        D_SECTIONTIMERSTOPLOC[i] = (int)(NextSignalDistanceM(i));
                    }
                    else
                    {
                        L_SECTION[i] = (int)(NextSignalDistanceM(i) - NextSignalDistanceM(i - 1));
                        D_SECTIONTIMERSTOPLOC[i] = (int)(NextSignalDistanceM(i) - NextSignalDistanceM(i - 1));
                    }
                    Q_SECTIONTIMER[i] = 1;
                    T_SECTIONTIMER[i] = 10 * 60;
                }
                MA = new L2MA(0, 0, Q_SCALE, V_MAIN, 0, 0, N_ITER, L_SECTION, Q_SECTIONTIMER, T_SECTIONTIMER, D_SECTIONTIMERSTOPLOC, 15, 0, 0, 0, 1, 10, 6, 0, 0, 0, 0, 0);
                NumPost = 2;
                if (NextPostDistanceM(0) <= MAEnd) NumPost = 3;
                if (NextPostDistanceM(1) <= MAEnd) NumPost = 4;
                if (NextPostDistanceM(2) <= MAEnd) NumPost = 5;
                if (NextPostDistanceM(3) <= MAEnd) NumPost = 6;
                if (NextPostDistanceM(4) <= MAEnd) NumPost = 7;
                if (NextPostDistanceM(5) <= MAEnd) NumPost = 8;
                if (NextPostDistanceM(6) <= MAEnd) NumPost = 9;
                if (NextPostDistanceM(7) <= MAEnd) NumPost = 10;
                if (NextPostDistanceM(0) < 0)
                {
                    NumPost = 2;
                }
                int[] Dist = new int[NumPost];
                int[] Spd = new int[NumPost];
                int[] Front = new int[NumPost];
                iter = 0;
                for (iter = 0; iter < NumPost - 1; iter++)
                {
                    Dist[iter] = (int)NextPostDistanceM(iter);
                    if (iter == 0) Spd[iter] = (int)(MpS.ToKpH(CurrentPostSpeedLimitMpS()) / 5);
                    else Spd[iter] = (int)(MpS.ToKpH(NextPostSpeedLimitMpS(iter - 1)) / 5);
                    if (Spd[iter] > MpS.ToKpH(NextPostSpeedLimitMpS(iter)) / 5) Front[iter] = 1;
                    else Front[iter] = 0;
                }
                Spd[NumPost - 1] = 127;
                Dist[NumPost - 1] = (int)MAEnd;
                Front[NumPost - 1] = 0;
                Packet Speed = new InternationalStaticSpeedProfile(2, 0, 1, Dist, Spd, Front, new int[1] { NumPost }, new int[NumPost], null, null, new int[NumPost]);
                return new RadioMA(1, 0, 0, MA, Speed);
            }
            else return null;
        }
        int NID_BG = 0;
        protected EurobaliseTelegram Eurobalise()
        {
            L1MA MA = null;
            ModeProfile MP = null;
            int N_ITER = 0;
            int[] L_SECTION;
            int[] Q_SECTIONTIMER;
            int[] T_SECTIONTIMER;
            int[] D_SECTIONTIMERSTOPLOC;
            int NumPost;
            int iter;
            int Q_SCALE = 1;
            int V_MAIN = 60;
            int V_LOA = 0;
            if (tcs.SBalisePassed)
            {
                NID_BG = NID_BG + 1;
                switch (NextSignalAspect(0))
                {
                    case Aspect.Approach_1:
                    case Aspect.Approach_3:
                        if (NextSignalAspect(0) == Aspect.Approach_1 && NextSignalSpeedLimitMpS(0) < MpS.FromKpH(35f) && NextSignalSpeedLimitMpS(0) > MpS.FromKpH(25f))
                        {
                            NID_BG--;
                            return new EurobaliseTelegram(NID_BG + 800, 0, new TemporarySpeedRestriction(0, 0, 1, 1, 490, 20, 1, 2));
                        }
                        N_ITER = 2;
                        break;
                    case Aspect.Clear_1:
                    case Aspect.Approach_2:
                        N_ITER = 3;
                        break;
                    case Aspect.Clear_2:
                        if (NextSignalSpeedLimitMpS(0) < MpS.FromKpH(165) && NextSignalSpeedLimitMpS(0) > MpS.FromKpH(155))
                        {
                            NID_BG--;
                            return new EurobaliseTelegram(NID_BG + 800, 0, new TemporarySpeedRestrictionRevocation(0, 0, 1));
                        }
                        if (NextSignalAspect(1) == Aspect.Clear_1) N_ITER = 4;
                        else N_ITER = 3;
                        break;
                    case Aspect.StopAndProceed:
                    case Aspect.Permission:
                    case Aspect.Restricted:
                        N_ITER = 2;
                        V_MAIN = 6;
                        break;
                    case Aspect.Stop:
                    default:
                        N_ITER = 1;
                        break;
                }
                for (int i = 1; i < N_ITER; i++)
                {
                    if ((NextSignalAspect(i) == Aspect.Approach_1 && NextSignalSpeedLimitMpS(i) < MpS.FromKpH(35f) && NextSignalSpeedLimitMpS(i) > MpS.FromKpH(25f)) || (NextSignalAspect(i) == Aspect.Clear_2 && NextSignalSpeedLimitMpS(i).AlmostEqual(MpS.FromKpH(160), 2))) N_ITER++;
                }
                MAEnd = NextSignalDistanceM(N_ITER - 1);
                if (NextSignalAspect(N_ITER - 1) == Aspect.StopAndProceed) V_LOA = 6;
                if (NextSignalAspect(0) == Aspect.StopAndProceed || NextSignalAspect(0) == Aspect.Permission)
                {
                    MP = new ModeProfile(0, 0, 1, 1, new int[] { 0 }, new int[] { 0 }, new int[] { 6 }, new int[] { (int)MAEnd }, new int[] { 50 }, new int[] { 0 });
                }
                if (NextSignalAspect(0) == Aspect.Restricted)
                {
                    MP = new ModeProfile(0, 0, 1, 1, new int[] { 0 }, new int[] { 1 }, new int[] { 6 }, new int[] { (int)MAEnd }, new int[] { 50 }, new int[] { 0 });
                }
                L_SECTION = new int[N_ITER];
                Q_SECTIONTIMER = new int[N_ITER];
                T_SECTIONTIMER = new int[N_ITER];
                D_SECTIONTIMERSTOPLOC = new int[N_ITER];
                for (int i = 0; i < N_ITER; i++)
                {
                    if (i == 0)
                    {
                        L_SECTION[i] = 10;
                        D_SECTIONTIMERSTOPLOC[i] = 10;
                    }
                    else
                    {
                        L_SECTION[i] = (int)(NextSignalDistanceM(i) - NextSignalDistanceM(i - 1));
                        D_SECTIONTIMERSTOPLOC[i] = (int)(NextSignalDistanceM(i) - NextSignalDistanceM(i - 1));
                    }
                    Q_SECTIONTIMER[i] = 1;
                    T_SECTIONTIMER[i] = 10 * 60;
                }
                MAEnd = 0;
                foreach (var s in L_SECTION)
                {
                    MAEnd += s;
                }
                int V_RELEASE = 127;
                MA = new L1MA(IsDirectionReverse() ? 0 : 1, 0, Q_SCALE, V_MAIN, V_LOA, 0, N_ITER, L_SECTION, Q_SECTIONTIMER, T_SECTIONTIMER, D_SECTIONTIMERSTOPLOC, 15, 0, 0, 0, 1, 15, V_RELEASE, 0, 0, 0, 0, 0);
                NumPost = 2;
                NextPostDistanceM(7);
                if (NextPostDistanceM(0) <= MAEnd) NumPost = 3;
                if (NextPostDistanceM(1) <= MAEnd) NumPost = 4;
                if (NextPostDistanceM(2) <= MAEnd) NumPost = 5;
                if (NextPostDistanceM(3) <= MAEnd) NumPost = 6;
                if (NextPostDistanceM(4) <= MAEnd) NumPost = 7;
                if (NextPostDistanceM(5) <= MAEnd) NumPost = 8;
                if (NextPostDistanceM(6) <= MAEnd) NumPost = 9;
                if (NextPostDistanceM(7) <= MAEnd) NumPost = 10;
                if (NextPostDistanceM(0) < 0)
                {
                    NumPost = 2;
                }
                int[] Dist = new int[NumPost];
                int[] Spd = new int[NumPost];
                int[] Front = new int[NumPost];
                iter = 0;
                Dist[0] = 0;
                Spd[0] = (int)MpS.ToKpH((CurrentPostSpeedLimitMpS() + 0.5f) / 5);
                Front[0] = 0;
                for (iter = 1; iter < NumPost; iter++)
                {
                    if (iter + 1 == NumPost)
                    {
                        Spd[iter] = 127;
                        Dist[iter] = (int)MAEnd;
                    }
                    else
                    {
                        Spd[iter] = (int)MpS.ToKpH((NextPostSpeedLimitMpS(iter - 1) + 0.5f) / 5);
                        Dist[iter] = (int)NextPostDistanceM(iter - 1);
                    }
                    Front[iter] = 0;
                }
                var Packets = new List<Packet>();
                var Link = new Linking(2, 0, 1, (int)(NextSignalDistanceM(1) - 10), 0, 0, NID_BG + 2, 0, 0, 5);
                Packet Speed = new InternationalStaticSpeedProfile(2, 0, 1, Dist, Spd, Front, new int[1] { NumPost }, new int[NumPost], null, null, new int[NumPost]);
                if (NextSignalAspect(0) != Aspect.Stop) Packets.Add(Link);
                Packets.Add(Speed);
                Packets.Add(MA);
                if (NextSignalAspect(0) == Aspect.Approach_2) Packets.Add(new TemporarySpeedRestriction(0, 0, 1, 5, (int)(NextSignalDistanceM(1) + 20), (int)(NextSignalDistanceM(2) - NextSignalDistanceM(1) + 100), 0, NextSignalSpeedLimitMpS(0) > MpS.FromKpH(100) ? 12 : 6));
                if (MP != null) Packets.Add(MP);
                return new EurobaliseTelegram(NID_BG, 1, Packets.ToArray());
            }
            else if (tcs.PreviaPassed)
            {
                switch (NextSignalAspect(0))
                {
                    case Aspect.Approach_1:
                    case Aspect.Approach_3:
                        if (NextSignalAspect(0) == Aspect.Approach_1 && NextSignalSpeedLimitMpS(0) < MpS.FromKpH(35f) && NextSignalSpeedLimitMpS(0) > MpS.FromKpH(25f))
                        {
                            return null;
                        }
                        N_ITER = 2;
                        break;
                    case Aspect.Clear_1:
                    case Aspect.Approach_2:
                        N_ITER = 3;
                        break;
                    case Aspect.Clear_2:
                        if (NextSignalSpeedLimitMpS(0) < MpS.FromKpH(165) && NextSignalSpeedLimitMpS(0) > MpS.FromKpH(155))
                        {
                            return null;
                        }
                        if (NextSignalAspect(1) == Aspect.Clear_1) N_ITER = 4;
                        else N_ITER = 3;
                        break;
                    case Aspect.StopAndProceed:
                    case Aspect.Permission:
                    case Aspect.Restricted:
                        N_ITER = 1;
                        V_LOA = 6;
                        return null;
                        break;
                    case Aspect.Stop:
                    default:
                        N_ITER = 1;
                        break;
                }
                for (int i = 1; i < N_ITER; i++)
                {
                    if ((NextSignalAspect(i) == Aspect.Approach_1 && NextSignalSpeedLimitMpS(i) < MpS.FromKpH(35f) && NextSignalSpeedLimitMpS(i) > MpS.FromKpH(25f)) || (NextSignalAspect(i) == Aspect.Clear_2 && NextSignalSpeedLimitMpS(i).AlmostEqual(MpS.FromKpH(160), 2))) N_ITER++;
                }
                MAEnd = NextSignalDistanceM(N_ITER - 1) - NextSignalDistanceM(0) + 10;
                if (NextSignalAspect(N_ITER - 1) == Aspect.StopAndProceed) V_LOA = 6;
                if (NextSignalAspect(0) == Aspect.StopAndProceed || NextSignalAspect(0) == Aspect.Permission)
                {
                    MP = new ModeProfile(0, 0, 1, 1, new int[] { 0 }, new int[] { 0 }, new int[] { 127 }, new int[] { (int)MAEnd }, new int[] { 50 }, new int[] { 0 });
                }
                if (NextSignalAspect(0) == Aspect.Restricted)
                {
                    MP = new ModeProfile(0, 0, 1, 1, new int[] { 0 }, new int[] { 1 }, new int[] { 127 }, new int[] { (int)MAEnd }, new int[] { 50 }, new int[] { 0 });
                }
                L_SECTION = new int[N_ITER];
                Q_SECTIONTIMER = new int[N_ITER];
                T_SECTIONTIMER = new int[N_ITER];
                D_SECTIONTIMERSTOPLOC = new int[N_ITER];
                for (int i = 0; i < N_ITER; i++)
                {
                    if (i == 0)
                    {
                        L_SECTION[i] = 10;
                        D_SECTIONTIMERSTOPLOC[i] = 10;
                    }
                    else
                    {
                        L_SECTION[i] = (int)(NextSignalDistanceM(i) - NextSignalDistanceM(i - 1));
                        D_SECTIONTIMERSTOPLOC[i] = (int)(NextSignalDistanceM(i) - NextSignalDistanceM(i - 1));
                    }
                    Q_SECTIONTIMER[i] = 1;
                    T_SECTIONTIMER[i] = 10 * 60;
                }
                MAEnd = 0;
                foreach (var s in L_SECTION)
                {
                    MAEnd += s;
                }
                int V_RELEASE = 127;
                MA = new L1MA(IsDirectionReverse() ? 0 : 1, 0, Q_SCALE, V_MAIN, V_LOA, 0, N_ITER, L_SECTION, Q_SECTIONTIMER, T_SECTIONTIMER, D_SECTIONTIMERSTOPLOC, 15, 0, 0, 0, 1, 15, V_RELEASE, 0, 0, 0, 0, 0);
                return new EurobaliseTelegram(NID_BG + 500, 0, new InfillLocationReference(2, 0, 0, 0, NID_BG + 2), MA);
            }
            else
            {
                return null;
            }
        }
        public void Botones()
        {
        }
        public override void Update()
        {
            SetNV();
            if (Messages == null) Messages = new List<ETCSMessage>();
            Odometry();
            ServiceBrake = EmergencyBraking = false;
            TrainInfo.Calc();
            EBD = Eurobalise();
            ManageBaliseData();
            switch (CurrentLevel)
            {
                case Level.L0:
                    var m = new ETCSMessage("Reconocer modo UN", ClockTime(), () => false, 0, true);
                    if (CurrentMode != Mode.UN)
                    {
                        m.Revoke = () => m.Acknowledged;
                        if (!Messages.Contains(m)) Messages.Add(m);
                    }
                    CurrentMode = Mode.UN;
                    if (!(tcs.ASFA as ASFAclasico).Activated)
                    {
                        tcs.ActiveCCS = TCS_Spain.CCS.ETCS;
                    }
                    if (tcs.ActiveCCS == TCS_Spain.CCS.ETCS)
                    {
                        if (tcs.ASFA != null)
                        {
                            if (tcs.ASFA is ASFADigital)
                            {
                                (tcs.ASFA as ASFADigital).AKT = true;
                                (tcs.ASFA as ASFADigital).CON = false;
                            }
                        }
                        var a = Messages.Find(x => x.Equals(m));
                        if (a != null && a.Acknowledged)
                        {
                            if (tcs.ASFA != null && (tcs.ASFA as ASFAclasico).Activated)
                            {
                                tcs.ActiveCCS = TCS_Spain.CCS.ASFA;
                                (tcs.ASFA as ASFADigital).AKT = false;
                                (tcs.ASFA as ASFADigital).CON = true;
                            }
                        }
                    }
                    break;
                case Level.L1:
                    tcs.ActiveCCS = TCS_Spain.CCS.ETCS;
                    if (tcs.ASFA != null)
                    {
                        if (tcs.ASFA is ASFADigital)
                        {
                            (tcs.ASFA as ASFADigital).AKT = true;
                            (tcs.ASFA as ASFADigital).CON = false;
                        }
                        else (tcs.ASFA as ASFAclasico).Eficacia = false;
                    }
                    break;
                case Level.L2:
                case Level.L3:
                    tcs.ActiveCCS = TCS_Spain.CCS.ETCS;
                    if (tcs.ASFA != null)
                    {
                        if (tcs.ASFA is ASFADigital)
                        {
                            (tcs.ASFA as ASFADigital).AKT = true;
                            (tcs.ASFA as ASFADigital).CON = false;
                        }
                        else (tcs.ASFA as ASFAclasico).Eficacia = false;
                    }
                    ERM = RBC(TRBCM);
                    if (ERM != null)
                    {
                        switch (ERM.NID_MESSAGE)
                        {
                            case 3:
                                MA = ((RadioMA)ERM).MA;
                                if (MA.V_MAIN == 0)
                                {
                                    if (!OverrideEoA)
                                    {
                                        Trip();
                                        Messages.Add(new ETCSMessage("EoA o LoA rebasado", ClockTime(), () => CurrentMode != Mode.TR && CurrentMode != Mode.PT, 1, false));
                                    }
                                }
                                if (((RadioMA)ERM).OptionalPackets != null)
                                {
                                    foreach (var pck in ((RadioMA)ERM).OptionalPackets)
                                    {
                                        switch (pck.NID_PACKET)
                                        {
                                            case 27:
                                                ISSP = (InternationalStaticSpeedProfile)pck;
                                                break;
                                            case 80:
                                                MP = (ModeProfile)pck;
                                                break;
                                        }
                                    }
                                }
                                break;
                            case 16:
                                EmergencyBraking = true;
                                if (!OverrideEoA)
                                {
                                    Trip();
                                }
                                break;
                        }
                    }
                    break;
                case Level.NTC:
                    if (tcs.LZB != null)
                    {
                        if (tcs.ActiveCCS != TCS_Spain.CCS.LZB)
                        {
                            tcs.ActiveCCS = TCS_Spain.CCS.LZB;
                            tcs.LZB.LZBAlertarLiberar();
                            CurrentMode = Mode.SN;
                        }
                        if (tcs.LZB.LZBOE) CurrentMode = Mode.TR;
                        tcs.LZB.LZBOE = tcs.LZB.LZBEmergencyBrake = false;
                    }
                    break;
            }
            if (CurrentMode == Mode.SB)
            {
                if (TrainInfo.T_bs < 0)
                {
                    var a = BrakeTest();
                    if (a > 0)
                    {
                        TrainInfo.T_bs = TrainInfo.T_be = a + 2;
                        var ct = ClockTime();
                        Messages.Add(new ETCSMessage("Test de freno de emergencia: completado", ClockTime(), () => ClockTime() > ct + 5, 0, false));
                    }
                }
                TrainInfo.PF = 180;
                TrainInfo.Driver_id = 1234566;
                TrainInfo.Train_number = 20142;
                if (TrainLengthM() > 0) TrainInfo.Length = TrainLengthM();
                TrainInfo.R = 1;
                TrainInfo.T_traction = 0;
                TrainInfo.MaxSpeed = tcs.TrainMaxSpeed;
            }
            if (CurrentMode == Mode.SB && IsTrainControlEnabled() && TrainInfo.IsOK && !Start && Pressed)
            {
                StartMission();
                Pressed = false;
            }
            if (CurrentMode == Mode.SB && Start)
            {
                var a = Messages.Find(x => x.Equals(new ETCSMessage("Reconocer modo SR", 0, null, 0, true)));
                if (a != null && a.Acknowledged)
                {
                    SetVigilanceAlarmDisplay(false);
                    SetVigilanceEmergencyDisplay(false);
                    CurrentMode = Mode.SR;
                    SetSR();
                }
            }
            if (MA != null && (MP == null || MP.D_MAMODE[0] > dMaxFront) && ISSP != null && ISSP.D_STATIC[0] <= dMaxFront && CurrentMode != Mode.FS)
            {
                CurrentMode = Mode.FS;
                Messages.Add(new ETCSMessage("Entrada en FS", ClockTime(), () => CurrentMode != Mode.FS || ISSP.D_STATIC[0] < dMinFront - TrainLengthM(), 1, false));
                SetVigilanceAlarmDisplay(true);
            }
            if (CurrentMode == Mode.FS && ISSP.D_STATIC[0] < dMinFront - TrainLengthM())
            {
                SetVigilanceAlarmDisplay(false);
            }
            if (CurrentMode == Mode.TR) Trip();
            /*if (tcs.ASFA.RebasePressed && SpeedMpS() < NationalValues.V_NVALLOWOVTRP)
            {
                SetVigilanceAlarmDisplay(true);
                SetVigilanceEmergencyDisplay(true);
                SetNextSignalAspect(Aspect.Approach_3);
                AcknowledgeEoA = true;
                Pressed = false;
                tcs.ASFA.Pressed = false;
                tcs.ASFA.RebasePressed = false;
                tcs.ASFA.BotonesASFA();
            }*/
            if (CurrentLevel != Level.L0)
            {
                if (AcknowledgeEoA && Pressed)
                {
                    SetNextSignalAspect(Aspect.Approach_3);
                    CurrentMode = Mode.SR;
                    AcknowledgeEoA = false;
                    OverrideEoA = true;
                    MA = null;
                    ISSP = null;
                    MP = null;
                    SetVigilanceAlarmDisplay(false);
                    SetVigilanceEmergencyDisplay(false);
                    SetSR();
                    SetOverride();
                }
                if (OverrideEoA && CurrentMode != Mode.SR) OverrideEoA = false;
            }
            if (tcs.ActiveCCS == TCS_Spain.CCS.ETCS)
            {
                switch (CurrentMode)
                {
                    case Mode.FS:
                        SetNextSignalAspect(Aspect.Clear_2);
                        break;
                    case Mode.SR:
                    default:
                        if (OverrideEoA || AcknowledgeEoA) SetNextSignalAspect(Aspect.Approach_3);
                        else SetNextSignalAspect(Aspect.Clear_1);
                        break;
                    case Mode.UN:
                        SetNextSignalAspect(Aspect.Approach_2);
                        break;
                    case Mode.TR:
                        if (SpeedMpS() > 0) SetNextSignalAspect(Aspect.Stop);
                        else SetNextSignalAspect(Aspect.Approach_1);
                        break;
                    case Mode.SH:
                        SetNextSignalAspect(Aspect.Restricted);
                        break;
                    case Mode.OS:
                        SetNextSignalAspect(Aspect.StopAndProceed);
                        break;
                }
            }
            if (CurrentMode == Mode.TR || CurrentMode == Mode.NP || CurrentMode == Mode.SF || CurrentMode == Mode.SB)
            {
                if (CurrentMode != Mode.SB) EmergencyBraking = true;
                else StandstillSupervision();
                SetCurrentSpeedLimitMpS(0.3f);
                SetNextSpeedLimitMpS(0);
                SetInterventionSpeedLimitMpS(0);
                SetMonitoringStatus(MonitoringStatus.Normal);
                ViewMessages();
                return;
            }
            if (CurrentMode == Mode.PT)
            {
                SetCurrentSpeedLimitMpS(0.3f);
                SetNextSpeedLimitMpS(0);
                SetInterventionSpeedLimitMpS(0);
                SetMonitoringStatus(MonitoringStatus.Normal);
                PostTripReverse();
                ViewMessages();
                return;
            }
            if (CurrentMode != Mode.RV && CurrentMode != Mode.PT && MA != null) ReverseMovement();
            Rollaway();
            UpdateControls();
            SpeedProfiles();
            SupervisedTargets();
            SpeedMonitors();
            ETCSCurves();
            LinkingBrake &= SpeedMpS() > 0.1f;
            ServiceBrake |= LinkingBrake;
            ViewMessages();
            Pressed = false;
        }
        protected void StartMission()
        {
            SetVigilanceAlarmDisplay(true);
            SetVigilanceEmergencyDisplay(true);
            var m = new ETCSMessage("Reconocer modo SR", ClockTime(), () => false, 0, true);
            m.Revoke = () => m.Acknowledged;
            Messages.Add(m);
            Start = true;
        }
        float PostTrip = -1;
        protected void PostTripReverse()
        {
            if (PostTrip == -1) PostTrip = DistanceM();
            if ((IsDirectionForward() && DistanceM() - PostTrip > NationalValues.D_NVROLL) || (IsDirectionReverse() && DistanceM() - PostTrip > NationalValues.D_NVPOTRP))
            {
                EmergencyBraking = true;
            }
        }
        float ProtectionDistance = -1;
        bool StandstillApply = false;
        protected void StandstillSupervision()
        {
            if (ProtectionDistance == -1) ProtectionDistance = DistanceM();
            if (DistanceM() - ProtectionDistance > NationalValues.D_NVROLL)
                StandstillApply = true;
            if (StandstillApply)
            {
                EmergencyBraking = true;
                if (SpeedMpS() < 0.1f)
                {
                    SetVigilanceEmergencyDisplay(true);
                    var m = new ETCSMessage("Standstill supervision", ClockTime(), () => false, 0, true);
                    m.Revoke = () => m.Acknowledged;
                    if (!Messages.Contains(m)) Messages.Add(m);
                    var a = Messages.Find(x => x.Equals(m));
                    if (a != null && a.Acknowledged)
                    {
                        StandstillApply = false;
                        ProtectionDistance = -1;
                        SetVigilanceEmergencyDisplay(false);
                    }
                }
            }
        }
        float RollDistance = -1;
        bool RollawayApply;
        protected void Rollaway()
        {
            try {
                if (RollDistance == -1 || !IsDirectionNeutral()) RollDistance = DistanceM();
                if (IsDirectionNeutral())
                {
                    if (DistanceM() - RollDistance > NationalValues.D_NVROLL) RollawayApply = true;
                }
                if (RollawayApply)
                {
                    EmergencyBraking = true;
                    if (SpeedMpS() < 0.1f)
                    {
                        SetVigilanceEmergencyDisplay(true);
                        var m = new ETCSMessage("Roll away protection", ClockTime(), () => false, 0, true);
                        m.Revoke = () => m.Acknowledged;
                        if (!Messages.Contains(m)) Messages.Add(m);
                        var a = Messages.Find(x => x.Equals(m));
                        if (a != null && a.Acknowledged)
                        {
                            RollawayApply = false;
                            RollDistance = -1;
                            SetVigilanceEmergencyDisplay(false);
                        }
                    }
                }
            }
            catch (Exception e) { }
        }
        float ReverseDistance = -1;
        bool ReverseApply = false;
        protected void ReverseMovement()
        {
            if ((!IsDirectionReverse() && MA.Q_DIR == 0) || (IsDirectionReverse() && MA.Q_DIR == 1))
            {
                if (ReverseDistance == -1) ReverseDistance = DistanceM();
                if (DistanceM() - ReverseDistance > NationalValues.D_NVROLL) ReverseApply = true;
                if (ReverseApply)
                {
                    EmergencyBraking = true;
                    if (SpeedMpS() < 0.1f)
                    {
                        SetVigilanceEmergencyDisplay(true);
                        var m = new ETCSMessage("Reverse movement protection", ClockTime(), () => false, 0, true);
                        m.Revoke = () => m.Acknowledged;
                        if (!Messages.Contains(m)) Messages.Add(m);
                        var a = Messages.Find(x => x.Equals(m));
                        if (a != null && a.Acknowledged)
                        {
                            ReverseApply = false;
                            ReverseDistance = -1;
                            SetVigilanceEmergencyDisplay(false);
                        }
                    }
                }
            }
            else
            {
                ReverseDistance = -1;
                ReverseApply = false;
            }
        }
        float timeapp;
        float timerel;
        bool Apply;
        bool Release;
        float MaxPres = 0;
        float MinPres = float.MaxValue;
        protected int BrakeTest()
        {
            MaxPres = 4.9f;
            MinPres = 0.1f;
            if (!Apply && !Release && BrakePipePressureBar() >= MaxPres)
            {
                Apply = true;
                timeapp = ClockTime();
                Messages.Add(new ETCSMessage("Test de freno de emergencia: en curso", ClockTime(), () => TrainInfo.T_bs >= 0, 2, false));
            }
            EmergencyBraking |= Apply && !Release;
            if (Apply && BrakePipePressureBar() <= MinPres && !Release)
            {
                Release = true;
                timeapp = ClockTime() - timeapp;
                timerel = ClockTime();
            }
            if (BrakePipePressureBar() >= MaxPres && Apply && Release)
            {
                timerel = ClockTime() - timerel;
                return timeapp - (int)timeapp >= 0.1f ? (int)timeapp + 1 : (int)timeapp;
            }
            else return -1;
        }
        protected void Trip()
        {
            if (!OverrideEoA)
            {
                if (CurrentMode != Mode.TR)
                {
                    CurrentMode = Mode.TR;
                    MA = null;
                    MAsp = null;
                    MP = null;
                    if (MPsp != null) MPsp.Clear();
                    ISSP = null;
                    if (SSPsp != null) SSPsp.Clear();
                    if (MRSPTargets != null) MRSPTargets.Clear();
                    EoA = SvL = LoA = null;
                    SRsp = null;
                    EmergencyBraking = true;
                }
                SetVigilanceAlarmDisplay(true);
                SetVigilanceEmergencyDisplay(true);
                if (SpeedMpS() < 0.1)
                {
                    SetNextSignalAspect(Aspect.Approach_1);
                    var m = new ETCSMessage("Modo TRIP", ClockTime(), () => false, 0, true);
                    if (!Messages.Contains(m))
                    {
                        Messages.Add(m);
                        m.Revoke = () => m.Acknowledged;
                    }
                    var a = Messages.Find(x => x.Equals(m));
                    if (a != null && a.Acknowledged)
                    {
                        CurrentMode = Mode.PT;
                        SetVigilanceAlarmDisplay(false);
                        SetVigilanceEmergencyDisplay(false);
                    }
                }
                else SetNextSignalAspect(Aspect.Stop);
            }
        }
        bool LinkingBrake;
        protected void ManageBaliseData()
        {
            EBD = Eurobalise();
            if (EBD != null)
            {
                InfillLocationReference infill = null;
                if (CurrentLevel == Level.L1 || CurrentLevel == Level.L2 || CurrentLevel == Level.L3)
                {
                    if (EBD.Q_LINK == 1 && link != null && ((link.NID_BG != EBD.NID_BG && link.D_LINK < dMaxFront && link.D_LINK < dMinFront) || (link.NID_BG == EBD.NID_BG && link.D_LINK > dMaxFront)))
                    {
                        switch (link.Q_LINKREACTION)
                        {
                            case 0:
                                Messages.Add(new ETCSMessage("Datos de eurobaliza no consistentes", ClockTime(), () => CurrentMode != Mode.TR && CurrentMode != Mode.PT, 1, false));
                                Trip();
                                link = null;
                                return;
                            case 1:
                                Messages.Add(new ETCSMessage("Datos de eurobaliza no consistentes", ClockTime(), () => LinkingBrake == false, 1, false));
                                LinkingBrake = true;
                                link = null;
                                break;
                        }
                    }
                    else if (EBD.Q_LINK == 1 && link != null && link.NID_BG == EBD.NID_BG && TrainPosition.LRBG.Pos + link.D_LINK < dMaxFront && TrainPosition.LRBG.Pos + link.D_LINK > dMinFront)
                    {
                        TrainPosition.SetLRBG(new BaliseGroup(TrainPosition.LRBG.Pos + link.D_LINK, link.Q_LOCACC, EBD.NID_BG, this));
                        UpdateDistances(TrainPosition.PrevLRBG.Dist());
                        Odometry();
                    }
                    else if (EBD.Q_LINK == 1 && link == null)
                    {
                        TrainPosition.SetLRBG(new BaliseGroup(TrainPosition.LRBG.Pos + dEstFront, NationalValues.Q_NVLOCACC, EBD.NID_BG, this));
                        UpdateDistances(TrainPosition.PrevLRBG.Dist());
                        Odometry();
                    }
                }
                foreach (var pck in EBD.packet)
                {
                    switch (pck.NID_PACKET)
                    {
                        case 12:
                            if (CurrentLevel == Level.L1)
                            {
                                if (((L1MA)pck).V_MAIN == 0 && infill != null) Trip();
                                else if (MA != null && link != null && infill != null && link.NID_BG == infill.NID_BG) SetMA((L1MA)pck, link.D_LINK);
                                else if (infill == null) SetMA((L1MA)pck, 0);
                            }
                            break;
                        case 5:
                            link = (Linking)pck;
                            break;
                        case 27:
                            SetSSP((InternationalStaticSpeedProfile)pck, 0);
                            break;
                        case 65:
                            SetTSR((TemporarySpeedRestriction)pck);
                            break;
                        case 66:
                            SetTSR((TemporarySpeedRestrictionRevocation)pck);
                            break;
                        case 80:
                            SetMP((ModeProfile)pck);
                            break;
                        case 136:
                            infill = (InfillLocationReference)pck;
                            break;
                    }
                }
                var a = new List<Packet>();
                a.AddRange(EBD.packet);
                if (!a.Exists(x => x.NID_PACKET == 80))
                {
                    MP = null;
                    MPsp = null;
                }
                if (!a.Exists(x => x.NID_PACKET == 5)) link = null;
                if (a.Exists(x => x.NID_PACKET == 12)) LinkingBrake = false;
            }
        }
        protected void UpdateDistances(float Reference)
        {
            if (link != null) link.D_LINK -= (int)Reference;
            if (MAsp != null) MAsp.Distance -= Reference;
            if (SSPsp != null)
                foreach (var SP in SSPsp)
                {
                    SP.Distance -= Reference;
                }
            if (TSRsp != null)
                foreach (var SP in TSRsp)
                {
                    SP.Distance -= Reference;
                }
            if (MP != null)
                for (int i = 0; i < MP.D_MAMODE.Length; i++)
                {
                    MP.D_MAMODE[i] -= (int)Reference;
                }
            if (MA != null)
            {
                for (int i = 0; i < MA.L_SECTION.Length; i++)
                {
                    if (i == 0) MA.L_SECTION[i] -= (int)Reference;
                    else if (MA.L_SECTION[i - 1] < 0) MA.L_SECTION[i] += MA.L_SECTION[i - 1];
                }
            }
            if (OVsp != null) OVsp.Distance -= Reference;
            if (MRSPTargets != null)
                foreach (var T in MRSPTargets)
                {
                    T.Distance -= Reference;
                }
            if (EoA != null) EoA.Distance -= Reference;
            if (SvL != null) SvL.Distance -= Reference;
            if (LoA != null) LoA.Distance -= Reference;
            dEoA -= Reference;
        }
        public bool EmergencyBraking;
        public bool ServiceBrake;
        public float Vtarget;
        public float Vperm;
        public float Vsbi;
        public float Vrelease;
        float A_brake_emergency(float start, float target)
        {
            /*if (start > MpS.FromKpH(60) && target < MpS.FromKpH(60)) return ((start - target) * 0.809f * 1.29f) / (0.809f * (MpS.FromKpH(60) - target) + 1.29f * (MpS.FromKpH(target - MpS.FromKpH(60))));
            else if (target > MpS.FromKpH(60)) return 1.29f;
            else */
            return 0.809f;
        }
        float A_brake_service(float start, float target)
        {
            /*if (start > MpS.FromKpH(60) && target < MpS.FromKpH(60)) return ((start - target) * 0.5f * 0.9f) / (0.5f * (MpS.FromKpH(60) - target) + 0.9f * (MpS.FromKpH(target - MpS.FromKpH(60))));
            else if (target > MpS.FromKpH(60)) return 0.9f;
            else */
            return 0.5f;
        }
        MonitoringStatus Monitoring;
        List<SpeedProfile> SpdProf;
        float dEoA;
        float dSvL;
        float dTrip;
        protected void SetMA(MovementAuthority nMA, float LocationReference)
        {
            if (LocationReference == 0) MA = nMA;
            else
            {
                int offset = -1;
                float dist = 0;
                for (int i = 0; i < MA.L_SECTION.Length; i++) { if (MA.L_SECTION[i] > 0) { offset = i; break; } }
                var L_SECTION = new int[MA.L_SECTION.Length - offset];
                for (int i = 0; i < L_SECTION.Length; i++)
                {
                    L_SECTION[i] = MA.L_SECTION[i + offset];
                }
                for (int i = 0; i < L_SECTION.Length; i++)
                {
                    if (L_SECTION[i] + dist >= LocationReference || i + 1 == L_SECTION.Length)
                    {
                        dist = 0;
                        MA.L_SECTION = new int[i + 1 + nMA.L_SECTION.Length];
                        for (int a = 0; a < MA.L_SECTION.Length; a++)
                        {
                            if (a <= i)
                            {
                                MA.L_SECTION[a] = (int)Math.Min(L_SECTION[a], LocationReference - dist);
                                dist += L_SECTION[a];
                            }
                            else MA.L_SECTION[a] = nMA.L_SECTION[a - i - 1];
                        }
                        break;
                    }
                    dist += L_SECTION[i];
                }
            }
            dEoA = dEstFront;
            dSvL = dEstFront;
            for (int i = 0; i < MA.L_SECTION.Length; i++)
            {
                dEoA += Math.Max(MA.L_SECTION[i], 0);
            }
            if (MA.Q_OVERLAP == 1/* && !MA.T_OL.Triggered*/) dSvL = dEoA + MA.D_OL;
            else if (MA.Q_DANGERPOINT == 1) dSvL = dEoA + MA.D_DP;
            else dSvL = dEoA;
            dTrip = dEoA + Math.Max(2 * TrainPosition.LRBG.Acc + 10 + 0.1f * dEoA, dMaxFront - dMinFront);
            MAsp = new SpeedProfile(MpS.FromKpH(MA.V_MAIN * 5), dEstFront, dEoA);
            if (MA.V_LOA == 0)
            {
                EoA = new Target(dEoA, 0, true, this);
                SvL = new Target(dSvL, 0, false, this);
                var rs = MA.Q_OVERLAP == 1 ? MA.V_RELEASEOL : MA.V_RELEASEDP;
                switch (rs)
                {
                    case 126:
                        float rsob = float.MaxValue;
                        if (Targets != null) foreach (var t in MRSPTargets.FindAll(x => x.dEBD != null))
                            {
                                if (t.Distance > dTrip)
                                {
                                    rsob = Math.Min(rsob, t.vEBD(dTrip));
                                }
                            }
                        if (rsob == float.MaxValue) rsob = 0;
                        rsob = Math.Min(rsob, MRSPInterval(EoA.dSBI1(rsob), dTrip));
                        EoA.ReleaseSpeed = SvL.ReleaseSpeed = rsob;
                        break;
                    case 127:
                        EoA.ReleaseSpeed = SvL.ReleaseSpeed = NationalValues.V_NVREL;
                        break;
                    default:
                        EoA.ReleaseSpeed = SvL.ReleaseSpeed = MpS.FromKpH(rs * 5);
                        break;
                }
            }
            else LoA = new Target(dEoA, MpS.FromKpH(MA.V_LOA * 5), false, this);
        }
        protected void SetSSP(InternationalStaticSpeedProfile nISSP, float LocationReference)
        {
            ISSP = nISSP;
            SSPsp.Clear();
            for (int i = 0; i < ISSP.D_STATIC.Length - 1; i++)
            {
                var ssp = new SpeedProfile(MpS.FromKpH(ISSP.V_STATIC[i] * 5), ISSP.D_STATIC[i] + dEstFront, ISSP.D_STATIC[i + 1] + (ISSP.V_STATIC[i + 1] > ISSP.V_STATIC[i] && ISSP.Q_FRONT[i] == 0 ? TrainLengthM() : 0) - ISSP.D_STATIC[i]);
                SSPsp.Add(ssp);
            }
        }
        protected void SetSR()
        {
            SRsp = new SpeedProfile(NationalValues.V_NVSTFF, dEstFront, NationalValues.D_NVSTFF);
        }
        protected void SetTSR(TemporarySpeedRestriction TSR)
        {
            if (TSR != null)
            {
                for (int i = 0; i < TSRs.Count; i++)
                {
                    if (TSRs[i].NID_TSR == TSR.NID_TSR && TSRs[i].NID_TSR != 255)
                    {
                        TSRs.RemoveAt(i);
                        TSRsp.RemoveAt(i);
                    }
                }
                TSRs.Add(TSR);
                TSRsp.Add(new SpeedProfile(MpS.FromKpH(TSR.V_TSR * 5), TSR.D_TSR + dEstFront, TSR.L_TSR + (TSR.Q_FRONT == 1 ? 0 : TrainLengthM())));
            }
        }
        protected void SetTSR(TemporarySpeedRestrictionRevocation TSR)
        {
            for (int i = 0; i < TSRs.Count; i++)
            {
                if (TSRs[i].NID_TSR == TSR.NID_TSR && TSRs[i].NID_TSR != 255)
                {
                    TSRs.RemoveAt(i);
                    TSRsp.RemoveAt(i);
                }
            }
        }
        protected void SetMP(ModeProfile nMP)
        {
            MP = nMP;
            if (MPsp != null) MPsp.Clear();
            else MPsp = new List<SpeedProfile>();
            for (int i = 0; i < MP.N_ITER; i++)
            {
                MPsp.Add(new SpeedProfile(MP.V_MAMODE[i] == 127 ? (MP.M_MAMODE[i] == 0 ? NationalValues.V_NVONSIGHT : NationalValues.V_NVSHUNT) : MpS.FromKpH(MP.V_MAMODE[i] * 5), MP.D_MAMODE[i] + dEstFront, MP.L_MAMODE[i]));
            }
        }
        protected void SetOverride()
        {
            OVsp = new SpeedProfile(NationalValues.V_NVSUPOVTRP, dEstFront, NationalValues.D_NVOVTRP);
        }
        bool AcknowledgeMP;
        bool Test = false;
        protected void UpdateControls()
        {
            if (link != null && link.D_LINK < dMinFront)
            {
                switch (link.Q_LINKREACTION)
                {
                    case 0:
                        Messages.Add(new ETCSMessage("Datos de eurobaliza no consistentes", ClockTime(), () => CurrentMode != Mode.TR && CurrentMode != Mode.PT, 1, false));
                        Trip();
                        link = null;
                        return;
                    case 1:
                        Messages.Add(new ETCSMessage("Datos de eurobaliza no consistentes", ClockTime(), () => LinkingBrake = false, 1, false));
                        LinkingBrake = true;
                        break;
                }
                link = null;
            }
            if (CurrentMode == Mode.SR)
            {
                if (SRsp == null || SRsp.Distance + SRsp.Length < dMaxFront)
                {
                    SRsp = null;
                    Trip();
                }
            }
            if (CurrentMode != Mode.SR && SRsp != null)
            {
                SRsp = null;
            }
            if (OVsp == null || (OverrideEoA && OVsp.Distance + OVsp.Length < dMinFront))
            {
                OVsp = null;
                OverrideEoA = false;
            }
            if (MA != null)
            {
                /*for(int i = 0; i<MA.L_SECTION.Length; i++)
                {
                    if (MA.Q_SECTIONTIMER[i] && MA.T_SECTIONTIMER[i].Triggered)
                    {
                        Trip();
                        break;
                    }
                }*/
                if (dEoA < dMinFront)
                {
                    Messages.Add(new ETCSMessage("EoA o LoA rebasado", ClockTime(), () => CurrentMode != Mode.TR && CurrentMode != Mode.PT, 1, false));
                    Trip();
                }
            }
            if (MP != null)
            {
                if (MP.L_MAMODE[0] + MP.D_MAMODE[0] < dMinFront)
                {
                    MP = null;
                    AcknowledgeMP = false;
                }
                else
                {
                    for (int i = 0; i < MP.N_ITER; i++)
                    {
                        Mode TransitionMode = CurrentMode;
                        switch (MP.M_MAMODE[i])
                        {
                            case 0:
                                TransitionMode = Mode.OS;
                                break;
                            case 1:
                                TransitionMode = Mode.SH;
                                break;
                            case 2:
                                TransitionMode = Mode.LS;
                                break;
                        }
                        if (CurrentMode != TransitionMode && !AcknowledgeMP)
                        {
                            AcknowledgeMP = true;
                            var m = new ETCSMessage("Reconocer modo " + TransitionMode.ToString(), ClockTime(), () => true, 0, true);
                            if (!Messages.Contains(m))
                            {
                                m.Revoke = () => m.Acknowledged;
                                Messages.Add(m);
                            }
                        }
                        var a = Messages.Find(x => x.id == 5);
                        if (a != null && a.Acknowledged)
                        {
                            AcknowledgeMP = false;
                            CurrentMode = TransitionMode;
                            Messages.Remove(a);
                        }
                        SetVigilanceAlarmDisplay(AcknowledgeMP);
                        SetVigilanceEmergencyDisplay(AcknowledgeMP);
                        if (MP.D_MAMODE[i] < dMaxFront && AcknowledgeMP) ServiceBrake = true;
                        if (MP.D_MAMODE[i] < dMaxFront) CurrentMode = TransitionMode;
                    }
                }
                for (int i = 0; i < TSRs.Count; i++)
                {
                    if (TSRs[i].L_TSR + TSRs[i].D_TSR < dMinFront)
                    {
                        TSRs.RemoveAt(i);
                        TSRsp.RemoveAt(i);
                    }
                }
            }
            if (MRSPS != null) MRSPS.RemoveAll(x => x==null || x.Distance + x.Length < dMinFront);
        }
        List<SpeedProfile> Last;
        protected void SpeedProfiles()
        {
            SpdProf.Clear();
            SpdProf.Add(new SpeedProfile(TrainInfo.MaxSpeed, 0, float.PositiveInfinity));
            if (CurrentMode == Mode.FS || CurrentMode == Mode.OS) SpdProf.Add(MAsp);
            if (CurrentMode == Mode.FS || CurrentMode == Mode.OS) SpdProf.AddRange(SSPsp);
            if (CurrentMode == Mode.LS || CurrentMode == Mode.OS || CurrentMode == Mode.SH) SpdProf.AddRange(MPsp);
            if (CurrentMode == Mode.SR) SpdProf.Add(SRsp);
            if (OverrideEoA) SpdProf.Add(OVsp);
            if (TSRsp != null) SpdProf.AddRange(TSRsp);
            SpdProf.RemoveAll(x => x == null);
            SpdProf.RemoveAll(x => x.Distance + x.Length < dMinFront);
            if (Last == null ||/*Last.Count!=SpdProf.Count*/true)
            {
                MRSP(SpdProf);
            }
            Last = SpdProf;
        }
        public class SpeedProfile
        {
            public float Speed;
            public float Distance;
            public float Length;
            public float Offset = 0;
            public SpeedProfile(float S, float D, float L)
            {
                Distance = D;
                Length = L;
                Speed = S;
            }
            public override bool Equals(object obj)
            {
                var prof = obj as SpeedProfile;
                if (prof != null)
                {
                    return Distance.AlmostEqual(prof.Distance, 2) && Speed.AlmostEqual(prof.Speed, 2) && Length.AlmostEqual(prof.Length, 2);
                }
                else return base.Equals(obj);
            }
            public override int GetHashCode()
            {
                return base.GetHashCode();
            }
        }
        List<SpeedProfile> MRSPS;
        protected void MRSP(List<SpeedProfile> sps)
        {
            MRSPS = new List<SpeedProfile>();
            var Distances = new List<float>();
            sps.RemoveAll(x => x == null);
            sps.RemoveAll(x => x.Distance + x.Length <= dMinFront);
            foreach (var p in sps)
            {
                Distances.Add(p.Distance + 1);
                Distances.Add(p.Distance - 1);
                Distances.Add(p.Distance + p.Length + 1);
                Distances.Add(p.Distance + p.Length - 1);
            }
            Distances.Sort();
            for (int i = 0; i < Distances.Count;)
            {
                var a = MRSPI(sps, Distances[i]);
                while (MRSPI(sps, Distances[i]).Speed == a.Speed && i + 1 < Distances.Count)
                {
                    a = MRSPI(sps, Distances[i]);
                    i++;
                }
                MRSPS.Add(new SpeedProfile(a.Speed, a.Distance, Distances[i]));
                if (i + 1 == Distances.Count) break;
            }
            MRSPTargets = new List<Target>();
            for (int i = 1; i < MRSPS.Count; i++)
            {
                if (MRSPS[i - 1].Speed > MRSPS[i].Speed) MRSPTargets.Add(new Target(MRSPS[i].Distance, MRSPS[i].Speed, false, this));
            }
        }
        protected SpeedProfile MRSPI(List<SpeedProfile> sps, float Dist)
        {
            SpeedProfile sp = sps[0];
            for (int i = 0; i < sps.Count; i++)
            {
                if (sps[i].Distance <= Dist && sps[i].Length + sps[i].Distance > Dist)
                {
                    if (sp.Speed > sps[i].Speed) sp = sps[i];
                }
            }
            return sp;
        }
        protected float MRSPInterval(float Start, float End)
        {
            float spd = float.MaxValue;
            if (MRSPS != null)
            {
                for (int i = 0; i < MRSPS.Count; i++)
                {
                    if (MRSPS[i].Distance <= End && MRSPS[i].Distance + MRSPS[i].Length >= Start)
                    {
                        spd = Math.Min(spd, MRSPS[i].Speed);
                    }
                }
            }
            return spd;
        }
        struct NationalValues
        {
            public static int A_MAXREDADH1 = /*(int)(1f / 0.05f)*/62;
            public static float V_NVSHUNT = MpS.FromKpH(30);
            public static float V_NVSTFF = MpS.FromKpH(40);
            public static float V_NVONSIGHT = MpS.FromKpH(30);
            public static float V_NVUNFIT = MpS.FromKpH(100);
            public static float V_NVREL = MpS.FromKpH(40);
            public static bool Q_NVSBTSMPERM = true;
            public static float D_NVOVTRP = 200;
            public static float D_NVPOTRP = 200;
            public static float D_NVROLL = 2;
            public static float T_NVOVTRP = 60;
            public static float D_NVSTFF = float.MaxValue;
            public static float Q_NVLOCACC = 12;
            public static float V_NVALLOWOVTRP = 0.1f;
            public static float V_NVSUPOVTRP = MpS.FromKpH(30);
        }
        struct FixedData
        {
            public const float T_bs1_locked = 0;
            public const float T_bs2_locked = 2;
            public const float T_warning = 2;
            public const float T_driver = 4;
            public const float dV_ebi_min = 7.5f / 3.6f;
            public const float dV_ebi_max = 15f / 3.6f;
            public const float dV_sbi_min = 5.5f / 3.6f;
            public const float dV_sbi_max = 10f / 3.6f;
            public const float dV_warning_min = 4f / 3.6f;
            public const float dV_warning_max = 5f / 3.6f;
            public const float V_ebi_min = 110f / 3.6f;
            public const float V_ebi_max = 210f / 3.6f;
            public const float V_sbi_min = 110f / 3.6f;
            public const float V_sbi_max = 210f / 3.6f;
            public const float V_warning_min = 110f / 3.6f;
            public const float V_warning_max = 140f / 2.6f;
            public const int T_dispTTI = 14;
        }
        struct TrainInfo
        {
            public static float Length = -1;
            public static float PF = -1;
            public static float R = -1;
            public static float T_be = -1;
            public static float T_bs = -1;
            public static float T_bs1;
            public static float T_bs2;
            public static float T_traction = -1;
            public static float T_indication;
            public static int Driver_id = -1;
            public static int Train_number = -1;
            public static float MaxSpeed = -1;
            public static bool IsOK
            {
                get
                {
                    return Length > -1 && PF > -1 && R > -1 && T_be > -1 && T_bs > -1 && T_traction > -1 && Driver_id > -1 && Train_number > -1 && MaxSpeed > -1;
                }
            }
            public static void Calc()
            {
                T_bs1 = T_bs;
                T_bs2 = T_bs;
                T_indication = Math.Max(5, 0.8f * T_bs1) + FixedData.T_driver;
            }
        }
        protected class Target
        {
            public float Distance;
            public float TargetSpeed;
            public Func<float, float> dEBD;
            public Func<float, float> dSBD;
            public Func<float, float> dEBI;
            public Func<float, float> dSBI2;
            public Func<float, float> dSBI1;
            public Func<float, float> dW;
            public Func<float, float> dP;
            public Func<float, float> dI;
            public Func<float, float> vEBD;
            public Func<float, float> vSBD;
            public Func<float, float> vSBI2;
            public Func<float, float> vSBI1;
            public Func<float, float> vP;
            public float ReleaseSpeed;
            public MonitoringStatus Monitoring;
            public Target(float Distance, float TargetSpeed, bool SBD, ETCS tcs)
            {
                this.TargetSpeed = TargetSpeed;
                this.Distance = Distance;
                if (SBD)
                {
                    dSBD = (Vest) => this.Distance - tcs.DistanceCurve(Vest, TargetSpeed, 0, 0, tcs.A_brake_service(Vest, TargetSpeed));
                    vSBD = (D) => tcs.SpeedCurve(this.Distance - Math.Min(D, this.Distance), TargetSpeed, 0, 0, tcs.A_brake_service(tcs.SpeedMpS(), TargetSpeed));
                    dSBI1 = (Vest) => dSBD(Vest) - Vest * TrainInfo.T_bs1;
                    dEBI = (Vest) => dSBI1(Vest) + Vest * TrainInfo.T_bs2;
                    dW = (Vest) => dSBI1(Vest) - Vest * FixedData.T_warning;
                    dP = (Vest) => dSBI1(Vest) - Vest * FixedData.T_driver;
                    dI = (Vest) => dP(Vest) - Vest * TrainInfo.T_indication;
                    vSBI1 = (D) => vSBD(D + tcs.SpeedMpS() * TrainInfo.T_bs1);
                    vP = (D) => vSBI1(D + tcs.SpeedMpS() * FixedData.T_driver);
                }
                else
                {
                    dEBD = (Vest) => this.Distance - tcs.DistanceCurve(Vest, TargetSpeed == 0 ? 0 : TargetSpeed + tcs.dVebi(TargetSpeed), 0, 0, tcs.A_brake_emergency(Vest, TargetSpeed));
                    vEBD = (D) => tcs.SpeedCurve(dEBD(TargetSpeed) - Math.Min(dEBD(TargetSpeed), D), TargetSpeed, 0, 0, tcs.A_brake_emergency(0, TargetSpeed));
                    dEBI = (Vest) => dEBD(Vest - 0) - 0;
                    dSBI2 = (Vest) => dEBI(Vest) - Vest * TrainInfo.T_bs2;
                    dW = (Vest) => dSBI2(Vest) - Vest * FixedData.T_warning;
                    dP = (Vest) => dSBI2(Vest) - Vest * FixedData.T_driver;
                    dI = (Vest) => dP(Vest) - Vest * TrainInfo.T_indication;
                    vSBI2 = (D) => vEBD(D - 0 + tcs.SpeedMpS() * TrainInfo.T_bs2) - 0;
                    vP = (D) => vSBI2(D + tcs.SpeedMpS() * FixedData.T_driver);
                }
            }
            public Target() { }
            public override bool Equals(object obj)
            {
                var targ = obj as Target;
                if (targ != null)
                {
                    return dP(TargetSpeed).AlmostEqual(targ.dP(TargetSpeed), 5);
                }
                else return base.Equals(obj);
            }
            public override int GetHashCode()
            {
                return base.GetHashCode();
            }
        }
        List<Target> Targets;
        enum Monitors
        {
            CSM,
            TSM,
            RSM
        }
        Monitors Monitor;
        Target MRDT = null;
        protected float dVebi(float Speed)
        {
            return Math.Max(FixedData.dV_ebi_min, Math.Min(FixedData.dV_ebi_min + ((FixedData.dV_ebi_max - FixedData.dV_ebi_min) / (FixedData.V_ebi_max - FixedData.V_ebi_min)) * (Speed - FixedData.V_ebi_min), FixedData.dV_ebi_max));
        }
        protected float dVsbi(float Speed)
        {
            return Math.Max(FixedData.dV_sbi_min, Math.Min(FixedData.dV_sbi_min + ((FixedData.dV_sbi_max - FixedData.dV_sbi_min) / (FixedData.V_sbi_max - FixedData.V_sbi_min)) * (Speed - FixedData.V_sbi_min), FixedData.dV_sbi_max));
        }
        protected float dVw(float Speed)
        {
            return Math.Max(FixedData.dV_warning_min, Math.Min(FixedData.dV_warning_min + ((FixedData.dV_warning_max - FixedData.dV_warning_min) / (FixedData.V_warning_max - FixedData.V_warning_min)) * (Speed - FixedData.V_warning_min), FixedData.dV_warning_max));
        }
        bool Normal = false;
        bool Indication = false;
        bool Overspeed = false;
        bool Warning = false;
        bool Intervention = false;
        public bool TCO = false;
        bool SB = false;
        bool EB = false;
        Target EoA = null, SvL = null, LoA = null;
        List<Target> MRSPTargets;
        protected void SupervisedTargets()
        {
            Targets = new List<Target>();
            if (CurrentMode == Mode.FS || CurrentMode == Mode.OS || CurrentMode == Mode.LS || CurrentMode == Mode.SR || CurrentMode == Mode.UN) Targets.AddRange(MRSPTargets);
            if (MA != null)
            {
                if (MA.V_LOA == 0)
                {
                    Targets.Add(EoA);
                    Targets.Add(SvL);
                }
                else Targets.Add(LoA);
            }
            if (CurrentMode == Mode.SR) Targets.Add(new Target(SRsp.Distance + SRsp.Length, 0, true, this));
            Targets.RemoveAll(x => x == null);
            Targets.RemoveAll(x => x.Distance < dMaxFront && MRSPTargets.Contains(x));
        }
        float TTI;
        float dIndication;
        float dStartRSM;
        float dTarget;
        List<Target> PreviousTargets;
        protected void SpeedMonitors()
        {
            float Vmrsp = MRSPInterval(dMinFront, dMaxFront);
            Vperm = Vmrsp;
            Vsbi = Vmrsp + dVsbi(Vmrsp);
            Vrelease = 0;
            dStartRSM = EoA != null ? EoA.dSBI1(EoA.ReleaseSpeed) : float.PositiveInfinity;
            dIndication = dTarget = float.MaxValue;
            if (Monitor == Monitors.CSM)
            {
                MRDT = null;
                foreach (Target t in Targets)
                {
                    if (t.TargetSpeed > SpeedMpS()) continue;
                    if (MRDT == null || t.dI(SpeedMpS()) < MRDT.dI(SpeedMpS())) MRDT = t;
                    if (EoA == null || SpeedMpS() > EoA.ReleaseSpeed)
                    {
                        dIndication = Math.Min(dIndication, t.dI(SpeedMpS()));
                    }
                    else dIndication = dStartRSM;
                }
                if (MRDT != null && NationalValues.A_MAXREDADH1 == 63)
                {
                    Vtarget = MRDT.TargetSpeed;
                    dTarget = MRDT.dP(MRDT.TargetSpeed);
                }
                if (dIndication < float.MaxValue && NationalValues.A_MAXREDADH1 == 62)
                {
                    TTI = (dIndication - dEstFront) / SpeedMpS();
                }
            }
            bool c1 = dIndication < dEstFront && SpeedMpS() >= Vrelease;
            bool c2 = dStartRSM < dEstFront;
            bool c3 = !Targets.Contains(MRDT) && !c1 && !c2;
            bool c4 = (Targets == null || PreviousTargets == null || Targets.Count != PreviousTargets.Count) && c1 && !c2;
            bool c5 = (Targets == null || PreviousTargets == null || Targets.Count != PreviousTargets.Count) && c2;
            PreviousTargets = Targets;
            if ((Monitor == Monitors.CSM && c1) || c4)
            {
                if (Monitor == Monitors.CSM) TriggerSoundInfo1();
                Monitor = Monitors.TSM;
            }
            if (c2 || c5)
            {
                if (Monitor == Monitors.CSM) TriggerSoundInfo1();
                Monitor = Monitors.RSM;
            }
            if (c3) Monitor = Monitors.CSM;
            if (Monitor != Monitors.CSM)
            {
                if (Monitor == Monitors.TSM)
                {
                    var MRDTs = new List<Target>();
                    Targets.Sort(delegate (Target x, Target y)
                    {
                        if (x == null && y == null) return 0;
                        else if (x == null) return -1;
                        else if (y == null) return 1;
                        else if (x == y) return 0;
                        else if ((x == EoA ? x.vP(dEstFront) : x.vP(dMaxFront)) > (y == EoA ? y.vP(dEstFront) : y.vP(dMaxFront))) return -1;
                        else return 1;
                    });
                    int n = 0;
                    var PreviousMRDT = MRDT;
                    MRDT = null;
                    while (n < Targets.Count)
                    {
                        if (Targets[n].dI(SpeedMpS()) < (Targets[n] == EoA ? dEstFront : dMaxFront) && SpeedMpS() > Math.Max(Targets[n].TargetSpeed, Targets[n].ReleaseSpeed))
                        {
                            MRDTs.Add(Targets[n]);
                            var index = MRDTs.IndexOf(Targets[n]);
                            for (int a = 0; a < Targets.Count; a++)
                            {
                                if (!MRDTs.Contains(Targets[n]) && Targets[n].dI(MRDTs[index].TargetSpeed) < MRDTs[index].dP(MRDTs[index].TargetSpeed))
                                {
                                    n = a;
                                    break;
                                }
                                else if (a + 1 == Targets.Count)
                                {
                                    MRDT = MRDTs[index];
                                    if (PreviousMRDT != null && !MRDT.Equals(PreviousMRDT) && !((PreviousMRDT == EoA || PreviousMRDT == SvL) && (MRDT == LoA || MRDT == SvL))) TriggerSoundInfo1();
                                    break;
                                }
                            }
                            break;
                        }
                        else n++;
                    }
                    if (MRDT == null) MRDT = PreviousMRDT;
                }
                foreach (var t in Targets)
                {
                    Vperm = Math.Min(Vperm, Math.Max(t == EoA ? t.vP(dEstFront) : t.vP(dMaxFront), t.TargetSpeed));
                    if (t.vSBI1 != null) Vsbi = Math.Min(Vsbi, Math.Max(t.vSBI1(dEstFront), t.TargetSpeed + dVsbi(t.TargetSpeed)));
                    if (t.vSBI2 != null) Vsbi = Math.Min(Vsbi, Math.Max(t.vSBI2(dMaxFront), t.TargetSpeed + dVsbi(t.TargetSpeed)));
                }
                Vtarget = MRDT.TargetSpeed;
                if (MRDT == EoA || MRDT == SvL) Vrelease = MRDT.ReleaseSpeed;
                dTarget = MRDT.dP(MRDT.TargetSpeed);
            }
            bool rOverspeed = true;
            bool rWarning = true;
            bool rIntervention = true;
            bool rTCO = true;
            bool rSB = true;
            bool rEB = true;
            bool pEB = EB;
            bool pSB = SB;
            if (Monitor == Monitors.CSM)
            {
                bool t1 = SpeedMpS() < Vmrsp;
                bool t2 = SpeedMpS() > Vmrsp;
                bool t3 = SpeedMpS() > Vmrsp + dVw(Vmrsp);
                bool t4 = SpeedMpS() > Vmrsp + dVsbi(Vmrsp);
                bool t5 = SpeedMpS() > Vmrsp + dVebi(Vmrsp);
                bool r0 = SpeedMpS() < 0.1;
                bool r1 = SpeedMpS() < Vmrsp;
                Normal |= t1;
                Indication = false;
                Overspeed |= t2;
                Warning |= t3;
                Intervention |= t4 || t5;
                SB |= t4;
                EB |= t5;
                rTCO &= r1 || r0;
                rSB &= r0 || r1;
                rEB &= r0;
                rIntervention &= r0 || (!EB && r1);
                rWarning &= r0 || r1;
                rOverspeed &= r0 || r1;
            }
            else if (Monitor == Monitors.RSM)
            {
                bool t1 = SpeedMpS() <= Vrelease;
                bool t2 = SpeedMpS() > Vrelease;
                bool r0 = SpeedMpS() < 0.1;
                bool r1 = SpeedMpS() < Vrelease;
                Indication |= t1;
                Intervention |= t2;
                EB |= t2;
                rOverspeed &= r1;
                rWarning &= r1;
                rIntervention &= r0;
                rTCO &= r1;
                rEB &= r0;
                rSB &= r1;
            }
            else if (Monitor == Monitors.TSM)
            {
                foreach (Target t in Targets)
                {
                    if (t.TargetSpeed > 0)
                    {
                        bool t3 = t.TargetSpeed < SpeedMpS() && SpeedMpS() <= Vmrsp && t.dI(SpeedMpS()) < dMaxFront && t.dP(SpeedMpS()) >= dMaxFront;
                        bool t4 = t.TargetSpeed < SpeedMpS() && SpeedMpS() <= Vmrsp && t.dP(SpeedMpS()) < dMaxFront;
                        bool t6 = Vmrsp < SpeedMpS() && SpeedMpS() <= Vmrsp + dVw(Vmrsp) && t.dI(SpeedMpS()) < dMaxFront && t.dW(SpeedMpS()) >= dMaxFront;
                        bool t7 = t.TargetSpeed + dVw(t.TargetSpeed) < SpeedMpS() && SpeedMpS() <= Vmrsp + dVsbi(Vmrsp) && dMaxFront > t.dW(SpeedMpS());
                        bool t9 = Vmrsp + dVw(Vmrsp) < SpeedMpS() && SpeedMpS() <= Vmrsp + dVsbi(Vmrsp) && t.dI(SpeedMpS()) < dMaxFront && t.dSBI2(SpeedMpS()) >= dMaxFront;
                        bool t10 = t.TargetSpeed + dVsbi(t.TargetSpeed) < SpeedMpS() && SpeedMpS() <= Vmrsp + dVsbi(Vmrsp) && t.dSBI2(SpeedMpS()) < dMaxFront;
                        bool t12 = Vmrsp + dVsbi(Vmrsp) < SpeedMpS() && SpeedMpS() <= Vmrsp + dVebi(Vmrsp) && t.dI(SpeedMpS()) < dMaxFront && t.dEBI(SpeedMpS()) >= dMaxFront;
                        bool t13 = t.TargetSpeed + dVebi(t.TargetSpeed) < SpeedMpS() && SpeedMpS() <= Vmrsp + dVebi(Vmrsp) && t.dEBI(SpeedMpS()) < dMaxFront;
                        bool t15 = SpeedMpS() > Vmrsp + dVebi(Vmrsp) && t.dI(SpeedMpS()) < dMaxFront;
                        bool r0 = SpeedMpS() < 0.1;
                        bool r1 = SpeedMpS() < t.TargetSpeed;
                        bool r3 = t.TargetSpeed < SpeedMpS() && SpeedMpS() <= Vmrsp && t.dP(SpeedMpS()) >= dMaxFront;
                        Indication |= t3;
                        Overspeed |= t4 || t6;
                        TCO |= t7 || t9;
                        Warning |= t7 || t9;
                        SB |= t10 || t12;
                        EB |= t13 || t15;
                        Intervention |= t10 || t12 || t13 || t15;
                        rTCO &= r1 || r3;
                        rSB &= r1 || r3;
                        rOverspeed &= r1 || r3;
                        rWarning &= r1 || r3;
                        rEB &= r0;
                        rIntervention &= r0 || (!EB && (r1 || r3));
                    }
                    else
                    {
                        bool t3 = t.ReleaseSpeed < SpeedMpS() && SpeedMpS() <= Vmrsp && t.dI(SpeedMpS()) < (t == EoA ? dEstFront : dMaxFront) && t.dP(SpeedMpS()) >= (t == EoA ? dEstFront : dMaxFront);
                        bool t4 = t.ReleaseSpeed < SpeedMpS() && SpeedMpS() <= Vmrsp && t.dP(SpeedMpS()) < (t == EoA ? dEstFront : dMaxFront);
                        bool t6 = Vmrsp < SpeedMpS() && SpeedMpS() <= Vmrsp + dVw(Vmrsp) && t.dI(SpeedMpS()) < (t == EoA ? dEstFront : dMaxFront) && t.dW(SpeedMpS()) >= (t == EoA ? dEstFront : dMaxFront);
                        bool t7 = t.ReleaseSpeed + dVw(t.ReleaseSpeed) < SpeedMpS() && SpeedMpS() <= Vmrsp + dVsbi(Vmrsp) && (t == EoA ? dEstFront : dMaxFront) > t.dW(SpeedMpS());
                        bool t9 = Vmrsp + dVw(Vmrsp) < SpeedMpS() && SpeedMpS() <= Vmrsp + dVsbi(Vmrsp) && t.dI(SpeedMpS()) < (t == EoA ? dEstFront : dMaxFront) && (t.dSBI1 != null ? t.dSBI1(SpeedMpS()) : t.dSBI2(SpeedMpS())) >= (t == EoA ? dEstFront : dMaxFront);
                        bool t10 = t.ReleaseSpeed + dVsbi(t.ReleaseSpeed) < SpeedMpS() && SpeedMpS() <= Vmrsp + dVsbi(Vmrsp) && (t.dSBI1 != null ? t.dSBI1(SpeedMpS()) : t.dSBI2(SpeedMpS())) < (t == EoA ? dEstFront : dMaxFront);
                        bool t12 = Vmrsp + dVsbi(Vmrsp) < SpeedMpS() && SpeedMpS() <= Vmrsp + dVebi(Vmrsp) && t.dI(SpeedMpS()) < (t == EoA ? dEstFront : dMaxFront) && t.dEBI(SpeedMpS()) >= (t == EoA ? dEstFront : dMaxFront);
                        bool t13 = t.ReleaseSpeed + dVebi(t.ReleaseSpeed) < SpeedMpS() && SpeedMpS() <= Vmrsp + dVebi(Vmrsp) && t.dEBI(SpeedMpS()) < (t == EoA ? dEstFront : dMaxFront);
                        bool t15 = SpeedMpS() > Vmrsp + dVebi(Vmrsp) && t.dI(SpeedMpS()) < (t == EoA ? dEstFront : dMaxFront);
                        bool r0 = SpeedMpS() < 0.1;
                        bool r1 = SpeedMpS() < t.ReleaseSpeed;
                        bool r3 = t.ReleaseSpeed < SpeedMpS() && SpeedMpS() <= Vmrsp && t.dP(SpeedMpS()) >= (t == EoA ? dEstFront : dMaxFront);
                        if ((t.dSBI1 != null ? t.dSBI1(t.ReleaseSpeed) : t.dSBI2(t.ReleaseSpeed)) < (t == EoA ? dEstFront : dMaxFront)) Monitor = Monitors.RSM;
                        Indication |= t3;
                        Overspeed |= t4 || t6;
                        TCO |= t7 || t9;
                        Warning |= t7 || t9;
                        SB |= t10 || t12;
                        EB |= t13 || t15;
                        Intervention |= t10 || t12 || t13 || t15;
                        rTCO &= r1 || r3;
                        rSB &= r1 || r3;
                        rOverspeed &= r1 || r3;
                        rWarning &= r1 || r3;
                        rEB &= r0;
                        rIntervention &= r0 || (!EB && (r1 || r3));
                    }
                }
            }
            if (rIntervention) Intervention = false;
            if (rWarning) Warning = false;
            if (rOverspeed) Overspeed = false;
            if (rSB)
            {
                if (SB && !EB && !EmergencyBraking) TriggerSoundInfo1();
                SB = false;
            }
            if (rEB)
            {
                if (EB) TriggerSoundInfo1();
                EB = false;
            }
            if (rTCO) TCO = false;
            if (SB && !ServiceBrake && !pSB)
            {
                TriggerSoundInfo1();
                Messages.Add(new ETCSMessage("Freno de servicio aplicado", ClockTime(), () => !ServiceBrake, 1, false));
            }
            if (Monitor == Monitors.TSM && !NationalValues.Q_NVSBTSMPERM) EmergencyBraking |= SB;
            else ServiceBrake |= SB;
            if (EB && !EmergencyBraking && !pEB) TriggerSoundInfo1();
            EmergencyBraking |= EB;
            if (Intervention) Monitoring = MonitoringStatus.Intervention;
            else if (Warning) Monitoring = MonitoringStatus.Warning;
            else if (Overspeed)
            {
                if (Monitor == Monitors.TSM && Monitoring == MonitoringStatus.Indication) TriggerSoundAlert1();
                Monitoring = MonitoringStatus.Overspeed;
            }
            else if (Indication) Monitoring = MonitoringStatus.Indication;
            else if (Normal) Monitoring = MonitoringStatus.Normal;
        }
        protected void ETCSCurves()
        {
            if (Monitoring == MonitoringStatus.Warning)
            {
                SetOverspeedWarningDisplay(true);
                TriggerSoundWarning1();
            }
            else
            {
                SetOverspeedWarningDisplay(false);
                TriggerSoundWarning2();
            }
            SetInterventionSpeedLimitMpS(Vsbi);
            SetCurrentSpeedLimitMpS(Vperm);
            if (Monitor != Monitors.CSM || NationalValues.A_MAXREDADH1 == 63) SetNextSpeedLimitMpS(Vtarget);
            else SetNextSpeedLimitMpS(Vperm);
            if (Vperm < Vrelease)
            {
                SetCurrentSpeedLimitMpS(Vrelease);
                SetNextSpeedLimitMpS(Vperm);
            }
            if (Monitoring == MonitoringStatus.Indication) SetMonitoringStatus(MonitoringStatus.Overspeed);
            else if (Monitoring == MonitoringStatus.Overspeed) SetMonitoringStatus(MonitoringStatus.Warning);
            else SetMonitoringStatus(Monitoring);
            SetOverspeedWarningDisplay(Monitoring == MonitoringStatus.Overspeed || Monitoring == MonitoringStatus.Warning);
            //SetReleaseSpeedMpS(ETCSVrelease);
            if (TTI > 0 && FixedData.T_dispTTI < TTI && NationalValues.A_MAXREDADH1 == 62)
            {
                for (int n = 1; n <= 10; n++)
                {
                    if (FixedData.T_dispTTI * ((10f - n) / 10f) <= TTI && TTI < FixedData.T_dispTTI * ((10f - (n - 1f)) / 10f))
                    {
                        //SetTTI(n);
                        break;
                    }
                }
            }
#if _OR_PERS
            SetReleaseSpeedMpS(ETCSVrelease);
#endif
        }
    }
    public class HM : TrainControlSystem
    {
        TrainControlSystem tcs;

        float HMReleasedAlertDelayS;
        float HMReleasedEmergencyDelayS;
        float HMPressedAlertDelayS;
        float HMPressedEmergencyDelayS;

        public bool HMEmergencyBraking = false;
        public bool Pressed = false;
        Timer HMPressedAlertTimer;
        Timer HMPressedEmergencyTimer;
        Timer HMReleasedAlertTimer;
        Timer HMReleasedEmergencyTimer;

        public HM(TrainControlSystem tcs)
        {
            this.tcs = tcs;
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
                HMEmergencyBraking = false;
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
            if (!HMEmergencyBraking && (HMPressedEmergencyTimer.Triggered || HMReleasedEmergencyTimer.Triggered))
            {
                HMEmergencyBraking = true;
                if (tcs.AlerterSound()) SetVigilanceAlarm(false);
                SetVigilanceAlarmDisplay(false);
                SetVigilanceEmergencyDisplay(true);
            }
            if (HMEmergencyBraking && tcs.SpeedMpS() < 1.5f)
            {
                HMEmergencyBraking = false;
                SetVigilanceEmergencyDisplay(false);
            }
        }
    }
    enum FrecASFA
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
    class ASFADigital : ASFA
    {
        TCS_Spain tcs;
        //Combinador general
        public bool Connected;
        public bool FE;
        //Transición a LZB/ERTMS
        public bool AKT = false; //Inhibir freno de urgencia
        public bool CON = true; //Conexión de ASFA
        TcpClient client;
        Stream stm;
        public ASFADigital(TCS_Spain tcs)
        {
            this.tcs = tcs;
            client = new TcpClient();
            client.Connect("127.0.0.1", 5000);
            stm = client.GetStream();
        }
        public void Conex()
        {
            //ToDo: send DIV data
        }
        public void Update()
        {
            /*Connected = tcs.serial.Port != null;
            if (!Connected) return;*/
            tcs.SetCurrentSpeedLimitMpS(1);
            byte[] b = new byte[] {8, (byte)Baliza(), 255, 9, (byte)(tcs.SpeedMpS()*3.6f), 255};
            stm.Write(b,0,b.Length);
            if(client.Available < 3) return;
            byte[] c = new byte[3];
            stm.Read(c,0,3);
            if(c[0] == 4 && c[1] < 2) FE = c[1] == 1;
            if(c[0] == 3)
            {
                tcs.SetNextSpeedLimitMpS(MpS.FromKpH(((c[1]>>1)&0xFF)*5));
            }
            if(c[0] == 15)
            {
                bool trig = (c[1]&2) != 0;
                int num = c[1]>>2;
                if(trig)
                {
                    if(num == 0) tcs.TriggerSoundInfo1();
                    if(num == 1) tcs.TriggerSoundPenalty1();
                    if(num == 2) tcs.TriggerSoundAlert1();
                    if(num == 3) tcs.TriggerSoundAlert2();
                    if(num == 9) tcs.TriggerSoundSystemDeactivate();
                }
            }
        }
        Aspect BalizaAspect;
        Aspect BalizaNextAspect;
        float LVIstart = 0;
        FrecASFA lvi1 = FrecASFA.L11;
        FrecASFA lvi2 = FrecASFA.L11;
        public FrecASFA Baliza()
        {
            if (/*tcs.IsDirectionReverse()*/false)
            {
                if (tcs.SignalPassed) return FrecASFA.L8;
                else if (tcs.PreviaPassed) return FrecASFA.L7;
                else return FrecASFA.FP;
            }
            else
            {
                if (tcs.NextSignalDistanceM(tcs.PreviaSignalNumber) < tcs.PreviaDistance + 10 && tcs.NextSignalDistanceM(tcs.PreviaSignalNumber) > tcs.PreviaDistance)
                {
                    BalizaAspect = tcs.NextSignalAspect(tcs.PreviaSignalNumber);
                    switch (BalizaAspect)
                    {
                        case Aspect.Stop:
                        case Aspect.StopAndProceed:
                        case Aspect.Restricted:
                        case Aspect.Permission:
                            return FrecASFA.L7;
                        case Aspect.Approach_1:
                        case Aspect.Approach_2:
                        case Aspect.Approach_3:
                            return FrecASFA.L1;
                        case Aspect.Clear_1:
                            return FrecASFA.L2;
                        case Aspect.Clear_2:
                            return FrecASFA.L3;
                        default:
                            return FrecASFA.FP;
                    }
                }
                if (tcs.NextSignalDistanceM(0) < 10 && tcs.NextSignalDistanceM(0) > 5)
                {
                    BalizaAspect = tcs.NextSignalAspect(0);
                    switch (BalizaAspect)
                    {
                        case Aspect.Stop:
                        case Aspect.StopAndProceed:
                        case Aspect.Restricted:
                        case Aspect.Permission:
                            return FrecASFA.L8;
                        case Aspect.Approach_1:
                        case Aspect.Approach_2:
                        case Aspect.Approach_3:
                            return FrecASFA.L1;
                        case Aspect.Clear_1:
                            return FrecASFA.L2;
                        case Aspect.Clear_2:
                            return FrecASFA.L3;
                        default:
                            return FrecASFA.FP;
                    }
                }
                else
                {
                    BalizaNextAspect = tcs.NextSignalAspect(0);
                }
                if (tcs.AnuncioLTVPassed)
                {
                    LVIstart = tcs.DistanceM();
                    float speed = MpS.ToKpH(tcs.NextPostSpeedLimitMpS(0));
                    if (speed < 50) lvi1 = lvi2 = FrecASFA.L11;
                    else if (speed < 80)
                    {
                        lvi1 = FrecASFA.L11;
                        lvi2 = FrecASFA.L10;
                    }
                    else if (speed < 120)
                    {
                        lvi1 = FrecASFA.L10;
                        lvi2 = FrecASFA.L11;
                    }
                    else lvi1 = lvi2 = FrecASFA.L10;
                }
                if (LVIstart != 0)
                {
                    if (tcs.DistanceM() - LVIstart < 3) return lvi1;
                    if (tcs.DistanceM() - LVIstart < 6) return FrecASFA.FP;
                    if (tcs.DistanceM() - LVIstart < 9) return lvi2;
                    if (tcs.DistanceM() - LVIstart < 12) return FrecASFA.FP;
                    if (tcs.DistanceM() - LVIstart < 15) return FrecASFA.L9;
                    LVIstart = 0;
                }
            }
            return FrecASFA.FP;
        }

        public bool Urgencia()
        {
            return FE;
        }

        bool ASFA.Connected()
        {
            return Connected;
        }
    }
    public class Serial
    {
        public string Port;
        protected SerialPort sp;
        protected float PreviousTime = 0;
        protected float LastConex = 0;
        protected TCS_Spain tcs;
        public Serial(int BaudRate, TCS_Spain tcs, string port)
        {
            Port = port;
            start(BaudRate, tcs);
        }
        public void start(int BaudRate, TCS_Spain tcs)
        {
            this.tcs = tcs;
            if (Port == null)
            {
                /*string[] ports = null;
                ports = SerialPort.GetPortNames();
                foreach (string port in ports)
                {
                    try
                    {
                        using (SerialPort sp = new SerialPort(port))
                        {
                            sp.Open();
                            string incoming = sp.ReadExisting();
                            if (incoming.Contains("ASFA")) Port = port;
                            else sp.Close();
                        }
                        if (Port != null)
                        {
                            sp = new SerialPort(Port, BaudRate);
                            sp.WriteBufferSize = 128;
                            sp.ReadBufferSize = 128;
                            LastConex = tcs.ClockTime();
                            break;
                        }
                    }
                    catch (Exception) { }
                }*/
            }
            else
            {
                try
                {
                    sp = new SerialPort(Port, BaudRate);
                    sp.WriteBufferSize = 128;
                    sp.ReadBufferSize = 128;
                    LastConex = tcs.ClockTime();
                }
                catch (Exception e) { Console.WriteLine(e); }
            }
        }
        public void write(byte[] data)
        {
            try
            {
                if (!sp.IsOpen) sp.Open();
                sp.WriteTimeout = 10;
                sp.Write(data, 0, data.Length);
            }
            catch (Exception e) 
            {
                Console.WriteLine(e);
            }
        }
        public virtual void poll()
        {
            if (Port != null && tcs.ASFA!=null && !(tcs.ASFA is ASFADigital))
            {
                try
                {
                    if (!sp.IsOpen) sp.Open();
                    sp.WriteTimeout = 10;
                    var asfa = tcs.ASFA as ASFAclasico;
                    while (sp.BytesToRead > 7 && sp.ReadChar() != '\n') ;
                    byte[] data = new byte[1];
                    if (sp.BytesToRead > 0)
                    {
                        sp.Read(data, 0, 1);
                        LastConex = tcs.ClockTime();
                    }
                    if (data[0] == 48) asfa.Urgencia = false;
                    if (data[0] == 49) asfa.Urgencia = true;
                    char freq = '/';
                    switch (asfa.Baliza())
                    {
                        case ASFAclasico.Freq.FP: freq = '0'; break;
                        case ASFAclasico.Freq.L1: freq = '1'; break;
                        case ASFAclasico.Freq.L2: freq = '2'; break;
                        case ASFAclasico.Freq.L3: freq = '3'; break;
                        case ASFAclasico.Freq.L4: freq = '4'; break;
                        case ASFAclasico.Freq.L5: freq = '5'; break;
                        case ASFAclasico.Freq.L6: freq = '6'; break;
                        case ASFAclasico.Freq.L7: freq = '7'; break;
                        case ASFAclasico.Freq.L8: freq = '8'; break;
                        case ASFAclasico.Freq.L9: freq = '9'; break;
                    }
                    byte speed = Convert.ToByte(Math.Abs(MpS.ToKpH(tcs.SpeedMpS())));
                    if (PreviousTime + 0.5f < tcs.ClockTime() || freq != '0')
                    {
                        sp.Write(new char[1] { freq }, 0, 1);
                        sp.Write(new byte[1] { speed }, 0, 1);
                        sp.Write("ASFA\n");
                        PreviousTime = tcs.ClockTime();
                    }
                }
                catch (Exception)
                {
                }
            }
        }
    }
    public class HMSerial : Serial
    {
        public HMSerial(int BaudRate, TCS_Spain tcs, string port) : base(BaudRate, tcs, port)
        {
        }
        public override void poll()
        {
            if (Port != null)
            {
                try
                {
                    if (!sp.IsOpen) sp.Open();
                    sp.WriteTimeout = 10;
                    if(sp.BytesToRead > 0) tcs.HM.Pressed = sp.ReadChar()=='1';
                }
                catch (Exception)
                {
                }
            }
        }
    }
}
