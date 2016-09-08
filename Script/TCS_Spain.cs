//Scripts de sistemas de seguridad Españoles
//César Benito Lamata
//Versión 3.0
using ORTS.Common;
using ORTS.Scripting.Api;
using System;
using System.Collections.Generic;
using System.IO.Ports;
namespace ORTS.Scripting.Script
{
    public class TCS_Spain : TrainControlSystem
    {
        enum CCS
        {
            ASFA,
            LZB,
            EBICAB,
            ETCS,
            EXT
        }
        CCS ActiveCCS;

        enum Tipo_ASFA
        {
            Basico,
            ASFA200,
            Refuerzo,
            Digital,
        }
        Tipo_ASFA TipoASFA;

        enum ASFADigital_Modo
        {
            CONV,
            AVE,
            MBRA,
            BTS,
            BasicoCONV,
            BasicoAVE,
            EXT,
            OFF
        }
        ASFADigital_Modo ASFADigitalModo;

        enum ASFAInfo
        {
            Ninguno,
            Via_Libre,
            Via_Libre_Cond,
            Preanuncio_AV,
            Preanuncio,
            Anuncio_Precaucion,
            Anuncio_Parada,
            Previa_Rojo,
            Senal_Rojo,
            Rebase_Autorizado
        }
        Dictionary<ASFAInfo, Aspect> ASFASenales = new Dictionary<ASFAInfo, Aspect>()
        {
            {ASFAInfo.Ninguno, Aspect.None},
            {ASFAInfo.Via_Libre, Aspect.Clear_2},
            {ASFAInfo.Via_Libre_Cond, Aspect.Clear_1},
            {ASFAInfo.Preanuncio_AV, Aspect.Approach_1},
            {ASFAInfo.Preanuncio, Aspect.Approach_1},
            {ASFAInfo.Anuncio_Precaucion, Aspect.Approach_2},
            {ASFAInfo.Anuncio_Parada, Aspect.Approach_1},
            {ASFAInfo.Previa_Rojo, Aspect.Stop},
            {ASFAInfo.Senal_Rojo, Aspect.Stop},
            {ASFAInfo.Rebase_Autorizado, Aspect.StopAndProceed}
        };

        enum ETCS_Level
        {
            L0,
            NTC,
            L1,
            L2,
            L3
        }
        ETCS_Level CurrentETCSLevel = ETCS_Level.L0;

        public enum ETCSMode
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
        //ETCSMode CurrentETCSMode;

        enum ASFAFreq
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

        bool ASFAInstalled;
        bool ASFADigital;
        bool ETCSInstalled;
        bool ATFInstalled;
        bool ASFAActivated;
        bool LZBInstalled;
        bool LZBActivated;

        float TrainMaxSpeed;

        float VelocidadPrefijada = 0;

        int SerieTren;

        //ASFA Digital
        Aspect ASFAUltimaInfo = Aspect.Clear_2;
        Aspect BalizaAspect;
        Aspect BalizaNextAspect;
        ASFAFreq FrecBaliza = ASFAFreq.L3;

        float ASFAMaxSpeed;
        float LTVMaxSpeed = MpS.FromKpH(160f);
        float PaNMaxSpeed = MpS.FromKpH(160f);
        float ASFASpeed;
        float ASFATargetSpeed;
        float LTVTargetSpeed = MpS.FromKpH(160f);
        float PaNTargetSpeed = MpS.FromKpH(160f);
        float ASFADisplaySpeed;
        bool ASFAPressed = false;
        bool ASFARearmeFrenoPressed;
        bool ASFAOcultacionPressed;
        bool ASFARebasePressed;
        bool ASFAAumVelPressed;
        bool ASFAPaNPressed;
        bool ASFALTVPressed;
        bool ASFAAnParPressed;
        bool ASFAAnPrePressed;
        bool ASFAPrePar_VLCondPressed;
        bool ASFAModoPressed;
        bool ASFAConexPressed;
        int ASFATimesPressed;
        int ASFAConexTimesPressed;
        bool BalizaASFAPassed = false;

        Timer ASFARecTimer;
        Timer ASFAPressedTimer;
        Timer ASFAButtonsTimer;
        Timer ASFAOverrideTimer;
        Timer ASFACurveTimer;
        Timer ASFALTVCurveTimer;
        Timer ASFAPaNCurveTimer;
        Timer ASFASignalCurveTimer;
        Timer ASFAVLParpadeo1;
        Timer ASFAVLParpadeo2;
        Timer ASFARojoEncendido;
        Timer ASFACurveL2Timer;
        OdoMeter ASFAPreviaDistance;
        OdoMeter ASFATravelledDistance;
        OdoMeter ASFASignalTravelledDistance;
        OdoMeter ASFAPaNTravelledDistance;
        OdoMeter ASFALTVTravelledDistance;

        //ETCS
        int ETCSInstalledLevel;

        float LastMpS;
        float LastTime;
        float ATFAcceleration = 0;
        float ATFThrottle = 0;
        float ATFBrake = 0;
        bool ATFActivated = false;
        float ATFSpeed = 0;

        //Hombre Muerto
        float HMReleasedAlertDelayS;
        float HMReleasedEmergencyDelayS;
        float HMPressedAlertDelayS;
        float HMPressedEmergencyDelayS;

        bool HMInhibited = false;
        bool HMEmergencyBraking = false;
        bool HMPressed = false;
        Timer HMPressedAlertTimer;
        Timer HMPressedEmergencyTimer;
        Timer HMReleasedAlertTimer;
        Timer HMReleasedEmergencyTimer;

        bool ExternalEmergencyBraking;

        float PreviousSignalDistanceM = 0f;
        float PreviousPostDistanceM = 0f;
        public bool SignalPassed = false;
        bool PreviaPassed = false;
        bool IntermediateDist = false;
        bool IntermediateLTVDist = false;
        bool IntermediatePreLTVDist = false;
        bool PostPassed = false;
        bool AnuncioLTVPassed = false;
        bool PreanuncioLTVPassed = false;
        float PreviaDistance = 300f;
        int PreviaSignalNumber;
        float AnuncioDistance = 1500f;
        Aspect CurrentSignalAspect;

        bool ASFAUrgencia;
        bool ASFAEficacia;
        bool ASFAAlarma;
        bool ASFAControlArranque;
        bool ASFAControlL1;
        bool ASFAControlL2;
        bool ASFAControlL3;
        bool ASFAControlL5;
        bool ASFAControlL6;
        bool ASFAControlL7;
        bool ASFAControlL8;
        bool ASFAControlDesvio;
        bool ASFAControlPNDesprotegido;
        bool ASFAControlLTV;
        bool ASFAControlPreanuncioLTV;
        bool ASFALTVCumplida;
        bool ASFAPNVelocidadBajada;
        bool ASFARebaseAuto;
        bool ASFAAumentoVelocidadL5;
        bool ASFAAumentoVelocidadL6;
        bool ASFAAumentoVelocidadL8;
        bool ASFAAumentoVelocidadDesvio;
        bool ASFAAumentoVelocidadLTV;
        bool ASFABalizaRecibida;
        bool ASFABalizaSenal;
        bool ASFAModoAV = false;
        bool ASFAModoCONV = true;

        float ASFAVCControlArranque = 100f / 3.6f;
        float ASFAVCIVlCond = 100f / 3.6f;
        float ASFAVCFVlCond = 100f / 3.6f;
        float ASFAVCIAnPar = 100f / 3.6f;
        float ASFAVCFAnPar = 80f / 3.6f;
        float ASFAVCFAnPre = 80f / 3.6f;
        float ASFAVCFAnPreAumVel = 100f / 3.6f;
        float ASFAVCFPrePar = 80f / 3.6f;
        float ASFAVCFPreParAumVel = 100f / 3.6f;
        float ASFAVCISecPreParA;
        float ASFAVCFSecPreParA;
        float ASFAVCISecPreParA1AumVel;
        float ASFAVCFSecPreParA1AumVel;
        float ASFAVCISecPreParA2AumVel;
        float ASFAVCFSecPreParA2AumVel;
        float ASFAVCFLTV = 60f / 3.6f;
        float ASFAVCFLTVAumVel = 100f / 3.6f;
        float ASFAVCFPN;
        float ASFAVCIPar = 60f / 3.6f;
        float ASFAVCFPar = 30f / 3.6f;
        float ASFAVCPar = 40f / 3.6f;
        float ASFAVCParAumVel = 100 / 3.6f;
        float ASFAVCDesv = 60f / 3.6f;
        float ASFAVCDesvAumVel = 90f / 3.6f;
        float ASFATipoTren = 100f / 3.6f;
        float ASFAVControl;
        float ASFAVControlFinal;
        float ASFAVIntervencion;

        float ASFAVCVlCondTReac = 9f;
        float ASFAVIVlCondTReac = 9f;
        float ASFAVCVlCondDec = 0.5f;
        float ASFAVIVlCondDec = 0.5f;
        float ASFAVCAnParTReac = 9f;
        float ASFAVIAnParTReac = 9f;
        float ASFAVCAnParDec = 0.5f;
        float ASFAVIAnParDec = 0.5f;
        float ASFAVCParTReac = 9f;
        float ASFAVIParTReac = 9f;
        float ASFAVCParDec = 0.5f;
        float ASFAVIParDec = 0.5f;
        float ASFAVCSecPreParATReac = 9f;
        float ASFAVCSecPreParA1AumVelTReac = 9f;
        float ASFAVCSecPreParA2AumVelTReac = 9f;
        float ASFAVCSecPreParADec = 0.5f;
        float ASFAVISecPreParATReac = 9f;
        float ASFAVISecPreParA1AumVelTReac = 9f;
        float ASFAVISecPreParA2AumVelTReac = 9f;
        float ASFAVISecPreParADec = 0.5f;

        float ASFAVCFAnParAV;
        float ASFAVCFAnPreAV;
        float ASFAVCFAnPreAumVelAV;
        float ASFAVCFPreParAV;
        float ASFAVCFPreParAumVelAV;
        float ASFAVCDesvAV;
        float ASFAVCDesvAumVelAV;
        float ASFAVCFLTVAV;
        float ASFAVCFLTVAumVelAV;
        float ASFAVCISecPreParAAV;
        float ASFAVCFSecPreParAAV;
        float ASFAVCISecPreParA1AumVelAV;
        float ASFAVCFSecPreParA1AumVelAV;
        float ASFAVCISecPreParA2AumVelAV;
        float ASFAVCFSecPreParA2AumVelAV;

        int ASFANumeroBaliza = 0;

        ASFAInfo ASFAUltimaInfoRecibida;
        ASFAInfo ASFAAnteriorInfo;

        ASFAFreq ASFAFrecuencia;
        ASFAFreq ASFAUltimaFrecuencia;
        ASFAFreq ASFAAnteriorFrecuencia;

        Timer ASFARec;
        Timer ASFACurva;
        Timer ASFACurvaL1;
        Timer ASFACurvaL2;
        Timer ASFACurvaL7;
        Timer ASFACurvaL8;
        Timer ASFACurvaLTV;
        Timer ASFACurvaPN;
        Timer ASFALiberacionRojo;
        Timer ASFATiempoDesvio;
        Timer ASFATiempoAA;
        Timer ASFATiempoRebase;
        Timer ASFATiempoEncendido;

        OdoMeter ASFADistancia;
        OdoMeter ASFADistanciaPN;
        OdoMeter ASFADistanciaPrevia;

