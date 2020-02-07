#include <avr/sleep.h>
const int PConex = 3;
const int PREC = 11;
const int PAlarma = 7;
const int PRearme = 5;
const int PRebase = 2;
const int Zumbador = 9;
const int LuzFrenar = A0;
const int LuzL2 = A1;
const int LuzRojo = A2;
const int LuzVL = A3;
const int LuzCV = A4;
const int LuzEficacia = 13;
const int LuzREC = 12;
const int LuzAlarma = 6;
const int LuzRearme = 4;
const int LuzRebase = 8;
const int SelTipoTren = A5;
void setup() {
  // put your setup code here, to run once:
  Serial.begin(115200);
  pinMode(PConex, INPUT_PULLUP);
  pinMode(PRebase, INPUT_PULLUP);
  pinMode(PREC, INPUT_PULLUP);
  pinMode(PAlarma, INPUT_PULLUP);
  pinMode(PRearme, INPUT_PULLUP);
  pinMode(LuzREC, OUTPUT);
  pinMode(LuzFrenar, OUTPUT);
  pinMode(LuzRojo, OUTPUT);
  pinMode(LuzAlarma, OUTPUT);
  pinMode(LuzEficacia, OUTPUT);
  pinMode(LuzRearme, OUTPUT);
  pinMode(LuzL2, OUTPUT);
  pinMode(LuzCV, OUTPUT);
  pinMode(LuzVL, OUTPUT);
  pinMode(Zumbador, OUTPUT);
  digitalWrite(LuzREC, HIGH);
  digitalWrite(LuzFrenar, HIGH);
  digitalWrite(LuzRojo, HIGH);
  digitalWrite(LuzAlarma, HIGH);
  digitalWrite(LuzEficacia, HIGH);
  digitalWrite(LuzL2, HIGH);
  digitalWrite(LuzRebase, HIGH);
  digitalWrite(LuzRearme, HIGH);
  digitalWrite(LuzVL, HIGH);
  digitalWrite(LuzCV, HIGH);
  digitalWrite(LuzREC, HIGH);
  delay(3000);
  digitalWrite(LuzFrenar, LOW);
  digitalWrite(LuzRojo, LOW);
  digitalWrite(LuzAlarma, LOW);
  digitalWrite(LuzEficacia, LOW);
  digitalWrite(LuzL2, LOW);
  digitalWrite(LuzRebase, LOW);
  digitalWrite(LuzRearme, LOW);
  digitalWrite(LuzVL, LOW);
  digitalWrite(LuzCV, LOW);
  digitalWrite(LuzREC, LOW);
  if(digitalRead(PConex)==LOW) start();
  else shutdown();
}
enum Frequency
{
  AL = -1,
  FP = 0,
  L1 = 1,
  L2 = 2,
  L3 = 3,
  L4 = 4,
  L5 = 5,
  L6 = 6,
  L7 = 7,
  L8 = 8,
  L9 = 9
} freq = FP, prev_freq = FP;
bool Encendido = false;
bool Urgencia = true;
bool RebaseAuto = false;
bool Eficacia = false;
bool ASFA200 = true;
bool RecL2;
bool Connected = false;
unsigned TipoTren;
unsigned long RECStarted = 0;
unsigned long RojoStarted = 0;
unsigned long AlarmaStarted = 0;
unsigned long RebaseStarted = 0;
unsigned long CondStarted = 0;
byte Velocidad = 0;
unsigned long Previous;
unsigned long LastPConex;
unsigned long BuzzEnd = 0;
unsigned long poweroff = 0;
void buzz(unsigned long time)
{
  digitalWrite(Zumbador, HIGH);
  //tone(Zumbador, 435, time);
  BuzzEnd = millis() + time;
}
void nobuzz()
{
  digitalWrite(Zumbador,LOW);
  //noTone(Zumbador);
}
void shutdown()
{
  freq = FP;
  RECStarted = RojoStarted = AlarmaStarted = RebaseStarted = CondStarted = 0;
  Urgencia = true;
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
  Serial.print("asfa_emergency=1\n");
  sleep_enable();
  set_sleep_mode(SLEEP_MODE_PWR_DOWN);
  attachInterrupt(digitalPinToInterrupt(PConex), start, LOW);
  delay(1000);
  sleep_cpu();
}
void start()
{
  sleep_disable();
  detachInterrupt(digitalPinToInterrupt(PConex));
  Urgencia = false;
  Encendido = true;
  buzz(500);
  LastPConex = millis();
}
byte data[30];
int windex=0;
int reindex=0;
void readSerial()
{
  //if(LastPConex + 5000 < millis()&&!AlarmaStarted) freq = AL;
  if(reindex>=windex)
  {
    windex = reindex = 0;
  }
  if(reindex>20)
  {
    for(int i=reindex; i<windex; i++)
    {
      data[i-reindex] = data[i];
    }
    windex = windex-reindex;
    reindex = 0;
  }
  while(Serial.available() && windex<30)
  {
    data[windex] = Serial.read();
    Connected = true;
    windex++;
  }
  for(int i=reindex; i<windex; i++)
  {
    if(data[i]=='\n')
    {
      char line[i-reindex+1];
      for(int j=reindex; j<i; j++)
      {
        line[j-reindex] = data[j];
      }
      line[i-reindex] = 0;
      if(!strncmp(line, "asfa_baliza", 11))
      {
        if(line[13]=='P') freq = FP;
        else freq = (Frequency)atoi(line+13);
      }
      else if(!strncmp(line, "speed", 5))
      {
        Velocidad = atoi(line+6);
      }
      else if(!strncmp(line, "connected", 9))
      {
        Serial.println("register(speed)");
        Serial.println("register(asfa_baliza)");
        Connected = true;
      }
      reindex = i+1;
      break;
    }
    else if(i+1==30) windex = reindex = 0;
  }
}
void loop() {
  // put your main code here, to run repeatedly:
  if(digitalRead(PConex)==HIGH&&digitalRead(PRebase)==HIGH)
  {
    if(Encendido && poweroff == 0) poweroff = millis();
  }
  else poweroff = 0;
  if(poweroff != 0 && poweroff+2000<millis()) shutdown();
  if(Encendido)
  {
    if(!RebaseStarted&&digitalRead(PRebase)==LOW)
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
    readSerial();
    if(BuzzEnd!=0 && BuzzEnd<millis())
    {
      BuzzEnd = 0;
      nobuzz();
    }
    if(prev_freq!=freq)
    {
      if(ASFA200 && freq != FP) CondStarted = RecL2 = 0;
      switch(freq)
      {
        case L1:
          buzz(3000);
          RECStarted = millis();
          break;
        case L2:
          if(ASFA200)
          {
            buzz(3000);
            CondStarted = millis();
            digitalWrite(LuzREC, HIGH);
          }
          else buzz(500);
          break;
        case L3:
          buzz(500);
          break;
        case L7:
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
        case L8:
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
        case FP:
          break;
        default:
          Eficacia = false;
          if(!AlarmaStarted)
          {
            buzz(3000);
            AlarmaStarted = millis();
            digitalWrite(LuzAlarma, HIGH);
          }
          break;
      }
      prev_freq = freq;
    }
    Eficacia = freq==FP;  
    digitalWrite(LuzEficacia, Eficacia);
    if(AlarmaStarted)
    {
      if(digitalRead(PAlarma)==LOW&&Eficacia)
      {
        nobuzz();
        AlarmaStarted = 0;
        digitalWrite(LuzAlarma, LOW);
      }
      else if(AlarmaStarted+3000<millis()) Urgencia = true;
    }
    if(RECStarted)
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
    if(RojoStarted)
    {
      digitalWrite(LuzRojo, HIGH);
      if(RojoStarted+10000<millis())
      {
        digitalWrite(LuzRojo, LOW);
        RojoStarted = 0;
      }
    }
    if(CondStarted)
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
      digitalWrite(LuzL2, (millis() - CondStarted) / 500 % 2);
    }
    else digitalWrite(LuzL2, LOW);
    if(Urgencia&&Velocidad<5)
    { 
      digitalWrite(LuzRojo, LOW);
      if(!AlarmaStarted)
      {
        digitalWrite(LuzRearme, HIGH);
        if(digitalRead(PRearme)==LOW) Urgencia = false;
      }
    }
    else digitalWrite(LuzRearme, LOW);
  }
  if(Previous + 500 < millis()&&Serial.availableForWrite()>10)
  {
    if(!Connected)
    {
      Serial.println("register(speed)");
      Serial.println("register(asfa_baliza)");
    }
    else
    {
      Serial.print("asfa_emergency=");
      Serial.write(Urgencia ? '1' : '0');
      Serial.print("\n");
    }
    Previous = millis();
  }
}
