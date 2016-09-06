const int LlaveEncendido = 2;
const int LlaveRebase = 3;
const int REC = 4;
const int RearmeFreno = 5;
const int Alarma = 6;
const int Zumbador = 10;
const int LuzEficacia = 13;
const int LuzFrenar = A1;
const int LuzREC = 11;
const int LuzRojo = 12;
const int LuzAlarma = A2;
const int LuzL2 = A3;
const int SelTipoTren = A0;
void setup() {
  // put your setup code here, to run once:
  Serial.begin(9600);
  pinMode(LlaveEncendido, INPUT);
  pinMode(LlaveRebase, INPUT);
  pinMode(REC, INPUT);
  pinMode(Alarma, INPUT);
  pinMode(RearmeFreno, INPUT);
  pinMode(LuzREC, OUTPUT);
  pinMode(LuzFrenar, OUTPUT);
  pinMode(LuzRojo, OUTPUT);
  pinMode(LuzAlarma, OUTPUT);
  pinMode(LuzEficacia, OUTPUT);
  pinMode(LuzL2, OUTPUT);
  pinMode(Zumbador, OUTPUT);
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
} freq = FP;
bool Encendido = false;
bool Urgencia = true;
bool Rebase = false;
bool Eficacia = false;
bool ASFA200 = true;
bool RecL2;
unsigned TipoTren;
unsigned long RECStarted = 0;
unsigned long RojoStarted = 0;
unsigned long AlarmaStarted = 0;
unsigned long RebaseStarted = 0;
unsigned long CondStarted = 0;
byte Velocidad = 0;
unsigned long Previous;
unsigned long LastConex;
void loop() {
  // put your main code here, to run repeatedly:
  if(!Encendido&&(digitalRead(LlaveEncendido)==HIGH||digitalRead(LlaveRebase)==HIGH))
  {
    Urgencia = false;
    Encendido = true;
    LastConex = millis();
  }
  if(Encendido&&digitalRead(LlaveEncendido)==LOW&&digitalRead(LlaveRebase)==LOW)
  {
    noTone(Zumbador);
    freq = FP;
    Urgencia = true;
    Eficacia = false;
    digitalWrite(LuzREC, LOW);
    digitalWrite(LuzFrenar, LOW);
    digitalWrite(LuzRojo, LOW);
    digitalWrite(LuzAlarma, LOW);
    digitalWrite(LuzEficacia, LOW);
    digitalWrite(LuzL2, LOW);
    Encendido = false;
    Serial.print(Urgencia ? 1 : 0);
    Serial.println("ASFA");
  }
  if(Encendido)
  {
    if(!RebaseStarted&&digitalRead(LlaveRebase)==HIGH)
    {
      RebaseStarted = millis();
      Rebase = true;
    }
    if(digitalRead(LlaveRebase)==LOW) RebaseStarted = 0;
    if(RebaseStarted+10000<millis()) Rebase = false;
    byte info[8] = {0, 0, 0, 0, 0, 0, 0, 0};
    if(LastConex + 5000 < millis()&&!AlarmaStarted) freq = AL;
    if(freq!=AL) freq = FP;
    if(Serial.available()>=8)
    {
      if(Serial.peek()>='0'&&Serial.peek()<='9') Serial.readBytes(info, 8);
      if(info[7]!='\n')
      {
        while((Serial.read()!='\n'&&Serial.peek()<='0'&&Serial.peek()>='9')||Serial.available());
      }
      else if(info[2]=='A')
      {
        freq = (Frequency)(info[0]-48);
        Velocidad = info[1];
        LastConex = millis();
      }
    }
    if(ASFA200 && freq != FP) CondStarted = RecL2 = 0;
    int Vmax = 60;
    if(TipoTren == 110) Vmax = 60;
    if(TipoTren == 90) Vmax = 50;
    if(TipoTren == 70) Vmax = 35;
    switch(freq)
    {
      case L1:
        tone(Zumbador, 659, 3000);
        RECStarted = millis();
        break;
      case L2:
        if(ASFA200)
        {
          tone(Zumbador, 659, 3000);
          CondStarted = millis();
          digitalWrite(LuzREC, HIGH);
        }
        else tone(Zumbador, 659, 500);
        break;
      case L3:
        tone(Zumbador, 659, 500);
        break;
      case L7:
        if(Velocidad>Vmax)
        {
          Urgencia = true;
          tone(Zumbador, 659, 5000);
          digitalWrite(LuzRojo, HIGH);
        }
        else
        {
          tone(Zumbador, 659, 3000);
          RojoStarted = millis();
        }
        break;
      case L8:
        if(!Rebase)
        {
          Urgencia = true;
          tone(Zumbador, 659, 5000);
          digitalWrite(LuzRojo, HIGH);
        }
        else
        {
          tone(Zumbador, 659, 3000);
          RojoStarted = millis();
        }
        break;
      case FP:
        Eficacia = true;
        break;
      default:
        Eficacia = false;
        if(!AlarmaStarted)
        {
          tone(Zumbador, 659, 3000);
          AlarmaStarted = millis();
        }
        digitalWrite(LuzAlarma, HIGH);
        break;
    }
    digitalWrite(LuzEficacia, Eficacia ? HIGH : LOW);
    if(AlarmaStarted)
    {
      if(digitalRead(Alarma)==HIGH&&Eficacia)
      {
        noTone(Zumbador);
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
      if(digitalRead(REC)==HIGH)
      {
        noTone(Zumbador);
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
      if(digitalRead(REC)==HIGH && !RecL2)
      {
        noTone(Zumbador);
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
      if(digitalRead(RearmeFreno)==HIGH&&!AlarmaStarted) Urgencia = false;
    }
    if(Previous + 500 < millis()&&Serial.availableForWrite()>10)
    {
      Serial.print(Urgencia);
      Serial.println("ASFA");
      Previous = millis();
    }
  }
}