        bool ETCSPressed;
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
        public override void Initialize()
        {
            /*MAS = new MovementAuthoritySection[1];
			MAS[0] = new MovementAuthoritySection(SRSpeedAuth, SRDistAuth, 0f, this);
			MA = new MovementAuthority(SRDistAuth, 0f, ETCSMode.SR, this, MAS);*/
            ASFAInstalled = GetBoolParameter("General", "ASFA", true);
            ETCSInstalled = GetBoolParameter("General", "ETCS", false);
            HMInhibited = GetBoolParameter("General", "HMInhibited", true);
            ATFActivated = ATFInstalled = GetBoolParameter("General", "ATO", false) || GetBoolParameter("General", "ATF", false);
            LZBActivated = LZBInstalled = GetBoolParameter("General", "LZB", false);
            ASFADigital = GetBoolParameter("ASFA", "Digital", false);
            ETCSInstalledLevel = GetIntParameter("ETCS", "Level", 0);
            ASFAUltimaInfo = Aspect.None;
            SetNextSignalAspect(ASFAUltimaInfo);
            TrainMaxSpeed = MpS.FromKpH(GetFloatParameter("General", "MaxSpeed", 380f));
            ASFATipoTren = MpS.FromKpH(GetFloatParameter("ASFA", "TipoTren", 250f));
            SerieTren = GetIntParameter("General", "Serie", 440);
            if (ASFATipoTren > MpS.FromKpH(205f)) ASFATipoTren = TrainMaxSpeed;
            ASFASpeed = 0f;
            ASFADisplaySpeed = ASFATargetSpeed = ASFASpeed;
            if (ASFAInstalled)
            {
                ActiveCCS = CCS.ASFA;
                ASFAConexPressed = true;
                SetNextSpeedLimitMpS(ASFATargetSpeed);
                if (ASFADigital)
                {
                    ASFAUrgencia = true;
                    TipoASFA = Tipo_ASFA.Digital;
                    ASFADigitalModo = ASFADigital_Modo.OFF;
                }
                else
                {
                    if (TrainMaxSpeed <= MpS.FromKpH(160f)) TipoASFA = Tipo_ASFA.Basico;
                    else TipoASFA = Tipo_ASFA.ASFA200;
                }
            }
            if (LZBInstalled)
            {
                LZBCenter = new LZBCenter(!GetBoolParameter("LZB", "Canton_Movil", false));
                ActiveCCS = CCS.LZB;
                LZBAlertarLiberar();
                LZBSupervising = true;
                LZBRecTimer = new Timer(this);
                LZBRecTimer.Setup(8);
                LZBCurveTimer = new Timer(this);
                LZBVTimer = new Timer(this);
                LZBVTimer.Setup(0.5f);
            }
            if (ETCSInstalled)
            {
                ActiveCCS = CCS.ETCS;
                switch (ETCSInstalledLevel)
                {
                    case 0:
                        CurrentETCSLevel = ETCS_Level.L0;
                        break;
                    case 1:
                        CurrentETCSLevel = ETCS_Level.L1;
                        break;
                    case 2:
                        CurrentETCSLevel = ETCS_Level.L2;
                        break;
                    case 3:
                        CurrentETCSLevel = ETCS_Level.L3;
                        break;
                    case 4:
                        CurrentETCSLevel = ETCS_Level.NTC;
                        break;
                }
            }
            HMReleasedAlertDelayS = 2.5f;
            HMReleasedEmergencyDelayS = 5f;
            HMPressedAlertDelayS = 32.5f;
            HMPressedEmergencyDelayS = 35f;
            HMPressedAlertTimer = new Timer(this);
            HMPressedAlertTimer.Setup(HMPressedAlertDelayS);
            HMPressedEmergencyTimer = new Timer(this);
            HMPressedEmergencyTimer.Setup(HMPressedEmergencyDelayS);
            HMReleasedAlertTimer = new Timer(this);
            HMReleasedAlertTimer.Setup(HMReleasedAlertDelayS);
            HMReleasedEmergencyTimer = new Timer(this);
            HMReleasedEmergencyTimer.Setup(HMReleasedEmergencyDelayS);

            ASFARecTimer = new Timer(this);
            ASFARecTimer.Setup(3f);
            ASFAPressedTimer = new Timer(this);
            ASFAPressedTimer.Setup(3f);
            ASFAButtonsTimer = new Timer(this);
            ASFAButtonsTimer.Setup(0.5f);
            ASFAOverrideTimer = new Timer(this);
            ASFAOverrideTimer.Setup(10f);
            ASFAPreviaDistance = new OdoMeter(this);
            ASFAPreviaDistance.Setup(450f);
            ASFACurveTimer = new Timer(this);
            ASFACurveTimer.Setup(100f);
            ASFALTVCurveTimer = new Timer(this);
            ASFALTVCurveTimer.Setup(100f);
            ASFAPaNCurveTimer = new Timer(this);
            ASFAPaNCurveTimer.Setup(100f);
            ASFASignalCurveTimer = new Timer(this);
            ASFASignalCurveTimer.Setup(100f);
            ASFATravelledDistance = new OdoMeter(this);
            ASFATravelledDistance.Setup(1800f);
            ASFALTVTravelledDistance = new OdoMeter(this);
            ASFALTVTravelledDistance.Setup(1800f);
            ASFAPaNTravelledDistance = new OdoMeter(this);
            ASFAPaNTravelledDistance.Setup(1800f);
            ASFASignalTravelledDistance = new OdoMeter(this);
            ASFASignalTravelledDistance.Setup(1800f);
            ASFAVLParpadeo1 = new Timer(this);
            ASFAVLParpadeo1.Setup(0.5f);
            ASFAVLParpadeo2 = new Timer(this);
            ASFAVLParpadeo2.Setup(0.5f);
            ASFARojoEncendido = new Timer(this);
            ASFARojoEncendido.Setup(10f);
            ASFACurveL2Timer = new Timer(this);
            ASFACurveL2Timer.Setup(29f);

            Activated = true;
            PreviousSignalDistanceM = 0f;
            PreviousPostDistanceM = 0f;

            ASFARec = new Timer(this);
            ASFARec.Setup(3f);
            ASFACurva = new Timer(this);
            ASFACurva.Setup(100f);
            ASFACurvaL1 = new Timer(this);
            ASFACurvaL1.Setup(100f);
            ASFACurvaL2 = new Timer(this);
            ASFACurvaL2.Setup(100f);
            ASFACurvaL7 = new Timer(this);
            ASFACurvaL7.Setup(100f);
            ASFACurvaL8 = new Timer(this);
            ASFACurvaL8.Setup(100f);
            ASFACurvaLTV = new Timer(this);
            ASFACurvaLTV.Setup(100f);
            ASFACurvaPN = new Timer(this);
            ASFACurvaPN.Setup(100f);
            ASFALiberacionRojo = new Timer(this);
            ASFALiberacionRojo.Setup(20f);
            ASFATiempoDesvio = new Timer(this);
            ASFATiempoDesvio.Setup(20f);
            ASFATiempoAA = new Timer(this);
            ASFATiempoAA.Setup(20f);
            ASFATiempoRebase = new Timer(this);
            ASFATiempoRebase.Setup(10f);
            ASFATiempoEncendido = new Timer(this);
            ASFATiempoEncendido.Setup(10f);

            ASFADistancia = new OdoMeter(this);
            ASFADistancia.Setup(2000f);
            ASFADistanciaPN = new OdoMeter(this);
            ASFADistanciaPN.Setup(1800f);
            ASFADistanciaPrevia = new OdoMeter(this);
            ASFADistanciaPrevia.Setup(450f);
            SetVelocidadesASFA();

            MPsp = new List<SpeedProfile>();
            TSRsp = new List<SpeedProfile>();
            SSPsp = new List<SpeedProfile>();
            SpdProf = new List<SpeedProfile>();
        }
        bool LineaConvencional;
        public override void Update()
        {
            LineaConvencional = true;
            if (CurrentPostSpeedLimitMpS() > MpS.FromKpH(200)) LineaConvencional = false;
            for (int i = 0; i < 5; i++)
            {
                if (NextPostSpeedLimitMpS(i) > MpS.FromKpH(200)) LineaConvencional = false;
            }
            if (LineaConvencional)
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
            UpdateSignalPassed();
            UpdateDistanciaPrevia();
            UpdatePostPassed();
            UpdateAnuncioLTVPassed();
            if (!HMInhibited && !ASFAActivated && !ETCSInstalled && !ATFActivated) UpdateHM();
            else if (AlerterSound()) SetVigilanceAlarm(false);
            Arduino();
            if (!IsTrainControlEnabled())
            {
                ASFAActivated = ASFAEficacia = false;
                CurrentETCSMode = ETCSMode.IS;
            }
            if (ArduinoPort != null && TipoASFA != Tipo_ASFA.Digital) ActiveCCS = CCS.EXT;
            if (ActiveCCS != CCS.EXT)
            {
                BotonesASFA();
                if (ASFAConexPressed)
                {
                    if (!ASFAActivated && !ASFATiempoEncendido.Started)
                    {
                        if (TipoASFA == Tipo_ASFA.Digital)
                        {
                            if (ASFADigitalModo != ASFADigital_Modo.EXT) ActiveCCS = CCS.ASFA;
                        }
                        else if (ASFAEficacia) ActiveCCS = CCS.ASFA;
                        if (TipoASFA == Tipo_ASFA.Digital)
                        {
                            if (ASFADigitalModo != ASFADigital_Modo.EXT)
                            {
                                ASFAUrgencia = true;
                                if (ASFAModoAV && !ASFAModoCONV) ASFADigitalModo = ASFADigital_Modo.AVE;
                                else ASFADigitalModo = ASFADigital_Modo.CONV;
                                TriggerSoundInfo1();
                                ASFATiempoEncendido.Start();
                                ASFAConexTimesPressed = 0;
                            }
                            else
                            {
                                ASFAEficacia = false;
                                ASFAActivated = true;
                            }
                        }
                        else
                        {
                            ASFAActivated = true;
                            ASFAEficacia = ActiveCCS == CCS.ASFA;
                            ASFAConexTimesPressed = 0;
                            SetVelocidadesASFA();
                            TriggerSoundInfo1();
                        }
                    }
                    if (ASFATiempoEncendido.Triggered)
                    {
                        ASFATiempoEncendido.Stop();
                        TriggerSoundSystemActivate();
                        ASFAActivated = true;
                        ASFAUrgencia = true;
                        ASFAUltimaInfoRecibida = ASFAInfo.Ninguno;
                        ASFAControlL1 = ASFAControlL2 = ASFAControlL3 = ASFAControlL5 = ASFAControlL6 = ASFAControlL7 = ASFAControlL8 = ASFAControlLTV = ASFAControlPNDesprotegido = ASFAControlPreanuncioLTV = false;
                        ASFAControlArranque = true;
                        ASFAEficacia = BalizaASFA() == ASFAFreq.FP;
                    }
                }
                else if (ASFAActivated)
                {
                    ASFAActivated = false;
                    ASFAConexTimesPressed = 0;
                }
                if (ASFAInstalled && ASFAActivated)
                {
                    switch (TipoASFA)
                    {
                        case Tipo_ASFA.Basico:
                            UpdateASFABasico();
                            break;
                        case Tipo_ASFA.ASFA200:
                            UpdateASFA200();
                            break;
                        case Tipo_ASFA.Digital:
                            UpdateASFADigital();
                            break;
                    }
                    if (ActiveCCS == CCS.ASFA) SetCurrentSpeedLimitMpS(0);
                }
                else
                {
                    ASFAUrgencia = false;
                    if (ActiveCCS == CCS.ASFA) SetCurrentSpeedLimitMpS(TrainMaxSpeed);
                }
            }
            if (LZBInstalled && (!ETCSInstalled || CurrentETCSMode == ETCSMode.IS))
            {
                UpdateLZB();
                if (LZBSupervising)
                {
                    ActiveCCS = CCS.LZB;
                    if (ASFAInstalled)
                    {
                        if (TipoASFA == Tipo_ASFA.Digital) ASFADigitalModo = ASFADigital_Modo.EXT;
                        ASFAEficacia = false;
                    }
                }
                else if (ASFAInstalled)
                {
                    ActiveCCS = CCS.ASFA;
                    if (TipoASFA == Tipo_ASFA.Digital) ASFADigitalModo = ASFADigital_Modo.CONV;
                    ASFAEficacia = ASFAControlArranque = ASFAActivated;
                }
            }
            if (ETCSInstalled) UpdateETCS();
            else ETCSEmergencyBraking = false;
            if (ATFInstalled)
            {
                if (ATFActivated) ATFActivated = false;
                ATFSpeed = float.MaxValue;
#if _OR_PERS
                VelocidadPrefijada = Locomotive.ThrottleController.CurrentValue * TrainMaxSpeed;
#endif
                if (LZBSupervising && (BrakePipePressureBar() > 4.8 || LZBEmergencyBrake || ATFFullBrake))
                {
                    ATFActivated = true;
                    ATFSpeed = LZBMaxSpeed;
                    if (LZBTargetDistance < 15 && SpeedMpS() < 4 && LZBTargetSpeed == 0) ATFSpeed = 0;
                }
                if (CurrentETCSMode == ETCSMode.FS && ETCSVperm > ETCSVrelease && SpeedMpS() > 0.1 && (BrakePipePressureBar() > 4.8 || ETCSEmergencyBraking || ETCSServiceBrake || ATFFullBrake))
                {
                    ATFActivated = true;
                    ATFSpeed = ETCSVperm > ETCSVtarget ? ETCSVperm - 1 : ETCSVperm;
                }
                if (VelocidadPrefijada > 1)
                {
                    ATFActivated = true;
                    ATFSpeed = Math.Min(VelocidadPrefijada, ATFSpeed);
                    if (ActiveCCS != CCS.ETCS && ActiveCCS != CCS.LZB) SetCurrentSpeedLimitMpS(VelocidadPrefijada);
                }
                if (ATFActivated && VelocidadPrefijada < 1 && (CurrentETCSMode != ETCSMode.FS || SpeedMpS() < 0.1 || ETCSVrelease > ETCSVperm || (BrakePipePressureBar() < 4.8 && !ETCSEmergencyBraking && !ETCSServiceBrake && !ATFFullBrake)) && (!LZBSupervising /*|| SpeedMpS() < 1 */|| (BrakePipePressureBar() < 4.8 && !LZBEmergencyBrake && !ATFFullBrake)))
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
            SetPenaltyApplicationDisplay(ASFAUrgencia || ETCSEmergencyBraking || ETCSServiceBrake || LZBOE);
            SetFullBrake((ETCSServiceBrake && !ETCSEmergencyBraking) || ATFFullBrake);
            SetEmergencyBrake(ASFAUrgencia || ETCSEmergencyBraking || HMEmergencyBraking || LZBEmergencyBrake);
            SetTractionAuthorization(!ASFAUrgencia && !TCO && !ETCSEmergencyBraking && !ETCSServiceBrake && !HMEmergencyBraking && !LZBEmergencyBrake);
            bool ETCSNeutralZone = false;
            bool ETCSLowerPantographs = false;
            SetPowerAuthorization(!ETCSNeutralZone);
            if (ETCSLowerPantographs) SetPantographsDown();
        }
        bool ASFAFrenarOn = false;
        bool ASFARecOn = false;
        bool ASFARebaseOn = false;
        bool ASFAParadaOn = false;
        bool ASFAVLOn = false;
        protected void UpdateASFABasico()
        {
            if (ASFAEficacia)
            {
                if (ASFARebasePressed && !ASFAOverrideTimer.Started)
                {
                    ASFAOverrideTimer.Setup(10f);
                    ASFAOverrideTimer.Start();
                    ASFARebaseAuto = true;
                    SetNextSignalAspect(Aspect.StopAndProceed);
                    ASFARebaseOn = true;
                }
                ASFARearmeFrenoPressed = ASFAPressed;
                FrecBaliza = BalizaASFA();
                if (FrecBaliza != ASFAFreq.FP)
                {
                    ASFARojoEncendido.Stop();
                    switch (FrecBaliza)
                    {
                        case ASFAFreq.L1:
                            ASFARecTimer.Start();
                            ASFAUltimaInfo = Aspect.Approach_1;
                            break;
                        case ASFAFreq.L2:
                        case ASFAFreq.L3:
                            TriggerSoundInfo1();
                            ASFAUltimaInfo = Aspect.Clear_2;
                            break;
                        case ASFAFreq.L7:
                            TriggerSoundInfo2();
                            if (ASFATipoTren >= MpS.FromKpH(110f)) ASFAMaxSpeed = MpS.FromKpH(60f);
                            else if (ASFATipoTren >= MpS.FromKpH(80f)) ASFAMaxSpeed = MpS.FromKpH(50f);
                            else ASFAMaxSpeed = MpS.FromKpH(35f);
                            if (SpeedMpS() < ASFAMaxSpeed)
                            {
                                ASFARojoEncendido.Start();
                                TriggerSoundPenalty1();
                            }
                            else
                            {
                                TriggerSoundInfo2();
                                ASFAUrgencia = true;
                            }
                            ASFAUltimaInfo = Aspect.Stop;
                            break;
                        case ASFAFreq.L8:
                            ASFAUltimaInfo = Aspect.Stop;
                            if (!ASFARebaseAuto)
                            {
                                ASFAUrgencia = true;
                                TriggerSoundInfo2();
                            }
                            else
                            {
                                ASFARojoEncendido.Start();
                                TriggerSoundInfo2();
                            }
                            ASFARebaseAuto = false;
                            break;
                        default:
                            ASFARecTimer.Start();
                            ASFAUltimaInfo = Aspect.Stop;
                            break;
                    }
                    SetNextSignalAspect(ASFAUltimaInfo);
                }
                if (ASFARojoEncendido.Triggered)
                {
                    SetNextSignalAspect(Aspect.None);
                    ASFARojoEncendido.Stop();
                }
                if (ASFAOverrideTimer.Triggered)
                {
                    ASFARebaseAuto = false;
                }
                if (!ASFARebasePressed && ASFAOverrideTimer.Started)
                {
                    ASFAOverrideTimer.Stop();
                    SetNextSignalAspect(Aspect.None);
                    ASFARebaseOn = false;
                }
                if (ASFARecTimer.Started && !ASFARecTimer.Triggered)
                {
                    TriggerSoundPenalty1();
                    SetVigilanceAlarmDisplay(true);
                    ASFARecOn = true;
                }
                if (ASFARecTimer.Triggered)
                {
                    ASFAUrgencia = true;
                    TriggerSoundPenalty2();
                    ASFARecTimer.Stop();
                    SetVigilanceAlarmDisplay(false);
                    ASFARecOn = false;
                }
                if (ASFAPressed && ASFARecTimer.Started)
                {
                    SetVigilanceAlarmDisplay(false);
                    ASFARecOn = false;
                    ASFARecTimer.Stop();
                    TriggerSoundPenalty2();
                    SetNextSignalAspect(Aspect.None);
                }
                ASFAFrenarOn = ASFAUltimaInfo == Aspect.Approach_1 && ASFARecTimer.Started;
                ASFAParadaOn = ASFARojoEncendido.Started;
                if (ASFAUrgencia && ASFARearmeFrenoPressed && SpeedMpS() < 1.5f) ASFAUrgencia = false;
            }
            else ASFAUrgencia = false;
        }
        protected void UpdateASFA200()
        {
            if (ASFAEficacia)
            {
                if (ASFAMaxSpeed == 0) ASFAMaxSpeed = float.MaxValue;
                if (ASFARebasePressed)
                {
                    ASFAOverrideTimer.Setup(10f);
                    ASFAOverrideTimer.Start();
                    ASFARebaseAuto = true;
                    SetNextSignalAspect(Aspect.StopAndProceed);
                    ASFARebaseOn = true;
                }
                ASFARearmeFrenoPressed = ASFAPressed;
                FrecBaliza = BalizaASFA();
                if (FrecBaliza != ASFAFreq.FP)
                {
                    ASFAMaxSpeed = float.MaxValue;
                    ASFAVLParpadeo1.Stop();
                    ASFAVLParpadeo2.Stop();
                    ASFARojoEncendido.Stop();
                    ASFACurveL2Timer.Stop();
                    switch (FrecBaliza)
                    {
                        case ASFAFreq.L1:
                            ASFAMaxSpeed = MpS.FromKpH(160f);
                            ASFARecTimer.Start();
                            ASFAUltimaInfo = Aspect.Approach_1;
                            break;
                        case ASFAFreq.L2:
                            ASFARecTimer.Start();
                            ASFACurveL2Timer.Start();
                            ASFAUltimaInfo = Aspect.Clear_1;
                            ASFAVLParpadeo1.Start();
                            break;
                        case ASFAFreq.L3:
                            TriggerSoundInfo1();
                            ASFAUltimaInfo = Aspect.Clear_2;
                            break;
                        case ASFAFreq.L7:
                            if (ASFATipoTren >= MpS.FromKpH(110f)) ASFAMaxSpeed = MpS.FromKpH(60f);
                            else if (ASFATipoTren >= MpS.FromKpH(80f)) ASFAMaxSpeed = MpS.FromKpH(50f);
                            else ASFAMaxSpeed = MpS.FromKpH(35f);
                            if (SpeedMpS() < ASFAMaxSpeed)
                            {
                                ASFARojoEncendido.Start();
                                TriggerSoundPenalty1();
                            }
                            else
                            {
                                TriggerSoundInfo2();
                                ASFAUrgencia = true;
                            }
                            ASFAMaxSpeed = float.MaxValue;
                            ASFAUltimaInfo = Aspect.Stop;
                            break;
                        case ASFAFreq.L8:
                            ASFAUltimaInfo = Aspect.Stop;
                            if (!ASFARebaseAuto)
                            {
                                ASFAUrgencia = true;
                                TriggerSoundInfo2();
                            }
                            else
                            {
                                ASFARojoEncendido.Start();
                                TriggerSoundInfo2();
                            }
                            ASFARebaseAuto = false;
                            break;
                        default:
                            ASFARecTimer.Start();
                            ASFAUltimaInfo = Aspect.Stop;
                            break;
                    }
                    SetNextSignalAspect(ASFAUltimaInfo);
                }
                if (ASFACurveL2Timer.Started && ASFACurveL2Timer.RemainingValue < 11f)
                {
                    if (ASFACurveL2Timer.Triggered) ASFAMaxSpeed = MpS.FromKpH(160f);
                    else ASFAMaxSpeed = MpS.FromKpH(180f);
                }
                if (ASFAVLParpadeo1.Started)
                {
                    if (ASFAVLParpadeo1.Triggered)
                    {
                        ASFAVLParpadeo1.Stop();
                        ASFAVLParpadeo2.Start();
                    }
                    else SetNextSignalAspect(Aspect.Clear_1);
                }
                if (ASFAVLParpadeo2.Started)
                {
                    if (ASFAVLParpadeo2.Triggered)
                    {
                        ASFAVLParpadeo2.Stop();
                        ASFAVLParpadeo1.Start();
                    }
                    else SetNextSignalAspect(Aspect.None);
                }
                if (ASFARojoEncendido.Triggered)
                {
                    SetNextSignalAspect(Aspect.None);
                    ASFARojoEncendido.Stop();
                }
                if (ASFAOverrideTimer.Triggered) ASFARebaseAuto = false;
                if (SpeedMpS() > ASFAMaxSpeed) ASFAUrgencia = true;
                if (ASFARecTimer.Started && !ASFARecTimer.Triggered)
                {
                    SetVigilanceAlarmDisplay(true);
                    ASFARecOn = true;
                    TriggerSoundPenalty1();
                }
                if (ASFARecTimer.Triggered)
                {
                    ASFAUrgencia = true;
                    TriggerSoundPenalty2();
                    ASFARecTimer.Stop();
                    SetVigilanceAlarmDisplay(false);
                    ASFARecOn = false;
                }
                if (ASFAPressed && ASFARecTimer.Started)
                {
                    ASFARecTimer.Stop();
                    TriggerSoundPenalty2();
                    SetVigilanceAlarmDisplay(false);
                    ASFARecOn = false;
                    SetNextSignalAspect(Aspect.None);
                    ASFAMaxSpeed = float.MaxValue;
                }
                ASFAFrenarOn = ASFAUltimaInfo == Aspect.Approach_1 && ASFARecTimer.Started;
                ASFAParadaOn = ASFARojoEncendido.Started;
                ASFAVLOn = ASFAVLParpadeo1.Started && !ASFAVLParpadeo1.Triggered;
                if (ASFAUrgencia && ASFARearmeFrenoPressed && SpeedMpS() < 1.5f) ASFAUrgencia = false;
            }
            else ASFAUrgencia = false;
        }
        protected void UpdateASFADigital()
        {
            if (ASFAModoPressed && ASFADigitalModo != ASFADigital_Modo.EXT)
            {
                if (ASFAModoAV && ASFAModoCONV)
                {
                    if (SpeedMpS() < 0.1f) switch (ASFADigitalModo)
                        {
                            case ASFADigital_Modo.CONV:
                                ASFADigitalModo = ASFADigital_Modo.AVE;
                                break;
                            case ASFADigital_Modo.AVE:
                                ASFADigitalModo = ASFADigital_Modo.BTS;
                                break;
                            case ASFADigital_Modo.BTS:
                                ASFADigitalModo = ASFADigital_Modo.MBRA;
                                break;
                            case ASFADigital_Modo.MBRA:
                                ASFADigitalModo = ASFADigital_Modo.CONV;
                                ASFAControlArranque = true;
                                break;
                        }
                    else if (ASFADigitalModo == ASFADigital_Modo.CONV) ASFADigitalModo = ASFADigital_Modo.AVE;
                    else if (ASFADigitalModo == ASFADigital_Modo.AVE) ASFADigitalModo = ASFADigital_Modo.CONV;
                }
                if (ASFAModoAV && !ASFAModoCONV)
                {
                    if (SpeedMpS() < 0.1f) switch (ASFADigitalModo)
                        {
                            case ASFADigital_Modo.AVE:
                                ASFADigitalModo = ASFADigital_Modo.BTS;
                                break;
                            case ASFADigital_Modo.BTS:
                                ASFADigitalModo = ASFADigital_Modo.MBRA;
                                break;
                            case ASFADigital_Modo.MBRA:
                                ASFADigitalModo = ASFADigital_Modo.AVE;
                                ASFAControlArranque = true;
                                break;
                        }
                }
                if (ASFAModoCONV && !ASFAModoAV)
                {
                    if (SpeedMpS() < 0.1f) switch (ASFADigitalModo)
                        {
                            case ASFADigital_Modo.CONV:
                                ASFADigitalModo = ASFADigital_Modo.BTS;
                                break;
                            case ASFADigital_Modo.BTS:
                                ASFADigitalModo = ASFADigital_Modo.MBRA;
                                break;
                            case ASFADigital_Modo.MBRA:
                                ASFADigitalModo = ASFADigital_Modo.CONV;
                                ASFAControlArranque = true;
                                break;
                        }
                }
                ASFAModoPressed = false;
                ASFATimesPressed = 0;
            }
            switch (ASFADigitalModo)
            {
                case ASFADigital_Modo.CONV:
                    if (ASFAModoCONV) UpdateASFADigitalConv();
                    else ASFADigitalModo = ASFADigital_Modo.AVE;
                    break;
                case ASFADigital_Modo.AVE:
                    if (ASFAModoAV) UpdateASFADigitalAVE();
                    else ASFADigitalModo = ASFADigital_Modo.CONV;
                    break;
                case ASFADigital_Modo.MBRA:
                    ASFAVControl = MpS.FromKpH(30f);
                    ASFAVIntervencion = MpS.FromKpH(35f);
                    ASFAVControlFinal = ASFAVControl;
                    if (SpeedMpS() > 0.1f)
                    {
                        ASFAUltimaInfoRecibida = ASFAInfo.Ninguno;
                        ASFAControlL1 = ASFAControlL2 = ASFAControlL3 = ASFAControlL5 = ASFAControlL6 = ASFAControlL7 = ASFAControlL8 = ASFAControlArranque = ASFAControlPNDesprotegido = ASFAControlDesvio = ASFAControlPreanuncioLTV = ASFAControlLTV = false;
                    }
                    SetNextSignalAspect(ASFASenales[ASFAUltimaInfoRecibida]);
                    SetNextSpeedLimitMpS(ASFAVControlFinal);
                    SetInterventionSpeedLimitMpS(ASFAVIntervencion);
                    break;
                case ASFADigital_Modo.BTS:
                    ASFAVControl = ASFAVCControlArranque;
                    ASFAVIntervencion = ASFAVCControlArranque + MpS.FromKpH(5f);
                    ASFAVControlFinal = ASFAVCControlArranque;
                    ASFAVControl = Math.Min(ASFAVControl, TrainMaxSpeed);
                    ASFAVIntervencion = Math.Min(ASFAVIntervencion, TrainMaxSpeed + MpS.FromKpH(5f));
                    ASFAVControlFinal = Math.Min(ASFAVControlFinal, TrainMaxSpeed);
                    ASFAUltimaInfoRecibida = ASFAInfo.Ninguno;
                    SetNextSignalAspect(ASFASenales[ASFAUltimaInfoRecibida]);
                    SetNextSpeedLimitMpS(ASFAVControlFinal);
                    SetInterventionSpeedLimitMpS(ASFAVIntervencion);
                    break;
                case ASFADigital_Modo.EXT:
                    ASFAUrgencia = false;
                    ASFAEficacia = false;
                    break;
            }
            if (ASFAEficacia)
            {
                SetCurrentSpeedLimitMpS(0);
                if (ASFAVControlFinal == Math.Min(ASFATipoTren, TrainMaxSpeed) && (!ASFAControlArranque || ASFAControlLTV || ASFAControlPNDesprotegido))
                {
                    SetVigilanceAlarmDisplay(false);
                    SetVigilanceEmergencyDisplay(true);
                }
                else if (ASFAVControlFinal < 0.1f || ASFAVControl == ASFAVControlFinal)
                {
                    SetVigilanceAlarmDisplay(false);
                    SetVigilanceEmergencyDisplay(false);
                }
                else
                {
                    SetVigilanceAlarmDisplay(true);
                    SetVigilanceEmergencyDisplay(false);
                }
                if (SpeedMpS() >= ASFAVControl + 0.25f * (ASFAVIntervencion - ASFAVControl) && !ASFAUrgencia)
                {
                    TriggerSoundWarning1();
                    SetOverspeedWarningDisplay(true);
                }
                if (SpeedMpS() >= ASFAVIntervencion && !ASFAUrgencia)
                {
                    ASFAUrgencia = true;
                    TriggerSoundSystemDeactivate();
                    TriggerSoundWarning2();
                }
                if (SpeedMpS() <= ASFAVControl - MpS.FromKpH(3f) || ASFAUrgencia)
                {
                    SetOverspeedWarningDisplay(false);
                    TriggerSoundWarning2();
                }
                if (SpeedMpS() < MpS.FromKpH(5f) && ASFAUrgencia && ASFARearmeFrenoPressed) ASFAUrgencia = false;
            }
            else if (ASFADigitalModo != ASFADigital_Modo.EXT) ASFAUrgencia = false;
        }
        protected void UpdateASFADigitalConv()
        {
            ASFABalizaSenal = false;
            ASFAFrecuencia = BalizaASFA();
            ASFABalizaRecibida = ASFAFrecuencia != ASFAFreq.FP;
            if (ASFABalizaRecibida)
            {
                ASFACurva.Start();
                ASFADistancia.Start();
                ASFARec.Stop();
                ASFAAnteriorFrecuencia = ASFAUltimaFrecuencia;
                ASFAUltimaFrecuencia = ASFAFrecuencia;
                switch (ASFAFrecuencia)
                {
                    case ASFAFreq.L2:
                        BalizaSenal();
                        ASFAUltimaInfoRecibida = ASFAInfo.Via_Libre_Cond;
                        if (TrainMaxSpeed >= MpS.FromKpH(160f))
                        {
                            TriggerSoundPenalty1();
                            ASFARec.Start();
                            if (!ASFAControlL2)
                            {
                                ASFAControlL2 = true;
                                if (ASFAControlL1 || ASFAControlL5 || ASFAControlL6 || ASFAControlL7 || ASFAControlL8) ASFACurvaL2.Setup(0f);
                                else ASFACurvaL2.Setup(100f);
                                ASFACurvaL2.Start();
                            }
                        }
                        else
                        {
                            TriggerSoundInfo1();
                            ASFAControlL3 = true;
                        }
                        break;
                    case ASFAFreq.L3:
                        TriggerSoundInfo1();
                        ASFARec.Start();
                        break;
                    case ASFAFreq.L4:
                        TriggerSoundInfo1();
                        ASFAControlPNDesprotegido = false;
                        ASFAPNVelocidadBajada = false;
                        ASFADistanciaPN.Stop();
                        break;
                    case ASFAFreq.L1:
                        if (!ASFAControlL1 && ASFAVControl > ASFAVCFAnPar)
                        {
                            ASFAControlL1 = true;
                            ASFACurvaL1.Setup(100f);
                            ASFACurvaL1.Start();
                        }
                        TriggerSoundPenalty1();
                        ASFARec.Start();
                        break;
                    case ASFAFreq.L5:
                        ASFAControlL5 = true;
                        ASFACurvaL1.Setup(100f);
                        ASFACurvaL1.Start();
                        TriggerSoundPenalty1();
                        ASFARec.Start();
                        break;
                    case ASFAFreq.L6:
                        ASFAControlL6 = true;
                        ASFACurvaL1.Setup(100f);
                        ASFACurvaL1.Start();
                        TriggerSoundPenalty1();
                        ASFARec.Start();
                        break;
                    case ASFAFreq.L9:
                        TriggerSoundPenalty1();
                        ASFARec.Start();
                        break;
                    case ASFAFreq.L7:
                        BalizaSenal();
                        ASFAUltimaInfoRecibida = ASFAInfo.Previa_Rojo;
                        ASFACurvaL7.Start();
                        ASFAControlL7 = true;
                        TriggerSoundPenalty1();
                        break;
                    case ASFAFreq.L8:
                        BalizaSenal();
                        ASFAUltimaInfoRecibida = ASFAInfo.Senal_Rojo;
                        ASFACurvaL8.Start();
                        ASFAControlL8 = true;
                        ASFAAumentoVelocidadL8 = false;
                        ASFALiberacionRojo.Stop();
                        if (ASFARebaseAuto)
                        {
                            ASFAUltimaInfoRecibida = ASFAInfo.Rebase_Autorizado;
                        }
                        else ASFAUrgencia = true;
                        TriggerSoundPenalty1();
                        break;
                    default:
                        ASFAAlarma = true;
                        break;
                }
            }
            ASFAVControl = 0f;
            ASFAVControlFinal = 0f;
            ASFAVIntervencion = 0f;
            if (ASFARec.Started)
            {
                switch (ASFAUltimaFrecuencia)
                {
                    case ASFAFreq.L1:
                        if (ASFARec.Triggered)
                        {
                            ASFAUrgencia = true;
                            BalizaSenal();
                            ASFAUltimaInfoRecibida = ASFAInfo.Anuncio_Parada;
                            ASFAControlL1 = true;
                            ASFACurvaL1.Setup(ASFACurva.RemainingValue);
                            ASFACurvaL1.Start();
                            ASFARec.Stop();
                        }
                        else if (ASFAAnParPressed)
                        {
                            BalizaSenal();
                            ASFAUltimaInfoRecibida = ASFAInfo.Anuncio_Parada;
                            ASFAControlL1 = true;
                            ASFACurvaL1.Setup(ASFACurva.RemainingValue);
                            ASFACurvaL1.Start();
                            TriggerSoundAlert1();
                            ASFARec.Stop();
                        }
                        else if (ASFAAnPrePressed)
                        {
                            BalizaSenal();
                            ASFAUltimaInfoRecibida = ASFAInfo.Anuncio_Precaucion;
                            ASFAAumentoVelocidadDesvio = ASFAAnteriorInfo == ASFAInfo.Anuncio_Precaucion && ASFAAumentoVelocidadL6;
                            ASFAAumentoVelocidadL6 = false;
                            ASFAControlL6 = true;
                            ASFACurvaL1.Setup(ASFACurva.RemainingValue);
                            ASFACurvaL1.Start();
                            TriggerSoundAlert2();
                            ASFARec.Stop();
                        }
                        else if (ASFAPrePar_VLCondPressed)
                        {
                            BalizaSenal();
                            ASFAUltimaInfoRecibida = ASFAInfo.Preanuncio;
                            ASFAControlL5 = true;
                            ASFACurvaL1.Setup(ASFACurva.RemainingValue);
                            ASFACurvaL1.Start();
                            //TriggerSoundPreanuncio();
                            ASFARec.Stop();
                        }
                        else if (ASFALTVPressed)
                        {
                            ASFAAumentoVelocidadLTV = false;
                            ASFAControlLTV = true;
                            ASFAControlL1 = ASFAUltimaInfoRecibida == ASFAInfo.Anuncio_Parada && ASFAControlL1;
                            ASFALTVCumplida = false;
                            ASFACurvaLTV.Setup(ASFACurva.RemainingValue);
                            ASFACurvaLTV.Start();
                            TriggerSoundInfo2();
                            ASFARec.Stop();
                        }
                        else if (ASFAPaNPressed)
                        {
                            ASFAControlPNDesprotegido = true;
                            ASFAControlL1 = ASFAUltimaInfoRecibida == ASFAInfo.Anuncio_Parada && ASFAControlL1;
                            ASFACurvaPN.Setup(ASFACurva.RemainingValue);
                            ASFACurvaPN.Start();
                            ASFADistanciaPN.Setup(ASFADistancia.RemainingValue - 200f);
                            ASFADistanciaPN.Start();
                            //TriggerSoundPN();
                            TriggerSoundInfo2();
                            ASFARec.Stop();
                        }
                        break;
                    case ASFAFreq.L2:
                        if (ASFARec.Triggered)
                        {
                            ASFAUrgencia = true;
                            ASFAControlL2 = true;
                            ASFARec.Stop();
                        }
                        else if (ASFAPrePar_VLCondPressed)
                        {
                            ASFAUltimaInfoRecibida = ASFAInfo.Via_Libre_Cond;
                            ASFAControlL2 = true;
                            TriggerSoundAlert1();
                            ASFARec.Stop();
                        }
                        break;
                    case ASFAFreq.L3:
                        if (ASFARec.Triggered)
                        {
                            BalizaSenal();
                            ASFAUltimaInfoRecibida = ASFAInfo.Via_Libre;
                            ASFAControlL3 = true;
                            ASFARec.Stop();
                        }
                        else if (ASFAPaNPressed)
                        {
                            ASFAControlPNDesprotegido = false;
                            ASFADistanciaPN.Stop();
                            ASFAPNVelocidadBajada = false;
                            TriggerSoundInfo1();
                            ASFARec.Stop();
                        }
                        break;
                    case ASFAFreq.L5:
                        if (ASFARec.Triggered)
                        {
                            BalizaSenal();
                            ASFAUltimaInfoRecibida = ASFAInfo.Preanuncio;
                            ASFAUrgencia = true;
                            ASFAControlL5 = true;
                            ASFACurvaL1.Setup(ASFACurva.RemainingValue);
                            ASFACurvaL1.Start();
                            ASFARec.Stop();
                        }
                        else if (ASFAPrePar_VLCondPressed)
                        {
                            BalizaSenal();
                            ASFAUltimaInfoRecibida = ASFAInfo.Preanuncio;
                            ASFAControlL5 = true;
                            ASFACurvaL1.Setup(ASFACurva.RemainingValue);
                            ASFACurvaL1.Start();
                            //TriggerSoundPreanuncio();
                            ASFARec.Stop();
                        }
                        break;
                    case ASFAFreq.L6:
                        if (ASFARec.Triggered)
                        {
                            BalizaSenal();
                            ASFAUltimaInfoRecibida = ASFAInfo.Anuncio_Precaucion;
                            ASFAUrgencia = true;
                            ASFAControlL6 = true;
                            ASFACurvaL1.Setup(ASFACurva.RemainingValue);
                            ASFACurvaL1.Start();
                            ASFARec.Stop();
                        }
                        else if (ASFAAnPrePressed)
                        {
                            BalizaSenal();
                            ASFAUltimaInfoRecibida = ASFAInfo.Anuncio_Precaucion;
                            ASFAAumentoVelocidadDesvio = ASFAAnteriorInfo == ASFAInfo.Anuncio_Precaucion && ASFAAumentoVelocidadL6;
                            ASFAControlL6 = true;
                            ASFACurvaL1.Setup(ASFACurva.RemainingValue);
                            ASFACurvaL1.Start();
                            TriggerSoundAlert1();
                            ASFARec.Stop();
                        }
                        break;
                    case ASFAFreq.L9:
                        if (ASFARec.Triggered)
                        {
                            ASFAUrgencia = true;
                            ASFAControlPNDesprotegido = true;
                            ASFACurvaPN.Setup(ASFACurva.RemainingValue);
                            ASFACurvaPN.Start();
                            ASFADistanciaPN.Setup(ASFADistancia.RemainingValue - 200f);
                            ASFADistanciaPN.Start();
                            ASFARec.Stop();
                        }
                        else if (ASFALTVPressed)
                        {
                            ASFALTVCumplida = false;
                            ASFAAumentoVelocidadLTV = false;
                            ASFAControlLTV = true;
                            ASFACurvaLTV.Setup(ASFACurva.RemainingValue);
                            ASFACurvaLTV.Start();
                            TriggerSoundInfo2();
                            ASFARec.Stop();
                        }
                        else if (ASFAPaNPressed)
                        {
                            ASFAControlPNDesprotegido = true;
                            ASFACurvaPN.Setup(ASFACurva.RemainingValue);
                            ASFACurvaPN.Start();
                            ASFADistanciaPN.Setup(ASFADistancia.RemainingValue - 200f);
                            ASFADistanciaPN.Start();
                            //TriggerSoundPN();
                            ASFARec.Stop();
                        }
                        break;
                }
                if (!ASFARec.Started)
                {
                    ASFAPressed = false;
                    BotonesASFA();
                }
            }
            if (ASFARebasePressed && !ASFATiempoRebase.Started)
            {
                ASFARebaseAuto = true;
                TriggerSoundPenalty2();
                ASFATiempoRebase.Start();
                SetNextSignalAspect(Aspect.StopAndProceed);
            }
            if (!ASFARebasePressed) ASFATiempoRebase.Stop();
            if ((ASFATiempoRebase.Triggered || !ASFATiempoRebase.Started) && ASFARebaseAuto) ASFARebaseAuto = false;
            if ((ASFAUltimaInfoRecibida != ASFAInfo.Senal_Rojo && ASFAUltimaInfoRecibida != ASFAInfo.Rebase_Autorizado) && (ASFAAnteriorInfo == ASFAInfo.Senal_Rojo || ASFAAnteriorInfo == ASFAInfo.Rebase_Autorizado) && ASFABalizaSenal && !ASFALiberacionRojo.Started) ASFALiberacionRojo.Start();
            if (ASFAUltimaInfoRecibida == ASFAInfo.Senal_Rojo || ASFAUltimaInfoRecibida == ASFAInfo.Previa_Rojo) ASFALiberacionRojo.Stop();
            if (ASFAAnteriorInfo == ASFAInfo.Anuncio_Precaucion)
            {
                if (ASFABalizaSenal) ASFATiempoDesvio.Start();
                ASFAControlDesvio = true;
                ASFACurvaL1.Setup(0f);
                ASFACurvaL1.Start();
            }
            if (ASFAAnteriorInfo != ASFAInfo.Anuncio_Precaucion)
            {
                if (ASFATiempoDesvio.Triggered && ASFAControlDesvio) ASFAAumentoVelocidadDesvio = false;
                ASFATiempoDesvio.Stop();
                ASFAControlDesvio = false;
            }
            if (ASFATiempoDesvio.Triggered) ASFAControlDesvio = false;
            if (ASFABalizaSenal) ASFAControlArranque = false;
            if (ASFAUltimaInfoRecibida != ASFAInfo.Anuncio_Parada && ASFABalizaSenal) ASFAControlL1 = false;
            if (ASFABalizaSenal) ASFAAumentoVelocidadL5 = ASFAAumentoVelocidadL6 = false;
            if (ASFAUltimaInfoRecibida != ASFAInfo.Preanuncio_AV && ASFAUltimaInfoRecibida != ASFAInfo.Preanuncio && ASFABalizaSenal)
            {
                ASFAControlL5 = false;
                ASFAAumentoVelocidadL5 = false;
            }
            if (ASFAUltimaInfoRecibida != ASFAInfo.Anuncio_Precaucion && ASFABalizaSenal)
            {
                ASFAControlL6 = false;
                ASFAAumentoVelocidadL6 = false;
            }
            if (ASFAUltimaInfoRecibida != ASFAInfo.Previa_Rojo && ASFABalizaSenal) ASFAControlL7 = false;
            if (ASFALiberacionRojo.Triggered)
            {
                ASFALiberacionRojo.Stop();
                ASFAControlL8 = false;
                ASFAAumentoVelocidadL8 = false;
            }
            if (ASFAUltimaInfoRecibida != ASFAInfo.Via_Libre_Cond && ASFABalizaSenal) ASFAControlL2 = false;
            if (ASFAUltimaInfoRecibida != ASFAInfo.Via_Libre && ASFAUltimaInfoRecibida != ASFAInfo.Via_Libre_Cond && ASFABalizaSenal) ASFAControlL3 = false;
            if (ASFADistanciaPN.Triggered)
            {
                ASFAControlPNDesprotegido = false;
                ASFAPNVelocidadBajada = false;
                ASFADistanciaPN.Stop();
            }
            if (ASFAControlArranque)
            {
                ASFAVControl = ASFAVCControlArranque;
                ASFAVIntervencion = ASFAVCControlArranque + MpS.FromKpH(5f);
                ASFAVControlFinal = ASFAVControl;
            }
            if (ASFAControlL3)
            {
                ASFAVControl = Math.Min(TrainMaxSpeed, ASFATipoTren);
                ASFAVIntervencion = Math.Min(TrainMaxSpeed, ASFATipoTren) + MpS.FromKpH(5f);
                ASFAVControlFinal = ASFAVControl;
            }
            if (ASFAControlL2)
            {
                ASFAVControl = Math.Max(Math.Min(ASFAVCIVlCond, ASFAVCIVlCond - ASFAVCVlCondDec * (100f - ASFAVCVlCondTReac - ASFACurvaL2.RemainingValue)), ASFAVCFVlCond);
                ASFAVIntervencion = Math.Max(Math.Min(ASFAVCIVlCond + MpS.FromKpH(3f), (ASFAVCIVlCond + MpS.FromKpH(3f)) - ASFAVIVlCondDec * (100f - ASFAVIVlCondTReac - ASFACurvaL2.RemainingValue)), ASFAVCFVlCond + MpS.FromKpH(3f));
                ASFAVControlFinal = ASFAVCFVlCond;
            }
            if (ASFAControlL5)
            {
                if (ASFAAumVelPressed && ASFACurvaL1.RemainingValue > 90f)
                {
                    ASFAAumentoVelocidadL5 = true;
                    ASFAUltimaInfoRecibida = ASFAInfo.Preanuncio_AV;
                }
                if (ASFAAumentoVelocidadL5)
                {
                    ASFAVControl = Math.Max(Math.Min(ASFAVCIAnPar, ASFAVCIAnPar - ASFAVCAnParDec * (100f - ASFAVCAnParTReac - ASFACurvaL1.RemainingValue)), ASFAVCFPreParAumVel);
                    ASFAVIntervencion = Math.Max(Math.Min(ASFAVCIAnPar + MpS.FromKpH(3f), (ASFAVCIAnPar + MpS.FromKpH(3f)) - ASFAVIAnParDec * (100f - ASFAVIAnParTReac - ASFACurvaL1.RemainingValue)), ASFAVCFPreParAumVel + MpS.FromKpH(3f));
                    ASFAVControlFinal = ASFAVCFPreParAumVel;
                }
                else
                {
                    ASFAVControl = Math.Max(Math.Min(ASFAVCIAnPar, ASFAVCIAnPar - ASFAVCAnParDec * (100f - ASFAVCAnParTReac - ASFACurvaL1.RemainingValue)), ASFAVCFPrePar);
                    ASFAVIntervencion = Math.Max(Math.Min(ASFAVCIAnPar + MpS.FromKpH(3f), (ASFAVCIAnPar + MpS.FromKpH(3f)) - ASFAVIAnParDec * (100f - ASFAVIAnParTReac - ASFACurvaL1.RemainingValue)), ASFAVCFPrePar + MpS.FromKpH(3f));
                    ASFAVControlFinal = ASFAVCFPrePar;
                }
            }
            if (ASFAControlL6)
            {
                if (ASFAAumVelPressed && ASFACurvaL1.RemainingValue > 90f) ASFAAumentoVelocidadL6 = ASFAAumentoVelocidadDesvio = true;
                if (ASFAAumentoVelocidadL6)
                {
                    ASFAVControl = Math.Max(Math.Min(ASFAVCIAnPar, ASFAVCIAnPar - ASFAVCAnParDec * (100f - ASFAVCAnParTReac - ASFACurvaL1.RemainingValue)), ASFAVCFAnPreAumVel);
                    ASFAVIntervencion = Math.Max(Math.Min(ASFAVCIAnPar + MpS.FromKpH(3f), (ASFAVCIAnPar + MpS.FromKpH(3f)) - ASFAVIAnParDec * (100f - ASFAVIAnParTReac - ASFACurvaL1.RemainingValue)), ASFAVCFAnPreAumVel + MpS.FromKpH(3f));
                    ASFAVControlFinal = ASFAVCFAnPreAumVel;
                }
                else
                {
                    ASFAVControl = Math.Max(Math.Min(ASFAVCIAnPar, ASFAVCIAnPar - ASFAVCAnParDec * (100f - ASFAVCAnParTReac - ASFACurvaL1.RemainingValue)), ASFAVCFAnPre);
                    ASFAVIntervencion = Math.Max(Math.Min(ASFAVCIAnPar + MpS.FromKpH(3f), (ASFAVCIAnPar + MpS.FromKpH(3f)) - ASFAVIAnParDec * (100f - ASFAVIAnParTReac - ASFACurvaL1.RemainingValue)), ASFAVCFAnPre + MpS.FromKpH(3f));
                    ASFAVControlFinal = ASFAVCFAnPre;
                }
            }
            if (ASFAControlL1)
            {
                ASFAVControl = Math.Max(Math.Min(ASFAVCIAnPar, ASFAVCIAnPar - ASFAVCAnParDec * (100f - ASFAVCAnParTReac - ASFACurvaL1.RemainingValue)), ASFAVCFAnPar);
                ASFAVIntervencion = Math.Max(Math.Min(ASFAVCIAnPar + MpS.FromKpH(3f), (ASFAVCIAnPar + MpS.FromKpH(3f)) - ASFAVIAnParDec * (100f - ASFAVIAnParTReac - ASFACurvaL1.RemainingValue)), ASFAVCFAnPar + MpS.FromKpH(3f));
                ASFAVControlFinal = ASFAVCFAnPar;
                if (ASFAAnteriorInfo == ASFAInfo.Anuncio_Parada && ASFAUltimaInfoRecibida == ASFAInfo.Anuncio_Parada)
                {
                    if (ASFABalizaSenal) ASFATiempoAA.Start();
                    if (ASFATiempoAA.Started && !ASFATiempoAA.Triggered)
                    {
                        ASFAVControl = ASFAVControlFinal = ASFAVCDesv;
                        ASFAVIntervencion = ASFAVCDesv + MpS.FromKpH(3f);
                        ASFACurvaL1.Setup(0f);
                        ASFACurvaL1.Start();
                        SetNextSignalAspect(Aspect.Approach_3);
                    }
                }
                if (ASFAAnteriorInfo == ASFAInfo.Preanuncio)
                {
                    ASFAVControl = Math.Max(Math.Min(ASFAVCISecPreParA, ASFAVCISecPreParA - ASFAVCSecPreParADec * (100f - ASFAVCSecPreParATReac - ASFACurvaL1.RemainingValue)), ASFAVCFSecPreParA);
                    ASFAVIntervencion = Math.Max(Math.Min(ASFAVCISecPreParA + MpS.FromKpH(3f), (ASFAVCISecPreParA + MpS.FromKpH(3f)) - ASFAVCSecPreParADec * (100f - ASFAVCSecPreParATReac - ASFACurvaL1.RemainingValue)), ASFAVCFSecPreParA + MpS.FromKpH(3f));
                    ASFAVControlFinal = ASFAVCFSecPreParA;
                    SetNextSignalAspect(Aspect.Approach_3);
                }
                if (ASFAAnteriorInfo == ASFAInfo.Preanuncio_AV)
                {
                    if (ASFANumeroBaliza == 1)
                    {
                        ASFAVControl = Math.Max(Math.Min(ASFAVCISecPreParA1AumVel, ASFAVCISecPreParA1AumVel - ASFAVCSecPreParADec * (100f - ASFAVCSecPreParA1AumVelTReac - ASFACurvaL1.RemainingValue)), ASFAVCFSecPreParA1AumVel);
                        ASFAVIntervencion = Math.Max(Math.Min(ASFAVCISecPreParA1AumVel + MpS.FromKpH(3f), (ASFAVCISecPreParA1AumVel + MpS.FromKpH(3f)) - ASFAVCSecPreParADec * (100f - ASFAVCSecPreParA1AumVelTReac - ASFACurvaL1.RemainingValue)), ASFAVCFSecPreParA1AumVel + MpS.FromKpH(3f));
                        ASFAVControlFinal = ASFAVCFSecPreParA1AumVel;
                    }
                    else
                    {
                        ASFAVControl = Math.Max(Math.Min(ASFAVCISecPreParA2AumVel, ASFAVCISecPreParA2AumVel - ASFAVCSecPreParADec * (100f - ASFAVCSecPreParA2AumVelTReac - ASFACurvaL1.RemainingValue)), ASFAVCFSecPreParA2AumVel);
                        ASFAVIntervencion = Math.Max(Math.Min(ASFAVCISecPreParA2AumVel + MpS.FromKpH(3f), (ASFAVCISecPreParA2AumVel + MpS.FromKpH(3f)) - ASFAVCSecPreParADec * (100f - ASFAVCSecPreParA2AumVelTReac - ASFACurvaL1.RemainingValue)), ASFAVCFSecPreParA2AumVel + MpS.FromKpH(3f));
                        ASFAVControlFinal = ASFAVCFSecPreParA2AumVel;
                    }
                    SetNextSignalAspect(Aspect.Approach_3);
                }
            }
            if (ASFAControlDesvio)
            {
                if (ASFAAumentoVelocidadDesvio)
                {
                    ASFAVControl = ASFAVCDesvAumVel;
                    ASFAVIntervencion = ASFAVCDesvAumVel + MpS.FromKpH(3f);
                    ASFAVControlFinal = ASFAVCDesvAumVel;
                }
                else
                {
                    ASFAVControl = ASFAVCDesv;
                    ASFAVIntervencion = ASFAVCDesv + MpS.FromKpH(3f);
                    ASFAVControlFinal = ASFAVCDesv;
                }
                SetNextSignalAspect(Aspect.Restricted);
            }
            if (ASFAControlL8)
            {
                if (ASFAAumVelPressed && ASFACurvaL8.RemainingValue > 90f) ASFAAumentoVelocidadL8 = true;
                if (ASFAAumentoVelocidadL8)
                {
                    ASFAVControl = ASFAVCParAumVel;
                    ASFAVIntervencion = ASFAVCParAumVel + MpS.FromKpH(3);
                    ASFAVControlFinal = ASFAVControl;
                }
                else
                {
                    ASFAVControl = ASFAVCPar;
                    ASFAVIntervencion = ASFAVCPar + MpS.FromKpH(3);
                    ASFAVControlFinal = ASFAVControl;
                }
                if (ASFAUltimaInfoRecibida != ASFAInfo.Senal_Rojo && ASFAUltimaInfoRecibida != ASFAInfo.Rebase_Autorizado) SetNextSignalAspect(ASFASenales[ASFAAnteriorInfo]);
                else SetNextSignalAspect(ASFASenales[ASFAUltimaInfoRecibida]);
            }
            if (ASFAControlL7)
            {
                ASFAVControl = Math.Max(Math.Min(ASFAVCIPar, ASFAVCIPar - ASFAVCParDec * (100f - ASFAVCParTReac - ASFACurvaL7.RemainingValue)), ASFAVCFPar);
                ASFAVIntervencion = Math.Max(Math.Min(ASFAVCIPar + MpS.FromKpH(3f), (ASFAVCIPar + MpS.FromKpH(3f)) - ASFAVIParDec * (100f - ASFAVIParTReac - ASFACurvaL7.RemainingValue)), ASFAVCFPar + MpS.FromKpH(3f));
                ASFAVControlFinal = 0f;
                if (ASFANumeroBaliza == 2)
                {
                    ASFAVControl = Math.Max(Math.Min(ASFAVCFPar, ASFAVCFPar - ASFAVCParDec * (100f - ASFAVCParTReac - ASFACurvaL7.RemainingValue)), ASFAVCFPar / 2f);
                    ASFAVIntervencion = Math.Max(Math.Min(ASFAVCFPar + MpS.FromKpH(3f), (ASFAVCFPar + MpS.FromKpH(3f)) - ASFAVIParDec * (100f - ASFAVIParTReac - ASFACurvaL7.RemainingValue)), ASFAVCFPar / 2f + MpS.FromKpH(3f));
                }
                if (ASFADistanciaPrevia.Triggered)
                {
                    ASFAUrgencia = true;
                    ASFAControlL7 = false;
                    ASFAControlL1 = true;
                    ASFACurvaL1.Setup(0f);
                    ASFACurvaL1.Start();
                    ASFAUltimaInfoRecibida = ASFAInfo.Anuncio_Parada;
                }
            }
            if (ASFAControlLTV)
            {
                if (ASFAAumVelPressed && ASFACurvaLTV.RemainingValue > 90f && !ASFAAumentoVelocidadLTV)
                {
                    ASFAPressed = false;
                    BotonesASFA();
                    ASFAAumentoVelocidadLTV = true;
                }
                if (ASFAAumentoVelocidadLTV)
                {
                    ASFAVControl = Math.Min(ASFAVControl, Math.Max(Math.Min(ASFAVCIAnPar, ASFAVCIAnPar - ASFAVCAnParDec * (100f - ASFAVCAnParTReac - ASFACurvaLTV.RemainingValue)), ASFAVCFLTVAumVel));
                    ASFAVIntervencion = Math.Min(ASFAVIntervencion, Math.Max(Math.Min(ASFAVCIAnPar + MpS.FromKpH(3f), (ASFAVCIAnPar + MpS.FromKpH(3f)) - ASFAVIAnParDec * (100f - ASFAVIAnParTReac - ASFACurvaLTV.RemainingValue)), ASFAVCFLTVAumVel + MpS.FromKpH(3f)));
                    ASFAVControlFinal = ASFAVCFLTVAumVel;
                    if (SpeedMpS() < ASFAVCFLTVAumVel) ASFALTVCumplida = true;
                    if (ASFALTVCumplida)
                    {
                        ASFAVControl = Math.Min(ASFAVControl, ASFAVCFLTVAumVel);
                        ASFAVIntervencion = Math.Min(ASFAVIntervencion, ASFAVCFLTVAumVel + MpS.FromKpH(3f));
                        if (ASFALTVPressed) ASFALTVCumplida = ASFAControlLTV = ASFAAumentoVelocidadLTV = false;
                    }
                }
                else
                {
                    ASFAVControl = Math.Min(ASFAVControl, Math.Max(Math.Min(ASFAVCIAnPar, ASFAVCIAnPar - ASFAVCAnParDec * (100f - ASFAVCAnParTReac - ASFACurvaLTV.RemainingValue)), ASFAVCFLTV));
                    ASFAVIntervencion = Math.Min(ASFAVIntervencion, Math.Max(Math.Min(ASFAVCIAnPar + MpS.FromKpH(3f), (ASFAVCIAnPar + MpS.FromKpH(3f)) - ASFAVIAnParDec * (100f - ASFAVIAnParTReac - ASFACurvaLTV.RemainingValue)), ASFAVCFLTV + MpS.FromKpH(3f)));
                    ASFAVControlFinal = ASFAVCFLTV;
                    if (SpeedMpS() < ASFAVCFLTV) ASFALTVCumplida = true;
                    if (ASFALTVCumplida)
                    {
                        ASFAVControl = Math.Min(ASFAVControl, ASFAVCFLTV);
                        ASFAVIntervencion = Math.Min(ASFAVIntervencion, ASFAVCFLTV + MpS.FromKpH(3f));
                        if (ASFALTVPressed) ASFALTVCumplida = ASFAControlLTV = false;
                    }
                }
            }
            if (ASFAControlPNDesprotegido)
            {
                if (SpeedMpS() <= MpS.FromKpH(30f)) ASFAPNVelocidadBajada = true;
                if (ASFAPNVelocidadBajada)
                {
                    ASFAVControl = Math.Min(ASFAVControl, ASFAVCFAnPar);
                    ASFAVIntervencion = Math.Min(ASFAVIntervencion, ASFAVCFAnPar + MpS.FromKpH(3f));
                    ASFAVControlFinal = Math.Min(ASFAVControlFinal, ASFAVCFAnPar);
                }
                else
                {
                    ASFAVControl = Math.Min(ASFAVControl, Math.Max(Math.Min(ASFAVCIAnPar, ASFAVCIAnPar - ASFAVCAnParDec * (100f - ASFAVCAnParTReac - ASFACurvaPN.RemainingValue)), MpS.FromKpH(30f)));
                    ASFAVIntervencion = Math.Min(ASFAVIntervencion, Math.Max(Math.Min(ASFAVCIAnPar + MpS.FromKpH(3f), (ASFAVCIAnPar + MpS.FromKpH(3f)) - ASFAVIAnParDec * (100f - ASFAVIAnParTReac - ASFACurvaPN.RemainingValue)), MpS.FromKpH(33f)));
                    ASFAVControlFinal = Math.Min(ASFAVControlFinal, MpS.FromKpH(30f));
                }
            }
            ASFAVControl = Math.Min(ASFAVControl, TrainMaxSpeed);
            ASFAVIntervencion = Math.Min(ASFAVIntervencion, TrainMaxSpeed + MpS.FromKpH(5f));
            ASFAVControlFinal = Math.Min(ASFAVControlFinal, TrainMaxSpeed);
            //SetCurrentSpeedLimitMpS(ASFAVControl);
            SetNextSpeedLimitMpS(ASFAVControlFinal);
            SetInterventionSpeedLimitMpS(ASFAVIntervencion);
            if (!ASFAControlDesvio && !ASFARebaseAuto && !ASFAControlL8 && (!ASFATiempoAA.Started || ASFATiempoAA.Triggered)) SetNextSignalAspect(ASFASenales[ASFAUltimaInfoRecibida]);
        }
        protected void UpdateASFADigitalAVE()
        {
            ASFABalizaSenal = false;
            ASFAFrecuencia = BalizaASFA();
            ASFABalizaRecibida = ASFAFrecuencia != ASFAFreq.FP;
            if (ASFABalizaRecibida)
            {
                ASFACurva.Start();
                ASFADistancia.Start();
                ASFARec.Stop();
                ASFAAnteriorFrecuencia = ASFAUltimaFrecuencia;
                ASFAUltimaFrecuencia = ASFAFrecuencia;
                switch (ASFAFrecuencia)
                {
                    case ASFAFreq.L3:
                        TriggerSoundInfo1();
                        BalizaSenal();
                        ASFAUltimaInfoRecibida = ASFAInfo.Via_Libre;
                        ASFAControlL3 = true;
                        break;
                    case ASFAFreq.L1:
                        if (!ASFAControlL1 && ASFAVControl > ASFAVCFAnParAV)
                        {
                            ASFAControlL1 = true;
                            ASFACurvaL1.Setup(100f);
                            ASFACurvaL1.Start();
                        }
                        TriggerSoundPenalty1();
                        ASFARec.Start();
                        break;
                    case ASFAFreq.L5:
                        ASFAControlL5 = true;
                        ASFACurvaL1.Setup(100f);
                        ASFACurvaL1.Start();
                        TriggerSoundPenalty1();
                        ASFARec.Start();
                        break;
                    case ASFAFreq.L6:
                        ASFAControlL6 = true;
                        ASFACurvaL1.Setup(100f);
                        ASFACurvaL1.Start();
                        TriggerSoundPenalty1();
                        ASFARec.Start();
                        break;
                    case ASFAFreq.L9:
                        TriggerSoundPenalty1();
                        ASFAControlLTV = true;
                        ASFACurvaLTV.Setup(100f);
                        ASFACurvaLTV.Start();
                        ASFARec.Start();
                        break;
                    case ASFAFreq.L7:
                        BalizaSenal();
                        ASFAUltimaInfoRecibida = ASFAInfo.Previa_Rojo;
                        ASFACurvaL7.Start();
                        ASFAControlL7 = true;
                        TriggerSoundPenalty1();
                        break;
                    case ASFAFreq.L8:
                        BalizaSenal();
                        ASFAUltimaInfoRecibida = ASFAInfo.Senal_Rojo;
                        ASFACurvaL8.Start();
                        ASFAControlL8 = true;
                        ASFAAumentoVelocidadL8 = false;
                        ASFALiberacionRojo.Stop();
                        if (ASFARebaseAuto)
                        {
                            ASFAUltimaInfoRecibida = ASFAInfo.Rebase_Autorizado;
                        }
                        else ASFAUrgencia = true;
                        TriggerSoundPenalty1();
                        break;
                    default:
                        ASFAAlarma = true;
                        break;
                }
            }
            if (ASFARec.Started)
            {
                switch (ASFAUltimaFrecuencia)
                {
                    case ASFAFreq.L1:
                        if (ASFARec.Triggered)
                        {
                            ASFAUrgencia = true;
                            BalizaSenal();
                            ASFAUltimaInfoRecibida = ASFAInfo.Anuncio_Parada;
                            ASFAControlL1 = true;
                            ASFACurvaL1.Setup(ASFACurva.RemainingValue);
                            ASFACurvaL1.Start();
                            ASFARec.Stop();
                        }
                        else if (ASFAAnParPressed)
                        {
                            BalizaSenal();
                            ASFAUltimaInfoRecibida = ASFAInfo.Anuncio_Parada;
                            ASFAControlL1 = true;
                            ASFACurvaL1.Setup(ASFACurva.RemainingValue);
                            ASFACurvaL1.Start();
                            TriggerSoundAlert1();
                            ASFARec.Stop();
                        }
                        else if (ASFAAnPrePressed)
                        {
                            BalizaSenal();
                            ASFAUltimaInfoRecibida = ASFAInfo.Anuncio_Precaucion;
                            ASFAAumentoVelocidadDesvio = ASFAAnteriorInfo == ASFAInfo.Anuncio_Precaucion && ASFAAumentoVelocidadL6;
                            ASFAControlL6 = true;
                            ASFACurvaL1.Setup(ASFACurva.RemainingValue);
                            ASFACurvaL1.Start();
                            TriggerSoundAlert2();
                            ASFARec.Stop();
                        }
                        else if (ASFAPrePar_VLCondPressed)
                        {
                            BalizaSenal();
                            ASFAUltimaInfoRecibida = ASFAInfo.Preanuncio;
                            ASFAControlL5 = true;
                            ASFACurvaL1.Setup(ASFACurva.RemainingValue);
                            ASFACurvaL1.Start();
                            //TriggerSoundPreanuncio();
                            ASFARec.Stop();
                        }
                        else if (ASFALTVPressed)
                        {
                            ASFAAumentoVelocidadLTV = false;
                            ASFAControlLTV = true;
                            ASFAControlL1 = ASFAUltimaInfoRecibida == ASFAInfo.Anuncio_Parada && ASFAControlL1;
                            ASFALTVCumplida = false;
                            ASFACurvaLTV.Setup(ASFACurva.RemainingValue);
                            ASFACurvaLTV.Start();
                            TriggerSoundInfo2();
                            ASFARec.Stop();
                        }
                        break;
                    case ASFAFreq.L5:
                        if (ASFARec.Triggered)
                        {
                            BalizaSenal();
                            ASFAUltimaInfoRecibida = ASFAInfo.Preanuncio;
                            ASFAUrgencia = true;
                            ASFAControlL5 = true;
                            ASFACurvaL1.Setup(ASFACurva.RemainingValue);
                            ASFACurvaL1.Start();
                            ASFARec.Stop();
                        }
                        else if (ASFAPrePar_VLCondPressed)
                        {
                            BalizaSenal();
                            ASFAUltimaInfoRecibida = ASFAInfo.Preanuncio;
                            ASFAControlL5 = true;
                            ASFACurvaL1.Setup(ASFACurva.RemainingValue);
                            ASFACurvaL1.Start();
                            //TriggerSoundPreanuncio();
                            ASFARec.Stop();
                        }
                        break;
                    case ASFAFreq.L6:
                        if (ASFARec.Triggered)
                        {
                            BalizaSenal();
                            ASFAUltimaInfoRecibida = ASFAInfo.Anuncio_Precaucion;
                            ASFAUrgencia = true;
                            ASFAControlL6 = true;
                            ASFACurvaL1.Setup(ASFACurva.RemainingValue);
                            ASFACurvaL1.Start();
                            ASFARec.Stop();
                        }
                        else if (ASFAAnPrePressed)
                        {
                            BalizaSenal();
                            ASFAUltimaInfoRecibida = ASFAInfo.Anuncio_Precaucion;
                            ASFAAumentoVelocidadDesvio = ASFAAnteriorInfo == ASFAInfo.Anuncio_Precaucion && ASFAAumentoVelocidadL6;
                            ASFAControlL6 = true;
                            ASFACurvaL1.Setup(ASFACurva.RemainingValue);
                            ASFACurvaL1.Start();
                            TriggerSoundAlert1();
                            ASFARec.Stop();
                        }
                        break;
                    case ASFAFreq.L9:
                        if (ASFARec.Triggered)
                        {
                            ASFAUrgencia = true;
                            ASFARec.Stop();
                        }
                        else if (ASFALTVPressed)
                        {
                            TriggerSoundInfo2();
                            ASFARec.Stop();
                        }
                        break;
                }
                if (!ASFARec.Started)
                {
                    ASFAPressed = false;
                    BotonesASFA();
                }
            }
            if (ASFARebasePressed && !ASFATiempoRebase.Started)
            {
                ASFARebaseAuto = true;
                TriggerSoundPenalty2();
                ASFATiempoRebase.Start();
                SetNextSignalAspect(Aspect.StopAndProceed);
            }
            if (!ASFARebasePressed) ASFATiempoRebase.Stop();
            if ((ASFATiempoRebase.Triggered || !ASFATiempoRebase.Started) && ASFARebaseAuto) ASFARebaseAuto = false;
            if ((ASFAUltimaInfoRecibida != ASFAInfo.Senal_Rojo && ASFAUltimaInfoRecibida != ASFAInfo.Rebase_Autorizado) && (ASFAAnteriorInfo == ASFAInfo.Senal_Rojo || ASFAAnteriorInfo == ASFAInfo.Rebase_Autorizado) && ASFABalizaSenal && !ASFALiberacionRojo.Started) ASFALiberacionRojo.Start();
            if (ASFAUltimaInfoRecibida == ASFAInfo.Senal_Rojo || ASFAUltimaInfoRecibida == ASFAInfo.Previa_Rojo) ASFALiberacionRojo.Stop();
            if (ASFAAnteriorInfo == ASFAInfo.Anuncio_Precaucion)
            {
                if (ASFABalizaSenal) ASFATiempoDesvio.Start();
                ASFAControlDesvio = true;
                ASFACurvaL1.Setup(0f);
                ASFACurvaL1.Start();
            }
            if (ASFAAnteriorInfo != ASFAInfo.Anuncio_Precaucion)
            {
                if (ASFATiempoDesvio.Triggered && ASFAControlDesvio) ASFAAumentoVelocidadDesvio = false;
                ASFATiempoDesvio.Stop();
                ASFAControlDesvio = false;
            }
            if (ASFATiempoDesvio.Triggered) ASFAControlDesvio = false;
            if (ASFABalizaSenal) ASFAControlArranque = false;
            if (ASFAUltimaInfoRecibida != ASFAInfo.Anuncio_Parada && ASFABalizaSenal) ASFAControlL1 = false;
            if (ASFABalizaSenal) ASFAAumentoVelocidadL5 = ASFAAumentoVelocidadL6 = false;
            if (ASFAUltimaInfoRecibida != ASFAInfo.Preanuncio_AV && ASFAUltimaInfoRecibida != ASFAInfo.Preanuncio && ASFABalizaSenal)
            {
                ASFAControlL5 = false;
                ASFAAumentoVelocidadL5 = false;
            }
            if (ASFAUltimaInfoRecibida != ASFAInfo.Anuncio_Precaucion && ASFABalizaSenal)
            {
                ASFAControlL6 = false;
                ASFAAumentoVelocidadL6 = false;
            }
            if (ASFAUltimaInfoRecibida != ASFAInfo.Previa_Rojo && ASFABalizaSenal) ASFAControlL7 = false;
            if (ASFALiberacionRojo.Triggered)
            {
                ASFALiberacionRojo.Stop();
                ASFAControlL8 = false;
                ASFAAumentoVelocidadL8 = false;
            }
            if (ASFAUltimaInfoRecibida != ASFAInfo.Via_Libre && ASFABalizaSenal) ASFAControlL3 = false;
            if (ASFAControlArranque)
            {
                ASFAVControl = ASFAVCControlArranque;
                ASFAVIntervencion = ASFAVCControlArranque + MpS.FromKpH(5f);
                ASFAVControlFinal = ASFAVControl;
            }
            if (ASFAControlL3)
            {
                ASFAVControl = Math.Min(TrainMaxSpeed, ASFATipoTren);
                ASFAVIntervencion = Math.Min(TrainMaxSpeed, ASFATipoTren) + MpS.FromKpH(5f);
                ASFAVControlFinal = ASFAVControl;
            }
            if (ASFAControlL5)
            {
                if (ASFAAumVelPressed && ASFACurvaL1.RemainingValue > 90f)
                {
                    ASFAAumentoVelocidadL5 = true;
                    ASFAUltimaInfoRecibida = ASFAInfo.Preanuncio_AV;
                }
                if (ASFAAumentoVelocidadL5)
                {
                    ASFAVControl = Math.Max(Math.Min(ASFATipoTren, ASFATipoTren - ASFAVCAnParDec * (100f - ASFAVCAnParTReac - ASFACurvaL1.RemainingValue)), ASFAVCFPreParAumVelAV);
                    ASFAVIntervencion = Math.Max(Math.Min(ASFATipoTren + MpS.FromKpH(5f), (ASFATipoTren + MpS.FromKpH(5f)) - ASFAVIAnParDec * (100f - ASFAVIAnParTReac - ASFACurvaL1.RemainingValue)), ASFAVCFPreParAumVelAV + MpS.FromKpH(3f));
                    ASFAVControlFinal = ASFAVCFPreParAumVelAV;
                }
                else
                {
                    ASFAVControl = Math.Max(Math.Min(ASFATipoTren, ASFATipoTren - ASFAVCAnParDec * (100f - ASFAVCAnParTReac - ASFACurvaL1.RemainingValue)), ASFAVCFPreParAV);
                    ASFAVIntervencion = Math.Max(Math.Min(ASFATipoTren + MpS.FromKpH(5f), (ASFATipoTren + MpS.FromKpH(5f)) - ASFAVIAnParDec * (100f - ASFAVIAnParTReac - ASFACurvaL1.RemainingValue)), ASFAVCFPreParAV + MpS.FromKpH(3f));
                    ASFAVControlFinal = ASFAVCFPreParAV;
                }
            }
            if (ASFAControlL6)
            {
                if (ASFAAumVelPressed && ASFACurvaL1.RemainingValue > 90f) ASFAAumentoVelocidadL6 = ASFAAumentoVelocidadDesvio = true;
                if (ASFAAumentoVelocidadL6)
                {
                    ASFAVControl = Math.Max(Math.Min(ASFATipoTren, ASFATipoTren - ASFAVCAnParDec * (100f - ASFAVCAnParTReac - ASFACurvaL1.RemainingValue)), ASFAVCFAnPreAumVelAV);
                    ASFAVIntervencion = Math.Max(Math.Min(ASFATipoTren + MpS.FromKpH(3f), (ASFATipoTren + MpS.FromKpH(3f)) - ASFAVIAnParDec * (100f - ASFAVIAnParTReac - ASFACurvaL1.RemainingValue)), ASFAVCFAnPreAumVelAV + MpS.FromKpH(3f));
                    ASFAVControlFinal = ASFAVCFAnPreAumVelAV;
                }
                else
                {
                    ASFAVControl = Math.Max(Math.Min(ASFATipoTren, ASFATipoTren - ASFAVCAnParDec * (100f - ASFAVCAnParTReac - ASFACurvaL1.RemainingValue)), ASFAVCFAnPreAV);
                    ASFAVIntervencion = Math.Max(Math.Min(ASFATipoTren + MpS.FromKpH(3f), (ASFATipoTren + MpS.FromKpH(3f)) - ASFAVIAnParDec * (100f - ASFAVIAnParTReac - ASFACurvaL1.RemainingValue)), ASFAVCFAnPreAV + MpS.FromKpH(3f));
                    ASFAVControlFinal = ASFAVCFAnPreAV;
                }
            }
            if (ASFAControlL1)
            {
                ASFAVControl = Math.Max(Math.Min(ASFATipoTren, ASFATipoTren - ASFAVCAnParDec * (100f - ASFAVCAnParTReac - ASFACurvaL1.RemainingValue)), ASFAVCFAnParAV);
                ASFAVIntervencion = Math.Max(Math.Min(ASFATipoTren + MpS.FromKpH(5f), (ASFATipoTren + MpS.FromKpH(5f)) - ASFAVIAnParDec * (100f - ASFAVIAnParTReac - ASFACurvaL1.RemainingValue)), ASFAVCFAnParAV + MpS.FromKpH(3f));
                ASFAVControlFinal = ASFAVCFAnParAV;
                if (ASFAAnteriorInfo == ASFAInfo.Anuncio_Parada && ASFAUltimaInfoRecibida == ASFAInfo.Anuncio_Parada)
                {
                    if (ASFABalizaSenal) ASFATiempoAA.Start();
                    if (ASFATiempoAA.Started && !ASFATiempoAA.Triggered)
                    {
                        ASFAVControl = ASFAVControlFinal = ASFAVCDesvAV;
                        ASFAVIntervencion = ASFAVCDesvAV + MpS.FromKpH(3f);
                        ASFACurvaL1.Setup(0f);
                        ASFACurvaL1.Start();
                        SetNextSignalAspect(Aspect.Approach_3);
                    }
                }
                if (ASFAAnteriorInfo == ASFAInfo.Preanuncio)
                {
                    ASFAVControl = Math.Max(Math.Min(ASFAVCISecPreParAAV, ASFAVCISecPreParAAV - ASFAVCSecPreParADec * (100f - ASFAVCSecPreParATReac - ASFACurvaL1.RemainingValue)), ASFAVCFSecPreParAAV);
                    ASFAVIntervencion = Math.Max(Math.Min(ASFAVCISecPreParAAV + MpS.FromKpH(3f), (ASFAVCISecPreParAAV + MpS.FromKpH(3f)) - ASFAVCSecPreParADec * (100f - ASFAVCSecPreParATReac - ASFACurvaL1.RemainingValue)), ASFAVCFSecPreParAAV + MpS.FromKpH(3f));
                    ASFAVControl = ASFAVCFSecPreParAAV;
                }
                if (ASFAAnteriorInfo == ASFAInfo.Preanuncio_AV)
                {
                    if (ASFANumeroBaliza == 1)
                    {
                        ASFAVControl = Math.Max(Math.Min(ASFAVCISecPreParA1AumVelAV, ASFAVCISecPreParA1AumVelAV - ASFAVCSecPreParADec * (100f - ASFAVCSecPreParA1AumVelTReac - ASFACurvaL1.RemainingValue)), ASFAVCFSecPreParA1AumVelAV);
                        ASFAVIntervencion = Math.Max(Math.Min(ASFAVCISecPreParA1AumVelAV + MpS.FromKpH(3f), (ASFAVCISecPreParA1AumVelAV + MpS.FromKpH(3f)) - ASFAVCSecPreParADec * (100f - ASFAVCSecPreParA1AumVelTReac - ASFACurvaL1.RemainingValue)), ASFAVCFSecPreParA1AumVelAV + MpS.FromKpH(3f));
                        ASFAVControl = ASFAVCFSecPreParA1AumVelAV;
                    }
                    else
                    {
                        ASFAVControl = Math.Max(Math.Min(ASFAVCISecPreParA2AumVelAV, ASFAVCISecPreParA2AumVelAV - ASFAVCSecPreParADec * (100f - ASFAVCSecPreParA2AumVelTReac - ASFACurvaL1.RemainingValue)), ASFAVCFSecPreParA2AumVelAV);
                        ASFAVIntervencion = Math.Max(Math.Min(ASFAVCISecPreParA2AumVelAV + MpS.FromKpH(3f), (ASFAVCISecPreParA2AumVelAV + MpS.FromKpH(3f)) - ASFAVCSecPreParADec * (100f - ASFAVCSecPreParA2AumVelTReac - ASFACurvaL1.RemainingValue)), ASFAVCFSecPreParA2AumVelAV + MpS.FromKpH(3f));
                        ASFAVControl = ASFAVCFSecPreParA2AumVelAV;
                    }
                }
            }
            if (ASFAControlDesvio)
            {
                if (ASFAAumentoVelocidadDesvio)
                {
                    ASFAVControl = ASFAVCDesvAumVelAV;
                    ASFAVIntervencion = ASFAVCDesvAumVelAV + MpS.FromKpH(3f);
                    ASFAVControlFinal = ASFAVCDesvAumVelAV;
                }
                else
                {
                    ASFAVControl = ASFAVCDesvAV;
                    ASFAVIntervencion = ASFAVCDesvAV + MpS.FromKpH(3f);
                    ASFAVControlFinal = ASFAVCDesvAV;
                }
                SetNextSignalAspect(Aspect.Restricted);
            }
            if (ASFAControlL8)
            {
                if (ASFAAumVelPressed && ASFACurvaL8.RemainingValue > 90f) ASFAAumentoVelocidadL8 = true;
                if (ASFAAumentoVelocidadL8)
                {
                    ASFAVControl = ASFAVCParAumVel;
                    ASFAVIntervencion = ASFAVCParAumVel + MpS.FromKpH(3);
                    ASFAVControlFinal = ASFAVControl;
                }
                else
                {
                    ASFAVControl = ASFAVCPar;
                    ASFAVIntervencion = ASFAVCPar + MpS.FromKpH(3);
                    ASFAVControlFinal = ASFAVControl;
                }
                if (ASFAUltimaInfoRecibida != ASFAInfo.Senal_Rojo && ASFAUltimaInfoRecibida != ASFAInfo.Rebase_Autorizado) SetNextSignalAspect(ASFASenales[ASFAAnteriorInfo]);
                else SetNextSignalAspect(ASFASenales[ASFAUltimaInfoRecibida]);
            }
            if (ASFAControlL7)
            {
                ASFAVControl = Math.Max(Math.Min(ASFAVCIPar, ASFAVCIPar - ASFAVCParDec * (100f - ASFAVCParTReac - ASFACurvaL7.RemainingValue)), ASFAVCFPar);
                ASFAVIntervencion = Math.Max(Math.Min(ASFAVCIPar + MpS.FromKpH(3f), (ASFAVCIPar + MpS.FromKpH(3f)) - ASFAVIParDec * (100f - ASFAVIParTReac - ASFACurvaL7.RemainingValue)), ASFAVCFPar + MpS.FromKpH(3f));
                ASFAVControlFinal = 0f;
                if (ASFANumeroBaliza == 2)
                {
                    ASFAVControl = Math.Max(Math.Min(ASFAVCFPar, ASFAVCFPar - ASFAVCParDec * (100f - ASFAVCParTReac - ASFACurvaL7.RemainingValue)), ASFAVCFPar / 2f);
                    ASFAVIntervencion = Math.Max(Math.Min(ASFAVCFPar + MpS.FromKpH(3f), (ASFAVCFPar + MpS.FromKpH(3f)) - ASFAVIParDec * (100f - ASFAVIParTReac - ASFACurvaL7.RemainingValue)), ASFAVCFPar / 2f + MpS.FromKpH(3f));
                }
                if (ASFADistanciaPrevia.Triggered)
                {
                    ASFAUrgencia = true;
                    ASFAControlL7 = false;
                    ASFAControlL1 = true;
                    ASFACurvaL1.Setup(0f);
                    ASFACurvaL1.Start();
                    ASFAUltimaInfoRecibida = ASFAInfo.Anuncio_Parada;
                }
            }
            if (ASFAControlLTV)
            {
                if (ASFAAumVelPressed && ASFACurvaLTV.RemainingValue > 90f && !ASFAAumentoVelocidadLTV)
                {
                    ASFAPressed = false;
                    BotonesASFA();
                    ASFAAumentoVelocidadLTV = true;
                }
                if (ASFAAumentoVelocidadLTV)
                {
                    ASFAVControl = Math.Min(ASFAVControl, Math.Max(Math.Min(ASFATipoTren, ASFATipoTren - ASFAVCAnParDec * (100f - ASFAVCAnParTReac - ASFACurvaLTV.RemainingValue)), ASFAVCFLTVAumVelAV));
                    ASFAVIntervencion = Math.Min(ASFAVIntervencion, Math.Max(Math.Min(ASFATipoTren + MpS.FromKpH(5f), (ASFATipoTren + MpS.FromKpH(5f)) - ASFAVIAnParDec * (100f - ASFAVIAnParTReac - ASFACurvaLTV.RemainingValue)), ASFAVCFLTVAumVelAV + MpS.FromKpH(3f)));
                    ASFAVControlFinal = ASFAVCFLTVAumVelAV;
                    if (SpeedMpS() < ASFAVCFLTVAumVel) ASFALTVCumplida = true;
                    if (ASFALTVCumplida)
                    {
                        ASFAVControl = Math.Min(ASFAVControl, ASFAVCFLTVAumVelAV);
                        ASFAVIntervencion = Math.Min(ASFAVIntervencion, ASFAVCFLTVAumVelAV + MpS.FromKpH(3f));
                        if (ASFALTVPressed) ASFALTVCumplida = ASFAControlLTV = ASFAAumentoVelocidadLTV = false;
                    }
                }
                else
                {
                    ASFAVControl = Math.Min(ASFAVControl, Math.Max(Math.Min(ASFATipoTren, ASFATipoTren - ASFAVCAnParDec * (100f - ASFAVCAnParTReac - ASFACurvaLTV.RemainingValue)), ASFAVCFLTVAV));
                    ASFAVIntervencion = Math.Min(ASFAVIntervencion, Math.Max(Math.Min(ASFATipoTren + MpS.FromKpH(5f), (ASFATipoTren + MpS.FromKpH(5f)) - ASFAVIAnParDec * (100f - ASFAVIAnParTReac - ASFACurvaLTV.RemainingValue)), ASFAVCFLTVAV + MpS.FromKpH(3f)));
                    ASFAVControlFinal = ASFAVCFLTVAV;
                    if (SpeedMpS() < ASFAVCFLTV) ASFALTVCumplida = true;
                    if (ASFALTVCumplida)
                    {
                        ASFAVControl = Math.Min(ASFAVControl, ASFAVCFLTVAV);
                        ASFAVIntervencion = Math.Min(ASFAVIntervencion, ASFAVCFLTVAV + MpS.FromKpH(3f));
                        if (ASFALTVPressed) ASFALTVCumplida = ASFAControlLTV = false;
                    }
                }
            }
            ASFAVControl = Math.Min(ASFAVControl, TrainMaxSpeed);
            ASFAVIntervencion = Math.Min(ASFAVIntervencion, TrainMaxSpeed + MpS.FromKpH(5f));
            ASFAVControlFinal = Math.Min(ASFAVControlFinal, TrainMaxSpeed);
            //SetCurrentSpeedLimitMpS(ASFAVControl);
            SetNextSpeedLimitMpS(ASFAVControlFinal);
            SetInterventionSpeedLimitMpS(ASFAVIntervencion);
            if (!ASFAControlDesvio && !ASFARebaseAuto && !ASFAControlL8 && (!ASFATiempoAA.Started || ASFATiempoAA.Triggered)) SetNextSignalAspect(ASFASenales[ASFAUltimaInfoRecibida]);
        }
        string ArduinoPort = null;
        SerialPort sp;
        float PreviousTime = 0;
        float LastConex = 0;
        protected void Arduino()
        {
            string[] ports = null;
            if (ArduinoPort == null)
            {
                ports = SerialPort.GetPortNames();
                foreach (string port in ports)
                {
                    try
                    {
                        using (SerialPort sp = new SerialPort(port))
                        {
                            sp.Open();
                            string incoming = sp.ReadExisting();
                            if (incoming.Contains("ASFA")) ArduinoPort = port;
                            else sp.Close();
                        }
                        if (ArduinoPort != null)
                        {
                            sp = new SerialPort(ArduinoPort, 9600);
                            sp.WriteBufferSize = 128;
                            sp.ReadBufferSize = 128;
                            LastConex = ClockTime();
                            break;
                        }
                    }
                    catch (Exception) { }
                }
            }
            if (ArduinoPort != null)
            {
                try
                {
                    if (!sp.IsOpen) sp.Open();
                    sp.WriteTimeout = 10;
                    char[] incoming = new char[13];
                    if (TipoASFA == Tipo_ASFA.Digital)
                    {
                        ASFAConexPressed = true;
                        while (sp.BytesToRead > 13 && sp.ReadChar() != '\n') ;
                        if (sp.BytesToRead >= 13)
                        {
                            sp.Read(incoming, 0, 13);
                            LastConex = ClockTime();
                        }
                        if (incoming[9] == 'A')
                        {
                            ASFAAnParPressed = incoming[0] == '1';
                            ASFAAnPrePressed = incoming[1] == '1';
                            ASFAPrePar_VLCondPressed = incoming[2] == '1';
                            ASFAPaNPressed = incoming[3] == '1';
                            ASFALTVPressed = incoming[4] == '1';
                            ASFARebasePressed = incoming[5] == '1';
                            ASFAAumVelPressed = incoming[6] == '1';
                            ASFAModoPressed = incoming[7] == '1';
                            ASFARearmeFrenoPressed = incoming[8] == '1';
                        }
                        if (PreviousTime + 0.5f < ClockTime())
                        {
                            switch (ASFAUltimaInfoRecibida)
                            {
                                case ASFAInfo.Via_Libre_Cond:
                                    sp.Write("1");
                                    break;
                                case ASFAInfo.Anuncio_Parada:
                                    sp.Write("2");
                                    break;
                                case ASFAInfo.Anuncio_Precaucion:
                                    sp.Write("3");
                                    break;
                                case ASFAInfo.Via_Libre:
                                    sp.Write("0");
                                    break;
                                case ASFAInfo.Senal_Rojo:
                                    sp.Write("7");
                                    break;
                                case ASFAInfo.Previa_Rojo:
                                    sp.Write("6");
                                    break;
                                default:
                                    sp.Write("8");
                                    break;
                            }
                            sp.Write("ASFA" + '\r' + '\n');
                            PreviousTime = ClockTime();
                        }
                    }
                    else
                    {
                        while (sp.BytesToRead > 7 && sp.ReadChar() != '\n') ;
                        byte[] data = new byte[1];
                        if (sp.BytesToRead > 0)
                        {
                            sp.Read(data, 0, 1);
                            LastConex = ClockTime();
                        }
                        if (data[0] == 48) ASFAUrgencia = false;
                        if (data[0] == 49) ASFAUrgencia = true;
                        char freq = '/';
                        switch (BalizaASFA())
                        {
                            case ASFAFreq.FP: freq = '0'; break;
                            case ASFAFreq.L1: freq = '1'; break;
                            case ASFAFreq.L2: freq = '2'; break;
                            case ASFAFreq.L3: freq = '3'; break;
                            case ASFAFreq.L4: freq = '4'; break;
                            case ASFAFreq.L5: freq = '5'; break;
                            case ASFAFreq.L6: freq = '6'; break;
                            case ASFAFreq.L7: freq = '7'; break;
                            case ASFAFreq.L8: freq = '8'; break;
                            case ASFAFreq.L9: freq = '9'; break;
                        }
                        byte speed = Convert.ToByte(Math.Abs(MpS.ToKpH(SpeedMpS())));
                        if (PreviousTime + 0.5f < ClockTime() || freq != '0')
                        {
                            sp.Write(new char[1] { freq }, 0, 1);
                            sp.Write(new byte[1] { speed }, 0, 1);
                            sp.Write("ASFA" + '\r' + '\n');
                            PreviousTime = ClockTime();
                        }
                    }
                }
                catch (Exception)
                {
                }
            }
        }
        bool IsLTV;
        bool IsPN;
        protected void BotonesASFA()
        {
            if (ArduinoPort == null)
            {
                if (AnuncioLTVPassed) IsLTV = true;
                if (PreviaPassed || SignalPassed) IsLTV = false;
                if (PreviaPassed) IsPN = false;
                if (ASFAPressed && !ASFAPressedTimer.Started)
                {
                    ASFATimesPressed++;
                    ASFAConexTimesPressed++;
                    ASFAButtonsTimer.Stop();
                }
                if ((ASFATimesPressed != 0 || ASFAConexTimesPressed != 0) && !ASFAPressed && !ASFAButtonsTimer.Started) ASFAButtonsTimer.Start();
                if (ASFAButtonsTimer.Triggered) ASFATimesPressed = ASFAConexTimesPressed = 0;
                ASFAModoPressed = ASFAPressed && ASFATimesPressed == 3;
                if (ASFAPressed && ASFAConexTimesPressed == 4)
                {
                    if (ASFAConexPressed) ASFAConexPressed = false;
                    else ASFAConexPressed = true;
                }
                if (ASFAPressed && !ASFAPressedTimer.Started)
                {
                    ASFAPressedTimer.Start();
                }
                if (!ASFAPressed)
                {
                    if (ASFAPressedTimer.Started) ASFAPressedTimer.Stop();
                    ASFAAnParPressed = false;
                    ASFAAnPrePressed = false;
                    ASFAPrePar_VLCondPressed = false;
                    ASFALTVPressed = false;
                    ASFAPaNPressed = ASFAAumVelPressed = ASFARearmeFrenoPressed = false;
                }
                if (ASFAPressedTimer.Started && ASFAPressedTimer.RemainingValue < 2.5f)
                {
                    ASFAAnParPressed = (BalizaAspect == Aspect.Approach_1 || BalizaAspect == Aspect.Approach_3) && ASFARec.Started && ASFAUltimaFrecuencia == ASFAFreq.L1 && !IsLTV;
                    ASFAAnPrePressed = BalizaAspect == Aspect.Approach_2 && ASFARec.Started && !IsLTV;
                    ASFAPrePar_VLCondPressed = ((BalizaAspect == Aspect.Clear_1 && ASFARec.Started)) && !IsLTV;
                    ASFALTVPressed = (ASFAUltimaFrecuencia == ASFAFreq.L9 && ASFARec.Started) || (ASFAControlLTV && ASFALTVCumplida && !ASFARec.Started) || (ASFAUltimaFrecuencia == ASFAFreq.L1 && IntermediateLTVDist && ASFARec.Started && IsLTV);
                    ASFAPaNPressed = (ASFAUltimaFrecuencia == ASFAFreq.L3 && ASFARec.Started) || (BalizaAspect == Aspect.Approach_1 && CurrentSignalSpeedLimitMpS() > MpS.FromKpH(25f) && CurrentSignalSpeedLimitMpS() < MpS.FromKpH(35f) && ASFARec.Started && IsPN);
                    if (ASFAPaNPressed) ASFAAnParPressed = false;
                    ASFAAumVelPressed = ASFARearmeFrenoPressed = true;
                }
                if (ASFAPressedTimer.Triggered)
                {
                    ASFAAnParPressed = false;
                    ASFAAnPrePressed = false;
                    ASFAPrePar_VLCondPressed = false;
                    ASFAPaNPressed = ASFAAumVelPressed = ASFARearmeFrenoPressed = false;
                    ASFARebasePressed = !ASFARebasePressed;
                    ASFAPressedTimer.Stop();
                }
                ASFAOcultacionPressed = false;
                if (ASFALTVPressed) IsLTV = false;
            }
        }
        ASFAFreq BalizaASFA()
        {
            if (IsDirectionReverse())
            {
                if (SignalPassed) return ASFAFreq.L8;
                else if (PreviaPassed) return ASFAFreq.L7;
                else return ASFAFreq.FP;
            }
            else
            {
                if (PreviaPassed)
                {
                    BalizaAspect = NextSignalAspect(PreviaSignalNumber);
                    switch (BalizaAspect)
                    {
                        case Aspect.Stop:
                        case Aspect.StopAndProceed:
                        case Aspect.Restricted:
                        case Aspect.Permission:
                            return ASFAFreq.L7;
                        case Aspect.Approach_1:
                        case Aspect.Approach_2:
                        case Aspect.Approach_3:
                            return ASFAFreq.L1;
                        case Aspect.Clear_1:
                            return ASFAFreq.L2;
                        case Aspect.Clear_2:
                            return ASFAFreq.L3;
                        default:
                            BalizaASFAPassed = false;
                            break;
                    }
                }
                if (SignalPassed)
                {
                    BalizaAspect = BalizaNextAspect;
                    switch (BalizaAspect)
                    {
                        case Aspect.Stop:
                        case Aspect.StopAndProceed:
                        case Aspect.Restricted:
                        case Aspect.Permission:
                            return ASFAFreq.L8;
                        case Aspect.Approach_1:
                        case Aspect.Approach_2:
                        case Aspect.Approach_3:
                            return ASFAFreq.L1;
                        case Aspect.Clear_1:
                            return ASFAFreq.L2;
                        case Aspect.Clear_2:
                            return ASFAFreq.L3;
                        default:
                            BalizaASFAPassed = false;
                            break;
                    }
                }
                else
                {
                    BalizaNextAspect = NextSignalAspect(0);
                }
                if (AnuncioLTVPassed) return ASFAFreq.L1;
                if (PreanuncioLTVPassed) return ASFAFreq.L2;
            }
            return ASFAFreq.FP;
        }
        protected void BalizaSenal()
        {
            ASFABalizaSenal = true;
            if (ASFADistanciaPrevia.Triggered)
            {
                ASFANumeroBaliza = 0;
                ASFAAnteriorInfo = ASFAUltimaInfoRecibida;
            }
            ASFANumeroBaliza++;
            if ((ASFANumeroBaliza == 2 && ASFAUltimaFrecuencia != ASFAFreq.L7) || ASFAUltimaFrecuencia == ASFAFreq.L8)
            {
                if (ASFADistanciaPrevia.RemainingValue < 370f)
                {
                    ASFADistanciaPrevia.Setup(0f);
                    ASFADistanciaPrevia.Start();
                }
            }
            if (ASFANumeroBaliza == 1 || (ASFAUltimaFrecuencia == ASFAFreq.L7 && ASFADistanciaPrevia.RemainingValue < 370f))
            {
                ASFANumeroBaliza = 1;
                if (ASFADigitalModo == ASFADigital_Modo.AVE) ASFADistanciaPrevia.Setup(ASFADistancia.RemainingValue - 1400f);
                else ASFADistanciaPrevia.Setup(ASFADistancia.RemainingValue - 1550f);
                ASFADistanciaPrevia.Start();
            }
        }
        Aspect LZBLastAspect;
        Aspect LZBPreviousAspect;
        bool LZBLiberar;
        bool LZBRebasar;
        bool LZBAnularParada;
        bool LZBSupervising;
        bool LZBEmergencyBrake;
        bool LZBOE;
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
        float LZBMaxSpeed = 0;
        float LZBTargetSpeed = 0;
        float LZBTargetDistance = 0;
        float LZBDeceleration = 0.5f;
        LZBCenter LZBCenter;
        bool LZBAhorroEnergia = false;
        bool LZBEnd;
        bool LZBVOn;
        bool LZBV;
        Timer LZBRecTimer;
        Timer LZBCurveTimer;
        Timer LZBVTimer;
        protected void UpdateLZB()
        {
            LZBEmergencyBrake = false;
            LZBRebasar = ASFARebasePressed;
            LZBLiberar = ASFAPressed;
            LZBAnularParada = ASFAModoPressed && SerieTren == 446;
            if (ASFAModoPressed)
            {
                ASFATimesPressed = 0;
                ASFAModoPressed = false;
            }
            if ((DateTime.Now.DayOfWeek == DayOfWeek.Saturday || DateTime.Now.DayOfWeek == DayOfWeek.Sunday) && SerieTren == 446)
            {
                LZBAhorroEnergia = true;
            }
            else LZBAhorroEnergia = false;
            if (((LZBLiberar && !LZBOE && SerieTren == 446) || (SignalPassed && !LZBOE && SerieTren != 446)) && !LZBSupervising)
            {
                LZBSupervising = true;
                LZBTR = null;
            }
            if (LZBRebasar)
            {
                LZBSupervising = LZBOE;
                LZBOE = false;
                LZBEmergencyBrake = false;
                ASFARebasePressed = false;
                ASFAPressedTimer.Stop();
                ASFAPressed = false;
                BotonesASFA();
                if (SerieTren == 446) LZBSupervising = false;
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
                    if (SerieTren == 446)
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
                    if (SerieTren == 446)
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
                LZBV = (LZBTargetDistance < 350 || SpeedCurve(LZBTargetDistance - 350, LZBTargetSpeed, 0, 0, LZBPFT / 250) <= LZBSpeedLimit) && LZBTargetSpeed < LZBSpeedLimit;
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
                if (ActiveCCS == CCS.LZB && (!ETCSInstalled || CurrentETCSMode == ETCSMode.IS))
                {
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
            else if (ActiveCCS == CCS.LZB)
            {
                SetVigilanceAlarmDisplay(false);
                SetCurrentSpeedLimitMpS(0);
                SetNextSpeedLimitMpS(0);
            }
        }
        protected void LZBAlertarLiberar()
        {
            LZBVMT = TrainMaxSpeed;
            if (LZBVMT < MpS.FromKpH(101)) LZBMaxDistance = 2000;
            else if (LZBVMT < MpS.FromKpH(161)) LZBMaxDistance = 4000;
            else if (LZBVMT < MpS.FromKpH(201)) LZBMaxDistance = 9900;
            else LZBMaxDistance = 12000;
            LZBLT = TrainLengthM();
            if (SerieTren == 446)
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
            if (SignalPassed)
            {
                if (ETCSAspect == Aspect.Stop) return new UnconditionalEmergencyStop();
                switch (ETCSAspect)
                {
                    case Aspect.Approach_1:
                    case Aspect.Approach_3:
                        if (IsPN && CurrentSignalSpeedLimitMpS() < MpS.FromKpH(35f) && CurrentSignalSpeedLimitMpS() > MpS.FromKpH(25f)) { }
                        else N_ITER = 1;
                        break;
                    case Aspect.Clear_1:
                    case Aspect.Approach_2:
                        N_ITER = 2;
                        break;
                    case Aspect.Clear_2:
                        if (CurrentPostSpeedLimitMpS() < MpS.FromKpH(165) && CurrentPostSpeedLimitMpS() > MpS.FromKpH(155) && IsPN) { }
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
            if (SBalisePassed)
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
                if(NextSignalAspect(0)!=Aspect.Stop) Packets.Add(Link);
                Packets.Add(Speed);
                Packets.Add(MA);
                if (NextSignalAspect(0) == Aspect.Approach_2) Packets.Add(new TemporarySpeedRestriction(0, 0, 1, 5, (int)(NextSignalDistanceM(1) + 20), (int)(NextSignalDistanceM(2) - NextSignalDistanceM(1) + 100), 0, NextSignalSpeedLimitMpS(0) > MpS.FromKpH(100) ? 12 : 6));
                if (MP != null) Packets.Add(MP);
                return new EurobaliseTelegram(NID_BG, 1, Packets.ToArray());
            }
            else if (PreviaPassed)
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
        ETCSMode CurrentETCSMode = ETCSMode.SB;
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
            {"Standstil supervision", "Standstill supervision" },
            {"Reverse movement protection", "Reverse movement protection"}
        };
        class ETCSMessage
        {
            public string Text;
            public int id = 0;
            public float Time;
            public Func<bool> Revoke;
            public int Priority;
            public bool Acknowledgement;
            public bool Acknowledged;
            public bool Displayed;
            public ETCSMessage(string text, float time, Func<bool> revoke, int priority, bool ack)
            {
                Text = text;
                for(int i=0; i<ETCSFixedText.Length/2; i++)
                {
                    if(Text == ETCSFixedText[i, 1])
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
                    if (id == a.id) return true;
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
        float LastAck = -1;
        protected void ViewMessages()
        {
            Messages.RemoveAll(x => x.Revoke());
            DispMsg = new List<ETCSMessage>();
            DispMsg.Clear();
            if (Messages.Count == 0) return;
            var A = Messages.Find(x => x.Acknowledgement);
            if(A!=null)
            {
                if (LastAck + 1 <= ClockTime() || A.Displayed)
                {
                    DispMsg.Add(A);
                    if (!A.Displayed)
                    {
                        TriggerSoundInfo1();
                        A.Displayed = true;
                        if(A.id == 5) Message(Orts.Simulation.ConfirmLevel.None, "ETCS: " + ETCSFixedText[A.id, 0]);
                    }
                    if(!A.Acknowledged && !A.Revoke() && A.id!=5) Message(Orts.Simulation.ConfirmLevel.None, "ETCS: " + ETCSFixedText[A.id, 0]);
                    LastAck = ClockTime();
                }
                if (ASFAPressed && A.Displayed)
                {
                    A.Acknowledged = true;
                    if (A.Revoke()) A.Revoke = () => true;
                    else A.Acknowledgement = false;
                }
            }
            else
            {
                Messages.Sort(delegate(ETCSMessage x, ETCSMessage y)
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
                foreach (var m in Messages)
                {
                    m.Acknowledgement = false;
                    m.Acknowledged = true;
                    if (m.Revoke()) m.Revoke = () => true;
                    if(!DispMsg.Contains(m))
                    {
                        DispMsg.Add(m);
                        if (!m.Displayed)
                        {
                            if(m.Priority == 1) TriggerSoundInfo1();
                            m.Displayed = true;
                        }
                    }
                    if (DispMsg.Contains(m)) Message(Orts.Simulation.ConfirmLevel.None, "ETCS: " + ETCSFixedText[m.id, 0]);
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
        bool Start;
        protected void UpdateETCS()
		{
            if (Messages == null) Messages = new List<ETCSMessage>();
            Odometry();
            ETCSServiceBrake = ETCSEmergencyBraking = false;
            TrainInfo.Calc();
            EBD = Eurobalise();
            ManageBaliseData();
            switch (CurrentETCSLevel)
            {
                case ETCS_Level.L0:
                    var m = new ETCSMessage("Reconocer modo UN", ClockTime(), () => false, 0, true);
                    if (CurrentETCSMode != ETCSMode.UN)
                    {
                        m.Revoke = () => m.Acknowledged;
                        if(!Messages.Contains(m)) Messages.Add(m);
                    }
                    CurrentETCSMode = ETCSMode.UN;
                    if (!ASFAActivated)
                    {
                        ActiveCCS = CCS.ETCS;
                    }
                    if (ActiveCCS == CCS.ETCS)
                    {
                        if (ASFAActivated)
                        {
                            if (TipoASFA == Tipo_ASFA.Digital) ASFADigitalModo = ASFADigital_Modo.EXT;
                            ASFAEficacia = false;
                        }
                        var a = Messages.Find(x => x.Equals(m));
                        if (a != null && a.Acknowledged)
                        {
                            if (ASFAActivated)
                            {
                                ActiveCCS = CCS.ASFA;
                                ASFADigitalModo = ASFAModoCONV ? ASFADigital_Modo.CONV : ASFADigital_Modo.AVE;
                                ASFAEficacia = true;
                                SetCurrentSpeedLimitMpS(0);
                            }
                        }
                    }
                    break;
                case ETCS_Level.L1:
                    ActiveCCS = CCS.ETCS;
                    if (ASFAInstalled)
                    {
                        if (TipoASFA == Tipo_ASFA.Digital) ASFADigitalModo = ASFADigital_Modo.EXT;
                        ASFAEficacia = false;
                    }
                    break;
                case ETCS_Level.L2:
                case ETCS_Level.L3:
                    ActiveCCS = CCS.ETCS;
                    if (ASFAInstalled)
                    {
                        if (TipoASFA == Tipo_ASFA.Digital) ASFADigitalModo = ASFADigital_Modo.EXT;
                        ASFAEficacia = false;
                    }
                    ERM = RBC(TRBCM);
                    if (ERM != null)
                    {
                        switch(ERM.NID_MESSAGE)
                        {
                            case 3:
                                MA = ((RadioMA)ERM).MA;
                                if (MA.V_MAIN == 0)
                                {
                                    if(!OverrideEoA)
                                    {
                                        CurrentETCSMode = ETCSMode.TR;
                                        TriggerSoundPenalty1();
                                        Messages.Add(new ETCSMessage("EoA o LoA rebasado", ClockTime(), () => CurrentETCSMode!=ETCSMode.TR && CurrentETCSMode!=ETCSMode.PT, 1, false));
                                    }
                                    MA = null;
                                }
                                if (((RadioMA)ERM).OptionalPackets != null)
                                {
                                    foreach(var pck in ((RadioMA)ERM).OptionalPackets)
                                    {
                                        switch(pck.NID_PACKET)
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
                                ETCSEmergencyBraking = true;
                                if (!OverrideEoA)
                                {
                                    CurrentETCSMode = ETCSMode.TR;
                                    TriggerSoundPenalty1();
                                }
                                break;
                        }
                    }
                    break;
                case ETCS_Level.NTC:
                    if(LZBInstalled)
                    {
                        if(ActiveCCS != CCS.LZB)
                        {
                            ActiveCCS = CCS.LZB;
                            LZBAlertarLiberar();
                            CurrentETCSMode = ETCSMode.SN;
                        }
                        UpdateLZB();
                        if (LZBOE) CurrentETCSMode = ETCSMode.TR;
                        LZBOE = LZBEmergencyBrake = false;
                    }
                    break;
            }
            if(CurrentETCSMode == ETCSMode.SB)
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
            }
            if (CurrentETCSMode == ETCSMode.SB && IsTrainControlEnabled() && TrainInfo.IsOK && !Start && ETCSPressed)
            {
                StartMission();
            }
            if (CurrentETCSMode == ETCSMode.SB && Start)
            {
                var a = Messages.Find(x => x.Equals(new ETCSMessage("Reconocer modo SR", 0, null, 0, true)));
                if (a != null && a.Acknowledged)
                {
                    SetVigilanceAlarmDisplay(false);
                    SetVigilanceEmergencyDisplay(false);
                    CurrentETCSMode = ETCSMode.SR;
                    SetSR();
                }
            }
            if (MA != null && (MP == null || MP.D_MAMODE[0]>dMaxFront) && ISSP != null && ISSP.D_STATIC[0]<=dMaxFront && CurrentETCSMode != ETCSMode.FS)
            {
                CurrentETCSMode = ETCSMode.FS;
                Messages.Add(new ETCSMessage("Entrada en FS", ClockTime(), () => CurrentETCSMode != ETCSMode.FS || ISSP.D_STATIC[0] < dMinFront - TrainLengthM(), 1, false));
                SetVigilanceAlarmDisplay(true);
            }
            if (CurrentETCSMode == ETCSMode.FS && ISSP.D_STATIC[0]<dMinFront-TrainLengthM())
            {
                SetVigilanceAlarmDisplay(false);
            }
            if (CurrentETCSMode == ETCSMode.TR) Trip();
            if (ASFARebasePressed && SpeedMpS() < NationalValues.V_NVALLOWOVTRP)
            {
                SetVigilanceAlarmDisplay(true);
                SetVigilanceEmergencyDisplay(true);
                SetNextSignalAspect(Aspect.Approach_3);
                AcknowledgeEoA = true;
                ASFAPressed = false;
                ASFARebasePressed = false;
                BotonesASFA();
            }
            if (CurrentETCSLevel != ETCS_Level.L0)
            {
                if (AcknowledgeEoA && ETCSPressed)
                {
                    SetNextSignalAspect(Aspect.Approach_3);
                    CurrentETCSMode = ETCSMode.SR;
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
                if (OverrideEoA && CurrentETCSMode != ETCSMode.SR) OverrideEoA = false;
            }
            if (ActiveCCS == CCS.ETCS)
            {
                switch(CurrentETCSMode)
                {
                    case ETCSMode.FS:
                        SetNextSignalAspect(Aspect.Clear_2);
                        break;
                    case ETCSMode.SR:
                    default:
                        if (OverrideEoA||AcknowledgeEoA) SetNextSignalAspect(Aspect.Approach_3);
                        else SetNextSignalAspect(Aspect.Clear_1);
                        break;
                    case ETCSMode.UN:
                        SetNextSignalAspect(Aspect.Approach_2);
                        break;
                    case ETCSMode.TR:
                        if (SpeedMpS() > 0) SetNextSignalAspect(Aspect.Stop);
                        else SetNextSignalAspect(Aspect.Approach_1);
                        break;
                    case ETCSMode.SH:
                        SetNextSignalAspect(Aspect.Restricted);
                        break;
                    case ETCSMode.OS:
                        SetNextSignalAspect(Aspect.StopAndProceed);
                        break;
                }
            }
            if (CurrentETCSMode == ETCSMode.TR || CurrentETCSMode == ETCSMode.NP || CurrentETCSMode == ETCSMode.SF || CurrentETCSMode == ETCSMode.SB)
            {
                if (CurrentETCSMode != ETCSMode.SB) ETCSEmergencyBraking = true;
                else StandstillSupervision();
                SetCurrentSpeedLimitMpS(0.3f);
                SetNextSpeedLimitMpS(0);
                SetInterventionSpeedLimitMpS(0);
                SetMonitoringStatus(MonitoringStatus.Normal);
                ViewMessages();
                return;
            }
            if(CurrentETCSMode == ETCSMode.PT)
            {
                SetCurrentSpeedLimitMpS(0.3f);
                SetNextSpeedLimitMpS(0);
                SetInterventionSpeedLimitMpS(0);
                SetMonitoringStatus(MonitoringStatus.Normal);
                PostTripReverse();
                ViewMessages();
                return;
            }
            if (CurrentETCSMode != ETCSMode.RV && CurrentETCSMode != ETCSMode.PT && MA != null) ReverseMovement();
            UpdateControls();
            SpeedProfiles();
            SupervisedTargets();
            SpeedMonitors();
            ETCSCurves();
            ETCSLinkingBrake &= SpeedMpS() > 0.1f;
            ETCSServiceBrake |= ETCSLinkingBrake;
            ViewMessages();
            ETCSPressed = false;
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
            if (!IsDirectionReverse() || DistanceM()-PostTrip>NationalValues.D_NVPOTRP) ETCSEmergencyBraking = true;
        }
        float ProtectionDistance = -1;
        bool StandstillApply = false;
        protected void StandstillSupervision()
        {
            if(ProtectionDistance == -1) ProtectionDistance = DistanceM();
            if (DistanceM() - ProtectionDistance > NationalValues.D_NVROLL)
            StandstillApply = true;
            if (StandstillApply)
            {
                ETCSEmergencyBraking = true;
                if (SpeedMpS() < 0.1f)
                {
                    SetVigilanceEmergencyDisplay(true);
                    var m = new ETCSMessage("Standstill supervision", ClockTime(), () => false, 0, true);
                    m.Revoke = () => m.Acknowledged;
                    if(!Messages.Contains(m)) Messages.Add(m);
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
        float ReverseDistance = -1;
        bool ReverseApply = false;
        protected void ReverseMovement()
        {
            if((!IsDirectionReverse()&&MA.Q_DIR==0)||(IsDirectionReverse()&&MA.Q_DIR==1))
            {
                if (ReverseDistance == -1) ReverseDistance = DistanceM();
                if (DistanceM() - ReverseDistance > NationalValues.D_NVROLL) ReverseApply = true;
                if (ReverseApply)
                {
                    ETCSEmergencyBraking = true;
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
            if (!Apply && !Release && BrakePipePressureBar()>=MaxPres)
            {
                Apply = true;
                timeapp = ClockTime();
                Messages.Add(new ETCSMessage("Test de freno de emergencia: en curso", ClockTime(), () => TrainInfo.T_bs >= 0, 2, false));
            }
            ETCSEmergencyBraking |= Apply && !Release;
            if(Apply && BrakePipePressureBar()<=MinPres && !Release)
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
            if(!OverrideEoA)
            {
                if (CurrentETCSMode != ETCSMode.TR)
                {
                    CurrentETCSMode = ETCSMode.TR;
                    TriggerSoundPenalty1();
                    MA = null;
                    MAsp = null;
                    MP = null;
                    if(MPsp!=null) MPsp.Clear();
                    ISSP = null;
                    if (SSPsp != null) SSPsp.Clear();
                    if (MRSPTargets != null) MRSPTargets.Clear();
                    EoA = SvL = LoA = null;
                    SRsp = null;
                    ETCSEmergencyBraking = true;
                }
                SetVigilanceAlarmDisplay(true);
                SetVigilanceEmergencyDisplay(true);
                if (SpeedMpS() < 0.1)
                {
                    SetNextSignalAspect(Aspect.Approach_1);
                    var m = new ETCSMessage("Modo TRIP", ClockTime(), () => false, 0, true);
                    m.Revoke = () => m.Acknowledged;
                    if (!Messages.Contains(m)) Messages.Add(m);
                    var a = Messages.Find(x => x.Equals(m));
                    if (a != null && a.Acknowledged)
                    {
                        CurrentETCSMode = ETCSMode.PT;
                        SetVigilanceAlarmDisplay(false);
                        SetVigilanceEmergencyDisplay(false);
                    }
                }
                else SetNextSignalAspect(Aspect.Stop);
            }
        }
        bool ETCSLinkingBrake;
        protected void ManageBaliseData()
        {
            EBD = Eurobalise();
            if (EBD != null)
            {
                InfillLocationReference infill = null;
                if (CurrentETCSLevel == ETCS_Level.L1 || CurrentETCSLevel == ETCS_Level.L2 || CurrentETCSLevel == ETCS_Level.L3)
                {
                    if (EBD.Q_LINK == 1 && link != null && ((link.NID_BG != EBD.NID_BG && link.D_LINK < dMaxFront && link.D_LINK < dMinFront) || (link.NID_BG == EBD.NID_BG && link.D_LINK > dMaxFront)))
                    {
                        switch (link.Q_LINKREACTION)
                        {
                            case 0:
                                Messages.Add(new ETCSMessage("Datos de eurobaliza no consistentes", ClockTime(), () => CurrentETCSMode != ETCSMode.TR && CurrentETCSMode != ETCSMode.PT, 1, false));
                                Trip();
                                return;
                            case 1:
                                Messages.Add(new ETCSMessage("Datos de eurobaliza no consistentes", ClockTime(), () => ETCSLinkingBrake = false, 1, false));
                                ETCSLinkingBrake = true;
                                break;
                        }
                    }
                    else if (EBD.Q_LINK == 1 && link != null && link.NID_BG == EBD.NID_BG && TrainPosition.LRBG.Pos + link.D_LINK < dMaxFront && TrainPosition.LRBG.Pos + link.D_LINK > dMinFront)
                    {
                        TrainPosition.SetLRBG(new BaliseGroup(TrainPosition.LRBG.Pos + link.D_LINK, link.Q_LOCACC, EBD.NID_BG, this));
                        UpdateDistances(TrainPosition.PrevLRBG.Dist());
                        Odometry();
                    }
                    else if(EBD.Q_LINK == 1 && link == null)
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
                            if(CurrentETCSLevel==ETCS_Level.L1)
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
            }
        }
        protected void UpdateDistances(float Reference)
        {
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
            if(MA!=null)
            {
                for(int i=0; i<MA.L_SECTION.Length; i++)
                {
                    if (i == 0) MA.L_SECTION[i] -= (int)Reference;
                    else if(MA.L_SECTION[i - 1]<0) MA.L_SECTION[i] += MA.L_SECTION[i - 1];
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
		bool ETCSEmergencyBraking;
		bool ETCSServiceBrake;
		float ETCSVtarget;
		float ETCSVperm;
		float ETCSVsbi;
        float ETCSVrelease;
        float A_brake_emergency(float start, float target)
        {
            /*if (start > MpS.FromKpH(60) && target < MpS.FromKpH(60)) return ((start - target) * 0.809f * 1.29f) / (0.809f * (MpS.FromKpH(60) - target) + 1.29f * (MpS.FromKpH(target - MpS.FromKpH(60))));
            else if (target > MpS.FromKpH(60)) return 1.29f;
            else */return 0.809f;
        }
        float A_brake_service(float start, float target)
        {
            /*if (start > MpS.FromKpH(60) && target < MpS.FromKpH(60)) return ((start - target) * 0.5f * 0.9f) / (0.5f * (MpS.FromKpH(60) - target) + 0.9f * (MpS.FromKpH(target - MpS.FromKpH(60))));
            else if (target > MpS.FromKpH(60)) return 0.9f;
            else */return 0.5f;
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
                for (int i=0; i<L_SECTION.Length; i++)
                {
                    L_SECTION[i] = MA.L_SECTION[i + offset];
                }
                for(int i = 0; i < L_SECTION.Length; i++)
                {
                    if (L_SECTION[i] + dist >= LocationReference || i + 1 == L_SECTION.Length)
                    {
                        dist = 0;
                        MA.L_SECTION = new int[i + 1 + nMA.L_SECTION.Length];
                        for(int a=0; a<MA.L_SECTION.Length; a++)
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
            else if (MA.Q_DANGERPOINT==1) dSvL = dEoA + MA.D_DP;
            else dSvL = dEoA;
            dTrip = dEoA + Math.Max(2 * TrainPosition.LRBG.Acc + 10 + 0.1f * dEoA, dMaxFront - dMinFront);
            MAsp = new SpeedProfile(MpS.FromKpH(MA.V_MAIN * 5), dEstFront, dEoA);
            if(MA.V_LOA == 0)
            {
                EoA = new Target(dEoA, 0, true, this);
                SvL = new Target(dSvL, 0, false, this);
                var rs = MA.Q_OVERLAP == 1 ? MA.V_RELEASEOL : MA.V_RELEASEDP;
                switch(rs)
                {
                    case 126:
                        float rsob = float.MaxValue;
                        if(Targets!=null) foreach(var t in MRSPTargets.FindAll(x => x.dEBD!=null))
                        {
                            if(t.Distance > dTrip)
                            {
                                rsob = Math.Min(rsob, t.vEBD(dTrip));
                            }
                        }
                        if(rsob==float.MaxValue) rsob = 0;
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
                    if (TSRs[i].NID_TSR == TSR.NID_TSR && TSRs[i].NID_TSR!=255)
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
            for(int i = 0; i<TSRs.Count; i++)
            {
                if(TSRs[i].NID_TSR == TSR.NID_TSR && TSRs[i].NID_TSR!=255)
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
            if(link!=null&&link.D_LINK<dMinFront)
            {
                link = null;
                switch (link.Q_LINKREACTION)
                {
                    case 0:
                        Messages.Add(new ETCSMessage("Datos de eurobaliza no consistentes", ClockTime(), () => CurrentETCSMode != ETCSMode.TR && CurrentETCSMode != ETCSMode.PT, 1, false));
                        Trip();
                        return;
                    case 1:
                        Messages.Add(new ETCSMessage("Datos de eurobaliza no consistentes", ClockTime(), () => ETCSLinkingBrake = false, 1, false));
                        ETCSLinkingBrake = true;
                        break;
                }
            }
            if (CurrentETCSMode == ETCSMode.SR )
            {
                if(SRsp==null||SRsp.Distance + SRsp.Length<dMaxFront)
                {
                    SRsp = null;
                    Trip();
                }
            }
            if (CurrentETCSMode != ETCSMode.SR && SRsp!=null)
            {
                SRsp = null;
            }
            if(OverrideEoA && OVsp.Distance + OVsp.Length <dMinFront)
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
                if(dEoA < dMinFront)
                {
                    Messages.Add(new ETCSMessage("EoA o LoA rebasado", ClockTime(), () => CurrentETCSMode != ETCSMode.TR && CurrentETCSMode != ETCSMode.PT, 1, false));
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
                        ETCSMode TransitionMode = CurrentETCSMode;
                        switch (MP.M_MAMODE[i])
                        {
                            case 0:
                                TransitionMode = ETCSMode.OS;
                                break;
                            case 1:
                                TransitionMode = ETCSMode.SH;
                                break;
                            case 2:
                                TransitionMode = ETCSMode.LS;
                                break;
                        }
                        if (CurrentETCSMode != TransitionMode && !AcknowledgeMP)
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
                            CurrentETCSMode = TransitionMode;
                            Messages.Remove(a);
                        }
                        SetVigilanceAlarmDisplay(AcknowledgeMP);
                        SetVigilanceEmergencyDisplay(AcknowledgeMP);
                        if (MP.D_MAMODE[i] < dMaxFront && AcknowledgeMP) ETCSServiceBrake = true;
                        if (MP.D_MAMODE[i] < dMaxFront) CurrentETCSMode = TransitionMode;
                    }
                }
                for (int i = 0; i < TSRs.Count; i++)
                {
                    if (TSRs[i].L_TSR+TSRs[i].D_TSR<dMinFront)
                    {
                        TSRs.RemoveAt(i);
                        TSRsp.RemoveAt(i);
                    }
                }
            }
            if(MRSPS!=null) MRSPS.RemoveAll(x => x.Distance + x.Length < dMinFront);
        }
        List<SpeedProfile> Last;
        protected void SpeedProfiles()
        {
            SpdProf.Clear();
            SpdProf.Add(new SpeedProfile(TrainMaxSpeed, 0, float.PositiveInfinity));
            if (CurrentETCSMode == ETCSMode.FS || CurrentETCSMode == ETCSMode.OS) SpdProf.Add(MAsp);
            if (CurrentETCSMode == ETCSMode.FS || CurrentETCSMode == ETCSMode.OS) SpdProf.AddRange(SSPsp);
            if (CurrentETCSMode == ETCSMode.LS || CurrentETCSMode == ETCSMode.OS || CurrentETCSMode == ETCSMode.SH) SpdProf.AddRange(MPsp);
            if (CurrentETCSMode == ETCSMode.SR) SpdProf.Add(SRsp);
            if (OverrideEoA) SpdProf.Add(OVsp);
            if (TSRsp != null) SpdProf.AddRange(TSRsp);
            SpdProf.RemoveAll(x => x == null);
            SpdProf.RemoveAll(x => x.Distance + x.Length < dMinFront);
            if (Last==null||/*Last.Count!=SpdProf.Count*/true)
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
            sps.RemoveAll(x => x.Distance + x.Length<=dMinFront);
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
            for(int i=0; i<sps.Count; i++)
            {
                if(sps[i].Distance <= Dist && sps[i].Length + sps[i].Distance > Dist)
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
            public static bool IsOK
            {
                get
                {
                    return Length > -1 && PF > -1 && R > -1 && T_be > -1 && T_bs > -1 && T_traction > -1 && Driver_id > -1 && Train_number > -1;
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
            public Target(float Distance, float TargetSpeed, bool SBD, TCS_Spain tcs)
            {
                this.TargetSpeed = TargetSpeed;
                this.Distance  = Distance;
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
                if(targ != null)
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
        bool TCO = false;
        bool SB = false;
        bool EB = false;
        Target EoA = null, SvL = null, LoA = null;
        List<Target> MRSPTargets;
        protected void SupervisedTargets()
        {
            Targets = new List<Target>();
            if (CurrentETCSMode == ETCSMode.FS || CurrentETCSMode == ETCSMode.OS || CurrentETCSMode == ETCSMode.LS || CurrentETCSMode == ETCSMode.SR || CurrentETCSMode == ETCSMode.UN) Targets.AddRange(MRSPTargets);
            if (MA != null)
            {
                if (MA.V_LOA == 0)
                {
                    Targets.Add(EoA);
                    Targets.Add(SvL);
                }
                else Targets.Add(LoA);
            }
            if (CurrentETCSMode == ETCSMode.SR) Targets.Add(new Target(SRsp.Distance + SRsp.Length, 0, true, this));
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
            ETCSVperm = Vmrsp;
            ETCSVsbi = Vmrsp + dVsbi(Vmrsp);
            ETCSVrelease = 0;
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
                if(MRDT != null && NationalValues.A_MAXREDADH1 == 63)
                {
                    ETCSVtarget = MRDT.TargetSpeed;
                    dTarget = MRDT.dP(MRDT.TargetSpeed);
                }
                if(dIndication<float.MaxValue && NationalValues.A_MAXREDADH1 == 62)
                {
                    TTI = (dIndication - dEstFront) / SpeedMpS();
                }
            }
            bool c1 = dIndication < dEstFront && SpeedMpS() >= ETCSVrelease;
            bool c2 = dStartRSM < dEstFront;
            bool c3 = !Targets.Contains(MRDT) && !c1 && !c2;
            bool c4 = (Targets == null || PreviousTargets == null ||Targets.Count != PreviousTargets.Count) && c1 && !c2;
            bool c5 = (Targets == null || PreviousTargets == null || Targets.Count != PreviousTargets.Count) && c2;
            PreviousTargets = Targets;
            if((Monitor == Monitors.CSM && c1) || c4)
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
                                    if (PreviousMRDT != null && MRDT!=PreviousMRDT && !((PreviousMRDT==EoA || PreviousMRDT == SvL)&&(MRDT == LoA || MRDT == SvL)) ) TriggerSoundInfo1();
                                    break;
                                }
                            }
                            break;
                        }
                        else n++;
                    }
                    if (MRDT == null) MRDT = PreviousMRDT;
                    else if (PreviousMRDT != null && MRDT != null && PreviousMRDT != MRDT) TriggerSoundInfo1();
                }
                foreach (var t in Targets)
                {
                    ETCSVperm = Math.Min(ETCSVperm, Math.Max(t == EoA ? t.vP(dEstFront) : t.vP(dMaxFront), t.TargetSpeed));
                    if (t.vSBI1 != null) ETCSVsbi = Math.Min(ETCSVsbi, Math.Max(t.vSBI1(dEstFront), t.TargetSpeed + dVsbi(t.TargetSpeed)));
                    if (t.vSBI2 != null) ETCSVsbi = Math.Min(ETCSVsbi, Math.Max(t.vSBI2(dMaxFront), t.TargetSpeed + dVsbi(t.TargetSpeed)));
                }
                ETCSVtarget = MRDT.TargetSpeed;
                if (MRDT == EoA || MRDT == SvL) ETCSVrelease = MRDT.ReleaseSpeed;
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
                bool t1 = SpeedMpS() <= ETCSVrelease;
                bool t2 = SpeedMpS() > ETCSVrelease;
                bool r0 = SpeedMpS() < 0.1;
                bool r1 = SpeedMpS() < ETCSVrelease;
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
                        bool t3 = t.ReleaseSpeed < SpeedMpS() && SpeedMpS() <= Vmrsp && t.dI(SpeedMpS()) < (t==EoA ? dEstFront : dMaxFront) && t.dP(SpeedMpS()) >= (t==EoA ? dEstFront : dMaxFront);
                        bool t4 = t.ReleaseSpeed < SpeedMpS() && SpeedMpS() <= Vmrsp && t.dP(SpeedMpS()) < (t==EoA ? dEstFront : dMaxFront);
                        bool t6 = Vmrsp < SpeedMpS() && SpeedMpS() <= Vmrsp + dVw(Vmrsp) && t.dI(SpeedMpS()) < (t==EoA ? dEstFront : dMaxFront) && t.dW(SpeedMpS()) >= (t==EoA ? dEstFront : dMaxFront);
                        bool t7 = t.ReleaseSpeed + dVw(t.ReleaseSpeed) < SpeedMpS() && SpeedMpS() <= Vmrsp + dVsbi(Vmrsp) && (t==EoA ? dEstFront : dMaxFront) > t.dW(SpeedMpS());
                        bool t9 = Vmrsp + dVw(Vmrsp) < SpeedMpS() && SpeedMpS() <= Vmrsp + dVsbi(Vmrsp) && t.dI(SpeedMpS()) < (t==EoA ? dEstFront : dMaxFront) && (t.dSBI1 != null ? t.dSBI1(SpeedMpS()) : t.dSBI2(SpeedMpS())) >= (t==EoA ? dEstFront : dMaxFront);
                        bool t10 = t.ReleaseSpeed + dVsbi(t.ReleaseSpeed) < SpeedMpS() && SpeedMpS() <= Vmrsp + dVsbi(Vmrsp) && (t.dSBI1 != null ? t.dSBI1(SpeedMpS()) : t.dSBI2(SpeedMpS())) < (t==EoA ? dEstFront : dMaxFront);
                        bool t12 = Vmrsp + dVsbi(Vmrsp) < SpeedMpS() && SpeedMpS() <= Vmrsp + dVebi(Vmrsp) && t.dI(SpeedMpS()) < (t==EoA ? dEstFront : dMaxFront) && t.dEBI(SpeedMpS()) >= (t==EoA ? dEstFront : dMaxFront);
                        bool t13 = t.ReleaseSpeed + dVebi(t.ReleaseSpeed) < SpeedMpS() && SpeedMpS() <= Vmrsp + dVebi(Vmrsp) && t.dEBI(SpeedMpS()) < (t==EoA ? dEstFront : dMaxFront);
                        bool t15 = SpeedMpS() > Vmrsp + dVebi(Vmrsp) && t.dI(SpeedMpS()) < (t==EoA ? dEstFront : dMaxFront);
                        bool r0 = SpeedMpS() < 0.1;
                        bool r1 = SpeedMpS() < t.ReleaseSpeed;
                        bool r3 = t.ReleaseSpeed < SpeedMpS() && SpeedMpS() <= Vmrsp && t.dP(SpeedMpS()) >= (t==EoA ? dEstFront : dMaxFront);
                        if ((t.dSBI1 != null ? t.dSBI1(t.ReleaseSpeed) : t.dSBI2(t.ReleaseSpeed)) < (t==EoA ? dEstFront : dMaxFront)) Monitor = Monitors.RSM;
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
                if (SB&&!EB&&!ETCSEmergencyBraking) TriggerSoundInfo1();
                SB = false;
            }
            if (rEB)
            {
                if (EB) TriggerSoundInfo1();
                EB = false;
            }
            if (rTCO) TCO = false;
            if (SB && !ETCSServiceBrake && !pSB)
            {
                TriggerSoundInfo1();
                Messages.Add(new ETCSMessage("Freno de servicio aplicado", ClockTime(), () => !ETCSServiceBrake, 1, false));
            }
            if (Monitor == Monitors.TSM && !NationalValues.Q_NVSBTSMPERM) ETCSEmergencyBraking |= SB;
            else ETCSServiceBrake |= SB;
            if (EB && !ETCSEmergencyBraking && !pEB) TriggerSoundInfo1();
            ETCSEmergencyBraking |= EB;
            if (Intervention) Monitoring = MonitoringStatus.Intervention;
            else if (Warning) Monitoring = MonitoringStatus.Warning;
            else if (Overspeed)
            {
                if (Monitor == Monitors.TSM && Monitoring==MonitoringStatus.Indication) TriggerSoundAlert1();
                Monitoring = MonitoringStatus.Overspeed;
            }
            else if (Indication) Monitoring = MonitoringStatus.Indication;
            else if (Normal) Monitoring = MonitoringStatus.Normal;
        }
        protected void ETCSCurves()
        {
            if (Monitoring==MonitoringStatus.Warning)
            {
                SetOverspeedWarningDisplay(true);
                TriggerSoundWarning1();
            }
            else
            {
                SetOverspeedWarningDisplay(false);
                TriggerSoundWarning2();
            }
            SetInterventionSpeedLimitMpS(ETCSVsbi);
            SetCurrentSpeedLimitMpS(ETCSVperm);
            if (Monitor != Monitors.CSM || NationalValues.A_MAXREDADH1 == 63) SetNextSpeedLimitMpS(ETCSVtarget);
            else SetNextSpeedLimitMpS(ETCSVperm);
            if(ETCSVperm<ETCSVrelease)
            {
                SetCurrentSpeedLimitMpS(ETCSVrelease);
                SetNextSpeedLimitMpS(ETCSVperm);
            }
            if (Monitoring == MonitoringStatus.Indication) SetMonitoringStatus(MonitoringStatus.Overspeed);
            else if (Monitoring == MonitoringStatus.Overspeed) SetMonitoringStatus(MonitoringStatus.Warning);
            else SetMonitoringStatus(Monitoring);
            SetOverspeedWarningDisplay(Monitoring == MonitoringStatus.Overspeed || Monitoring == MonitoringStatus.Warning);
            //SetReleaseSpeedMpS(ETCSVrelease);
            if(TTI>0 && FixedData.T_dispTTI<TTI && NationalValues.A_MAXREDADH1 == 62)
            {
                for(int n=1; n<=10; n++)
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
        protected void ATF(float limit)
		{
            limit = limit - MpS.FromKpH(1);
            ATFAcceleration = (SpeedMpS() - LastMpS) / (ClockTime() - LastTime);
            float diff = limit - (SpeedMpS() + ATFAcceleration * 0.7f);
            if (diff < -0.15)
            {
                if (ATFThrottle > 0)
                {
                    ATFThrottle = Math.Max(ATFThrottle - 0.05f, 0);
                }
                else
                {
                    ATFBrake = Math.Min(ATFBrake + 0.05f, 1.5f);
                }
            }
            if (diff > 0.15)
            {
                if (ATFBrake > 0)
                {
                    ATFBrake = Math.Max(ATFBrake - 0.05f, 0);
                }
                else
                {
                    ATFThrottle = Math.Min(ATFThrottle + 0.05f, 1);
#if _OR_PERS
                    if(Locomotive.WheelSlip) ATFThrottle = Math.Max(ATFThrottle - 0.1f, 0);
#endif
                }
            }
#if _OR_PERS
            Locomotive.ThrottleIntervention = ATFThrottle != 0 ? ATFThrottle : 0;
            Locomotive.DynamicBrakeIntervention = ATFBrake > 0 ? ATFBrake : -1;
            Locomotive.TrainBrakeIntervention = Math.Max(ATFBrake - 1, 0);
#else
            SetThrottleController(ATFThrottle);
            try
            {
                
                ATFFullBrake = ATFBrake > 1 && ATFAcceleration > -0.2;
                SetDynamicBrakeController(Math.Min(ATFBrake, 1));
            }
            catch (Exception)
            {
                if (ATFBrake >= 1) SetEmergencyBrake(true);
                else if (ATFBrake > 0.3) ATFFullBrake = true;
            }
#endif
            LastMpS = SpeedMpS();
            LastTime = ClockTime();
		}
		public override void SetEmergency(bool emergency)
        {
            ExternalEmergencyBraking = emergency;
        }
		public override void HandleEvent(TCSEvent evt, string message)
   		{
			switch (evt)
            {
                case TCSEvent.AlerterPressed:
                    HMPressed = true;
					ASFAPressed = true;
                    if (ActiveCCS == CCS.ETCS) TriggerSoundInfo2();
                    break;
                case TCSEvent.AlerterReleased:
                    HMPressed = false;
					ASFAPressed = false;
                    ETCSPressed = true;
                    break;
            }		
		}
		protected void UpdateHM()
        {
            if (!Activated || !IsAlerterEnabled()
#if _OR_PERS
                || Locomotive.Direction == Direction.N
#endif
                )
            {
                HMReleasedAlertTimer.Stop();
                HMReleasedEmergencyTimer.Stop();
                HMPressedAlertTimer.Stop();
                HMPressedEmergencyTimer.Stop();
                HMEmergencyBraking = false;
				if (AlerterSound()) SetVigilanceAlarm(false);
				SetVigilanceAlarmDisplay(false);
				SetVigilanceEmergencyDisplay(false);
                return;
            }
            if (HMPressed && (!HMPressedAlertTimer.Started || !HMPressedEmergencyTimer.Started))
            {
                HMReleasedAlertTimer.Stop();
                HMReleasedEmergencyTimer.Stop();
                HMPressedAlertTimer.Start();
                HMPressedEmergencyTimer.Start();
				if (!AlerterSound()) SetVigilanceAlarm(false);
				SetVigilanceAlarmDisplay(false);
            }
			if(HMPressed && HMPressedAlertTimer.RemainingValue<2.5f)
			{
				SetVigilanceAlarmDisplay(true);
			}
            if (!HMPressed && (!HMReleasedAlertTimer.Started || !HMReleasedEmergencyTimer.Started))
            {
                HMReleasedAlertTimer.Start();
                HMReleasedEmergencyTimer.Start();
                HMPressedAlertTimer.Stop();
                HMPressedEmergencyTimer.Stop();
				if (AlerterSound()) SetVigilanceAlarm(false);
				SetVigilanceAlarmDisplay(true);
            }
            if (HMReleasedAlertTimer.Triggered || HMPressedAlertTimer.Triggered)
			{
				if (!AlerterSound()) SetVigilanceAlarm(true);
				SetVigilanceAlarmDisplay(true);
			}
			else
			{
				if (AlerterSound()) SetVigilanceAlarm(false);
			}
            if (!HMEmergencyBraking && (HMPressedEmergencyTimer.Triggered || HMReleasedEmergencyTimer.Triggered))
            {
                HMEmergencyBraking = true;
				if (AlerterSound()) SetVigilanceAlarm(false);
				SetVigilanceAlarmDisplay(false);
                SetVigilanceEmergencyDisplay(true);
            }
            if (HMEmergencyBraking && SpeedMpS() < 1.5f)
            {
                HMEmergencyBraking = false;
                SetVigilanceEmergencyDisplay(false);
            }
        }
		protected void UpdateSignalPassed()
        {
            SignalPassed = (NextSignalDistanceM(0) > PreviousSignalDistanceM+20)&&(SpeedMpS()>0.1f);
            PreviousSignalDistanceM = NextSignalDistanceM(0);
            if (SignalPassed && NextSignalAspect(0) == Aspect.None) SignalPassed = false;
            UpdateSignalBalisePassed();
        }
        bool SBalisePassed;
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
		protected void UpdateDistanciaPrevia()
		{
			if(SignalPassed)
			{
				if((NextSignalAspect(0)==Aspect.Clear_2&&NextSignalSpeedLimitMpS(0)<MpS.FromKpH(165f)&&NextSignalSpeedLimitMpS(0)>MpS.FromKpH(155f))||(NextSignalAspect(0)==Aspect.Approach_1&&NextSignalSpeedLimitMpS(0)<MpS.FromKpH(35f)&&NextSignalSpeedLimitMpS(0)>MpS.FromKpH(25f)))
				{
					PreviaSignalNumber = 1;
					IsPN = true;
				}
				else PreviaSignalNumber = 0;
				if((CurrentSignalSpeedLimitMpS()>165f||CurrentSignalSpeedLimitMpS()<155f||CurrentSignalAspect!=Aspect.Clear_2)&&((CurrentSignalSpeedLimitMpS()>35f||CurrentSignalSpeedLimitMpS()<25f||CurrentSignalAspect!=Aspect.Approach_1)))
				{
					if(NextSignalDistanceM(PreviaSignalNumber)<100f)
					{
						PreviaDistance = 0f;
					}
					else if(NextSignalDistanceM(PreviaSignalNumber)<400f)
					{
						PreviaDistance = 50f;
					}
					else if(NextSignalDistanceM(PreviaSignalNumber)<700f)
					{
						PreviaDistance = 100f;
					}
					else
					{
						PreviaDistance = 300f;
					}
				}
				if(!LineaConvencional)
				{
					PreviaSignalNumber = 0;
					if(NextSignalDistanceM(0)<100f)
					{
						PreviaDistance = 0f;
					}
					else if(NextSignalDistanceM(0)<700f)
					{
						PreviaDistance = 100f;
					}
					else if(NextSignalDistanceM(0)<1000f)
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
			if((NextSignalAspect(0)==Aspect.Clear_2&&NextSignalSpeedLimitMpS(0)<MpS.FromKpH(165f)&&NextSignalSpeedLimitMpS(0)>MpS.FromKpH(155f))||(NextSignalAspect(0)==Aspect.Approach_1&&NextSignalSpeedLimitMpS(0)<MpS.FromKpH(35f)&&NextSignalSpeedLimitMpS(0)>MpS.FromKpH(25f))) IsPN = true;
			if(!SignalPassed&&!ASFARec.Started&&IsPN&&!((NextSignalAspect(0)==Aspect.Clear_2&&NextSignalSpeedLimitMpS(0)<MpS.FromKpH(165f)&&NextSignalSpeedLimitMpS(0)>MpS.FromKpH(155f))||(NextSignalAspect(0)==Aspect.Approach_1&&NextSignalSpeedLimitMpS(0)<MpS.FromKpH(35f)&&NextSignalSpeedLimitMpS(0)>MpS.FromKpH(25f)))) IsPN = false;
			if(PreviaDistance!=0f) UpdatePreviaPassed();
			else PreviaPassed = false;
		}
		protected void UpdateBalizaASFAPassed()
		{
			BalizaASFAPassed = PreviaPassed||SignalPassed||AnuncioLTVPassed; 
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
		protected void SetVelocidadesASFA()
		{
			if(TipoASFA == Tipo_ASFA.Digital)
			{
				if(Math.Min(TrainMaxSpeed, ASFATipoTren)>=MpS.FromKpH(190f)) ASFATipoTren = MpS.FromKpH(200f);
				else if(Math.Min(TrainMaxSpeed, ASFATipoTren)>=MpS.FromKpH(170f)) ASFATipoTren = MpS.FromKpH(180f);
				else if(Math.Min(TrainMaxSpeed, ASFATipoTren)>=MpS.FromKpH(150f)) ASFATipoTren = MpS.FromKpH(160f);
				else if(Math.Min(TrainMaxSpeed, ASFATipoTren)>=MpS.FromKpH(130f)) ASFATipoTren = MpS.FromKpH(140f);
				else if(Math.Min(TrainMaxSpeed, ASFATipoTren)>=MpS.FromKpH(110f)) ASFATipoTren = MpS.FromKpH(120f);
				else if(Math.Min(TrainMaxSpeed, ASFATipoTren)>=MpS.FromKpH(95f)) ASFATipoTren = MpS.FromKpH(100f);
				else if(Math.Min(TrainMaxSpeed, ASFATipoTren)>=MpS.FromKpH(85f)) ASFATipoTren = MpS.FromKpH(90f);
				else if(Math.Min(TrainMaxSpeed, ASFATipoTren)<=MpS.FromKpH(85f)) ASFATipoTren = MpS.FromKpH(80f);
				if(ASFATipoTren>=MpS.FromKpH(160f))
				{
					ASFAVCControlArranque = MpS.FromKpH(140f);
					ASFAVCIVlCond = ASFATipoTren;
					ASFAVCFVlCond = MpS.FromKpH(160f);
					ASFAVCIAnPar = MpS.FromKpH(160f);
					ASFAVCFAnPar = MpS.FromKpH(80f);
					ASFAVCFAnPre = MpS.FromKpH(80f);
					ASFAVCFAnPreAumVel = MpS.FromKpH(100f);
					ASFAVCFPrePar = MpS.FromKpH(80f);
					ASFAVCFPreParAumVel = MpS.FromKpH(100f);
					ASFAVCISecPreParA = MpS.FromKpH(80f);
					ASFAVCFSecPreParA = MpS.FromKpH(60f);
					ASFAVCISecPreParA1AumVel = MpS.FromKpH(100f);
					ASFAVCFSecPreParA1AumVel = MpS.FromKpH(90f);
					ASFAVCISecPreParA2AumVel = MpS.FromKpH(90f);
					ASFAVCFSecPreParA2AumVel = MpS.FromKpH(80f);
					ASFAVCFLTV = MpS.FromKpH(60f);
					ASFAVCFLTVAumVel = MpS.FromKpH(100f);
					ASFAVCFPN = MpS.FromKpH(30f);
					ASFAVCIPar = MpS.FromKpH(60f);
					ASFAVCFPar = MpS.FromKpH(30f);
					ASFAVCPar = MpS.FromKpH(40f);
					ASFAVCParAumVel = MpS.FromKpH(100f);
					ASFAVCDesv = MpS.FromKpH(60f);
					ASFAVCDesvAumVel = MpS.FromKpH(90f);
					
					ASFAVCVlCondTReac = 7.5f;
					ASFAVIVlCondTReac = 9f;
					ASFAVCVlCondDec = 0.55f;
					ASFAVIVlCondDec = 0.5f;
					ASFAVCAnParTReac = 7.5f;
					ASFAVIAnParTReac = 9f;
					ASFAVCAnParDec = 0.6f;
					ASFAVIAnParDec = 0.5f;
					ASFAVCParTReac = 1.5f;
					ASFAVIParTReac = 3.5f;
					ASFAVCParDec = 0.6f;
					ASFAVIParDec = 0.55f;
					ASFAVCSecPreParATReac = 3.5f;
					ASFAVISecPreParATReac = 5f;
					ASFAVCSecPreParA1AumVelTReac = 3.5f;
					ASFAVISecPreParA1AumVelTReac = 9f;
					ASFAVCSecPreParA2AumVelTReac = 7.5f;
					ASFAVISecPreParA2AumVelTReac = 9f;
					ASFAVCSecPreParADec = 0.6f;
					ASFAVISecPreParADec = 0.5f;
					
					ASFAVCFAnParAV = MpS.FromKpH(100f);
					ASFAVCFAnPreAV = MpS.FromKpH(120f);
					ASFAVCFAnPreAumVelAV = MpS.FromKpH(160f);
					ASFAVCFPreParAV = MpS.FromKpH(100f);
					ASFAVCFPreParAumVelAV = MpS.FromKpH(120f);
					ASFAVCDesvAV = MpS.FromKpH(100f);
					ASFAVCDesvAumVelAV = MpS.FromKpH(160f);
					ASFAVCFLTVAV = MpS.FromKpH(100f);
					ASFAVCFLTVAumVelAV = MpS.FromKpH(160f);
					ASFAVCISecPreParAAV = MpS.FromKpH(100f);
					ASFAVCFSecPreParAAV = MpS.FromKpH(100f);
					ASFAVCISecPreParA1AumVelAV = MpS.FromKpH(140f);
					ASFAVCFSecPreParA1AumVelAV = MpS.FromKpH(120f);
					ASFAVCISecPreParA2AumVelAV = MpS.FromKpH(120f);
					ASFAVCFSecPreParA2AumVelAV = MpS.FromKpH(100f);
				}
				else if(ASFATipoTren>=MpS.FromKpH(140f))
				{
					ASFAVCControlArranque = MpS.FromKpH(140f);
					ASFAVCIVlCond = ASFATipoTren;
					ASFAVCFVlCond = ASFATipoTren;
					ASFAVCIAnPar = ASFATipoTren;
					ASFAVCFAnPar = MpS.FromKpH(80f);
					ASFAVCFAnPre = MpS.FromKpH(80f);
					ASFAVCFAnPreAumVel = MpS.FromKpH(100f);
					ASFAVCFPrePar = MpS.FromKpH(80f);
					ASFAVCFPreParAumVel = MpS.FromKpH(100f);
					ASFAVCISecPreParA = MpS.FromKpH(80f);
					ASFAVCFSecPreParA = MpS.FromKpH(60f);
					ASFAVCISecPreParA1AumVel = MpS.FromKpH(100f);
					ASFAVCFSecPreParA1AumVel = MpS.FromKpH(90f);
					ASFAVCISecPreParA2AumVel = MpS.FromKpH(90f);
					ASFAVCFSecPreParA2AumVel = MpS.FromKpH(80f);
					ASFAVCFLTV = MpS.FromKpH(60f);
					ASFAVCFLTVAumVel = MpS.FromKpH(100f);
					ASFAVCFPN = MpS.FromKpH(30f);
					ASFAVCIPar = MpS.FromKpH(60f);
					ASFAVCFPar = MpS.FromKpH(30f);
					ASFAVCPar = MpS.FromKpH(40f);
					ASFAVCParAumVel = MpS.FromKpH(100f);
					ASFAVCDesv = MpS.FromKpH(60f);
					ASFAVCDesvAumVel = MpS.FromKpH(90f);
					
					ASFAVCVlCondTReac = 0f;
					ASFAVIVlCondTReac = 0f;
					ASFAVCVlCondDec = 0f;
					ASFAVIVlCondDec = 0f;
					ASFAVCAnParTReac = 7.5f;
					ASFAVIAnParTReac = 10f;
					ASFAVCAnParDec = 0.6f;
					ASFAVIAnParDec = 0.5f;
					ASFAVCParTReac = 1.5f;
					ASFAVIParTReac = 3.5f;
					ASFAVCParDec = 0.6f;
					ASFAVIParDec = 0.55f;
					ASFAVCSecPreParATReac = 3.5f;
					ASFAVISecPreParATReac = 5f;
					ASFAVCSecPreParA1AumVelTReac = 3.5f;
					ASFAVISecPreParA1AumVelTReac = 5f;
					ASFAVCSecPreParA2AumVelTReac = 7.5f;
					ASFAVISecPreParA2AumVelTReac = 10f;
					ASFAVCSecPreParADec = 0.6f;
					ASFAVISecPreParADec = 0.5f;
					
					ASFAVCFAnParAV = MpS.FromKpH(100f);
					ASFAVCFAnPreAV = MpS.FromKpH(120f);
					ASFAVCFAnPreAumVelAV = ASFATipoTren;
					ASFAVCFPreParAV = MpS.FromKpH(100f);
					ASFAVCFPreParAumVelAV = MpS.FromKpH(120f);
					ASFAVCDesvAV = MpS.FromKpH(100f);
					ASFAVCDesvAumVelAV = ASFATipoTren;
					ASFAVCFLTVAV = MpS.FromKpH(100f);
					ASFAVCFLTVAumVelAV = ASFATipoTren;
					ASFAVCISecPreParAAV = MpS.FromKpH(100f);
					ASFAVCFSecPreParAAV = MpS.FromKpH(100f);
					ASFAVCISecPreParA1AumVelAV = MpS.FromKpH(140f);
					ASFAVCFSecPreParA1AumVelAV = MpS.FromKpH(120f);
					ASFAVCISecPreParA2AumVelAV = MpS.FromKpH(120f);
					ASFAVCFSecPreParA2AumVelAV = MpS.FromKpH(100f);
				}
				else if(ASFATipoTren>=MpS.FromKpH(120f))
				{
					ASFAVCControlArranque = ASFATipoTren;
					ASFAVCIVlCond = ASFATipoTren;
					ASFAVCFVlCond = ASFATipoTren;
					ASFAVCIAnPar = ASFATipoTren;
					ASFAVCFAnPar = MpS.FromKpH(80f);
					ASFAVCFAnPre = MpS.FromKpH(80f);
					ASFAVCFAnPreAumVel = MpS.FromKpH(100f);
					ASFAVCFPrePar = MpS.FromKpH(80f);
					ASFAVCFPreParAumVel = MpS.FromKpH(100f);
					ASFAVCISecPreParA = MpS.FromKpH(80f);
					ASFAVCFSecPreParA = MpS.FromKpH(60f);
					ASFAVCISecPreParA1AumVel = MpS.FromKpH(100f);
					ASFAVCFSecPreParA1AumVel = MpS.FromKpH(90f);
					ASFAVCISecPreParA2AumVel = MpS.FromKpH(90f);
					ASFAVCFSecPreParA2AumVel = MpS.FromKpH(80f);
					ASFAVCFLTV = MpS.FromKpH(60f);
					ASFAVCFLTVAumVel = MpS.FromKpH(100f);
					ASFAVCFPN = MpS.FromKpH(30f);
					ASFAVCIPar = MpS.FromKpH(60f);
					ASFAVCFPar = MpS.FromKpH(30f);
					ASFAVCPar = MpS.FromKpH(40f);
					ASFAVCParAumVel = MpS.FromKpH(100f);
					ASFAVCDesv = MpS.FromKpH(60f);
					ASFAVCDesvAumVel = MpS.FromKpH(90f);
					
					ASFAVCVlCondTReac = 0f;
					ASFAVIVlCondTReac = 0f;
					ASFAVCVlCondDec = 0f;
					ASFAVIVlCondDec = 0f;
					ASFAVCAnParTReac = 7.5f;
					ASFAVIAnParTReac = 12f;
					ASFAVCAnParDec = 0.46f;
					ASFAVIAnParDec = 0.36f;
					ASFAVCParTReac = 1.5f;
					ASFAVIParTReac = 3.5f;
					ASFAVCParDec = 0.6f;
					ASFAVIParDec = 0.55f;
					ASFAVCSecPreParATReac = 3.5f;
					ASFAVISecPreParATReac = 5f;
					ASFAVCSecPreParA1AumVelTReac = 3.5f;
					ASFAVISecPreParA1AumVelTReac = 5f;
					ASFAVCSecPreParA2AumVelTReac = 7.5f;
					ASFAVISecPreParA2AumVelTReac = 12f;
					ASFAVCSecPreParADec = 0.46f;
					ASFAVISecPreParADec = 0.36f;
					
					ASFAVCFAnParAV = MpS.FromKpH(100f);
					ASFAVCFAnPreAV = MpS.FromKpH(120f);
					ASFAVCFAnPreAumVelAV = ASFATipoTren;
					ASFAVCFPreParAV = MpS.FromKpH(100f);
					ASFAVCFPreParAumVelAV = MpS.FromKpH(120f);
					ASFAVCDesvAV = MpS.FromKpH(100f);
					ASFAVCDesvAumVelAV = ASFATipoTren;
					ASFAVCFLTVAV = MpS.FromKpH(100f);
					ASFAVCFLTVAumVelAV = ASFATipoTren;
					ASFAVCISecPreParAAV = MpS.FromKpH(100f);
					ASFAVCFSecPreParAAV = MpS.FromKpH(100f);
					ASFAVCISecPreParA1AumVelAV = ASFATipoTren;
					ASFAVCFSecPreParA1AumVelAV = ASFATipoTren;
					ASFAVCISecPreParA2AumVelAV = ASFATipoTren;
					ASFAVCFSecPreParA2AumVelAV = ASFATipoTren;
				}
				else if(ASFATipoTren>=MpS.FromKpH(110f))
				{
					ASFAVCControlArranque = ASFATipoTren;
					ASFAVCIVlCond = ASFATipoTren;
					ASFAVCFVlCond = ASFATipoTren;
					ASFAVCIAnPar = ASFATipoTren;
					ASFAVCFAnPar = MpS.FromKpH(80f);
					ASFAVCFAnPre = MpS.FromKpH(80f);
					ASFAVCFAnPreAumVel = MpS.FromKpH(100f);
					ASFAVCFPrePar = MpS.FromKpH(80f);
					ASFAVCFPreParAumVel = MpS.FromKpH(100f);
					ASFAVCISecPreParA = MpS.FromKpH(80f);
					ASFAVCFSecPreParA = MpS.FromKpH(60f);
					ASFAVCISecPreParA1AumVel = MpS.FromKpH(100f);
					ASFAVCFSecPreParA1AumVel = MpS.FromKpH(90f);
					ASFAVCISecPreParA2AumVel = MpS.FromKpH(90f);
					ASFAVCFSecPreParA2AumVel = MpS.FromKpH(80f);
					ASFAVCFLTV = MpS.FromKpH(60f);
					ASFAVCFLTVAumVel = MpS.FromKpH(100f);
					ASFAVCFPN = MpS.FromKpH(30f);
					ASFAVCIPar = MpS.FromKpH(60f);
					ASFAVCFPar = MpS.FromKpH(25f);
					ASFAVCPar = MpS.FromKpH(40f);
					ASFAVCParAumVel = MpS.FromKpH(100f);
					ASFAVCDesv = MpS.FromKpH(60f);
					ASFAVCDesvAumVel = MpS.FromKpH(90f);
					
					ASFAVCVlCondTReac = 0f;
					ASFAVIVlCondTReac = 0f;
					ASFAVCVlCondDec = 0f;
					ASFAVIVlCondDec = 0f;
					ASFAVCAnParTReac = 7.5f;
					ASFAVIAnParTReac = 12f;
					ASFAVCAnParDec = 0.46f;
					ASFAVIAnParDec = 0.36f;
					ASFAVCParTReac = 1.5f;
					ASFAVIParTReac = 3.5f;
					ASFAVCParDec = 0.6f;
					ASFAVIParDec = 0.55f;
					ASFAVCSecPreParATReac = 3.5f;
					ASFAVISecPreParATReac = 5f;
					ASFAVCSecPreParA1AumVelTReac = 3.5f;
					ASFAVISecPreParA1AumVelTReac = 5f;
					ASFAVCSecPreParA2AumVelTReac = 7.5f;
					ASFAVISecPreParA2AumVelTReac = 12f;
					ASFAVCSecPreParADec = 0.46f;
					ASFAVISecPreParADec = 0.36f;
					
					ASFAVCFAnParAV = MpS.FromKpH(100f);
					ASFAVCFAnPreAV = MpS.FromKpH(120f);
					ASFAVCFAnPreAumVelAV = ASFATipoTren;
					ASFAVCFPreParAV = MpS.FromKpH(100f);
					ASFAVCFPreParAumVelAV = ASFATipoTren;
					ASFAVCDesvAV = MpS.FromKpH(100f);
					ASFAVCDesvAumVelAV = ASFATipoTren;
					ASFAVCFLTVAV = MpS.FromKpH(100f);
					ASFAVCFLTVAumVelAV = ASFATipoTren;
					ASFAVCISecPreParAAV = MpS.FromKpH(100f);
					ASFAVCFSecPreParAAV = MpS.FromKpH(100f);
					ASFAVCISecPreParA1AumVelAV = ASFATipoTren;
					ASFAVCFSecPreParA1AumVelAV = ASFATipoTren;
					ASFAVCISecPreParA2AumVelAV = ASFATipoTren;
					ASFAVCFSecPreParA2AumVelAV = ASFATipoTren;
				}
				else if(ASFATipoTren<=MpS.FromKpH(100f))
				{
					ASFAVCControlArranque = ASFATipoTren;
					ASFAVCIVlCond = ASFATipoTren;
					ASFAVCFVlCond = ASFATipoTren;
					ASFAVCIAnPar = ASFATipoTren;
					ASFAVCFAnPar = MpS.FromKpH(60f);
					ASFAVCFAnPre = MpS.FromKpH(60f);
					ASFAVCFAnPreAumVel = ASFATipoTren;
					ASFAVCFPrePar = MpS.FromKpH(60f);
					ASFAVCFPreParAumVel = ASFATipoTren;
					ASFAVCISecPreParA = MpS.FromKpH(60f);
					ASFAVCFSecPreParA = MpS.FromKpH(60f);
					ASFAVCISecPreParA1AumVel = ASFATipoTren;
					ASFAVCFSecPreParA1AumVel = MpS.FromKpH(60f);
					ASFAVCISecPreParA2AumVel = ASFATipoTren;
					ASFAVCFSecPreParA2AumVel = MpS.FromKpH(60f);
					ASFAVCFLTV = MpS.FromKpH(60f);
					ASFAVCFLTVAumVel = ASFATipoTren;
					ASFAVCFPN = MpS.FromKpH(30f);
					ASFAVCIPar = MpS.FromKpH(60f);
					ASFAVCFPar = MpS.FromKpH(25f);
					ASFAVCPar = MpS.FromKpH(40f);
					ASFAVCParAumVel = ASFATipoTren;
					ASFAVCDesv = MpS.FromKpH(60f);
					ASFAVCDesvAumVel = MpS.FromKpH(90f);
					
					ASFAVCVlCondTReac = 0f;
					ASFAVIVlCondTReac = 0f;
					ASFAVCVlCondDec = 0f;
					ASFAVIVlCondDec = 0f;
					ASFAVCAnParTReac = 7.5f;
					ASFAVIAnParTReac = 11f;
					ASFAVCAnParDec = 0.36f;
					ASFAVIAnParDec = 0.26f;
					ASFAVCParTReac = 2.5f;
					ASFAVIParTReac = 5.5f;
					ASFAVCParDec = 0.36f;
					ASFAVIParDec = 0.26f;
					ASFAVCSecPreParATReac = 0f;
					ASFAVISecPreParATReac = 0f;
					ASFAVCSecPreParA1AumVelTReac = 2.5f;
					ASFAVISecPreParA1AumVelTReac = 5f;
					ASFAVCSecPreParA2AumVelTReac = 7.5f;
					ASFAVISecPreParA2AumVelTReac = 11f;
					ASFAVCSecPreParADec = 0.36f;
					ASFAVISecPreParADec = 0.26f;
					
					ASFAVCFAnParAV = ASFATipoTren;
					ASFAVCFAnPreAV = ASFATipoTren;
					ASFAVCFAnPreAumVelAV = ASFATipoTren;
					ASFAVCFPreParAV = ASFATipoTren;
					ASFAVCFPreParAumVelAV = ASFATipoTren;
					ASFAVCDesvAV = ASFATipoTren;
					ASFAVCDesvAumVelAV = ASFATipoTren;
					ASFAVCFLTVAV = ASFATipoTren;
					ASFAVCFLTVAumVelAV = ASFATipoTren;
					ASFAVCISecPreParAAV = ASFATipoTren;
					ASFAVCFSecPreParAAV = MpS.FromKpH(80f);
					ASFAVCISecPreParA1AumVelAV = ASFATipoTren;
					ASFAVCFSecPreParA1AumVelAV = ASFATipoTren;
					ASFAVCISecPreParA2AumVelAV = ASFATipoTren;
					ASFAVCFSecPreParA2AumVelAV = ASFATipoTren;
				}
				if(SerieTren == 446)
				{
					ASFAVCControlArranque = MpS.FromKpH(100f);
					ASFAVCIVlCond = MpS.FromKpH(100f);
					ASFAVCFVlCond = MpS.FromKpH(100f);
					ASFAVCIAnPar = MpS.FromKpH(100f);
					ASFAVCFAnPar = MpS.FromKpH(80f);
					ASFAVCFAnPre = MpS.FromKpH(80f);
					ASFAVCFAnPreAumVel = MpS.FromKpH(100f);
					ASFAVCFPrePar = MpS.FromKpH(80f);
					ASFAVCFPreParAumVel = MpS.FromKpH(100f);
					ASFAVCISecPreParA = MpS.FromKpH(80f);
					ASFAVCFSecPreParA = MpS.FromKpH(60f);
					ASFAVCISecPreParA1AumVel = MpS.FromKpH(100f);
					ASFAVCFSecPreParA1AumVel = MpS.FromKpH(90f);
					ASFAVCISecPreParA2AumVel = MpS.FromKpH(90f);
					ASFAVCFSecPreParA2AumVel = MpS.FromKpH(80f);
					ASFAVCFLTV = MpS.FromKpH(60f);
					ASFAVCFLTVAumVel = MpS.FromKpH(100f);
					ASFAVCFPN = MpS.FromKpH(30f);
					ASFAVCIPar = MpS.FromKpH(60f);
					ASFAVCFPar = MpS.FromKpH(30f);
					ASFAVCPar = MpS.FromKpH(40f);
					ASFAVCParAumVel = MpS.FromKpH(100f);
					ASFAVCDesv = MpS.FromKpH(60f);
					ASFAVCDesvAumVel = MpS.FromKpH(90f);
					
					ASFAVCVlCondTReac = 0f;
					ASFAVIVlCondTReac = 0f;
					ASFAVCVlCondDec = 0f;
					ASFAVIVlCondDec = 0f;
					ASFAVCAnParTReac = 7.5f;
					ASFAVIAnParTReac = 12f;
					ASFAVCAnParDec = 0.46f;
					ASFAVIAnParDec = 0.36f;
					ASFAVCParTReac = 1.5f;
					ASFAVIParTReac = 3.5f;
					ASFAVCParDec = 0.6f;
					ASFAVIParDec = 0.55f;
					ASFAVCSecPreParATReac = 3.5f;
					ASFAVISecPreParATReac = 5f;
					ASFAVCSecPreParA1AumVelTReac = 3.5f;
					ASFAVISecPreParA1AumVelTReac = 5f;
					ASFAVCSecPreParA2AumVelTReac = 7.5f;
					ASFAVISecPreParA2AumVelTReac = 12f;
					ASFAVCSecPreParADec = 0.46f;
					ASFAVISecPreParADec = 0.36f;
				}
				if((SerieTren>=100&&SerieTren<200)||SerieTren==252||SerieTren==319||SerieTren==333||SerieTren==334||SerieTren==335) ASFAModoAV = true;
				else ASFAModoAV = false;
				if(SerieTren>=100&&SerieTren<120) ASFAModoCONV = false;
				else ASFAModoCONV = true;
			}
			else
			{
                ASFATipoTren = Math.Min(TrainMaxSpeed, ASFATipoTren);
                if (ASFATipoTren > MpS.FromKpH(160f)) ASFATipoTren = MpS.FromKpH(200f);
                else if (ASFATipoTren >= MpS.FromKpH(110f)) ASFATipoTren = MpS.FromKpH(160);
                else if (ASFATipoTren >= MpS.FromKpH(80f)) ASFATipoTren = MpS.FromKpH(100);
                else ASFATipoTren = MpS.FromKpH(70f);
			}
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
        public TCS_Spain tcs;
        public LZBProfile Profile;
        public List<float> Stops;
        public LZBTrain NextTrain;
        public float LastTime;
        public LZBTrain(float VMT, float TF, float PFT, float LT, float Speed, LZBPosition Position, int Number, TCS_Spain tcs)
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
}