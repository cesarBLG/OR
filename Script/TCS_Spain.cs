//Scripts de sistemas de seguridad Españoles
//César Benito Lamata
//Versión 2.0
using System;
using System.Collections.Generic;
using ORTS.Common;
using ORTS.Scripting.Api;

namespace ORTS.Scripting.Script
{
	public class TCS_Spain : TrainControlSystem
	{
		enum CCS
		{
			ASFA,
			LZB,
			EBICAB,
			ETCS
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
			L1,
			L2,
			L3,
			L4,
			L5,
			L6,
			L7,
			L8,
			L9,
			FP
		}
		
		bool ASFAInstalled;
		bool ASFADigital;
		bool ETCSInstalled;
		bool ATOInstalled;
        bool ASFAActivated;
		
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
		/*Aspect ETCSPreviousSignal;
		Aspect ETCSThisSignal;*/
		int ETCSInstalledLevel;
		/*float MAEnd;
		MovementAuthority MA;
		MovementAuthoritySection[] MAS = new MovementAuthoritySection[40];
		float OSDistAuth = 10000f;
		float OSSpeedAuth = MpS.FromKpH(30f);
		float SRDistAuth = 10000f;
		float SRSpeedAuth = MpS.FromKpH(100f);
		float ETCSCurrentSpeed;
		float ETCSApplicationSpeed;
		float ETCSCurve;
		float ETCSReleaseSpeed;
		int i;
		int a;
		bool ETCSEmergencyBraking;*/
		
		float LastMpS;
        float LastTime;
        float ATOAcceleration;
        float ATOThrottle;
		float ATOBrake;
		bool ATOActivated = false;
        float ATOSpeed;
		
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
        bool SignalPassed = false;
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
		
		float ASFAVCControlArranque = 100f/3.6f;
		float ASFAVCIVlCond = 100f/3.6f;
		float ASFAVCFVlCond = 100f/3.6f;
		float ASFAVCIAnPar = 100f/3.6f;
		float ASFAVCFAnPar = 80f/3.6f;
		float ASFAVCFAnPre = 80f/3.6f;
		float ASFAVCFAnPreAumVel = 100f/3.6f;
		float ASFAVCFPrePar = 80f/3.6f;
		float ASFAVCFPreParAumVel = 100f/3.6f;
		float ASFAVCISecPreParA;
		float ASFAVCFSecPreParA;
		float ASFAVCISecPreParA1AumVel;
		float ASFAVCFSecPreParA1AumVel;
		float ASFAVCISecPreParA2AumVel;
		float ASFAVCFSecPreParA2AumVel;
		float ASFAVCFLTV = 60f/3.6f;
		float ASFAVCFLTVAumVel = 100f/3.6f;
		float ASFAVCFPN;
		float ASFAVCIPar = 60f/3.6f;
		float ASFAVCFPar = 30f/3.6f;
		float ASFAVCPar = 40f/3.6f;
		float ASFAVCParAumVel = 100/3.6f;
		float ASFAVCDesv = 60f/3.6f;
		float ASFAVCDesvAumVel = 90f/3.6f;
		float ASFATipoTren = 100f/3.6f;
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

        OdoMeter FrontEndTrain;
		
