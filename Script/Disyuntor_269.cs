using System;
using System.Collections.Generic;
using System.IO.Ports;
using ORTS.Common;
using ORTS.Scripting.Api;
using Orts.Simulation;

namespace ORTS.Scripting.Script
{
    public class Disyuntor_269 : CircuitBreaker
    {
        private Timer ClosingTimer;

        public override void Initialize()
        {
            ClosingTimer = new Timer(this);
            ClosingTimer.Setup(ClosingDelayS());
        }
        PantographState PantoPrevState;
        public override void Update(float elapsedSeconds)
        {
            SetClosingAuthorization(TCSClosingAuthorization() && DriverClosingAuthorization() && CurrentPantographState() == PantographState.Up);
            switch(CurrentState())
            {
                case CircuitBreakerState.Open:
                    if(ClosingAuthorization() && DriverClosingOrder())
                    {
                        SetCurrentState(CircuitBreakerState.Closing);
                    }
                    break;
                case CircuitBreakerState.Closing:
                    if(ClosingAuthorization() && DriverClosingOrder())
                    {
                        if (!ClosingTimer.Started) ClosingTimer.Start();
                        if (ClosingTimer.Triggered) SetCurrentState(CircuitBreakerState.Closed);
                    }
                    else
                    {
                        ClosingTimer.Stop();
                        SetCurrentState(CircuitBreakerState.Open);
                    }
                    break;
                case CircuitBreakerState.Closed:
                    if (!ClosingAuthorization()) SetCurrentState(CircuitBreakerState.Open);
                    break;
            }
        }
        public override void HandleEvent(PowerSupplyEvent evt)
        {
            switch (evt)
            {
                case PowerSupplyEvent.CloseCircuitBreakerButtonPressed:
                    SetDriverClosingOrder(true);
                    break;
                case PowerSupplyEvent.CloseCircuitBreakerButtonReleased:
                    SetDriverClosingOrder(false);
                    break;
                case PowerSupplyEvent.GiveCircuitBreakerClosingAuthorization:
                    SetDriverClosingAuthorization(true);
                    Confirm(CabControl.CircuitBreakerClosingAuthorization, CabSetting.On);
                    break;
                case PowerSupplyEvent.RemoveCircuitBreakerClosingAuthorization:
                    SetDriverClosingAuthorization(false);
                    Confirm(CabControl.CircuitBreakerClosingAuthorization, CabSetting.Off);
                    break;
            }
        }
    }
}