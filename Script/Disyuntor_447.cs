using System;
using System.Collections.Generic;
using System.IO.Ports;
using ORTS.Common;
using ORTS.Scripting.Api;
using Orts.Simulation;

namespace ORTS.Scripting.Script
{
    public class Disyuntor_447 : CircuitBreaker
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
            if (!TCSClosingAuthorization() || CurrentPantographState()!=PantographState.Up) SetCurrentState(CircuitBreakerState.Open);
            if (PantoPrevState != PantographState.Up && CurrentPantographState() == PantographState.Up) SetCurrentState(CircuitBreakerState.Closing);
            if (DriverClosingAuthorization())
            {
                SetDriverClosingAuthorization(false);
                SetCurrentState(CircuitBreakerState.Open);
            }
            if (DriverClosingOrder())
            {
                if (CurrentState() == CircuitBreakerState.Open && CurrentPantographState() == PantographState.Up) SetCurrentState(CircuitBreakerState.Closing);
            }
            if (CurrentState() == CircuitBreakerState.Closing)
            {
                if (!ClosingTimer.Started)
                {
                    ClosingTimer.Start();
                }
                if (ClosingTimer.Triggered)
                {
                    ClosingTimer.Stop();
                    SetCurrentState(CircuitBreakerState.Closed);
                }
            }
            PantoPrevState = CurrentPantographState();
        }
        public override void HandleEvent(PowerSupplyEvent evt)
        {
            switch (evt)
            {
                case PowerSupplyEvent.CloseCircuitBreakerButtonPressed:
                    SetDriverClosingOrder(true);
                    if (CurrentState() == CircuitBreakerState.Open) Message(ConfirmLevel.None, "Disyuntor cerrado");
                    break;
                case PowerSupplyEvent.CloseCircuitBreakerButtonReleased:
                    SetDriverClosingOrder(false);
                    break;
                case PowerSupplyEvent.GiveCircuitBreakerClosingAuthorization:
                    SetDriverClosingAuthorization(true);
                    if(CurrentState()==CircuitBreakerState.Closed) Message(ConfirmLevel.None, "Disyuntor abierto");
                    break;
                case PowerSupplyEvent.RemoveCircuitBreakerClosingAuthorization:
                    SetDriverClosingAuthorization(false);
                    break;
            }
        }
    }
}