		public TCS_Spain(){}
		public override void Initialize()
		{
            /*MAS = new MovementAuthoritySection[1];
			MAS[0] = new MovementAuthoritySection(SRSpeedAuth, SRDistAuth, 0f, this);
			MA = new MovementAuthority(SRDistAuth, 0f, ETCSMode.SR, this, MAS);*/
            ASFAInstalled = GetBoolParameter("General", "ASFA", true);
			ETCSInstalled = GetBoolParameter("General", "ETCS", false);
			HMInhibited = GetBoolParameter("General", "HMInhibited", true);
			ATOActivated = ATOInstalled = GetBoolParameter("General", "ATO", false);
			ASFADigital = GetBoolParameter("ASFA", "Digital", false);
			ETCSInstalledLevel = GetIntParameter("ETCS", "Level", 0);
			ASFAUltimaInfo  = Aspect.None;
			SetNextSignalAspect( ASFAUltimaInfo );
			TrainMaxSpeed = MpS.FromKpH(GetFloatParameter("General", "MaxSpeed", 380f));
			ASFATipoTren = MpS.FromKpH(GetFloatParameter("ASFA", "TipoTren", 250f));
			SerieTren = GetIntParameter("General", "Serie", 440);
			if(ASFATipoTren>MpS.FromKpH(205f)) ASFATipoTren = TrainMaxSpeed;
			ASFASpeed = 0f;
			ASFADisplaySpeed = ASFATargetSpeed = ASFASpeed;
			if(ASFAInstalled)
			{
				ActiveCCS = CCS.ASFA;
				SetNextSpeedLimitMpS(ASFATargetSpeed);
				if(ASFADigital)
				{
					ASFAUrgencia = true;
					TipoASFA = Tipo_ASFA.Digital;
					ASFADigitalModo = ASFADigital_Modo.OFF;
				}
				else
				{
					if(TrainMaxSpeed<=MpS.FromKpH(160f)) TipoASFA = Tipo_ASFA.Basico;
					else TipoASFA = Tipo_ASFA.ASFA200;
				}
			}
			if(ETCSInstalled)
			{
				ActiveCCS = CCS.ETCS;
				/*SetCurrentSpeedLimitMpS(SRSpeedAuth);
				SetNextSpeedLimitMpS(SRSpeedAuth);*/
				switch(ETCSInstalledLevel)
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

            FrontEndTrain = new OdoMeter(this);
        }
		public override void Update()
		{
			UpdateSignalPassed();
			UpdateDistanciaPrevia();
			UpdatePostPassed();
			UpdateAnuncioLTVPassed();
			if(!HMInhibited&&!ASFAActivated&&!ETCSInstalled&&!ATOActivated) UpdateHM();
            BotonesASFA();
            if(ASFAConexPressed)
            {
                if(!ASFAActivated)
                {
                    if (ActiveCCS==CCS.ETCS)
                    {
                        CurrentETCSLevel = ETCS_Level.L0;
                        CurrentETCSMode = ETCSMode.UN;
                        MA = null;
                        ISSP = null;
                    }
                    ActiveCCS = CCS.ASFA;
                    if (TipoASFA == Tipo_ASFA.Digital)
                    {
                        ASFAUrgencia = true;
                        if (ASFAModoAV && !ASFAModoCONV) ASFADigitalModo = ASFADigital_Modo.AVE;
                        else ASFADigitalModo = ASFADigital_Modo.CONV;
                        TriggerSoundInfo1();
                        ASFATiempoEncendido.Start();
                        ASFAPressed = false;
                        ASFATimesPressed = 0;
                        BotonesASFA();
                    }
                    else
                    {
                        ASFAActivated = true;
                        ASFAPressed = false;
                        ASFATimesPressed = 0;
                        BotonesASFA();
                        SetVelocidadesASFA();
                        TriggerSoundInfo1();
                    }
                }
                else if(ASFAActivated)
                {
                    if (ETCSInstalled)
                    {
                        ActiveCCS = CCS.ETCS;
                        if (ETCSInstalledLevel == 2) CurrentETCSLevel = ETCS_Level.L2;
                        else CurrentETCSLevel = ETCS_Level.L1;
                        CurrentETCSMode = ETCSMode.SB;
                    }
                    ASFAActivated = false;
                    ASFAConexPressed = false;
                    ASFAConexTimesPressed = 0;
                }
            }
            if(ASFATiempoEncendido.Triggered)
            {
                ASFATiempoEncendido.Stop();
                TriggerSoundSystemActivate();
                ASFAActivated = true;
                ASFAUrgencia = true;
                ASFAUltimaInfoRecibida = ASFAInfo.Ninguno;
                ASFAControlL1 = ASFAControlL2 = ASFAControlL3 = ASFAControlL5 = ASFAControlL6 = ASFAControlL7 = ASFAControlL8 = ASFAControlLTV = ASFAControlPNDesprotegido = ASFAControlPreanuncioLTV = false;
                ASFAControlArranque = true;
                ASFAEficacia = BalizaASFA()==ASFAFreq.FP;
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
            }
            else ASFAUrgencia = false;
            if (ETCSInstalled) UpdateETCS();
            else ETCSEmergencyBraking = false;
            if(ATOInstalled)
            {
                if (VelocidadPrefijada > MpS.FromKpH(10))
                {
                    ATOActivated = true;
                    ATOSpeed = VelocidadPrefijada;
                }
                if(CurrentETCSMode == ETCSMode.FS && ETCSVperm>ETCSVrelease)
                {
                    ATOActivated = true;
                    ATOSpeed = Math.Min(ETCSVind, ETCSVperm);
                }
                if (VelocidadPrefijada == 0 && (CurrentETCSMode != ETCSMode.FS || SpeedMpS() < 1 || ETCSVrelease>ETCSVperm))
                {
                    ATOActivated = false;
                }
                if(ATOActivated) ATO(ATOSpeed);
            }
			SetPenaltyApplicationDisplay(ASFAUrgencia || ETCSEmergencyBraking);
			SetEmergencyBrake( ASFAUrgencia || ETCSEmergencyBraking || HMEmergencyBraking );
			SetPowerAuthorization( !ASFAUrgencia && !ETCSEmergencyBraking && !HMEmergencyBraking && !ETCSServiceBrake );
            SetFullBrake(ETCSServiceBrake && !ETCSEmergencyBraking);
		}
		protected void UpdateASFABasico()
		{
			if(ASFARebasePressed&&!ASFARebaseAuto)
			{
                ASFARebasePressed = ASFAPressed = false;
                BotonesASFA();
                ASFAOverrideTimer.Setup(10f);
				ASFAOverrideTimer.Start();
				ASFARebaseAuto = true;
				SetNextSignalAspect(Aspect.StopAndProceed);
			}
			ASFARearmeFrenoPressed = ASFAPressed;
			FrecBaliza = BalizaASFA();
			if(FrecBaliza!=ASFAFreq.FP)
			{
				ASFARojoEncendido.Stop();
				switch(FrecBaliza)
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
						if(ASFATipoTren>=MpS.FromKpH(110f)) ASFAMaxSpeed = MpS.FromKpH(60f);
						else if(ASFATipoTren>=MpS.FromKpH(80f)) ASFAMaxSpeed = MpS.FromKpH(50f);
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
				SetNextSignalAspect( ASFAUltimaInfo );
			}
			if(ASFARojoEncendido.Triggered)
			{
				SetNextSignalAspect(Aspect.None);
				ASFARojoEncendido.Stop();
			}
            if (ASFAOverrideTimer.Triggered||ASFARebasePressed)
            {
                ASFARebaseAuto = false;
                ASFAOverrideTimer.Stop();
                ASFARebasePressed = ASFAPressed = false;
                BotonesASFA();
                SetNextSignalAspect(Aspect.None);
            }
			if(ASFARecTimer.Started&&!ASFARecTimer.Triggered)
			{
				TriggerSoundPenalty1();
				SetVigilanceAlarmDisplay(true);
			}
			if(ASFARecTimer.Triggered)
			{
				ASFAUrgencia = true;
				TriggerSoundPenalty2();
				ASFARecTimer.Stop();
				SetVigilanceAlarmDisplay(false);
			}
			if(ASFAPressed&&ASFARecTimer.Started)
			{
				SetVigilanceAlarmDisplay(false);
				ASFARecTimer.Stop();
				TriggerSoundPenalty2();
				SetNextSignalAspect(Aspect.None);
			}
			if(ASFAUrgencia&&ASFARearmeFrenoPressed&&SpeedMpS()<1.5f) ASFAUrgencia = false;
		}
		protected void UpdateASFA200()
		{
            if (ASFAMaxSpeed == 0) ASFAMaxSpeed = float.MaxValue;
            if (ASFARebasePressed)
            {
                ASFAOverrideTimer.Setup(10f);
                ASFAOverrideTimer.Start();
                ASFARebaseAuto = true;
                SetNextSignalAspect(Aspect.StopAndProceed);
            }
            ASFARearmeFrenoPressed = ASFAPressed;
			FrecBaliza = BalizaASFA();
			if(FrecBaliza!=ASFAFreq.FP)
			{
				ASFAMaxSpeed = float.MaxValue;
				ASFAVLParpadeo1.Stop();
				ASFAVLParpadeo2.Stop();
				ASFARojoEncendido.Stop();
				ASFACurveL2Timer.Stop();
				switch(FrecBaliza)
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
						if(ASFATipoTren>=MpS.FromKpH(110f)) ASFAMaxSpeed = MpS.FromKpH(60f);
						else if(ASFATipoTren>=MpS.FromKpH(80f)) ASFAMaxSpeed = MpS.FromKpH(50f);
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
				SetNextSignalAspect( ASFAUltimaInfo );
			}
			if(ASFACurveL2Timer.Started&&ASFACurveL2Timer.RemainingValue<11f)
			{
				if(ASFACurveL2Timer.Triggered) ASFAMaxSpeed = MpS.FromKpH(160f);
				else ASFAMaxSpeed = MpS.FromKpH(180f);
			}
			if(ASFAVLParpadeo1.Started)
			{
				if(ASFAVLParpadeo1.Triggered)
				{
					ASFAVLParpadeo1.Stop();
					ASFAVLParpadeo2.Start();
				}	
				else SetNextSignalAspect(Aspect.Clear_1);
			}
			if(ASFAVLParpadeo2.Started)
			{
				if(ASFAVLParpadeo2.Triggered)
				{
					ASFAVLParpadeo2.Stop();
					ASFAVLParpadeo1.Start();
				}	
				else SetNextSignalAspect(Aspect.None);
			}
			if(ASFARojoEncendido.Triggered)
			{
				SetNextSignalAspect(Aspect.None);
				ASFARojoEncendido.Stop();
			}
			if(ASFAOverrideTimer.Triggered) ASFARebaseAuto = false;
			if(SpeedMpS()>ASFAMaxSpeed) ASFAUrgencia = true;
			if(ASFARecTimer.Started&&!ASFARecTimer.Triggered)
			{
				SetVigilanceAlarmDisplay(true);
				TriggerSoundPenalty1();
			}
			if(ASFARecTimer.Triggered)
			{
				ASFAUrgencia = true;
				TriggerSoundPenalty2();
				ASFARecTimer.Stop();
				SetVigilanceAlarmDisplay(false);
			}
			if(ASFAPressed&&ASFARecTimer.Started)
			{
				ASFARecTimer.Stop();
				TriggerSoundPenalty2();
				SetVigilanceAlarmDisplay(false);
                SetNextSignalAspect(Aspect.None);
                ASFAMaxSpeed = float.MaxValue;
            }
			if(ASFAUrgencia&&ASFARearmeFrenoPressed&&SpeedMpS()<1.5f) ASFAUrgencia = false;
		}
		protected void UpdateASFADigital()
		{
            if (ASFAModoPressed)
			{
				if(ASFAModoAV&&ASFAModoCONV)
				{
					if(SpeedMpS()<0.1f) switch(ASFADigitalModo)
					{
						case ASFADigital_Modo.CONV:
							ASFADigitalModo=ASFADigital_Modo.AVE;
							break;
						case ASFADigital_Modo.AVE:
							ASFADigitalModo=ASFADigital_Modo.BTS;
							break;
						case ASFADigital_Modo.BTS:
							ASFADigitalModo=ASFADigital_Modo.MBRA;
							break;
						case ASFADigital_Modo.MBRA:
                            ASFADigitalModo = ASFADigital_Modo.CONV;
                            ASFAControlArranque = true;
                            break;
					}
					else if(ASFADigitalModo==ASFADigital_Modo.CONV) ASFADigitalModo=ASFADigital_Modo.AVE;
					else if(ASFADigitalModo==ASFADigital_Modo.AVE) ASFADigitalModo=ASFADigital_Modo.CONV;
				}
				if(ASFAModoAV&&!ASFAModoCONV)
				{
					if(SpeedMpS()<0.1f) switch(ASFADigitalModo)
					{
						case ASFADigital_Modo.AVE:
							ASFADigitalModo=ASFADigital_Modo.BTS;
							break;
						case ASFADigital_Modo.BTS:
							ASFADigitalModo=ASFADigital_Modo.MBRA;
							break;
						case ASFADigital_Modo.MBRA:
							ASFADigitalModo=ASFADigital_Modo.AVE;
                            ASFAControlArranque = true;
                            break;
					}
				}
				if(ASFAModoCONV&&!ASFAModoAV)
				{
					if(SpeedMpS()<0.1f) switch(ASFADigitalModo)
					{
						case ASFADigital_Modo.CONV:
							ASFADigitalModo=ASFADigital_Modo.BTS;
							break;
						case ASFADigital_Modo.BTS:
							ASFADigitalModo=ASFADigital_Modo.MBRA;
							break;
						case ASFADigital_Modo.MBRA:
							ASFADigitalModo=ASFADigital_Modo.CONV;
                            ASFAControlArranque = true;
							break;
					}
				}
                ASFAModoPressed = false;
                ASFATimesPressed = 0;
			}
			switch(ASFADigitalModo)
			{
				case ASFADigital_Modo.CONV:
					if(ASFAModoCONV) UpdateASFADigitalConv();
					else ASFADigitalModo = ASFADigital_Modo.AVE;
					break;
				case ASFADigital_Modo.AVE:
					if(ASFAModoAV) UpdateASFADigitalAVE();
					else ASFADigitalModo = ASFADigital_Modo.CONV;
					break;
				case ASFADigital_Modo.MBRA:
					ASFAVControl = MpS.FromKpH(30f);
					ASFAVIntervencion = MpS.FromKpH(35f);
					ASFAVControlFinal = ASFAVControl;
					if(SpeedMpS()>0.1f)
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
					ASFAVIntervencion = Math.Min(ASFAVIntervencion, TrainMaxSpeed+MpS.FromKpH(5f));
					ASFAVControlFinal = Math.Min(ASFAVControlFinal, TrainMaxSpeed);
					ASFAUltimaInfoRecibida = ASFAInfo.Ninguno;
					SetNextSignalAspect(ASFASenales[ASFAUltimaInfoRecibida]);
					SetNextSpeedLimitMpS(ASFAVControlFinal);
					SetInterventionSpeedLimitMpS(ASFAVIntervencion);
					break;
				case ASFADigital_Modo.EXT:
					ASFAUrgencia = false;
					return;
			}
			if(ASFAVControlFinal==Math.Min(ASFATipoTren, TrainMaxSpeed)&&!ASFAControlArranque)
			{
				SetVigilanceAlarmDisplay(false);
				SetVigilanceEmergencyDisplay(true);
			}
			else if(ASFAVControlFinal<0.1f||ASFAVControl==ASFAVControlFinal)
			{ 
				SetVigilanceAlarmDisplay(false);
				SetVigilanceEmergencyDisplay(false);
			}
			else
			{
				SetVigilanceAlarmDisplay(true);
				SetVigilanceEmergencyDisplay(false);
			}
 			if(SpeedMpS()>=ASFAVControl+0.25f*(ASFAVIntervencion-ASFAVControl)&&!ASFAUrgencia)
			{
				TriggerSoundWarning1();
				SetOverspeedWarningDisplay( true );
			}
			if(SpeedMpS()>=ASFAVIntervencion&&!ASFAUrgencia)
			{
				ASFAUrgencia = true;
				TriggerSoundSystemDeactivate();
				TriggerSoundWarning2();
			}
			if(SpeedMpS()<=ASFAVControl-MpS.FromKpH(3f)||ASFAUrgencia)
			{
				SetOverspeedWarningDisplay( false );
				TriggerSoundWarning2();
			}
			if(SpeedMpS()<MpS.FromKpH(5f)&&ASFAUrgencia&&ASFARearmeFrenoPressed) ASFAUrgencia = false;
		}
		protected void UpdateASFADigitalConv()
		{
			ASFABalizaSenal = false;
			ASFAFrecuencia = BalizaASFA();
			ASFABalizaRecibida = ASFAFrecuencia != ASFAFreq.FP;
			if(ASFABalizaRecibida)
			{
				ASFACurva.Start();
				ASFADistancia.Start();
				ASFARec.Stop();
				ASFAAnteriorFrecuencia = ASFAUltimaFrecuencia;
				ASFAUltimaFrecuencia = ASFAFrecuencia;
				switch(ASFAFrecuencia)
				{
					case ASFAFreq.L2:
						BalizaSenal();
						ASFAUltimaInfoRecibida = ASFAInfo.Via_Libre_Cond;
						if(TrainMaxSpeed>=MpS.FromKpH(160f))
						{
							TriggerSoundPenalty1();
							ASFARec.Start();
							if(!ASFAControlL2)
							{
								ASFAControlL2 = true;
								if(ASFAControlL1||ASFAControlL5||ASFAControlL6||ASFAControlL7||ASFAControlL8) ASFACurvaL2.Setup(0f);
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
						if(!ASFAControlL1&&ASFAVControl>ASFAVCFAnPar)
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
			if(ASFARec.Started)
			{
				switch(ASFAUltimaFrecuencia)
				{
					case ASFAFreq.L1:
						if(ASFARec.Triggered)
						{
							ASFAUrgencia = true;
							BalizaSenal();
							ASFAUltimaInfoRecibida = ASFAInfo.Anuncio_Parada;
							ASFAControlL1 = true;
							ASFACurvaL1.Setup(ASFACurva.RemainingValue);
							ASFACurvaL1.Start();
							ASFARec.Stop();
						}
						else if(ASFAAnParPressed)
						{
							BalizaSenal();
							ASFAUltimaInfoRecibida = ASFAInfo.Anuncio_Parada;
							ASFAControlL1 = true;
							ASFACurvaL1.Setup(ASFACurva.RemainingValue);
							ASFACurvaL1.Start();
							TriggerSoundAlert1();
							ASFARec.Stop();
						}
						else if(ASFAAnPrePressed)
						{
							BalizaSenal();
							ASFAUltimaInfoRecibida = ASFAInfo.Anuncio_Precaucion;
							ASFAControlL6 = true;
							ASFACurvaL1.Setup(ASFACurva.RemainingValue);
							ASFACurvaL1.Start();
							TriggerSoundAlert2();
							ASFARec.Stop();						
						}
						else if(ASFAPrePar_VLCondPressed)
						{
							BalizaSenal();
							ASFAUltimaInfoRecibida = ASFAInfo.Preanuncio;
							ASFAControlL5 = true;
							ASFACurvaL1.Setup(ASFACurva.RemainingValue);
							ASFACurvaL1.Start();
							//TriggerSoundPreanuncio();
							ASFARec.Stop();
						}
						else if(ASFALTVPressed)
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
						else if(ASFAPaNPressed)
						{
							ASFAControlPNDesprotegido = true;
                            ASFAControlL1 = ASFAUltimaInfoRecibida == ASFAInfo.Anuncio_Parada && ASFAControlL1;
                            ASFACurvaPN.Setup(ASFACurva.RemainingValue);
							ASFACurvaPN.Start();
							ASFADistanciaPN.Setup(ASFADistancia.RemainingValue-200f);
							ASFADistanciaPN.Start();
							//TriggerSoundPN();
							TriggerSoundInfo2();
							ASFARec.Stop();
						}
						break;
					case ASFAFreq.L2:
						if(ASFARec.Triggered)
						{
							ASFAUrgencia = true;
							ASFAControlL2 = true;
							ASFARec.Stop();
						}
						else if(ASFAPrePar_VLCondPressed)
						{
							ASFAUltimaInfoRecibida = ASFAInfo.Via_Libre_Cond;
							ASFAControlL2 = true;
							TriggerSoundAlert1();
							ASFARec.Stop();
						}
						break;
					case ASFAFreq.L3:
						if(ASFARec.Triggered)
						{
							BalizaSenal();
							ASFAUltimaInfoRecibida = ASFAInfo.Via_Libre;
							ASFAControlL3 = true;
							ASFARec.Stop();
						}
						else if(ASFAPaNPressed)
						{
							ASFAControlPNDesprotegido = false;
                            ASFADistanciaPN.Stop();
                            ASFAPNVelocidadBajada = false;
                            TriggerSoundInfo1();
							ASFARec.Stop();
						}
						break;
					case ASFAFreq.L5:
						if(ASFARec.Triggered)
						{
							BalizaSenal();
							ASFAUltimaInfoRecibida = ASFAInfo.Preanuncio;
							ASFAUrgencia = true;
							ASFAControlL5 = true;
							ASFACurvaL1.Setup(ASFACurva.RemainingValue);
							ASFACurvaL1.Start();
							ASFARec.Stop();
						}
						else if(ASFAPrePar_VLCondPressed)
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
						if(ASFARec.Triggered)
						{
							BalizaSenal();
							ASFAUltimaInfoRecibida = ASFAInfo.Anuncio_Precaucion;
							ASFAUrgencia = true;
							ASFAControlL6 = true;
							ASFACurvaL1.Setup(ASFACurva.RemainingValue);
							ASFACurvaL1.Start();
							ASFARec.Stop();
						}
						else if(ASFAAnPrePressed)
						{
							BalizaSenal();
							ASFAUltimaInfoRecibida = ASFAInfo.Anuncio_Precaucion;
							ASFAControlL6 = true;
							ASFACurvaL1.Setup(ASFACurva.RemainingValue);
							ASFACurvaL1.Start();
							TriggerSoundAlert1();
							ASFARec.Stop();
						}
						break;
					case ASFAFreq.L9:
						if(ASFARec.Triggered)
						{
							ASFAUrgencia = true;
							ASFAControlPNDesprotegido = true;
							ASFACurvaPN.Setup(ASFACurva.RemainingValue);
							ASFACurvaPN.Start();
							ASFADistanciaPN.Setup(ASFADistancia.RemainingValue-200f);
							ASFADistanciaPN.Start();
							ASFARec.Stop();
						}
						else if(ASFALTVPressed)
						{
							ASFALTVCumplida = false;
							ASFAAumentoVelocidadLTV = false;
							ASFAControlLTV = true;
							ASFACurvaLTV.Setup(ASFACurva.RemainingValue);
							ASFACurvaLTV.Start();
							TriggerSoundInfo2();
							ASFARec.Stop();
						}
						else if(ASFAPaNPressed)
						{
							ASFAControlPNDesprotegido = true;
							ASFACurvaPN.Setup(ASFACurva.RemainingValue);
							ASFACurvaPN.Start();
							ASFADistanciaPN.Setup(ASFADistancia.RemainingValue-200f);
							ASFADistanciaPN.Start();
							//TriggerSoundPN();
							ASFARec.Stop();
						}
						break;
				}
				if(!ASFARec.Started)
				{
					ASFAPressed = false;
					BotonesASFA();
				}
			}
			if(ASFARebasePressed)
			{
				ASFARebaseAuto = true;
				ASFARebasePressed = false;
				TriggerSoundPenalty2();
				ASFATiempoRebase.Start();
				SetNextSignalAspect(Aspect.StopAndProceed);
			}
			if(ASFATiempoRebase.Triggered&&ASFARebaseAuto)
			{
				ASFARebaseAuto = false;
				ASFATiempoRebase.Stop();
			}
            if ((ASFAUltimaInfoRecibida != ASFAInfo.Senal_Rojo && ASFAUltimaInfoRecibida != ASFAInfo.Rebase_Autorizado) && (ASFAAnteriorInfo == ASFAInfo.Senal_Rojo || ASFAAnteriorInfo == ASFAInfo.Rebase_Autorizado) && ASFABalizaSenal && !ASFALiberacionRojo.Started) ASFALiberacionRojo.Start();
            if (ASFAUltimaInfoRecibida == ASFAInfo.Senal_Rojo || ASFAUltimaInfoRecibida == ASFAInfo.Previa_Rojo) ASFALiberacionRojo.Stop();
            if (ASFAAnteriorInfo==ASFAInfo.Anuncio_Precaucion)
			{
				if(ASFABalizaSenal) ASFATiempoDesvio.Start();
				ASFAControlDesvio = true;
                ASFACurvaL1.Setup(0f);
                ASFACurvaL1.Start();
            }
			if(ASFAAnteriorInfo!=ASFAInfo.Anuncio_Precaucion)
			{
                if (ASFATiempoDesvio.Triggered && ASFAControlDesvio) ASFAAumentoVelocidadDesvio = false;
                ASFATiempoDesvio.Stop();
                ASFAControlDesvio = false;	
			}
			if(ASFATiempoDesvio.Triggered) ASFAControlDesvio = false;
			if(ASFABalizaSenal) ASFAControlArranque = false;
			if(ASFAUltimaInfoRecibida != ASFAInfo.Anuncio_Parada&&ASFABalizaSenal) ASFAControlL1 = false;
			if(ASFABalizaSenal) ASFAAumentoVelocidadL5 = ASFAAumentoVelocidadL6 = false;
			if(ASFAUltimaInfoRecibida!=ASFAInfo.Preanuncio_AV&&ASFAUltimaInfoRecibida!=ASFAInfo.Preanuncio&&ASFABalizaSenal)
			{
				ASFAControlL5 = false;
				ASFAAumentoVelocidadL5 = false;
			}
			if(ASFAUltimaInfoRecibida != ASFAInfo.Anuncio_Precaucion&&ASFABalizaSenal) 
			{
				ASFAControlL6 = false;
				ASFAAumentoVelocidadL6 = false;
			}
			if(ASFAUltimaInfoRecibida!=ASFAInfo.Previa_Rojo&&ASFABalizaSenal) ASFAControlL7 = false;
			if(ASFALiberacionRojo.Triggered)
			{
				ASFALiberacionRojo.Stop();
				ASFAControlL8 = false;
				ASFAAumentoVelocidadL8 = false;
			}
			if(ASFAUltimaInfoRecibida!=ASFAInfo.Via_Libre_Cond&&ASFABalizaSenal) ASFAControlL2 = false;
			if(ASFAUltimaInfoRecibida!=ASFAInfo.Via_Libre&&ASFAUltimaInfoRecibida!=ASFAInfo.Via_Libre_Cond&&ASFABalizaSenal) ASFAControlL3 = false;
			if(ASFADistanciaPN.Triggered)
			{
				ASFAControlPNDesprotegido = false;
				ASFAPNVelocidadBajada = false;
				ASFADistanciaPN.Stop();
			}
			if(ASFAControlArranque)
			{
				ASFAVControl = ASFAVCControlArranque;
				ASFAVIntervencion = ASFAVCControlArranque + MpS.FromKpH(5f);
				ASFAVControlFinal = ASFAVControl;
			}
			if(ASFAControlL3)
			{
				ASFAVControl = Math.Min(TrainMaxSpeed, ASFATipoTren);
				ASFAVIntervencion = Math.Min(TrainMaxSpeed, ASFATipoTren)+MpS.FromKpH(5f);
				ASFAVControlFinal = ASFAVControl;
			}
			if(ASFAControlL2)
			{
				ASFAVControl = Math.Max(Math.Min(ASFAVCIVlCond, ASFAVCIVlCond-ASFAVCVlCondDec*(100f-ASFAVCVlCondTReac-ASFACurvaL2.RemainingValue)), ASFAVCFVlCond);
				ASFAVIntervencion = Math.Max(Math.Min(ASFAVCIVlCond+MpS.FromKpH(3f), (ASFAVCIVlCond+MpS.FromKpH(3f))-ASFAVIVlCondDec*(100f-ASFAVIVlCondTReac-ASFACurvaL2.RemainingValue)), ASFAVCFVlCond+MpS.FromKpH(3f));
				ASFAVControlFinal = ASFAVCFVlCond;
			}
			if(ASFAControlL5)
			{
				if(ASFAAumVelPressed&&ASFACurvaL1.RemainingValue>90f)
				{
					ASFAAumentoVelocidadL5 = true;
					ASFAUltimaInfoRecibida = ASFAInfo.Preanuncio_AV;
				}
				if(ASFAAumentoVelocidadL5)
				{
					ASFAVControl = Math.Max(Math.Min(ASFAVCIAnPar, ASFAVCIAnPar-ASFAVCAnParDec*(100f-ASFAVCAnParTReac-ASFACurvaL1.RemainingValue)), ASFAVCFPreParAumVel);
					ASFAVIntervencion = Math.Max(Math.Min(ASFAVCIAnPar+MpS.FromKpH(3f), (ASFAVCIAnPar+MpS.FromKpH(3f))-ASFAVIAnParDec*(100f-ASFAVIAnParTReac-ASFACurvaL1.RemainingValue)), ASFAVCFPreParAumVel+MpS.FromKpH(3f));
					ASFAVControlFinal = ASFAVCFPreParAumVel;
				}
				else
				{
					ASFAVControl = Math.Max(Math.Min(ASFAVCIAnPar, ASFAVCIAnPar-ASFAVCAnParDec*(100f-ASFAVCAnParTReac-ASFACurvaL1.RemainingValue)), ASFAVCFPrePar);
					ASFAVIntervencion = Math.Max(Math.Min(ASFAVCIAnPar+MpS.FromKpH(3f), (ASFAVCIAnPar+MpS.FromKpH(3f))-ASFAVIAnParDec*(100f-ASFAVIAnParTReac-ASFACurvaL1.RemainingValue)), ASFAVCFPrePar+MpS.FromKpH(3f));
					ASFAVControlFinal = ASFAVCFPrePar;
				}
			}
			if(ASFAControlL6)
			{
				if(ASFAAumVelPressed&&ASFACurvaL1.RemainingValue>90f) ASFAAumentoVelocidadL6 = ASFAAumentoVelocidadDesvio = true;
				if(ASFAAumentoVelocidadL6)
				{
					ASFAVControl = Math.Max(Math.Min(ASFAVCIAnPar, ASFAVCIAnPar-ASFAVCAnParDec*(100f-ASFAVCAnParTReac-ASFACurvaL1.RemainingValue)), ASFAVCFAnPreAumVel);
					ASFAVIntervencion = Math.Max(Math.Min(ASFAVCIAnPar+MpS.FromKpH(3f), (ASFAVCIAnPar+MpS.FromKpH(3f))-ASFAVIAnParDec*(100f-ASFAVIAnParTReac-ASFACurvaL1.RemainingValue)), ASFAVCFAnPreAumVel+MpS.FromKpH(3f));
					ASFAVControlFinal = ASFAVCFAnPreAumVel;
				}
				else
				{
					ASFAVControl = Math.Max(Math.Min(ASFAVCIAnPar, ASFAVCIAnPar-ASFAVCAnParDec*(100f-ASFAVCAnParTReac-ASFACurvaL1.RemainingValue)), ASFAVCFAnPre);
					ASFAVIntervencion = Math.Max(Math.Min(ASFAVCIAnPar+MpS.FromKpH(3f), (ASFAVCIAnPar+MpS.FromKpH(3f))-ASFAVIAnParDec*(100f-ASFAVIAnParTReac-ASFACurvaL1.RemainingValue)), ASFAVCFAnPre+MpS.FromKpH(3f));
					ASFAVControlFinal = ASFAVCFAnPre;
				}
			}
			if(ASFAControlL1)
			{
				ASFAVControl = Math.Max(Math.Min(ASFAVCIAnPar, ASFAVCIAnPar-ASFAVCAnParDec*(100f-ASFAVCAnParTReac-ASFACurvaL1.RemainingValue)), ASFAVCFAnPar);
				ASFAVIntervencion = Math.Max(Math.Min(ASFAVCIAnPar+MpS.FromKpH(3f), (ASFAVCIAnPar+MpS.FromKpH(3f))-ASFAVIAnParDec*(100f-ASFAVIAnParTReac-ASFACurvaL1.RemainingValue)), ASFAVCFAnPar+MpS.FromKpH(3f));
				ASFAVControlFinal = ASFAVCFAnPar;
				if(ASFAAnteriorInfo == ASFAInfo.Anuncio_Parada&&ASFAUltimaInfoRecibida == ASFAInfo.Anuncio_Parada)
				{	
					if(ASFABalizaSenal) ASFATiempoAA.Start();
					if(ASFATiempoAA.Started&&!ASFATiempoAA.Triggered)
					{
						ASFAVControl = ASFAVControlFinal = ASFAVCDesv;
						ASFAVIntervencion = ASFAVCDesv + MpS.FromKpH(3f);
                        ASFACurvaL1.Setup(0f);
                        ASFACurvaL1.Start();
                        SetNextSignalAspect(Aspect.Approach_3);
					} 
				}
				if(ASFAAnteriorInfo == ASFAInfo.Preanuncio)
				{
					ASFAVControl = Math.Max(Math.Min(ASFAVCISecPreParA, ASFAVCISecPreParA-ASFAVCSecPreParADec*(100f-ASFAVCSecPreParATReac-ASFACurvaL1.RemainingValue)), ASFAVCFSecPreParA);
					ASFAVIntervencion = Math.Max(Math.Min(ASFAVCISecPreParA+MpS.FromKpH(3f), (ASFAVCISecPreParA+MpS.FromKpH(3f))-ASFAVCSecPreParADec*(100f-ASFAVCSecPreParATReac-ASFACurvaL1.RemainingValue)), ASFAVCFSecPreParA+MpS.FromKpH(3f));
					ASFAVControlFinal = ASFAVCFSecPreParA;
					SetNextSignalAspect(Aspect.Approach_3);
				}
				if(ASFAAnteriorInfo == ASFAInfo.Preanuncio_AV)
				{
					if(ASFANumeroBaliza==1)
					{
						ASFAVControl = Math.Max(Math.Min(ASFAVCISecPreParA1AumVel, ASFAVCISecPreParA1AumVel-ASFAVCSecPreParADec*(100f-ASFAVCSecPreParA1AumVelTReac-ASFACurvaL1.RemainingValue)), ASFAVCFSecPreParA1AumVel);
						ASFAVIntervencion = Math.Max(Math.Min(ASFAVCISecPreParA1AumVel+MpS.FromKpH(3f), (ASFAVCISecPreParA1AumVel+MpS.FromKpH(3f))-ASFAVCSecPreParADec*(100f-ASFAVCSecPreParA1AumVelTReac-ASFACurvaL1.RemainingValue)), ASFAVCFSecPreParA1AumVel+MpS.FromKpH(3f));
						ASFAVControlFinal = ASFAVCFSecPreParA1AumVel;
					}
					else
					{
						ASFAVControl = Math.Max(Math.Min(ASFAVCISecPreParA2AumVel, ASFAVCISecPreParA2AumVel-ASFAVCSecPreParADec*(100f-ASFAVCSecPreParA2AumVelTReac-ASFACurvaL1.RemainingValue)), ASFAVCFSecPreParA2AumVel);
						ASFAVIntervencion = Math.Max(Math.Min(ASFAVCISecPreParA2AumVel+MpS.FromKpH(3f), (ASFAVCISecPreParA2AumVel+MpS.FromKpH(3f))-ASFAVCSecPreParADec*(100f-ASFAVCSecPreParA2AumVelTReac-ASFACurvaL1.RemainingValue)), ASFAVCFSecPreParA2AumVel+MpS.FromKpH(3f));
						ASFAVControlFinal = ASFAVCFSecPreParA2AumVel;
					}
					SetNextSignalAspect(Aspect.Approach_3);
				}
			}
			if(ASFAControlDesvio)
			{
				if(ASFAAumentoVelocidadDesvio)
				{
					ASFAVControl = ASFAVCDesvAumVel;
					ASFAVIntervencion = ASFAVCDesvAumVel+MpS.FromKpH(3f);
					ASFAVControlFinal = ASFAVCDesvAumVel;
				}
				else
				{
					ASFAVControl = ASFAVCDesv;
					ASFAVIntervencion = ASFAVCDesv+MpS.FromKpH(3f);
					ASFAVControlFinal = ASFAVCDesv;
				}
				SetNextSignalAspect(Aspect.Restricted);
			}
			if(ASFAControlL8)
			{
				if(ASFAAumVelPressed&&ASFACurvaL8.RemainingValue>90f) ASFAAumentoVelocidadL8 = true;
				if(ASFAAumentoVelocidadL8)
				{
					ASFAVControl = ASFAVCParAumVel;
					ASFAVIntervencion = ASFAVCParAumVel+MpS.FromKpH(3);
					ASFAVControlFinal = ASFAVControl;
				}
				else
				{
					ASFAVControl = ASFAVCPar;
					ASFAVIntervencion = ASFAVCPar+MpS.FromKpH(3);
					ASFAVControlFinal = ASFAVControl;
				}
				if(ASFAUltimaInfoRecibida!=ASFAInfo.Senal_Rojo&&ASFAUltimaInfoRecibida!=ASFAInfo.Rebase_Autorizado) SetNextSignalAspect(ASFASenales[ASFAAnteriorInfo]);
				else SetNextSignalAspect(ASFASenales[ASFAUltimaInfoRecibida]);
			}
			if(ASFAControlL7)
			{
				ASFAVControl = Math.Max(Math.Min(ASFAVCIPar, ASFAVCIPar-ASFAVCParDec*(100f-ASFAVCParTReac-ASFACurvaL7.RemainingValue)), ASFAVCFPar);
				ASFAVIntervencion = Math.Max(Math.Min(ASFAVCIPar+MpS.FromKpH(3f), (ASFAVCIPar+MpS.FromKpH(3f))-ASFAVIParDec*(100f-ASFAVIParTReac-ASFACurvaL7.RemainingValue)), ASFAVCFPar+MpS.FromKpH(3f));
				ASFAVControlFinal = 0f;
				if(ASFANumeroBaliza == 2)
				{
					ASFAVControl = Math.Max(Math.Min(ASFAVCFPar, ASFAVCFPar-ASFAVCParDec*(100f-ASFAVCParTReac-ASFACurvaL7.RemainingValue)), ASFAVCFPar/2f);
					ASFAVIntervencion = Math.Max(Math.Min(ASFAVCFPar+MpS.FromKpH(3f), (ASFAVCFPar+MpS.FromKpH(3f))-ASFAVIParDec*(100f-ASFAVIParTReac-ASFACurvaL7.RemainingValue)), ASFAVCFPar/2f+MpS.FromKpH(3f));
				}
				if(ASFADistanciaPrevia.Triggered)
				{
					ASFAUrgencia = true;
					ASFAControlL7 = false;
					ASFAControlL1 = true;
					ASFACurvaL1.Setup(0f);
					ASFACurvaL1.Start();
					ASFAUltimaInfoRecibida = ASFAInfo.Anuncio_Parada;
				}
			}
			if(ASFAControlLTV)
			{
				if(ASFAAumVelPressed&&ASFACurvaLTV.RemainingValue>90f&&!ASFAAumentoVelocidadLTV)
				{
					ASFAPressed = false;
					BotonesASFA();
					ASFAAumentoVelocidadLTV = true;
				}
				if(ASFAAumentoVelocidadLTV)
				{
					ASFAVControl = Math.Min(ASFAVControl, Math.Max(Math.Min(ASFAVCIAnPar, ASFAVCIAnPar-ASFAVCAnParDec*(100f-ASFAVCAnParTReac-ASFACurvaLTV.RemainingValue)), ASFAVCFLTVAumVel));
					ASFAVIntervencion = Math.Min(ASFAVIntervencion, Math.Max(Math.Min(ASFAVCIAnPar+MpS.FromKpH(3f), (ASFAVCIAnPar+MpS.FromKpH(3f))-ASFAVIAnParDec*(100f-ASFAVIAnParTReac-ASFACurvaLTV.RemainingValue)), ASFAVCFLTVAumVel+MpS.FromKpH(3f)));
					ASFAVControlFinal = ASFAVCFLTVAumVel;
					if(SpeedMpS()<ASFAVCFLTVAumVel) ASFALTVCumplida = true;
					if(ASFALTVCumplida)
					{
						ASFAVControl = Math.Min(ASFAVControl, ASFAVCFLTVAumVel);
						ASFAVIntervencion = Math.Min(ASFAVIntervencion, ASFAVCFLTVAumVel+MpS.FromKpH(3f));
						if(ASFALTVPressed) ASFALTVCumplida = ASFAControlLTV = ASFAAumentoVelocidadLTV = false;
					}
				}
				else
				{
					ASFAVControl = Math.Min(ASFAVControl, Math.Max(Math.Min(ASFAVCIAnPar, ASFAVCIAnPar-ASFAVCAnParDec*(100f-ASFAVCAnParTReac-ASFACurvaLTV.RemainingValue)), ASFAVCFLTV));
					ASFAVIntervencion = Math.Min(ASFAVIntervencion, Math.Max(Math.Min(ASFAVCIAnPar+MpS.FromKpH(3f), (ASFAVCIAnPar+MpS.FromKpH(3f))-ASFAVIAnParDec*(100f-ASFAVIAnParTReac-ASFACurvaLTV.RemainingValue)), ASFAVCFLTV+MpS.FromKpH(3f)));
					ASFAVControlFinal = ASFAVCFLTV;
					if(SpeedMpS()<ASFAVCFLTV) ASFALTVCumplida = true;
					if(ASFALTVCumplida)
					{
						ASFAVControl = Math.Min(ASFAVControl, ASFAVCFLTV);
						ASFAVIntervencion = Math.Min(ASFAVIntervencion, ASFAVCFLTV+MpS.FromKpH(3f));
						if(ASFALTVPressed) ASFALTVCumplida = ASFAControlLTV = false;
					}
				}
			}
			if(ASFAControlPNDesprotegido)
			{
				if(SpeedMpS()<=MpS.FromKpH(30f)) ASFAPNVelocidadBajada = true;
				if(ASFAPNVelocidadBajada)
				{
					ASFAVControl = Math.Min(ASFAVControl, ASFAVCFAnPar);
					ASFAVIntervencion = Math.Min(ASFAVIntervencion, ASFAVCFAnPar+MpS.FromKpH(3f));
					ASFAVControlFinal = Math.Min(ASFAVControlFinal, ASFAVCFAnPar);
				}
				else
				{
					ASFAVControl = Math.Min(ASFAVControl, Math.Max(Math.Min(ASFAVCIAnPar, ASFAVCIAnPar-ASFAVCAnParDec*(100f-ASFAVCAnParTReac-ASFACurvaPN.RemainingValue)), MpS.FromKpH(30f)));
					ASFAVIntervencion = Math.Min(ASFAVIntervencion, Math.Max(Math.Min(ASFAVCIAnPar+MpS.FromKpH(3f), (ASFAVCIAnPar+MpS.FromKpH(3f))-ASFAVIAnParDec*(100f-ASFAVIAnParTReac-ASFACurvaPN.RemainingValue)), MpS.FromKpH(33f)));
					ASFAVControlFinal = Math.Min(ASFAVControlFinal, MpS.FromKpH(30f));
				}
			}
			ASFAVControl = Math.Min(ASFAVControl, TrainMaxSpeed);
			ASFAVIntervencion = Math.Min(ASFAVIntervencion, TrainMaxSpeed+MpS.FromKpH(5f));
			ASFAVControlFinal = Math.Min(ASFAVControlFinal, TrainMaxSpeed);
			SetCurrentSpeedLimitMpS(ASFAVControl);
			SetNextSpeedLimitMpS(ASFAVControlFinal);
			SetInterventionSpeedLimitMpS(ASFAVIntervencion);
			if(!ASFAControlDesvio&&!ASFARebaseAuto&&!ASFAControlL8&&(!ASFATiempoAA.Started||ASFATiempoAA.Triggered)) SetNextSignalAspect(ASFASenales[ASFAUltimaInfoRecibida]);
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
				switch(ASFAFrecuencia)
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
						if(ASFARebaseAuto)
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
			if(ASFARec.Started)
			{
				switch(ASFAUltimaFrecuencia)
				{
					case ASFAFreq.L1:
						if(ASFARec.Triggered)
						{
							ASFAUrgencia = true;
							BalizaSenal();
							ASFAUltimaInfoRecibida = ASFAInfo.Anuncio_Parada;
							ASFAControlL1 = true;
							ASFACurvaL1.Setup(ASFACurva.RemainingValue);
							ASFACurvaL1.Start();
							ASFARec.Stop();
						}
						else if(ASFAAnParPressed)
						{
							BalizaSenal();
							ASFAUltimaInfoRecibida = ASFAInfo.Anuncio_Parada;
							ASFAControlL1 = true;
							ASFACurvaL1.Setup(ASFACurva.RemainingValue);
							ASFACurvaL1.Start();
							TriggerSoundAlert1();
							ASFARec.Stop();
						}
						else if(ASFAAnPrePressed)
						{
							BalizaSenal();
							ASFAUltimaInfoRecibida = ASFAInfo.Anuncio_Precaucion;
							ASFAControlL6 = true;
							ASFACurvaL1.Setup(ASFACurva.RemainingValue);
							ASFACurvaL1.Start();
							TriggerSoundAlert2();
							ASFARec.Stop();						
						}
						else if(ASFAPrePar_VLCondPressed)
						{
							BalizaSenal();
							ASFAUltimaInfoRecibida = ASFAInfo.Preanuncio;
							ASFAControlL5 = true;
							ASFACurvaL1.Setup(ASFACurva.RemainingValue);
							ASFACurvaL1.Start();
							//TriggerSoundPreanuncio();
							ASFARec.Stop();
						}
						else if(ASFALTVPressed)
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
						if(ASFARec.Triggered)
						{
							BalizaSenal();
							ASFAUltimaInfoRecibida = ASFAInfo.Preanuncio;
							ASFAUrgencia = true;
							ASFAControlL5 = true;
							ASFACurvaL1.Setup(ASFACurva.RemainingValue);
							ASFACurvaL1.Start();
							ASFARec.Stop();
						}
						else if(ASFAPrePar_VLCondPressed)
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
						if(ASFARec.Triggered)
						{
							BalizaSenal();
							ASFAUltimaInfoRecibida = ASFAInfo.Anuncio_Precaucion;
							ASFAUrgencia = true;
							ASFAControlL6 = true;
							ASFACurvaL1.Setup(ASFACurva.RemainingValue);
							ASFACurvaL1.Start();
							ASFARec.Stop();
						}
						else if(ASFAAnPrePressed)
						{
							BalizaSenal();
							ASFAUltimaInfoRecibida = ASFAInfo.Anuncio_Precaucion;
							ASFAControlL6 = true;
							ASFACurvaL1.Setup(ASFACurva.RemainingValue);
							ASFACurvaL1.Start();
							TriggerSoundAlert1();
							ASFARec.Stop();
						}
						break;
					case ASFAFreq.L9:
						if(ASFARec.Triggered)
						{
							ASFAUrgencia = true;
							ASFARec.Stop();
						}
						else if(ASFALTVPressed)
						{
							TriggerSoundInfo2();
							ASFARec.Stop();
						}
						break;
				}
				if(!ASFARec.Started)
				{
					ASFAPressed = false;
					BotonesASFA();
				}
			}
			if(ASFARebasePressed)
			{
				ASFARebaseAuto = true;
				ASFARebasePressed = false;
				ASFATiempoRebase.Start();
				TriggerSoundPenalty2();
				SetNextSignalAspect(Aspect.StopAndProceed);
			}
			if(ASFATiempoRebase.Triggered&&ASFARebaseAuto)
			{
				ASFARebaseAuto = false;
				ASFATiempoRebase.Stop();
			}
            if ((ASFAUltimaInfoRecibida != ASFAInfo.Senal_Rojo && ASFAUltimaInfoRecibida != ASFAInfo.Rebase_Autorizado) && (ASFAAnteriorInfo == ASFAInfo.Senal_Rojo || ASFAAnteriorInfo == ASFAInfo.Rebase_Autorizado) && ASFABalizaSenal && !ASFALiberacionRojo.Started) ASFALiberacionRojo.Start();
            if (ASFAUltimaInfoRecibida == ASFAInfo.Senal_Rojo || ASFAUltimaInfoRecibida == ASFAInfo.Previa_Rojo) ASFALiberacionRojo.Stop();
            if (ASFAAnteriorInfo==ASFAInfo.Anuncio_Precaucion)
			{
				if(ASFABalizaSenal) ASFATiempoDesvio.Start();
				ASFAControlDesvio = true;
                ASFACurvaL1.Setup(0f);
                ASFACurvaL1.Start();
            }
			if(ASFAAnteriorInfo!=ASFAInfo.Anuncio_Precaucion)
			{
				if(ASFATiempoDesvio.Triggered&&ASFAControlDesvio) ASFAAumentoVelocidadDesvio = false;
                ASFATiempoDesvio.Stop();
				ASFAControlDesvio = false;	
			}
			if(ASFATiempoDesvio.Triggered) ASFAControlDesvio = false;
			if(ASFABalizaSenal) ASFAControlArranque = false;
			if(ASFAUltimaInfoRecibida != ASFAInfo.Anuncio_Parada&&ASFABalizaSenal) ASFAControlL1 = false;
			if(ASFABalizaSenal) ASFAAumentoVelocidadL5 = ASFAAumentoVelocidadL6 = false;
			if(ASFAUltimaInfoRecibida!=ASFAInfo.Preanuncio_AV&&ASFAUltimaInfoRecibida!=ASFAInfo.Preanuncio&&ASFABalizaSenal)
			{
				ASFAControlL5 = false;
				ASFAAumentoVelocidadL5 = false;
			}
			if(ASFAUltimaInfoRecibida != ASFAInfo.Anuncio_Precaucion&&ASFABalizaSenal) 
			{
				ASFAControlL6 = false;
				ASFAAumentoVelocidadL6 = false;
			}
			if(ASFAUltimaInfoRecibida!=ASFAInfo.Previa_Rojo&&ASFABalizaSenal) ASFAControlL7 = false;
			if(ASFALiberacionRojo.Triggered)
			{
				ASFALiberacionRojo.Stop();
				ASFAControlL8 = false;
				ASFAAumentoVelocidadL8 = false;
			}
			if(ASFAUltimaInfoRecibida!=ASFAInfo.Via_Libre&&ASFABalizaSenal) ASFAControlL3 = false;
			if(ASFAControlArranque)
			{
				ASFAVControl = ASFAVCControlArranque;
				ASFAVIntervencion = ASFAVCControlArranque + MpS.FromKpH(5f);
				ASFAVControlFinal = ASFAVControl;
			}
			if(ASFAControlL3)
			{
				ASFAVControl = Math.Min(TrainMaxSpeed, ASFATipoTren);
				ASFAVIntervencion = Math.Min(TrainMaxSpeed, ASFATipoTren)+MpS.FromKpH(5f);
				ASFAVControlFinal = ASFAVControl;
			}
			if(ASFAControlL5)
			{
				if(ASFAAumVelPressed&&ASFACurvaL1.RemainingValue>90f)
				{
					ASFAAumentoVelocidadL5 = true;
					ASFAUltimaInfoRecibida = ASFAInfo.Preanuncio_AV;
				}
				if(ASFAAumentoVelocidadL5)
				{
					ASFAVControl = Math.Max(Math.Min(ASFATipoTren, ASFATipoTren-ASFAVCAnParDec*(100f-ASFAVCAnParTReac-ASFACurvaL1.RemainingValue)), ASFAVCFPreParAumVelAV);
					ASFAVIntervencion = Math.Max(Math.Min(ASFATipoTren+MpS.FromKpH(5f), (ASFATipoTren+MpS.FromKpH(5f))-ASFAVIAnParDec*(100f-ASFAVIAnParTReac-ASFACurvaL1.RemainingValue)), ASFAVCFPreParAumVelAV+MpS.FromKpH(3f));
					ASFAVControlFinal = ASFAVCFPreParAumVelAV;
				}
				else
				{
					ASFAVControl = Math.Max(Math.Min(ASFATipoTren, ASFATipoTren-ASFAVCAnParDec*(100f-ASFAVCAnParTReac-ASFACurvaL1.RemainingValue)), ASFAVCFPreParAV);
					ASFAVIntervencion = Math.Max(Math.Min(ASFATipoTren+MpS.FromKpH(5f), (ASFATipoTren+MpS.FromKpH(5f))-ASFAVIAnParDec*(100f-ASFAVIAnParTReac-ASFACurvaL1.RemainingValue)), ASFAVCFPreParAV+MpS.FromKpH(3f));
					ASFAVControlFinal = ASFAVCFPreParAV;
				}
			}
			if(ASFAControlL6)
			{
				if(ASFAAumVelPressed&&ASFACurvaL1.RemainingValue>90f) ASFAAumentoVelocidadL6 = ASFAAumentoVelocidadDesvio = true;
				if(ASFAAumentoVelocidadL6)
				{
					ASFAVControl = Math.Max(Math.Min(ASFATipoTren, ASFATipoTren-ASFAVCAnParDec*(100f-ASFAVCAnParTReac-ASFACurvaL1.RemainingValue)), ASFAVCFAnPreAumVelAV);
					ASFAVIntervencion = Math.Max(Math.Min(ASFATipoTren+MpS.FromKpH(3f), (ASFATipoTren+MpS.FromKpH(3f))-ASFAVIAnParDec*(100f-ASFAVIAnParTReac-ASFACurvaL1.RemainingValue)), ASFAVCFAnPreAumVelAV+MpS.FromKpH(3f));
					ASFAVControlFinal = ASFAVCFAnPreAumVelAV;
				}
				else
				{
					ASFAVControl = Math.Max(Math.Min(ASFATipoTren, ASFATipoTren-ASFAVCAnParDec*(100f-ASFAVCAnParTReac-ASFACurvaL1.RemainingValue)), ASFAVCFAnPreAV);
					ASFAVIntervencion = Math.Max(Math.Min(ASFATipoTren+MpS.FromKpH(3f), (ASFATipoTren+MpS.FromKpH(3f))-ASFAVIAnParDec*(100f-ASFAVIAnParTReac-ASFACurvaL1.RemainingValue)), ASFAVCFAnPreAV+MpS.FromKpH(3f));
					ASFAVControlFinal = ASFAVCFAnPreAV;
				}
			}
			if(ASFAControlL1)
			{
				ASFAVControl = Math.Max(Math.Min(ASFATipoTren, ASFATipoTren-ASFAVCAnParDec*(100f-ASFAVCAnParTReac-ASFACurvaL1.RemainingValue)), ASFAVCFAnParAV);
				ASFAVIntervencion = Math.Max(Math.Min(ASFATipoTren+MpS.FromKpH(5f), (ASFATipoTren+MpS.FromKpH(5f))-ASFAVIAnParDec*(100f-ASFAVIAnParTReac-ASFACurvaL1.RemainingValue)), ASFAVCFAnParAV+MpS.FromKpH(3f));
				ASFAVControlFinal = ASFAVCFAnParAV;
				if(ASFAAnteriorInfo == ASFAInfo.Anuncio_Parada&&ASFAUltimaInfoRecibida == ASFAInfo.Anuncio_Parada)
				{	
					if(ASFABalizaSenal) ASFATiempoAA.Start();
					if(ASFATiempoAA.Started&&!ASFATiempoAA.Triggered)
					{
						ASFAVControl = ASFAVControlFinal = ASFAVCDesvAV;
						ASFAVIntervencion = ASFAVCDesvAV + MpS.FromKpH(3f);
                        ASFACurvaL1.Setup(0f);
                        ASFACurvaL1.Start();
                        SetNextSignalAspect(Aspect.Approach_3);
					} 
				}
				if(ASFAAnteriorInfo == ASFAInfo.Preanuncio)
				{
					ASFAVControl = Math.Max(Math.Min(ASFAVCISecPreParAAV, ASFAVCISecPreParAAV-ASFAVCSecPreParADec*(100f-ASFAVCSecPreParATReac-ASFACurvaL1.RemainingValue)), ASFAVCFSecPreParAAV);
					ASFAVIntervencion = Math.Max(Math.Min(ASFAVCISecPreParAAV+MpS.FromKpH(3f), (ASFAVCISecPreParAAV+MpS.FromKpH(3f))-ASFAVCSecPreParADec*(100f-ASFAVCSecPreParATReac-ASFACurvaL1.RemainingValue)), ASFAVCFSecPreParAAV+MpS.FromKpH(3f));
					ASFAVControl = ASFAVCFSecPreParAAV;
				}
				if(ASFAAnteriorInfo == ASFAInfo.Preanuncio_AV)
				{
					if(ASFANumeroBaliza==1)
					{
						ASFAVControl = Math.Max(Math.Min(ASFAVCISecPreParA1AumVelAV, ASFAVCISecPreParA1AumVelAV-ASFAVCSecPreParADec*(100f-ASFAVCSecPreParA1AumVelTReac-ASFACurvaL1.RemainingValue)), ASFAVCFSecPreParA1AumVelAV);
						ASFAVIntervencion = Math.Max(Math.Min(ASFAVCISecPreParA1AumVelAV+MpS.FromKpH(3f), (ASFAVCISecPreParA1AumVelAV+MpS.FromKpH(3f))-ASFAVCSecPreParADec*(100f-ASFAVCSecPreParA1AumVelTReac-ASFACurvaL1.RemainingValue)), ASFAVCFSecPreParA1AumVelAV+MpS.FromKpH(3f));
						ASFAVControl = ASFAVCFSecPreParA1AumVelAV;
					}
					else
					{
						ASFAVControl = Math.Max(Math.Min(ASFAVCISecPreParA2AumVelAV, ASFAVCISecPreParA2AumVelAV-ASFAVCSecPreParADec*(100f-ASFAVCSecPreParA2AumVelTReac-ASFACurvaL1.RemainingValue)), ASFAVCFSecPreParA2AumVelAV);
						ASFAVIntervencion = Math.Max(Math.Min(ASFAVCISecPreParA2AumVelAV+MpS.FromKpH(3f), (ASFAVCISecPreParA2AumVelAV+MpS.FromKpH(3f))-ASFAVCSecPreParADec*(100f-ASFAVCSecPreParA2AumVelTReac-ASFACurvaL1.RemainingValue)), ASFAVCFSecPreParA2AumVelAV+MpS.FromKpH(3f));
						ASFAVControl = ASFAVCFSecPreParA2AumVelAV;
					}
				}
			}
			if(ASFAControlDesvio)
			{
				if(ASFAAumentoVelocidadDesvio)
				{
					ASFAVControl = ASFAVCDesvAumVelAV;
					ASFAVIntervencion = ASFAVCDesvAumVelAV+MpS.FromKpH(3f);
					ASFAVControlFinal = ASFAVCDesvAumVelAV;
				}
				else
				{
					ASFAVControl = ASFAVCDesvAV;
					ASFAVIntervencion = ASFAVCDesvAV+MpS.FromKpH(3f);
					ASFAVControlFinal = ASFAVCDesvAV;
				}
				SetNextSignalAspect(Aspect.Restricted);
			}
			if(ASFAControlL8)
			{
				if(ASFAAumVelPressed&&ASFACurvaL8.RemainingValue>90f) ASFAAumentoVelocidadL8 = true;
				if(ASFAAumentoVelocidadL8)
				{
					ASFAVControl = ASFAVCParAumVel;
					ASFAVIntervencion = ASFAVCParAumVel+MpS.FromKpH(3);
					ASFAVControlFinal = ASFAVControl;
				}
				else
				{
					ASFAVControl = ASFAVCPar;
					ASFAVIntervencion = ASFAVCPar+MpS.FromKpH(3);
					ASFAVControlFinal = ASFAVControl;
				}
				if(ASFAUltimaInfoRecibida!=ASFAInfo.Senal_Rojo&&ASFAUltimaInfoRecibida!=ASFAInfo.Rebase_Autorizado) SetNextSignalAspect(ASFASenales[ASFAAnteriorInfo]);
				else SetNextSignalAspect(ASFASenales[ASFAUltimaInfoRecibida]);
			}
			if(ASFAControlL7)
			{
				ASFAVControl = Math.Max(Math.Min(ASFAVCIPar, ASFAVCIPar-ASFAVCParDec*(100f-ASFAVCParTReac-ASFACurvaL7.RemainingValue)), ASFAVCFPar);
				ASFAVIntervencion = Math.Max(Math.Min(ASFAVCIPar+MpS.FromKpH(3f), (ASFAVCIPar+MpS.FromKpH(3f))-ASFAVIParDec*(100f-ASFAVIParTReac-ASFACurvaL7.RemainingValue)), ASFAVCFPar+MpS.FromKpH(3f));
				ASFAVControlFinal = 0f;
				if(ASFANumeroBaliza == 2)
				{
					ASFAVControl = Math.Max(Math.Min(ASFAVCFPar, ASFAVCFPar-ASFAVCParDec*(100f-ASFAVCParTReac-ASFACurvaL7.RemainingValue)), ASFAVCFPar/2f);
					ASFAVIntervencion = Math.Max(Math.Min(ASFAVCFPar+MpS.FromKpH(3f), (ASFAVCFPar+MpS.FromKpH(3f))-ASFAVIParDec*(100f-ASFAVIParTReac-ASFACurvaL7.RemainingValue)), ASFAVCFPar/2f+MpS.FromKpH(3f));
				}
				if(ASFADistanciaPrevia.Triggered)
				{
					ASFAUrgencia = true;
					ASFAControlL7 = false;
					ASFAControlL1 = true;
					ASFACurvaL1.Setup(0f);
					ASFACurvaL1.Start();
					ASFAUltimaInfoRecibida = ASFAInfo.Anuncio_Parada;
				}
			}
			if(ASFAControlLTV)
			{
				if(ASFAAumVelPressed&&ASFACurvaLTV.RemainingValue>90f&&!ASFAAumentoVelocidadLTV)
				{
					ASFAPressed = false;
					BotonesASFA();
					ASFAAumentoVelocidadLTV = true;
				}
				if(ASFAAumentoVelocidadLTV)
				{
					ASFAVControl = Math.Min(ASFAVControl, Math.Max(Math.Min(ASFATipoTren, ASFATipoTren-ASFAVCAnParDec*(100f-ASFAVCAnParTReac-ASFACurvaLTV.RemainingValue)), ASFAVCFLTVAumVelAV));
					ASFAVIntervencion = Math.Min(ASFAVIntervencion, Math.Max(Math.Min(ASFATipoTren+MpS.FromKpH(5f), (ASFATipoTren+MpS.FromKpH(5f))-ASFAVIAnParDec*(100f-ASFAVIAnParTReac-ASFACurvaLTV.RemainingValue)), ASFAVCFLTVAumVelAV+MpS.FromKpH(3f)));
					ASFAVControlFinal = ASFAVCFLTVAumVelAV;
					if(SpeedMpS()<ASFAVCFLTVAumVel) ASFALTVCumplida = true;
					if(ASFALTVCumplida)
					{
						ASFAVControl = Math.Min(ASFAVControl, ASFAVCFLTVAumVelAV);
						ASFAVIntervencion = Math.Min(ASFAVIntervencion, ASFAVCFLTVAumVelAV+MpS.FromKpH(3f));
						if(ASFALTVPressed) ASFALTVCumplida = ASFAControlLTV = ASFAAumentoVelocidadLTV = false;
					}
				}
				else
				{
					ASFAVControl = Math.Min(ASFAVControl, Math.Max(Math.Min(ASFATipoTren, ASFATipoTren-ASFAVCAnParDec*(100f-ASFAVCAnParTReac-ASFACurvaLTV.RemainingValue)), ASFAVCFLTVAV));
					ASFAVIntervencion = Math.Min(ASFAVIntervencion, Math.Max(Math.Min(ASFATipoTren+MpS.FromKpH(5f), (ASFATipoTren+MpS.FromKpH(5f))-ASFAVIAnParDec*(100f-ASFAVIAnParTReac-ASFACurvaLTV.RemainingValue)), ASFAVCFLTVAV+MpS.FromKpH(3f)));
					ASFAVControlFinal = ASFAVCFLTVAV;
					if(SpeedMpS()<ASFAVCFLTV) ASFALTVCumplida = true;
					if(ASFALTVCumplida)
					{
						ASFAVControl = Math.Min(ASFAVControl, ASFAVCFLTVAV);
						ASFAVIntervencion = Math.Min(ASFAVIntervencion, ASFAVCFLTVAV+MpS.FromKpH(3f));
						if(ASFALTVPressed) ASFALTVCumplida = ASFAControlLTV = false;
					}
				}
			}
			ASFAVControl = Math.Min(ASFAVControl, TrainMaxSpeed);
			ASFAVIntervencion = Math.Min(ASFAVIntervencion, TrainMaxSpeed+MpS.FromKpH(5f));
			ASFAVControlFinal = Math.Min(ASFAVControlFinal, TrainMaxSpeed);
			SetCurrentSpeedLimitMpS(ASFAVControl);
			SetNextSpeedLimitMpS(ASFAVControlFinal);
			SetInterventionSpeedLimitMpS(ASFAVIntervencion);
			if(!ASFAControlDesvio&&!ASFARebaseAuto&&!ASFAControlL8&&(!ASFATiempoAA.Started||ASFATiempoAA.Triggered)) SetNextSignalAspect(ASFASenales[ASFAUltimaInfoRecibida]);
		}
		bool IsLTV;
		bool IsPN;
		protected void BotonesASFA()
		{
			if(AnuncioLTVPassed) IsLTV = true;
			if(PreviaPassed||SignalPassed) IsLTV = false;
			if(PreviaPassed) IsPN = false;
			if(ASFAPressed&&!ASFAPressedTimer.Started)
			{ 
				ASFATimesPressed++;
                ASFAConexTimesPressed++;
				ASFAButtonsTimer.Stop();
			}
			if((ASFATimesPressed!=0||ASFAConexTimesPressed!=0)&&!ASFAPressed&&!ASFAButtonsTimer.Started) ASFAButtonsTimer.Start();
			if(ASFAButtonsTimer.Triggered) ASFATimesPressed = ASFAConexTimesPressed = 0;
			ASFAModoPressed = ASFAPressed&&ASFATimesPressed==3&&ASFAActivated;
            ASFAConexPressed = ASFAPressed&&ASFAConexTimesPressed==4;
			if(ASFAPressed&&!ASFAPressedTimer.Started)
			{
				ASFAPressedTimer.Start();
			}
			if(!ASFAPressed)
			{
				if(ASFAPressedTimer.Started) ASFAPressedTimer.Stop();
				ASFAAnParPressed = false;
				ASFAAnPrePressed = false;
				ASFAPrePar_VLCondPressed = false;
				ASFARebasePressed = false;
				ASFALTVPressed = false;
				ASFAPaNPressed = ASFAAumVelPressed = ASFARearmeFrenoPressed = false;
			}
			if(ASFAPressedTimer.Started && ASFAPressedTimer.RemainingValue<2.5f)
			{
				ASFAAnParPressed = (BalizaAspect == Aspect.Approach_1 || BalizaAspect == Aspect.Approach_3)&&ASFARec.Started&&ASFAUltimaFrecuencia == ASFAFreq.L1&&!IsLTV;
				ASFAAnPrePressed = BalizaAspect == Aspect.Approach_2&&ASFARec.Started&&!IsLTV;
				ASFAPrePar_VLCondPressed = ((BalizaAspect == Aspect.Clear_1&&ASFARec.Started))&&!IsLTV;
				ASFALTVPressed = (ASFAUltimaFrecuencia == ASFAFreq.L9&&ASFARec.Started)||(ASFAControlLTV&&ASFALTVCumplida&&!ASFARec.Started)||(ASFAUltimaFrecuencia == ASFAFreq.L1 && IntermediateLTVDist && ASFARec.Started && IsLTV);
				ASFAPaNPressed = (ASFAUltimaFrecuencia == ASFAFreq.L3&&ASFARec.Started)||(BalizaAspect == Aspect.Approach_1&&CurrentSignalSpeedLimitMpS()>MpS.FromKpH(25f)&&CurrentSignalSpeedLimitMpS()<MpS.FromKpH(35f)&&ASFARec.Started&&IsPN);
				if(ASFAPaNPressed) ASFAAnParPressed = false;
				ASFAAumVelPressed = ASFARearmeFrenoPressed = true;
			}
			if(ASFAPressedTimer.Triggered)
			{
				ASFAAnParPressed = false;
				ASFAAnPrePressed = false;
				ASFAPrePar_VLCondPressed = false;
				ASFAPaNPressed = ASFAAumVelPressed = ASFARearmeFrenoPressed = false;
				ASFARebasePressed = true;
				ASFAPressedTimer.Stop();
			}
			ASFAOcultacionPressed = false;
			if(ASFALTVPressed) IsLTV = false;
		}
		ASFAFreq BalizaASFA()
		{
            if(IsDirectionReverse())
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
			if(ASFADistanciaPrevia.Triggered)
			{
				ASFANumeroBaliza = 0;
				ASFAAnteriorInfo = ASFAUltimaInfoRecibida;
			}
			ASFANumeroBaliza++;
			if((ASFANumeroBaliza==2&&ASFAUltimaFrecuencia!=ASFAFreq.L7)||ASFAUltimaFrecuencia==ASFAFreq.L8)
			{
				if(ASFADistanciaPrevia.RemainingValue<370f)
				{
					ASFADistanciaPrevia.Setup(0f);
					ASFADistanciaPrevia.Start();
				}
			}
			if(ASFANumeroBaliza==1||(ASFAUltimaFrecuencia==ASFAFreq.L7&&ASFADistanciaPrevia.RemainingValue<370f))
			{
				ASFANumeroBaliza = 1;
				if(ASFADigitalModo==ASFADigital_Modo.AVE) ASFADistanciaPrevia.Setup(ASFADistancia.RemainingValue-1400f);
				else ASFADistanciaPrevia.Setup(ASFADistancia.RemainingValue-1550f);
				ASFADistanciaPrevia.Start();
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
            else if (ETCSPreviousAspect != Aspect.Stop && ETCSPreviousAspect != Aspect.StopAndProceed && ETCSPreviousAspect != Aspect.Restricted && (ETCSAspect != NextSignalAspect(0) || ((int)ClockTime()%10==0)))
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
        protected EurobaliseTelegram Eurobalise()
		{
            L1MA MA = null;
            ModeProfile MP = null ;
            int N_ITER = 0;
            int[] L_SECTION;
            int[] Q_SECTIONTIMER;
            int[] T_SECTIONTIMER;
            int[] D_SECTIONTIMERSTOPLOC;
            int NumPost;
            int iter;
            int Q_SCALE = 1;
            int V_MAIN = 60;
            if (SignalPassed)
            {
                ETCSPreviousAspect = ETCSAspect;
                switch (ETCSAspect)
                {
                    case Aspect.Approach_1:
                    case Aspect.Approach_3:
                        if (IsPN && CurrentSignalSpeedLimitMpS() < MpS.FromKpH(35f) && CurrentSignalSpeedLimitMpS() > MpS.FromKpH(25f))
                        {
                            return new EurobaliseTelegram(new TemporarySpeedRestriction(0, 0, 1, 1, 490, 20, 1, 2));
                        }
                        N_ITER = 1;
                        break;
                    case Aspect.Clear_1:
                    case Aspect.Approach_2:
                        N_ITER = 2;
                        break;
                    case Aspect.Clear_2:
                        if (CurrentPostSpeedLimitMpS() < MpS.FromKpH(165) && CurrentPostSpeedLimitMpS() > MpS.FromKpH(155) && IsPN)
                        {
                            return new EurobaliseTelegram(/*new PNStatus*/);
                        }
                        if (NextSignalAspect(0) == Aspect.Clear_1) N_ITER = 3;
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
                    if ((NextSignalAspect(i) == Aspect.Approach_1 && NextSignalSpeedLimitMpS(i) < MpS.FromKpH(35f) && NextSignalSpeedLimitMpS(i) > MpS.FromKpH(25f))||(NextSignalAspect(i)==Aspect.Clear_2&&NextSignalSpeedLimitMpS(i).AlmostEqual(MpS.FromKpH(160), 2))) N_ITER++;
                }
                MAEnd = NextSignalDistanceM(N_ITER - 1);
                if (ETCSAspect == Aspect.StopAndProceed||ETCSAspect == Aspect.Permission)
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
                MA = new L1MA(0, 0, Q_SCALE, V_MAIN, 0, 0, N_ITER, L_SECTION, Q_SECTIONTIMER, T_SECTIONTIMER, D_SECTIONTIMERSTOPLOC, 15, 0, 0, 0, 1, 10, 6, 0, 0, 0, 0, 0);
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
                for (iter = 0; iter < NumPost-1; iter++)
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
                if (MP != null) return new EurobaliseTelegram(MA, Speed, MP);
                return new EurobaliseTelegram(MA, Speed);
            }
            else if (PreviaPassed)
            {
                switch (NextSignalAspect(PreviaSignalNumber))
                {
                    case Aspect.Approach_1:
                    case Aspect.Approach_3:
                        N_ITER = 2;
                        break;
                    case Aspect.Clear_1:
                    case Aspect.Approach_2:
                        N_ITER = 3;
                        break;
                    case Aspect.Clear_2:
                        if (NextSignalAspect(0) == Aspect.Clear_1) N_ITER = 4;
                        else N_ITER = 3;
                        break;
                    case Aspect.StopAndProceed:
                    case Aspect.Permission:
                        N_ITER = 1;
                        break;
                    case Aspect.Restricted:
                        N_ITER = 1;
                        break;
                    case Aspect.Stop:
                    default:
                        N_ITER = 1;
                        break;
                }
                for (int i = 0; i < N_ITER; i++)
                {
                    if ((NextSignalAspect(i) == Aspect.Approach_1 && NextSignalSpeedLimitMpS(i) < MpS.FromKpH(35f) && NextSignalSpeedLimitMpS(i) > MpS.FromKpH(25f)) || (NextSignalAspect(i) == Aspect.Clear_2 && NextSignalSpeedLimitMpS(i).AlmostEqual(MpS.FromKpH(160), 2))) N_ITER++;
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
                return new EurobaliseTelegram(new L1MA(0, 0, Q_SCALE, V_MAIN, 0, 0, N_ITER, L_SECTION, Q_SECTIONTIMER, T_SECTIONTIMER, D_SECTIONTIMERSTOPLOC, 15, 0, 0, 0, 1, 10, 6, 0, 0, 0, 0, 0));
            }
            else
            {
                ETCSAspect = NextSignalAspect(0);
                return null;
            }
		}
		ETCSMode CurrentETCSMode=ETCSMode.SB;
		EurobaliseTelegram EBD;
        TrackToTrainEuroradioMessage ERM;
        TrainToTrackEuroradioMessage TRBCM;
        List<OnBoardTemporarySpeedRestriction> TSRs = new List<OnBoardTemporarySpeedRestriction>();
        TemporarySpeedRestrictionRevocation TSRR;
        OnBoardSR SR;
        OnBoardMA MA;
        OnBoardModeProfile MP;
        StaticSpeedProfile ISSP;
        bool OverrideEoA;
        bool AcknowledgeEoA;
        protected void UpdateETCS()
		{
            ETCSServiceBrake = false;
            EBD = Eurobalise();
            switch (CurrentETCSLevel)
            {
                case ETCS_Level.L0:
                    CurrentETCSMode = ETCSMode.UN;
                    break;
                case ETCS_Level.L1:
                    if(ASFAActivated)
                    {
                        if (TipoASFA == Tipo_ASFA.Digital) ASFADigitalModo = ASFADigital_Modo.EXT;
                        else ASFAActivated = false;
                        ASFAUrgencia = false;
                    }
                    if (EBD != null) foreach (Packet pck in EBD.packet)
                    {
                        if (pck != null) switch (pck.NID_PACKET)
                        {
                            case 12:
                                if (((L1MA)pck).V_MAIN == 0)
                                {
                                    if (!OverrideEoA)
                                    {
                                        CurrentETCSMode = ETCSMode.TR;
                                        SetNextSignalAspect(Aspect.Stop);
                                        TriggerSoundPenalty1();
                                    }        
                                    MA = null;
                                }
                                else MA = new OnBoardMA((L1MA)pck, this);
                                break;
                            case 27:
                                ISSP = new StaticSpeedProfile((InternationalStaticSpeedProfile)pck, this);
                                break;
                            case 65:
                                TSRs.Add(new OnBoardTemporarySpeedRestriction((TemporarySpeedRestriction)pck, this));
                                break;
                            case 66:
                                TSRR = (TemporarySpeedRestrictionRevocation)pck;
                                foreach(OnBoardTemporarySpeedRestriction tsr in TSRs)
                                {
                                     if (tsr.NID_TSR == TSRR.NID_TSR)
                                     {
                                          TSRs.Remove(tsr);
                                     }
                                }
                                break;
                             case 80:
                                MP = new OnBoardModeProfile((ModeProfile)pck, this);
                                break;
                        }
                    }
                    if(CurrentETCSMode == ETCSMode.SB)
                    {
                        CurrentETCSMode = ETCSMode.SR;
                        SR = new OnBoardSR(10000, MpS.FromKpH(100), this);
                    }
                    if(MA!=null&&(MP==null||!MP.L_MAMODE[0].Started)&ISSP!=null&&CurrentETCSMode!=ETCSMode.FS)
                    {
                        TriggerSoundInfo1();
                        CurrentETCSMode = ETCSMode.FS;
                        FrontEndTrain.Setup(TrainLengthM());
                        FrontEndTrain.Start();
                        SetNextSignalAspect(Aspect.Clear_1);
                    }
                    if(CurrentETCSMode==ETCSMode.TR)
                    {
                        if(ASFAPressed)
                        {
                            SetNextSignalAspect(Aspect.Clear_2);
                            TriggerSoundInfo2();
                        }
                    }
                    if(ASFARebasePressed&&SpeedMpS()<0.1f)
                    {
                        TriggerSoundInfo2();
                        SetNextSignalAspect(Aspect.Approach_3);
                        AcknowledgeEoA = true;
                        ASFAPressed = false;
                        BotonesASFA();
                    }
                    break;
                case ETCS_Level.L2:
                case ETCS_Level.L3:
                    ASFAUrgencia = false;
                    ERM = RBC(TRBCM);
                    if (ERM != null)
                    {
                        switch(ERM.NID_MESSAGE)
                        {
                            case 3:
                                MA = new OnBoardMA(((RadioMA)ERM).MA, this);
                                if (((RadioMA)ERM).MA.V_MAIN == 0)
                                {
                                    if(!OverrideEoA)
                                    {
                                        CurrentETCSMode = ETCSMode.TR;
                                        SetNextSignalAspect(Aspect.Stop);
                                        TriggerSoundPenalty1();
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
                                                ISSP = new StaticSpeedProfile((InternationalStaticSpeedProfile)pck, this);
                                                break;
                                            case 80:
                                                MP = new OnBoardModeProfile((ModeProfile)pck, this);
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
                                    SetNextSignalAspect(Aspect.Stop);
                                    TriggerSoundPenalty1();
                                }
                                break;
                        }
                    }
                    if (EBD != null) foreach (Packet pck in EBD.packet)
                        {
                            if (pck != null) switch (pck.NID_PACKET)
                                {
                                    case 65:
                                        TSRs.Add(new OnBoardTemporarySpeedRestriction((TemporarySpeedRestriction)pck, this));
                                        break;
                                    case 66:
                                        TSRR = (TemporarySpeedRestrictionRevocation)pck;
                                        foreach (OnBoardTemporarySpeedRestriction tsr in TSRs)
                                        {
                                            if (tsr.NID_TSR == TSRR.NID_TSR)
                                            {
                                                TSRs.Remove(tsr);
                                            }
                                        }
                                        break;
                                }
                        }
                    if (CurrentETCSMode == ETCSMode.SB)
                    {
                        CurrentETCSMode = ETCSMode.SR;
                        SR = new OnBoardSR(10000, MpS.FromKpH(100), this);
                    }
                    if (MA != null && (MP == null || !MP.L_MAMODE[0].Started) && ISSP != null && CurrentETCSMode != ETCSMode.FS)
                    {
                        TriggerSoundInfo1();
                        CurrentETCSMode = ETCSMode.FS;
                        FrontEndTrain.Setup(TrainLengthM());
                        FrontEndTrain.Start();
                        SetNextSignalAspect(Aspect.Clear_1);
                    }
                    if (CurrentETCSMode == ETCSMode.TR)
                    {
                        if (ASFAPressed)
                        {
                            CurrentETCSMode = ETCSMode.PT;
                            SetNextSignalAspect(Aspect.Clear_2);
                            TriggerSoundInfo2();
                        }
                    }
                    if (ASFARebasePressed && SpeedMpS() < 0.1f)
                    {
                        TriggerSoundInfo2();
                        SetNextSignalAspect(Aspect.Approach_3);
                        AcknowledgeEoA = true;
                        ASFAPressed = false;
                        BotonesASFA();
                    }
                    break;
                case ETCS_Level.NTC:
                    break;
            }
            if(FrontEndTrain.Triggered)
            {
                FrontEndTrain.Stop();
                SetNextSignalAspect(Aspect.Clear_2);
            }
            if (AcknowledgeEoA&&ASFAPressed)
            {
                TriggerSoundInfo2();
                CurrentETCSMode = ETCSMode.SR;
                SR = new OnBoardSR(2000, MpS.FromKpH(100), this);
                AcknowledgeEoA = false;
                OverrideEoA = true;
                MA = null;
                ISSP = null;
                MP = null;
                SetNextSignalAspect(Aspect.Clear_2);
            }
            if (OverrideEoA && CurrentETCSMode != ETCSMode.SR) OverrideEoA = false;
            UpdateControls();
            ETCSCurves();
		}
		bool S1Played;
        bool SInfoPlayed;
		bool ETCSEmergencyBraking;
		bool ETCSServiceBrake;
		float ETCSDec=0.7f;
		float ETCSSBDec=1f;
		float ETCSEBDec=1.2f;
		float ETCSVtarget;
		float ETCSVind;
		float ETCSVperm;
		float ETCSVwarn;
		float ETCSVsbi;
		float ETCSVebi;
        float ETCSVrelease;
        float ETCSVlast;
		MonitoringStatus Monitoring;
        protected void UpdateControls()
        {
            if(CurrentETCSMode == ETCSMode.SR &&(SR==null||SR.SRMaxDist.Triggered))
            {
                SR = null;
                CurrentETCSMode = ETCSMode.TR;
                SetNextSignalAspect(Aspect.Stop);
                TriggerSoundPenalty1();
            }
            if (MA != null)
            {
                MA.Update();
                for(int i = 0; i<MA.N_ITER; i++)
                {
                    if (MA.Q_SECTIONTIMER[i] && MA.T_SECTIONTIMER[i].Triggered)
                    {
                        MA = null;
                        CurrentETCSMode = ETCSMode.TR;
                        SetNextSignalAspect(Aspect.Stop);
                        TriggerSoundPenalty1();
                    }
                }
            }
            if (MP != null)
            {
                MP.Update();
                if (MP.N_ITER < 1 || MP.L_MAMODE[MP.N_ITER - 1].Triggered) MP = null;
                else for (int i = 0; i<MP.N_ITER; i++)
                {
                    if(MP.L_ACKMAMODE[i].Started)
                    {
                        CurrentETCSMode = MP.M_MAMODE[i];
                        switch (MP.M_MAMODE[i])
                        {
                            case ETCSMode.OS:
                                SetNextSignalAspect(Aspect.StopAndProceed);
                                break;
                            case ETCSMode.SH:
                                SetNextSignalAspect(Aspect.Restricted);
                                break;
                            case ETCSMode.LS:
                            default:
                                break;
                        }
                        if (MP.L_ACKMAMODE[i].Triggered) ETCSServiceBrake = true;
                        if (ASFAPressed)
                        {
                            SetNextSignalAspect(Aspect.Clear_2);
                            MP.L_ACKMAMODE[i].Stop();
                            TriggerSoundInfo2();
                        }
                    }
                }
            }
            if (ISSP != null)
            {
                for (int i = 1; i < ISSP.N_ITER; i++)
                {
                    //if ((int)(MpS.ToKpH(ISSP.V_STATIC[i]) / 5) == 127 && ISSP.D_STATIC[i - 1].Triggered) ISSP = null;
                }
            }
            var ETSRs = new List<OnBoardTemporarySpeedRestriction>();
            foreach (var tsr in TSRs)
            {
                tsr.Update();
                if (tsr.L_TSR.Triggered) ETSRs.Add(tsr);
            }
            foreach (var etsr in ETSRs)
            {
                TSRs.Remove(etsr);
            }
        }
        bool ETCSServiceBrakeApplied;
        protected void ETCSCurves()
        {
            ETCSVlast = ETCSVperm;
            ETCSVperm = ETCSVsbi = ETCSVebi = ETCSVtarget = ETCSVind = float.MaxValue;
            ETCSVperm = TrainMaxSpeed;
            ETCSVsbi = TrainMaxSpeed + 1;
            ETCSVebi = TrainMaxSpeed + 2;
            if(CurrentETCSMode == ETCSMode.SR)
            {
                ETCSVperm = Math.Min(ETCSVperm, SR.SRSpeed);
                ETCSVsbi = Math.Min(ETCSVsbi, SR.SRSpeed + 1);
                ETCSVebi = Math.Min(ETCSVebi, SR.SRSpeed + 2);
            }
            if(MP != null&&MP.L_MAMODE[0].Started)
            {
                ETCSVperm = Math.Min(ETCSVperm, MP.V_MAMODE[0]);
                ETCSVsbi = Math.Min(ETCSVsbi, MP.V_MAMODE[0] + 1);
                ETCSVebi = Math.Min(ETCSVebi, MP.V_MAMODE[0] + 2);
            }
            if (MA != null && (CurrentETCSMode == ETCSMode.FS || CurrentETCSMode == ETCSMode.OS))
            {
                float EoADist = 0;
                for (int i = 0; i < MA.L_SECTION.Length; i++)
                {
                    if (!MA.L_SECTION[i].Triggered)
                    {
                        if (MA.L_SECTION[i].Started) EoADist = EoADist + MA.L_SECTION[i].RemainingValue;
                        else EoADist = EoADist + MA.L_SECTION[i].AlarmValue;
                    }
                }
                if (EoADist > 50) ETCSVperm = Math.Min(ETCSVperm, Math.Min(MA.V_MAIN, Math.Max(SpeedCurve(EoADist - 50, MA.V_LOA, 0f, 0f, ETCSDec), MA.V_LOA)));
                else ETCSVperm = Math.Min(ETCSVperm, MA.V_LOA);
                if (EoADist > 25) ETCSVsbi = Math.Min(ETCSVsbi, Math.Min(MA.V_MAIN + 1, Math.Max(SpeedCurve(EoADist - 25, MA.V_LOA + 2, 0f, 0f, ETCSSBDec), MA.V_LOA + 1)));
                else ETCSVsbi = Math.Min(ETCSVsbi, MA.V_LOA + 1);
                if (EoADist > 0) ETCSVebi = Math.Min(ETCSVebi, Math.Min(MA.V_MAIN + 2, Math.Max(SpeedCurve(EoADist, MA.V_LOA + 2, 0f, 0f, ETCSEBDec), MA.V_LOA + 2)));
                else ETCSVebi = Math.Min(ETCSVebi, MA.V_LOA + 2);
                if (DistanceCurve(ETCSVlast, MA.V_LOA, 0, 15f, ETCSDec) > EoADist)
                {
                    ETCSVtarget = Math.Min(ETCSVtarget, MA.V_LOA);
                    ETCSVind = Math.Min(ETCSVind, SpeedCurve(EoADist, MA.V_LOA, 0, 10f, ETCSDec));
                    if (MA.Q_DANGERPOINT) ETCSVrelease = MA.V_RELEASEDP;
                }
            }
            if (ISSP != null && (CurrentETCSMode == ETCSMode.FS || CurrentETCSMode == ETCSMode.OS))
            {
                for (int i = 0; i < ISSP.D_STATIC.Length - 1; i++)
                {
                    if (!ISSP.D_STATIC[i].Triggered)
                    {
                        if (i == 0 || ISSP.D_STATIC[i - 1].Triggered)
                        {
                            ETCSVperm = Math.Min(ETCSVperm, ISSP.V_STATIC[i]);
                            ETCSVsbi = Math.Min(ETCSVsbi, ISSP.V_STATIC[i] + 1);
                            ETCSVebi = Math.Min(ETCSVebi, ISSP.V_STATIC[i] + 2);
                        }
                        else
                        {
                            ETCSVperm = Math.Min(ETCSVperm, Math.Max(SpeedCurve(ISSP.D_STATIC[i - 1].RemainingValue-50, ISSP.V_STATIC[i], 0f, 0f, ETCSDec), ISSP.V_STATIC[i]));
                            ETCSVsbi = Math.Min(ETCSVsbi, Math.Max(SpeedCurve(ISSP.D_STATIC[i - 1].RemainingValue-25, ISSP.V_STATIC[i]+1, 0f, 0f, ETCSDec), ISSP.V_STATIC[i]+1));
                            ETCSVebi = Math.Min(ETCSVebi, Math.Max(SpeedCurve(ISSP.D_STATIC[i - 1].RemainingValue, ISSP.V_STATIC[i]+2, 0f, 0f, ETCSDec), ISSP.V_STATIC[i]+2));
                            if (DistanceCurve(ETCSVlast, ISSP.V_STATIC[i], 0, 15f, ETCSDec) > ISSP.D_STATIC[i-1].RemainingValue)
                            {
                                ETCSVtarget = Math.Min(ETCSVtarget, ISSP.V_STATIC[i]);
                                ETCSVind = Math.Min(ETCSVind, SpeedCurve(ISSP.D_STATIC[i - 1].RemainingValue - 50, ISSP.V_STATIC[i], 0, 10f, ETCSDec));
                            }
                        }
                    }
                }
            }
            if(CurrentETCSLevel != ETCS_Level.L0)
            {
                foreach(var tsr in TSRs)
                {
                    if(tsr.D_TSR.Started && !tsr.D_TSR.Triggered)
                    {
                        if (tsr.D_TSR.RemainingValue > 50) ETCSVperm = Math.Min(ETCSVperm, Math.Max(tsr.V_TSR, SpeedCurve(tsr.D_TSR.RemainingValue - 50, tsr.V_TSR, 0f, 0f, ETCSDec)));
                        else ETCSVperm = Math.Min(ETCSVperm, tsr.V_TSR);
                        if (tsr.D_TSR.RemainingValue > 25) ETCSVsbi = Math.Min(ETCSVsbi, Math.Max(tsr.V_TSR + 1, SpeedCurve(tsr.D_TSR.RemainingValue - 25, tsr.V_TSR + 2, 0f, 0f, ETCSDec)));
                        else ETCSVsbi = Math.Min(ETCSVsbi, tsr.V_TSR + 1);
                        ETCSVebi = Math.Min(ETCSVebi, Math.Max(tsr.V_TSR + 2, SpeedCurve(tsr.D_TSR.RemainingValue, tsr.V_TSR + 1, 0f, 0f, ETCSDec)));
                        if (DistanceCurve(ETCSVlast, tsr.V_TSR, 0, 15f, ETCSDec) > tsr.D_TSR.RemainingValue)
                        {
                            ETCSVtarget = Math.Min(ETCSVtarget, tsr.V_TSR);
                            ETCSVind = Math.Min(ETCSVind, SpeedCurve(tsr.D_TSR.RemainingValue - 50, tsr.V_TSR, 0f, 10f, ETCSDec));
                        }
                    }
                    else
                    {
                        ETCSVperm = Math.Min(ETCSVperm, tsr.V_TSR);
                        ETCSVsbi = Math.Min(ETCSVsbi, tsr.V_TSR + 1);
                        ETCSVebi = Math.Min(ETCSVebi, tsr.V_TSR + 2);
                    }
                }
            }
            if(CurrentETCSMode==ETCSMode.TR)
            {
                ETCSVperm = ETCSVsbi = ETCSVebi = ETCSVtarget = 0;
                ETCSEmergencyBraking = true;
                SetCurrentSpeedLimitMpS(0);
                return;
            }
            ETCSVtarget = Math.Min(ETCSVtarget, ETCSVperm);
            ETCSVind = Math.Min(ETCSVind, ETCSVperm);
            ETCSVwarn = ETCSVperm + 0.5f * (ETCSVsbi - ETCSVperm);
            if (ETCSVtarget < ETCSVrelease)
            {
                ETCSVsbi = Math.Max(ETCSVsbi, ETCSVrelease + 1);
                ETCSVebi = Math.Max(ETCSVebi, ETCSVrelease + 2);
            }
            if (ETCSVperm < ETCSVrelease)
            {
                SetNextSpeedLimitMpS(ETCSVperm);
                SetCurrentSpeedLimitMpS(ETCSVrelease);
                SetInterventionSpeedLimitMpS(ETCSVsbi);
                //SetReleaseSpeedMpS(ETCSVrelease);
                ETCSVind = 0;
                ETCSVsbi = Math.Max(ETCSVsbi, ETCSVrelease + 1);
                ETCSVebi = Math.Max(ETCSVebi, ETCSVrelease + 2);
                ETCSVwarn = ETCSVrelease;
            }
            else
            {
                if (ActiveCCS == CCS.ETCS)
                {
                    SetNextSpeedLimitMpS(ETCSVtarget);
                }
                SetCurrentSpeedLimitMpS(Math.Max(ETCSVperm, ETCSVrelease));
                SetInterventionSpeedLimitMpS(ETCSVsbi);
                //SetReleaseSpeedMpS(ETCSVrelease);
            }
            if (ActiveCCS == CCS.ETCS) SetVigilanceEmergencyDisplay(true);
            //if (ETCSVperm > ETCSVlast + 1) TriggerSoundInfo1();
            if (ETCSVtarget<ETCSVperm-1||ETCSVperm<ETCSVrelease)
			{
                if ( Monitoring == MonitoringStatus.Normal)
                {
                    TriggerSoundInfo1();
                    Monitoring = MonitoringStatus.Indication;
                }
                if (Monitoring == MonitoringStatus.Overspeed && SpeedMpS() < ETCSVtarget) Monitoring = MonitoringStatus.Indication;
                if (SpeedMpS() > ETCSVind) Monitoring = MonitoringStatus.Overspeed;
            }
			else Monitoring=MonitoringStatus.Normal;
            SetMonitoringStatus(Monitoring);
            if (SpeedMpS() > Math.Max(ETCSVperm, ETCSVrelease))
            {
                if (Monitoring == MonitoringStatus.Overspeed && !S1Played)
                {
                    TriggerSoundAlert1();
                    S1Played = true;
                }
                SetMonitoringStatus(MonitoringStatus.Warning);
                SetOverspeedWarningDisplay(true);
            }
            else
            {
                S1Played = false;
                SetOverspeedWarningDisplay(false);
            }
            if (SpeedMpS() > ETCSVwarn)
            {
                TriggerSoundWarning1();
            }
            if (SpeedMpS() < Math.Max(ETCSVperm, ETCSVrelease))
            {
                ETCSServiceBrakeApplied = false;
                TriggerSoundWarning2();
            }
            if (SpeedMpS() > ETCSVsbi)
			{
                if (!ETCSServiceBrakeApplied) TriggerSoundInfo1();
                ETCSServiceBrakeApplied = true;
                TriggerSoundWarning2();
                SetMonitoringStatus(MonitoringStatus.Intervention);
			}
            ETCSServiceBrake |= ETCSServiceBrakeApplied;
            if (SpeedMpS() > ETCSVebi)
            {
                if (!ETCSEmergencyBraking) TriggerSoundInfo1();
                ETCSEmergencyBraking = true;
            }
            if (SpeedMpS() < 0.5f&&ETCSEmergencyBraking) ETCSEmergencyBraking = false;
            if (ETCSEmergencyBraking) TriggerSoundWarning2();
		}
        public class OnBoardSR
        {
            public OdoMeter SRMaxDist;
            public float SRSpeed;
            public OnBoardSR(float Dist, float Speed, TrainControlSystem tcs)
            {
                SRMaxDist = new OdoMeter(tcs);
                SRMaxDist.Setup(Dist);
                SRMaxDist.Start();
                SRSpeed = Speed;
            }
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
                    D_STATIC[i].Setup((float)(SSP.D_STATIC[i] * Math.Pow(10, Q_SCALE - 1) + tcs.TrainLengthM() * Math.Abs(SSP.Q_FRONT[i] - 1)));
                    D_STATIC[i].Start();
                    V_STATIC[i] = MpS.FromKpH(SSP.V_STATIC[i] * 5);
                    Q_FRONT[i] = Convert.ToBoolean(SSP.Q_FRONT[i]);
                }
            }
        }
        public class OnBoardModeProfile
        {
            public int Q_DIR;
            public int L_PACKET;
            public int Q_SCALE;
            public int N_ITER;
            public OdoMeter[] D_MAMODE;
            public ETCSMode[] M_MAMODE;
            public float[] V_MAMODE;
            public OdoMeter[] L_MAMODE;
            public OdoMeter[] L_ACKMAMODE;
            public int[] Q_MAMODE;
            public OnBoardModeProfile(ModeProfile MP, AbstractScriptClass asc)
            {
                Q_DIR = MP.Q_DIR;
                L_PACKET = MP.L_PACKET;
                Q_SCALE = MP.Q_SCALE;
                N_ITER = MP.N_ITER;
                D_MAMODE = new OdoMeter[N_ITER];
                M_MAMODE = new ETCSMode[N_ITER];
                V_MAMODE = new float[N_ITER];
                L_MAMODE = new OdoMeter[N_ITER];
                L_ACKMAMODE = new OdoMeter[N_ITER];
                Q_MAMODE = new int[N_ITER];
                for (int i = 0; i < N_ITER; i++)
                {
                    D_MAMODE[i] = new OdoMeter(asc);
                    D_MAMODE[i].Setup((float)(MP.D_MAMODE[i] * Math.Pow(10, Q_SCALE - 1)));
                    switch (MP.M_MAMODE[i])
                    {
                        case 0:
                            M_MAMODE[i] = ETCSMode.OS;
                            break;
                        case 1:
                            M_MAMODE[i] = ETCSMode.SH;
                            break;
                        case 2:
                            M_MAMODE[i] = ETCSMode.LS;
                            break;
                    }
                    V_MAMODE[i] = MpS.FromKpH(MP.V_MAMODE[i] * 5);
                    L_MAMODE[i] = new OdoMeter(asc);
                    L_MAMODE[i].Setup((float)(MP.L_MAMODE[i] * Math.Pow(10, Q_SCALE - 1)));
                    L_ACKMAMODE[i] = new OdoMeter(asc);
                    L_ACKMAMODE[i].Setup((float)(MP.L_ACKMAMODE[i] * Math.Pow(10, Q_SCALE - 1)));
                    Q_MAMODE[i] = MP.Q_MAMODE[i];
                }
            }
            public void Update()
            {
                for (int i = 0; i < N_ITER; i++)
                {
                    if (!D_MAMODE[i].Started) D_MAMODE[i].Start();
                    if (!L_MAMODE[i].Started)
                    {
                        L_MAMODE[i].Start();
                        L_ACKMAMODE[i].Start();
                    }
                }
                if (L_MAMODE[0].Triggered)
                {
                    var l_mamode = new OdoMeter[N_ITER - 1];
                    var d_mamode = new OdoMeter[N_ITER - 1];
                    N_ITER--;
                    for(int i=0; i<N_ITER; i++)
                    {
                        l_mamode[i] = L_MAMODE[i + 1];
                        d_mamode[i] = D_MAMODE[i + 1];
                    }
                }
            }
        }
        public class OnBoardTemporarySpeedRestriction
        {
            public int Q_DIR;
            public int L_PACKET;
            public int Q_SCALE;
            public int NID_TSR;
            public OdoMeter D_TSR;
            public OdoMeter L_TSR;
            public bool Q_FRONT;
            public float V_TSR;
            public OnBoardTemporarySpeedRestriction(TemporarySpeedRestriction tsr, TrainControlSystem tcs)
            {
                Q_DIR = tsr.Q_DIR;
                L_PACKET = tsr.L_PACKET;
                Q_SCALE = tsr.Q_SCALE;
                NID_TSR = tsr.NID_TSR;
                D_TSR = new OdoMeter(tcs);
                D_TSR.Setup((float)(tsr.D_TSR * Math.Pow(10, Q_SCALE - 1)));
                D_TSR.Start();
                L_TSR = new OdoMeter(tcs);
                L_TSR.Setup((float)(tsr.L_TSR * Math.Pow(10, Q_SCALE - 1) + tcs.TrainLengthM() * Math.Abs(tsr.Q_FRONT - 1)));
                Q_FRONT = Convert.ToBoolean(tsr.Q_FRONT);
                V_TSR = MpS.FromKpH(tsr.V_TSR * 5);
            }
            public void Update()
            {
                if (D_TSR.Triggered)
                {
                    L_TSR.Start();
                    D_TSR.Stop();
                }
            }
        }
        protected void ATO(float limit)
		{
            limit = limit - MpS.FromKpH(1);
            ATOAcceleration = (SpeedMpS() - LastMpS) / (ClockTime() - LastTime);
            float diff = limit - (SpeedMpS() + ATOAcceleration * (ClockTime() - LastTime));
            if (diff < 0)
            {
                if (ATOThrottle > 0)
                {
                    ATOThrottle = Math.Max(ATOThrottle - 0.1f, 0);
                }
                else
                {
                    ATOBrake = Math.Min(ATOBrake + 0.1f, 1);
                }
            }
            if (diff > 0)
            {
                if (ATOBrake > 0)
                {
                    ATOBrake = Math.Max(ATOBrake - 0.1f, 0);
                }
                else
                {
                    ATOThrottle = Math.Min(ATOThrottle + 0.1f, 1);
                }
            }
            SetThrottleController(ATOThrottle);
			SetDynamicBrakeController(ATOBrake);
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
                    break;
                case TCSEvent.AlerterReleased:
                    HMPressed = false;
					ASFAPressed = false;
                    break;
            }		
		}
		protected void UpdateHM()
        {
            if (!Activated || !IsAlerterEnabled())
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
				if(ASFADigitalModo==ASFADigital_Modo.AVE)
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
		public EurobaliseTelegram(params Packet[] packets)
		{
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
}